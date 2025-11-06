using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
using UnityEngine.UI;
using System.Linq;
using System;
using Random = UnityEngine.Random;


public class PlayerManager : NetworkBehaviour
{

    // --- PATCH: Barrier Hooks ---
    private static bool s_barrierHooksBoundServer = false;
    private static bool s_barrierHooksBoundClient = false;

    // ให้ barrier เป็นคนสั่งเริ่มแจก/เริ่มเกม (ค่าเริ่ม true)
    // ถ้ากลับไปใช้ดีเลย์เดิม ให้ตั้งเป็น false
    public static bool DeferInitialDealToBarrier = true;

    // ป้องกันเริ่มแมตช์ซ้ำ เมื่อ BarrierGoServer ถูกยิงหลายครั้ง
    private static bool s_matchStarted = false;

    // ============= GameObject References =============

    // การ์ด แอคชั่น
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
    // public GameObject resurrectionPrefab;
    // public GameObject duckAndCoverPrefab;


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
    // === NEW: รองรับ 5 ช่องศัตรู ===
    [Header("Enemies Slots (up to 5)")]
    [SerializeField] private string enemiesAreaRootName = "EnemiesArea";   // ชื่อ parent
    [SerializeField] private string enemySlotPrefix = "EnemyArea";      // EnemyArea1..5

    // ที่นั่งของผู้เล่น (ใช้ที่นี่เพื่อจัดตำแหน่งศัตรูให้คงที่)
    [SyncVar] public int seatIndex = -1;

    // แคชสล็อตศัตรู (ฝั่ง client ใช้รวมกัน)
    private static Transform[] s_enemySlots = null;

    // map: netId ของ PlayerManager (ศัตรู) -> slot index [0..4]
    private static readonly Dictionary<uint, int> s_remoteSlotIndex = new Dictionary<uint, int>();



    //////////////////////////////////////////////////////////////////////
    public static PlayerManager localInstance;

    // ========== Resurrection  State ==========
    private bool isResurrectionModeActive = false;
    // ========== GivePeaceAChance  State ==========
    private bool isGivePeaceActive = false;
    // ========== DuckShuffle  State ==========
    [SyncVar] private bool isDuckShuffleActive = false;
    public bool IsDuckShuffleActive => isDuckShuffleActive;
    // ========== DisorderlyConduckt  State ==========
    [SyncVar] private bool isDisorderlyConducktActive = false;
    public bool IsDisorderlyConducktActive => isDisorderlyConducktActive;
    private DuckCard firstSelectedDuck = null; // เก็บการ์ดใบแรกที่เลือก

    // ========== FastForward  State ==========
    [SyncVar] private bool isFastForwardActive = false;
    public bool IsFastForwardActive => isFastForwardActive;
    // ========== HangBack  State ==========
    [SyncVar] private bool isHangBackActive = false;
    public bool IsHangBackActive => isHangBackActive;
    // ========== MoveAhead  State ==========
    [SyncVar] private bool isMoveAheadActive = false;
    public bool IsMoveAheadActive => isMoveAheadActive;
    // ========== LineForward  State ==========
    [SerializeField] private GameObject cardPoolLineForward; // สมมติว่าเป็น Parent วาง "การ์ดที่กลับสู่ pool"
    public bool isLineForwardActive = false;

    public bool IsLineForwardActive => isLineForwardActive;
    // ========== BumpRight  State ==========
    [SyncVar] private bool isBumpRightActive;
    public bool IsBumpRightActive => isBumpRightActive;
    // ========== BumpLeft  State ==========
    [SyncVar] private bool isBumpLeftActive;
    public bool IsBumpLeftActive => isBumpLeftActive;

    // ========== TwoBirds State ==========
    [SyncVar] private bool isTwoBirdsActive;
    public bool IsTwoBirdsActive => isTwoBirdsActive;

    private NetworkIdentity firstTwoBirdsCard = null;
    private int twoBirdsClickCount = 0;

    // ========== DoubleBarrel State ==========
    [SyncVar] private bool isDoubleBarrelActive = false;

    // ตัวนับว่าเราคลิกการ์ด DoubleBarrel ไปกี่ใบแล้ว (0,1,...)
    private int doubleBarrelClickCount = 0;
    // เก็บ Card ใบแรกที่คลิก
    private NetworkIdentity firstClickedCard = null;

    //  ========== Misfire State ==========
    [SyncVar] private bool isMisfireActive = false;
    // สำหรับเช็กว่าอยู่ในโหมด MisfireAim หรือเปล่า
    public bool IsMisfireActive => isMisfireActive;


    //  ========== Shoot State ==========
    [SyncVar] bool isShootActive;
    //  ========== QuickShot State ==========
    [SyncVar] bool isQuickShotActive;

    [SerializeField] private GameObject targetPrefab;

    // ============= Card Collections =============
    [SyncVar] public int playerID;
    [Header("Action Card Prefab List")]
    [SerializeField]
    private List<GameObject> actionCardPrefabList; // Prefabs ของการ์ดแอคชั่นทั้งหมด
    private Dictionary<string, GameObject> actionCardPrefabMap;

    private List<GameObject> cards = new List<GameObject>();
    private Dictionary<GameObject, int> cardPool = new Dictionary<GameObject, int>();
    public readonly SyncDictionary<string, int> actionCardPool = new SyncDictionary<string, int>();
    private bool isTekeAimActive = false;

    [SyncVar]
    private uint targetedDuckNetId;



    void Start()
    {
        // ถ้า DuckZone ไม่ใช่ null ให้ Subscribe Event OnCardClicked ให้การ์ดข้างใน
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

    // === Turn state (เทิร์นแรก + ออเดอร์ซ้ายมือ) ===
    // Mirror ห้าม SyncVar แบบ static → เก็บ static ใช้ในโค้ด
    private static int s_currentTurnSeat = -1;

    // สำเนาแบบ SyncVar (instance) เพื่อซิงก์ไป client ทุกคน
    [SyncVar(hook = nameof(OnTurnSeatChanged))]
    private int _currentTurnSeatNet = -1;

    // Hook: ถูกเรียกบน client เมื่อค่า _currentTurnSeatNet เปลี่ยน
    private void OnTurnSeatChanged(int oldValue, int newValue)
    {
        s_currentTurnSeat = newValue;
    }

    // ออเดอร์เทิร์น (ใช้ภายในโค้ด)
    private static readonly List<int> s_turnOrder = new List<int>();

    // สีเป็ดผู้เล่น (SyncVar นี้ของคุณอยู่เดิม)
    [SyncVar] public int duckColorIndex = 0; // 0..N-1









    //////////////////////////////////////////  Barrier ////////////////////////////////////////////////////////////////////


    // ไคลเอนต์: หลัง barrier ปล่อย ให้ local player เริ่มวงจั่วอัตโนมัติ
    [Client]
    private static void OnBarrierGo_Client()
    {
        if (DeferInitialDealToBarrier && localInstance != null)
            localInstance.StartAutoDrawIfLocal();
    }

    // ผูก event จาก GameplayLoadCoordinator แค่ครั้งเดียว
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

    // อินสแตนซ์: สั่งเริ่มวงจั่วเฉพาะของ local player
    [Client]
    private void StartAutoDrawIfLocal()
    {
        if (isLocalPlayer)
            StartCoroutine(AutoDrawCards());
    }




    public override void OnStartClient()
    {
        base.OnStartClient();

        // 1) ผูก Barrier ฝั่งไคลเอนต์ (กันซ้ำภายใน)
        TryBindBarrierClient();

        // 2) หาโซนแชร์ในซีน
        DropZone = GameObject.Find("DropZone");
        DuckZone = GameObject.Find("DuckZone");

        // 3) แคช EnemyArea1..5 จากซีน (ถ้ายังไม่เจอจะพยายามหาใหม่ในฟังก์ชัน)
        CacheEnemySlotsFromScene();

        // 4) ถ้าเป็นไคลเอนต์ที่เราเป็นเจ้าของ → ชี้ PlayerArea
        var ni = GetComponent<NetworkIdentity>();
        if (ni != null && ni.isOwned)
        {
            PlayerArea = GameObject.Find("PlayerArea");
        }

        // 5) คำนวณเลย์เอาต์ตาม “วงกลมตายตัว 1..6” ให้ทุกคนบนไคลเอนต์นี้
        //    (จะ map ตัวเราไป PlayerArea, คนอื่นไป EnemyArea1..5 ตาม (seat - localSeat))
        RecomputeLocalLayoutBySeat();

        // 6) (ตัวเลือก) ถ้ายังไม่ได้ defer ไปให้ Barrier ก็เริ่มจั่วออโต้เหมือนเดิม
        if (ni != null && ni.isOwned && !DeferInitialDealToBarrier)
            StartCoroutine(AutoDrawCards());
    }


    public override void OnStopClient()
    {
        base.OnStopClient();
        // เมื่อมีคนหายไป → จัดสรรสล็อตศัตรูใหม่
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

    // Helper หา/จัดสรรสล็อตศัตรู
    // หา/แคช EnemyArea1..5 จาก Scene
    private void CacheEnemySlotsFromScene()
    {
        if (s_enemySlots != null && s_enemySlots.All(t => t != null)) return;

        var root = GameObject.Find(enemiesAreaRootName);
        if (root == null)
        {
            // ไม่มี EnemiesArea ก็ข้ามไป (จะ fallback ไปใช้ EnemyArea เดียว)
            s_enemySlots = null;
            return;
        }

        s_enemySlots = new Transform[5];
        for (int i = 0; i < 5; i++)
        {
            string childName = $"{enemySlotPrefix}{i + 1}";
            var child = root.transform.Find(childName);
            if (child == null) child = GameObject.Find(childName)?.transform;
            s_enemySlots[i] = child;
        }
    }

    /// คืน Transform ของสล็อตตามค่า rel (0..5)
    /// rel=0 -> PlayerArea (ของ local), rel=1..5 -> EnemyArea1..5
    private Transform GetSlotByRelIndex(int rel)
    {
        if (rel == 0)
        {
            // สำหรับ local เท่านั้น: ใช้ PlayerArea ที่เราหามาใน OnStartClient
            return PlayerArea != null ? PlayerArea.transform : null;
        }

        // ให้แน่ใจว่าเราแคช EnemyArea1..5 แล้ว
        if (s_enemySlots == null || s_enemySlots.Any(t => t == null))
            CacheEnemySlotsFromScene();

        int idx = rel - 1; // 1..5 -> 0..4
        if (s_enemySlots != null && idx >= 0 && idx < s_enemySlots.Length)
            return s_enemySlots[idx];

        return null;
    }


    // เลือก slot ให้ PlayerManager (ศัตรูตัวนี้) ตามลำดับ seatIndex ของทุกคน (ยกเว้น local)
    [Client]
    private void RecomputeLocalLayoutBySeat()
    {
        // หา local seat
        var owned = FindObjectsOfType<PlayerManager>()
            .FirstOrDefault(p =>
            {
                var ni = p.GetComponent<NetworkIdentity>();
                return ni != null && ni.isOwned;
            });

        if (owned == null)
        {
            // ยังหา local ไม่เจอ รอเฟรมถัดไป
            StartCoroutine(_RecomputeNextFrame());
            return;
        }

        int localSeat = Mathf.Clamp(owned.seatIndex, 0, 5);

        // นับทั้งหมด (2..6)
        var all = FindObjectsOfType<PlayerManager>().ToList();
        int total = Mathf.Clamp(all.Count, 2, 6);

        // เคลียร์แมพสล็อตเก่า
        s_remoteSlotIndex.Clear();

        foreach (var pm in all)
        {
            var ni = pm.GetComponent<NetworkIdentity>();
            if (ni != null && ni.isOwned)
            {
                // ของเรา → PlayerArea เสมอ (rel=0)
                pm.PlayerArea = GameObject.Find("PlayerArea");
                continue;
            }

            // ของศัตรู → คำนวณ rel แล้วแมปไป EnemyArea1..5
            int rel = ((pm.seatIndex - localSeat) % 6 + 6) % 6; // safe mod
            if (rel == 0) rel = 1; // กันเหตุ edge (ไม่ควรเกิดหาก seatIndex ไม่ชนกัน)

            var t = GetSlotByRelIndex(rel);
            if (t != null)
            {
                pm.EnemyArea = t.gameObject;
                // เก็บดัชนีไว้ถ้าจำเป็น (เช่นเอาไป anim/จัดเรียง UI)
                s_remoteSlotIndex[pm.netId] = rel - 1; // 0..4
            }
            else
            {
                // fallback เดิม
                pm.EnemyArea = GameObject.Find("EnemyArea");
            }
        }

        // (ออปชัน) ดีบักดูผล
        Debug.Log($"[Layout] localSeat={localSeat}, total={total}");
        foreach (var pm in all) Debug.Log($" [Seat] netId={pm.netId} seat={pm.seatIndex} rel={((pm.seatIndex - localSeat + 6) % 6)}");
    }

    private IEnumerator _RecomputeNextFrame()
    {
        yield return null;
        RecomputeLocalLayoutBySeat();
    }

    // คืน Transform ของสล็อตศัตรูที่ถูกจองให้ PlayerManager ตัวนี้ (ถ้าไม่มีจะเป็น null)
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

    // server: แจก seatIndex ช่องว่างถัดไป 0..5
    [Server]
    private void EnsureSeatIndexAssigned()
    {
        if (seatIndex >= 0) return;

        // เก็บที่นั่งที่ถูกใช้ไปแล้ว
        var used = new HashSet<int>();
        foreach (var pm in FindObjectsOfType<PlayerManager>())
            if (pm.seatIndex >= 0) used.Add(pm.seatIndex);

        // หาเลขว่าง 0..5
        for (int i = 0; i < 6; i++)
            if (!used.Contains(i)) { seatIndex = i; return; }

        // กันพลาด
        seatIndex = 5;
    }


    // ลำดับสีต้อง “ตรงกับ index ที่เลือกในล็อบบี้”
    private static readonly string[] DUCK_KEYS_BY_INDEX =
    {
    "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    // ปรับให้ตรงกับ index จริงของคุณได้เลย
    };

    private static string ColorIndexToDuckKey(int idx)
    {
        return (idx >= 0 && idx < DUCK_KEYS_BY_INDEX.Length) ? DUCK_KEYS_BY_INDEX[idx] : null;
    }

    [Server]
    private static HashSet<string> Server_GetSelectedDuckKeysFromLobby()
    {
        var keys = new HashSet<string>();
        // อ่านจาก PlayerManager ทุกตัว (ต้องให้ PlayerManager มี/รับค่า duckColorIndex มาจากล็อบบี้)
        foreach (var pm in FindObjectsOfType<PlayerManager>())
        {
            string key = ColorIndexToDuckKey(pm.duckColorIndex);
            if (!string.IsNullOrEmpty(key)) keys.Add(key);
        }
        return keys;
    }

    // ถูกเรียกจาก OnBarrierGo_Server() หลังทุกคนโหลดเสร็จ
    [Server]
    private void Server_BeginMatch_AfterBarrier()
    {
        // 1) เติม DuckZone ให้ครบ 6 จาก pool ที่ “เฉพาะสีที่เลือก”
        RefillDuckZoneIfNeeded();

        // 2) เลือกคนเริ่มจากสีของ “การ์ดบนสุด”
        Server_PickStarterFromTopDuckCard_AndBuildOrder();

        // (ถ้ามีระบบเทิร์น) เริ่มเทิร์นแรกได้เลย เช่น:
        // TurnSystem.Server_BeginFirstTurn(s_currentTurnSeat, s_turnOrder);
    }

    [Server]
    private static void Server_PickStarterFromTopDuckCard_AndBuildOrder()
    {
        var any = FindObjectsOfType<PlayerManager>().FirstOrDefault();
        if (any == null || any.DuckZone == null) return;

        // ✅ ทำให้แน่ใจว่าเราทำงานกับ Transform เสมอ
        var zone = any.DuckZone.transform;

        // หาใบบนสุดที่ "ไม่ใช่ Marsh"
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

        // ถ้าทั้งกองเป็น Marsh หรือหาคีย์ไม่ได้ → fallback เป็นที่นั่งต่ำสุด
        PlayerManager starter = null;
        if (!string.IsNullOrEmpty(topKey))
            starter = players.FirstOrDefault(p => ColorIndexToDuckKey(p.duckColorIndex) == topKey);

        if (starter == null)
            starter = players.OrderBy(p => p.seatIndex).FirstOrDefault();

        s_currentTurnSeat = (starter != null) ? starter.seatIndex : 0;
        if (any != null)
        {
            any._currentTurnSeatNet = s_currentTurnSeat; // เซิร์ฟเวอร์เซ็ต → Mirror sync ไปทุก client → hook อัปเดต static
        }

        // ลำดับซ้ายมือ (ถ้าทิศตรงข้าม ให้เปลี่ยน +i เป็น -i)
        s_turnOrder.Clear();
        for (int i = 0; i < total; i++)
        {
            int seat = (s_currentTurnSeat + i) % total;
            s_turnOrder.Add(seat);
        }

        Debug.Log($"[Turn] Starter seat = {s_currentTurnSeat}, order = {string.Join(",", s_turnOrder)}");

        // ✅ แจ้งทุกคลไคลเอนต์ให้คำนวณเลย์เอาต์ใหม่ตามวงกลมตายตัว 1..6
        var caller = any; // ใช้อินสแตนซ์ PM ใดก็ได้บนเซิร์ฟเวอร์
        if (caller != null)
            caller.RpcRecomputeLayoutAllClients();  // <<< เพิ่มตรงนี้
    }

    [ClientRpc]
    public void RpcRecomputeLayoutAllClients()
    {
        RecomputeLocalLayoutBySeat();
    }

    // แปลงชื่อ GameObject การ์ด → DuckKey ("DuckBlue"...)
    private static string ExtractDuckKeyFromCard(GameObject go)
    {
        var name = go.name.Replace("(Clone)", "").Trim();

        // Marsh มาก่อนเพื่อแมตช์แบบชัดๆ
        if (name.IndexOf("Marsh", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "Marsh";

        foreach (var key in DUCK_KEYS_BY_INDEX)
        {
            if (name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return key;
        }
        return null;
    }


    // คืน DuckZone ของ "อินสแตนซ์ในซีน" เสมอ
    private Transform GetSceneDuckZone()
    {
        // ถ้ามี DuckZone อ้างอยู่ และมันอยู่ในซีนจริง
        if (DuckZone != null)
        {
            var t = DuckZone.transform;
            if (t != null && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded)
                return t;
        }
        // หาใหม่จากชื่อในซีน
        var go = GameObject.Find("DuckZone");
        return go != null ? go.transform : null;
    }



    // ==== วางไว้ใน PlayerManager.cs (ส่วน server helpers) 

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

        // บอกการ์ดให้เซ็ตโซน/ตำแหน่ง (DuckCard จะจัด parent ทั้ง server+client ผ่าน SyncVar hook)
        dc.ServerAssignToZone(zone, row, col);
    }











    // ========================
    // OnStartServer, Deal Card
    // ========================
    // ใช้: using System.Linq;

    public override void OnStartServer()
    {
        base.OnStartServer();

        // 1) ผูก Barrier ฝั่งเซิร์ฟเวอร์
        TryBindBarrierServer();

        // 2) เซ็ตที่นั่ง + เด็ค Action เท่านั้น (เด็คเป็ดไปทำตอน Barrier)
        EnsureSeatIndexAssigned();
        InitializeActionCardPool();

        // 3) แม็ป Prefab ของ Action Card ให้คำสั่งจั่วใช้งานได้
        actionCardPrefabMap = new Dictionary<string, GameObject>();
        if (resurrectionPrefab != null) actionCardPrefabMap["Resurrection"] = resurrectionPrefab;
        if (duckAndCoverPrefab != null) actionCardPrefabMap["DuckAndCover"] = duckAndCoverPrefab;
        foreach (var prefab in actionCardPrefabList)
            if (prefab != null && !actionCardPrefabMap.ContainsKey(prefab.name))
                actionCardPrefabMap[prefab.name] = prefab;

        // ❌ อย่าประกอบเด็คเป็ด/อย่าเติม DuckZone ที่นี่
        CmdSyncDuckCards();
    }


    [Server]
    private static HashSet<string> Server_GetSelectedDuckKeysFromRoom()
    {
        var keys = new HashSet<string>();
        // อ่านจาก PlayerManager (GamePlayer) ที่ถูกคัดลอกจากล็อบบี้มาแล้ว
        foreach (var pm in FindObjectsOfType<PlayerManager>())
        {
            int idx = pm.duckColorIndex;
            if (idx >= 0 && idx < DUCK_KEYS_BY_INDEX.Length)
                keys.Add(DUCK_KEYS_BY_INDEX[idx]);
        }

        // log รายชื่อ + index ที่เกมเพลย์เห็น
        foreach (var pm in FindObjectsOfType<PlayerManager>())
            Debug.Log($"[Deck][SeenInGameplay] netId={pm.netId} seat={pm.seatIndex} colorIndex={pm.duckColorIndex}");

        // log สรุปชุด key
        Debug.Log("[Deck][SelectedFromRoom] " + string.Join(",", keys));

        return keys;
    }

    [Server]
    private static void OnBarrierGo_Server()
    {
        if (s_matchStarted) return;
        s_matchStarted = true;

        // เอาอินสแตนซ์ใดก็ได้เพื่อดึง prefab reference
        var any = FindObjectsOfType<PlayerManager>().FirstOrDefault();
        if (any == null) return;

        // 1) สร้างเด็คจาก “สีที่ผู้เล่นเลือก” + Marsh (สีละ 5)
        var duckPrefabs = new Dictionary<string, GameObject>
    {
        { "DuckBlue",   any.DuckBluePrefab   },
        { "DuckOrange", any.DuckOrangePrefab },
        { "DuckPink",   any.DuckPinkPrefab   },
        { "DuckGreen",  any.DuckGreenPrefab  },
        { "DuckYellow", any.DuckYellowPrefab },
        { "DuckPurple", any.DuckPurplePrefab },
        { "Marsh",      any.MarshPrefab      },
    };

        var selected = Server_GetSelectedDuckKeysFromRoom();
        selected.Add("Marsh");
        if (selected.SetEquals(new[] { "Marsh" })) selected.Add("DuckBlue"); // safety

        var selectedPrefabs = duckPrefabs
            .Where(kv => selected.Contains(kv.Key) && kv.Value != null)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        CardPoolManager.Initialize(selectedPrefabs, initialCount: 5);

        // 2) เติม DuckZone ให้ครบ 6 ใบ
        any.RefillDuckZoneIfNeeded();

        // 3) เลือกผู้เริ่มจากใบบนสุด (ข้าม Marsh) + สร้างลำดับเทิร์น
        Server_PickStarterFromTopDuckCard_AndBuildOrder();
    }





    [Server]
    private void InitializeActionCardPool()
    {
        actionCardPool.Clear();

        actionCardPool.Add("Shoot", 3);
        actionCardPool.Add("QuickShot", 3);
        actionCardPool.Add("TekeAim", 3);
        actionCardPool.Add("DoubleBarrel", 3);
        actionCardPool.Add("Misfire", 3);
        actionCardPool.Add("TwoBirds", 3);
        actionCardPool.Add("BumpLeft", 3);
        actionCardPool.Add("BumpRight", 3);
        actionCardPool.Add("LineForward", 3);
        actionCardPool.Add("MoveAhead", 3);
        actionCardPool.Add("HangBack", 3);
        actionCardPool.Add("FastForward", 3);
        actionCardPool.Add("DisorderlyConduckt", 3);
        actionCardPool.Add("DuckShuffle", 3);
        actionCardPool.Add("GivePeaceAChance", 3);
        actionCardPool.Add("Resurrection", 3);

    }
    private int GetDuckCardCountInDuckZone()
    {
        if (DuckZone == null) return 0;

        int count = 0;
        foreach (Transform child in DuckZone.transform)
        {
            // มี DuckCard component ไหม
            DuckCard duck = child.GetComponent<DuckCard>();
            if (duck != null)
            {
                count++;
            }
        }
        return count;
    }

    // ===== Helper: นับจำนวนการ์ดในโซน (ฝั่ง Server) =====
    [Server]
    private int Server_CountCardsInZone(ZoneKind z)
    {
        int c = 0;
        foreach (var dc in FindObjectsOfType<DuckCard>())
            if (dc.zone == z) c++;
        return c;
    }

    // ===== เติม DuckZone ถ้าขาด (เรียกซ้ำได้ ปลอดภัย) =====
    [Server]
    private void RefillDuckZoneIfNeeded()
    {
        int current = Server_CountCardsInZone(ZoneKind.DuckZone);
        if (current < 0) { Debug.LogError("[RefillDuckZoneIfNeeded] DuckZone count invalid."); return; }
        if (current >= 6) return;
        if (!CardPoolManager.HasCards()) { Debug.LogWarning("[RefillDuckZoneIfNeeded] No cards left in pool."); return; }

        int col = current; // จะเติมต่อจากตำแหน่งที่มีอยู่
        while (col < 6 && CardPoolManager.HasCards())
        {
            var card = CardPoolManager.DrawRandomCard();   // ❗ ไม่ส่ง parent
            if (card == null) break;

            var dc = card.GetComponent<DuckCard>();
            if (dc == null) { UnityEngine.Object.Destroy(card); continue; }

            // เซ็ต Zone/Row/Column ผ่าน SyncVar ก่อน Spawn
            dc.ServerAssignToZone(ZoneKind.DuckZone, 0, col);

            // ค่อย Spawn → SyncVar จะถูกส่งไปทุก client รวมคนมาช้า
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
        if (DuckZone == null)
        {
            // Debug.LogWarning("RpcSyncDuckCards: DuckZone not found!");
            return;
        }

        // ซิงค์การ์ดที่มีอยู่ใน DuckZone ให้ผู้เล่นใหม่
        foreach (Transform child in DuckZone.transform)
        {
            child.SetParent(DuckZone.transform, false);
        }
        // Debug.Log("DuckZone synced for the new player.");
    }

    // ===== สปอนแบบดีเลย์: เติมให้ครบ 6 ใบ โดยอิง ZoneKind + SyncVar =====
    [Server]
    private IEnumerator DealDuckCardsWithDelay()
    {
        // รอระบบ Mirror/ซีนพร้อมสั้น ๆ
        yield return new WaitForSeconds(0.25f);

        int col = Server_CountCardsInZone(ZoneKind.DuckZone);
        if (col < 0) { Debug.LogError("[DealDuckCardsWithDelay] DuckZone count invalid."); yield break; }

        // เติมจนครบ 6 หรือเด็คหมด
        while (col < 6 && CardPoolManager.HasCards())
        {
            var card = CardPoolManager.DrawRandomCard();   // ❗ ไม่ส่ง parent
            if (card == null) break;

            var dc = card.GetComponent<DuckCard>();
            if (dc == null) { UnityEngine.Object.Destroy(card); continue; }

            // ตั้งค่า SyncVar ก่อน Spawn (ให้ late-joiner ได้ค่าถูกต้องตั้งแต่เกิด)
            dc.ServerAssignToZone(ZoneKind.DuckZone, 0, col);

            NetworkServer.Spawn(card);

            col++;
            yield return null; // ขยับเฟรมให้ UI/hook ทำงานลื่น ๆ
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

        // สร้าง list เกมการ์ดที่ยังเหลือ (value > 0)
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

        // ลด stock
        cardPool[selectedCard] -= 1;

        // ถ้าหมดแล้ว ลบออกจาก dictionary ก็ได้
        if (cardPool[selectedCard] <= 0)
        {
            cardPool.Remove(selectedCard);
        }

        // พิมพ์ Log บอกว่าเราหยิบการ์ดอะไรมา, เหลือเท่าไหร่
        Debug.Log($"[GetRandomCardFromPool] Spawned: {selectedCard.name}. Left in that color: {(cardPool.ContainsKey(selectedCard) ? cardPool[selectedCard] : 0)}");

        // ปิดท้ายด้วย log สรุปทั้งหมด
        LogTotalDuckCounts();

        return selectedCard;
    }

    /// <summary>
    /// คืน Dictionary ของชื่อการ์ดเป็ด → จำนวนที่เหลือในเกม (pool + DuckZone)
    /// เรียกได้ทั้งฝั่ง server และ client (แต่ pool เฉพาะ server)
    /// </summary>
    public Dictionary<string, int> GetTotalDuckCounts()
    {
        var totalCounts = new Dictionary<string, int>();

        // 1) นับจาก pool (server only)
        var poolCounts = CardPoolManager.GetAllPoolCounts();
        foreach (var kv in poolCounts)
        {
            totalCounts[kv.Key] = kv.Value;
        }

        // 2) นับจาก DuckZone
        if (DuckZone != null)
        {
            foreach (Transform child in DuckZone.transform)
            {
                if (child.TryGetComponent<DuckCard>(out var duck))
                {
                    // Clean ชื่อ (ลบ "(Clone)")
                    string key = duck.gameObject.name.Replace("(Clone)", "").Trim();
                    if (totalCounts.ContainsKey(key))
                        totalCounts[key]++;
                    else
                        totalCounts[key] = 1;
                }
            }
        }

        return totalCounts;
    }

    /// <summary>
    /// ตัวอย่างการใช้งาน: พิมพ์สถานะปัจจุบันลง console
    /// </summary>
    [Server]  // สั่งบน server ก็พอ
    private void LogTotalDuckCounts()
    {
        // 1) ดูแค่ pool
        var poolCounts = CardPoolManager.GetAllPoolCounts();
        foreach (var kv in poolCounts)
            Debug.Log($"[PoolCounts] {kv.Key}: {kv.Value}");

        // 2) ดูแค่ใน DuckZone
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

        // 3) รวม
        var total = GetTotalDuckCounts();
        foreach (var kv in total)
            Debug.Log($"[TotalCounts] {kv.Key}: {kv.Value}");
    }





    private void ReorderDuckZoneLayout()
    {
        // สมมติ DuckZone อยู่บนแถวเดียว
        // ระยะห่างการ์ดแต่ละใบ = 150px
        float spacing = 150f;

        foreach (Transform child in DuckZone.transform)
        {
            DuckCard duck = child.GetComponent<DuckCard>();
            if (duck != null)
            {
                // สมมติคุณใช้ RectTransform
                RectTransform rt = child.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // เอา row, column ไปคำนวณ
                    rt.anchoredPosition = new Vector2(duck.Column * spacing, 0f);
                }
            }
        }
    }


    [Server]
    private void ShiftColumnsDown(int shotRow, int shotCol)
    {
        // วนทุก child ใน DuckZone
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard duck = child.GetComponent<DuckCard>();
            if (duck != null)
            {
                // ถ้าอยู่ row เดียวกัน และ column > shotCol
                if (duck.Row == shotRow && duck.Column > shotCol)
                {
                    duck.Column -= 1;
                    Debug.Log($"Shifted {duck.name} from col {duck.Column + 1} => {duck.Column}");
                }
            }
        }

        // หลังจากเลื่อน column เสร็จ ถ้าคุณมีฟังก์ชัน Layout UI ใหม่ ก็เรียกได้
        ReorderDuckZoneLayout();
    }




    [Server]
    private void SpawnAndAddCardToDuckZone(GameObject cardPrefab)
    {
        var dz = GetSceneDuckZone();
        if (dz == null) return;

        GameObject card = Instantiate(cardPrefab);   // ⬅️ ไม่ส่ง parent ตรงๆ
        NetworkServer.Spawn(card);

        if (card.TryGetComponent<DuckCard>(out var duck))
        {
            int realCount = 0; foreach (Transform t in dz) if (t.GetComponent<DuckCard>() != null) realCount++;
            duck.Row = 0; duck.Column = realCount;   // วางท้ายแถว
        }

        RpcAddCardToDuckZone(card);                  // ผูก parent ที่ client
    }


    [ClientRpc]
    private void RpcAddCardToDuckZone(GameObject card)
    {
        if (card == null) return;

        var dz = GetSceneDuckZone();
        if (dz != null)
            card.transform.SetParent(dz, false);
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



    // 🔹 ฟังก์ชันดึงการ์ดจาก pool
    // ✅ ฟังก์ชันสุ่มการ์ดจาก pool (ใช้ string แทน GameObject)
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
            // Debug.LogWarning("⚠️ No action cards left in the pool!");
            return null;
        }

        string selectedCard = availableCards[UnityEngine.Random.Range(0, availableCards.Count)];
        actionCardPool[selectedCard]--;  // ลดจำนวนการ์ดใน pool

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
            Debug.LogWarning("⚠️ No duck cards left in the pool!");
            return null;
        }

        GameObject selectedCard = availableCards[UnityEngine.Random.Range(0, availableCards.Count)];
        cardPool[selectedCard]--; // ลดจำนวนการ์ดลง
        return selectedCard;
    }








    // ========================
    // Auto Draw
    // ========================
    public void DrawRandomActionCard()
    {
        string cardName = GetRandomActionCardFromPool(); // ✅ รับค่าเป็น string
        if (cardName == null)
        {
            // Debug.LogWarning("❌ No action cards left in the pool!");
            return;
        }

        GameObject drawnCard = FindCardPrefabByName(cardName); // ✅ หา GameObject จากชื่อ
        if (drawnCard == null)
        {
            Debug.LogError($"❌ Cannot find prefab for card: {cardName}");
            return;
        }

        Debug.Log($"🎴 Drew action card: {drawnCard.name}");

        // Spawn หรือเพิ่มการ์ดให้ผู้เล่น
        SpawnAndAddCardToDuckZone(drawnCard);
    }

    private IEnumerator AutoDrawCards()
    {
        yield return new WaitForSeconds(5f);

        while (true)
        {
            if (PlayerArea != null && PlayerArea.transform.childCount < 3)
            {
                CmdDrawActionCard();
            }
            yield return new WaitForSeconds(1f);
        }
    }

    // ✅ Client ขอจั่วการ์ดโดยเรียก Command
    public void DrawActionCard()
    {
        if (isLocalPlayer)
        {
            CmdDrawActionCard();
        }
    }

    // ✅ Command ให้ Client ขอจั่วการ์ดจาก Server
    [Command]
    public void CmdDrawActionCard()
    {
        string cardName = GetRandomActionCardFromPool();
        if (string.IsNullOrEmpty(cardName))
        {
            Debug.LogWarning("❌ No action cards left in the pool!");
            return;
        }

        GameObject prefab = FindCardPrefabByName(cardName);
        if (prefab == null)
        {
            Debug.LogError($"❌ Cannot find prefab for card: {cardName}");
            return;
        }

        GameObject spawnedCard = Instantiate(prefab, Vector2.zero, Quaternion.identity);
        NetworkServer.Spawn(spawnedCard, connectionToClient);

        Debug.Log($"🎴 {connectionToClient} drew an action card: {spawnedCard.name}");

        RpcShowCard(spawnedCard, "Dealt");
    }


    private GameObject FindCardPrefabByName(string cardName)
    {
        if (actionCardPrefabMap != null && actionCardPrefabMap.TryGetValue(cardName, out var prefab))
            return prefab;

        Debug.LogWarning($"⚠️ Action card “{cardName}” not found!");
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

        // ตรวจสอบว่า card ยังมีอยู่ในเกมหรือไม่ก่อนที่จะเรียก Rpc
        if (card.scene.isLoaded)
        {
            RpcShowCard(card, "Played");
        }
        else
        {
            Debug.LogError("Card has been destroyed or not found in the scene.");
        }


    }

    private void RemoveCardFromGame(GameObject card)
    {
        if (card == null) return;
        NetworkServer.Destroy(card); // 🔥 ลบทิ้งจากเซิร์ฟเวอร์และซิงก์ไปยัง Client
        Debug.Log($"🗑️ {card.name} has been removed from the game.");
    }




    [ClientRpc]
    void RpcLogToClients(string message)
    {
        Debug.Log(message);
    }



    // ========================
    // TekeAim Logic
    // ========================
    [Command(requiresAuthority = false)]
    public void CmdActivateTekeAim()
    {
        // Debug.Log("CmdActivateTekeAim called on server. TekeAim is now active!");
        if (!isTekeAimActive)
        {
            isTekeAimActive = true;
            // Debug.Log("TekeAim activated on server.");
            RpcEnableTekeAim();
        }
        else
        {
            // Debug.Log("TekeAim was already active on server.");
        }
    }

    [ClientRpc]
    private void RpcEnableTekeAim()
    {
        isTekeAimActive = true;

        if (DuckZone == null)
        {
            // DuckZone = GameObject.Find("DuckZone");
            if (DuckZone == null)
            {
                Debug.LogError("RpcEnableTekeAim: DuckZone still null!");
                return;
            }
        }


        // Debug.Log("All DuckCards can be clicked for TekeAim now!");
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateTekeAim()
    {
        isTekeAimActive = false;
        // Debug.Log("TekeAim is now deactivated on server.");
        RpcDeactivateTekeAim();
    }
    [ClientRpc]
    void RpcDeactivateTekeAim()
    {
        // Debug.Log($"[RpcDeactivateShoot] (1) Client PM netId={netId}, isLocalPlayer={isLocalPlayer}");
        isTekeAimActive = false;
        // Debug.Log("[RpcDeactivateShoot] (2) ปิดโหมดยิงแล้ว (ฝั่ง Client).");
        // Debug.Log("[RpcDeactivateShoot] (3) Some extra debug to see if it's skipping or not!");
    }
    public bool IsTekeAimActive => isTekeAimActive;


    [Command(requiresAuthority = false)]
    public void CmdSpawnTarget(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null || targetPrefab == null) return;

        var dc = duckCardIdentity.GetComponent<DuckCard>();
        if (dc == null) return;

        GameObject newTarget = Instantiate(targetPrefab);
        var marker = newTarget.GetComponent<TargetMarker>();
        var tf = newTarget.GetComponent<TargetFollow>();

        if (marker != null)
        {
            // วางใน TargetZone, ใช้คอลัมน์เดียวกับการ์ด duck
            marker.ServerAssignToZone(ZoneKind.TargetZone, 0, dc.ColNet);
            marker.FollowDuckNetId = duckCardIdentity.netId;   // ซิงค์ให้ TargetFollow ผ่าน hook
        }

        // (สำรอง) ติดค่าให้ TargetFollow โดยตรงด้วยก็ได้ — แต่ marker ก็จัดให้แล้ว
        if (tf != null) tf.targetNetId = duckCardIdentity.netId;

        NetworkServer.Spawn(newTarget);
    }


    [ClientRpc]
    void RpcSetTargetNetId(NetworkIdentity targetIdentity, NetworkIdentity duckCardIdentity)
    {
        if (targetIdentity == null || duckCardIdentity == null)
        {
            Debug.LogError("[RpcSetTargetNetId] targetIdentity or duckCardIdentity is null!");
            return;
        }

        TargetFollow tf = targetIdentity.GetComponent<TargetFollow>();
        if (tf != null)
        {
            tf.targetNetId = duckCardIdentity.netId;
            tf.ResetTargetTransform();
        }

        RectTransform targetRect = targetIdentity.GetComponent<RectTransform>();
        RectTransform cardRect = duckCardIdentity.GetComponent<RectTransform>();

        if (targetRect != null && cardRect != null)
        {
            // หาโซนวางเป้า
            var tzObj = GameObject.Find("TargetZone");
            var zoneRect = tzObj.GetComponent<RectTransform>();

            // **ดึงขนาดและสเกลจาก Prefab**
            var prefabRect = targetPrefab.GetComponent<RectTransform>();
            Vector3 prefabScale = prefabRect.localScale;
            Vector2 prefabSize = prefabRect.sizeDelta;

            // ตั้ง parent โดยไม่เปลี่ยน local transform
            targetRect.SetParent(zoneRect, false);

            // คัดลอกสเกล + ขนาดมาจาก Prefab
            targetRect.localScale = prefabScale;
            targetRect.sizeDelta = prefabSize;

            // คำนวณตำแหน่งแบบเดิม
            Canvas mainCanvas = zoneRect.GetComponentInParent<Canvas>();
            Vector2 screenPos = RectTransformUtility
                .WorldToScreenPoint(mainCanvas.worldCamera, cardRect.position);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                zoneRect,
                screenPos,
                mainCanvas.worldCamera,
                out Vector2 localPoint
            );

            targetRect.anchoredPosition = localPoint + new Vector2(0f, 150f);
        }
    }

    // ========================
    // Shoot Logic
    // ========================
    // เรียกตอนวางการ์ด Shoot (ส่วนใหญ่เรียกจาก RpcShowCard)
    [Command(requiresAuthority = false)]
    public void CmdActivateShoot()
    {
        // Server เซตค่า
        isShootActive = true;
        // เรียก Rpc ถ้าต้องการแสดงข้อความ
        RpcActivateShoot();
    }

    [ClientRpc]
    void RpcActivateShoot()
    {
        // Client
        isShootActive = true;
        // Debug.Log("Shoot Mode is now active on all clients. You can click a targeted DuckCard to shoot it!");
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateShoot()
    {
        // Debug.Log($"[CmdDeactivateShoot] Server PM netId={netId}, isServer={isServer}, isClient={isClient}");
        isShootActive = false;
        // Debug.Log("[CmdDeactivateShoot] ปิดโหมดยิงแล้ว (ฝั่ง Server).");
        RpcDeactivateShoot();
    }

    [ClientRpc]
    void RpcDeactivateShoot()
    {
        // Debug.Log($"[RpcDeactivateShoot] (1) Client PM netId={netId}, isLocalPlayer={isLocalPlayer}");
        isShootActive = false;
        // Debug.Log("[RpcDeactivateShoot] (2) ปิดโหมดยิงแล้ว (ฝั่ง Client).");
        // Debug.Log("[RpcDeactivateShoot] (3) Some extra debug to see if it's skipping or not!");
    }


    public bool IsShootActive => isShootActive;

    /// <summary>
    /// เรียกตอนคลิกเป้าหมายที่อยากยิง
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdShootCard(NetworkIdentity duckCardIdentity)
    {
        if (!isShootActive)
        {
            return;
        }

        // 1) เช็กว่า duckCardIdentity ปกติ
        if (duckCardIdentity == null)
        {
            return;
        }
        // 2) ดึง DuckCard
        DuckCard shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null)
        {
            return;
        }

        int shotRow = shotDuck.Row;
        int shotCol = shotDuck.Column;
        // 3) เช็กว่า “มีเป้าเล็ง” ไหม
        if (!IsCardTargeted(duckCardIdentity))
        {
            return;
        }

        // 4) Destroy การ์ด
        NetworkServer.Destroy(duckCardIdentity.gameObject);
        // 4.1) ทำลายเป้าเล็งที่ชี้การ์ดนี้
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckCardIdentity.netId)
            {
                NetworkServer.Destroy(target.gameObject);
            }
        }

        // เรียกใช้ HandleDuckShot เพื่อความเป็นระบบเดียวกัน (ถ้ามี)
        // HandleDuckShot(duckCardIdentity.netId);

        // 4.2) เลื่อน column
        ShiftColumnsDown(shotRow, shotCol);

        // 5) ปิด Shoot Mode ทันที
        CmdDeactivateShoot();

        // 6) เติมการ์ดใหม่รอบหน้า (Refill)
        StartCoroutine(RefillNextFrame());
    }


    [Server]
    IEnumerator RefillNextFrame()
    {
        // รอจนจบเฟรม (ให้ Mirror เคลียร์การ์ดที่ถูก Destroy ออกจาก Hierarchy จริง ๆ)
        yield return null;

        // เรียกเติม
        RefillDuckZoneIfNeeded();
        // หรือถ้าอยากให้แน่ใจรออีกเฟรม ก็ทำ yield return null อีกรอบได้
    }


    /// <summary>
    /// ตัวอย่างฟังก์ชันเช็คว่าการ์ดใบนี้มีเป้าเล็งอยู่ไหม
    /// </summary>
    bool IsCardTargeted(NetworkIdentity duckCardIdentity)
    {
        // สมมติเราหา TargetFollow ที่เป็นเป้าทั้งหมดในฉาก
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            // ถ้า targetFollow นี้ชี้ว่า targetNetId = การ์ดที่เราคลิก
            if (target.targetNetId == duckCardIdentity.netId)
            {
                return true;
            }
        }
        return false;
    }


    // ========================
    // DoubleBarrel Logic
    // ========================
    /// <summary>
    /// เรียกเมื่อเล่นการ์ด DoubleBarrel (เช่น วางลง DropZone แล้ว)
    /// </summary>
    [Command]
    public void CmdActivateDoubleBarrel()
    {
        if (!isDoubleBarrelActive)
        {
            isDoubleBarrelActive = true;
            doubleBarrelClickCount = 0;
            firstClickedCard = null;

            RpcEnableDoubleBarrel();
        }
    }

    [ClientRpc]
    void RpcEnableDoubleBarrel()
    {
        // Debug.Log("DoubleBarrel Mode is now active on all clients. You can place 2 targets on adjacent cards in the same row!");
    }

    /// <summary>
    /// ปิดโหมด DoubleBarrel
    /// </summary>
    [Command]
    public void CmdDeactivateDoubleBarrel()
    {
        isDoubleBarrelActive = false;
        doubleBarrelClickCount = 0;
        firstClickedCard = null;

        RpcDisableDoubleBarrel();
    }

    [ClientRpc]
    void RpcDisableDoubleBarrel()
    {
        // Debug.Log("DoubleBarrel Mode is now deactivated on all clients.");
    }


    public bool IsDoubleBarrelActive => isDoubleBarrelActive;


    // ========== ฟังก์ชันสำหรับวางเป้าเล็ง 2 ใบ (เรียกจาก DuckCard.OnPointerClick) ==========

    [Command(requiresAuthority = false)]
    public void CmdDoubleBarrelClick(NetworkIdentity clickedCard)
    {
        if (!isDoubleBarrelActive) return;
        if (clickedCard == null) return;

        // สมมติว่า doubleBarrelClickCount เก็บจำนวนใบที่คลิก
        if (doubleBarrelClickCount == 0)
        {
            // ใบแรก
            firstClickedCard = clickedCard;
            doubleBarrelClickCount = 1;
            Debug.Log($"First card = {clickedCard.name}, waiting second...");
        }
        else if (doubleBarrelClickCount == 1)
        {
            // ใบสอง
            bool canPlace = CheckAdjacent(firstClickedCard, clickedCard);
            if (!canPlace)
            {
                Debug.LogWarning($"[CmdDoubleBarrelClick] {clickedCard.name} is NOT adjacent to {firstClickedCard.name} in same row. Canceling or ignoring.");
                return;
            }


            // ถ้า adjacent → spawn target 2 อัน
            CmdSpawnTargetDoubleBarre(firstClickedCard);
            CmdSpawnTargetDoubleBarre(clickedCard);
            // ปิดโหมด
            CmdDeactivateDoubleBarrel();
        }
    }


    /// <summary>
    /// เช็กว่า card1 อยู่แถวเดียวกับ card2 และ index ต่างกัน 1 หรือเปล่า
    /// วิธีเช็กขึ้นกับระบบคุณ
    /// </summary>
    [Server]
    private bool CheckAdjacent(NetworkIdentity card1, NetworkIdentity card2)
    {
        DuckCard duck1 = card1.GetComponent<DuckCard>();
        DuckCard duck2 = card2.GetComponent<DuckCard>();

        if (duck1 == null || duck2 == null) return false;

        Debug.Log($"Check: {duck1.name}(r{duck1.Row},c{duck1.Column}), {duck2.name}(r{duck2.Row},c{duck2.Column})");
        // ต้องอยู่แถวเดียวกัน
        if (duck1.Row != duck2.Row)
            return false;

        // ต้องเป็นคอลัมน์ติดกัน (ต่างกันแค่ 1)
        int diff = Mathf.Abs(duck1.Column - duck2.Column);
        return (diff == 1);
    }

    // =============================
    // ยืมฟังก์ชัน "SpawnTarget" แบบ TekeAim
    // =============================
    [Command(requiresAuthority = false)]
    public void CmdSpawnTargetDoubleBarre(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null || targetPrefab == null)
        {
            // Debug.LogError("[CmdSpawnTarget] duckCardIdentity or targetPrefab is null!");
            return;
        }

        // 1) สร้างเป้าเล็งบนเซิร์ฟ
        GameObject newTarget = Instantiate(targetPrefab);

        // 2) Spawn ให้ client เห็น
        NetworkServer.Spawn(newTarget);

        // 3) บอก client ผ่าน RPC ให้ตั้ง parent + ตำแหน่ง
        NetworkIdentity targetNi = newTarget.GetComponent<NetworkIdentity>();
        RpcSetTargetNetId(targetNi, duckCardIdentity);
    }


    // ========================
    // Quick Shot Logic
    // ========================
    // เรียกตอนวางการ์ด QuickShot (ส่วนใหญ่เรียกจาก RpcShowCard)
    [Command(requiresAuthority = false)]
    public void CmdActivateQuickShot()
    {
        // Server เซตค่า
        isQuickShotActive = true;
        // เรียก Rpc ถ้าต้องการแสดงข้อความ
        RpcActivateQuickShot();
    }

    [ClientRpc]
    void RpcActivateQuickShot()
    {
        // Client
        isQuickShotActive = true;
        // Debug.Log("QuickShot Mode is now active on all clients. You can click a targeted DuckCard to shoot it!");
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateQuickShot()
    {
        // Debug.Log($"[CmdDeactivateQuickShot] Server PM netId={netId}, isServer={isServer}, isClient={isClient}");
        isQuickShotActive = false;
        // Debug.Log("[CmdDeactivateQuickShot] ปิดโหมดยิงแล้ว (ฝั่ง Server).");
        RpcDeactivateQuickShot();
    }

    [ClientRpc]
    void RpcDeactivateQuickShot()
    {
        // Debug.Log($"[RpcDeactivateQuickShot] (ฝ1) Client PM netId={netId}, isLocalPlayer={isLocalPlayer}");
        isQuickShotActive = false;
        // Debug.Log("[RpcDeactivateQuickShot] (2) ปิดโหมดยิงแล้ว (ฝั่ง Client).");
        // Debug.Log("[RpcDeactivateQuickShot] (3ฝ) Some extra debug to see if it's skipping or not!");
    }


    public bool IsQuickShotActive => isQuickShotActive;

    /// <summary>
    /// เรียกตอนคลิกเป้าหมายที่อยากยิง
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdQuickShotCard(NetworkIdentity duckCardIdentity)
    {
        // 0) เช็ก QuickShot Mode บนเซิร์ฟ
        if (!isQuickShotActive)
        {
            return;
        }

        // 1) เช็กว่า duckCardIdentity ปกติ
        if (duckCardIdentity == null)
        {
            return;
        }

        // 2) ดึง DuckCard
        DuckCard shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null)
        {
            return;
        }

        int shotRow = shotDuck.Row;
        int shotCol = shotDuck.Column;

        // 4) Destroy การ์ด
        NetworkServer.Destroy(duckCardIdentity.gameObject);
        // 4.1) ทำลายเป้าเล็งที่ชี้การ์ดนี้
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckCardIdentity.netId)
            {
                NetworkServer.Destroy(target.gameObject);
            }
        }

        // 4.2) เลื่อน column
        ShiftColumnsDown(shotRow, shotCol);

        // 5) ปิด Shoot Mode ทันที
        CmdDeactivateQuickShot();

        // 6) เติมการ์ดใหม่รอบหน้า (Refill)
        StartCoroutine(RefillNextFrame());
    }

    // ========================
    // Misfire Logic
    // ========================
    // เรียกตอนวางการ์ด Misfire ลงสนาม
    [Command(requiresAuthority = false)]
    public void CmdActivateMisfire()
    {
        if (!isMisfireActive)
        {
            isMisfireActive = true;
            // Debug.Log("[CmdActivateMisfire] Misfire mode active on Server!");
            RpcEnableMisfire();
        }
    }

    [ClientRpc]
    void RpcEnableMisfire()
    {
        // Debug.Log("[RpcEnableMisfire] Misfire mode is now active on all clients. Next shot will misfire to a random adjacent duck!");
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateMisfire()
    {
        isMisfireActive = false;
        // Debug.Log("[CmdDeactivateMisfire] Misfire mode off on Server!");
        RpcDisableMisfire();
    }

    [ClientRpc]
    void RpcDisableMisfire()
    {
        // Debug.Log("[RpcDisableMisfire] Misfire mode is now deactivated on all clients.");
    }

    [Command(requiresAuthority = false)]
    public void CmdMisfireClick(NetworkIdentity clickedCard)
    {
        if (!isMisfireActive)
        {
            return;
        }
        if (clickedCard == null)
        {
            return;
        }

        // 1) เช็กว่าการ์ดใบนี้ "มีเป้าเล็ง" จริงไหม
        if (!IsCardTargeted(clickedCard))
        {
            Debug.LogWarning($"[CmdMisfireClick] {clickedCard.name} is NOT targeted => can't misfire!");
            return;
        }

        // 2) หาดูว่าข้าง ๆ (Adjacent) มีใบไหนบ้าง
        DuckCard duckComp = clickedCard.GetComponent<DuckCard>();
        if (duckComp == null)
        {
            Debug.LogWarning("[CmdMisfireClick] No DuckCard component on clicked!");
            return;
        }

        // หา "เพื่อนบ้าน" ที่ row เดียวกัน col±1
        List<NetworkIdentity> neighbors = GetAdjacentDuckCards(duckComp.Row, duckComp.Column);

        if (neighbors.Count == 0)
        {
            Debug.Log("[CmdMisfireClick] No adjacent ducks => misfire does nothing!");
            // จะยิงไม่ออกก็ได้ หรือจะยิงใบนั้นเองก็แล้วแต่
            return;
        }

        // 3) สุ่ม 1 ใบจาก neighbors
        NetworkIdentity randomNeighbor = neighbors[Random.Range(0, neighbors.Count)];

        // 4) ยิงการ์ดที่สุ่มได้
        Debug.Log($"[CmdMisfireClick] MISFIRE -> Shooting {randomNeighbor.name} instead of {clickedCard.name}!");
        ShootCardDirect(randomNeighbor);

        // 5) ทำลายเป้าเล็งบนการ์ดเดิม
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var t in allTargets)
        {
            if (t.targetNetId == clickedCard.netId)
            {
                NetworkServer.Destroy(t.gameObject);
                Debug.Log($"[CmdMisfireClick] Destroyed target {t.name} on {clickedCard.name}");
            }
        }

        // ปิดโหมด Misfire
        CmdDeactivateMisfire();

        StartCoroutine(RefillNextFrame());
    }

    private List<NetworkIdentity> GetAdjacentDuckCards(int row, int col)
    {
        List<NetworkIdentity> results = new List<NetworkIdentity>();
        // loop การ์ดทุกใบใน DuckZone
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard duck = child.GetComponent<DuckCard>();
            if (duck != null && duck.Row == row)
            {
                // สมมติ col +/-1 ถือเป็น adjacent
                if (Mathf.Abs(duck.Column - col) == 1)
                {
                    NetworkIdentity ni = duck.GetComponent<NetworkIdentity>();
                    if (ni != null) results.Add(ni);
                }
            }
        }
        return results;
    }

    private void ShootCardDirect(NetworkIdentity duckNi)
    {
        if (duckNi == null) return;

        // Destroy การ์ด
        NetworkServer.Destroy(duckNi.gameObject);
        Debug.Log($"[ShootCardDirect] Destroyed {duckNi.name}");


        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckNi.netId)
            {
                NetworkServer.Destroy(target.gameObject);
                Debug.Log($"[ShootCardDirect] Also destroyed target {target.name} pointing to {duckNi.name}");
            }
        }

        DuckCard dc = duckNi.GetComponent<DuckCard>();
        if (dc != null)
        {
            ShiftColumnsDown(dc.Row, dc.Column);
        }
    }
    // ========================
    // TwoBirds Logic
    // ========================

    // เรียกตอนวางการ์ด TwoBirds
    [Command(requiresAuthority = false)]
    public void CmdActivateTwoBirds()
    {
        if (!isTwoBirdsActive)
        {
            isTwoBirdsActive = true;
            twoBirdsClickCount = 0;
            firstTwoBirdsCard = null;

            // Debug.Log("[CmdActivateTwoBirds] TwoBirds mode active on server!");
            RpcEnableTwoBirds();
        }
    }

    [ClientRpc]
    void RpcEnableTwoBirds()
    {
        // Debug.Log("[RpcEnableTwoBirds] TwoBirds mode is now active on all clients. Click 2 targeted ducks (if adjacent) to shoot both!");
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateTwoBirds()
    {
        isTwoBirdsActive = false;
        twoBirdsClickCount = 0;
        firstTwoBirdsCard = null;

        // Debug.Log("[CmdDeactivateTwoBirds] TwoBirds mode off on server!");
        RpcDisableTwoBirds();
    }

    [ClientRpc]
    void RpcDisableTwoBirds()
    {
        // Debug.Log("[RpcDisableTwoBirds] TwoBirds mode is now deactivated on all clients.");
    }

    [Command(requiresAuthority = false)]
    public void CmdTwoBirdsClick(NetworkIdentity clickedCard)
    {
        if (!isTwoBirdsActive)
        {
            // Debug.LogWarning("[CmdTwoBirdsClick] Not in TwoBirds mode, ignoring click!");
            return;
        }
        if (clickedCard == null)
        {
            // Debug.LogWarning("[CmdTwoBirdsClick] clickedCard is null!");
            return;
        }

        // เช็กว่าใบที่คลิกมีเป้าเล็ง
        if (!IsCardTargeted(clickedCard))
        {
            Debug.LogWarning($"[CmdTwoBirdsClick] {clickedCard.name} has NO target, can't shoot!");
            return;
        }

        // --- ถ้าเป็นคลิกครั้งแรก ---
        if (twoBirdsClickCount == 0)
        {
            firstTwoBirdsCard = clickedCard;
            twoBirdsClickCount = 1;
            Debug.Log($"[CmdTwoBirdsClick] First card = {clickedCard.name}, waiting for second...");
            return;
        }
        // --- ถ้าเป็นคลิกครั้งสอง ---
        else if (twoBirdsClickCount == 1)
        {
            // เช็ก adjacency
            bool canShootBoth = false;
            if (firstTwoBirdsCard != null)
            {
                canShootBoth = CheckAdjacentTwoBirds(firstTwoBirdsCard, clickedCard);
            }

            if (canShootBoth)
            {
                // =============== ยิง 2 ใบพร้อมกัน ===============

                // 1) เก็บ row/col ของสองใบ
                DuckCard dc1 = firstTwoBirdsCard.GetComponent<DuckCard>();
                DuckCard dc2 = clickedCard.GetComponent<DuckCard>();
                if (dc1 == null || dc2 == null)
                {
                    // ถ้ามีใบไหนไม่มี DuckCard ก็ยิงไม่ได้
                    // Debug.LogWarning("[CmdTwoBirdsClick] One of the cards has no DuckCard component!");
                    CmdDeactivateTwoBirds();
                    return;
                }

                int row1 = dc1.Row;
                int col1 = dc1.Column;
                int row2 = dc2.Row;
                int col2 = dc2.Column;

                // 2) Destroy สองใบ
                NetworkServer.Destroy(firstTwoBirdsCard.gameObject);
                NetworkServer.Destroy(clickedCard.gameObject);
                // Debug.Log($"[CmdTwoBirdsClick] TwoBirds => destroyed {firstTwoBirdsCard.name} & {clickedCard.name}");

                // 3) ทำลายเป้าที่ทั้งสองใบ (ถ้ามี)
                RemoveTargetFromCard(firstTwoBirdsCard);
                RemoveTargetFromCard(clickedCard);

                // 4) เลื่อน Column (ให้เลื่อนคอลัมน์ที่มากก่อน)
                if (col1 > col2)
                {
                    ShiftColumnsDown(row1, col1);
                    ShiftColumnsDown(row2, col2);
                }
                else
                {
                    ShiftColumnsDown(row2, col2);
                    ShiftColumnsDown(row1, col1);
                }
            }
            else
            {
                // ยิงได้แค่ใบแรกใบเดียว
                // Debug.Log("[CmdTwoBirdsClick] Cards are NOT adjacent => shoot only the first one.");

                if (firstTwoBirdsCard != null)
                {
                    DuckCard dc1 = firstTwoBirdsCard.GetComponent<DuckCard>();
                    int row1 = dc1.Row;
                    int col1 = dc1.Column;

                    // ทำลายใบแรก
                    NetworkServer.Destroy(firstTwoBirdsCard.gameObject);
                    RemoveTargetFromCard(firstTwoBirdsCard);

                    // เลื่อน column
                    ShiftColumnsDown(row1, col1);
                }
            }

            // ปิดโหมด TwoBirds
            CmdDeactivateTwoBirds();

            StartCoroutine(RefillNextFrame());
        }
    }


    [Server]
    private bool CheckAdjacentTwoBirds(NetworkIdentity card1, NetworkIdentity card2)
    {
        DuckCard dc1 = card1.GetComponent<DuckCard>();
        DuckCard dc2 = card2.GetComponent<DuckCard>();
        if (dc1 == null || dc2 == null) return false;

        // ตัวอย่าง: อยู่ row เดียวกัน และ col ห่าง 1
        if (dc1.Row == dc2.Row && Mathf.Abs(dc1.Column - dc2.Column) == 1)
        {
            return true;
        }
        return false;
    }

    private void RemoveTargetFromCard(NetworkIdentity duckNi)
    {
        if (duckNi == null) return;

        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == duckNi.netId)
            {
                NetworkServer.Destroy(tf.gameObject);
                // Debug.Log($"[RemoveTargetFromCard] Also destroyed target {tf.name} pointing to {duckNi.name}");
            }
        }
    }
    // ========================
    // BumpLeft  Logic
    // ========================
    // เรียกเมื่อวางการ์ด Bump Left
    [Command(requiresAuthority = false)]
    public void CmdActivateBumpLeft()
    {
        if (!isBumpLeftActive)
        {
            isBumpLeftActive = true;
            // Debug.Log("[CmdActivateBumpLeft] BumpLeft mode active on server!");
            RpcEnableBumpLeft();
        }
    }

    [ClientRpc]
    void RpcEnableBumpLeft()
    {
        // Debug.Log("[RpcEnableBumpLeft] BumpLeft mode is now active on all clients.");
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateBumpLeft()
    {
        isBumpLeftActive = false;
        // Debug.Log("[CmdDeactivateBumpLeft] BumpLeft mode off on server!");
        RpcDisableBumpLeft();
    }

    [ClientRpc]
    void RpcDisableBumpLeft()
    {
        // Debug.Log("[RpcDisableBumpLeft] BumpLeft mode is now deactivated on all clients.");
    }

    [Command(requiresAuthority = false)]
    public void CmdBumpLeftClick(NetworkIdentity clickedCard)
    {
        if (!isBumpLeftActive)
        {
            // Debug.LogWarning("[CmdBumpLeftClick] Not in BumpLeft mode, ignoring!");
            return;
        }
        if (clickedCard == null)
        {
            // Debug.LogWarning("[CmdBumpLeftClick] clickedCard is null!");
            return;
        }

        // 1) เช็กว่าการ์ดใบนี้มีเป้าเล็ง (target) อยู่จริงไหม
        if (!IsCardTargeted(clickedCard))
        {
            // Debug.LogWarning($"[CmdBumpLeftClick] {clickedCard.name} has NO target => can't bump left!");
            return;
        }

        // 2) หา DuckCard
        DuckCard duck = clickedCard.GetComponent<DuckCard>();
        if (duck == null)
        {
            // Debug.LogWarning("[CmdBumpLeftClick] No DuckCard on clickedCard!");
            return;
        }

        int curRow = duck.Row;
        int curCol = duck.Column;
        // Debug.Log($"[CmdBumpLeftClick] Attempting to bump target from col={curCol} to col={curCol - 1} in row={curRow}");

        // 3) หาใบซ้าย (Column = curCol - 1) (ถ้ามี)
        DuckCard leftDuck = FindDuckAt(curRow, curCol - 1);
        if (leftDuck == null)
        {
            // Debug.LogWarning("[CmdBumpLeftClick] No duck on the left => can't bump!");
            return;
        }

        // 4) ย้ายเป้า = หา TargetFollow ที่เล็งการ์ดปัจจุบัน => เปลี่ยนให้ไปเล็งการ์ดใบซ้าย
        MoveTargetFromTo(clickedCard, leftDuck.GetComponent<NetworkIdentity>());

        // 5) ปิดโหมด BumpLeft (หรือจะให้ผู้เล่นทำหลายครั้งก็ได้)
        CmdDeactivateBumpLeft();
    }

    private void MoveTargetFromTo(NetworkIdentity fromCard, NetworkIdentity toCard)
    {
        if (fromCard == null || toCard == null)
            return;

        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == fromCard.netId)
            {
                // ย้ายเป้าเล็ง
                tf.targetNetId = toCard.netId;
                // Debug.Log($"[MoveTargetFromTo] Moved target from {fromCard.name} => {toCard.name}");
                tf.ResetTargetTransform();
                // อัปเดตตำแหน่ง (RPC ก็ได้ หรือ local code)
                RpcUpdateTargetPosition(tf.GetComponent<NetworkIdentity>(), toCard);

                NetworkIdentity tfNi = tf.GetComponent<NetworkIdentity>();
                RpcSetTargetNetId(tfNi, toCard);
            }
        }
    }

    // อัปเดตตำแหน่งจริงบน Client
    [ClientRpc]
    private void RpcUpdateTargetPosition(NetworkIdentity targetNi, NetworkIdentity cardNi)
    {
        if (targetNi == null || cardNi == null) return;

        RectTransform targetRect = targetNi.GetComponent<RectTransform>();
        RectTransform cardRect = cardNi.GetComponent<RectTransform>();

        if (targetRect != null && cardRect != null)
        {
            targetRect.anchoredPosition = cardRect.anchoredPosition + new Vector2(0, 150);
            // Debug.Log($"[RpcUpdateTargetPosition] BumpLeft => target goes to {targetRect.anchoredPosition} above {cardNi.name}");
        }
    }

    private DuckCard FindDuckAt(int row, int col)
    {
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null && d.Row == row && d.Column == col)
                return d;
        }
        return null;
    }

    // ========================
    // BumpRight Logic
    // ========================

    // เรียกเมื่อวางการ์ด BumpRight
    [Command(requiresAuthority = false)]
    public void CmdActivateBumpRight()
    {
        if (!isBumpRightActive)
        {
            isBumpRightActive = true;
            // Debug.Log("[CmdActivateBumpRight] BumpRight mode active on server!");
            RpcEnableBumpRight();
        }
    }

    [ClientRpc]
    void RpcEnableBumpRight()
    {
        // Debug.Log("[RpcEnableBumpRight] BumpRight mode is now active on all clients. Click a card with target to bump right!");
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateBumpRight()
    {
        isBumpRightActive = false;
        // Debug.Log("[CmdDeactivateBumpRight] BumpRight mode off on server!");
        RpcDisableBumpRight();
    }

    [ClientRpc]
    void RpcDisableBumpRight()
    {
        // Debug.Log("[RpcDisableBumpRight] BumpRight mode is now deactivated on all clients.");
    }

    [Command(requiresAuthority = false)]
    public void CmdBumpRightClick(NetworkIdentity clickedCard)
    {
        if (!isBumpRightActive)
        {
            // Debug.LogWarning("[CmdBumpRightClick] Not in BumpRight mode, ignoring!");
            return;
        }
        if (clickedCard == null)
        {
            // Debug.LogWarning("[CmdBumpRightClick] clickedCard is null!");
            return;
        }

        // 1) เช็กว่าการ์ดใบนี้มีเป้าเล็งจริงไหม
        if (!IsCardTargeted(clickedCard))
        {
            // Debug.LogWarning($"[CmdBumpRightClick] {clickedCard.name} has NO target => can't bump right!");
            return;
        }

        // 2) หา DuckCard
        DuckCard duck = clickedCard.GetComponent<DuckCard>();
        if (duck == null)
        {
            // Debug.LogWarning("[CmdBumpRightClick] No DuckCard on clickedCard!");
            return;
        }

        int curRow = duck.Row;
        int curCol = duck.Column;
        // Debug.Log($"[CmdBumpRightClick] Attempting to bump target from col={curCol} to col={curCol + 1} in row={curRow}");

        // 3) หาใบทางขวา
        DuckCard rightDuck = FindDuckAt(curRow, curCol + 1);
        if (rightDuck == null)
        {
            // Debug.LogWarning("[CmdBumpRightClick] No duck on the right => can't bump right!");
            return;
        }

        // 4) ย้ายเป้า (target) จากการ์ดปัจจุบัน => ใบขวา
        MoveTargetFromTo(clickedCard, rightDuck.GetComponent<NetworkIdentity>());

        // 5) ปิดโหมด BumpRight (หรือจะ allow หลายครั้งก็แล้วแต่)
        CmdDeactivateBumpRight();
    }

    // ========================
    // LineForward Logic
    // ========================

    public void TryLineForward()
    {
        if (!isLocalPlayer) return;
        CmdActivateLineForward();
    }


    [Command]
    public void CmdActivateLineForward()
    {
        if (isLineForwardActive) return;
        isLineForwardActive = true;

        // 1) เก็บเป้าก่อน
        var oldTargets = CollectTargetColumns();

        // 2) คืนและทำลายเฉพาะใบซ้ายสุด
        var leftmost = FindLeftmostDuck(0);
        if (leftmost != null)
        {
            NetworkServer.Destroy(leftmost.gameObject);           // remove card
            CardPoolManager.ReturnCard(leftmost.gameObject);      // +1 pool

        }

        // 3) ลบเป้าเดิม
        RemoveAllTargets();


        // 5) สร้างเป้าย้อนหลัง
        StartCoroutine(RecreateTargetsAfterShuffle(oldTargets));

        StartCoroutine(DelayedLog());

        // 6) ปิดโหมด
        CmdDeactivateLineForward();

    }

    private IEnumerator DelayedLog()
    {
        // รอจนจบ frame ให้ OnStopServer() คืน pool เสร็จ
        yield return null;
        LogTotalDuckCounts();
    }




    // ปิดโหมดหลังจากจบการทำงาน
    [Command(requiresAuthority = false)]
    public void CmdDeactivateLineForward()
    {
        isLineForwardActive = false;
        // LogTotalDuckCounts();
        // Debug.Log("[CmdDeactivateLineForward] LineForward mode off on server.");
        RpcDisableLineForward();
    }

    [ClientRpc]
    void RpcDisableLineForward()
    {
        // Debug.Log("[RpcDisableLineForward] LineForward mode deactivated on all clients.");
    }

    // ========================================================
    // ✅ 1) บันทึกตำแหน่ง Column ของเป้าเล็งทั้งหมดก่อนลบการ์ด
    // ========================================================
    private List<int> CollectTargetColumns()
    {
        List<int> targetColumns = new List<int>();
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();

        foreach (var tf in allTargets)
        {
            if (NetworkClient.spawned.TryGetValue(tf.targetNetId, out NetworkIdentity duckNi))
            {
                DuckCard duck = duckNi.GetComponent<DuckCard>();
                if (duck != null && !targetColumns.Contains(duck.Column))
                {
                    targetColumns.Add(duck.Column);
                    // Debug.Log($"[CollectTargetColumns] Target at Column {duck.Column} recorded.");
                }
            }
        }

        targetColumns.Sort();
        return targetColumns;
    }

    // ========================================================
    // ✅ 2) หาและลบการ์ดใบซ้ายสุด (Column 0) เท่านั้น
    // ========================================================
    private DuckCard FindLeftmostDuck(int row)
    {
        DuckCard result = null;
        int minCol = int.MaxValue;

        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null && d.Row == row)
            {
                if (d.Column < minCol)
                {
                    minCol = d.Column;
                    result = d;
                }
            }
        }
        return result;
    }

    // ========================================================
    // ✅ 3) ลบเป้าเล็งทั้งหมด
    // ========================================================
    private void RemoveAllTargets()
    {
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();

        foreach (var tf in allTargets)
        {
            NetworkServer.Destroy(tf.gameObject);
            // Debug.Log($"[RemoveAllTargets] Destroyed target: {tf.name}");
        }
    }

    // ========================================================
    // ✅ 4) เติมการ์ดใหม่ แล้วค่อยสร้างเป้าเล็งใหม่หลังจาก Grid Layout จัดเรียงเสร็จ
    // ========================================================
    private List<DuckCard> FindDucksInRow(int row)
    {
        List<DuckCard> list = new List<DuckCard>();

        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null && d.Row == row) // เช็คว่าเป็ดนี้อยู่ใน row ที่ต้องการหรือไม่
            {
                list.Add(d);
            }
        }
        return list;
    }

    [Server]
    IEnumerator RefillAndRecreateTargets(List<int> oldTargetColumns)
    {
        // 4.1) เรียก `RefillNextFrame()` เพื่อเติมการ์ดก่อน
        yield return StartCoroutine(RefillNextFrameLineForward());

        // 4.2) หลังจากเติมการ์ดเสร็จ -> รออีก 1 เฟรมให้ Grid Layout อัปเดตตำแหน่ง
        yield return null;

        // 4.3) สร้างเป้าเล็งใหม่ตามตำแหน่งที่บันทึกไว้
        List<DuckCard> ducks = FindDucksInRow(0); // <<<< ตรงนี้จะไม่มี error แล้ว

        foreach (int col in oldTargetColumns)
        {
            DuckCard duckAtCol = ducks.Find(d => d.Column == col);
            if (duckAtCol != null)
            {
                CmdSpawnTargetForDuck(duckAtCol.netId);
                // Debug.Log($"[RecreateTargetsNextFrame] Spawn target at col={col} for {duckAtCol.name}");
            }
            else
            {
                // Debug.Log($"[RecreateTargetsNextFrame] No duck found at col={col}, skipping target.");
            }
        }
    }

    // ========================================================
    // ✅ 5) เติมการ์ดใหม่ (`RefillNextFrame()` ถูกใช้ในขั้นตอนที่ 4)
    // ========================================================
    [Server]
    private IEnumerator RefillNextFrameLineForward()
    {
        yield return null;
        RefillDuckZoneIfNeededLineForward();
    }

    [Server]
    private void RefillDuckZoneIfNeededLineForward()
    {
        if (DuckZone == null)
        {
            // Debug.LogError("RefillDuckZoneIfNeeded: DuckZone is NULL!");
            return;
        }

        int currentCount = GetDuckCardCountInDuckZone();
        if (currentCount >= 6)
        {
            // Debug.Log($"[RefillDuckZoneIfNeeded] Already {currentCount} cards in DuckZone, no need to refill.");
            return;
        }

        if (!CardPoolManager.HasCards())
        {
            // Debug.LogWarning("[RefillDuckZone] No cards left in pool!");
            return;
        }

        int needed = 6 - currentCount;
        for (int i = 0; i < needed; i++)
        {
            // สุ่มและสปอนการ์ดใหม่ผ่าน abstraction
            GameObject newCard = CardPoolManager.DrawRandomCard(DuckZone.transform);
            if (newCard == null) break;

            NetworkServer.Spawn(newCard);
            RpcAddCardToDuckZone(newCard);
        }
    }

    // ========================================================
    // ✅ 6) สร้างเป้าเล็งใหม่ให้กับการ์ดที่อยู่ในตำแหน่งที่บันทึกไว้
    // ========================================================
    [Command(requiresAuthority = false)]
    private void CmdSpawnTargetForDuck(uint duckNetId)
    {
        if (!NetworkClient.spawned.TryGetValue(duckNetId, out NetworkIdentity duckNi))
        {
            // Debug.LogWarning($"[CmdSpawnTargetForDuck] Duck netId={duckNetId} not found!");
            return;
        }

        if (targetPrefab == null)
        {
            // Debug.LogError("[CmdSpawnTargetForDuck] targetPrefab is null!");
            return;
        }

        GameObject newTarget = Instantiate(targetPrefab);
        NetworkServer.Spawn(newTarget);

        NetworkIdentity targetNi = newTarget.GetComponent<NetworkIdentity>();
        RpcSetTargetNetId(targetNi, duckNi);
    }

    // ========================
    // Move Ahead Logic 
    // ========================

    [Command(requiresAuthority = false)]
    public void CmdActivateMoveAhead()
    {
        if (!isMoveAheadActive)
        {
            isMoveAheadActive = true;
            // Debug.Log("[CmdActivateMoveAhead] MoveAhead mode active on server!");
            RpcEnableMoveAhead();
        }
    }

    [ClientRpc]
    void RpcEnableMoveAhead()
    {
        // Debug.Log("[RpcEnableMoveAhead] MoveAhead mode is now active on all clients. Click a duck to swap with the one ahead!");
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateMoveAhead()
    {
        isMoveAheadActive = false;
        // Debug.Log("[CmdDeactivateMoveAhead] MoveAhead mode off on server!");
        RpcDisableMoveAhead();
    }

    [ClientRpc]
    void RpcDisableMoveAhead()
    {
        // Debug.Log("[RpcDisableMoveAhead] MoveAhead mode is now deactivated on all clients.");
    }

    [Command(requiresAuthority = false)]
    public void CmdMoveAheadClick(NetworkIdentity clickedCard)
    {
        if (!isMoveAheadActive) return;
        if (clickedCard == null) return;

        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        int currentCol = selectedDuck.Column;
        int targetCol = currentCol - 1; // เดินหน้าไปทางซ้าย

        DuckCard targetDuck = FindDuckAtMoveAhead(0, targetCol);
        if (targetDuck == null)
        {
            // Debug.LogWarning($"[CmdMoveAheadClick] No duck at column {targetCol}, can't swap!");
            return;
        }

        // 🔹 **1) ตรวจสอบว่ามีเป้าเล็งที่การ์ดทั้งสองใบหรือไม่ และเก็บ Column ของเป้านั้น**
        Dictionary<int, TargetFollow> targetMap = new Dictionary<int, TargetFollow>();

        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == selectedDuck.netId)
            {
                targetMap[selectedDuck.Column] = tf;
            }
            else if (tf.targetNetId == targetDuck.netId)
            {
                targetMap[targetDuck.Column] = tf;
            }
        }

        // 🔹 **2) ทำลายเป้าเล็งทั้งหมดที่เกี่ยวข้อง**
        foreach (var tf in targetMap.Values)
        {
            NetworkServer.Destroy(tf.gameObject);
        }

        // 🔹 **3) สลับตำแหน่งการ์ด**
        SwapDuckColumns(selectedDuck, targetDuck);

        // Debug.Log($"[CmdMoveAheadClick] Swapped {selectedDuck.name} (col={selectedDuck.Column}) <-> {targetDuck.name} (col={targetDuck.Column})");

        // 🔹 **4) Sync ตำแหน่งการ์ดไป Client**
        RpcUpdateDuckPositions(selectedDuck.netId, targetDuck.netId);

        // 🔹 **5) สร้างเป้าเล็งใหม่ที่ตำแหน่งเดิม**
        foreach (var entry in targetMap)
        {
            int originalCol = entry.Key;
            TargetFollow oldTarget = entry.Value;

            DuckCard newDuckAtCol = FindDuckAtMoveAhead(0, originalCol);
            if (newDuckAtCol != null)
            {
                CmdSpawnTargetForDuck(newDuckAtCol.netId);
                // Debug.Log($"[CmdMoveAheadClick] Recreated target at column {originalCol} for {newDuckAtCol.name}");
            }
        }

        // ปิดโหมด
        CmdDeactivateMoveAhead();
    }

    // 🔹 ฟังก์ชันสลับ Column ของการ์ดเป็ดสองใบ
    private void SwapDuckColumns(DuckCard duck1, DuckCard duck2)
    {
        int tempCol = duck1.Column;
        duck1.Column = duck2.Column;
        duck2.Column = tempCol;
    }

    // 🔹 หาเป็ดที่อยู่ใน Column ที่กำหนด
    private DuckCard FindDuckAtMoveAhead(int row, int col)
    {
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null && d.Row == row && d.Column == col)
                return d;
        }
        return null;
    }

    // 🔹 ซิงก์ตำแหน่งการ์ดไปยังทุก Client
    [ClientRpc]
    void RpcUpdateDuckPositions(uint netIdA, uint netIdB)
    {
        if (!NetworkClient.spawned.TryGetValue(netIdA, out NetworkIdentity niA) ||
            !NetworkClient.spawned.TryGetValue(netIdB, out NetworkIdentity niB))
        {
            // Debug.LogWarning($"[RpcUpdateDuckPositions] Could not find ducks!");
            return;
        }

        DuckCard duckA = niA.GetComponent<DuckCard>();
        DuckCard duckB = niB.GetComponent<DuckCard>();

        if (duckA != null && duckB != null)
        {
            int tempIdx = duckA.transform.GetSiblingIndex();
            duckA.transform.SetSiblingIndex(duckB.transform.GetSiblingIndex());
            duckB.transform.SetSiblingIndex(tempIdx);

            // Debug.Log($"[RpcUpdateDuckPositions] Updated positions: {duckA.name} <-> {duckB.name}");
        }
    }

    // ========================
    // HangBack Logic
    // ========================

    [Command(requiresAuthority = false)]
    public void CmdHangBackClick(NetworkIdentity clickedCard)
    {
        if (!isHangBackActive) return;
        if (clickedCard == null) return;

        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        int currentCol = selectedDuck.Column;
        int targetCol = currentCol + 1; // ถอยหลังไปทางขวา

        DuckCard targetDuck = FindDuckAtHangBack(0, targetCol);
        if (targetDuck == null)
        {
            // Debug.LogWarning($"[CmdHangBackClick] No duck at column {targetCol}, can't swap!");
            return;
        }

        // 🔹 **1) ตรวจสอบว่ามีเป้าเล็งที่การ์ดทั้งสองใบหรือไม่ และเก็บ Column ของเป้านั้น**
        Dictionary<int, TargetFollow> targetMap = new Dictionary<int, TargetFollow>();

        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == selectedDuck.netId)
            {
                targetMap[selectedDuck.Column] = tf;
            }
            else if (tf.targetNetId == targetDuck.netId)
            {
                targetMap[targetDuck.Column] = tf;
            }
        }

        // 🔹 **2) ทำลายเป้าเล็งทั้งหมดที่เกี่ยวข้อง**
        foreach (var tf in targetMap.Values)
        {
            NetworkServer.Destroy(tf.gameObject);
        }

        // 🔹 **3) สลับตำแหน่งการ์ด**
        SwapDuckColumns(selectedDuck, targetDuck);

        // Debug.Log($"[CmdHangBackClick] Swapped {selectedDuck.name} (col={selectedDuck.Column}) <-> {targetDuck.name} (col={targetDuck.Column})");

        // 🔹 **4) Sync ตำแหน่งการ์ดไป Client**
        RpcUpdateDuckPositions(selectedDuck.netId, targetDuck.netId);

        // 🔹 **5) สร้างเป้าเล็งใหม่ที่ตำแหน่งเดิม**
        foreach (var entry in targetMap)
        {
            int originalCol = entry.Key;
            TargetFollow oldTarget = entry.Value;

            DuckCard newDuckAtCol = FindDuckAtHangBack(0, originalCol);
            if (newDuckAtCol != null)
            {
                CmdSpawnTargetForDuck(newDuckAtCol.netId);
                // Debug.Log($"[CmdHangBackClick] Recreated target at column {originalCol} for {newDuckAtCol.name}");
            }
        }

        // ปิดโหมด
        CmdDeactivateHangBack();
    }

    // 🔹 หา DuckCard ที่อยู่ใน Column ที่กำหนด
    private DuckCard FindDuckAtHangBack(int row, int col)
    {
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null && d.Row == row && d.Column == col)
                return d;
        }
        return null;
    }






    // 🔹 ฟังก์ชันปิดโหมด Hang Back
    [Command(requiresAuthority = false)]
    public void CmdDeactivateHangBack()
    {
        isHangBackActive = false;
        // Debug.Log("[CmdDeactivateHangBack] HangBack mode off on server!");
        RpcDisableHangBack();
    }

    [ClientRpc]
    void RpcDisableHangBack()
    {
        // Debug.Log("[RpcDisableHangBack] HangBack mode is now deactivated on all clients.");
    }

    // 🔹 ฟังก์ชันเปิดโหมด Hang Back
    [Command(requiresAuthority = false)]
    public void CmdActivateHangBack()
    {
        if (!isHangBackActive)
        {
            isHangBackActive = true;
            // Debug.Log("[CmdActivateHangBack] HangBack mode active on server!");
            RpcEnableHangBack();
        }
    }

    [ClientRpc]
    void RpcEnableHangBack()
    {
        // Debug.Log("[RpcEnableHangBack] HangBack mode is now active on all clients. Click a duck to swap with the one behind!");
    }


    // ========================
    // FastForward Logic
    // ========================
    [Command(requiresAuthority = false)]
    public void CmdFastForwardClick(NetworkIdentity clickedCard)
    {
        if (!isFastForwardActive) return;
        if (clickedCard == null) return;

        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        StartCoroutine(FastForwardCoroutine(selectedDuck));
    }

    [Server]
    private IEnumerator FastForwardCoroutine(DuckCard selectedDuck)
    {
        float delay = 0.3f; // หน่วงเวลาแต่ละรอบ

        Dictionary<int, TargetFollow> targetMap = new Dictionary<int, TargetFollow>();

        // 🔹 เก็บตำแหน่งเป้าก่อนลบ
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == selectedDuck.netId)
            {
                targetMap[selectedDuck.Column] = tf;
            }
            else
            {
                DuckCard duck = FindDuckByNetId(tf.targetNetId);
                if (duck != null)
                {
                    targetMap[duck.Column] = tf;
                }
            }
        }

        // 🔹 ลบเป้าทั้งหมดก่อนเริ่มย้ายการ์ด
        foreach (var tf in targetMap.Values)
        {
            NetworkServer.Destroy(tf.gameObject);
        }

        // 🔹 ค่อยๆ สลับไปด้านหน้าเรื่อยๆ
        while (selectedDuck.Column > 0)
        {
            int targetCol = selectedDuck.Column - 1;
            DuckCard targetDuck = FindDuckAtColumn(0, targetCol);

            if (targetDuck == null)
            {
                // Debug.LogWarning($"[FastForwardCoroutine] No duck at column {targetCol}, stopping swap.");
                break;
            }

            // 🔹 สลับตำแหน่ง Column
            int tempCol = selectedDuck.Column;
            selectedDuck.Column = targetDuck.Column;
            targetDuck.Column = tempCol;

            // Debug.Log($"[FastForwardCoroutine] Swapped {selectedDuck.name} (col={selectedDuck.Column}) <-> {targetDuck.name} (col={targetDuck.Column})");

            // 🔹 ซิงค์ตำแหน่งไปทุก Client
            RpcUpdateDuckPositions(selectedDuck.netId, targetDuck.netId);

            yield return new WaitForSeconds(delay); // รอให้เห็นการสลับ
        }

        // 🔹 คืนเป้าเล็งกลับไปที่ตำแหน่งเดิม
        foreach (var entry in targetMap)
        {
            int originalCol = entry.Key;
            DuckCard newDuckAtCol = FindDuckAtColumn(0, originalCol);
            if (newDuckAtCol != null)
            {
                CmdSpawnTargetForDuck(newDuckAtCol.netId);
                // Debug.Log($"[FastForwardCoroutine] Recreated target at column {originalCol} for {newDuckAtCol.name}");
            }
        }

        // 🔹 ปิดโหมด
        CmdDeactivateFastForward();
    }


    // 🔹 สลับตำแหน่งของการ์ดผ่าน Server เพื่อ Sync กับ Client
    [Command(requiresAuthority = false)]
    private void CmdSwapDuckColumns(uint netIdA, uint netIdB)
    {
        if (!NetworkClient.spawned.TryGetValue(netIdA, out NetworkIdentity niA) ||
            !NetworkClient.spawned.TryGetValue(netIdB, out NetworkIdentity niB))
        {
            // Debug.LogWarning($"[CmdSwapDuckColumns] Could not find ducks!");
            return;
        }

        DuckCard duckA = niA.GetComponent<DuckCard>();
        DuckCard duckB = niB.GetComponent<DuckCard>();

        if (duckA != null && duckB != null)
        {
            // 🔹 สลับค่าของ Column
            int tempCol = duckA.Column;
            duckA.Column = duckB.Column;
            duckB.Column = tempCol;

            // 🔹 ส่งไปให้ Client Sync
            RpcUpdateDuckPositions();
        }
    }



    // 🔹 หา DuckCard โดยใช้ NetId
    private DuckCard FindDuckByNetId(uint netId)
    {
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null && d.netId == netId)
                return d;
        }
        return null;
    }

    // 🔹 หา DuckCard ที่อยู่ใน Column ที่กำหนด
    private DuckCard FindDuckAtColumn(int row, int col)
    {
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null && d.Row == row && d.Column == col)
                return d;
        }
        return null;
    }

    // 🔹 Sync ตำแหน่งไปยังทุก Client
    [ClientRpc]
    void RpcUpdateDuckPositions()
    {
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null)
            {
                d.transform.SetSiblingIndex(d.Column);
            }
        }
    }




    // 🔹 ฟังก์ชันปิดโหมด Fast Forward
    [Command(requiresAuthority = false)]
    public void CmdDeactivateFastForward()
    {
        isFastForwardActive = false;
        // Debug.Log("[CmdDeactivateFastForward] FastForward mode off on server!");
        RpcDisableFastForward();
    }

    [ClientRpc]
    void RpcDisableFastForward()
    {
        // Debug.Log("[RpcDisableFastForward] FastForward mode is now deactivated on all clients.");
    }

    // 🔹 ฟังก์ชันเปิดโหมด Fast Forward
    [Command(requiresAuthority = false)]
    public void CmdActivateFastForward()
    {
        if (!isFastForwardActive)
        {
            isFastForwardActive = true;
            // Debug.Log("[CmdActivateFastForward] FastForward mode active on server!");
            RpcEnableFastForward();
        }
    }

    [ClientRpc]
    void RpcEnableFastForward()
    {
        // Debug.Log("[RpcEnableFastForward] FastForward mode is now active on all clients. Click a duck to move to the front!");
    }







    // ========================
    // Disorderly Conduckt Logic
    // ========================
    [Command(requiresAuthority = false)]
    public void CmdDisorderlyClick(NetworkIdentity clickedCard)
    {
        if (!isDisorderlyConducktActive) return;
        if (clickedCard == null) return;

        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        // ถ้าไม่มีการ์ดที่เลือกก่อนหน้า => เก็บเป็นใบแรก
        if (firstSelectedDuck == null)
        {
            firstSelectedDuck = selectedDuck;
            // Debug.Log($"[CmdDisorderlyClick] First selected: {selectedDuck.name} (Col: {selectedDuck.Column})");
            return;
        }

        // ถ้าเลือกการ์ดที่สอง => เช็คว่าอยู่ติดกันหรือเปล่า
        DuckCard secondDuck = selectedDuck;
        if (Mathf.Abs(firstSelectedDuck.Column - secondDuck.Column) != 1)
        {
            // Debug.LogWarning("[CmdDisorderlyClick] Ducks are not adjacent, ignoring!");
            firstSelectedDuck = null; // รีเซ็ตการเลือก
            return;
        }

        // 🔹 บันทึกตำแหน่งเป้าหมายทั้งหมดก่อนทำการสลับ
        List<int> targetColumns = new List<int>();
        List<uint> targetNetIds = new List<uint>();

        Dictionary<int, TargetFollow> targetMap = new Dictionary<int, TargetFollow>();
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();

        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == selectedDuck.netId)
            {
                targetMap[selectedDuck.Column] = tf;
            }
            else
            {
                DuckCard duck = FindDuckByNetId(tf.targetNetId);
                if (duck != null)
                {
                    targetMap[duck.Column] = tf;
                }
            }
        }

        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == firstSelectedDuck.netId)
            {
                targetColumns.Add(firstSelectedDuck.Column);
                targetNetIds.Add(tf.targetNetId);
            }
            else if (tf.targetNetId == secondDuck.netId)
            {
                targetColumns.Add(secondDuck.Column);
                targetNetIds.Add(tf.targetNetId);
            }
        }

        // 🔹 ลบเป้าทั้งหมดที่เกี่ยวข้องทั้งใน server และ client
        // foreach (var tf in allTargets)
        // {
        //     NetworkServer.Destroy(tf.gameObject);
        //     // if (targetNetIds.Contains(tf.targetNetId))
        //     // {
        //     //     NetworkServer.Destroy(tf.gameObject); // ลบจาก server
        //     // }
        // }

        foreach (var tf in targetMap.Values)
        {
            NetworkServer.Destroy(tf.gameObject);
        }

        // 🔹 สลับตำแหน่งการ์ด
        SwapDuckColumns(firstSelectedDuck, secondDuck);

        // Debug.Log($"[CmdDisorderlyClick] Swapped {firstSelectedDuck.name} (Col: {firstSelectedDuck.Column}) <-> {secondDuck.name} (Col: {secondDuck.Column})");

        // 🔹 ซิงค์ตำแหน่งการ์ดไป Client
        RpcUpdateDuckPositionsForDuckforDisorderlyConduckt(firstSelectedDuck.netId, secondDuck.netId);

        // // 🔹 คืนเป้ากลับไปที่ตำแหน่งเดิมทั้งใน server และ client
        // RpcRecreateTargets(targetColumns, targetNetIds);

        // 🔹 คืนเป้าเล็งกลับไปที่ตำแหน่งเดิม
        foreach (var entry in targetMap)
        {
            int originalCol = entry.Key;
            DuckCard newDuckAtCol = FindDuckAtColumn(0, originalCol);
            if (newDuckAtCol != null)
            {
                CmdSpawnTargetForDuck(newDuckAtCol.netId);
                // Debug.Log($"[FastForwardCoroutine] Recreated target at column {originalCol} for {newDuckAtCol.name}");
            }
        }

        // รีเซ็ตการเลือก
        firstSelectedDuck = null;

        // 🔹 ปิดโหมด Disorderly Conduckt
        CmdDeactivateDisorderlyConduckt();
    }

    // เพิ่มการลบเป้าใน Client
    [ClientRpc]
    void RpcDestroyTargetsOnClient(uint targetNetId)
    {
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == targetNetId)
            {
                Destroy(tf.gameObject);
            }
        }
    }

    // ซิงค์ตำแหน่งการ์ดไปยัง Client
    [ClientRpc]
    void RpcUpdateDuckPositionsForDuckforDisorderlyConduckt(uint netIdA, uint netIdB)
    {
        if (!NetworkClient.spawned.TryGetValue(netIdA, out var niA) ||
            !NetworkClient.spawned.TryGetValue(netIdB, out var niB))
        {
            // Debug.LogWarning($"[RpcUpdateDuckPositions] Could not find ducks!");
            return;
        }

        DuckCard duckA = niA.GetComponent<DuckCard>();
        DuckCard duckB = niB.GetComponent<DuckCard>();

        if (duckA != null && duckB != null)
        {
            int tempIdx = duckA.transform.GetSiblingIndex();
            duckA.transform.SetSiblingIndex(duckB.transform.GetSiblingIndex());
            duckB.transform.SetSiblingIndex(tempIdx);

            // Debug.Log($"[RpcUpdateDuckPositions] Updated positions: {duckA.name} <-> {duckB.name}");
        }
    }

    [Server]
    private IEnumerator RecreateTargetsAfterSwap(Dictionary<int, uint> targetMap)
    {
        yield return null; // รอให้ Grid Layout Group ปรับการ์ดก่อน

        foreach (var entry in targetMap)
        {
            int originalCol = entry.Key;
            uint oldTargetNetId = entry.Value;

            DuckCard newDuckAtCol = FindDuckAtColumnforDisorderlyConduckt(0, originalCol);
            if (newDuckAtCol != null)
            {
                CmdSpawnTargetForDuckforDisorderlyConduckt(newDuckAtCol.netId);
                // Debug.Log($"[RecreateTargetsAfterSwap] Recreated target at column {originalCol} for {newDuckAtCol.name}");
            }
        }
    }



    // ฟังก์ชันคืนเป้าเล็งกลับไปที่ตำแหน่งเดิมทั้งใน server และ client
    [ClientRpc]
    void RpcRecreateTargets(List<int> targetColumns, List<uint> targetNetIds)
    {
        if (targetColumns.Count != targetNetIds.Count)
        {
            // Debug.LogError("[RpcRecreateTargets] Mismatch between columns and targetNetIds!");
            return;
        }

        // สร้างเป้าหมายใหม่ในตำแหน่งเดิมหลังการสลับ
        for (int i = 0; i < targetColumns.Count; i++)
        {
            int originalCol = targetColumns[i];
            uint oldTargetNetId = targetNetIds[i];

            // ค้นหาการ์ดใหม่ที่อยู่ในคอลัมน์เดิม
            DuckCard newDuckAtCol = FindDuckAtColumnforDisorderlyConduckt(0, originalCol);
            if (newDuckAtCol != null)
            {
                CmdSpawnTargetForDuckforDisorderlyConduckt(newDuckAtCol.netId);
                // Debug.Log($"[RpcRecreateTargets] Recreated target at column {originalCol} for {newDuckAtCol.name}");
            }
        }
    }

    // ฟังก์ชันสร้างเป้าหมายใหม่
    [Server]
    private void CmdSpawnTargetForDuckforDisorderlyConduckt(uint duckNetId)
    {
        if (!NetworkClient.spawned.TryGetValue(duckNetId, out NetworkIdentity duckNi))
        {
            // Debug.LogWarning($"[CmdSpawnTargetForDuck] Duck netId={duckNetId} not found!");
            return;
        }

        if (targetPrefab == null)
        {
            // Debug.LogError("[CmdSpawnTargetForDuck] targetPrefab is null!");
            return;
        }

        GameObject newTarget = Instantiate(targetPrefab);
        NetworkServer.Spawn(newTarget);

        NetworkIdentity targetNi = newTarget.GetComponent<NetworkIdentity>();
        RpcSetTargetNetId(targetNi, duckNi);
    }

    // ฟังก์ชันค้นหาการ์ดเป็ดในคอลัมน์
    private DuckCard FindDuckAtColumnforDisorderlyConduckt(int row, int col)
    {
        foreach (Transform child in DuckZone.transform)
        {
            DuckCard d = child.GetComponent<DuckCard>();
            if (d != null && d.Row == row && d.Column == col)
                return d;
        }
        return null;
    }




    [Command(requiresAuthority = false)]
    public void CmdActivateDisorderlyConduckt()
    {
        if (!isDisorderlyConducktActive)
        {
            isDisorderlyConducktActive = true;
            // Debug.Log("[CmdActivateDisorderlyConduckt] Disorderly Conduckt mode active!");

            RpcEnableDisorderlyConduckt();
        }
    }

    [ClientRpc]
    void RpcEnableDisorderlyConduckt()
    {
        isDisorderlyConducktActive = true;
        // Debug.Log("[RpcEnableDisorderlyConduckt] Disorderly Conduckt mode is active! Click two adjacent ducks to swap.");
    }


    // 🔹 ปิดโหมด Disorderly Conduckt
    [Command(requiresAuthority = false)]
    public void CmdDeactivateDisorderlyConduckt()
    {
        isDisorderlyConducktActive = false;
        firstSelectedDuck = null;
        // Debug.Log("[CmdDeactivateDisorderlyConduckt] DisorderlyConduckt mode off on server!");
        RpcDisableDisorderlyConduckt();
    }

    [ClientRpc]
    void RpcDisableDisorderlyConduckt()
    {
        isDisorderlyConducktActive = false;
        // Debug.Log("[RpcDisableDisorderlyConduckt] DisorderlyConduckt mode is now deactivated on all clients.");
    }

    // ========================
    // Duck Shuffle  Logic
    // ========================
    public void TryDuckShuffle()
    {
        CmdActivateDuckShuffle();
    }

    [Command(requiresAuthority = false)]
    public void CmdActivateDuckShuffle()
    {
        if (isDuckShuffleActive) return;
        isDuckShuffleActive = true;

        // 1) เก็บเป้าก่อน
        var oldTargets = CollectTargetColumns();

        // 2) คืนทุกใบใน zone → pool แล้วทำลาย
        foreach (Transform child in DuckZone.transform)
        {
            if (child.TryGetComponent<DuckCard>(out var duck))
            {
                CardPoolManager.ReturnCard(duck.gameObject);    // +1 pool
                NetworkServer.Destroy(duck.gameObject);
            }
        }

        // 3) ลบเป้าเดิม
        RemoveAllTargets();
        // UpdateDuckColumns();
        // 4) รีฟิลใหม่ถึง 6 ใบ
        int toDeal = 6 - GetDuckCardCountInDuckZone();
        for (int i = 0; i < toDeal; i++)
        {
            if (!CardPoolManager.HasCards()) break;

            // 1) DrawRandomCard จะ Instantiate ไว้บน DuckZone.transform
            GameObject cardGO = CardPoolManager.DrawRandomCard(DuckZone.transform);
            if (cardGO == null) break;

            // 2) ตั้งค่า Row/Column ให้ถูกต้อง (Column = index หลัง parent)
            var duck = cardGO.GetComponent<DuckCard>();
            if (duck != null)
            {
                duck.Row = 0;
                // childCount ยังไม่เพิ่มใบนี้หรือเพิ่มแล้ว? 
                // DrawRandomCard ใส่ไว้บน transform แล้ว childCount เพิ่มแล้ว
                duck.Column = DuckZone.transform.childCount - 1;
            }

            // 3) Spawn & RPC add
            NetworkServer.Spawn(cardGO);
            RpcAddCardToDuckZone(cardGO);
        }
        // UpdateDuckColumns();
        // 5) สร้างเป้าย้อน
        StartCoroutine(RecreateTargetsAfterShuffle(oldTargets));

        StartCoroutine(DelayedLog());


        // 6) ปิดโหมด
        CmdDeactivateDuckShuffle();
    }


    [Server]
    private IEnumerator RecreateTargetsAfterShuffle(List<int> oldCols)
    {
        yield return StartCoroutine(RefillNextFrameDuckShuffle());
        // รอ 1 เฟรมให้ layout ปรับตำแหน่งเสร็จ
        yield return null;
        // UpdateDuckColumns();

        // ค้น DuckCard แต่ละใบใน row 0
        List<DuckCard> ducks = new List<DuckCard>();
        foreach (Transform child in DuckZone.transform)
            if (child.TryGetComponent(out DuckCard dc) && dc.Row == 0)
                ducks.Add(dc);

        // สร้างเป้าย้อนกลับ
        foreach (int col in oldCols)
        {
            var duckAtCol = ducks.Find(d => d.Column == col);
            if (duckAtCol != null)
            {
                CmdSpawnTargetForDuck(duckAtCol.netId);
                // Debug.Log($"[DuckShuffle] Recreated target at col {col} for {duckAtCol.name}");
            }
        }
    }

    // ปิดโหมดหลังจากจบการทำงาน
    [Command(requiresAuthority = false)]
    public void CmdDeactivateDuckShuffle()
    {
        isDuckShuffleActive = false;
        RpcDisableDuckShuffle();
    }

    [ClientRpc]
    void RpcDisableDuckShuffle()
    {
        // Debug.Log("[RpcDisableDuckShuffle] DuckShuffle mode is now deactivated on all clients.");
    }

    // ========================
    // ลบการ์ดทั้งหมดจาก DuckZone
    // ========================
    private void RemoveAllDucks()
    {
        DuckCard[] allDucks = FindObjectsOfType<DuckCard>();

        foreach (var duck in allDucks)
        {
            NetworkServer.Destroy(duck.gameObject);
            // Debug.Log($"[RemoveAllDucks] Destroyed duck: {duck.name}");
        }
    }



    // ========================
    // เติมการ์ดใหม่ (`RefillNextFrame()` ถูกใช้ในขั้นตอนที่ 4)
    // ========================
    [Server]
    private IEnumerator RefillNextFrameDuckShuffle()
    {
        yield return null;
        RefillDuckZoneIfNeededDuckShuffle();
    }

    [Server]
    private void RefillDuckZoneIfNeededDuckShuffle()
    {
        if (DuckZone == null) return;
        int currentCount = GetDuckCardCountInDuckZone();
        if (currentCount >= 6) return;
        if (!CardPoolManager.HasCards())
        {
            // Debug.LogWarning("[RefillDuckZone] No cards left in pool!");
            return;
        }

        int needed = 6 - currentCount;
        for (int i = 0; i < needed; i++)
        {
            GameObject newCard = CardPoolManager.DrawRandomCard(DuckZone.transform);
            if (newCard == null) break;
            NetworkServer.Spawn(newCard);
            RpcAddCardToDuckZone(newCard);
        }
    }

    // ========================
    // GivePeaceAChance Logic
    // ========================

    public void TryGivePeaceAChance()
    {
        if (!isLocalPlayer) return;
        CmdActivateGivePeaceAChance();
    }

    [Command(requiresAuthority = false)]
    private void CmdActivateGivePeaceAChance()
    {
        if (isGivePeaceActive) return;
        isGivePeaceActive = true;
        // Debug.Log("[CmdActivateGivePeaceAChance] Removing all targets...");

        // ลบเป้าเล็งทั้งหมด
        RemoveAllTargets();



        // ปิดโหมด
        CmdDeactivateGivePeaceAChance();
    }

    [Command(requiresAuthority = false)]
    private void CmdDeactivateGivePeaceAChance()
    {
        isGivePeaceActive = false;
        RpcDisableGivePeaceAChance();
    }

    [ClientRpc]
    private void RpcDisableGivePeaceAChance()
    {
        // Debug.Log("[RpcDisableGivePeaceAChance] GivePeaceAChance deactivated on clients.");
    }

    // ========================
    // Resurrection  Logic
    // ========================
    // เปลี่ยนชื่อเมธอดให้ไม่ชนกับชื่อคลาสหรือฟิลด์เดิม
    public void TryUseResurrection()
    {
        if (!isLocalPlayer) return;
        CmdActivateResurrectionMode();
    }

    [Command]
    private void CmdActivateResurrectionMode()
    {
        if (isResurrectionModeActive) return;
        isResurrectionModeActive = true;

        const int maxPerColor = 5;

        // 1) ดึงจำนวนรวม (pool + zone)
        var totalCounts = GetTotalDuckCounts();

        // 2) หาเฉพาะสีที่มีน้อยกว่า maxPerColor
        var lowColors = new List<string>();
        foreach (var kv in totalCounts)
            if (kv.Value < maxPerColor)
                lowColors.Add(kv.Key);

        if (lowColors.Count > 0)
        {
            // 3) สุ่มเลือกสี แล้วบวกใน pool
            int idx = Random.Range(0, lowColors.Count);
            string color = lowColors[idx];

            CardPoolManager.AddToPool(color);
            // Debug.Log($"[Resurrection] Added one {color} back to pool (now total {totalCounts[color] + 1})");
        }
        else
        {
            // Debug.LogWarning("[Resurrection] No color below max count—nothing added");
        }

        StartCoroutine(DelayedLog());

        CmdDeactivateResurrectionMode();
    }

    [Command(requiresAuthority = false)]
    private void CmdDeactivateResurrectionMode()
    {
        isResurrectionModeActive = false;
        RpcDisableResurrectionMode();
    }

    [ClientRpc]
    private void RpcDisableResurrectionMode()
    {

    }












    // ========================
    // ShowCard Logic
    // ========================
    [ClientRpc]
    void RpcShowCard(GameObject card, string type)
    {
        if (card == null)
        {
            Debug.LogError("[RpcShowCard] Card is null!");
            return;
        }

        Debug.Log($"RpcShowCard called with type: {type} and card name: {card.name}");

        var networkIdentity = card.GetComponent<NetworkIdentity>();
        if (networkIdentity == null)
        {
            Debug.LogError("[RpcShowCard] NetworkIdentity is null!");
            return;
        }

        if (type == "Dealt")
        {
            // แสดงใน PlayerArea ถ้าเป็นการ์ดของเรา
            if (networkIdentity.isOwned && PlayerArea != null)
            {
                card.transform.SetParent(PlayerArea.transform, false);
            }
            else if (EnemyArea != null)
            {
                card.transform.SetParent(EnemyArea.transform, false);
                card.GetComponent<CardFlipper>()?.Flip();
            }
        }
        else if (type == "Played")
        {
            Debug.Log($"Card before setting parent: {card.name}");
            // วางการ์ดลง DropZone
            if (DropZone != null)
            {
                card.transform.SetParent(DropZone.transform, false);
            }
            var dropZone = FindObjectOfType<DropZone>();
            if (dropZone != null)
            {
                dropZone.PlaceCard(card);
            }

            // Debug log for checking after setting parent
            Debug.Log($"Card after setting parent: {card.name}");

            // ถ้าไม่ใช่เจ้าของการ์ด (คือเป็นของฝ่ายตรงข้าม) ก็หงาย/คว่ำหน้า
            if (!networkIdentity.isOwned)
            {
                card.GetComponent<CardFlipper>()?.Flip();
            }

            // ปิดการใช้งานของการ์ดอื่น ๆ ก่อน
            DeactivateAllOtherCards();

            // ใช้งานการ์ดที่เลือก
            HandleCardActivation(card, networkIdentity);

        }


    }

    private void HandleCardActivation(GameObject card, NetworkIdentity networkIdentity)
    {
        if (card.name.Contains("Shoot"))
        {
            // Debug.Log("Shoot card played → Activate Shoot Mode!");
            CmdActivateShoot();
        }
        else if (card.name.Contains("TekeAim"))
        {
            Debug.Log("TekeAim ทำงาน");
            CmdActivateTekeAim();
        }
        else if (card.name.Contains("DoubleBarrel"))
        {
            // Debug.Log("DoubleBarrel card played → Activate DoubleBarrel Mode!");
            CmdActivateDoubleBarrel();
        }
        else if (card.name.Contains("QuickShot"))
        {
            // Debug.Log("QuickShot card played → Activate QuickShot Mode!");
            CmdActivateQuickShot();
        }
        else if (card.name.Contains("Misfire"))
        {
            // Debug.Log("Misfire card played → Activate Misfire Mode!");
            CmdActivateMisfire();
        }
        else if (card.name.Contains("TwoBirds"))
        {
            // Debug.Log("TwoBirds card played → Activate TwoBirds Mode!");
            CmdActivateTwoBirds();
        }
        else if (card.name.Contains("BumpLeft"))
        {
            // Debug.Log("BumpLeft card played → Activate BumpLeft Mode!");
            CmdActivateBumpLeft();
        }
        else if (card.name.Contains("BumpRight"))
        {
            // Debug.Log("BumpRight card played → Activate BumpRight Mode!");
            CmdActivateBumpRight();
        }
        else if (card.name.Contains("LineForward"))
        {
            // Debug.Log("LineForward: card played → Activate LineForward: Mode!");
            CmdActivateLineForward();
        }
        else if (card.name.Contains("MoveAhead"))
        {
            // Debug.Log("MoveAhead: card played → Activate MoveAhead: Mode!");
            CmdActivateMoveAhead();
        }
        else if (card.name.Contains("HangBack"))
        {
            // Debug.Log("HangBack: card played → Activate HangBack: Mode!");
            CmdActivateHangBack();
        }
        else if (card.name.Contains("FastForward"))
        {
            // Debug.Log("FastForward: card played → Activate FastForward: Mode!");
            CmdActivateFastForward();
        }
        else if (card.name.Contains("DisorderlyConduckt"))
        {
            // Debug.Log("DisorderlyConduckt: card played → Activate DisorderlyConduckt: Mode!");

            CmdActivateDisorderlyConduckt();

        }
        else if (card.name.Contains("DuckShuffle"))
        {
            // Debug.Log("DuckShuffle: card played → Activate DuckShuffle: Mode!");
            CmdActivateDuckShuffle(); // เรียกใช้งานฟังก์ชันสำหรับ DuckShuffle
        }

        else if (card.name.Contains("GivePeaceAChance"))
        {
            CmdActivateGivePeaceAChance();
        }
        else if (card.name.Contains("Resurrection"))
        {
            CmdActivateResurrectionMode();
        }
        // else if (card.name.Contains("DuckAndCover"))
        // {
        //     CmdActivateDuckAndCoverMode();
        // }
    }

    private void DeactivateAllOtherCards()
    {
        // ปิดการใช้งานของการ์ดอื่น ๆ ก่อน
        CmdDeactivateTekeAim();
        CmdDeactivateShoot();
        CmdDeactivateQuickShot();
        CmdDeactivateDoubleBarrel();
        CmdDeactivateMisfire();
        CmdDeactivateTwoBirds();
        CmdDeactivateBumpLeft();
        CmdDeactivateBumpRight();
        CmdDeactivateLineForward();
        CmdDeactivateMoveAhead();
        CmdDeactivateHangBack();
        CmdDeactivateFastForward();
        CmdDeactivateDisorderlyConduckt();
        CmdDeactivateDuckShuffle();
        CmdDeactivateGivePeaceAChance();
        CmdDeactivateResurrectionMode();
        // CmdDeactivateDuckAndCoverMode();
    }


    // ========================
    // ตัวอย่าง Targeting
    // ========================
    [Command]
    public void CmdTargetSelfCard()
    {
        TargetSelfCard();
    }

    [Command(requiresAuthority = false)]
    public void CmdTargetOtherCard(GameObject target)
    {
        var opponentIdentity = target.GetComponent<NetworkIdentity>();
        if (opponentIdentity != null)
        {
            TargetOtherCard(opponentIdentity.connectionToClient);
        }

        if (!target)
        {
            Debug.LogError("[CmdTargetOtherCard] target GameObject is null!");
            return;
        }
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
        var increment = card.GetComponent<IncrementClick>();
        if (increment != null)
        {
            increment.NumberOfClicks++;
            Debug.Log("การ์ดนี้ถูกคลิกแล้ว " + increment.NumberOfClicks + " times!");
        }
    }
}