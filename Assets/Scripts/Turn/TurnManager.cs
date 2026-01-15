using System.Collections.Generic;
using Mirror;
using UnityEngine;
using System.Linq;

public enum TurnPhase : byte
{
  None = 0,
  PlayActionCard = 1,
  ResolveAbility = 2,
}

public partial class TurnManager : NetworkBehaviour
{

  [SyncVar] public string CurrentActionCardKey = "";
  public static TurnManager Instance { get; private set; }

  [Header("Rules")]
  [SerializeField] private float playPhaseSeconds = 30f;
  [SerializeField] private float resolvePhaseSeconds = 30f;

  [Header("Start / Debug")]
  [SerializeField] private bool autoStartWhenMinPlayersReady = false;
  [SerializeField] private int minPlayersToStart = 2;
  [SerializeField] private bool logServer = true;

  // ---- Replicated Turn State ----
  [SyncVar] public int TurnNumber = 0;

  [SyncVar(hook = nameof(OnPhaseChanged))]
  public TurnPhase Phase = TurnPhase.None;

  [SyncVar(hook = nameof(OnCurrentPlayerChanged))]
  public uint CurrentPlayerNetId = 0;

  // ใช้ NetworkTime.time บน server แล้ว sync ให้ client เอาไปทำ countdown
  [SyncVar] public double PhaseEndTime = 0;

  [SyncVar] public uint CurrentActionCardNetId = 0;
  [SyncVar] public int RemainingTargetPicks = 0;

  // Turn order ที่ replicate ได้ (debug/UI ได้)
  public readonly SyncList<uint> TurnOrder = new SyncList<uint>();

  // ---- Server-only lookup ----
  private readonly Dictionary<uint, PlayerManager> _playersByNetId = new Dictionary<uint, PlayerManager>();
  private int _turnIndex = 0;
  private bool _matchStarted = false;

  void Awake()
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
    if (logServer) Debug.Log("[TurnManager] Server started.");
  }

  public override void OnStopServer()
  {
    base.OnStopServer();
    _playersByNetId.Clear();
    TurnOrder.Clear();
    _matchStarted = false;
    _turnIndex = 0;
  }

  void Update()
  {
    if (!isServer) return;
    if (!_matchStarted) return;
    if (Phase == TurnPhase.None) return;

    if (NetworkTime.time >= PhaseEndTime)
    {
      if (logServer) Debug.Log($"[TurnManager] TIMEOUT Phase={Phase} Current={CurrentPlayerNetId}");
      HandleTimeout();
    }
  }

  // =========================
  // Server API (registration)
  // =========================

  [Server]
  public void ServerRegisterPlayer(PlayerManager pm)
  {
    if (pm == null) return;

    var id = pm.netId;

    _playersByNetId[id] = pm;
    if (!TurnOrder.Contains(id))
      TurnOrder.Add(id);

    if (logServer) Debug.Log($"[TurnManager] Register player netId={id} (count={TurnOrder.Count})");

    if (!_matchStarted && autoStartWhenMinPlayersReady && TurnOrder.Count >= minPlayersToStart)
      ServerBeginMatch();
  }

  [Server]
  public void ServerUnregisterPlayer(PlayerManager pm)
  {
    if (pm == null) return;

    var id = pm.netId;
    _playersByNetId.Remove(id);

    if (TurnOrder.Contains(id))
      TurnOrder.Remove(id);

    if (logServer) Debug.Log($"[TurnManager] Unregister player netId={id} (count={TurnOrder.Count})");

    if (!_matchStarted) return;

    // ถ้าคนที่ออกเป็นคนกำลังเล่นอยู่ → ข้ามเทิร์นทันที
    if (CurrentPlayerNetId == id)
      ServerAdvanceTurn("player disconnected");
    else if (TurnOrder.Count == 0)
      ServerEndMatch("no players");
  }

  // =========================
  // Server API (turn flow)
  // =========================

  [Server]
  public void ServerBeginMatch()
  {
    ServerRebuildTurnOrderBySeat();

    if (_matchStarted) return;
    if (TurnOrder.Count == 0) return;

    _matchStarted = true;
    TurnNumber = 1;
    _turnIndex = Mathf.Clamp(_turnIndex, 0, TurnOrder.Count - 1);

    CurrentPlayerNetId = TurnOrder[_turnIndex];
    if (logServer) Debug.Log($"[TurnManager] MATCH START -> Current={CurrentPlayerNetId}");

    ServerStartPlayPhase();
  }

  [Server]
  public void ServerEndMatch(string reason)
  {
    if (logServer) Debug.Log($"[TurnManager] MATCH END reason={reason}");
    _matchStarted = false;
    Phase = TurnPhase.None;
    CurrentPlayerNetId = 0;
    PhaseEndTime = 0;
    CurrentActionCardNetId = 0;
    RemainingTargetPicks = 0;
  }

  [Server]
  public bool ServerIsPlayersTurn(uint playerNetId)
    => _matchStarted && playerNetId != 0 && playerNetId == CurrentPlayerNetId;

  [Server]
  public bool ServerCanAct(uint playerNetId, TurnPhase requiredPhase)
    => ServerIsPlayersTurn(playerNetId) && Phase == requiredPhase;

  /// <summary>
  /// เรียกจาก “server logic ที่ยืนยันแล้วว่าเล่นการ์ดสำเร็จ”
  /// requiredTargetPicks = 0 สำหรับ instant
  /// </summary>
  [Server]
  public bool ServerNotifyActionCardPlayed(uint playerNetId, uint actionCardNetId, int requiredTargetPicks)
  {
    if (!ServerCanAct(playerNetId, TurnPhase.PlayActionCard)) return false;

    CurrentActionCardNetId = actionCardNetId;
    RemainingTargetPicks = Mathf.Max(0, requiredTargetPicks);

    if (logServer)
      Debug.Log($"[TurnManager] ActionCardPlayed by={playerNetId} card={actionCardNetId} picks={RemainingTargetPicks}");

    ServerStartResolvePhase();

    // instant → จบเลย
    if (RemainingTargetPicks <= 0)
      ServerNotifyAbilityResolved(playerNetId);

    return true;
  }

  /// <summary>
  /// เรียกทุกครั้งที่ server “รับ target click” แล้วนับเป็น 1 pick
  /// </summary>
  [Server]
  public bool ServerNotifyTargetPicked(uint playerNetId)
  {
    if (!ServerCanAct(playerNetId, TurnPhase.ResolveAbility)) return false;

    RemainingTargetPicks = Mathf.Max(0, RemainingTargetPicks - 1);

    if (logServer)
      Debug.Log($"[TurnManager] TargetPicked by={playerNetId} remaining={RemainingTargetPicks}");

    if (RemainingTargetPicks <= 0)
      ServerNotifyAbilityResolved(playerNetId);

    return true;
  }

  /// <summary>
  /// เรียกตอนสกิล “จบจริง” (รวมกรณีที่คุณทำ effect หลัง pick ครบแล้วค่อยจบ)
  /// </summary>
  [Server]
  public bool ServerNotifyAbilityResolved(uint playerNetId)
  {
    if (!ServerIsPlayersTurn(playerNetId)) return false;
    if (Phase != TurnPhase.ResolveAbility && Phase != TurnPhase.PlayActionCard) return false;

    if (logServer)
      Debug.Log($"[TurnManager] AbilityResolved by={playerNetId} -> advance turn");

    ServerAdvanceTurn("ability resolved");
    return true;
  }

  // =========================
  // Internal server flow
  // =========================

  [Server]
  private void ServerStartPlayPhase()
  {
    Phase = TurnPhase.PlayActionCard;
    CurrentActionCardNetId = 0;
    RemainingTargetPicks = 0;
    PhaseEndTime = NetworkTime.time + playPhaseSeconds;

    if (logServer)
      Debug.Log($"[TurnManager] Phase=PlayActionCard Current={CurrentPlayerNetId} endAt={PhaseEndTime:F3}");
  }

  [Server]
  private void ServerStartResolvePhase()
  {
    Phase = TurnPhase.ResolveAbility;
    PhaseEndTime = NetworkTime.time + resolvePhaseSeconds;

    if (logServer)
      Debug.Log($"[TurnManager] Phase=ResolveAbility Current={CurrentPlayerNetId} endAt={PhaseEndTime:F3}");
  }

  [Server]
  private void ServerAdvanceTurn(string reason)
  {
    if (TurnOrder.Count == 0)
    {
      ServerEndMatch("no players");
      return;
    }

    // move index to current in case list changed
    _turnIndex = TurnOrder.IndexOf(CurrentPlayerNetId);
    if (_turnIndex < 0) _turnIndex = 0;

    _turnIndex = (_turnIndex + 1) % TurnOrder.Count;

    TurnNumber += 1;
    CurrentPlayerNetId = TurnOrder[_turnIndex];

    if (logServer)
      Debug.Log($"[TurnManager] AdvanceTurn -> Turn#{TurnNumber} Current={CurrentPlayerNetId} reason={reason}");

    ServerStartPlayPhase();
  }

  [Server]
  private void HandleTimeout()
  {
    // คนที่โดนลงโทษ = คนที่กำลังเป็นเทิร์นอยู่ตอนนี้
    uint offender = CurrentPlayerNetId;

    // log ว่า timeout ใคร (phase ไหน)
    if (logServer)
      Debug.Log($"[TurnManager][PENALTY] TIMEOUT offender={offender} phase={Phase}");

    // (A) ลงโทษ: ทำลาย “เป็ดของคนนั้น” ตามกติกาที่นายบอก
    // - เด็ค/กองของเขาก่อน 1 ใบ
    // - ถ้าไม่เหลือในเด็คแล้วค่อยไปลบใน DuckZone
    ServerApplyTimeoutPenalty(offender); // <-- เมธอดนี้เดี๋ยวเราใส่ตามข้อกำหนดบทลงโทษ

    // (B) กัน state ค้าง (เผื่อกำลังอยู่ Phase B)
    RemainingTargetPicks = 0;
    CurrentActionCardNetId = 0;
    CurrentActionCardKey = "";

    // (C) ข้ามเทิร์นทันที
    ServerAdvanceTurn("timeout penalty");
  }

  [Server]
  private bool TryGetCurrentPlayer(out PlayerManager pm)
  {
    pm = null;
    if (!_playersByNetId.TryGetValue(CurrentPlayerNetId, out pm)) return false;
    return pm != null;
  }

  // =========================
  // Client hooks (optional)
  // =========================
  private void OnPhaseChanged(TurnPhase oldValue, TurnPhase newValue) { /* ทำ UI ได้ */ }
  private void OnCurrentPlayerChanged(uint oldValue, uint newValue) { /* ทำ UI ได้ */ }

  // Client helper: เหลือเวลากี่วิ (ใช้ทำ UI)
  public float GetRemainingSeconds()
  {
    if (Phase == TurnPhase.None) return 0f;
    double remain = PhaseEndTime - NetworkTime.time;
    return (float)Mathf.Max(0, (float)remain);
  }
  // ========================= 
  [Server]
  void ServerRebuildTurnOrderBySeat()
  {
    var players = FindObjectsOfType<PlayerManager>()
        .Where(p => p != null && p.netIdentity != null)
        .OrderBy(p => p.SeatIndex)       // ตำแหน่ง 1,2,3...
        .ThenBy(p => p.netId)            // กันกรณี seat ซ้ำ
        .ToList();

    TurnOrder.Clear();
    foreach (var pm in players)
      TurnOrder.Add(pm.netId);

    Debug.Log($"[TurnManager] TurnOrder by seat = {string.Join(",", players.Select(p => $"{p.SeatIndex}:{p.netId}"))}");
  }
  // ========================= logging helper =========================
  [Server]
  void ServerLogTurnStart(uint currentNetId)
  {
    var pm = FindObjectsOfType<PlayerManager>().FirstOrDefault(x => x.netId == currentNetId);
    if (pm == null)
    {
      Debug.LogWarning($"[TurnManager] TurnStart: player netId={currentNetId} not found");
      return;
    }

    string color = pm.ServerTurn_GetDuckColorLabel();
    Debug.Log($"[TurnManager] TURN START -> seat={pm.SeatIndex} netId={pm.netId} duckColor={color}");
  }

}
