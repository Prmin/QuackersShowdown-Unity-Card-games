using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEngine;

public enum TurnPhase
{
    None = 0,
    PlayActionCard = 1,
    ResolveAbility = 2,
}

[DisallowMultipleComponent]
public partial class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Turn Config")]
    [SerializeField] private float playActionSeconds = 30f;
    [SerializeField] private float resolveAbilitySeconds = 30f;
    [SerializeField] private int minPlayersToStart = 2;

    [Header("Logging")]
    [SerializeField] private bool logServer = true;

    // =========================
    // Sync state (clients read)
    // =========================
    [SyncVar] public TurnPhase Phase = TurnPhase.None;
    [SyncVar] public uint CurrentPlayerNetId;
    [SyncVar] public int TurnNumber;

    // server sets this using NetworkTime.time so clients can compute remaining time
    [SyncVar] public double PhaseEndsAtNetworkTime;

    [SyncVar] public uint CurrentActionCardNetId;
    [SyncVar] public string CurrentActionCardKey = "";
    [SyncVar] public int RequiredTargetPicks;
    [SyncVar] public int RemainingTargetPicks;

    // =========================
    // Server-only state
    // =========================
    private readonly List<uint> _turnOrder = new List<uint>();
    private int _turnIndex = 0;
    private bool _matchStarted = false;

    public IReadOnlyList<uint> TurnOrder => _turnOrder;

    // =========================
    // Unity / Mirror lifecycle
    // =========================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (logServer) Debug.Log("[TurnManager] OnStartServer");
    }

    public override void OnStopServer()
    {
        if (logServer) Debug.Log("[TurnManager] OnStopServer");
        base.OnStopServer();
    }

    private void Update()
    {
        if (!isServer) return;
        if (!_matchStarted) return;
        if (Phase == TurnPhase.None) return;

        if (NetworkTime.time >= PhaseEndsAtNetworkTime)
        {
            HandleTimeout();
        }
    }

    // =========================
    // Public helpers (client/UI)
    // =========================
    public float GetRemainingSeconds()
    {
        // works on both client & server
        double remain = PhaseEndsAtNetworkTime - NetworkTime.time;
        return (float)Math.Max(0, remain);
    }

    // =========================
    // Server API
    // =========================

    [Server]
    public void ServerBeginMatch()
    {
        if (_matchStarted) return;

        ServerRebuildTurnOrderBySeat();

        if (_turnOrder.Count < minPlayersToStart)
        {
            if (logServer) Debug.LogWarning($"[TurnManager] Cannot begin match. players={_turnOrder.Count} < min={minPlayersToStart}");
            return;
        }

        _matchStarted = true;
        TurnNumber = 1;

        _turnIndex = 0;
        CurrentPlayerNetId = _turnOrder[_turnIndex];

        if (logServer) Debug.Log($"[TurnManager] MATCH START -> Current={CurrentPlayerNetId} orderCount={_turnOrder.Count}");

        ServerLogTurnStart(CurrentPlayerNetId);
        ServerStartPlayPhase();
    }

    [Server]
    public void ServerStopMatch()
    {
        _matchStarted = false;

        Phase = TurnPhase.None;
        PhaseEndsAtNetworkTime = 0;

        CurrentPlayerNetId = 0;
        TurnNumber = 0;

        CurrentActionCardNetId = 0;
        CurrentActionCardKey = "";
        RequiredTargetPicks = 0;
        RemainingTargetPicks = 0;

        _turnOrder.Clear();
        _turnIndex = 0;

        if (logServer) Debug.Log("[TurnManager] MATCH STOP");
    }

    [Server]
    public bool ServerCanAct(uint playerNetId, TurnPhase requiredPhase)
    {
        if (!_matchStarted) return false;
        if (Phase != requiredPhase) return false;
        if (playerNetId != CurrentPlayerNetId) return false;
        return true;
    }

    /// <summary>
    /// เรียกหลังจาก “เล่นการ์ดแอคชั่นสำเร็จ” แล้ว (Phase A)
    /// requiredPicks: 0=instant, 1=เลือกเป้า 1 ครั้ง, 2=เลือกเป้า 2 ครั้ง
    /// </summary>
    [Server]
    public void ServerNotifyActionCardPlayed(uint playerNetId, uint actionCardNetId, int requiredPicks, string actionCardKey = "")
    {
        if (!ServerCanAct(playerNetId, TurnPhase.PlayActionCard))
        {
            if (logServer) Debug.LogWarning($"[TurnManager] Reject ActionCardPlayed: player={playerNetId} phase={Phase} current={CurrentPlayerNetId}");
            return;
        }

        CurrentActionCardNetId = actionCardNetId;
        CurrentActionCardKey = actionCardKey ?? "";

        RequiredTargetPicks = Mathf.Max(0, requiredPicks);
        RemainingTargetPicks = RequiredTargetPicks;

        if (logServer)
            Debug.Log($"[TurnManager] ActionCardPlayed -> player={playerNetId} key='{CurrentActionCardKey}' netId={actionCardNetId} requiredPicks={RequiredTargetPicks}");

        ServerStartResolvePhase();
    }

    /// <summary>
    /// เรียกทุกครั้งที่ “เลือกเป้าสำเร็จ 1 ครั้ง” (Phase B)
    /// </summary>
    [Server]
    public void ServerNotifyTargetPicked(uint playerNetId)
    {
        if (!ServerCanAct(playerNetId, TurnPhase.ResolveAbility))
        {
            if (logServer) Debug.LogWarning($"[TurnManager] Reject TargetPicked: player={playerNetId} phase={Phase} current={CurrentPlayerNetId}");
            return;
        }

        if (RemainingTargetPicks <= 0)
        {
            // กันซ้ำ
            return;
        }

        RemainingTargetPicks--;

        if (logServer)
            Debug.Log($"[TurnManager] TargetPicked -> player={playerNetId} remaining={RemainingTargetPicks}/{RequiredTargetPicks}");

        if (RemainingTargetPicks <= 0)
        {
            ServerNotifyAbilityResolved(playerNetId);
        }
    }

    /// <summary>
    /// ใช้สำหรับสกิลที่จบเอง/เป็น coroutine: ให้สกิลเรียกเมื่อ “จบจริง”
    /// </summary>
    [Server]
    public void ServerNotifyAbilityResolved(uint playerNetId)
    {
        // อนุญาตให้ resolve ได้ทั้งตอนอยู่ ResolveAbility หรือแม้แต่ PlayActionCard ในกรณี instant ที่ต้องรอ
        if (!_matchStarted) return;
        if (playerNetId != CurrentPlayerNetId) return;

        if (logServer)
            Debug.Log($"[TurnManager] AbilityResolved -> player={playerNetId}");

        // เคลียร์ state เผื่อไว้
        RemainingTargetPicks = 0;
        RequiredTargetPicks = 0;
        CurrentActionCardNetId = 0;
        CurrentActionCardKey = "";

        ServerAdvanceTurn("ability resolved");
    }

    // =========================
    // Core phase control
    // =========================

    [Server]
    private void ServerStartPlayPhase()
    {
        Phase = TurnPhase.PlayActionCard;

        // reset ability state
        CurrentActionCardNetId = 0;
        CurrentActionCardKey = "";
        RequiredTargetPicks = 0;
        RemainingTargetPicks = 0;

        PhaseEndsAtNetworkTime = NetworkTime.time + playActionSeconds;

        if (logServer)
            Debug.Log($"[TurnManager] Phase=PlayActionCard endAt={PhaseEndsAtNetworkTime:0.00}");
    }

    [Server]
    private void ServerStartResolvePhase()
    {
        Phase = TurnPhase.ResolveAbility;
        PhaseEndsAtNetworkTime = NetworkTime.time + resolveAbilitySeconds;

        if (logServer)
            Debug.Log($"[TurnManager] Phase=ResolveAbility picks={RemainingTargetPicks}/{RequiredTargetPicks} endAt={PhaseEndsAtNetworkTime:0.00}");
    }

    [Server]
    private void HandleTimeout()
    {
        uint offender = CurrentPlayerNetId;

        if (logServer)
            Debug.Log($"[TurnManager][TIMEOUT] offender={offender} phase={Phase}");

        // เคลียร์ skill mode ค้าง (ถ้ามีเมธอดนี้ใน PlayerManager ก็เรียก)
        if (TryGetPlayer(offender, out var pm) && pm != null)
        {
            InvokeIfExists(pm, "ServerTurn_CancelPendingAbility");
        }

        // ลงโทษตามกติกา
        ServerApplyTimeoutPenalty(offender, Phase);

        // เคลียร์ state
        RemainingTargetPicks = 0;
        RequiredTargetPicks = 0;
        CurrentActionCardNetId = 0;
        CurrentActionCardKey = "";

        ServerAdvanceTurn("timeout penalty");
    }

    [Server]
    private void ServerAdvanceTurn(string reason)
    {
        if (_turnOrder.Count == 0)
        {
            if (logServer) Debug.LogWarning("[TurnManager] AdvanceTurn called but turnOrder is empty");
            Phase = TurnPhase.None;
            return;
        }

        TurnNumber++;

        // ข้ามคนที่หลุด/ไม่มีใน spawned
        for (int guard = 0; guard < _turnOrder.Count; guard++)
        {
            _turnIndex = (_turnIndex + 1) % _turnOrder.Count;
            uint candidate = _turnOrder[_turnIndex];

            if (TryGetPlayer(candidate, out _))
            {
                CurrentPlayerNetId = candidate;
                break;
            }
        }

        if (logServer)
            Debug.Log($"[TurnManager] TURN ADVANCE -> Turn#{TurnNumber} Current={CurrentPlayerNetId} reason={reason}");

        ServerLogTurnStart(CurrentPlayerNetId);
        ServerStartPlayPhase();
    }

    // =========================
    // Turn order
    // =========================

    [Server]
    public void ServerRebuildTurnOrderBySeat()
    {
        var players = FindObjectsOfType<PlayerManager>()
            .Where(p => p != null && p.netIdentity != null)
            .OrderBy(p => GetSeatIndex(p))
            .ThenBy(p => p.netId)
            .ToList();

        _turnOrder.Clear();
        foreach (var p in players)
            _turnOrder.Add(p.netId);

        if (logServer)
        {
            string order = string.Join(", ", players.Select(p => $"seat={GetSeatIndex(p)} netId={p.netId}"));
            Debug.Log($"[TurnManager] TurnOrder(by seat): {order}");
        }
    }

    // =========================
    // Logging: whose turn + what color
    // =========================

    [Server]
    private void ServerLogTurnStart(uint playerNetId)
    {
        if (!TryGetPlayer(playerNetId, out var pm) || pm == null)
        {
            Debug.LogWarning($"[TurnManager] TurnStart: player netId={playerNetId} not found");
            return;
        }

        int seat = GetSeatIndex(pm);
        string color = GetDuckColorLabel(pm);

        Debug.Log($"[TurnManager] TURN START -> seat={seat} netId={playerNetId} duckColor={color}");
    }

    // =========================
    // Penalty: destroy duck card
    // =========================

    [Server]
    private void ServerApplyTimeoutPenalty(uint offenderNetId, TurnPhase phaseWhenTimeout)
    {
        if (!TryGetPlayer(offenderNetId, out var offender) || offender == null)
        {
            Debug.LogWarning($"[TurnManager][PENALTY] offender netId={offenderNetId} not found");
            return;
        }

        int seat = GetSeatIndex(offender);
        string color = GetDuckColorLabel(offender);

        // “Deck” นิยามเป็น inactive objects (เหมาะกับ pool) เพื่อไม่เผลอไปลบ action card
        var owned = Resources.FindObjectsOfTypeAll<DuckCard>()
            .Where(c => c != null && c.gameObject != null && c.gameObject.scene.IsValid())
            .Where(c => c.ownerNetId == offenderNetId)
            .ToList();

        var deck = owned.Where(c => !c.gameObject.activeInHierarchy).ToList();
        var zone = owned.Where(c => c.gameObject.activeInHierarchy && c.zone == ZoneKind.DuckZone).ToList();

        int totalBefore = deck.Count + zone.Count;
        if (totalBefore <= 0)
        {
            Debug.LogWarning($"[TurnManager][PENALTY] offender seat={seat} netId={offenderNetId} color={color} -> no duck left");
            return;
        }

        DuckCard victim;
        string from;

        if (deck.Count > 0)
        {
            victim = deck[UnityEngine.Random.Range(0, deck.Count)];
            from = "Deck";
        }
        else
        {
            victim = zone[UnityEngine.Random.Range(0, zone.Count)];
            from = "DuckZone";
        }

        DestroyNetworkObjectSafe(victim.gameObject);

        int remaining = Mathf.Max(0, totalBefore - 1);

        Debug.Log(
            $"[TurnManager][PENALTY] TIMEOUT({phaseWhenTimeout}) offender seat={seat} netId={offenderNetId} color={color} " +
            $"-> destroyed 1 from {from}. remaining({color})={remaining} (Deck+DuckZone)"
        );
    }

    [Server]
    private static void DestroyNetworkObjectSafe(GameObject go)
    {
        if (go == null) return;

        if (NetworkServer.active)
        {
            var ni = go.GetComponent<NetworkIdentity>();
            if (ni != null && ni.netId != 0 && NetworkServer.spawned.ContainsKey(ni.netId))
            {
                NetworkServer.Destroy(go);
                return;
            }
        }

        UnityEngine.Object.Destroy(go);
    }

    // =========================
    // Utilities: find player, seat, color
    // =========================

    [Server]
    private bool TryGetPlayer(uint netId, out PlayerManager pm)
    {
        pm = null;

        if (NetworkServer.active && netId != 0 && NetworkServer.spawned.TryGetValue(netId, out var ni) && ni != null)
        {
            pm = ni.GetComponent<PlayerManager>();
            if (pm != null) return true;
        }

        // fallback (slower)
        pm = FindObjectsOfType<PlayerManager>().FirstOrDefault(p => p != null && p.netId == netId);
        return pm != null;
    }

    private static int GetSeatIndex(PlayerManager pm)
    {
        if (pm == null) return 9999;

        // 1) property SeatIndex
        var prop = pm.GetType().GetProperty("SeatIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.PropertyType == typeof(int))
        {
            try { return (int)prop.GetValue(pm); } catch { }
        }

        // 2) field seatIndex
        var field = pm.GetType().GetField("seatIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(int))
        {
            try { return (int)field.GetValue(pm); } catch { }
        }

        return 9999;
    }

    private static string GetDuckColorLabel(PlayerManager pm)
    {
        if (pm == null) return "UnknownColor";

        // 1) ถ้ามีเมธอด ServerTurn_GetDuckColorLabel ก็ใช้ (จากที่เราทำ bridge ไว้ก่อนหน้า)
        var method = pm.GetType().GetMethod("ServerTurn_GetDuckColorLabel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(string))
        {
            try
            {
                var v = method.Invoke(pm, null) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            catch { }
        }

        // 2) ลองอ่าน field/property ยอดนิยม
        string[] candidates = { "duckColor", "duckColorIndex", "playerColor", "playerColorIndex", "DuckColor", "ColorIndex" };
        foreach (var name in candidates)
        {
            var f = pm.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                try
                {
                    var v = f.GetValue(pm);
                    if (v != null) return v.ToString();
                }
                catch { }
            }

            var p = pm.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null)
            {
                try
                {
                    var v = p.GetValue(pm);
                    if (v != null) return v.ToString();
                }
                catch { }
            }
        }

        return $"Seat{GetSeatIndex(pm)}";
    }

    private static void InvokeIfExists(object target, string methodName)
    {
        if (target == null) return;

        var m = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m == null) return;

        try { m.Invoke(target, null); } catch { }
    }
}
