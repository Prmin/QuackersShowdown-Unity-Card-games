using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance { get; private set; }

    // Authoritative turn order (netId list) replicated to clients.
    public readonly SyncList<uint> TurnOrder = new SyncList<uint>();

    [Header("Turn Timer")]
    [SerializeField] private float turnDurationSeconds = 30f;

    [SyncVar(hook = nameof(OnCurrentTurnIndexChanged))]
    public int currentTurnIndex = -1;

    [SyncVar(hook = nameof(OnCurrentTurnNetIdChanged))]
    public uint currentTurnNetId = 0;

    [SyncVar] public int currentTurnRemainingSeconds = 0;

    private bool _turnClockArmed;
    private double _turnDeadlineServerTime = -1d;
    private int _lastLoggedRemainingSecond = -1;
    private bool _currentTurnCardPlayed;
    private bool _currentTurnSkillDeclared;
    private bool _waitingForTurnFinishConditions;
    private double _nextPendingFinishLogAt;
    private bool _sawNonNoneSkillSinceCardPlayed;
    private SkillMode _lastObservedSkillMode = SkillMode.None;

    private static readonly string[] DuckKeysByIndex =
    {
        "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    };

    public static string DuckKeyFromIndex(int idx)
    {
        return (idx >= 0 && idx < DuckKeysByIndex.Length) ? DuckKeysByIndex[idx] : "-";
    }

    private static string DuckKeyFromCardName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
            return null;

        string name = cardName.Replace("(Clone)", string.Empty).Trim();
        if (name.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Marsh";

        foreach (string key in DuckKeysByIndex)
        {
            if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                return key;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ServerCallback]
    private void Update()
    {
        if (!_turnClockArmed) return;
        if (TurnOrder.Count <= 0 || currentTurnIndex < 0 || currentTurnIndex >= TurnOrder.Count)
        {
            ServerStopTurnTimer();
            return;
        }

        if (_waitingForTurnFinishConditions)
        {
            ServerUpdateWaitingForTurnFinish();
            return;
        }

        ServerTrackSkillStateAndLog(currentTurnRemainingSeconds);

        if (ServerTryAdvanceResolved("CardAndSkillResolved", currentTurnRemainingSeconds))
            return;

        if (_turnDeadlineServerTime <= 0d)
            return;

        double remain = _turnDeadlineServerTime - NetworkTime.time;
        int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remain));

        if (currentTurnRemainingSeconds != remainingSeconds)
            currentTurnRemainingSeconds = remainingSeconds;

        if (remainingSeconds != _lastLoggedRemainingSecond)
        {
            _lastLoggedRemainingSecond = remainingSeconds;
            ServerLogTurnClock("Tick", remainingSeconds, "Running");
        }

        if (remain > 0d) return;

        // If player ran out of time without playing any card, skip turn immediately.
        if (!_currentTurnCardPlayed)
        {
            ServerLogTurnClock("Timeout", 0, "TimerExpiredNoCardPlayed");
            ServerAdvanceTurn("TimerExpiredNoCardPlayed");
            return;
        }

        PlayerManager turnPlayer = ServerGetCurrentTurnPlayer();
        SkillMode forcedFromMode = SkillMode.None;
        bool forceEndedSkill = false;

        if (turnPlayer != null && turnPlayer.activeSkillMode != SkillMode.None)
        {
            forcedFromMode = turnPlayer.activeSkillMode;
            forceEndedSkill = turnPlayer.ServerForceEndActiveSkill("TimerExpired");
            if (forceEndedSkill)
                ServerLogTurnClock("SkillForceEnded", 0, $"from={forcedFromMode}");
        }

        if (!ServerCanFinishCurrentTurn(out string blockedBy))
        {
            // Fail-safe: never stay stuck at 0s.
            ServerLogTurnClock("Timeout", 0, $"TimerExpiredForcedAdvance blockedBy={blockedBy}");
            ServerAdvanceTurn("TimerExpiredForcedAdvance");
            return;
        }

        string timeoutReason = forceEndedSkill ? "TimerExpiredForceEndSkill" : "TimerExpired";
        ServerLogTurnClock("Timeout", 0, timeoutReason);
        ServerAdvanceTurn(timeoutReason);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerRebuildTurnOrder("OnStartServer");
    }

    public override void OnStopServer()
    {
        ServerStopTurnTimer();
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        TurnOrder.Callback += OnTurnOrderChangedClient;
        PlayerManager.RequestTurnOrderLayoutRefresh("TurnManager.OnStartClient");
    }

    public override void OnStopClient()
    {
        TurnOrder.Callback -= OnTurnOrderChangedClient;
        base.OnStopClient();
    }

    private void OnCurrentTurnIndexChanged(int oldValue, int newValue)
    {
        if (!NetworkClient.active) return;
        PlayerManager.RequestTurnOrderLayoutRefresh($"TurnManager.CurrentTurnIndex {oldValue}->{newValue}");
    }

    private void OnCurrentTurnNetIdChanged(uint oldValue, uint newValue)
    {
        if (!NetworkClient.active) return;
        PlayerManager.RequestTurnOrderLayoutRefresh($"TurnManager.CurrentTurnNetId {oldValue}->{newValue}");
    }

    private void OnTurnOrderChangedClient(SyncList<uint>.Operation op, int itemIndex, uint oldItem, uint newItem)
    {
        if (!NetworkClient.active) return;
        PlayerManager.RequestTurnOrderLayoutRefresh($"TurnManager.TurnOrder.{op}");
    }

    [Server]
    public void ServerRebuildTurnOrder(string reason = null)
    {
        uint keepNetId = ServerGetCurrentTurnNetId_Internal();

        var players = new List<PlayerManager>();
        foreach (var kv in NetworkServer.connections)
        {
            NetworkConnectionToClient conn = kv.Value;
            if (conn == null || conn.identity == null) continue;

            PlayerManager pm = conn.identity.GetComponent<PlayerManager>();
            if (pm == null) continue;
            if (pm.SeatIndex < 0) continue;
            if (!pm.isActiveAndEnabled) continue;

            players.Add(pm);
        }

        players.Sort((a, b) =>
        {
            int seatCompare = a.SeatIndex.CompareTo(b.SeatIndex);
            if (seatCompare != 0) return seatCompare;
            return a.netId.CompareTo(b.netId);
        });

        List<int> duplicateSeats = players
            .GroupBy(p => p.SeatIndex)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateSeats.Count > 0)
            Debug.LogWarning($"[TurnManager] Duplicate SeatIndex detected: {string.Join(", ", duplicateSeats)} (tie-break by netId)");

        TurnOrder.Clear();
        foreach (PlayerManager pm in players)
            TurnOrder.Add(pm.netId);

        if (TurnOrder.Count == 0)
        {
            currentTurnIndex = -1;
            currentTurnNetId = 0;
            ServerStopTurnTimer();
        }
        else
        {
            int idx = keepNetId != 0 ? TurnOrder.IndexOf(keepNetId) : -1;
            if (idx < 0) idx = 0;
            currentTurnIndex = idx;
            currentTurnNetId = TurnOrder[currentTurnIndex];

            if (_turnClockArmed)
                ServerStartCurrentTurnTimer($"Rebuild:{reason ?? "-"}");
        }

        ServerLogTurnOrder(reason);
        ServerRequestClientLayoutRefresh($"Rebuild:{reason ?? "-"}");
    }

    [Server]
    public void ServerPickStarterFromDuckZoneAndRotate(string reason = null)
    {
        if (TurnOrder.Count == 0)
        {
            Debug.LogWarning($"[TurnManager] Skip rotate because TurnOrder is empty. reason={reason}");
            return;
        }

        if (!ServerFindStarterNetIdFromDuckZone(out uint starterNetId, out DuckCard frontDuck, out string starterBy, out string frontDuckKey))
        {
            Debug.LogWarning($"[TurnManager] Starter not found from DuckZone. reason={reason}");
            return;
        }

        int starterIndex = TurnOrder.IndexOf(starterNetId);
        if (starterIndex < 0)
        {
            Debug.LogWarning($"[TurnManager] Starter netId={starterNetId} not found in TurnOrder. reason={reason}");
            return;
        }

        if (starterIndex != 0)
        {
            var rotated = new List<uint>(TurnOrder.Count);
            for (int i = 0; i < TurnOrder.Count; i++)
                rotated.Add(TurnOrder[(starterIndex + i) % TurnOrder.Count]);

            TurnOrder.Clear();
            foreach (uint id in rotated)
                TurnOrder.Add(id);
        }

        currentTurnIndex = 0;
        currentTurnNetId = TurnOrder[0];

        string starterSeat = "-1";
        string starterDuckKey = "-";
        if (NetworkServer.spawned.TryGetValue(starterNetId, out NetworkIdentity starterNi) &&
            starterNi != null &&
            starterNi.TryGetComponent(out PlayerManager starterPm))
        {
            starterSeat = starterPm.SeatIndex.ToString();
            starterDuckKey = DuckKeyFromIndex(starterPm.duckColorIndex);
        }

        Debug.Log(
            $"[TurnManager] Starter picked reason={reason ?? "-"} by={starterBy} " +
            $"frontDuckNetId={(frontDuck != null ? frontDuck.netId.ToString() : "-")} frontDuckKey={frontDuckKey ?? "-"} " +
            $"starterNetId={starterNetId} starterSeatIndex={starterSeat} starterDuckKey={starterDuckKey}"
        );

        _turnClockArmed = true;
        ServerStartCurrentTurnTimer($"Rotate:{reason ?? "-"}");

        ServerLogTurnOrder($"Rotate:{reason ?? "-"}");
        ServerRequestClientLayoutRefresh($"Rotate:{reason ?? "-"}");
    }

    [Server]
    public void ServerRequestClientLayoutRefresh(string reason = null)
    {
        RpcRequestClientLayoutRefresh(reason ?? "-");
    }

    [Server]
    public void ServerAdvanceTurn(string reason = null)
    {
        PlayerManager previousTurnPlayer = ServerGetCurrentTurnPlayer();
        if (previousTurnPlayer != null)
        {
            bool cleared = previousTurnPlayer.ServerForceEndActiveSkill($"TurnAdvance:{reason ?? "-"}");
            if (cleared)
            {
                Debug.Log(
                    $"[TurnManager] TurnCleanup reason={reason ?? "-"} clearedNetId={previousTurnPlayer.netId} " +
                    $"seatIndex={previousTurnPlayer.SeatIndex}"
                );
            }
        }

        if (TurnOrder.Count <= 0)
        {
            ServerStopTurnTimer();
            return;
        }

        if (currentTurnIndex < 0 || currentTurnIndex >= TurnOrder.Count)
            currentTurnIndex = 0;
        else
            currentTurnIndex = (currentTurnIndex + 1) % TurnOrder.Count;

        currentTurnNetId = TurnOrder[currentTurnIndex];

        ServerLogTurnClock("Advance", currentTurnRemainingSeconds, reason ?? "-");
        ServerStartCurrentTurnTimer($"Advance:{reason ?? "-"}");
    }

    [Server]
    public void ServerNotifyCardPlayed(uint playerNetId, string cardName = null)
    {
        uint turnNetId = ServerGetCurrentTurnNetId_Internal();
        if (turnNetId == 0)
            return;

        if (playerNetId != turnNetId)
        {
            Debug.LogWarning($"[TurnManager] Reject card play out-of-turn playerNetId={playerNetId} currentTurnNetId={turnNetId}");
            return;
        }

        if (_currentTurnCardPlayed)
            return;

        _currentTurnCardPlayed = true;
        _currentTurnSkillDeclared = false;
        ServerLogTurnClock("CardPlayed", currentTurnRemainingSeconds, $"card={cardName ?? "-"}");

        ServerTrackSkillStateAndLog(currentTurnRemainingSeconds);

        if (ServerTryAdvanceResolved("CardPlayedImmediate", currentTurnRemainingSeconds))
            return;
    }

    [Server]
    public void ServerNotifySkillModeSelected(uint playerNetId, SkillMode selectedSkill)
    {
        if (selectedSkill == SkillMode.None)
            return;

        uint turnNetId = ServerGetCurrentTurnNetId_Internal();
        if (turnNetId == 0)
            return;

        if (playerNetId != turnNetId)
        {
            Debug.LogWarning(
                $"[TurnManager] Reject skill select out-of-turn playerNetId={playerNetId} currentTurnNetId={turnNetId} mode={selectedSkill}"
            );
            return;
        }

        if (!_currentTurnCardPlayed)
        {
            Debug.LogWarning(
                $"[TurnManager] Reject skill select before card played playerNetId={playerNetId} mode={selectedSkill}"
            );
            return;
        }

        _currentTurnSkillDeclared = true;
        ServerLogTurnClock("SkillDeclared", currentTurnRemainingSeconds, $"mode={selectedSkill}");
        ServerTrackSkillStateAndLog(currentTurnRemainingSeconds);

        if (ServerTryAdvanceResolved("SkillDeclaredImmediate", currentTurnRemainingSeconds))
            return;
    }

    [ClientRpc]
    private void RpcRequestClientLayoutRefresh(string reason)
    {
        if (!NetworkClient.active) return;
        PlayerManager.RequestTurnOrderLayoutRefresh($"TurnManagerRpc:{reason}");
    }

    [Server]
    private bool ServerFindStarterNetIdFromDuckZone(out uint starterNetId, out DuckCard frontDuck, out string starterBy, out string frontDuckKey)
    {
        starterNetId = 0;
        frontDuck = null;
        starterBy = "-";
        frontDuckKey = "-";

        var ducksInDuckZone = new List<DuckCard>();
        foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
        {
            if (ni == null) continue;
            if (!ni.TryGetComponent(out DuckCard dc)) continue;
            if (dc.zone != ZoneKind.DuckZone) continue;
            ducksInDuckZone.Add(dc);
        }

        ducksInDuckZone.Sort((a, b) =>
        {
            int col = a.ColNet.CompareTo(b.ColNet); // Front-most first
            if (col != 0) return col;
            int zoneIdx = a.zoneIndex.CompareTo(b.zoneIndex);
            if (zoneIdx != 0) return zoneIdx;
            return a.netId.CompareTo(b.netId);
        });

        foreach (DuckCard dc in ducksInDuckZone)
        {
            frontDuck = dc;
            frontDuckKey = DuckKeyFromCardName(dc.name) ?? "-";

            if (dc.ownerNetId != 0 && TurnOrder.Contains(dc.ownerNetId))
            {
                starterNetId = dc.ownerNetId;
                starterBy = "ownerNetId";
                return true;
            }

            uint byDuckKey = ServerFindPlayerNetIdByDuckKey(frontDuckKey);
            if (byDuckKey != 0 && TurnOrder.Contains(byDuckKey))
            {
                starterNetId = byDuckKey;
                starterBy = "duckKey";
                return true;
            }
        }

        return false;
    }

    [Server]
    private static uint ServerFindPlayerNetIdByDuckKey(string duckKey)
    {
        if (string.IsNullOrWhiteSpace(duckKey) || duckKey == "-")
            return 0;

        foreach (var kv in NetworkServer.connections)
        {
            NetworkConnectionToClient conn = kv.Value;
            if (conn == null || conn.identity == null) continue;
            if (!conn.identity.TryGetComponent(out PlayerManager pm)) continue;
            if (!pm.isActiveAndEnabled || pm.SeatIndex < 0) continue;

            if (DuckKeyFromIndex(pm.duckColorIndex) == duckKey)
                return pm.netId;
        }

        return 0;
    }

    [Server]
    private bool ServerCanFinishCurrentTurn(out string blockedBy)
    {
        blockedBy = null;

        PlayerManager turnPlayer = ServerGetCurrentTurnPlayer();
        if (turnPlayer == null)
        {
            blockedBy = "NoCurrentTurnPlayer";
            return false;
        }

        if (!_currentTurnCardPlayed)
        {
            blockedBy = "CardNotPlayedThisTurn";
            return false;
        }

        if (!_currentTurnSkillDeclared)
        {
            blockedBy = "CardSkillNotActivatedYet";
            return false;
        }

        if (turnPlayer.activeSkillMode != SkillMode.None)
        {
            blockedBy = $"SkillMode={turnPlayer.activeSkillMode}";
            return false;
        }

        return true;
    }

    [Server]
    private void ServerTrackSkillStateAndLog(int remainingSeconds)
    {
        PlayerManager turnPlayer = ServerGetCurrentTurnPlayer();
        if (turnPlayer == null) return;

        SkillMode now = turnPlayer.activeSkillMode;
        if (_currentTurnCardPlayed && now != SkillMode.None)
            _sawNonNoneSkillSinceCardPlayed = true;

        if (now == _lastObservedSkillMode) return;

        if (_currentTurnCardPlayed)
        {
            if (now == SkillMode.None && _lastObservedSkillMode != SkillMode.None)
            {
                ServerLogTurnClock("SkillResolved", remainingSeconds, $"from={_lastObservedSkillMode}");
            }
            else if (now != SkillMode.None)
            {
                ServerLogTurnClock("SkillActive", remainingSeconds, $"mode={now}");
            }
        }

        _lastObservedSkillMode = now;
    }

    [Server]
    private bool ServerTryAdvanceResolved(string reason, int remainingSeconds)
    {
        if (!_currentTurnCardPlayed)
            return false;

        if (!ServerCanFinishCurrentTurn(out _))
            return false;

        string detail = _sawNonNoneSkillSinceCardPlayed ? "SkillResolved" : "SkillActivatedInstant";
        ServerLogTurnClock("ReadyToEnd", remainingSeconds, $"{reason}|{detail}");
        ServerAdvanceTurn(reason);
        return true;
    }

    [Server]
    private void ServerUpdateWaitingForTurnFinish()
    {
        currentTurnRemainingSeconds = 0;

        PlayerManager turnPlayer = ServerGetCurrentTurnPlayer();
        SkillMode forcedFromMode = SkillMode.None;
        bool forceEndedSkill = false;

        if (turnPlayer != null && turnPlayer.activeSkillMode != SkillMode.None)
        {
            forcedFromMode = turnPlayer.activeSkillMode;
            forceEndedSkill = turnPlayer.ServerForceEndActiveSkill("TimeoutWaiting");
            if (forceEndedSkill)
                ServerLogTurnClock("SkillForceEnded", 0, $"from={forcedFromMode}|reason=TimeoutWaiting");
        }

        string reason = forceEndedSkill ? "TimerExpiredForceEndSkill" : "TimerExpiredForcedAdvance";
        ServerLogTurnClock("Timeout", 0, reason);
        _waitingForTurnFinishConditions = false;
        ServerAdvanceTurn(reason);
    }

    [Server]
    private void ServerStartCurrentTurnTimer(string reason)
    {
        if (TurnOrder.Count <= 0 || currentTurnIndex < 0 || currentTurnIndex >= TurnOrder.Count)
        {
            ServerStopTurnTimer();
            return;
        }

        float duration = Mathf.Max(1f, turnDurationSeconds);
        _turnDeadlineServerTime = NetworkTime.time + duration;
        currentTurnRemainingSeconds = Mathf.CeilToInt(duration);
        _lastLoggedRemainingSecond = -1;
        _currentTurnCardPlayed = false;
        _currentTurnSkillDeclared = false;
        _waitingForTurnFinishConditions = false;
        _nextPendingFinishLogAt = 0d;
        _sawNonNoneSkillSinceCardPlayed = false;
        _lastObservedSkillMode = ServerGetCurrentTurnPlayer()?.activeSkillMode ?? SkillMode.None;

        ServerLogTurnClock("Start", currentTurnRemainingSeconds, reason);
    }

    [Server]
    private void ServerStopTurnTimer()
    {
        _turnDeadlineServerTime = -1d;
        _lastLoggedRemainingSecond = -1;
        currentTurnRemainingSeconds = 0;
        _currentTurnCardPlayed = false;
        _currentTurnSkillDeclared = false;
        _waitingForTurnFinishConditions = false;
        _nextPendingFinishLogAt = 0d;
        _sawNonNoneSkillSinceCardPlayed = false;
        _lastObservedSkillMode = SkillMode.None;
    }

    [Server]
    private void ServerLogTurnClock(string stage, int remainingSeconds, string reason)
    {
        uint turnNetId = ServerGetCurrentTurnNetId_Internal();
        int seatIndex = -1;
        int duckColorIndex = -1;
        string duckKey = "-";
        SkillMode skillMode = SkillMode.None;

        if (turnNetId != 0 &&
            NetworkServer.spawned.TryGetValue(turnNetId, out NetworkIdentity ni) &&
            ni != null &&
            ni.TryGetComponent(out PlayerManager pm))
        {
            seatIndex = pm.SeatIndex;
            duckColorIndex = pm.duckColorIndex;
            duckKey = DuckKeyFromIndex(duckColorIndex);
            skillMode = pm.activeSkillMode;
        }

        Debug.Log(
            $"[TurnManager] Turn{stage} reason={reason ?? "-"} " +
            $"turnIndex={currentTurnIndex} netId={turnNetId} seatIndex={seatIndex} duckKey={duckKey} " +
            $"remaining={remainingSeconds}s cardPlayed={_currentTurnCardPlayed} skillDeclared={_currentTurnSkillDeclared} skillMode={skillMode}"
        );
    }

    [Server]
    public uint ServerGetCurrentTurnNetId() => ServerGetCurrentTurnNetId_Internal();

    [Server]
    public PlayerManager ServerGetCurrentTurnPlayer()
    {
        uint id = ServerGetCurrentTurnNetId_Internal();
        if (id == 0) return null;

        return NetworkServer.spawned.TryGetValue(id, out NetworkIdentity ni) ? ni.GetComponent<PlayerManager>() : null;
    }

    [Server]
    public int ServerGetCurrentTurnSeatIndex()
    {
        PlayerManager pm = ServerGetCurrentTurnPlayer();
        return pm != null ? pm.SeatIndex : -1;
    }

    [Server]
    private uint ServerGetCurrentTurnNetId_Internal()
    {
        if (currentTurnIndex < 0 || currentTurnIndex >= TurnOrder.Count) return 0;
        return TurnOrder[currentTurnIndex];
    }

    [Server]
    private void ServerLogTurnOrder(string reason)
    {
        var lines = new List<string>
        {
            $"[TurnManager] TurnOrder reason={reason ?? "-"} count={TurnOrder.Count} currentTurnIndex={currentTurnIndex} currentTurnNetId={currentTurnNetId}"
        };

        for (int i = 0; i < TurnOrder.Count; i++)
        {
            uint id = TurnOrder[i];
            int seatIndex = -1;
            int duckColorIndex = -1;
            string duckKey = "-";

            if (NetworkServer.spawned.TryGetValue(id, out NetworkIdentity ni) &&
                ni != null &&
                ni.TryGetComponent(out PlayerManager pm))
            {
                seatIndex = pm.SeatIndex;
                duckColorIndex = pm.duckColorIndex;
                duckKey = DuckKeyFromIndex(duckColorIndex);
            }

            lines.Add(
                $"  Turn#{i + 1} | seatIndex={seatIndex} | netId={id} | duckColorIndex={duckColorIndex} | duckKey={duckKey}"
            );
        }

        Debug.Log(string.Join("\n", lines));
    }
}
