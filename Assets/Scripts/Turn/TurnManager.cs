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
    [SerializeField] private float turnDurationSeconds = 3000000f; // เลวาในแต่ละเทิร์น

    [Header("Timeout Penalty")]
    [SerializeField] private bool destroyOwnedDuckOnTimeout = true;

    [Header("Debug")]
    [SerializeField] private bool enableTurnLogs = false;

    [SyncVar(hook = nameof(OnCurrentTurnIndexChanged))]
    public int currentTurnIndex = -1;

    [SyncVar(hook = nameof(OnCurrentTurnNetIdChanged))]
    public uint currentTurnNetId = 0;

    [SyncVar] public int currentTurnRemainingSeconds = 0;

    [SyncVar] public bool isMatchEnded = false;
    [SyncVar] private string winnerDuckKey = "";
    [SyncVar] private int winnerRemainingCount = 0;

    private bool _turnClockArmed;
    private double _turnDeadlineServerTime = -1d;
    private int _lastLoggedRemainingSecond = -1;
    private bool _currentTurnCardPlayed;
    private bool _currentTurnSkillDeclared;
    private bool _waitingForTurnFinishConditions;
    private double _nextPendingFinishLogAt;
    private bool _sawNonNoneSkillSinceCardPlayed;
    private SkillMode _lastObservedSkillMode = SkillMode.None;
    private bool _localMatchEndOverlayShown;

    [Header("Match End UI")]
    [SerializeField] private GameObject matchEndOverlayPrefab;
    [SerializeField] private string matchEndCanvasName = "Main Canvas";

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
        if (isMatchEnded) return;
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
            ServerApplyTimeoutPenaltyAndLog(ServerGetCurrentTurnPlayer(), "TimerExpiredNoCardPlayed");
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
            ServerApplyTimeoutPenaltyAndLog(turnPlayer, "TimerExpiredForcedAdvance");
            // Fail-safe: never stay stuck at 0s.
            ServerLogTurnClock("Timeout", 0, $"TimerExpiredForcedAdvance blockedBy={blockedBy}");
            ServerAdvanceTurn("TimerExpiredForcedAdvance");
            return;
        }

        string timeoutReason = forceEndedSkill ? "TimerExpiredForceEndSkill" : "TimerExpired";
        ServerApplyTimeoutPenaltyAndLog(turnPlayer, timeoutReason);
        ServerLogTurnClock("Timeout", 0, timeoutReason);
        ServerAdvanceTurn(timeoutReason);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerRebuildTurnOrder("OnStartServer");

        if (DuckOwnershipStatusService.Instance != null)
            DuckOwnershipStatusService.Instance.ServerForceRefreshNow("TurnManager.OnStartServer");
    }

    public override void OnStopServer()
    {
        ServerStopTurnTimer();
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _localMatchEndOverlayShown = false;
        TurnOrder.Callback += OnTurnOrderChangedClient;
        PlayerManager.RequestTurnOrderLayoutRefresh("TurnManager.OnStartClient");
    }

    public override void OnStopClient()
    {
        _localMatchEndOverlayShown = false;
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

        if (enableTurnLogs)
        {
            Debug.Log(
                $"[TurnManager] Starter picked reason={reason ?? "-"} by={starterBy} " +
                $"frontDuckNetId={(frontDuck != null ? frontDuck.netId.ToString() : "-")} frontDuckKey={frontDuckKey ?? "-"} " +
                $"starterNetId={starterNetId} starterSeatIndex={starterSeat} starterDuckKey={starterDuckKey}"
            );
        }

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
        if (isMatchEnded)
            return;

        PlayerManager previousTurnPlayer = ServerGetCurrentTurnPlayer();
        if (previousTurnPlayer != null)
        {
            bool cleared = previousTurnPlayer.ServerForceEndActiveSkill($"TurnAdvance:{reason ?? "-"}");
            if (cleared && enableTurnLogs)
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

        DuckOwnershipStatusService.Instance?.ServerForceRefreshNow($"TurnAdvance:{reason ?? "-"}");
    }

    [Server]
    public void ServerNotifyCardPlayed(uint playerNetId, string cardName = null)
    {
        if (isMatchEnded)
            return;

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
        if (isMatchEnded)
            return;

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
                DuckOwnershipStatusService.Instance?.ServerForceRefreshNow("SkillResolved");
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

        // Re-evaluate win/draw right after card ability resolves and before advancing turn.
        DuckOwnershipStatusService.Instance?.ServerForceRefreshNow($"TurnResolved:{reason}");
        if (isMatchEnded)
            return true;

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
        ServerApplyTimeoutPenaltyAndLog(turnPlayer, reason);
        ServerLogTurnClock("Timeout", 0, reason);
        _waitingForTurnFinishConditions = false;
        ServerAdvanceTurn(reason);
    }

    [Server]
    private void ServerApplyTimeoutPenaltyAndLog(PlayerManager timedOutPlayer, string reason)
    {
        if (!destroyOwnedDuckOnTimeout)
        {
            ServerLogTurnClock("Penalty", currentTurnRemainingSeconds, $"Disabled|reason={reason}");
            return;
        }

        if (!ServerTryApplyTimeoutPenalty(timedOutPlayer, reason, out string penaltyDetail))
        {
            ServerLogTurnClock("Penalty", currentTurnRemainingSeconds, $"None|{penaltyDetail}");
            return;
        }

        ServerLogTurnClock("Penalty", currentTurnRemainingSeconds, penaltyDetail);
    }

    [Server]
    private bool ServerTryApplyTimeoutPenalty(PlayerManager timedOutPlayer, string reason, out string penaltyDetail)
    {
        penaltyDetail = "NoPenalty";

        if (timedOutPlayer == null)
        {
            penaltyDetail = $"NoCurrentTurnPlayer|reason={reason}";
            return false;
        }

        string duckKey = DuckKeyFromIndex(timedOutPlayer.duckColorIndex);
        if (string.IsNullOrWhiteSpace(duckKey) || duckKey == "-")
        {
            penaltyDetail = $"InvalidDuckKey|reason={reason}|netId={timedOutPlayer.netId}";
            return false;
        }

        if (CardPoolManager.TryConsumeCard(duckKey))
        {
            DuckOwnershipStatusService.Instance?.ServerForceRefreshNow($"TimeoutPenalty:{reason}");
            penaltyDetail =
                $"DestroyedOneDuck|reason={reason}|netId={timedOutPlayer.netId}|seatIndex={timedOutPlayer.SeatIndex}|duckKey={duckKey}|from=Pool";
            return true;
        }

        List<DuckCard> candidates = ServerCollectOwnedDuckCandidates(timedOutPlayer.netId, duckKey);
        if (candidates.Count > 0)
        {
            candidates.Sort((a, b) =>
            {
                int col = a.ColNet.CompareTo(b.ColNet); // front-most first
                if (col != 0) return col;
                return a.netId.CompareTo(b.netId);
            });

            DuckCard victim = candidates[0];
            uint victimNetId = victim.netId;

            ServerDestroyTargetsForDuck(victimNetId);
            NetworkServer.Destroy(victim.gameObject);
            ServerResequenceDuckZoneColumns();
            ServerRefillDuckZoneToSix();
            DuckOwnershipStatusService.Instance?.ServerForceRefreshNow($"TimeoutPenalty:{reason}");

            penaltyDetail =
                $"DestroyedOneDuck|reason={reason}|netId={timedOutPlayer.netId}|seatIndex={timedOutPlayer.SeatIndex}|duckKey={duckKey}|from=DuckZone|victimNetId={victimNetId}";
            return true;
        }

        penaltyDetail =
            $"NoOwnedDuckRemaining|reason={reason}|netId={timedOutPlayer.netId}|seatIndex={timedOutPlayer.SeatIndex}|duckKey={duckKey}";
        return false;
    }

    [Server]
    private static List<DuckCard> ServerCollectOwnedDuckCandidates(uint ownerNetId, string duckKey)
    {
        var candidates = new List<DuckCard>();
        foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
        {
            if (ni == null || !ni.TryGetComponent(out DuckCard dc))
                continue;
            if (dc.zone != ZoneKind.DuckZone)
                continue;

            bool byOwner = dc.ownerNetId != 0 && dc.ownerNetId == ownerNetId;
            bool byColor = string.Equals(DuckKeyFromCardName(dc.name), duckKey, StringComparison.OrdinalIgnoreCase);
            if (byOwner || byColor)
                candidates.Add(dc);
        }
        return candidates;
    }

    [Server]
    private static void ServerDestroyTargetsForDuck(uint duckNetId)
    {
        foreach (TargetMarker marker in FindObjectsOfType<TargetMarker>())
        {
            if (marker != null && marker.FollowDuckNetId == duckNetId)
                NetworkServer.Destroy(marker.gameObject);
        }

        foreach (TargetFollow follow in FindObjectsOfType<TargetFollow>())
        {
            if (follow != null && follow.targetNetId == duckNetId)
                NetworkServer.Destroy(follow.gameObject);
        }
    }

    [Server]
    private static void ServerResequenceDuckZoneColumns()
    {
        var ducks = new List<DuckCard>();
        foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
        {
            if (ni == null || !ni.TryGetComponent(out DuckCard dc))
                continue;
            if (dc.zone != ZoneKind.DuckZone)
                continue;

            ducks.Add(dc);
        }

        ducks.Sort((a, b) =>
        {
            int col = a.ColNet.CompareTo(b.ColNet);
            if (col != 0) return col;
            return a.netId.CompareTo(b.netId);
        });

        for (int i = 0; i < ducks.Count; i++)
            ducks[i].ServerAssignToZone(ZoneKind.DuckZone, 0, i);
    }

    [Server]
    private static void ServerRefillDuckZoneToSix()
    {
        int current = 0;
        foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
        {
            if (ni == null || !ni.TryGetComponent(out DuckCard dc))
                continue;
            if (dc.zone == ZoneKind.DuckZone)
                current++;
        }

        int col = current;
        while (col < 6 && CardPoolManager.HasCards())
        {
            GameObject card = CardPoolManager.DrawRandomCard();
            if (card == null)
                break;

            if (!card.TryGetComponent(out DuckCard dc))
            {
                UnityEngine.Object.Destroy(card);
                continue;
            }

            dc.ServerAssignToZone(ZoneKind.DuckZone, 0, col);
            NetworkServer.Spawn(card);
            col++;
        }
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
    public bool ServerIsMatchEnded()
    {
        return isMatchEnded;
    }

    [Server]
    public bool ServerEvaluateMatchEnd(Dictionary<string, int> totalsByKey, string reason = null)
    {
        if (isMatchEnded)
            return true;

        if (totalsByKey == null)
            return false;

        int activeColors = 0;
        string lastKey = null;
        int lastCount = 0;

        for (int i = 0; i < DuckKeysByIndex.Length; i++)
        {
            string key = DuckKeysByIndex[i];
            int count = totalsByKey.TryGetValue(key, out int value) ? value : 0;
            if (count <= 0)
                continue;

            activeColors++;
            lastKey = key;
            lastCount = count;

            if (activeColors > 1)
                break;
        }

        if (activeColors == 1 && !string.IsNullOrWhiteSpace(lastKey))
        {
            isMatchEnded = true;
            winnerDuckKey = lastKey;
            winnerRemainingCount = Mathf.Max(0, lastCount);

            uint winnerNetId = ServerFindPlayerNetIdByDuckKey(lastKey);
            int winnerSeat = -1;
            if (winnerNetId != 0 &&
                NetworkServer.spawned.TryGetValue(winnerNetId, out NetworkIdentity ni) &&
                ni != null &&
                ni.TryGetComponent(out PlayerManager winnerPm))
            {
                winnerSeat = winnerPm.SeatIndex;
            }

            _turnClockArmed = false;
            ServerStopTurnTimer();

            string endReason = reason ?? "-";
            Debug.Log(
                $"[TurnManager] MatchEnd reason={endReason} winnerDuckKey={winnerDuckKey} winnerRemaining={winnerRemainingCount} " +
                $"winnerNetId={winnerNetId} winnerSeatIndex={winnerSeat}"
            );

            RpcShowMatchEndOverlay(winnerDuckKey, winnerRemainingCount, endReason);
            return true;
        }

        if (ServerCanEvaluateDrawFromActionExhausted(out string drawBlockedBy) &&
            ServerAreActionCardsExhausted(out int actionPoolRemaining, out int actionHandRemaining, out int trackedPlayers))
        {
            isMatchEnded = true;
            winnerDuckKey = "Draw";
            winnerRemainingCount = 0;

            _turnClockArmed = false;
            ServerStopTurnTimer();

            string endReason = reason ?? "-";
            Debug.Log(
                $"[TurnManager] MatchDraw reason={endReason} activeDuckColors={activeColors} " +
                $"actionPoolRemaining={actionPoolRemaining} actionHandRemaining={actionHandRemaining} trackedPlayers={trackedPlayers}"
            );

            RpcShowMatchEndOverlay(winnerDuckKey, winnerRemainingCount, $"{endReason}|ActionCardsExhausted");
            return true;
        }
        else if (!string.IsNullOrEmpty(drawBlockedBy) && enableTurnLogs)
        {
            Debug.Log(
                $"[TurnManager] DrawCheckDeferred reason={reason ?? "-"} blockedBy={drawBlockedBy} " +
                $"cardPlayed={_currentTurnCardPlayed} skillDeclared={_currentTurnSkillDeclared}"
            );
        }

        return false;
    }

    [Server]
    private bool ServerCanEvaluateDrawFromActionExhausted(out string blockedBy)
    {
        blockedBy = null;

        PlayerManager turnPlayer = ServerGetCurrentTurnPlayer();
        if (turnPlayer == null)
            return true;

        if (turnPlayer.activeSkillMode != SkillMode.None)
        {
            blockedBy = $"SkillMode={turnPlayer.activeSkillMode}";
            return false;
        }

        // If card was played this turn, wait until its skill is declared/resolved before draw check.
        if (_currentTurnCardPlayed && !_currentTurnSkillDeclared)
        {
            blockedBy = "CardSkillNotActivatedYet";
            return false;
        }

        return true;
    }

    [Server]
    private bool ServerAreActionCardsExhausted(out int actionPoolRemaining, out int actionHandRemaining, out int trackedPlayers)
    {
        actionPoolRemaining = 0;
        actionHandRemaining = 0;
        trackedPlayers = 0;

        if (!_turnClockArmed || TurnOrder.Count <= 0)
            return false;

        int trackedHandRemaining = 0;
        foreach (var kv in NetworkServer.connections)
        {
            NetworkConnectionToClient conn = kv.Value;
            if (conn == null || conn.identity == null)
                continue;

            PlayerManager pm = conn.identity.GetComponent<PlayerManager>();
            if (pm == null || !pm.isActiveAndEnabled || pm.SeatIndex < 0)
                continue;

            trackedPlayers++;
            trackedHandRemaining += Mathf.Max(0, pm.ActionHandCount);
        }

        actionPoolRemaining = PlayerManager.ServerGetSharedActionPoolRemaining();

        int sceneHandRemaining = ServerCountActionCardsInPlayerArea();
        actionHandRemaining = Mathf.Max(trackedHandRemaining, sceneHandRemaining);

        if (trackedPlayers <= 0)
            return false;

        return actionPoolRemaining <= 0 && actionHandRemaining <= 0;
    }

    [Server]
    private static int ServerCountActionCardsInPlayerArea()
    {
        int count = 0;
        foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
        {
            if (ni == null || !ni.TryGetComponent(out DuckCard dc))
                continue;
            if (dc.zone != ZoneKind.PlayerArea)
                continue;

            // Duck colors/Marsh are identity cards, not action cards.
            if (!string.IsNullOrWhiteSpace(DuckKeyFromCardName(dc.name)))
                continue;

            count++;
        }

        return count;
    }

    [ClientRpc]
    private void RpcShowMatchEndOverlay(string winnerKey, int remainingCount, string reason)
    {
        if (!NetworkClient.active)
            return;

        ClientShowMatchEndOverlayLocal(winnerKey, remainingCount, reason);
    }

    [Client]
    public void ClientShowMatchCancelledOverlay(string reason)
    {
        ClientShowMatchEndOverlayLocal("Draw", 0, reason);
    }

    [Client]
    private void ClientShowMatchEndOverlayLocal(string winnerKey, int remainingCount, string reason)
    {
        if (_localMatchEndOverlayShown)
            return;

        _localMatchEndOverlayShown = true;

        if (matchEndOverlayPrefab == null)
        {
            Debug.LogWarning("[TurnManager] matchEndOverlayPrefab is not assigned.");
            return;
        }

        Transform parent = null;
        GameObject canvasGo = GameObject.Find(matchEndCanvasName);
        if (canvasGo != null)
            parent = canvasGo.transform;

        GameObject overlay = Instantiate(matchEndOverlayPrefab, parent, false);
        if (overlay.TryGetComponent(out MatchEndOverlayUI overlayUi))
            overlayUi.Initialize(winnerKey, remainingCount, reason);
    }

    [Server]
    private void ServerLogTurnClock(string stage, int remainingSeconds, string reason)
    {
        if (!enableTurnLogs)
            return;

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
        if (!enableTurnLogs)
            return;

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
