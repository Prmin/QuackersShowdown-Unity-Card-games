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


    // ใช้บน server (เรียกได้ทั้ง server/host) — cache ผลลัพธ์เล็กน้อย
    private Transform _cachedDuckZone;
    [Server]
    private Transform GetSceneDuckZone()
    {
        // ถ้ามีค่า cache แล้วยัง valid ให้ใช้
        if (_cachedDuckZone != null && _cachedDuckZone.gameObject.scene.IsValid() && _cachedDuckZone.gameObject.scene.isLoaded)
            return _cachedDuckZone;

        // ถ้ามีฟีลด์ DuckZone ที่อ้างไว้ และ valid ให้ใช้
        if (DuckZone != null)
        {
            var t = DuckZone.transform;
            if (t != null && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded)
            {
                _cachedDuckZone = t;
                return t;
            }
        }

        // หาใหม่จากชื่อในซีน
        var go = GameObject.Find("DuckZone");
        if (go != null)
        {
            _cachedDuckZone = go.transform;
            return _cachedDuckZone;
        }

        // ไม่เจอ
        return null;
    }

    // ถ้ามีจังหวะที่ scene อาจเปลี่ยน (unload/load) ให้เคลียร์ cache
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

        // ดึงการ์ดเป็ดทั้งหมดใน DuckZone
        var list = new List<DuckCard>();
        foreach (Transform t in dz)
        {
            var dc = t.GetComponent<DuckCard>();
            if (dc != null) list.Add(dc);
        }

        // จัดลำดับตามตำแหน่ง X ปัจจุบัน (หรือจะใช้ siblingIndex ก็ได้)
        list.Sort((a, b) =>
        {
            var ra = a.GetComponent<RectTransform>();
            var rb = b.GetComponent<RectTransform>();
            float ax = ra ? ra.anchoredPosition.x : a.transform.GetSiblingIndex();
            float bx = rb ? rb.anchoredPosition.x : b.transform.GetSiblingIndex();
            return ax.CompareTo(bx);
        });

        // ไล่กำหนดคอลัมน์ใหม่
        for (int i = 0; i < list.Count; i++)
        {
            var dc = list[i];
            // ใช้โซนเดิม (DuckZone), แถวเดิม (0), คอลัมน์ใหม่ i
            dc.ServerAssignToZone(ZoneKind.DuckZone, 0, i);
        }
    }

    [Server]
    private void Server_DestroyAllTargetsFor(uint duckNetId)
    {
        // รุ่นใหม่: TargetMarker
        var markers = FindObjectsOfType<TargetMarker>();
        foreach (var m in markers)
            if (m != null && m.FollowDuckNetId == duckNetId)
                NetworkServer.Destroy(m.gameObject);

        // สำรอง: รุ่นเดิม TargetFollow
        var follows = FindObjectsOfType<TargetFollow>();
        foreach (var f in follows)
            if (f != null && f.targetNetId == duckNetId)
                NetworkServer.Destroy(f.gameObject);
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
    /// เรียกตอนคลิกการ์ดเป็ดที่จะยิง
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdShootCard(NetworkIdentity duckCardIdentity)
    {
        if (!isShootActive) return;
        if (duckCardIdentity == null) return;

        var shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null) return;

        // ต้องเป็นใบที่ถูกเล็งอยู่เท่านั้น
        if (!IsCardTargeted(duckCardIdentity)) return;

        // เก็บตำแหน่งไว้ก่อนเผื่อใช้ (ตอนนี้ row ใช้ 0 ตลอด)
        int shotRow = shotDuck.RowNet;
        int shotCol = shotDuck.ColNet;

        // 1) ทำลายเป็ดที่ถูกยิง
        NetworkServer.Destroy(duckCardIdentity.gameObject);

        // 2) ทำลาย target ที่ตามการ์ดนี้อยู่ทั้งหมด
        Server_DestroyAllTargetsFor(duckCardIdentity.netId);

        // 3) Resequence: จัด ColNet ใหม่ให้ DuckZone ทั้งแถว
        Server_ResequenceDuckZoneColumns();

        // 4) ปิดโหมดยิง
        CmdDeactivateShoot();

        // 5) เติมการ์ดใหม่รอบหน้า
        StartCoroutine(RefillNextFrame());
    }


    [Server]
    IEnumerator RefillNextFrame()
    {
        // ให้ Mirror เคลียร์วัตถุที่เพิ่ง Destroy ออกจากต้นไม้ซีนก่อน
        yield return null;
        RefillDuckZoneIfNeeded();
    }


    /// <summary>
    /// การ์ดใบนี้ถูกเล็งอยู่ไหม (รองรับทั้ง TargetMarker แบบใหม่ และ TargetFollow แบบเดิม)
    /// </summary>
    bool IsCardTargeted(NetworkIdentity duckCardIdentity)
    {
        uint duckId = duckCardIdentity.netId;

        // รุ่นใหม่: TargetMarker
        var markers = FindObjectsOfType<TargetMarker>();
        foreach (var m in markers)
            if (m != null && m.FollowDuckNetId == duckId)
                return true;

        // สำรอง: รุ่นเดิม TargetFollow
        var follows = FindObjectsOfType<TargetFollow>();
        foreach (var f in follows)
            if (f != null && f.targetNetId == duckId)
                return true;

        return false;
    }


    // ========================
    // DoubleBarrel Logic
    // ========================

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
        // client-side hint (ถ้าต้องการ UI)
        // Debug.Log("DoubleBarrel Mode is now active on all clients.");
    }

    /// <summary>ปิดโหมด DoubleBarrel</summary>
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

        if (doubleBarrelClickCount == 0)
        {
            // ใบแรก
            firstClickedCard = clickedCard;
            doubleBarrelClickCount = 1;
            Debug.Log($"[DoubleBarrel] First card = {clickedCard.name} (waiting second)");
        }
        else if (doubleBarrelClickCount == 1)
        {
            // ใบสอง
            if (firstClickedCard == null)
            {
                // safety
                doubleBarrelClickCount = 0;
                return;
            }

            bool canPlace = CheckAdjacent(firstClickedCard, clickedCard);
            if (!canPlace)
            {
                Debug.LogWarning($"[DoubleBarrel] {clickedCard.name} is NOT adjacent to {firstClickedCard.name} in same row. Ignoring second click.");
                // ไม่รีเซ็ตเลยให้ผู้เล่นลองคลิกใหม่ หราจะรีเซ็ตก็ได้:
                // doubleBarrelClickCount = 0; firstClickedCard = null;
                return;
            }

            // ถ้า adjacent → spawn target 2 อัน โดยใช้ TargetMarker (ServerAssignToZone) แบบปลอดภัย
            CmdSpawnTargetDoubleBarrel_Internal(firstClickedCard);
            CmdSpawnTargetDoubleBarrel_Internal(clickedCard);

            // ปิดโหมด
            CmdDeactivateDoubleBarrel();
        }
    }

    // internal helper ใช้ Server-side สร้าง marker + set zone/col แล้ว Spawn
    [Server]
    private void CmdSpawnTargetDoubleBarrel_Internal(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null || targetPrefab == null) return;

        var dc = duckCardIdentity.GetComponent<DuckCard>();
        if (dc == null)
        {
            Debug.LogWarning("[DoubleBarrel] target card has no DuckCard component.");
            return;
        }

        GameObject newTarget = Instantiate(targetPrefab);

        var marker = newTarget.GetComponent<TargetMarker>();
        var tf = newTarget.GetComponent<TargetFollow>();

        if (marker != null)
        {
            // ตั้ง SyncVar ของ marker ก่อน Spawn — late-joiner จะได้ค่า
            marker.ServerAssignToZone(ZoneKind.TargetZone, 0, dc.ColNet);
            marker.FollowDuckNetId = duckCardIdentity.netId;
        }
        else
        {
            // ถ้า Prefab ไม่มี TargetMarker ให้ fallback: ตั้ง TargetFollow.targetNetId ถ้ามี
            if (tf != null)
            {
                tf.targetNetId = duckCardIdentity.netId;
            }
        }

        NetworkServer.Spawn(newTarget);
    }

    // =============================
    // เช็กว่า card1 อยู่แถวเดียวกับ card2 และ index ต่างกัน 1 หรือเปล่า
    // =============================
    [Server]
    private bool CheckAdjacent(NetworkIdentity card1, NetworkIdentity card2)
    {
        if (card1 == null || card2 == null) return false;

        var duck1 = card1.GetComponent<DuckCard>();
        var duck2 = card2.GetComponent<DuckCard>();
        if (duck1 == null || duck2 == null) return false;

        Debug.Log($"[DoubleBarrel] CheckAdjacent: {duck1.name}(r{duck1.RowNet},c{duck1.ColNet}) vs {duck2.name}(r{duck2.RowNet},c{duck2.ColNet})");

        // ต้องอยู่แถวเดียวกัน
        if (duck1.RowNet != duck2.RowNet) return false;

        // ต้องเป็นคอลัมน์ติดกัน (ต่างกันแค่ 1)
        int diff = Mathf.Abs(duck1.ColNet - duck2.ColNet);
        return diff == 1;
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
        if (!isQuickShotActive) return;

        // 1) เช็กว่า duckCardIdentity ปกติ
        if (duckCardIdentity == null) return;

        // 2) ดึง DuckCard
        DuckCard shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null) return;

        // ใช้ RowNet / ColNet ตาม DuckCard เวอร์ชันใหม่
        int shotRow = shotDuck.RowNet;
        int shotCol = shotDuck.ColNet;

        // 3) (ถ้าต้องการ) เช็กว่าการ์ดอยู่ในโซนที่ยิงได้ — ถ้าอยากบังคับให้มีเป้า ก็เรียก IsCardTargeted() ก่อน Destroy
        // if (!IsCardTargeted(duckCardIdentity)) return;

        // 4) ทำลายการ์ดบนเซิร์ฟ
        NetworkServer.Destroy(duckCardIdentity.gameObject);

        // 4.1) ทำลายเป้าเล็งที่ชี้การ์ดนี้ (ถ้ามี)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckCardIdentity.netId)
            {
                NetworkServer.Destroy(target.gameObject);
            }
        }

        // 4.2) เลื่อนคอลัมน์ลง (server-side function ของคุณ)
        ShiftColumnsDown(shotRow, shotCol);

        // 5) ปิด QuickShot Mode ทันที
        CmdDeactivateQuickShot();

        // 6) เติมการ์ดใหม่รอบหน้า (Refill) — รันเป็น coroutine ฝั่ง server
        StartCoroutine(RefillNextFrame());
    }


    // ========================
    // Misfire Logic (ปรับแล้ว)
    // ========================

    [Command(requiresAuthority = false)]
    public void CmdActivateMisfire()
    {
        if (!isMisfireActive)
        {
            isMisfireActive = true;
            RpcEnableMisfire();
        }
    }

    [ClientRpc]
    void RpcEnableMisfire()
    {
        // แจ้ง client ว่า active
    }

    [Command(requiresAuthority = false)]
    public void CmdDeactivateMisfire()
    {
        isMisfireActive = false;
        RpcDisableMisfire();
    }

    [ClientRpc]
    void RpcDisableMisfire()
    {
        // แจ้ง client ว่า inactive
    }

    [Command(requiresAuthority = false)]
    public void CmdMisfireClick(NetworkIdentity clickedCard)
    {
        if (!isMisfireActive) return;
        if (clickedCard == null) return;

        // 1) ต้องมีเป้าเล็งที่ชี้การ์ดนี้ก่อน
        if (!IsCardTargeted(clickedCard))
        {
            Debug.LogWarning($"[CmdMisfireClick] {clickedCard.name} is NOT targeted => can't misfire!");
            return;
        }

        // 2) ดึง DuckCard component
        DuckCard duckComp = clickedCard.GetComponent<DuckCard>();
        if (duckComp == null)
        {
            Debug.LogWarning("[CmdMisfireClick] No DuckCard component on clicked!");
            return;
        }

        // ใช้ RowNet / ColNet (server-side fields)
        int row = duckComp.RowNet;
        int col = duckComp.ColNet;

        // 3) หา adjacent
        List<NetworkIdentity> neighbors = GetAdjacentDuckCards(row, col);

        if (neighbors.Count == 0)
        {
            Debug.Log("[CmdMisfireClick] No adjacent ducks => misfire does nothing!");
            return;
        }

        // 4) สุ่ม neighbor หนึ่งตัว
        var randomNeighbor = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];

        // 5) ยิงการ์ดที่ถูกสุ่ม
        Debug.Log($"[CmdMisfireClick] MISFIRE -> Shooting {randomNeighbor.name} instead of {clickedCard.name}!");
        ShootCardDirect(randomNeighbor);

        // 6) ทำลายเป้าเล็งที่ชี้การ์ดเดิม (ถ้ามี)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var t in allTargets)
        {
            if (t.targetNetId == clickedCard.netId)
            {
                NetworkServer.Destroy(t.gameObject);
                Debug.Log($"[CmdMisfireClick] Destroyed target {t.name} on {clickedCard.name}");
            }
        }

        // ปิดโหมด Misfire และ refill
        CmdDeactivateMisfire();
        StartCoroutine(RefillNextFrame());
    }

    // ใช้ GetSceneDuckZone() เพื่อความปลอดภัย (กรณี DuckZone null)
    private List<NetworkIdentity> GetAdjacentDuckCards(int row, int col)
    {
        List<NetworkIdentity> results = new List<NetworkIdentity>();
        var dz = GetSceneDuckZone();
        if (dz == null)
        {
            Debug.LogWarning("[GetAdjacentDuckCards] DuckZone not found in scene!");
            return results;
        }

        foreach (Transform child in dz)
        {
            DuckCard duck = child.GetComponent<DuckCard>();
            if (duck == null) continue;

            // เปรียบเทียบกับ RowNet/ColNet
            if (duck.RowNet == row)
            {
                if (Mathf.Abs(duck.ColNet - col) == 1)
                {
                    var ni = duck.GetComponent<NetworkIdentity>();
                    if (ni != null) results.Add(ni);
                }
            }
        }
        return results;
    }

    private void ShootCardDirect(NetworkIdentity duckNi)
    {
        if (duckNi == null) return;

        // ทำลายการ์ด
        NetworkServer.Destroy(duckNi.gameObject);
        Debug.Log($"[ShootCardDirect] Destroyed {duckNi.name}");

        // ทำลาย target ที่ชี้การ์ดนี้ (ถ้ามี)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckNi.netId)
            {
                NetworkServer.Destroy(target.gameObject);
                Debug.Log($"[ShootCardDirect] Also destroyed target {target.name} pointing to {duckNi.name}");
            }
        }

        // ดึง DuckCard ก่อน Shift (ใช้ RowNet/ColNet)
        DuckCard dc = duckNi.GetComponent<DuckCard>();
        if (dc != null)
        {
            ShiftColumnsDown(dc.RowNet, dc.ColNet);
        }
    }


    // ========================
    // TwoBirds Logic (Refactored)
    // ========================

    // (ตัวแปร isTwoBirdsActive, twoBirdsClickCount, firstTwoBirdsCard ควรมีอยู่แล้ว)

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

    // (เพิ่ม Property นี้ ถ้า DuckCard.cs ต้องใช้เช็ก)
    public bool IsTwoBirdsActive => isTwoBirdsActive;


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

        // เช็กว่าใบที่คลิกมีเป้าเล็ง (สมมติว่า IsCardTargeted ทำงานถูกต้อง)
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
                // ใช้ Helper ที่แก้แล้ว
                canShootBoth = CheckAdjacentTwoBirds(firstTwoBirdsCard, clickedCard);
            }

            if (canShootBoth)
            {
                // =============== ยิง 2 ใบพร้อมกัน ===============

                // 1) เก็บ row/col ของสองใบ (จาก SyncVar)
                DuckCard dc1 = firstTwoBirdsCard.GetComponent<DuckCard>();
                DuckCard dc2 = clickedCard.GetComponent<DuckCard>();
                if (dc1 == null || dc2 == null)
                {
                    // Debug.LogWarning("[CmdTwoBirdsClick] One of the cards has no DuckCard component!");
                    CmdDeactivateTwoBirds();
                    return;
                }

                // === FIX: ใช้ RowNet / ColNet ===
                int row1 = dc1.RowNet;
                int col1 = dc1.ColNet;
                int row2 = dc2.RowNet;
                int col2 = dc2.ColNet;

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
                // ยิงได้แค่ใบแรกใบเดียว (ใบที่คลิกครั้งแรก)
                // Debug.Log("[CmdTwoBirdsClick] Cards are NOT adjacent => shoot only the first one.");

                if (firstTwoBirdsCard != null)
                {
                    DuckCard dc1 = firstTwoBirdsCard.GetComponent<DuckCard>();
                    if (dc1 == null)
                    {
                        CmdDeactivateTwoBirds();
                        return;
                    }

                    // === FIX: ใช้ RowNet / ColNet ===
                    int row1 = dc1.RowNet;
                    int col1 = dc1.ColNet;

                    // ทำลายใบแรก
                    NetworkServer.Destroy(firstTwoBirdsCard.gameObject);
                    RemoveTargetFromCard(firstTwoBirdsCard);

                    // เลื่อน column
                    ShiftColumnsDown(row1, col1);
                }
            }

            // ปิดโหมด TwoBirds
            CmdDeactivateTwoBirds();

            // (ถ้า ShiftColumnsDown ไม่ได้เรียก Refill ให้เรียกตรงนี้)
            // StartCoroutine(RefillNextFrame());
        }
    }


    [Server] // <-- (ควรแปะ [Server] เพราะเรียกใช้บน Server เท่านั้น)
    private bool CheckAdjacentTwoBirds(NetworkIdentity card1, NetworkIdentity card2)
    {
        DuckCard dc1 = card1.GetComponent<DuckCard>();
        DuckCard dc2 = card2.GetComponent<DuckCard>();
        if (dc1 == null || dc2 == null) return false;

        // === FIX: ใช้ RowNet / ColNet ===
        // เช็ก: อยู่ row เดียวกัน และ col ห่าง 1
        if (dc1.RowNet == dc2.RowNet && Mathf.Abs(dc1.ColNet - dc2.ColNet) == 1)
        {
            return true;
        }
        return false;
    }

    [Server] // <-- (ควรแปะ [Server] เพราะเรียก NetworkServer.Destroy)
    private void RemoveTargetFromCard(NetworkIdentity duckNi)
    {
        if (duckNi == null) return;

        // (วิธีนี้ O(n) แต่ถ้า Target ไม่เยอะก็ OK)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == duckNi.netId)
            {
                NetworkServer.Destroy(tf.gameObject);
                // Debug.Log($"[RemoveTargetFromCard] Also destroyed target {tf.name} pointing to {duckNi.name}");

                // ถ้ามั่นใจว่า 1 เป็ดมี 1 เป้า -> return; ได้เลย
                return;
            }
        }
    }

    // ========================
    // BumpLeft  Logic (Refactored)
    // ========================

    // (ตัวแปร isBumpLeftActive ควรมีอยู่แล้ว)

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

    public bool IsBumpLeftActive => isBumpLeftActive;


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

        // === FIX: ใช้ RowNet / ColNet ===
        int curRow = duck.RowNet;
        int curCol = duck.ColNet;
        // Debug.Log($"[CmdBumpLeftClick] Attempting to bump target from col={curCol} to col={curCol - 1} in row={curRow}");

        // 3) หาใบซ้าย (Column = curCol - 1) (ใช้ Helper ที่แก้แล้ว)
        DuckCard leftDuck = FindDuckAt(curRow, curCol - 1);
        if (leftDuck == null)
        {
            // Debug.LogWarning("[CmdBumpLeftClick] No duck on the left => can't bump!");
            return;
        }

        // 4) ย้ายเป้า = หา TargetFollow ที่เล็งการ์ดปัจจุบัน => เปลี่ยนให้ไปเล็งการ์ดใบซ้าย
        MoveTargetFromTo(clickedCard, leftDuck.GetComponent<NetworkIdentity>());

        // 5) ปิดโหมด BumpLeft
        CmdDeactivateBumpLeft();
    }

    // ========================
    // BumpRight Logic (Refactored)
    // ========================

    // (ตัวแปร isBumpRightActive ควรมีอยู่แล้ว)

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

    public bool IsBumpRightActive => isBumpRightActive;

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

        // === FIX: ใช้ RowNet / ColNet ===
        int curRow = duck.RowNet;
        int curCol = duck.ColNet;
        // Debug.Log($"[CmdBumpRightClick] Attempting to bump target from col={curCol} to col={curCol + 1} in row={curRow}");

        // 3) หาใบทางขวา (ใช้ Helper ที่แก้แล้ว)
        DuckCard rightDuck = FindDuckAt(curRow, curCol + 1);
        if (rightDuck == null)
        {
            // Debug.LogWarning("[CmdBumpRightClick] No duck on the right => can't bump right!");
            return;
        }

        // 4) ย้ายเป้า (target) จากการ์ดปัจจุบัน => ใบขวา
        MoveTargetFromTo(clickedCard, rightDuck.GetComponent<NetworkIdentity>());

        // 5) ปิดโหมด BumpRight
        CmdDeactivateBumpRight();
    }


    // ========================
    // Helpers (Refactored)
    // ========================

    [Server]
    private void MoveTargetFromTo(NetworkIdentity fromCard, NetworkIdentity toCard)
    {
        if (fromCard == null || toCard == null)
            return;

        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == fromCard.netId)
            {
                // === FIX: เปลี่ยน SyncVar [targetNetId] ===
                // สมมติว่า TargetFollow.targetNetId เป็น [SyncVar]
                // การเปลี่ยนค่านี้บน Server จะทำให้ Client ทุกคนอัปเดตตาม (ผ่าน Hook หรือ Update() ใน TargetFollow.cs)
                tf.targetNetId = toCard.netId;
                // Debug.Log($"[MoveTargetFromTo] Moved target from {fromCard.name} => {toCard.name}");

                // ไม่จำเป็นต้องเรียก RPC ซ้ำซ้อน (ถ้า targetNetId เป็น SyncVar)
                // RpcUpdateTargetPosition(...)
                // RpcSetTargetNetId(...)

                return; // ย้ายตัวเดียวแล้วออก
            }
        }
    }

    [Server]
    private DuckCard FindDuckAt(int row, int col)
    {
        // === FIX: วนหาจาก NetworkServer.spawned (ชัวร์ที่สุด) ===
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();

            // เช็กว่า 1. เป็น DuckCard, 2. อยู่ใน DuckZone, 3. ตำแหน่งตรง
            if (card != null && card.Zone == ZoneKind.DuckZone &&
                card.RowNet == row && card.ColNet == col)
            {
                return card;
            }
        }
        return null; // ไม่เจอ
    }

    // ========================
    // LineForward Logic (Refactored)
    // ========================

    // (ตัวแปร isLineForwardActive ควรมีอยู่แล้ว)

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

        // 1) เก็บเป้าก่อน (ใช้ Helper ที่แก้แล้ว)
        var oldTargets = CollectTargetColumns();

        // 2) คืนและทำลายเฉพาะใบซ้ายสุด (ใช้ Helper ที่แก้แล้ว)
        var leftmost = FindLeftmostDuck(0); // สมมติว่าต้องการแถว 0
        if (leftmost != null)
        {
            // (การ์ดที่ถูกทำลายจะถูกคืนเข้า Pool โดยอัตโนมัติผ่าน NetworkBehaviour.OnStopServer หรือ OnDestroy)
            NetworkServer.Destroy(leftmost.gameObject);         // remove card

            // (ถ้า CardPoolManager.ReturnCard ไม่ได้ถูกเรียกอัตโนมัติใน OnDestroy ก็ต้องเรียกเอง)
            // CardPoolManager.ReturnCard(leftmost.gameObject); // +1 pool
        }

        // 3) ลบเป้าเดิม
        RemoveAllTargets();

        // 5) สร้างเป้าย้อนหลัง (FIX: แก้ชื่อฟังก์ชันที่เรียก)
        StartCoroutine(RefillAndRecreateTargets(oldTargets));

        StartCoroutine(DelayedLog());

        // 6) ปิดโหมด
        CmdDeactivateLineForward();
    }

    private IEnumerator DelayedLog()
    {
        // รอจนจบ frame ให้ OnStopServer() คืน pool เสร็จ
        yield return null;
        // LogTotalDuckCounts();
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
    [Server] // <-- (Helper นี้ถูกเรียกบน Server)
    private List<int> CollectTargetColumns()
    {
        List<int> targetColumns = new List<int>();
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();

        foreach (var tf in allTargets)
        {
            // === FIX: ใช้ NetworkServer.spawned ===
            if (NetworkServer.spawned.TryGetValue(tf.targetNetId, out NetworkIdentity duckNi))
            {
                DuckCard duck = duckNi.GetComponent<DuckCard>();

                // === FIX: ใช้ ColNet ===
                if (duck != null && !targetColumns.Contains(duck.ColNet))
                {
                    targetColumns.Add(duck.ColNet);
                    // Debug.Log($"[CollectTargetColumns] Target at Column {duck.ColNet} recorded.");
                }
            }
        }

        targetColumns.Sort();
        return targetColumns;
    }

    // ========================================================
    // ✅ 2) หาและลบการ์ดใบซ้ายสุด (Column 0) เท่านั้น
    // ========================================================
    [Server] // <-- (Helper นี้ถูกเรียกบน Server)
    private DuckCard FindLeftmostDuck(int row)
    {
        DuckCard result = null;
        int minCol = int.MaxValue;

        // === FIX: วนหาจาก NetworkServer.spawned ===
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard d = netId.GetComponent<DuckCard>();

            // === FIX: ใช้ RowNet / ColNet และเช็ก Zone ===
            if (d != null && d.Zone == ZoneKind.DuckZone && d.RowNet == row)
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

    // ========================================================
    // ✅ 3) ลบเป้าเล็งทั้งหมด
    // ========================================================
    [Server] // <-- (Helper นี้ถูกเรียกบน Server)
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
    [Server] // <-- (Helper นี้ถูกเรียกบน Server)
    private List<DuckCard> FindDucksInRow(int row)
    {
        List<DuckCard> list = new List<DuckCard>();

        // === FIX: วนหาจาก NetworkServer.spawned ===
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard d = netId.GetComponent<DuckCard>();

            // === FIX: ใช้ RowNet และเช็ก Zone ===
            if (d != null && d.Zone == ZoneKind.DuckZone && d.RowNet == row)
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
        List<DuckCard> ducks = FindDucksInRow(0); // หาเป็ดแถว 0

        foreach (int col in oldTargetColumns)
        {
            // === FIX: ใช้ ColNet ===
            DuckCard duckAtCol = ducks.Find(d => d.ColNet == col);
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
        // (โค้ดส่วนนี้ดูถูกต้องแล้ว ไม่ต้องแก้)
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
        // (โค้ดส่วนนี้ดูถูกต้องแล้ว ไม่ต้องแก้)
        if (!NetworkServer.spawned.TryGetValue(duckNetId, out NetworkIdentity duckNi)) // (แก้เป็น NetworkServer.spawned เพื่อความชัวร์)
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
        RpcSetTargetNetId(targetNi, duckNi); // (สมมติว่ามี RpcSetTargetNetId อยู่)
    }

    // ========================
    // Move Ahead Logic (Refactored)
    // ========================

    // (ตัวแปร isMoveAheadActive ควรมีอยู่แล้ว)

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

    public bool IsMoveAheadActive => isMoveAheadActive;

    [Command(requiresAuthority = false)]
    public void CmdMoveAheadClick(NetworkIdentity clickedCard)
    {
        if (!isMoveAheadActive) return;
        if (clickedCard == null) return;

        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        // === FIX: ใช้ RowNet / ColNet ===
        int curRow = selectedDuck.RowNet;
        int curCol = selectedDuck.ColNet;
        int targetCol = curCol - 1; // "ข้างหน้า" คือ Col น้อยกว่า

        // === FIX: ใช้ FindDuckAt (ที่แก้แล้ว) ===
        // (สมมติว่าต้องการสลับกับแถวเดียวกัน)
        DuckCard targetDuck = FindDuckAt(curRow, targetCol);
        if (targetDuck == null)
        {
            // Debug.LogWarning($"[CmdMoveAheadClick] No duck at ({curRow}, {targetCol}), can't swap!");
            return;
        }

        // 🔹 1) ตรวจสอบว่ามีเป้าเล็งที่การ์ดทั้งสองใบหรือไม่
        // (เก็บสถานะ "ใครมีเป้า" ไว้ก่อน)
        bool selectedHadTarget = IsCardTargeted(selectedDuck.netId);
        bool targetHadTarget = IsCardTargeted(targetDuck.netId);

        // 🔹 2) ทำลายเป้าเล็งทั้งหมดที่เกี่ยวข้อง (ถ้ามี)
        if (selectedHadTarget)
        {
            RemoveTargetFromCard(selectedDuck.netId);
        }
        if (targetHadTarget)
        {
            RemoveTargetFromCard(targetDuck.netId);
        }

        // 🔹 3) สลับตำแหน่งการ์ด (โดยการสลับ SyncVar)
        // (Server เปลี่ยนค่านี้ -> Client ทุกคนจะอัปเดต UI เองผ่าน Hook)
        selectedDuck.ColNet = targetCol;
        targetDuck.ColNet = curCol;

        // (ถ้าต้องสลับ Row ด้วย ก็ทำตรงนี้)
        // int tempRow = selectedDuck.RowNet;
        // selectedDuck.RowNet = targetDuck.RowNet;
        // targetDuck.RowNet = tempRow;

        // Debug.Log($"[CmdMoveAheadClick] Swapped {selectedDuck.name} (now at {selectedDuck.ColNet}) <-> {targetDuck.name} (now at {targetDuck.ColNet})");

        // 🔹 4) (ลบทิ้ง) RpcUpdateDuckPositions ไม่จำเป็นต้องใช้

        // 🔹 5) สร้างเป้าเล็งใหม่ "ที่ตำแหน่งเดิม" (ให้เป็ดตัวใหม่ที่ย้ายมา)

        // ถ้า "ใบที่เลือก" (selectedDuck) เคยมีเป้า, ให้สร้างเป้าให้ "ใบที่มาแทน" (targetDuck)
        if (selectedHadTarget)
        {
            CmdSpawnTargetForDuck(targetDuck.netId);
            // Debug.Log($"[CmdMoveAheadClick] Recreated target at column {curCol} for new duck {targetDuck.name}");
        }

        // ถ้า "ใบเป้าหมาย" (targetDuck) เคยมีเป้า, ให้สร้างเป้าให้ "ใบที่มาแทน" (selectedDuck)
        if (targetHadTarget)
        {
            CmdSpawnTargetForDuck(selectedDuck.netId);
            // Debug.Log($"[CmdMoveAheadClick] Recreated target at column {targetCol} for new duck {selectedDuck.name}");
        }

        // ปิดโหมด
        CmdDeactivateMoveAhead();
    }

    // (ลบ 🔹 ฟังก์ชันสลับ Column ของการ์ดเป็ดสองใบ (SwapDuckColumns) ทิ้ง)

    // (ลบ 🔹 หาเป็ดที่อยู่ใน Column ที่กำหนด (FindDuckAtMoveAhead) ทิ้ง)
    // (เราจะใช้ FindDuckAt ตัวที่ถูกต้องที่เราแก้ไว้ก่อนหน้านี้แทน)

    // (นี่คือ FindDuckAt ตัวที่ถูกต้อง - ถ้ายังไม่มีให้เพิ่ม)
    [Server]
    private DuckCard FindDuckAt(int row, int col)
    {
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();

            if (card != null && card.Zone == ZoneKind.DuckZone &&
                card.RowNet == row && card.ColNet == col)
            {
                return card;
            }
        }
        return null; // ไม่เจอ
    }


    // (ลบ 🔹 ซิงก์ตำแหน่งการ์ดไปยังทุก Client (RpcUpdateDuckPositions) ทิ้ง)

    // ========================
    // HangBack Logic (Refactored)
    // ========================

    // (ตัวแปร isHangBackActive ควรมีอยู่แล้ว)

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

    public bool IsHangBackActive => isHangBackActive;


    [Command(requiresAuthority = false)]
    public void CmdHangBackClick(NetworkIdentity clickedCard)
    {
        if (!isHangBackActive) return;
        if (clickedCard == null) return;

        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        // === FIX: ใช้ RowNet / ColNet ===
        int curRow = selectedDuck.RowNet;
        int curCol = selectedDuck.ColNet;
        int targetCol = curCol + 1; // ถอยหลังไปทางขวา

        // === FIX: ใช้ FindDuckAt (ตัวที่แก้แล้ว) ===
        DuckCard targetDuck = FindDuckAt(curRow, targetCol);
        if (targetDuck == null)
        {
            // Debug.LogWarning($"[CmdHangBackClick] No duck at ({curRow}, {targetCol}), can't swap!");
            return;
        }

        // 🔹 1) ตรวจสอบว่ามีเป้าเล็งที่การ์ดทั้งสองใบหรือไม่
        bool selectedHadTarget = IsCardTargeted(selectedDuck.netId);
        bool targetHadTarget = IsCardTargeted(targetDuck.netId);

        // 🔹 2) ทำลายเป้าเล็งทั้งหมดที่เกี่ยวข้อง (ถ้ามี)
        if (selectedHadTarget)
        {
            RemoveTargetFromCard(selectedDuck.netId);
        }
        if (targetHadTarget)
        {
            RemoveTargetFromCard(targetDuck.netId);
        }

        // 🔹 3) สลับตำแหน่งการ์ด (โดยการสลับ SyncVar)
        // (Server เปลี่ยนค่านี้ -> Client ทุกคนจะอัปเดต UI เองผ่าน Hook)
        selectedDuck.ColNet = targetCol;
        targetDuck.ColNet = curCol;

        // Debug.Log($"[CmdHangBackClick] Swapped {selectedDuck.name} (now at {selectedDuck.ColNet}) <-> {targetDuck.name} (now at {targetDuck.ColNet})");

        // 🔹 4) (ลบทิ้ง) RpcUpdateDuckPositions ไม่จำเป็นต้องใช้

        // 🔹 5) สร้างเป้าเล็งใหม่ "ที่ตำแหน่งเดิม" (ให้เป็ดตัวใหม่ที่ย้ายมา)

        // ถ้า "ใบที่เลือก" (selectedDuck) เคยมีเป้า, ให้สร้างเป้าให้ "ใบที่มาแทน" (targetDuck)
        if (selectedHadTarget)
        {
            CmdSpawnTargetForDuck(targetDuck.netId); // (สมมติว่ามี CmdSpawnTargetForDuck อยู่แล้ว)
            // Debug.Log($"[CmdHangBackClick] Recreated target at column {curCol} for new duck {targetDuck.name}");
        }

        // ถ้า "ใบเป้าหมาย" (targetDuck) เคยมีเป้า, ให้สร้างเป้าให้ "ใบที่มาแทน" (selectedDuck)
        if (targetHadTarget)
        {
            CmdSpawnTargetForDuck(selectedDuck.netId);
            // Debug.Log($"[CmdHangBackClick] Recreated target at column {targetCol} for new duck {selectedDuck.name}");
        }

        // ปิดโหมด
        CmdDeactivateHangBack();
    }

    // (ลบ 🔹 หา DuckCard ที่อยู่ใน Column ที่กำหนด (FindDuckAtHangBack) ทิ้ง)
    // (เพราะเราใช้ FindDuckAt(row, col) ตัวที่ถูกต้องแทนแล้ว)


    // ========================
    // FastForward Logic (Refactored)
    // ========================

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

    public bool IsFastForwardActive => isFastForwardActive;


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
        int curRow = selectedDuck.RowNet; // สมมติว่าทำในแถวของเป็ดที่เลือก

        // 🔹 1) เก็บตำแหน่งเป้า (ColNet) ทั้งหมดในแถวนี้ก่อน
        List<int> originalTargetColumns = new List<int>();
        List<TargetFollow> targetsToDestroy = new List<TargetFollow>();

        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            // (ใช้ FindDuckAt(row, col) ไม่ได้ เพราะเราไม่รู้ col/row ของเป้า)
            // (ต้องใช้ FindDuckByNetId ที่ถูกต้อง)
            DuckCard duck = FindDuckByNetId(tf.targetNetId);

            if (duck != null && duck.RowNet == curRow)
            {
                if (!originalTargetColumns.Contains(duck.ColNet))
                {
                    originalTargetColumns.Add(duck.ColNet);
                }
                targetsToDestroy.Add(tf);
            }
        }

        // 🔹 2) ลบเป้าทั้งหมดก่อนเริ่มย้ายการ์ด
        foreach (var tf in targetsToDestroy)
        {
            NetworkServer.Destroy(tf.gameObject);
        }

        // 🔹 3) ค่อยๆ สลับไปด้านหน้าเรื่อยๆ
        // === FIX: ใช้ ColNet และ FindDuckAt ที่ถูกต้อง ===
        while (selectedDuck.ColNet > 0)
        {
            int currentCol = selectedDuck.ColNet;
            int targetCol = currentCol - 1;
            DuckCard targetDuck = FindDuckAt(curRow, targetCol); // ใช้ Helper ที่ถูกต้อง

            if (targetDuck == null)
            {
                // Debug.LogWarning($"[FastForwardCoroutine] No duck at column {targetCol}, stopping swap.");
                break;
            }

            // 🔹 สลับตำแหน่ง (SyncVar)
            // (Server เปลี่ยน -> Client ทุกคนอัปเดต UI เอง)
            selectedDuck.ColNet = targetCol;
            targetDuck.ColNet = currentCol;

            // Debug.Log($"[FastForwardCoroutine] Swapped {selectedDuck.name} (now at {selectedDuck.ColNet}) <-> {targetDuck.name} (now at {targetDuck.ColNet})");

            // 🔹 (ลบ RpcUpdateDuckPositions ทิ้ง)

            yield return new WaitForSeconds(delay); // รอให้เห็นการสลับ
        }

        // 🔹 4) คืนเป้าเล็งกลับไปที่ตำแหน่งเดิม

        // (รอ 1 frame ให้ SyncVar ตัวสุดท้ายส่งไปถึง Client ก่อน)
        yield return null;

        foreach (int originalCol in originalTargetColumns)
        {
            DuckCard newDuckAtCol = FindDuckAt(curRow, originalCol); // ใช้ Helper ที่ถูกต้อง
            if (newDuckAtCol != null)
            {
                CmdSpawnTargetForDuck(newDuckAtCol.netId); // (สมมติว่ามี CmdSpawnTargetForDuck)
                // Debug.Log($"[FastForwardCoroutine] Recreated target at column {originalCol} for {newDuckAtCol.name}");
            }
        }

        // 🔹 5) ปิดโหมด
        CmdDeactivateFastForward();
    }


    // === (ลบ Helper ที่ผิดทิ้งทั้งหมด) ===
    // [Command] private void CmdSwapDuckColumns(...)  <-- ลบทิ้ง
    // private DuckCard FindDuckByNetId(...)           <-- ลบทิ้ง (หรือแก้ให้ถูก)
    // private DuckCard FindDuckAtColumn(...)          <-- ลบทิ้ง
    // [ClientRpc] void RpcUpdateDuckPositions()       <-- ลบทิ้ง


    // === (เพิ่ม Helper ที่ถูกต้อง (ถ้ายังไม่มี)) ===

    [Server]
    private DuckCard FindDuckByNetId(uint netId)
    {
        // === FIX: หาจาก NetworkServer.spawned ===
        if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity ni))
        {
            return ni.GetComponent<DuckCard>();
        }
        return null;
    }

    [Server]
    private DuckCard FindDuckAt(int row, int col)
    {
        // === FIX: หาจาก NetworkServer.spawned ===
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();

            if (card != null && card.Zone == ZoneKind.DuckZone &&
                card.RowNet == row && card.ColNet == col)
            {
                return card;
            }
        }
        return null; // ไม่เจอ
    }



    // ========================
    // Disorderly Conduckt Logic (Refactored)
    // ========================

    // (ตัวแปร isDisorderlyConducktActive และ firstSelectedDuck (DuckCard) ควรมีอยู่แล้ว)

    // 🔹 ฟังก์ชันเปิดโหมด
    [Command(requiresAuthority = false)]
    public void CmdActivateDisorderlyConduckt()
    {
        if (!isDisorderlyConducktActive)
        {
            isDisorderlyConducktActive = true;
            firstSelectedDuck = null; // รีเซ็ตทุกครั้งที่เปิดโหมด
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

    // 🔹 ฟังก์ชันปิดโหมด
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
            // Debug.Log($"[CmdDisorderlyClick] First selected: {selectedDuck.name} (Col: {selectedDuck.ColNet})");
            return;
        }

        // ถ้าเลือกการ์ดใบเดิม => ยกเลิก
        if (firstSelectedDuck == selectedDuck)
        {
            firstSelectedDuck = null;
            // Debug.Log($"[CmdDisorderlyClick] Selection cancelled.");
            return;
        }

        // ถ้าเลือกการ์ดที่สอง => เช็คว่าอยู่ติดกันหรือเปล่า
        DuckCard secondDuck = selectedDuck;

        // === FIX: ใช้ ColNet และ RowNet (สมมติว่าต้องแถวเดียวกัน) ===
        bool sameRow = firstSelectedDuck.RowNet == secondDuck.RowNet;
        bool adjacentCol = Mathf.Abs(firstSelectedDuck.ColNet - secondDuck.ColNet) == 1;

        if (!sameRow || !adjacentCol)
        {
            // Debug.LogWarning("[CmdDisorderlyClick] Ducks are not adjacent in the same row, ignoring!");
            firstSelectedDuck = selectedDuck; // ให้ใบนี้เป็นใบแรกแทน (UX ที่ดี)
            return;
        }

        // --- เริ่มกระบวนการสลับ (เพราะ adjacent) ---

        // 🔹 1) บันทึกว่าใบไหนมีเป้า
        bool firstHadTarget = IsCardTargeted(firstSelectedDuck.netId);
        bool secondHadTarget = IsCardTargeted(secondDuck.netId);

        // 🔹 2) ลบเป้าทั้งหมดที่เกี่ยวข้อง
        if (firstHadTarget) RemoveTargetFromCard(firstSelectedDuck.netId);
        if (secondHadTarget) RemoveTargetFromCard(secondDuck.netId);

        // 🔹 3) สลับตำแหน่งการ์ด (โดยการสลับ SyncVar)
        // === FIX: สลับ ColNet โดยตรง ===
        int tempCol = firstSelectedDuck.ColNet;
        firstSelectedDuck.ColNet = secondDuck.ColNet;
        secondDuck.ColNet = tempCol;

        // (ถ้าต้องสลับ RowNet ด้วย ก็ทำตรงนี้)
        // int tempRow = firstSelectedDuck.RowNet;
        // firstSelectedDuck.RowNet = secondDuck.RowNet;
        // secondDuck.RowNet = tempRow;

        // Debug.Log($"[CmdDisorderlyClick] Swapped {firstSelectedDuck.name} (now at {firstSelectedDuck.ColNet}) <-> {secondDuck.name} (now at {secondDuck.ColNet})");

        // 🔹 4) (ลบทิ้ง) RpcUpdateDuckPositions... ไม่จำเป็นต้องใช้

        // 🔹 5) คืนเป้ากลับไปที่ตำแหน่งเดิม (ให้เป็ดตัวใหม่ที่มาแทน)
        if (firstHadTarget)
        {
            CmdSpawnTargetForDuck(secondDuck.netId); // ใบที่ 1 เคยมีเป้า -> สร้างให้ใบที่ 2 (ที่ย้ายมาแทน)
        }
        if (secondHadTarget)
        {
            CmdSpawnTargetForDuck(firstSelectedDuck.netId); // ใบที่ 2 เคยมีเป้า -> สร้างให้ใบที่ 1 (ที่ย้ายมาแทน)
        }

        // รีเซ็ตการเลือก (เพื่อให้คลิกคู่ต่อไปได้)
        firstSelectedDuck = null;

        // (ถ้าสกิลนี้ใช้ครั้งเดียวจบ ให้เรียก CmdDeactivateDisorderlyConduckt())
        // CmdDeactivateDisorderlyConduckt(); 
    }

    // === (ลบ Helper ที่ผิดทิ้งทั้งหมด) ===
    // [ClientRpc] void RpcDestroyTargetsOnClient(...)                 <-- ลบทิ้ง
    // [ClientRpc] void RpcUpdateDuckPositionsForDuck...(...)          <-- ลบทิ้ง
    // [Server] private IEnumerator RecreateTargetsAfterSwap(...)      <-- ลบทิ้ง
    // [ClientRpc] void RpcRecreateTargets(...)                        <-- ลบทิ้ง
    // [Server] private void CmdSpawnTargetForDuckforDisorderly...()  <-- ลบทิ้ง (ใช้ตัวปกติ)
    // private DuckCard FindDuckAtColumnforDisorderlyConduckt(...)    <-- ลบทิ้ง


    // === (ใช้ Helper ที่ถูกต้อง (ถ้ายังไม่มี)) ===

    // (ใช้ CmdSpawnTargetForDuck ตัวที่แก้ใน LineForward)
    [Command(requiresAuthority = false)]
    private void CmdSpawnTargetForDuck(uint duckNetId)
    {
        // === FIX: ใช้ NetworkServer.spawned ===
        if (!NetworkServer.spawned.TryGetValue(duckNetId, out NetworkIdentity duckNi))
        {
            // Debug.LogWarning($"[CmdSpawnTargetForDuck] Duck netId={duckNetId} not found!");
            return;
        }
        if (targetPrefab == null) return;

        GameObject newTarget = Instantiate(targetPrefab);
        NetworkServer.Spawn(newTarget);
        NetworkIdentity targetNi = newTarget.GetComponent<NetworkIdentity>();
        RpcSetTargetNetId(targetNi, duckNi); // (สมมติว่ามี RpcSetTargetNetId อยู่)
    }

    // (ใช้ FindDuckAt ตัวที่แก้ใน FastForward)
    [Server]
    private DuckCard FindDuckAt(int row, int col)
    {
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();
            if (card != null && card.Zone == ZoneKind.DuckZone &&
                card.RowNet == row && card.ColNet == col)
            {
                return card;
            }
        }
        return null; // ไม่เจอ
    }


    // ========================
    // Duck Shuffle  Logic (Refactored)
    // ========================
    public void TryDuckShuffle()
    {
        // (อันนี้เรียกจาก Client)
        if (!isLocalPlayer) return;
        CmdActivateDuckShuffle();
    }

    [Command(requiresAuthority = false)]
    public void CmdActivateDuckShuffle()
    {
        if (isDuckShuffleActive) return;
        isDuckShuffleActive = true;

        // 1) เก็บเป้าก่อน (ใช้ Helper ที่แก้แล้ว)
        var oldTargets = CollectTargetColumns();

        // 2) คืนทุกใบใน zone → pool แล้วทำลาย (ใช้ Helper ที่แก้แล้ว)
        RemoveAllDucks();

        // 3) ลบเป้าเดิม
        RemoveAllTargets(); // (สมมติว่ามี RemoveAllTargets() ที่ถูกต้อง)

        // 4) รีฟิลใหม่ถึง 6 ใบ
        if (DuckZone == null)
        {
            // Debug.LogError("DuckZone is null!");
            return;
        }

        int needed = 6; // (Shuffle คือเติม 6 ใบใหม่เสมอ)
        for (int i = 0; i < needed; i++)
        {
            if (!CardPoolManager.HasCards()) break;

            // 1) DrawRandomCard จะ Instantiate ไว้บน DuckZone.transform
            GameObject cardGO = CardPoolManager.DrawRandomCard(DuckZone.transform);
            if (cardGO == null) break;

            // 2) ตั้งค่า Row/Column (SyncVar) ให้ถูกต้อง
            var duck = cardGO.GetComponent<DuckCard>();
            if (duck != null)
            {
                // === FIX: ตั้งค่า SyncVar ===
                duck.RowNet = 0;
                duck.ColNet = i; // ใช้ i เป็น ColNet ที่ถูกต้อง
                duck.Zone = ZoneKind.DuckZone;
            }

            // 3) Spawn & RPC add
            NetworkServer.Spawn(cardGO);
            RpcAddCardToDuckZone(cardGO); // (สมมติว่ามี RpcAddCardToDuckZone)
        }

        // 5) สร้างเป้าย้อน
        StartCoroutine(RecreateTargetsAfterShuffle(oldTargets));

        StartCoroutine(DelayedLog()); // (สมมติว่ามี DelayedLog)

        // 6) ปิดโหมด
        CmdDeactivateDuckShuffle();
    }


    [Server]
    private IEnumerator RecreateTargetsAfterShuffle(List<int> oldCols)
    {
        // (ลบ RefillNextFrameDuckShuffle() ที่ซ้ำซ้อนทิ้ง)

        // รอ 1 เฟรมให้ layout ปรับตำแหน่งเสร็จ
        yield return null;

        // ค้น DuckCard แต่ละใบใน row 0 (ใช้ Helper ที่แก้แล้ว)
        List<DuckCard> ducks = FindDucksInRow(0);

        // สร้างเป้าย้อนกลับ
        foreach (int col in oldCols)
        {
            // === FIX: ค้นหาด้วย ColNet ===
            var duckAtCol = ducks.Find(d => d.ColNet == col);
            if (duckAtCol != null)
            {
                CmdSpawnTargetForDuck(duckAtCol.netId); // (สมมติว่ามี CmdSpawnTargetForDuck)
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
    // Helpers (Refactored)
    // ========================

    [Server]
    private void RemoveAllDucks()
    {
        // === FIX: วนหาจาก NetworkServer.spawned (ชัวร์ที่สุด) ===
        List<GameObject> ducksToDestroy = new List<GameObject>();

        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            // หาเป็ดที่อยู่ใน DuckZone เท่านั้น
            if (netId.TryGetComponent(out DuckCard duck) && duck.Zone == ZoneKind.DuckZone)
            {
                ducksToDestroy.Add(duck.gameObject);
            }
        }

        // ทำลาย (แยก List เพื่อไม่ให้ Collection เปลี่ยนขณะวนลูป)
        foreach (var duckGO in ducksToDestroy)
        {
            CardPoolManager.ReturnCard(duckGO); // +1 pool
            NetworkServer.Destroy(duckGO);
            // Debug.Log($"[RemoveAllDucks] Destroyed duck: {duckGO.name}");
        }
    }

    [Server]
    private List<int> CollectTargetColumns()
    {
        // (นี่คือ Helper ตัวที่แก้แล้วจาก LineForward)
        List<int> targetColumns = new List<int>();
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();

        foreach (var tf in allTargets)
        {
            if (NetworkServer.spawned.TryGetValue(tf.targetNetId, out NetworkIdentity duckNi))
            {
                DuckCard duck = duckNi.GetComponent<DuckCard>();

                // === FIX: ใช้ ColNet ===
                if (duck != null && !targetColumns.Contains(duck.ColNet))
                {
                    targetColumns.Add(duck.ColNet);
                }
            }
        }
        targetColumns.Sort();
        return targetColumns;
    }

    [Server]
    private List<DuckCard> FindDucksInRow(int row)
    {
        // (นี่คือ Helper ตัวที่แก้แล้วจาก LineForward)
        List<DuckCard> list = new List<DuckCard>();
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard d = netId.GetComponent<DuckCard>();
            // === FIX: ใช้ RowNet และ Zone ===
            if (d != null && d.Zone == ZoneKind.DuckZone && d.RowNet == row)
            {
                list.Add(d);
            }
        }
        return list;
    }


    // (ลบ RefillNextFrameDuckShuffle() และ RefillDuckZoneIfNeededDuckShuffle() ที่ซ้ำซ้อนทิ้ง)


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

        // ลบเป้าเล็งทั้งหมด (ใช้ Helper [Server] ที่แก้แล้ว)
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
    // Resurrection  Logic (Refactored)
    // ========================

    // เปลี่ยนชื่อเมธอดให้ไม่ชนกับชื่อคลาสหรือฟิลด์เดิม
    public void TryUseResurrection()
    {
        if (!isLocalPlayer) return;
        CmdActivateResurrectionMode();
    }

    [Command] // (Command นี้ควรเป็น requiresAuthority = true (default) เพราะผู้เล่นเรียกเอง)
    private void CmdActivateResurrectionMode()
    {
        if (isResurrectionModeActive) return;
        isResurrectionModeActive = true;

        const int maxPerColor = 5; // (สมมติว่า max คือ 5)

        // 1) ดึงจำนวนรวม (pool + zone) (ใช้ Helper ที่แก้แล้ว)
        var totalCounts = GetTotalDuckCounts();

        // 2) หาเฉพาะสีที่มีน้อยกว่า maxPerColor
        var lowColors = new List<string>();

        // (เราควรรู้จักสีทั้งหมดจาก CardPoolManager.GetAllColorKeys() หรือที่คล้ายกัน)
        // (ถ้า GetTotalDuckCounts() คืนค่าเฉพาะสีที่มี, เราต้องเช็กสีที่ "ไม่มีเลย" (0) ด้วย)

        // (สมมติว่า CardPoolManager มีสีทั้งหมด)
        foreach (string color in CardPoolManager.GetAllColorKeys())
        {
            int currentCount = 0;
            if (!totalCounts.TryGetValue(color, out currentCount))
            {
                currentCount = 0; // ถ้าไม่มีเลย = 0
            }

            if (currentCount < maxPerColor)
            {
                lowColors.Add(color);
            }
        }


        if (lowColors.Count > 0)
        {
            // 3) สุ่มเลือกสี แล้วบวกใน pool
            int idx = Random.Range(0, lowColors.Count);
            string color = lowColors[idx];

            CardPoolManager.AddToPool(color);
            // Debug.Log($"[Resurrection] Added one {color} back to pool");
        }
        else
        {
            // Debug.LogWarning("[Resurrection] No color below max count—nothing added");
        }

        // StartCoroutine(DelayedLog()); // (ถ้ามี DelayedLog)

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
        // (Client-side UI update)
    }

    // === NEW HELPER (Refactored) ===
    [Server]
    private Dictionary<string, int> GetTotalDuckCounts()
    {
        // 1. เอาจำนวนจากใน Pool มาก่อน
        Dictionary<string, int> counts = CardPoolManager.GetPoolCounts(); // (สมมติว่ามีฟังก์ชันนี้)

        // 2. วนหาเป็ดใน DuckZone (ที่ active ใน Server)
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();

            // (เช็กว่า 1. เป็นเป็ด, 2. อยู่ใน DuckZone, 3. มีสี (สมมติว่ามี .ColorKey))
            if (card != null && card.Zone == ZoneKind.DuckZone && !string.IsNullOrEmpty(card.ColorKey))
            {
                if (!counts.ContainsKey(card.ColorKey))
                {
                    counts[card.ColorKey] = 0;
                }
                counts[card.ColorKey]++;
            }
        }

        return counts;
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

// =================================================================
// นิยาม SkillMode Enum 
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