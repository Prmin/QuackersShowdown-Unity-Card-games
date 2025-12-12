using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
using UnityEngine.UI;
using System.Linq;
using System;
using Random = UnityEngine.Random;


// =================================================================
// ????? SkillMode Enum 
// =================================================================

public enum SkillMode
{
    None,
    Shoot,
    TakeAim,
    DoubleBarrel,
    QuickShot,
    Misfire,
    TwoBirds,
    BumpLeft,
    BumpRight,
    LineForward,
    MoveAhead,
    HangBack,
    FastForward,
    DisorderlyConduckt,
    DuckShuffle,
    GivePeaceAChance,
    Resurrection
}


public partial class PlayerManager : NetworkBehaviour
{


    // ?????? State ????
    [SyncVar(hook = nameof(OnSkillModeChanged))]
    public SkillMode activeSkillMode = SkillMode.None;


    // --- PATCH: Barrier Hooks ---
    private static bool s_barrierHooksBoundServer = false;
    private static bool s_barrierHooksBoundClient = false;

    // ??? barrier ??????????????????/???????? (???????? true)
    // ?????????????????????? ??????????? false
    public static bool DeferInitialDealToBarrier = true;

    // ???????????????????? ????? BarrierGoServer ???????????????
    private static bool s_matchStarted = false;

    // ============= GameObject References =============

    // ????? ???????
    public GameObject Shoot;
    public GameObject TekeAim;
    public GameObject DoubleBarrel;
    public GameObject QuickShot;
    public GameObject Misfire;
    public GameObject TwoBirds;
    public GameObject BumpLeft;
    public GameObject BumpRight;
    public GameObject LineForward;
    public GameObject MoveAhead;
    public GameObject HangBack;
    public GameObject FastForward;
    public GameObject DisorderlyConduckt;
    public GameObject DuckShuffle;
    public GameObject GivePeaceAChance;


    [Header("Action Card Prefabs")]
    [SerializeField] private GameObject ShootPrefab;
    [SerializeField] private GameObject TekeAimPrefab;
    [SerializeField] private GameObject DoubleBarrelPrefab;
    [SerializeField] private GameObject QuickShotPrefab;
    [SerializeField] private GameObject MisfirePrefab;
    [SerializeField] private GameObject TwoBirdsPrefab;
    [SerializeField] private GameObject BumpLeftPrefab;
    [SerializeField] private GameObject BumpRightPrefab;
    [SerializeField] private GameObject LineForwardPrefab;
    [SerializeField] private GameObject MoveAheadPrefab;
    [SerializeField] private GameObject HangBackPrefab;
    [SerializeField] private GameObject FastForwardPrefab;
    [SerializeField] private GameObject DisorderlyConducktPrefab;
    [SerializeField] private GameObject DuckShufflePrefab;
    [SerializeField] private GameObject GivePeaceAChancePrefab;
    [SerializeField] private GameObject resurrectionPrefab;
    [SerializeField] private GameObject duckAndCoverPrefab;



    /////////////////////////////////////////////////////////
    public GameObject PlayerArea;
    public GameObject EnemyArea;
    public GameObject DropZone;
    public GameObject DuckZone;
    public GameObject TargetZone;
    public GameObject DuckBlue;
    public GameObject DuckGreen;
    public GameObject DuckOrange;
    public GameObject DuckPink;
    public GameObject DuckPurple;
    public GameObject DuckYellow;
    public GameObject Marsh;
    public GameObject TargetCoverZone;

    [Header("Duck Card Prefabs")]
    [SerializeField] private GameObject DuckBluePrefab;
    [SerializeField] private GameObject DuckGreenPrefab;
    [SerializeField] private GameObject DuckOrangePrefab;
    [SerializeField] private GameObject DuckPinkPrefab;
    [SerializeField] private GameObject DuckPurplePrefab;
    [SerializeField] private GameObject DuckYellowPrefab;
    [SerializeField] private GameObject MarshPrefab;
    [SerializeField] private GameObject targetCoverPrefab;

    ///////////////////////////////////
    // === NEW: ?????? 5 ????????? ===
    [Header("Enemies Slots (up to 5)")]
    [SerializeField] private string enemiesAreaRootName = "EnemiesArea";   // ???? parent
    [SerializeField] private string enemySlotPrefix = "EnemyArea";      // EnemyArea1..5

    // ????????????????? (?????????????????????????????????????)
    [SyncVar] public int seatIndex = -1;

    // ????????????? (???? client ?????????)
    private static Transform[] s_enemySlots = null;

    // map: netId ??? PlayerManager (?????) -> slot index [0..4]
    private static readonly Dictionary<uint, int> s_remoteSlotIndex = new Dictionary<uint, int>();



    //////////////////////////////////////////////////////////////////////
    public static PlayerManager localInstance;

    public static uint LocalPlayerNetId
    {
        get
        {
            if (localInstance != null)
            {
                var ni = localInstance.GetComponent<NetworkIdentity>();
                if (ni != null)
                    return ni.netId;
            }

            var connIdentity = NetworkClient.connection?.identity;
            return connIdentity != null ? connIdentity.netId : 0;
        }
    }

    private DuckCard firstSelectedDuck = null; // ??????????????????????

    private NetworkIdentity firstTwoBirdsCard = null;
    private int twoBirdsClickCount = 0;
    private int doubleBarrelClickCount = 0;
    // // ???? Card ????????????
    private NetworkIdentity firstClickedCard = null;
    [SerializeField] private GameObject targetPrefab;

    // ============= Card Collections =============
    [SyncVar] public int playerID;
    [Header("Action Card Prefab List")]
    [SerializeField]
    private List<GameObject> actionCardPrefabList; // Prefabs ??????????????????????
    private Dictionary<string, GameObject> actionCardPrefabMap;

    private List<GameObject> cards = new List<GameObject>();
    private Dictionary<GameObject, int> cardPool = new Dictionary<GameObject, int>();
    public readonly SyncDictionary<string, int> actionCardPool = new SyncDictionary<string, int>();
    // private bool isTekeAimActive = false;

    [SyncVar]
    private uint targetedDuckNetId;



    void Start()
    {
        // ??? DuckZone ?????? null ??? Subscribe Event OnCardClicked ??????????????
        if (DuckZone != null)
        {

        }
        else
        {
            // Debug.LogWarning("DuckZone is null at Start().");
        }

        if (DuckZone == null)
        {
            // Debug.LogError("[Start] DuckZone is NULL! Trying to find it...");
            DuckZone = GameObject.Find("DuckZone");

            if (DuckZone == null)
            {
                // Debug.LogError("[Start] Could not find DuckZone in the scene!");
            }
            else
            {
                // Debug.Log($"[Start] DuckZone found: {DuckZone}");
            }
        }
    }

    // ///////////////////////////////////////////  Turn  ////////////////////////////////////////////////////////////////////

    // === Turn state (????????? + ??????????????) ===
    // Mirror ???? SyncVar ??? static ? ???? static ?????????
    private static int s_currentTurnSeat = -1;

    // ???????? SyncVar (instance) ???????????? client ?????
    [SyncVar(hook = nameof(OnTurnSeatChanged))]
    private int _currentTurnSeatNet = -1;

    // Hook: ?????????? client ???????? _currentTurnSeatNet ???????
    private void OnTurnSeatChanged(int oldValue, int newValue)
    {
        s_currentTurnSeat = newValue;
    }

    // ????????????? (????????????)
    private static readonly List<int> s_turnOrder = new List<int>();

    // ????????????? (SyncVar ?????????????????)
    [SyncVar] public int duckColorIndex = 0; // 0..N-1










    // ========================
    //  Core State Logic 
    // ========================

    // (Optional) Hook ?????? Client UI 
    void OnSkillModeChanged(SkillMode oldMode, SkillMode newMode)
    {
        // Debug.Log($"[Client] Skill mode changed from {oldMode} to {newMode}");
        // (???? UIManager.Instance.HighlightSkillButton(newMode);)
    }

    // Command ?????????? Client (Local Player) ??????????????
    [Command]
    public void CmdSetSkillMode(SkillMode newMode)
    {
        // Server ???????????????? SyncVar ???
        activeSkillMode = newMode;

        // --- ?? 3.1 (???? Logic ??????? "????????" ???????????) ---

        bool modeShouldClose = false;

        if (newMode == SkillMode.LineForward)
        {
            CmdActivateLineForward(); // (????? Logic ????)
            modeShouldClose = true; // ?????????? ???????
        }
        else if (newMode == SkillMode.DuckShuffle)
        {
            CmdActivateDuckShuffle(); // (????? Logic ????)
            modeShouldClose = true; // ?????????? ???????
        }
        else if (newMode == SkillMode.GivePeaceAChance)
        {
            CmdActivateGivePeaceAChance(); // (????? Logic ????)
            modeShouldClose = true; // ?????????? ???????
        }
        else if (newMode == SkillMode.Resurrection)
        {
            CmdActivateResurrectionMode(); // (????? Logic ????)
            modeShouldClose = true; // ?????????? ???????
        }

        // (??????? "??????????" ????? ??????????????)

        // ???????????????????? ?????????????
        if (modeShouldClose)
        {
            activeSkillMode = SkillMode.None;
        }
    }

    // Logic ?????????? "????????" (???????? DuckCard.cs)
    public void HandleDuckCardClick(DuckCard clickedCard)
    {
        if (!isLocalPlayer) return;

        // ??????????????????!
        switch (activeSkillMode)
        {
            case SkillMode.None:
                // ?????????????
                break;

            // --- ?? 3.2 (?????????????????) ---

            case SkillMode.Shoot:
                CmdShootCard(clickedCard.netIdentity);
                // (CmdShootCard ????????????)
                break;

            case SkillMode.TakeAim:
                CmdSpawnTarget(clickedCard.netIdentity);
                CmdSetSkillMode(SkillMode.None); // TakeAim ???????????????? HandleClick ??????????????????
                break;

            case SkillMode.DoubleBarrel:
                CmdDoubleBarrelClick(clickedCard.netIdentity);
                // (CmdDoubleBarrelClick ????????????????????)
                break;

            case SkillMode.QuickShot:
                CmdQuickShotCard(clickedCard.netIdentity);
                // (CmdQuickShotCard ????????????)
                break;

            case SkillMode.Misfire:
                CmdMisfireClick(clickedCard.netIdentity);
                // (CmdMisfireClick ????????????)
                break;

            case SkillMode.TwoBirds:
                CmdTwoBirdsClick(clickedCard.netIdentity);
                // (CmdTwoBirdsClick ????????????????????)
                break;

            case SkillMode.BumpLeft:
                CmdBumpLeftClick(clickedCard.netIdentity);
                // (CmdBumpLeftClick ????????????)
                break;

            case SkillMode.BumpRight:
                CmdBumpRightClick(clickedCard.netIdentity);
                // (CmdBumpRightClick ????????????)
                break;

            case SkillMode.MoveAhead:
                CmdMoveAheadClick(clickedCard.netIdentity);
                // (CmdMoveAheadClick ????????????)
                break;

            case SkillMode.HangBack:
                CmdHangBackClick(clickedCard.netIdentity);
                // (CmdHangBackClick ????????????)
                break;

            case SkillMode.FastForward:
                CmdFastForwardClick(clickedCard.netIdentity);
                // (CmdFastForwardClick ????????????)
                break;

            case SkillMode.DisorderlyConduckt:
                CmdDisorderlyClick(clickedCard.netIdentity);
                // (DisorderlyConduckt ????? state 2-click ??? ?????????????)
                break;

            // --- (??????????????????????????) ---
            case SkillMode.LineForward:
            case SkillMode.DuckShuffle:
            case SkillMode.GivePeaceAChance:
            case SkillMode.Resurrection:
                // ???????????????? ????????????????????? CmdSetSkillMode
                // ???????????????????????
                break;

            default:
                Debug.LogWarning($"Unhandled SkillMode in HandleDuckCardClick: {activeSkillMode}");
                break;
        }
    }


    //////////////////////////////////////////  Barrier ////////////////////////////////////////////////////////////////////


    // ????????: ???? barrier ????? ??? local player ????????????????????
    [Client]
    private static void OnBarrierGo_Client()
    {
        if (DeferInitialDealToBarrier && localInstance != null)
            localInstance.StartAutoDrawIfLocal();
    }

    // ??? event ??? GameplayLoadCoordinator ?????????????
    [Server]
    private static void TryBindBarrierServer()
    {
        if (s_barrierHooksBoundServer) return;
        s_barrierHooksBoundServer = true;
        GameplayLoadCoordinator.BarrierGoServer += OnBarrierGo_Server;
    }

    [Client]
    private static void TryBindBarrierClient()
    {
        if (s_barrierHooksBoundClient) return;
        s_barrierHooksBoundClient = true;
        GameplayLoadCoordinator.BarrierGoClient += OnBarrierGo_Client;
    }

    // ?????????: ??????????????????????? local player
    [Client]
    private void StartAutoDrawIfLocal()
    {
        // if (isLocalPlayer)
        //     StartCoroutine(AutoDrawCards());
    }



    public override void OnStartClient()
    {
        base.OnStartClient();

        TryBindBarrierClient();

        // ????? Main Canvas
        Transform mainCanvas = GameObject.Find("Main Canvas")?.transform;
        if (mainCanvas == null)
        {
            Debug.LogError("[PlayerManager.OnStartClient] ? 'Main Canvas' not found");
            return;
        }

        // ?? root UI ??????? "Image" (??????????? Main Canvas)
        Transform uiRoot = FindChildRecursive(mainCanvas, "Image");
        if (uiRoot == null)
        {
            Debug.LogError("[PlayerManager.OnStartClient] ? 'Image' root not found under Main Canvas");
            return;
        }

        // ????????? ?
        DuckZone = FindChildRecursive(uiRoot, "DuckZone")?.gameObject;
        DropZone = FindChildRecursive(uiRoot, "DropZone")?.gameObject;
        TargetZone = FindChildRecursive(uiRoot, "TargetZone")?.gameObject;
        EnemyArea = FindChildRecursive(uiRoot, "EnemyArea")?.gameObject;

        var ni = GetComponent<NetworkIdentity>();
        if (ni != null && ni.isOwned)
        {
            // ?????? local player
            PlayerArea = FindChildRecursive(uiRoot, "PlayerArea")?.gameObject;
            localInstance = this;
        }

        if (DuckZone == null) Debug.LogError("[PlayerManager.OnStartClient] ? DuckZone not found");
        if (DropZone == null) Debug.LogError("[PlayerManager.OnStartClient] ? DropZone not found");
        if (TargetZone == null) Debug.LogError("[PlayerManager.OnStartClient] ? TargetZone not found");
        if (EnemyArea == null) Debug.LogError("[PlayerManager.OnStartClient] ? EnemyArea not found");
        if (ni != null && ni.isOwned && PlayerArea == null)
            Debug.LogError("[PlayerManager.OnStartClient] ? PlayerArea not found for local player");

        Debug.Log("[PlayerManager.OnStartClient] ? Zones found successfully");

        CacheEnemySlotsFromScene();
        RecomputeLocalLayoutBySeat();
    }


    public override void OnStopClient()
    {
        base.OnStopClient();
        // ?????????????? ? ????????????????????
        RecomputeLocalLayoutBySeat();
    }


    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        localInstance = this;

    }

    private void OnDestroy()
    {
        var networkIdentity = GetComponent<NetworkIdentity>();
        if (networkIdentity.isOwned)
        {
            StopAllCoroutines();
        }
    }

    // Helper ??/????????????????
    // ??/??? EnemyArea1..5 ??? Scene
    private void CacheEnemySlotsFromScene()
    {
        Transform mainCanvas = GameObject.Find("Main Canvas")?.transform;
        if (mainCanvas == null)
        {
            Debug.LogWarning("[CacheEnemySlots] 'Main Canvas' not found!");
            s_enemySlots = null;
            return;
        }

        Transform uiRoot = null;
        if (mainCanvas != null)
            uiRoot = FindChildRecursive(mainCanvas, "Image");

        Transform root = null;
        if (uiRoot != null)
            root = FindChildRecursive(uiRoot, enemiesAreaRootName);

        if (root == null && mainCanvas != null)
            root = FindChildRecursive(mainCanvas, enemiesAreaRootName);

        if (root == null)
        {
            var fallback = GameObject.Find(enemiesAreaRootName);
            root = fallback != null ? fallback.transform : null;
        }

        if (root == null)
        {
            Debug.LogWarning($"[CacheEnemySlots] '{enemiesAreaRootName}' not found!");
            s_enemySlots = null;
            return;
        }
        else
        {
            Debug.Log($"[CacheEnemySlots] Using root '{root.name}' for enemy slots.");
        }

        if (s_enemySlots == null || s_enemySlots.Length != 5)
            s_enemySlots = new Transform[5];

        for (int i = 0; i < s_enemySlots.Length; i++)
        {
            string childName = $"{enemySlotPrefix}{i + 1}";
            var child = FindChildRecursive(root, childName);
            if (child == null)
            {
                string altChild = $"{enemySlotPrefix}{i}";
                child = FindChildRecursive(root, altChild);
            }

            s_enemySlots[i] = child;
            if (child == null)
                Debug.LogWarning($"[CacheEnemySlots] Slot '{childName}' not found!");
            else
                Debug.Log($"[CacheEnemySlots] Slot '{childName}' -> {child.name}");
        }
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }

    /// ??? Transform ?????????????? rel (0..5)
    /// rel=0 -> PlayerArea (??? local), rel=1..5 -> EnemyArea1..5
    private Transform GetSlotByRelIndex(int rel)
    {
        if (rel == 0)
        {
            // ?????? local ????????: ??? PlayerArea ???????????? OnStartClient
            return PlayerArea != null ? PlayerArea.transform : null;
        }

        // ????????????????? EnemyArea1..5 ????
        if (s_enemySlots == null || s_enemySlots.Any(t => t == null))
            CacheEnemySlotsFromScene();

        int idx = rel - 1; // 1..5 -> 0..4
        if (s_enemySlots != null && idx >= 0 && idx < s_enemySlots.Length)
            return s_enemySlots[idx];

        return null;
    }


    // ????? slot ??? PlayerManager (???????????) ???????? seatIndex ???????? (?????? local)
    [Client]
    private void RecomputeLocalLayoutBySeat()
    {
        // ?? local seat
        var owned = FindObjectsOfType<PlayerManager>()
            .FirstOrDefault(p =>
            {
                var ni = p.GetComponent<NetworkIdentity>();
                return ni != null && ni.isOwned;
            });

        if (owned == null)
        {
            // ????? local ?????? ???????????
            StartCoroutine(_RecomputeNextFrame());
            return;
        }

        int localSeat = Mathf.Clamp(owned.seatIndex, 0, 5);

        // ?????????? (2..6)
        var all = FindObjectsOfType<PlayerManager>().ToList();
        int total = Mathf.Clamp(all.Count, 2, 6);

        // ???????????????????
        s_remoteSlotIndex.Clear();

        foreach (var pm in all)
        {
            var ni = pm.GetComponent<NetworkIdentity>();
            if (ni != null && ni.isOwned)
            {
                // ?????? ? PlayerArea ???? (rel=0)
                pm.PlayerArea = GameObject.Find("PlayerArea");
                continue;
            }

            // ???????? ? ????? rel ????????? EnemyArea1..5
            int rel = ((pm.seatIndex - localSeat) % 6 + 6) % 6; // safe mod
            if (rel == 0) rel = 1; // ??????? edge (????????????? seatIndex ????????)

            var t = GetSlotByRelIndex(rel);
            if (t != null)
            {
                pm.EnemyArea = t.gameObject;
                // ????????????????????? (????????? anim/???????? UI)
                s_remoteSlotIndex[pm.netId] = rel - 1; // 0..4
            }
            else
            {
                // fallback ????
                pm.EnemyArea = GameObject.Find("EnemyArea");
            }
        }

        // (??????) ?????????
        // Debug.Log($"[Layout] localSeat={localSeat}, total={total}");
        // foreach (var pm in all) Debug.Log($" [Seat] netId={pm.netId} seat={pm.seatIndex} rel={((pm.seatIndex - localSeat + 6) % 6)}");
    }

    private IEnumerator _RecomputeNextFrame()
    {
        yield return null;
        RecomputeLocalLayoutBySeat();
    }

    // ??? Transform ????????????????????????? PlayerManager ?????? (?????????????? null)
    private Transform GetMyEnemySlot()
    {
        if (s_enemySlots == null) return null;

        var ni = GetComponent<NetworkIdentity>();
        if (ni != null && s_remoteSlotIndex.TryGetValue(ni.netId, out int idx))
        {
            if (idx >= 0 && idx < s_enemySlots.Length)
                return s_enemySlots[idx];
        }
        return null;
    }

    public static Transform TryGetEnemySlotForNetId(uint netId)
    {
        if ((s_enemySlots == null || s_enemySlots.Any(t => t == null)) && localInstance != null)
            localInstance.CacheEnemySlotsFromScene();

        if (s_enemySlots == null)
            return null;

        if (s_remoteSlotIndex.TryGetValue(netId, out int idx))
        {
            if (idx >= 0 && idx < s_enemySlots.Length)
            {
                var slot = s_enemySlots[idx];
                Debug.Log($"[TryGetEnemySlot] netId={netId} -> slotIndex={idx} ({(slot != null ? slot.name : "NULL")})");
                return slot;
            }
        }
        return null;
    }

    private GameObject FindUIObject(string childName)
    {
        var direct = GameObject.Find(childName);
        if (direct != null)
        {
            Debug.Log($"[FindUIObject] Found '{childName}' via GameObject.Find");
            return direct;
        }

        Transform mainCanvas = GameObject.Find("Main Canvas")?.transform ?? GameObject.Find("Canvas")?.transform;
        if (mainCanvas == null)
        {
            Debug.LogWarning($"[FindUIObject] Could not find canvas while searching for '{childName}'");
            return null;
        }

        var target = FindChildRecursive(mainCanvas, childName);
        if (target != null)
        {
            Debug.Log($"[FindUIObject] Found '{childName}' under canvas hierarchy ({target.name})");
            return target.gameObject;
        }

        Debug.LogWarning($"[FindUIObject] '{childName}' not found under canvas hierarchy");
        return null;
    }

    private void LogZoneStatus(string zoneName, GameObject go)
    {
        Debug.Log($"[OnStartClient] {zoneName} => {(go != null ? go.name : "NULL")}");
    }

    // server: ??? seatIndex ????????????? 0..5
    [Server]
    private void EnsureSeatIndexAssigned()
    {
        if (seatIndex >= 0) return;

        // ??????????????????????????
        var used = new HashSet<int>();
        foreach (var pm in FindObjectsOfType<PlayerManager>())
            if (pm.seatIndex >= 0) used.Add(pm.seatIndex);

        // ????????? 0..5
        for (int i = 0; i < 6; i++)
            if (!used.Contains(i)) { seatIndex = i; return; }

        // ???????
        seatIndex = 5;
    }


    // ??????????? �?????? index ?????????????????�
    private static readonly string[] DUCK_KEYS_BY_INDEX =
    {
    "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    // ????????????? index ????????????????
    };

    private static string ColorIndexToDuckKey(int idx)
    {
        return (idx >= 0 && idx < DUCK_KEYS_BY_INDEX.Length) ? DUCK_KEYS_BY_INDEX[idx] : null;
    }

    [Server]
    private static HashSet<string> Server_GetSelectedDuckKeysFromLobby()
    {
        var keys = new HashSet<string>();
        // ??????? PlayerManager ?????? (??????? PlayerManager ??/?????? duckColorIndex ????????????)
        foreach (var pm in FindObjectsOfType<PlayerManager>())
        {
            string key = ColorIndexToDuckKey(pm.duckColorIndex);
            if (!string.IsNullOrEmpty(key)) keys.Add(key);
        }
        return keys;
    }

    // ??????????? OnBarrierGo_Server() ??????????????????
    [Server]
    private void Server_BeginMatch_AfterBarrier()
    {
        // 1) ???? DuckZone ?????? 6 ??? pool ??? �???????????????�
        RefillDuckZoneIfNeeded();

        // 2) ???????????????????? �??????????�
        Server_PickStarterFromTopDuckCard_AndBuildOrder();

        // (???????????????) ???????????????????? ????:
        // TurnSystem.Server_BeginFirstTurn(s_currentTurnSeat, s_turnOrder);
    }

    [Server]
    private static void Server_PickStarterFromTopDuckCard_AndBuildOrder()
    {
        var any = FindObjectsOfType<PlayerManager>().FirstOrDefault();
        if (any == null || any.DuckZone == null) return;

        // ? ???????????????????????? Transform ????
        var zone = any.DuckZone.transform;

        // ???????????? "?????? Marsh"
        string topKey = null;
        DuckCard topDuck = null;
        for (int i = zone.childCount - 1; i >= 0; i--)
        {
            var tr = zone.GetChild(i);
            if (tr.TryGetComponent(out DuckCard dc))
            {
                var k = ExtractDuckKeyFromCard(dc.gameObject);
                if (!string.IsNullOrEmpty(k) && !string.Equals(k, "Marsh", StringComparison.OrdinalIgnoreCase))
                {
                    topDuck = dc;
                    topKey = k;
                    break;
                }
            }
        }

        var players = FindObjectsOfType<PlayerManager>().ToList();
        int total = Mathf.Clamp(players.Count, 2, 6);

        // ?????????????? Marsh ???????????????? ? fallback ?????????????????
        PlayerManager starter = null;
        if (!string.IsNullOrEmpty(topKey))
            starter = players.FirstOrDefault(p => ColorIndexToDuckKey(p.duckColorIndex) == topKey);

        if (starter == null)
            starter = players.OrderBy(p => p.seatIndex).FirstOrDefault();

        s_currentTurnSeat = (starter != null) ? starter.seatIndex : 0;
        if (any != null)
        {
            any._currentTurnSeatNet = s_currentTurnSeat; // ??????????????? ? Mirror sync ????? client ? hook ?????? static
        }

        // ???????????? (????????????? ?????????? +i ???? -i)
        s_turnOrder.Clear();
        for (int i = 0; i < total; i++)
        {
            int seat = (s_currentTurnSeat + i) % total;
            s_turnOrder.Add(seat);
        }

        Debug.Log($"[Turn] Starter seat = {s_currentTurnSeat}, order = {string.Join(",", s_turnOrder)}");

        // ? ???????????????????????????????????????????????????? 1..6
        var caller = any; // ???????????? PM ????????????????????
        if (caller != null)
            caller.RpcRecomputeLayoutAllClients();  // <<< ???????????
    }

    [ClientRpc]
    public void RpcRecomputeLayoutAllClients()
    {
        if (!NetworkClient.active) return;
        try
        {
            RecomputeLocalLayoutBySeat();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcRecomputeLayoutAllClients] ขัดข้อง: {ex}");
        }
    }

    // ???????? GameObject ????? ? DuckKey ("DuckBlue"...)
    private static string ExtractDuckKeyFromCard(GameObject go)
    {
        var name = go.name.Replace("(Clone)", "").Trim();

        // Marsh ???????????????????????
        if (name.IndexOf("Marsh", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "Marsh";

        foreach (var key in DUCK_KEYS_BY_INDEX)
        {
            if (name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return key;
        }
        return null;
    }


    // ????? server (???????????? server/host) � cache ???????????????
    private Transform _cachedDuckZone;
    [Server]
    private Transform GetSceneDuckZone()
    {
        // ???????? cache ??????? valid ??????
        if (_cachedDuckZone != null && _cachedDuckZone.gameObject.scene.IsValid() && _cachedDuckZone.gameObject.scene.isLoaded)
            return _cachedDuckZone;

        // ?????????? DuckZone ?????????? ??? valid ??????
        if (DuckZone != null)
        {
            var t = DuckZone.transform;
            if (t != null && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded)
            {
                _cachedDuckZone = t;
                return t;
            }
        }

        // ??????????????????
        var go = GameObject.Find("DuckZone");
        if (go != null)
        {
            _cachedDuckZone = go.transform;
            return _cachedDuckZone;
        }

        // ??????
        return null;
    }

    // ?????????????? scene ?????????? (unload/load) ?????????? cache
    [Server]
    private void ClearZoneCaches()
    {
        _cachedDuckZone = null;
    }

    [Server]
    private void Server_ResequenceDuckZoneColumns()
    {
        var dz = GetSceneDuckZone();
        if (dz == null) return;

        // ????????????????????? DuckZone
        var list = new List<DuckCard>();
        foreach (Transform t in dz)
        {
            var dc = t.GetComponent<DuckCard>();
            if (dc != null) list.Add(dc);
        }

        // ?????????????????? X ???????? (????????? siblingIndex ?????)
        list.Sort((a, b) =>
        {
            var ra = a.GetComponent<RectTransform>();
            var rb = b.GetComponent<RectTransform>();
            float ax = ra ? ra.anchoredPosition.x : a.transform.GetSiblingIndex();
            float bx = rb ? rb.anchoredPosition.x : b.transform.GetSiblingIndex();
            return ax.CompareTo(bx);
        });

        // ???????????????????
        for (int i = 0; i < list.Count; i++)
        {
            var dc = list[i];
            // ?????????? (DuckZone), ??????? (0), ??????????? i
            dc.ServerAssignToZone(ZoneKind.DuckZone, 0, i);
        }
    }

    [Server]
    private void Server_DestroyAllTargetsFor(uint duckNetId)
    {
        // ????????: TargetMarker
        var markers = FindObjectsOfType<TargetMarker>();
        foreach (var m in markers)
            if (m != null && m.FollowDuckNetId == duckNetId)
                NetworkServer.Destroy(m.gameObject);

        // ?????: ???????? TargetFollow
        var follows = FindObjectsOfType<TargetFollow>();
        foreach (var f in follows)
            if (f != null && f.targetNetId == duckNetId)
                NetworkServer.Destroy(f.gameObject);
    }




    // ====(???? server helpers) 

    [Server]
    private Transform GetSceneDropZone() => GameObject.Find("DropZone")?.transform;

    [Server]
    private void Server_PlaceDuckInZone(GameObject card, ZoneKind zone, int row = 0, int col = -1)
    {
        if (card == null) return;
        var dc = card.GetComponent<DuckCard>();
        if (dc == null) return;

        Transform parent = null;
        switch (zone)
        {
            case ZoneKind.DuckZone: parent = GetSceneDuckZone(); break;
            case ZoneKind.DropZone: parent = GetSceneDropZone(); break;
            case ZoneKind.PlayerArea: parent = GameObject.Find("PlayerArea")?.transform; break;
            default: break;
        }

        if (col < 0 && parent != null) col = parent.childCount;

        // ??????????????????/??????? (DuckCard ????? parent ???? server+client ???? SyncVar hook)
        dc.ServerAssignToZone(zone, row, col);
    }


    // ========================
    // OnStartServer, Deal Card
    // ========================
    public override void OnStartServer()
    {
        base.OnStartServer();

        // 1) ??? Barrier ???????????????
        TryBindBarrierServer();

        // 2) ??????????? + ???? Action ???????? (??????????????? Barrier)
        EnsureSeatIndexAssigned();
        InitializeActionCardPool();

        // 3) ???? Prefab ??? Action Card ??????????????????????
        actionCardPrefabMap = new Dictionary<string, GameObject>();
        if (resurrectionPrefab != null) actionCardPrefabMap["Resurrection"] = resurrectionPrefab;
        if (duckAndCoverPrefab != null) actionCardPrefabMap["DuckAndCover"] = duckAndCoverPrefab;
        foreach (var prefab in actionCardPrefabList)
            if (prefab != null && !actionCardPrefabMap.ContainsKey(prefab.name))
                actionCardPrefabMap[prefab.name] = prefab;

        // ? ??????????????????/???????? DuckZone ??????
        CmdSyncDuckCards();
    }


    [Server]
    private static HashSet<string> Server_GetSelectedDuckKeysFromRoom()
    {
        var keys = new HashSet<string>();
        // ??????? PlayerManager (GamePlayer) ????????????????????????????
        foreach (var pm in FindObjectsOfType<PlayerManager>())
        {
            int idx = pm.duckColorIndex;
            if (idx >= 0 && idx < DUCK_KEYS_BY_INDEX.Length)
                keys.Add(DUCK_KEYS_BY_INDEX[idx]);
        }

        // log ??????? + index ???????????????
        foreach (var pm in FindObjectsOfType<PlayerManager>())
            Debug.Log($"[Deck][SeenInGameplay] netId={pm.netId} seat={pm.seatIndex} colorIndex={pm.duckColorIndex}");

        // log ??????? key
        Debug.Log("[Deck][SelectedFromRoom] " + string.Join(",", keys));

        return keys;
    }

    [Server]
    private static void OnBarrierGo_Server()
    {
        if (s_matchStarted) return;
        s_matchStarted = true;

        var players = FindObjectsOfType<PlayerManager>().ToList();
        if (players.Count == 0) return;

        var host = players.First();

        // 1) Build duck deck from lobby selections (guarantee Marsh + fallback color)
        var duckPrefabs = new Dictionary<string, GameObject>
        {
            { "DuckBlue",   host.DuckBluePrefab   },
            { "DuckOrange", host.DuckOrangePrefab },
            { "DuckPink",   host.DuckPinkPrefab   },
            { "DuckGreen",  host.DuckGreenPrefab  },
            { "DuckYellow", host.DuckYellowPrefab },
            { "DuckPurple", host.DuckPurplePrefab },
            { "Marsh",      host.MarshPrefab      },
        };

        var selected = Server_GetSelectedDuckKeysFromRoom();
        selected.Add("Marsh");
        if (selected.SetEquals(new[] { "Marsh" }))
            selected.Add("DuckBlue");

        var selectedPrefabs = duckPrefabs
            .Where(kv => selected.Contains(kv.Key) && kv.Value != null)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        CardPoolManager.Initialize(selectedPrefabs, initialCount: 5);

        // 2) Ensure the shared DuckZone is filled before we begin
        host.RefillDuckZoneIfNeeded();

        // 3) Randomize the starting seat / order from the newly built deck
        Server_PickStarterFromTopDuckCard_AndBuildOrder();

        // 4) Deal three action cards to every connected player
        foreach (var pm in players)
        {
            var conn = pm.connectionToClient;
            if (conn == null) continue;

            for (int i = 0; i < 3; i++)
                host.Server_DrawActionCardFor(conn, pm.netId);
        }

        Debug.Log("[OnBarrierGo_Server] Deck prepared, turn order computed, and 3 action cards dealt to all players");
    }


    [Server]
    private void InitializeActionCardPool()
    {
        actionCardPool.Clear();

        actionCardPool.Add("Shoot", 10);
        // actionCardPool.Add("QuickShot", 10);
        actionCardPool.Add("TekeAim", 10);
        actionCardPool.Add("DoubleBarrel", 10);
        // actionCardPool.Add("Misfire", 10);
        // actionCardPool.Add("TwoBirds", 3);
        // actionCardPool.Add("BumpLeft", 3);
        // actionCardPool.Add("BumpRight", 3);
        actionCardPool.Add("LineForward", 3);
        // actionCardPool.Add("MoveAhead", 3);
        // actionCardPool.Add("HangBack", 3);
        // actionCardPool.Add("FastForward", 3);
        // actionCardPool.Add("DisorderlyConduckt", 3);
        // actionCardPool.Add("DuckShuffle", 3);
        // actionCardPool.Add("GivePeaceAChance", 3);
        // actionCardPool.Add("Resurrection", 3);

    }
    private int GetDuckCardCountInDuckZone()
    {
        if (DuckZone == null) return 0;

        int count = 0;
        foreach (Transform child in DuckZone.transform)
        {
            // ?? DuckCard component ???
            DuckCard duck = child.GetComponent<DuckCard>();
            if (duck != null)
            {
                count++;
            }
        }
        return count;
    }

    // ===== Helper: ?????????????????? (???? Server) =====
    [Server]
    private int Server_CountCardsInZone(ZoneKind z)
    {
        int c = 0;
        foreach (var dc in FindObjectsOfType<DuckCard>())
            if (dc.zone == z) c++;
        return c;
    }

    // ===== ???? DuckZone ?????? (??????????? ???????) =====
    [Server]
    private void RefillDuckZoneIfNeeded()
    {
        int current = Server_CountCardsInZone(ZoneKind.DuckZone);
        if (current < 0) { Debug.LogError("[RefillDuckZoneIfNeeded] DuckZone count invalid."); return; }
        if (current >= 6) return;
        if (!CardPoolManager.HasCards()) { Debug.LogWarning("[RefillDuckZoneIfNeeded] No cards left in pool."); return; }

        int col = current; // ????????????????????????????
        while (col < 6 && CardPoolManager.HasCards())
        {
            var card = CardPoolManager.DrawRandomCard();   // ? ?????? parent
            if (card == null) break;

            var dc = card.GetComponent<DuckCard>();
            if (dc == null) { UnityEngine.Object.Destroy(card); continue; }

            // ???? Zone/Row/Column ???? SyncVar ???? Spawn
            dc.ServerAssignToZone(ZoneKind.DuckZone, 0, col);

            // ???? Spawn ? SyncVar ????????????? client ??????????
            NetworkServer.Spawn(card);

            col++;
        }
    }






    [Command(requiresAuthority = false)]
    public void CmdSyncDuckCards()
    {
        if (DuckZone != null && DuckZone.transform.childCount <= 6)
        {
            RpcSyncDuckCards();
        }
        else
        {
            RpcSyncDuckCards();
        }
    }

    [ClientRpc]
    void RpcSyncDuckCards()
    {
        if (!NetworkClient.active) return;
        if (DuckZone == null) return;

        try
        {
            // จัด parent และเรียงตาม ColNet ให้ตรงทุก client
            var ducks = new System.Collections.Generic.List<DuckCard>();
            foreach (Transform child in DuckZone.transform)
            {
                if (child == null) continue;
                child.SetParent(DuckZone.transform, false);
                var dc = child.GetComponent<DuckCard>();
                if (dc != null) ducks.Add(dc);
            }

            ducks.Sort((a, b) => a.ColNet.CompareTo(b.ColNet));
            for (int i = 0; i < ducks.Count; i++)
            {
                ducks[i].transform.SetSiblingIndex(i);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcSyncDuckCards] ขัดข้อง: {ex}");
        }
    }

    // ===== ?????????????: ?????????? 6 ?? ?????? ZoneKind + SyncVar =====
    [Server]
    private IEnumerator DealDuckCardsWithDelay()
    {
        // ?????? Mirror/???????????? ?
        yield return new WaitForSeconds(5f);

        int col = Server_CountCardsInZone(ZoneKind.DuckZone);
        if (col < 0) { Debug.LogError("[DealDuckCardsWithDelay] DuckZone count invalid."); yield break; }

        // ????????? 6 ???????????
        while (col < 6 && CardPoolManager.HasCards())
        {
            var card = CardPoolManager.DrawRandomCard();   // ? ?????? parent
            if (card == null) break;

            var dc = card.GetComponent<DuckCard>();
            if (dc == null) { UnityEngine.Object.Destroy(card); continue; }

            // ??????? SyncVar ???? Spawn (??? late-joiner ????????????????????????)
            dc.ServerAssignToZone(ZoneKind.DuckZone, 0, col);

            NetworkServer.Spawn(card);

            col++;
            yield return null; // ??????????? UI/hook ????????? ?
        }
    }



    [Server]
    private GameObject GetRandomCardFromPool()
    {
        if (cardPool.Count == 0)
        {
            Debug.LogWarning("GetRandomCardFromPool: cardPool is empty!");
            return null;
        }

        // ????? list ??????????????????? (value > 0)
        List<GameObject> availableCards = new List<GameObject>();
        foreach (var kvp in cardPool)
        {
            if (kvp.Value > 0)
                availableCards.Add(kvp.Key);
        }

        if (availableCards.Count == 0)
        {
            Debug.LogWarning("GetRandomCardFromPool: No card left in sub-pool!");
            return null;
        }

        int randomIndex = Random.Range(0, availableCards.Count);
        GameObject selectedCard = availableCards[randomIndex];

        // ?? stock
        cardPool[selectedCard] -= 1;

        // ?????????? ???????? dictionary ?????
        if (cardPool[selectedCard] <= 0)
        {
            cardPool.Remove(selectedCard);
        }

        // ????? Log ????????????????????????, ?????????????
        Debug.Log($"[GetRandomCardFromPool] Spawned: {selectedCard.name}. Left in that color: {(cardPool.ContainsKey(selectedCard) ? cardPool[selectedCard] : 0)}");

        // ??????????? log ???????????
        LogTotalDuckCounts();

        return selectedCard;
    }


    /// <summary>
    /// ?????????????????: ???????????????????? console
    /// </summary>
    [Server]  // ?????? server ????
    private void LogTotalDuckCounts()
    {
        // 1) ????? pool
        var poolCounts = CardPoolManager.GetAllPoolCounts();
        foreach (var kv in poolCounts)
            Debug.Log($"[PoolCounts] {kv.Key}: {kv.Value}");

        // 2) ??????? DuckZone
        var zoneCounts = new Dictionary<string, int>();
        foreach (Transform child in DuckZone.transform)
        {
            if (child.TryGetComponent(out DuckCard d))
            {
                string key = d.gameObject.name.Replace("(Clone)", "").Trim();
                zoneCounts[key] = zoneCounts.GetValueOrDefault(key) + 1;
            }
        }
        foreach (var kv in zoneCounts)
            Debug.Log($"[ZoneCounts] {kv.Key}: {kv.Value}");

        // 3) ???
        var total = GetTotalDuckCounts();
        foreach (var kv in total)
            Debug.Log($"[TotalCounts] {kv.Key}: {kv.Value}");
    }





    private void ReorderDuckZoneLayout()
    {
        // ????? DuckZone ??????????????
        // ???????????????????? = 150px
        float spacing = 150f;

        foreach (Transform child in DuckZone.transform)
        {
            DuckCard duck = child.GetComponent<DuckCard>();
            if (duck != null)
            {
                // ??????????? RectTransform
                RectTransform rt = child.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // ??? row, column ???????
                    rt.anchoredPosition = new Vector2(duck.Column * spacing, 0f);
                }
            }
        }
    }


    [Server]
    private void ShiftColumnsDown(int shotRow, int shotCol)
    {
        // ????? child ?? DuckZone
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard duck = child.GetComponent<DuckCard>();
            if (duck != null)
            {
                // ??????? row ???????? ??? column > shotCol
                if (duck.Row == shotRow && duck.Column > shotCol)
                {
                    duck.Column -= 1;
                    Debug.Log($"Shifted {duck.name} from col {duck.Column + 1} => {duck.Column}");
                }
            }
        }

        // ????????????? column ????? ???????????????? Layout UI ???? ??????????
        ReorderDuckZoneLayout();
    }




    [Server]
    private void SpawnAndAddCardToDuckZone(GameObject cardPrefab)
    {
        var dz = GetSceneDuckZone();
        if (dz == null) return;

        GameObject card = Instantiate(cardPrefab);   // ?? ?????? parent ????
        NetworkServer.Spawn(card);

        if (card.TryGetComponent<DuckCard>(out var duck))
        {
            int realCount = 0; foreach (Transform t in dz) if (t.GetComponent<DuckCard>() != null) realCount++;
            duck.Row = 0; duck.Column = realCount;   // ??????????
        }

        RpcAddCardToDuckZone(card);                  // ??? parent ??? client
    }


    [ClientRpc]
    private void RpcAddCardToDuckZone(GameObject card)
    {
        if (!NetworkClient.active) return;
        if (card == null)
        {
            Debug.LogWarning("[RpcAddCardToDuckZone] ????????? null ??????????? parent");
            return;
        }

        try
        {
            var dz = GetSceneDuckZone();
            if (dz != null)
                card.transform.SetParent(dz, false);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcAddCardToDuckZone] ขัดข้อง: {ex}");
        }
    }


    private int GetDuckCardCount()
    {
        int count = 0;
        foreach (Transform t in DuckZone.transform)
        {
            if (t.GetComponent<DuckCard>() != null)
                count++;
        }
        return count;
    }



    // ?? ??????????????????? pool
    // ? ???????????????????? pool (??? string ??? GameObject)
    [Server]
    private string GetRandomActionCardFromPool()
    {
        List<string> availableCards = new List<string>();

        foreach (var card in actionCardPool)
        {
            if (card.Value > 0)
            {
                availableCards.Add(card.Key);
            }
        }

        if (availableCards.Count == 0)
        {
            // Debug.LogWarning("?? No action cards left in the pool!");
            return null;
        }

        string selectedCard = availableCards[UnityEngine.Random.Range(0, availableCards.Count)];
        actionCardPool[selectedCard]--;  // ?????????????? pool

        return selectedCard;
    }

    private GameObject GetRandomDuckCardFromPool()
    {
        List<GameObject> availableCards = new List<GameObject>();

        foreach (var card in cardPool)
        {
            if (card.Value > 0)
            {
                availableCards.Add(card.Key);
            }
        }

        if (availableCards.Count == 0)
        {
            Debug.LogWarning("?? No duck cards left in the pool!");
            return null;
        }

        GameObject selectedCard = availableCards[UnityEngine.Random.Range(0, availableCards.Count)];
        cardPool[selectedCard]--; // ??????????????
        return selectedCard;
    }








    // ========================
    // Auto Draw
    // ========================
    public void DrawRandomActionCard()
    {
        string cardName = GetRandomActionCardFromPool(); // ? ?????????? string
        if (cardName == null)
        {
            // Debug.LogWarning("? No action cards left in the pool!");
            return;
        }

        GameObject drawnCard = FindCardPrefabByName(cardName); // ? ?? GameObject ???????
        if (drawnCard == null)
        {
            Debug.LogError($"? Cannot find prefab for card: {cardName}");
            return;
        }

        Debug.Log($"?? Drew action card: {drawnCard.name}");

        // Spawn ????????????????????????
        SpawnAndAddCardToDuckZone(drawnCard);
    }

    // private IEnumerator AutoDrawCards()
    // {
    //     yield return new WaitForSeconds(3f); // ?? 3 ??????????????????

    //     while (true)
    //     {
    //         if (PlayerArea != null && PlayerArea.transform.childCount < 3)
    //         {
    //             CmdDrawActionCard();
    //         }
    //         yield return new WaitForSeconds(1f);
    //     }
    // }

    // ===== Helper: ?????????????????? (????????????????????) =====
    [Server]
    private int Server_CountCardsInZone(ZoneKind z, NetworkConnectionToClient owner)
    {
        if (owner == null) return 0;

        int c = 0;
        foreach (var dc in FindObjectsOfType<DuckCard>())
        {
            // ??????? 1. ????????????????? 2. ???????????????
            if (dc.zone == z && dc.netIdentity != null && dc.netIdentity.connectionToClient == owner)
            {
                c++;
            }
        }
        return c;
    }

    //  Server ??????????????????????? (conn)
    [Server]
    private void Server_DrawActionCardFor(NetworkConnectionToClient conn, uint ownerPMNetId)
    {
        string cardName = GetRandomActionCardFromPool();
        if (string.IsNullOrEmpty(cardName))
        {
            // Debug.LogWarning("? No action cards left in the pool!");
            return;
        }

        GameObject prefab = FindCardPrefabByName(cardName);
        if (prefab == null)
        {
            // Debug.LogError($"? Cannot find prefab for card: {cardName}");
            return;
        }

        GameObject spawnedCard = Instantiate(prefab);
        NetworkServer.Spawn(spawnedCard, conn); // Spawn ?????? Client (conn) ???????????

        // Debug.Log($"?? {conn} drew an action card: {spawnedCard.name}");

        // ตั้งเจ้าของและลำดับในมือแบบกำหนดเองตามจำนวนใบปัจจุบันของผู้เล่น
        var dc = spawnedCard.GetComponent<DuckCard>();
        if (dc != null)
        {
            dc.ownerNetId = ownerPMNetId;
            int handCount = Server_CountCardsInZone(ZoneKind.PlayerArea, conn);
            dc.ServerAssignToZone(ZoneKind.PlayerArea, 0, handCount);
        }

        var spawnedNi = spawnedCard.GetComponent<NetworkIdentity>();
        RpcShowCard(spawnedNi, "Dealt");
    }


    // ? Client ??????????????????? Command
    public void DrawActionCard()
    {
        if (isLocalPlayer)
        {
            CmdDrawActionCard();
        }
    }

    // ? Command ??? Client ?????????????? Server
    [Command]
    public void CmdDrawActionCard()
    {
        // string cardName = GetRandomActionCardFromPool();
        // if (string.IsNullOrEmpty(cardName))
        // {
        //     Debug.LogWarning("? No action cards left in the pool!");
        //     return;
        // }

        // GameObject prefab = FindCardPrefabByName(cardName);
        // if (prefab == null)
        // {
        //     Debug.LogError($"? Cannot find prefab for card: {cardName}");
        //     return;
        // }

        // GameObject spawnedCard = Instantiate(prefab, Vector2.zero, Quaternion.identity);
        // NetworkServer.Spawn(spawnedCard, connectionToClient);

        // Debug.Log($"?? {connectionToClient} drew an action card: {spawnedCard.name}");

        // var spawnedNi = spawnedCard.GetComponent<NetworkIdentity>();
        // RpcShowCard(spawnedNi, "Dealt");
        Server_DrawActionCardFor(connectionToClient, this.netId);
    }


    private GameObject FindCardPrefabByName(string cardName)
    {
        if (actionCardPrefabMap != null && actionCardPrefabMap.TryGetValue(cardName, out var prefab))
            return prefab;

        Debug.LogWarning($"?? Action card �{cardName}� not found!");
        return null;
    }




    public void PlayCard(GameObject card)
    {
        CmdPlayCard(card);
    }

    [Command]
    void CmdPlayCard(GameObject card)
    {
        if (card == null)
        {
            Debug.Log("Trying to play a null card!");
            return;
        }

        if (card.scene.isLoaded)
        {
            var duck = card.GetComponent<DuckCard>();
            if (duck != null)
            {
                Transform dropZoneT = GetSceneDropZone();
                int newCol = dropZoneT != null ? dropZoneT.childCount : 0;
                duck.ServerAssignToZone(ZoneKind.DropZone, 0, newCol);

                // (Log Logic ??????...)
                Debug.Log($"[Server-CmdPlayCard] ?? Moving '{card.name}'...");
                // ...
            }

            RpcShowCard(card.GetComponent<NetworkIdentity>(), "Played");

            // ---------------------------------------------------------
            // ??  ???????????????????????
            // ---------------------------------------------------------
            // (?????) ????????? 1 ???? ??? SyncVar (zone) ???????????????????? ???????????????
            StartCoroutine(DrawNextCardCoroutine(connectionToClient));
        }
        else
        {
            Debug.LogError("Card has been destroyed or not found in the scene.");
        }
    }


    // Helper ??????????????????? Coroutine 
    [Server]
    private IEnumerator DrawNextCardCoroutine(NetworkConnectionToClient conn)
    {
        // ?????????????????????? (DuckCard) ?????? SyncVar 'zone' ???? DropZone ????
        yield return null;

        // (??? Helper ????) ????????????? (PlayerArea) ???????????????
        // (?????????????????????????? 3)
        int cardsInHand = Server_CountCardsInZone(ZoneKind.PlayerArea, conn);
        while (cardsInHand < 3 && CardPoolManager.HasCards())
        {
            uint ownerPMNetId = conn.identity.netId;
            Server_DrawActionCardFor(conn, ownerPMNetId);
            cardsInHand++;
            yield return null; // ????????????????? Spawn/SyncVar ????????????????????????
        }
    }


    private void RemoveCardFromGame(GameObject card)
    {
        if (card == null) return;
        NetworkServer.Destroy(card); // ?? ????????????????????????????????? Client
        Debug.Log($"??? {card.name} has been removed from the game.");
    }




    [ClientRpc]
    void RpcLogToClients(string message)
    {
        if (!NetworkClient.active) return;
        Debug.Log(message);
    }


    // ========================================================
    // Helpers ?????? LineForward/DuckShuffle
    // ========================================================

    [Server]
    private List<int> CollectTargetColumns()
    {
        List<int> targetColumns = new List<int>();
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (NetworkServer.spawned.TryGetValue(tf.targetNetId, out NetworkIdentity duckNi))
            {
                DuckCard duck = duckNi.GetComponent<DuckCard>();
                // (FIX: ??? .zone ???????)
                if (duck != null && duck.zone == ZoneKind.DuckZone && !targetColumns.Contains(duck.ColNet))
                {
                    targetColumns.Add(duck.ColNet);
                }
            }
        }
        targetColumns.Sort();
        return targetColumns;
    }

    [Server]
    private DuckCard FindLeftmostDuck(int row)
    {
        DuckCard result = null;
        int minCol = int.MaxValue;
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard d = netId.GetComponent<DuckCard>();
            // (FIX: ??? .zone ???????)
            if (d != null && d.zone == ZoneKind.DuckZone && d.RowNet == row)
            {
                if (d.ColNet < minCol)
                {
                    minCol = d.ColNet;
                    result = d;
                }
            }
        }
        return result;
    }

    [Server]
    private void RemoveAllTargets()
    {
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            NetworkServer.Destroy(tf.gameObject);
        }
    }

    [Server]
    private List<DuckCard> FindDucksInRow(int row)
    {
        List<DuckCard> list = new List<DuckCard>();
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard d = netId.GetComponent<DuckCard>();
            // (FIX: ??? .zone ???????)
            if (d != null && d.zone == ZoneKind.DuckZone && d.RowNet == row)
            {
                list.Add(d);
            }
        }
        return list;
    }

    [Server]
    private IEnumerator RefillAndRecreateTargets(List<int> oldTargetColumns)
    {
        yield return StartCoroutine(RefillNextFrameLineForward());
        yield return null; // ?? layout

        List<DuckCard> ducks = FindDucksInRow(0); // ????????? 0
        foreach (int col in oldTargetColumns)
        {
            DuckCard duckAtCol = ducks.Find(d => d.ColNet == col);
            if (duckAtCol != null)
            {
                CmdSpawnTargetForDuck(duckAtCol.netId);
            }
        }
    }

    [Server]
    private IEnumerator RefillNextFrameLineForward()
    {
        yield return null;
        RefillDuckZoneIfNeededLineForward();
    }

    [Server]
    private void RefillDuckZoneIfNeededLineForward()
    {
        // (FIX: ????????????? reliable ??? CardPoolManager ?????????
        int currentCount = Server_CountCardsInZone(ZoneKind.DuckZone);
        if (currentCount >= 6) return;
        if (!CardPoolManager.HasCards()) return;

        int needed = 6 - currentCount;
        for (int i = 0; i < needed; i++)
        {
            // (FIX: ??? DrawRandomCard() ?????? Obsolete)
            GameObject newCard = CardPoolManager.DrawRandomCard();
            if (newCard == null) break;

            DuckCard dc = newCard.GetComponent<DuckCard>();
            if (dc != null)
            {
                // (FIX: ??? .zone ???????)
                int nextCol = currentCount + i;
                dc.ServerAssignToZone(ZoneKind.DuckZone, 0, nextCol);
            }

            NetworkServer.Spawn(newCard);
        }
    }

    private IEnumerator DelayedLog()
    {
        yield return null;
    }



    // ========================
    // TekeAim Logic (?????????????????????)
    // ========================
    // ?? (?? CmdActivateTekeAim, RpcEnableTekeAim, CmdDeactivateTekeAim, RpcDeactivateTekeAim)
    // ?? (?? isTekeAimActive)

    // (CmdSpawnTarget ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdSpawnTarget(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null || targetPrefab == null) return;
        var dc = duckCardIdentity.GetComponent<DuckCard>();
        if (dc == null) return;
        if (dc.zone != ZoneKind.DuckZone)
        {
            Debug.LogWarning($"[TakeAim] Ignore target spawn: {duckCardIdentity.name} is in zone {dc.zone}");
            return;
        }
        RemoveTargetFromCard(duckCardIdentity); // ?? Target ?????????????????
        GameObject newTarget = Instantiate(targetPrefab);
        var marker = newTarget.GetComponent<TargetMarker>();
        var tf = newTarget.GetComponent<TargetFollow>();
        if (marker != null)
        {
            marker.ServerAssignToZone(ZoneKind.TargetZone, 0, dc.ColNet);
            marker.FollowDuckNetId = duckCardIdentity.netId;
        }
        if (tf != null) tf.targetNetId = duckCardIdentity.netId;
        NetworkServer.Spawn(newTarget);
    }

    // (Helper ????????????)
    [ClientRpc]
    void RpcSetTargetNetId(NetworkIdentity targetIdentity, NetworkIdentity duckCardIdentity)
    {
        // กัน null/วัตถุหายฝั่ง client
        if (!NetworkClient.active) return;
        if (targetIdentity == null || duckCardIdentity == null || duckCardIdentity.gameObject == null)
        {
            Debug.LogWarning("[RpcSetTargetNetId] target หรือการ์ดเป้าหมายเป็น null ข้ามการตั้งค่า");
            return;
        }

        try
        {
            TargetFollow tf = targetIdentity.GetComponent<TargetFollow>();
            if (tf != null)
            {
                tf.targetNetId = duckCardIdentity.netId;
                tf.ResetTargetTransform();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcSetTargetNetId] ขัดข้อง: {ex}");
        }
    }

    // ========================
    // Shoot Logic (?????????????????????)
    // ========================
    // ?? (?? CmdActivateShoot, RpcActivateShoot, CmdDeactivateShoot, RpcDeactivateShoot)
    // ?? (?? isShootActive)

    // (CmdShootCard ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdShootCard(NetworkIdentity duckCardIdentity)
    {
        // if (!isShootActive) return; // (??????????? bool)
        if (duckCardIdentity == null) return;
        var shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null) return;
        if (!IsCardTargeted(duckCardIdentity)) return;

        int shotRow = shotDuck.RowNet;
        int shotCol = shotDuck.ColNet;
        NetworkServer.Destroy(duckCardIdentity.gameObject);
        Server_DestroyAllTargetsFor(duckCardIdentity.netId);
        Server_ResequenceDuckZoneColumns();

        // FIX: ?????????????
        activeSkillMode = SkillMode.None;

        StartCoroutine(RefillNextFrame());
    }

    [Server]
    IEnumerator RefillNextFrame()
    {
        yield return null;
        RefillDuckZoneIfNeeded();
    }

    // (Helper ????????????)
    bool IsCardTargeted(NetworkIdentity duckCardIdentity)
    {
        // (???? IsCardTargeted ??????...)
        uint duckId = duckCardIdentity.netId;
        var markers = FindObjectsOfType<TargetMarker>();
        foreach (var m in markers)
            if (m != null && m.FollowDuckNetId == duckId)
                return true;
        var follows = FindObjectsOfType<TargetFollow>();
        foreach (var f in follows)
            if (f != null && f.targetNetId == duckId)
                return true;
        return false;
    }


    // ========================
    // DoubleBarrel Logic (?????????????????????)
    // ========================
    // ?? (?? CmdActivateDoubleBarrel, RpcEnableDoubleBarrel, CmdDeactivateDoubleBarrel, RpcDisableDoubleBarrel)
    // ?? (?? isDoubleBarrelActive)

    // (CmdDoubleBarrelClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdDoubleBarrelClick(NetworkIdentity clickedCard)
    {
        // if (!isDoubleBarrelActive) return; // (??????????? bool)
        if (clickedCard == null) return;
        var dc = clickedCard.GetComponent<DuckCard>();
        if (dc == null || dc.zone != ZoneKind.DuckZone)
        {
            Debug.LogWarning($"[DoubleBarrel] Ignore click: {clickedCard.name} is not in DuckZone (zone={dc?.zone})");
            return;
        }

        if (doubleBarrelClickCount == 0)
        {
            firstClickedCard = clickedCard;
            doubleBarrelClickCount = 1;
        }
        else if (doubleBarrelClickCount == 1)
        {
            if (firstClickedCard == null)
            {
                doubleBarrelClickCount = 0;
                return;
            }
            if (!CheckAdjacent(firstClickedCard, clickedCard))
            {
                return;
            }
            CmdSpawnTargetDoubleBarrel_Internal(firstClickedCard);
            CmdSpawnTargetDoubleBarrel_Internal(clickedCard);

            // FIX: ?????????????
            activeSkillMode = SkillMode.None;
            doubleBarrelClickCount = 0;
            firstClickedCard = null;
        }
    }

    [Server]
    private void CmdSpawnTargetDoubleBarrel_Internal(NetworkIdentity duckCardIdentity)
    {
        // (???? CmdSpawnTargetDoubleBarrel_Internal ??????...)
        if (duckCardIdentity == null || targetPrefab == null) return;
        var dc = duckCardIdentity.GetComponent<DuckCard>();
        if (dc == null) return;
        RemoveTargetFromCard(duckCardIdentity); // ????? Target ???????????? ??????????
        GameObject newTarget = Instantiate(targetPrefab);
        var marker = newTarget.GetComponent<TargetMarker>();
        if (marker != null)
        {
            marker.ServerAssignToZone(ZoneKind.TargetZone, 0, dc.ColNet);
            marker.FollowDuckNetId = duckCardIdentity.netId;
        }
        NetworkServer.Spawn(newTarget);
    }

    [Server]
    private bool CheckAdjacent(NetworkIdentity card1, NetworkIdentity card2)
    {
        // (???? CheckAdjacent ??????...)
        if (card1 == null || card2 == null) return false;
        var duck1 = card1.GetComponent<DuckCard>();
        var duck2 = card2.GetComponent<DuckCard>();
        if (duck1 == null || duck2 == null) return false;
        if (duck1.RowNet != duck2.RowNet) return false;
        int diff = Mathf.Abs(duck1.ColNet - duck2.ColNet);
        return diff == 1;
    }

    // ========================
    // Quick Shot Logic (?????????????????????)
    // ========================
    // ?? (?? CmdActivateQuickShot, RpcActivateQuickShot, CmdDeactivateQuickShot, RpcDeactivateQuickShot)
    // ?? (?? isQuickShotActive)

    // (CmdQuickShotCard ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdQuickShotCard(NetworkIdentity duckCardIdentity)
    {
        // if (!isQuickShotActive) return; // (??????????? bool)
        if (duckCardIdentity == null) return;
        DuckCard shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null) return;

        int shotRow = shotDuck.RowNet;
        int shotCol = shotDuck.ColNet;
        NetworkServer.Destroy(duckCardIdentity.gameObject);

        // (?????????)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckCardIdentity.netId)
                NetworkServer.Destroy(target.gameObject);
        }

        // (?????????? ShiftColumnsDown)
        ShiftColumnsDown(shotRow, shotCol);

        // FIX: ?????????????
        activeSkillMode = SkillMode.None;

        StartCoroutine(RefillNextFrame());
    }

    // ========================
    // Misfire Logic (?????????????????????)
    // ========================
    // ?? (?? CmdActivateMisfire, RpcEnableMisfire, CmdDeactivateMisfire, RpcDisableMisfire)
    // ?? (?? isMisfireActive)

    // (CmdMisfireClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdMisfireClick(NetworkIdentity clickedCard)
    {
        // if (!isMisfireActive) return; // (??????????? bool)
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        DuckCard duckComp = clickedCard.GetComponent<DuckCard>();
        if (duckComp == null) return;

        int row = duckComp.RowNet;
        int col = duckComp.ColNet;
        List<NetworkIdentity> neighbors = GetAdjacentDuckCards(row, col);
        if (neighbors.Count == 0) return;

        var randomNeighbor = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
        ShootCardDirect(randomNeighbor); // (??? Helper ???)

        // (?????????????)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var t in allTargets)
        {
            if (t.targetNetId == clickedCard.netId)
                NetworkServer.Destroy(t.gameObject);
        }

        // FIX: ?????????????
        activeSkillMode = SkillMode.None;

        StartCoroutine(RefillNextFrame());
    }

    private List<NetworkIdentity> GetAdjacentDuckCards(int row, int col)
    {
        // (???? GetAdjacentDuckCards ??????... ?????????????? NetworkServer.spawned)
        List<NetworkIdentity> results = new List<NetworkIdentity>();
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard duck = netId.GetComponent<DuckCard>();
            if (duck == null || duck.zone != ZoneKind.DuckZone) continue;
            if (duck.RowNet == row && Mathf.Abs(duck.ColNet - col) == 1)
            {
                results.Add(netId);
            }
        }
        return results;
    }

    private void ShootCardDirect(NetworkIdentity duckNi)
    {
        // (???? ShootCardDirect ??????...)
        if (duckNi == null) return;
        NetworkServer.Destroy(duckNi.gameObject);
        // (?????????)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckNi.netId)
                NetworkServer.Destroy(target.gameObject);
        }
        DuckCard dc = duckNi.GetComponent<DuckCard>();
        if (dc != null)
        {
            ShiftColumnsDown(dc.RowNet, dc.ColNet); // (?????????? Helper ???)
        }
    }

    // ========================
    // TwoBirds Logic (Refactored)
    // ========================
    // ?? (?? CmdActivateTwoBirds, RpcEnableTwoBirds, CmdDeactivateTwoBirds, RpcDisableTwoBirds)
    // ?? (?? isTwoBirdsActive)

    // (CmdTwoBirdsClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdTwoBirdsClick(NetworkIdentity clickedCard)
    {
        // if (!isTwoBirdsActive) return; // (??????????? bool)
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        var dcClicked = clickedCard.GetComponent<DuckCard>();
        if (dcClicked == null || dcClicked.zone != ZoneKind.DuckZone)
        {
            Debug.LogWarning($"[TwoBirds] Ignore click: {clickedCard.name} not in DuckZone (zone={dcClicked?.zone})");
            return;
        }

        if (twoBirdsClickCount == 0)
        {
            firstTwoBirdsCard = clickedCard;
            twoBirdsClickCount = 1;
        }
        else if (twoBirdsClickCount == 1)
        {
            bool canShootBoth = false;
            if (firstTwoBirdsCard != null)
                canShootBoth = CheckAdjacentTwoBirds(firstTwoBirdsCard, clickedCard);

            if (canShootBoth)
            {
                DuckCard dc1 = firstTwoBirdsCard.GetComponent<DuckCard>();
                DuckCard dc2 = clickedCard.GetComponent<DuckCard>();
                if (dc1 == null || dc2 == null) { /* ... */ }
                int row1 = dc1.RowNet, col1 = dc1.ColNet;
                int row2 = dc2.RowNet, col2 = dc2.ColNet;
                NetworkServer.Destroy(firstTwoBirdsCard.gameObject);
                NetworkServer.Destroy(clickedCard.gameObject);
                RemoveTargetFromCard(firstTwoBirdsCard);
                RemoveTargetFromCard(clickedCard);
                if (col1 > col2) { ShiftColumnsDown(row1, col1); ShiftColumnsDown(row2, col2); }
                else { ShiftColumnsDown(row2, col2); ShiftColumnsDown(row1, col1); }
                StartCoroutine(RefillNextFrame());
            }
            else
            {
                if (firstTwoBirdsCard != null)
                {
                    DuckCard dc1 = firstTwoBirdsCard.GetComponent<DuckCard>();
                    if (dc1 != null)
                    {
                        int row1 = dc1.RowNet, col1 = dc1.ColNet;
                        NetworkServer.Destroy(firstTwoBirdsCard.gameObject);
                        RemoveTargetFromCard(firstTwoBirdsCard);
                        ShiftColumnsDown(row1, col1);
                        StartCoroutine(RefillNextFrame());
                    }
                }
            }

            // FIX: ?????????????
            activeSkillMode = SkillMode.None;
            twoBirdsClickCount = 0;
            firstTwoBirdsCard = null;
        }
    }

    [Server]
    private bool CheckAdjacentTwoBirds(NetworkIdentity card1, NetworkIdentity card2)
    {
        // (???? CheckAdjacentTwoBirds ??????????...)
        DuckCard dc1 = card1.GetComponent<DuckCard>();
        DuckCard dc2 = card2.GetComponent<DuckCard>();
        if (dc1 == null || dc2 == null) return false;
        if (dc1.RowNet == dc2.RowNet && Mathf.Abs(dc1.ColNet - dc2.ColNet) == 1)
            return true;
        return false;
    }

    [Server]
    private void RemoveTargetFromCard(NetworkIdentity duckNi)
    {
        if (duckNi == null) return;
        uint targetId = duckNi.netId;

        // ลบ TargetFollow ที่ชี้มาที่การ์ดนี้ทุกอัน
        foreach (var tf in FindObjectsOfType<TargetFollow>())
            if (tf != null && tf.targetNetId == targetId)
                NetworkServer.Destroy(tf.gameObject);

        // ลบ TargetMarker ที่ชี้มาที่การ์ดนี้ทุกอัน (ใน TargetZone)
        foreach (var mk in FindObjectsOfType<TargetMarker>())
            if (mk != null && mk.FollowDuckNetId == targetId)
                NetworkServer.Destroy(mk.gameObject);
    }

    // ========================
    // BumpLeft Logic (Refactored)
    // ========================
    // ?? (?? CmdActivateBumpLeft, RpcEnableBumpLeft, CmdDeactivateBumpLeft, RpcDisableBumpLeft)
    // ?? (?? isBumpLeftActive)

    // (CmdBumpLeftClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdBumpLeftClick(NetworkIdentity clickedCard)
    {
        // if (!isBumpLeftActive) return; // (??????????? bool)
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        DuckCard duck = clickedCard.GetComponent<DuckCard>();
        if (duck == null) return;
        int curRow = duck.RowNet, curCol = duck.ColNet;
        DuckCard leftDuck = FindDuckAt(curRow, curCol - 1);
        if (leftDuck == null) return;
        MoveTargetFromTo(clickedCard, leftDuck.GetComponent<NetworkIdentity>());

        // FIX: ?????????????
        activeSkillMode = SkillMode.None;
    }

    // ========================
    // BumpRight Logic (Refactored)
    // ========================
    // ?? (?? CmdActivateBumpRight, RpcEnableBumpRight, CmdDeactivateBumpRight, RpcDisableBumpRight)
    // ?? (?? isBumpRightActive)

    // (CmdBumpRightClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdBumpRightClick(NetworkIdentity clickedCard)
    {
        // if (!isBumpRightActive) return; // (??????????? bool)
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        DuckCard duck = clickedCard.GetComponent<DuckCard>();
        if (duck == null) return;
        int curRow = duck.RowNet, curCol = duck.ColNet;
        DuckCard rightDuck = FindDuckAt(curRow, curCol + 1);
        if (rightDuck == null) return;
        MoveTargetFromTo(clickedCard, rightDuck.GetComponent<NetworkIdentity>());

        // FIX: ?????????????
        activeSkillMode = SkillMode.None;
    }

    [Server]
    private void MoveTargetFromTo(NetworkIdentity fromCard, NetworkIdentity toCard)
    {
        if (fromCard == null || toCard == null) return;
        // ถ้าปลายทางมี Target อยู่ ลบทิ้งก่อน
        RemoveTargetFromCard(toCard);

        foreach (var tf in FindObjectsOfType<TargetFollow>())
        {
            if (tf != null && tf.targetNetId == fromCard.netId)
            {
                tf.targetNetId = toCard.netId;
                tf.ResetTargetTransform();

                // อัปเดต TargetMarker คู่กัน
                foreach (var mk in FindObjectsOfType<TargetMarker>())
                {
                    if (mk != null && mk.FollowDuckNetId == fromCard.netId)
                    {
                        mk.FollowDuckNetId = toCard.netId;
                        if (toCard.TryGetComponent(out DuckCard dcTo))
                            mk.ServerAssignToZone(ZoneKind.TargetZone, 0, dcTo.ColNet);
                    }
                }
            }
        }
    }


    [Server]
    private DuckCard FindDuckAt(int row, int col)
    {
        // (???? FindDuckAt ??????????...)
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();
            if (card != null && card.zone == ZoneKind.DuckZone &&
                card.RowNet == row && card.ColNet == col)
            {
                return card;
            }
        }
        return null;
    }

    // ========================
    // LineForward Logic (Refactored)
    // ========================
    // ?? (?? CmdDeactivateLineForward, RpcDisableLineForward)
    // ?? (?? isLineForwardActive)

    // (TryLineForward ????? CmdSetSkillMode(SkillMode.LineForward))
    // (CmdSetSkillMode ??????? CmdActivateLineForward)
    [Command(requiresAuthority = false)]
    public void CmdActivateLineForward()
    {
        Debug.Log("[LineForward] ?????????? (CmdActivateLineForward)");
        // เก็บคอลัมน์เดิมของ Target ไว้ก่อน
        var oldTargets = CollectTargetColumns();
        var leftmost = FindLeftmostDuck(0);

        if (leftmost != null)
        {
            int removedCol = leftmost.ColNet;
            NetworkServer.Destroy(leftmost.gameObject);
            RemoveTargetFromCard(leftmost.netIdentity);

            // รีเลขคอลัมน์ใหม่ให้แน่น หลังทำลายใบหน้าสุด
            Server_ResequenceDuckZoneColumns();

            // ปรับคอลัมน์เก่าของ Target ให้เลื่อนซ้ายตามการเลื่อนแถว
            for (int i = 0; i < oldTargets.Count; i++)
            {
                if (oldTargets[i] > removedCol)
                    oldTargets[i] -= 1;
            }
        }

        RemoveAllTargets();
        StartCoroutine(RefillAndRecreateTargets(oldTargets));
        StartCoroutine(DelayedLog());
        // (CmdSetSkillMode จะปิดเอง)
    }

    // (Helpers: DelayedLog, CollectTargetColumns, FindLeftmostDuck, RemoveAllTargets, FindDucksInRow, RefillAndRecreateTargets, ... ?????????????????)
    // ( ... ???? Helpers ????????????????? ... )
    // ... (???????????????????? ???????????????????????????????) ...


    // ========================
    // Move Ahead Logic (Refactored)
    // ========================
    // ?? (?? CmdActivateMoveAhead, RpcEnableMoveAhead, CmdDeactivateMoveAhead, RpcDisableMoveAhead)
    // ?? (?? isMoveAheadActive)

    // (CmdMoveAheadClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdMoveAheadClick(NetworkIdentity clickedCard)
    {
        // (???? CmdMoveAheadClick ??????????...)
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;
        int curRow = selectedDuck.RowNet, curCol = selectedDuck.ColNet;
        int targetCol = curCol - 1;
        DuckCard targetDuck = FindDuckAt(curRow, targetCol);
        if (targetDuck == null) return;

        bool selectedHadTarget = IsCardTargeted(selectedDuck.netIdentity);
        bool targetHadTarget = IsCardTargeted(targetDuck.netIdentity);
        if (selectedHadTarget) RemoveTargetFromCard(selectedDuck.netIdentity);
        if (targetHadTarget) RemoveTargetFromCard(targetDuck.netIdentity);

        selectedDuck.ColNet = targetCol;
        targetDuck.ColNet = curCol;

        if (selectedHadTarget) CmdSpawnTargetForDuck(targetDuck.netId);
        if (targetHadTarget) CmdSpawnTargetForDuck(selectedDuck.netId);

        // FIX: ?????????????
        activeSkillMode = SkillMode.None;
    }


    // ========================
    // HangBack Logic (Refactored)
    // ========================
    // ?? (?? CmdActivateHangBack, RpcEnableHangBack, CmdDeactivateHangBack, RpcDisableHangBack)
    // ?? (?? isHangBackActive)

    // (CmdHangBackClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdHangBackClick(NetworkIdentity clickedCard)
    {
        // (???? CmdHangBackClick ??????????...)
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;
        int curRow = selectedDuck.RowNet, curCol = selectedDuck.ColNet;
        int targetCol = curCol + 1;
        DuckCard targetDuck = FindDuckAt(curRow, targetCol);
        if (targetDuck == null) return;

        bool selectedHadTarget = IsCardTargeted(selectedDuck.netIdentity);
        bool targetHadTarget = IsCardTargeted(targetDuck.netIdentity);
        if (selectedHadTarget) RemoveTargetFromCard(selectedDuck.netIdentity);
        if (targetHadTarget) RemoveTargetFromCard(targetDuck.netIdentity);

        selectedDuck.ColNet = targetCol;
        targetDuck.ColNet = curCol;

        if (selectedHadTarget) CmdSpawnTargetForDuck(targetDuck.netId);
        if (targetHadTarget) CmdSpawnTargetForDuck(selectedDuck.netId);

        // FIX: ?????????????
        activeSkillMode = SkillMode.None;
    }


    // ========================
    // FastForward Logic (Refactored)
    // ========================
    // ?? (?? CmdActivateFastForward, RpcEnableFastForward, CmdDeactivateFastForward, RpcDisableFastForward)
    // ?? (?? isFastForwardActive)

    // (CmdFastForwardClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdFastForwardClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;
        StartCoroutine(FastForwardCoroutine(selectedDuck));

        // FIX: ?????????????
        activeSkillMode = SkillMode.None;
    }

    [Server]
    private IEnumerator FastForwardCoroutine(DuckCard selectedDuck)
    {
        // (???? FastForwardCoroutine ??????????...)
        float delay = 0.3f;
        int curRow = selectedDuck.RowNet;
        List<int> originalTargetColumns = new List<int>();
        List<TargetFollow> targetsToDestroy = new List<TargetFollow>();
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            DuckCard duck = FindDuckByNetId(tf.targetNetId);
            if (duck != null && duck.RowNet == curRow)
            {
                if (!originalTargetColumns.Contains(duck.ColNet))
                    originalTargetColumns.Add(duck.ColNet);
                targetsToDestroy.Add(tf);
            }
        }
        foreach (var tf in targetsToDestroy)
            NetworkServer.Destroy(tf.gameObject);

        while (selectedDuck.ColNet > 0)
        {
            int currentCol = selectedDuck.ColNet;
            int targetCol = currentCol - 1;
            DuckCard targetDuck = FindDuckAt(curRow, targetCol);
            if (targetDuck == null) break;

            selectedDuck.ColNet = targetCol;
            targetDuck.ColNet = currentCol;
            yield return new WaitForSeconds(delay);
        }
        yield return null;
        foreach (int originalCol in originalTargetColumns)
        {
            DuckCard newDuckAtCol = FindDuckAt(curRow, originalCol);
            if (newDuckAtCol != null)
                CmdSpawnTargetForDuck(newDuckAtCol.netId);
        }
        // (????????? CmdFastForwardClick ??????)
    }

    [Server]
    private DuckCard FindDuckByNetId(uint netId)
    {
        // (???? FindDuckByNetId ??????????...)
        if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity ni))
            return ni.GetComponent<DuckCard>();
        return null;
    }


    // ========================
    // Disorderly Conduckt Logic (Refactored)
    // ========================
    // ?? (?? CmdActivateDisorderlyConduckt, RpcEnableDisorderlyConduckt, CmdDeactivateDisorderlyConduckt, RpcDisableDisorderlyConduckt)
    // ?? (?? isDisorderlyConducktActive)

    // (CmdDisorderlyClick ??????????? HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdDisorderlyClick(NetworkIdentity clickedCard)
    {
        // (???? CmdDisorderlyClick ??????????...)
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        if (firstSelectedDuck == null)
        {
            firstSelectedDuck = selectedDuck;
            return;
        }
        if (firstSelectedDuck == selectedDuck)
        {
            firstSelectedDuck = null;
            return;
        }

        DuckCard secondDuck = selectedDuck;
        bool sameRow = firstSelectedDuck.RowNet == secondDuck.RowNet;
        bool adjacentCol = Mathf.Abs(firstSelectedDuck.ColNet - secondDuck.ColNet) == 1;
        if (!sameRow || !adjacentCol)
        {
            firstSelectedDuck = selectedDuck;
            return;
        }

        bool firstHadTarget = IsCardTargeted(firstSelectedDuck.netIdentity);
        bool secondHadTarget = IsCardTargeted(secondDuck.netIdentity);
        if (firstHadTarget) RemoveTargetFromCard(firstSelectedDuck.netIdentity);
        if (secondHadTarget) RemoveTargetFromCard(secondDuck.netIdentity);

        int tempCol = firstSelectedDuck.ColNet;
        firstSelectedDuck.ColNet = secondDuck.ColNet;
        secondDuck.ColNet = tempCol;

        if (firstHadTarget) CmdSpawnTargetForDuck(secondDuck.netId);
        if (secondHadTarget) CmdSpawnTargetForDuck(firstSelectedDuck.netId);

        firstSelectedDuck = null;
        // (??????????????????? Active ??????? ??????????)
        // activeSkillMode = SkillMode.None; 
    }

    [Command(requiresAuthority = false)]
    private void CmdSpawnTargetForDuck(uint duckNetId)
    {
        // (???? CmdSpawnTargetForDuck ??????????...)
        if (!NetworkServer.spawned.TryGetValue(duckNetId, out NetworkIdentity duckNi))
            return;
        if (targetPrefab == null) return;
        GameObject newTarget = Instantiate(targetPrefab);
        NetworkServer.Spawn(newTarget);
        NetworkIdentity targetNi = newTarget.GetComponent<NetworkIdentity>();
        RpcSetTargetNetId(targetNi, duckNi);
    }


    // ========================
    // Duck Shuffle  Logic (Refactored)
    // ========================
    // ?? (?? CmdDeactivateDuckShuffle, RpcDisableDuckShuffle)
    // ?? (?? isDuckShuffleActive)

    // (TryDuckShuffle ????? CmdSetSkillMode(SkillMode.DuckShuffle))
    // (CmdSetSkillMode ??????? CmdActivateDuckShuffle)
    [Command(requiresAuthority = false)]
    public void CmdActivateDuckShuffle()
    {
        var oldTargets = CollectTargetColumns();
        RemoveAllDucks();
        RemoveAllTargets();
        if (DuckZone == null) return;

        int needed = 6;
        for (int i = 0; i < needed; i++)
        {
            if (!CardPoolManager.HasCards()) break;

            // (FIX) ????? DrawRandomCard() ?????? Obsolete
            GameObject cardGO = CardPoolManager.DrawRandomCard();
            if (cardGO == null) break;

            // (FIX) ??????????????????????????? Zone/???????
            var duck = cardGO.GetComponent<DuckCard>();
            if (duck != null)
            {
                // ????????????????? zone, RowNet, ColNet ??????
                duck.ServerAssignToZone(ZoneKind.DuckZone, 0, i);
            }

            NetworkServer.Spawn(cardGO);

            // (FIX) ?? RpcAddCardToDuckZone(cardGO) ????
        }

        StartCoroutine(RecreateTargetsAfterShuffle(oldTargets));
        StartCoroutine(DelayedLog());
    }

    [Server]
    private IEnumerator RecreateTargetsAfterShuffle(List<int> oldCols)
    {
        // (???? RecreateTargetsAfterShuffle ??????????...)
        yield return null;
        List<DuckCard> ducks = FindDucksInRow(0);
        foreach (int col in oldCols)
        {
            var duckAtCol = ducks.Find(d => d.ColNet == col);
            if (duckAtCol != null)
                CmdSpawnTargetForDuck(duckAtCol.netId);
        }
    }

    [Server]
    private void RemoveAllDucks()
    {
        // (???? RemoveAllDucks ??????????...)
        List<GameObject> ducksToDestroy = new List<GameObject>();
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            if (netId.TryGetComponent(out DuckCard duck) && duck.zone == ZoneKind.DuckZone)
                ducksToDestroy.Add(duck.gameObject);
        }
        foreach (var duckGO in ducksToDestroy)
        {
            CardPoolManager.ReturnCard(duckGO);
            NetworkServer.Destroy(duckGO);
        }

        // ???? DuckZone ?????? 6 ???????????????????? (????????????????????)
        RefillDuckZoneIfNeeded();
    }


    // ========================
    // GivePeaceAChance Logic
    // ========================
    // ?? (?? CmdDeactivateGivePeaceAChance, RpcDisableGivePeaceAChance)
    // ?? (?? isGivePeaceActive)

    // (TryGivePeaceAChance ????? CmdSetSkillMode(SkillMode.GivePeaceAChance))
    // (CmdSetSkillMode ??????? CmdActivateGivePeaceAChance)
    [Command(requiresAuthority = false)]
    private void CmdActivateGivePeaceAChance()
    {
        RemoveAllTargets();
        // (CmdSetSkillMode ????????????)
    }

    // ========================
    // Resurrection  Logic (Refactored)
    // ========================
    // ?? (?? CmdDeactivateResurrectionMode, RpcDisableResurrectionMode)
    // ?? (?? isResurrectionModeActive)

    // (TryUseResurrection ????? CmdSetSkillMode(SkillMode.Resurrection))
    // (CmdSetSkillMode ??????? CmdActivateResurrectionMode)
    [Command]
    private void CmdActivateResurrectionMode()
    {
        const int maxPerColor = 5;

        // 1. (FIX) ????? GetTotalDuckCounts (?????????????????)
        var totalCounts = GetTotalDuckCounts();
        var lowColors = new List<string>();

        // 2. (FIX) ???????? Key ???????? (?????????????????????????)
        foreach (string color in totalCounts.Keys)
        {
            // (??????????????? Marsh)
            if (color == "Marsh") continue;

            int currentCount = totalCounts.GetValueOrDefault(color, 0);
            if (currentCount < maxPerColor)
                lowColors.Add(color);
        }

        if (lowColors.Count > 0)
        {
            int idx = Random.Range(0, lowColors.Count);
            string color = lowColors[idx];

            // 3. (?????????????)
            CardPoolManager.AddToPool(color);
        }

    }

    [Server]
    private Dictionary<string, int> GetTotalDuckCounts()
    {
        // 1. (FIX) ????????????????????????? (GetAllPoolCounts)
        Dictionary<string, int> counts = CardPoolManager.GetAllPoolCounts();

        // 2. ?????????? DuckZone
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();

            // (FIX) ??? .zone (???????) ?????? Helper 'ExtractDuckKeyFromCard' (????????????????)
            if (card != null && card.zone == ZoneKind.DuckZone)
            {
                string key = ExtractDuckKeyFromCard(card.gameObject); // (???????????????????)
                if (string.IsNullOrEmpty(key)) continue;

                if (!counts.ContainsKey(key))
                    counts[key] = 0;

                counts[key]++;
            }
        }
        return counts;
    }




    // ========================
    // ShowCard Logic
    // ========================
    // FIX 1: ClientRpc ??????? NetworkIdentity
    [ClientRpc]
    void RpcShowCard(NetworkIdentity cardIdentity, string type)
    {
        // กันเคส RPC มาช้า/การ์ดถูกลบไปแล้ว
        if (!NetworkClient.active) return;
        if (cardIdentity == null || cardIdentity.gameObject == null)
        {
            Debug.LogWarning("[RpcShowCard] การ์ดว่างหรือถูกทำลายแล้ว ข้ามการแสดงผล");
            return;
        }

        try
        {
            Debug.Log($"[RpcShowCard] แสดงการ์ด netId={cardIdentity.netId} type={type} isOwned={cardIdentity.isOwned}");
            GameObject card = cardIdentity.gameObject;

            if (type == "Dealt")
            {
                // ฝั่งศัตรูพลิกหลังทันที (ปล่อยให้ DuckCard จัด layout เอง)
                if (!cardIdentity.isOwned && EnemyArea != null)
                {
                    card.GetComponent<CardFlipper>()?.Flip();
                }
            }
            else if (type == "Played")
            {
                card.SetActive(true);
                Canvas.ForceUpdateCanvases();

                var dropZone = FindObjectOfType<DropZone>();
                if (dropZone != null)
                    dropZone.PlaceCard(card);

                if (!cardIdentity.isOwned)
                    card.GetComponent<CardFlipper>()?.Flip();

                if (isLocalPlayer && cardIdentity.isOwned)
                {
                    HandleCardActivation(card);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcShowCard] ขัดข้อง: {ex}");
        }
    }


    // (?????????????????? Client ?????????????????)
    private void HandleCardActivation(GameObject card)
    {
        SkillMode selectedSkill = SkillMode.None;

        if (card.name.Contains("Shoot"))
            selectedSkill = SkillMode.Shoot;
        else if (card.name.Contains("TekeAim"))
            selectedSkill = SkillMode.TakeAim;
        else if (card.name.Contains("DoubleBarrel"))
            selectedSkill = SkillMode.DoubleBarrel;
        else if (card.name.Contains("QuickShot"))
            selectedSkill = SkillMode.QuickShot;
        else if (card.name.Contains("Misfire"))
            selectedSkill = SkillMode.Misfire;
        else if (card.name.Contains("TwoBirds"))
            selectedSkill = SkillMode.TwoBirds;
        else if (card.name.Contains("BumpLeft"))
            selectedSkill = SkillMode.BumpLeft;
        else if (card.name.Contains("BumpRight"))
            selectedSkill = SkillMode.BumpRight;
        else if (card.name.Contains("LineForward"))
            selectedSkill = SkillMode.LineForward;
        else if (card.name.Contains("MoveAhead"))
            selectedSkill = SkillMode.MoveAhead;
        else if (card.name.Contains("HangBack"))
            selectedSkill = SkillMode.HangBack;
        else if (card.name.Contains("FastForward"))
            selectedSkill = SkillMode.FastForward;
        else if (card.name.Contains("DisorderlyConduckt"))
            selectedSkill = SkillMode.DisorderlyConduckt;
        else if (card.name.Contains("DuckShuffle"))
            selectedSkill = SkillMode.DuckShuffle;
        else if (card.name.Contains("GivePeaceAChance"))
            selectedSkill = SkillMode.GivePeaceAChance;
        else if (card.name.Contains("Resurrection"))
            selectedSkill = SkillMode.Resurrection;

        if (selectedSkill != SkillMode.None)
        {
            // ??? Command ??????? State ????? Server
            CmdSetSkillMode(selectedSkill);
        }
    }


    // ========================
    // ???????? Targeting
    // ========================
    [Command]
    public void CmdTargetSelfCard()
    {
        TargetSelfCard();
    }

    [Command(requiresAuthority = false)]
    public void CmdTargetOtherCard(GameObject target)
    {
        if (target == null)
        {
            Debug.LogError("[CmdTargetOtherCard] target GameObject เป็น null ข้ามคำสั่ง");
            return;
        }

        var opponentIdentity = target.GetComponent<NetworkIdentity>();
        if (opponentIdentity == null)
        {
            Debug.LogError("[CmdTargetOtherCard] target ไม่มี NetworkIdentity ข้ามคำสั่ง");
            return;
        }

        var conn = opponentIdentity.connectionToClient;
        if (conn == null)
        {
            Debug.LogWarning("[CmdTargetOtherCard] connectionToClient เป็น null ข้ามคำสั่ง");
            return;
        }

        TargetOtherCard(conn);
    }

    [TargetRpc]
    void TargetSelfCard()
    {
        Debug.Log("Targeted by self!");
    }

    [TargetRpc]
    void TargetOtherCard(NetworkConnection target)
    {
        Debug.Log("Targeted by other!");
    }

    [Command]
    public void CmdIncrementClick(GameObject card)
    {
        RpcIncrementClick(card);
    }

    [ClientRpc]
    void RpcIncrementClick(GameObject card)
    {
        if (!NetworkClient.active) return;
        if (card == null) return;

        try
        {
            var increment = card.GetComponent<IncrementClick>();
            if (increment != null)
            {
                increment.NumberOfClicks++;
                Debug.Log($"[RpcIncrementClick] นับคลิก = {increment.NumberOfClicks}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcIncrementClick] ขัดข้อง: {ex}");
        }
    }
}


