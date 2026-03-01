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
//  SkillMode Enum 
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

public enum DuckColor { Yellow, Blue, Red, Green, Pink, Black }


public partial class PlayerManager : NetworkBehaviour
{
    [SyncVar] public DuckColor duckColor;

    [SyncVar(hook = nameof(OnSkillModeChanged))]
    public SkillMode activeSkillMode = SkillMode.None;

    [SyncVar(hook = nameof(OnOwnedDuckCountChanged))]
    [SerializeField] private int ownedDuckCount = 0;
    public int OwnedDuckCount => ownedDuckCount;
    // --- PATCH: Barrier Hooks ---
    private static bool s_barrierHooksBoundServer = false;
    private static bool s_barrierHooksBoundClient = false;
    // ??? barrier ??????????????????/???????? (???????? true)
    // ?????????????????????? ??????????? false
    public static bool DeferInitialDealToBarrier = true;
    // ???????????????????? ????? BarrierGoServer ???????????????
    private static bool s_matchStarted = false;
    private static uint s_actionPoolOwnerNetId = 0;
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
    [SyncVar(hook = nameof(OnSeatIndexChanged))] public int seatIndex = -1;
    // ????????????? (???? client ?????????)
    private static Transform[] s_enemySlots = null;
    // map: netId ??? PlayerManager (?????) -> slot index [0..4]
    private static readonly Dictionary<uint, int> s_remoteSlotIndex = new Dictionary<uint, int>();
    private static bool s_pendingTurnOrderLayoutRefresh;
    private static string s_pendingTurnOrderLayoutReason;
    private Coroutine _turnOrderLayoutCoroutine;
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

    public static void RequestTurnOrderLayoutRefresh(string reason = null)
    {
        if (!NetworkClient.active) return;

        // Prevent stale netId->slot cache while TurnOrder/layout is changing.
        s_remoteSlotIndex.Clear();

        if (localInstance != null)
        {
            localInstance.ScheduleTurnOrderLayoutRecompute(reason);
            return;
        }

        s_pendingTurnOrderLayoutRefresh = true;
        s_pendingTurnOrderLayoutReason = reason;
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
                // ;
            }
        }
    }
    // ///////////////////////////////////////////  Turn  ////////////////////////////////////////////////////////////////////
    [SyncVar(hook = nameof(OnDuckColorIndexChanged))] public int duckColorIndex = 0; // 0..N-1

    private void OnSeatIndexChanged(int oldValue, int newValue)
    {
        if (!NetworkClient.active) return;
        RequestTurnOrderLayoutRefresh($"SeatIndexChanged:{oldValue}->{newValue}");
    }

    private void OnDuckColorIndexChanged(int oldValue, int newValue)
    {
        if (!NetworkClient.active) return;
        RequestTurnOrderLayoutRefresh($"DuckColorChanged:{oldValue}->{newValue}");
    }
    // ========================
    //  Core State Logic 
    // ========================
    // (Optional) Hook ?????? Client UI 
    void OnSkillModeChanged(SkillMode oldMode, SkillMode newMode)
    {
        if (!isLocalPlayer)
            return;

        ActiveSkillDescriptionUI.NotifySkillModeChanged(newMode);
    }

    void OnOwnedDuckCountChanged(int oldValue, int newValue)
    {
    }

    [Command]
    public void CmdSetSkillMode(SkillMode newMode)
    {
        TurnManager tm = TurnManager.Instance;
        if (tm != null)
        {
            uint turnNetId = tm.ServerGetCurrentTurnNetId();
            if (turnNetId != 0 && turnNetId != netId)
            {
                Debug.LogWarning(
                    $"[CmdSetSkillMode] Reject out-of-turn mode set playerNetId={netId} currentTurnNetId={turnNetId} mode={newMode}"
                );
                return;
            }
        }

        // Debug.Log($"[CmdSetSkillMode] from connId={connectionToClient?.connectionId} pmNetId={netId} mode={newMode}");

        activeSkillMode = newMode;

        bool modeShouldClose = false;
        if (newMode == SkillMode.LineForward)
        {
            CmdActivateLineForward();
            modeShouldClose = true;
        }
        else if (newMode == SkillMode.DuckShuffle)
        {
            CmdActivateDuckShuffle();
            modeShouldClose = true;
        }
        else if (newMode == SkillMode.GivePeaceAChance)
        {
            CmdActivateGivePeaceAChance();
            modeShouldClose = true;
        }
        else if (newMode == SkillMode.Resurrection)
        {
            Server_ActivateResurrectionMode();
            modeShouldClose = true;
        }

        if (modeShouldClose)
        {
            activeSkillMode = SkillMode.None;
        }

        if (tm != null && newMode != SkillMode.None)
        {
            tm.ServerNotifySkillModeSelected(netId, newMode);
        }
    }

    [Server]
    public bool ServerForceEndActiveSkill(string reason = null)
    {
        SkillMode previousMode = activeSkillMode;
        bool hadActiveSkill = previousMode != SkillMode.None;

        // Clear temporary multi-step selection state to prevent stale references
        // from carrying into the next turn.
        firstSelectedDuck = null;
        firstTwoBirdsCard = null;
        twoBirdsClickCount = 0;
        doubleBarrelClickCount = 0;
        firstClickedCard = null;
        targetedDuckNetId = 0;

        if (!hadActiveSkill)
            return false;

        activeSkillMode = SkillMode.None;
        Debug.Log(
            $"[PlayerManager] SkillForceEnded reason={reason ?? "-"} netId={netId} seatIndex={SeatIndex} from={previousMode} to={activeSkillMode}"
        );
        return true;
    }

    [Server]
    public void ServerSetOwnedDuckCount(int value)
    {
        int safeValue = Mathf.Max(0, value);
        if (ownedDuckCount == safeValue)
            return;

        ownedDuckCount = safeValue;
    }


    public void HandleDuckCardClick(DuckCard clickedCard)
    {
        if (!isLocalPlayer) return;

        if (clickedCard == null || clickedCard.zone != ZoneKind.DuckZone)
            return;

        switch (activeSkillMode)
        {
            case SkillMode.None:
                break;
            case SkillMode.Shoot:
                CmdShootCard(clickedCard.netIdentity);
                break;
            case SkillMode.TakeAim:
                CmdSpawnTarget(clickedCard.netIdentity);
                CmdSetSkillMode(SkillMode.None);
                break;
            case SkillMode.DoubleBarrel:
                CmdDoubleBarrelClick(clickedCard.netIdentity);
                break;
            case SkillMode.QuickShot:
                CmdQuickShotCard(clickedCard.netIdentity);
                break;
            case SkillMode.Misfire:
                CmdMisfireClick(clickedCard.netIdentity);
                break;
            case SkillMode.TwoBirds:
                CmdTwoBirdsClick(clickedCard.netIdentity);
                break;
            case SkillMode.BumpLeft:
                CmdBumpLeftClick(clickedCard.netIdentity);
                break;
            case SkillMode.BumpRight:
                CmdBumpRightClick(clickedCard.netIdentity);
                break;
            case SkillMode.MoveAhead:
                CmdMoveAheadClick(clickedCard.netIdentity);
                break;
            case SkillMode.HangBack:
                CmdHangBackClick(clickedCard.netIdentity);
                break;
            case SkillMode.FastForward:
                CmdFastForwardClick(clickedCard.netIdentity);
                break;
            case SkillMode.DisorderlyConduckt:
                CmdDisorderlyClick(clickedCard.netIdentity);
                break;
            case SkillMode.LineForward:
            case SkillMode.DuckShuffle:
            case SkillMode.GivePeaceAChance:
            case SkillMode.Resurrection:

                break;
            default:
                Debug.LogWarning($"Unhandled SkillMode in HandleDuckCardClick: {activeSkillMode}");
                break;
        }
    }


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
        // Resolve canvas from scene only.
        Transform mainCanvas = GameObject.Find("Main Canvas")?.transform ?? GameObject.Find("Canvas")?.transform;
        if (mainCanvas == null)
        {
            Debug.LogError("[PlayerManager.OnStartClient] ? Canvas not found");
            return;
        }

        // Prefer "Image" root if present, otherwise use canvas directly.
        Transform uiRoot = FindChildRecursive(mainCanvas, "Image") ?? mainCanvas;

        DuckZone = ResolveSceneUiObject(uiRoot, mainCanvas, "DuckZone", DuckZone);
        DropZone = ResolveSceneUiObject(uiRoot, mainCanvas, "DropZone", DropZone);
        TargetZone = ResolveSceneUiObject(uiRoot, mainCanvas, "TargetZone", TargetZone);
        EnemyArea = ResolveSceneUiObject(uiRoot, mainCanvas, "EnemyArea", EnemyArea);

        var ni = GetComponent<NetworkIdentity>();
        if (ni != null && ni.isOwned)
        {
            PlayerArea = ResolveSceneUiObject(uiRoot, mainCanvas, "PlayerArea", PlayerArea);
            localInstance = this;
        }

        CacheEnemySlotsFromScene();
        RequestTurnOrderLayoutRefresh("PlayerManager.OnStartClient");
    }
    public override void OnStopClient()
    {
        base.OnStopClient();
        if (_turnOrderLayoutCoroutine != null)
        {
            StopCoroutine(_turnOrderLayoutCoroutine);
            _turnOrderLayoutCoroutine = null;
        }

        if (isLocalPlayer && localInstance == this)
            localInstance = null;
    }
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        localInstance = this;

        if (s_pendingTurnOrderLayoutRefresh)
        {
            ScheduleTurnOrderLayoutRecompute(s_pendingTurnOrderLayoutReason, 0.05f);
            s_pendingTurnOrderLayoutRefresh = false;
            s_pendingTurnOrderLayoutReason = null;
        }
        else
        {
            ScheduleTurnOrderLayoutRecompute("PlayerManager.OnStartLocalPlayer", 0.05f);
        }
    }
    private void OnDestroy()
    {
        var networkIdentity = GetComponent<NetworkIdentity>();
        if (networkIdentity != null && networkIdentity.isOwned)
        {
            StopAllCoroutines();
        }

        if (localInstance == this)
            localInstance = null;
    }
    // Helper ??/????????????????
    // ??/??? EnemyArea1..5 ??? Scene
    private void CacheEnemySlotsFromScene()
    {
        Transform mainCanvas = GameObject.Find("Main Canvas")?.transform ?? GameObject.Find("Canvas")?.transform;
        if (mainCanvas == null)
        {
            Debug.LogWarning("[CacheEnemySlots] Canvas not found!");
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

        if (s_enemySlots == null || s_enemySlots.Length != 5)
            s_enemySlots = new Transform[5];
        for (int i = 0; i < s_enemySlots.Length; i++)
        {
            string childName = $"{enemySlotPrefix}{i + 1}";
            // Bind by direct child under EnemiesArea only (avoid recursive mismatches).
            var child = root.Find(childName);
            if (child == null)
            {
                string altChild = $"{enemySlotPrefix}{i}";
                child = root.Find(altChild);
            }
            s_enemySlots[i] = child;
            if (child == null)
                Debug.LogWarning($"[CacheEnemySlots] Slot '{childName}' not found!");

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

    private static bool IsSceneGameObject(GameObject go)
    {
        return go != null && go.scene.IsValid() && go.scene.isLoaded;
    }

    private static bool IsSceneTransform(Transform tr)
    {
        return tr != null && tr.gameObject != null && tr.gameObject.scene.IsValid() && tr.gameObject.scene.isLoaded;
    }

    private GameObject ResolveSceneUiObject(Transform preferredRoot, Transform fallbackRoot, string childName, GameObject currentValue)
    {
        if (IsSceneGameObject(currentValue))
            return currentValue;

        Transform found = null;
        if (IsSceneTransform(preferredRoot))
            found = FindChildRecursive(preferredRoot, childName);
        if (!IsSceneTransform(found) && IsSceneTransform(fallbackRoot))
            found = FindChildRecursive(fallbackRoot, childName);
        if (!IsSceneTransform(found))
            found = GameObject.Find(childName)?.transform;

        return IsSceneTransform(found) ? found.gameObject : null;
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

        return ResolveEnemySlotTransformByNumber(rel);
    }

    /// <summary>
    /// Resolve EnemyArea slot from TurnOrder indices.
    /// - Previous side: EA1, EA2, ...
    /// - Next side: EA5, EA4, ...
    /// Fixed ring mapping:
    /// delta < 0 => EA1, EA2, ...
    /// delta > 0 => EA5, EA4, ...
    /// </summary>
    private static int ComputeEnemySlotByTurnOrder(int myIndex, int otherIndex, int orderCount)
    {
        if (orderCount < 2 || myIndex < 0 || otherIndex < 0 || myIndex == otherIndex)
            return -1;
        int delta = otherIndex - myIndex;
        int slot = delta < 0 ? -delta : 6 - delta;
        return Mathf.Clamp(slot, 1, 5);
    }
    [Client]
    public void RecomputeLocalLayoutByTurnOrder()
    {
        if (!NetworkClient.active || !isLocalPlayer) return;
        ScheduleTurnOrderLayoutRecompute("PlayerManager.RecomputeLocalLayoutByTurnOrder");
    }

    // Backward-compatible entrypoint for old callers.
    [Client]
    private void RecomputeLocalLayoutBySeat()
    {
        RecomputeLocalLayoutByTurnOrder();
    }

    [Client]
    private void ScheduleTurnOrderLayoutRecompute(string reason = null, float delaySeconds = 0f)
    {
        if (!NetworkClient.active || !isLocalPlayer) return;

        if (_turnOrderLayoutCoroutine != null)
        {
            StopCoroutine(_turnOrderLayoutCoroutine);
            _turnOrderLayoutCoroutine = null;
        }

        _turnOrderLayoutCoroutine = StartCoroutine(CoRecomputeTurnOrderLayout(reason, delaySeconds));
    }

    [Client]
    private IEnumerator CoRecomputeTurnOrderLayout(string reason, float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        const int maxAttempts = 40;
        string waitReason = null;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (TryApplyLocalLayoutByTurnOrder(out waitReason))
            {
                _turnOrderLayoutCoroutine = null;
                yield break;
            }

            yield return new WaitForSeconds(0.05f);
        }

        _turnOrderLayoutCoroutine = null;
        Debug.LogWarning($"[PlayerManager] RecomputeLocalLayoutByTurnOrder timeout reason={reason ?? "-"} wait={waitReason ?? "-"}");
    }

    [Client]
    private bool TryApplyLocalLayoutByTurnOrder(out string waitReason)
    {
        waitReason = null;

        if (!NetworkClient.active)
        {
            waitReason = "NetworkClient inactive";
            return false;
        }

        if (!isLocalPlayer)
        {
            waitReason = "not local player";
            return false;
        }

        TurnManager tm = TurnManager.Instance;
        if (tm == null)
        {
            waitReason = "TurnManager missing";
            return false;
        }

        if (s_enemySlots == null || s_enemySlots.Any(t => t == null))
            CacheEnemySlotsFromScene();
        if (s_enemySlots == null || s_enemySlots.Any(t => t == null))
        {
            waitReason = "enemy slots missing";
            return false;
        }

        List<PlayerManager> players = FindObjectsOfType<PlayerManager>()
            .Where(pm => pm != null && pm.isActiveAndEnabled && pm.SeatIndex >= 0)
            .OrderBy(pm => pm.netId)
            .ToList();

        if (players.Count < 2)
        {
            waitReason = "players < 2";
            return false;
        }

        if (players.Any(pm => pm.duckColorIndex < 0 || pm.duckColorIndex > 5))
        {
            waitReason = "duckColorIndex not ready";
            return false;
        }

        if (tm.TurnOrder.Count != players.Count)
        {
            waitReason = $"TurnOrder({tm.TurnOrder.Count}) != players({players.Count})";
            return false;
        }

        List<uint> order = tm.TurnOrder.ToList();
        uint myNetId = netId;
        int myIndex = order.IndexOf(myNetId);
        if (myIndex < 0)
        {
            waitReason = "local player not in TurnOrder";
            return false;
        }

        var playerByNetId = players.ToDictionary(pm => pm.netId, pm => pm);
        for (int i = 0; i < order.Count; i++)
        {
            if (!playerByNetId.ContainsKey(order[i]))
            {
                waitReason = $"TurnOrder has unknown netId={order[i]}";
                return false;
            }
        }

        s_remoteSlotIndex.Clear();

        if (PlayerArea == null)
            PlayerArea = FindUIObject("PlayerArea");

        HashSet<int> usedEnemySlots = new HashSet<int>();
        foreach (PlayerManager pm in players)
        {
            if (pm.netId == myNetId)
            {
                pm.PlayerArea = PlayerArea;
                continue;
            }

            int otherIndex = order.IndexOf(pm.netId);
            if (otherIndex < 0) continue;

            int slot = ComputeEnemySlotByTurnOrder(myIndex, otherIndex, order.Count);
            if (slot < 1) continue;

            Transform slotTransform = GetSlotByRelIndex(slot);
            if (slotTransform != null)
            {
                pm.EnemyArea = slotTransform.gameObject;
                s_remoteSlotIndex[pm.netId] = slot - 1; // 0..4
                usedEnemySlots.Add(slot);
            }
            else
            {
                pm.EnemyArea = FindUIObject("EnemyArea");
            }
        }

        if (usedEnemySlots.Count > 0)
            PlayerTurnSeatingBinder.RefreshVisibleSlotsByUsedSlots(usedEnemySlots);
        else
            PlayerTurnSeatingBinder.RefreshVisibleSlotsForCount(players.Count);

        RefreshPlayerAreaCardParentsByMapping();
        return true;
    }

    [Client]
    private void RefreshPlayerAreaCardParentsByMapping()
    {
        foreach (DuckCard dc in FindAllClientDuckCards())
        {
            if (dc == null || dc.zone != ZoneKind.PlayerArea) continue;

            Transform targetParent = null;
            if (dc.ownerNetId == LocalPlayerNetId)
            {
                if (PlayerArea == null)
                    PlayerArea = FindUIObject("PlayerArea");
                targetParent = PlayerArea != null ? PlayerArea.transform : null;
            }
            else
            {
                targetParent = TryGetEnemySlotForNetId(dc.ownerNetId);
            }

            if (targetParent == null) continue;

            if (!targetParent.gameObject.activeSelf)
            {
                int preferredSlot = ResolveEnemySlotNumberFromTransform(targetParent);
                Transform forcedActive = ResolveActiveEnemySlotFallback(preferredSlot);
                if (forcedActive != null)
                    targetParent = forcedActive;
            }

            if (dc.transform.parent != targetParent)
                dc.transform.SetParent(targetParent, false);

            if (targetParent.childCount > 0)
            {
                int sibling = Mathf.Clamp(dc.zoneIndex, 0, targetParent.childCount - 1);
                dc.transform.SetSiblingIndex(sibling);
            }
        }
    }

    [Client]
    private static IEnumerable<DuckCard> FindAllClientDuckCards()
    {
        // Include inactive cards so we can recover cards that were temporarily parented under hidden EA slots.
        return Resources.FindObjectsOfTypeAll<DuckCard>()
            .Where(dc =>
                dc != null &&
                dc.gameObject != null &&
                dc.gameObject.scene.IsValid() &&
                dc.gameObject.scene.isLoaded);
    }

    [Client]
    private static int ResolveEnemySlotNumberFromTransform(Transform parent)
    {
        if (parent == null || s_enemySlots == null)
            return -1;

        for (int i = 0; i < s_enemySlots.Length; i++)
        {
            Transform slot = s_enemySlots[i];
            if (slot == null)
                continue;

            if (parent == slot || parent.IsChildOf(slot))
                return i + 1;
        }

        return -1;
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

        // Prefer authoritative TurnOrder mapping first.
        TurnManager tm = TurnManager.Instance;
        if (tm != null && localInstance != null && tm.TurnOrder.Count > 0)
        {
            List<uint> order = tm.TurnOrder.ToList();
            int myIndex = order.IndexOf(localInstance.netId);
            int otherIndex = order.IndexOf(netId);

            if (myIndex >= 0 && otherIndex >= 0 && myIndex != otherIndex)
            {
                int slotNumber = ComputeEnemySlotByTurnOrder(myIndex, otherIndex, order.Count);
                if (slotNumber < 1)
                    return null;
                Transform fallbackSlot = ResolveEnemySlotTransformByNumber(slotNumber);
                int fallbackIdx = slotNumber - 1;
                if (fallbackSlot != null && fallbackSlot.gameObject.activeSelf)
                {
                    s_remoteSlotIndex[netId] = fallbackIdx;
                    return fallbackSlot;
                }

                Transform activeFallback = ResolveActiveEnemySlotFallback(slotNumber);
                if (activeFallback != null)
                {
                    int activeSlot = ResolveEnemySlotNumberFromTransform(activeFallback);
                    if (activeSlot >= 1)
                        s_remoteSlotIndex[netId] = activeSlot - 1;
                    return activeFallback;
                }
            }

            // TurnOrder is available but mapping not ready yet: avoid stale cache fallback.
            return null;
        }

        // Fallback to cache only when TurnOrder mapping is unavailable.
        if (s_remoteSlotIndex.TryGetValue(netId, out int idx))
        {
            int slotNumber = idx + 1;
            if (slotNumber >= 1 && slotNumber <= 5)
            {
                var slot = ResolveEnemySlotTransformByNumber(slotNumber);
                if (slot != null && slot.gameObject.activeSelf)
                    return slot;
            }
        }

        return ResolveActiveEnemySlotFallback(-1);
    }

    private static Transform ResolveActiveEnemySlotFallback(int preferredSlot)
    {
        if (s_enemySlots == null || s_enemySlots.Length == 0)
            return null;

        // Prefer side-consistent fallback.
        if (preferredSlot >= 4)
        {
            for (int i = s_enemySlots.Length - 1; i >= 0; i--)
            {
                Transform slot = s_enemySlots[i];
                if (slot != null && slot.gameObject.activeSelf)
                    return slot;
            }
        }
        else if (preferredSlot >= 1)
        {
            for (int i = 0; i < s_enemySlots.Length; i++)
            {
                Transform slot = s_enemySlots[i];
                if (slot != null && slot.gameObject.activeSelf)
                    return slot;
            }
        }

        // Any active slot as final fallback.
        for (int i = 0; i < s_enemySlots.Length; i++)
        {
            Transform slot = s_enemySlots[i];
            if (slot != null && slot.gameObject.activeSelf)
                return slot;
        }

        return null;
    }

    private static Transform ResolveEnemySlotTransformByNumber(int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > 5)
            return null;

        int idx = slotNumber - 1;

        if (s_enemySlots != null && idx >= 0 && idx < s_enemySlots.Length)
        {
            Transform cached = s_enemySlots[idx];
            if (cached != null)
            {
                string n = cached.name;
                string oneBased = $"{localInstance?.enemySlotPrefix ?? "EnemyArea"}{slotNumber}";
                string zeroBased = $"{localInstance?.enemySlotPrefix ?? "EnemyArea"}{slotNumber - 1}";
                if (n == oneBased || n == zeroBased)
                    return cached;
            }
        }

        Transform root = FindEnemiesAreaRootForLookup();
        if (root == null)
            return null;

        string prefix = localInstance != null ? localInstance.enemySlotPrefix : "EnemyArea";
        Transform resolved = root.Find($"{prefix}{slotNumber}") ?? root.Find($"{prefix}{slotNumber - 1}");
        if (resolved != null)
        {
            if (s_enemySlots == null || s_enemySlots.Length != 5)
                s_enemySlots = new Transform[5];
            s_enemySlots[idx] = resolved;
        }

        return resolved;
    }

    private static Transform FindEnemiesAreaRootForLookup()
    {
        string rootName = localInstance != null ? localInstance.enemiesAreaRootName : "EnemiesArea";
        Transform mainCanvas = GameObject.Find("Main Canvas")?.transform ?? GameObject.Find("Canvas")?.transform;
        if (mainCanvas != null)
        {
            Transform uiRoot = localInstance != null
                ? localInstance.FindChildRecursive(mainCanvas, "Image")
                : null;
            Transform root = null;
            if (uiRoot != null && localInstance != null)
                root = localInstance.FindChildRecursive(uiRoot, rootName);
            if (root == null && localInstance != null)
                root = localInstance.FindChildRecursive(mainCanvas, rootName);
            if (root != null)
                return root;
        }

        GameObject fallback = GameObject.Find(rootName);
        return fallback != null ? fallback.transform : null;
    }

    private GameObject FindUIObject(string childName)
    {
        var direct = GameObject.Find(childName);
        if (direct != null)
        {
            ;
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
            ;
            return target.gameObject;
        }
        Debug.LogWarning($"[FindUIObject] '{childName}' not found under canvas hierarchy");
        return null;
    }
    private void LogZoneStatus(string zoneName, GameObject go)
    {
        ;
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
    // ??????????? ï¿½?????? index ?????????????????ï¿½
    private static readonly string[] DUCK_KEYS_BY_INDEX =
    {
    "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    // ????????????? index ????????????????
    };
    private static string ColorIndexToDuckKey(int idx)
    {
        return (idx >= 0 && idx < DUCK_KEYS_BY_INDEX.Length) ? DUCK_KEYS_BY_INDEX[idx] : null;
    }

    // Shared helper for all partial ability files.
    private static string ExtractDuckKeyFromCard(GameObject go)
    {
        if (go == null) return null;

        string name = go.name.Replace("(Clone)", "").Trim();
        if (name.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Marsh";

        foreach (string key in DUCK_KEYS_BY_INDEX)
        {
            if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                return key;
        }

        return null;
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
    [ClientRpc]
    public void RpcRecomputeLayoutAllClients()
    {
        if (!NetworkClient.active) return;
        RequestTurnOrderLayoutRefresh("PlayerManager.RpcRecomputeLayoutAllClients");
    }
    // ????? server (???????????? server/host) ï¿½ cache ???????????????
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
        // à¸­à¸¢à¹ˆà¸²à¹€à¸£à¸µà¸¢à¸‡à¸ˆà¸²à¸ UI (anchoredPosition) à¹€à¸žà¸£à¸²à¸° GridLayoutGroup / timing / headless server à¸—à¸³à¹ƒà¸«à¹‰à¹€à¸žà¸µà¹‰à¸¢à¸™à¹„à¸”à¹‰
        // à¹€à¸£à¸µà¸¢à¸‡à¸ˆà¸²à¸ state à¸à¸±à¹ˆà¸‡ server: ColNet à¹à¸¥à¹‰à¸§à¸„à¸­à¸¡à¹à¸žà¸„à¹ƒà¸«à¹‰à¹€à¸›à¹‡à¸™ 0..n-1
        List<DuckCard> ducks = FindDucksInRow(0);
        ducks.Sort((a, b) =>
        {
            int c = a.ColNet.CompareTo(b.ColNet);
            if (c != 0) return c;
            return a.netId.CompareTo(b.netId);
        });

        for (int i = 0; i < ducks.Count; i++)
            ducks[i].ServerAssignToZone(ZoneKind.DuckZone, 0, i);

        // à¸”à¸±à¸™ order à¹„à¸›à¸à¸±à¹ˆà¸‡ client à¹ƒà¸«à¹‰ GridLayoutGroup à¸ˆà¸±à¸”à¸•à¸³à¹à¸«à¸™à¹ˆà¸‡à¸„à¸­à¸¥à¸±à¸¡à¸™à¹Œà¸–à¸¹à¸à¸—à¸±à¸™à¸—à¸µ
        Server_PushDuckZoneOrder(0);
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
        EnsureSharedActionPoolOwnerAndInit();
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
            ;
        // log ??????? key
        ;
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
        // 3) Build/rotate authoritative TurnOrder from DuckZone front card
        var tm = TurnManager.Instance;
        if (tm != null)
        {
            tm.ServerRebuildTurnOrder("MatchStart");
            tm.ServerPickStarterFromDuckZoneAndRotate("MatchStart");
            tm.ServerRequestClientLayoutRefresh("MatchStart");
        }
        else
        {
            Debug.LogWarning("[PlayerManager] TurnManager.Instance is null on server (BarrierGo).");
        }

        // 4) Deal three action cards to every connected player
        foreach (var pm in players)
        {
            var conn = ServerResolveConnectionByPlayerNetId(pm.netId);

            for (int i = 0; i < 3; i++)
            {
                if (!host.Server_DrawActionCardFor(conn, pm.netId))
                    break;
            }
        }
        ;
    }
    [Server]
    private void EnsureSharedActionPoolOwnerAndInit()
    {
        if (s_actionPoolOwnerNetId == 0 ||
            !NetworkServer.spawned.TryGetValue(s_actionPoolOwnerNetId, out NetworkIdentity ownerNi) ||
            ownerNi == null ||
            !ownerNi.TryGetComponent(out PlayerManager ownerPm) ||
            ownerPm == null)
        {
            s_actionPoolOwnerNetId = netId;
            InitializeActionCardPool();
            return;
        }

        if (s_actionPoolOwnerNetId != netId && actionCardPool.Count > 0)
            actionCardPool.Clear();
    }

    [Server]
    private static PlayerManager ServerGetActionPoolOwner()
    {
        if (s_actionPoolOwnerNetId != 0 &&
            NetworkServer.spawned.TryGetValue(s_actionPoolOwnerNetId, out NetworkIdentity ownerNi) &&
            ownerNi != null &&
            ownerNi.TryGetComponent(out PlayerManager ownerPm) &&
            ownerPm != null)
        {
            return ownerPm;
        }

        foreach (var kv in NetworkServer.connections)
        {
            NetworkConnectionToClient conn = kv.Value;
            if (conn == null || conn.identity == null)
                continue;

            PlayerManager pm = conn.identity.GetComponent<PlayerManager>();
            if (pm == null || !pm.isActiveAndEnabled || pm.SeatIndex < 0)
                continue;

            s_actionPoolOwnerNetId = pm.netId;
            return pm;
        }

        return null;
    }

    [Server]
    public static int ServerGetSharedActionPoolRemaining()
    {
        PlayerManager owner = ServerGetActionPoolOwner();
        if (owner == null)
            return 0;

        int total = 0;
        foreach (var kv in owner.actionCardPool)
        {
            if (kv.Value > 0)
                total += kv.Value;
        }

        return total;
    }

    [Server]
    private void InitializeActionCardPool()
    {
        actionCardPool.Clear();
        actionCardPool.Add("Shoot", 10);
        actionCardPool.Add("QuickShot", 10);
        actionCardPool.Add("TekeAim", 10);
        actionCardPool.Add("DoubleBarrel", 10);
        actionCardPool.Add("Misfire", 10);
        actionCardPool.Add("TwoBirds", 10);
        actionCardPool.Add("BumpLeft", 10);
        actionCardPool.Add("BumpRight", 10);
        actionCardPool.Add("LineForward", 10);
        actionCardPool.Add("MoveAhead", 10);
        actionCardPool.Add("HangBack", 10);
        actionCardPool.Add("FastForward", 10);
        actionCardPool.Add("DisorderlyConduckt", 10);
        actionCardPool.Add("DuckShuffle", 10);
        actionCardPool.Add("GivePeaceAChance", 10);
        actionCardPool.Add("Resurrection", 10);
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
    // =================================================================
    // DuckZone UI order sync (GridLayoutGroup friendly)
    // =================================================================
    [Command(requiresAuthority = false)]
    public void CmdSyncDuckCards()
    {

        Server_PushDuckZoneOrder(0);
    }

    [Server]
    private void Server_PushDuckZoneOrder(int row)
    {
        List<DuckCard> ducks = FindDucksInRow(row);
        ducks.Sort((a, b) =>
        {
            int c = a.ColNet.CompareTo(b.ColNet);
            if (c != 0) return c;
            return a.netId.CompareTo(b.netId);
        });

        uint[] ordered = new uint[ducks.Count];
        for (int i = 0; i < ducks.Count; i++)
            ordered[i] = ducks[i].netId;

        RpcApplyDuckZoneOrder(row, ordered);
    }

    [ClientRpc]
    private void RpcApplyDuckZoneOrder(int row, uint[] orderedDuckNetIds)
    {
        if (!NetworkClient.active) return;

        if (DuckZone == null)
        {
            DuckZone = GameObject.Find("DuckZone");
            if (DuckZone == null) return;
        }

        try
        {
            Transform dz = DuckZone.transform;

            for (int i = 0; i < orderedDuckNetIds.Length; i++)
            {
                uint id = orderedDuckNetIds[i];
                if (!NetworkClient.spawned.TryGetValue(id, out NetworkIdentity ni) || ni == null) continue;

                if (ni.transform.parent != dz)
                    ni.transform.SetParent(dz, false);

                ni.transform.SetSiblingIndex(i);
            }

            var rt = dz as RectTransform;
            if (rt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                Canvas.ForceUpdateCanvases();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcApplyDuckZoneOrder] à¸‚à¸±à¸”à¸‚à¹‰à¸­à¸‡: {ex}");
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
        ;
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
            ;
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
            ;
        // 3) ???
        var total = GetTotalDuckCounts();
        foreach (var kv in total)
            ;
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
        List<DuckCard> ducks = FindDucksInRow(shotRow);
        for (int i = 0; i < ducks.Count; i++)
        {
            DuckCard duck = ducks[i];
            if (duck != null && duck.ColNet > shotCol)
                duck.ServerAssignToZone(ZoneKind.DuckZone, shotRow, duck.ColNet - 1);
        }

        Server_PushDuckZoneOrder(shotRow);
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
            Debug.LogError($"[RpcAddCardToDuckZone] à¸‚à¸±à¸”à¸‚à¹‰à¸­à¸‡: {ex}");
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
        PlayerManager owner = ServerGetActionPoolOwner();
        if (owner == null)
            return null;

        List<string> availableCards = new List<string>();
        foreach (var card in owner.actionCardPool)
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
        owner.actionCardPool[selectedCard]--;  // Shared action pool
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
        ;
        // Spawn ????????????????????????
        SpawnAndAddCardToDuckZone(drawnCard);
    }

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
    // ? Client ??????????????????? Command
    // ? Command ??? Client ?????????????? Server
    private GameObject FindCardPrefabByName(string cardName)
    {
        if (actionCardPrefabMap != null && actionCardPrefabMap.TryGetValue(cardName, out var prefab))
            return prefab;
        Debug.LogWarning($"?? Action card ï¿½{cardName}ï¿½ not found!");
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
            ;
            return;
        }

        TurnManager tm = TurnManager.Instance;
        if (tm != null)
        {
            uint turnNetId = tm.ServerGetCurrentTurnNetId();
            if (turnNetId != 0 && turnNetId != netId)
            {
                Debug.LogWarning($"[CmdPlayCard] Reject out-of-turn play playerNetId={netId} currentTurnNetId={turnNetId}");
                return;
            }
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
                ;
                // ...
            }
            RpcShowCard(card.GetComponent<NetworkIdentity>(), "Played");
            tm?.ServerNotifyCardPlayed(netId, card.name);
            // ---------------------------------------------------------
            // ??  ???????????????????????
            // ---------------------------------------------------------
            // (?????) ????????? 1 ???? ??? SyncVar (zone) ???????????????????? ???????????????
            StartCoroutine(DrawNextCardCoroutine(connectionToClient, netId));
        }
        else
        {
            Debug.LogError("Card has been destroyed or not found in the scene.");
        }
    }
    // ========================================================
    // Helpers ?????? LineForward/DuckShuffle
    // ========================================================
    private IEnumerator DelayedLog()
    {
        yield return null;
    }

    [Server]
    private void RemoveTargetFromCard(NetworkIdentity duckNi)
    {
        if (duckNi == null) return;
        uint targetId = duckNi.netId;

        foreach (var tf in FindObjectsOfType<TargetFollow>())
            if (tf != null && tf.targetNetId == targetId)
                NetworkServer.Destroy(tf.gameObject);

        foreach (var mk in FindObjectsOfType<TargetMarker>())
            if (mk != null && mk.FollowDuckNetId == targetId)
                NetworkServer.Destroy(mk.gameObject);
    }
    [Server]
    private void MoveTargetFromTo(NetworkIdentity fromCard, NetworkIdentity toCard)
    {
        if (fromCard == null || toCard == null) return;

        RemoveTargetFromCard(toCard);
        foreach (var tf in FindObjectsOfType<TargetFollow>())
        {
            if (tf != null && tf.targetNetId == fromCard.netId)
            {
                tf.targetNetId = toCard.netId;
                tf.ResetTargetTransform();

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
    // ShowCard Logic
    // ========================
    [ClientRpc]
    void RpcShowCard(NetworkIdentity cardIdentity, string type)
    {
        if (!NetworkClient.active) return;
        if (cardIdentity == null || cardIdentity.gameObject == null)
        {
            Debug.LogWarning("[RpcShowCard] cardIdentity or its gameObject is null.");
            return;
        }
        try
        {
            ;
            GameObject card = cardIdentity.gameObject;
            if (type == "Dealt")
            {
                card.SetActive(true);
                bool shouldShowBack = !cardIdentity.isOwned && EnemyArea != null;
                ApplyCardFace(card, showFront: !shouldShowBack);
            }
            else if (type == "Played")
            {
                card.SetActive(true);
                Canvas.ForceUpdateCanvases();
                var dropZone = FindObjectOfType<DropZone>();
                if (dropZone != null)
                    dropZone.PlaceCard(card);
                ApplyCardFace(card, showFront: true);
                if (isLocalPlayer && cardIdentity.isOwned)
                {
                    HandleCardActivation(card);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcShowCard] Error: {ex}");
        }
    }

    private static void ApplyCardFace(GameObject card, bool showFront)
    {
        if (card == null) return;

        Image cardImage = card.GetComponent<Image>();
        if (cardImage == null) return;

        CardFlipper flipper = card.GetComponent<CardFlipper>();
        if (flipper != null)
        {
            Sprite target = showFront ? flipper.CardFront : flipper.CardBack;
            if (target != null)
                cardImage.sprite = target;
        }

        if (!cardImage.enabled)
            cardImage.enabled = true;

        Color c = cardImage.color;
        if (c.a <= 0.01f)
        {
            c.a = 1f;
            cardImage.color = c;
        }

        CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup != null && canvasGroup.alpha <= 0.01f)
            canvasGroup.alpha = 1f;
    }

    private void HandleCardActivation(GameObject card)
    {
        if (!isLocalPlayer) return;

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
            ActiveSkillDescriptionUI.NotifySkillTriggered(selectedSkill);
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
            Debug.LogError("[CmdTargetOtherCard] target GameObject null à¸‚à¹‰à¸²à¸¡à¸„à¸³à¸ªà¸±à¹ˆà¸‡");
            return;
        }
        var opponentIdentity = target.GetComponent<NetworkIdentity>();
        if (opponentIdentity == null)
        {
            Debug.LogError("[CmdTargetOtherCard] target  NetworkIdentity à¸‚à¹‰à¸²à¸¡à¸„à¸³à¸ªà¸±à¹ˆà¸‡");
            return;
        }
        var conn = opponentIdentity.connectionToClient;
        if (conn == null)
        {
            Debug.LogWarning("[CmdTargetOtherCard] connectionToClient null à¸‚à¹‰à¸²à¸¡à¸„à¸³à¸ªà¸±à¹ˆà¸‡");
            return;
        }
        TargetOtherCard(conn);
    }
    [TargetRpc]
    void TargetSelfCard()
    {
        ;
    }
    [TargetRpc]
    void TargetOtherCard(NetworkConnection target)
    {
        ;
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
                ;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcIncrementClick] {ex}");
        }
    }
}


