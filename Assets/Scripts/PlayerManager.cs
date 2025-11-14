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


public class PlayerManager : NetworkBehaviour
{

    // ตัวแปร State กลาง
    [SyncVar(hook = nameof(OnSkillModeChanged))]
    public SkillMode activeSkillMode = SkillMode.None;


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

    // // ========== Resurrection  State ==========
    // private bool isResurrectionModeActive = false;
    // // ========== GivePeaceAChance  State ==========
    // private bool isGivePeaceActive = false;
    // // ========== DuckShuffle  State ==========
    // [SyncVar] private bool isDuckShuffleActive = false;
    // public bool IsDuckShuffleActive => isDuckShuffleActive;
    // // ========== DisorderlyConduckt  State ==========
    // [SyncVar] private bool isDisorderlyConducktActive = false;
    // public bool IsDisorderlyConducktActive => isDisorderlyConducktActive;
    private DuckCard firstSelectedDuck = null; // เก็บการ์ดใบแรกที่เลือก

    // // ========== FastForward  State ==========
    // [SyncVar] private bool isFastForwardActive = false;
    // public bool IsFastForwardActive => isFastForwardActive;
    // // ========== HangBack  State ==========
    // [SyncVar] private bool isHangBackActive = false;
    // public bool IsHangBackActive => isHangBackActive;
    // // ========== MoveAhead  State ==========
    // [SyncVar] private bool isMoveAheadActive = false;
    // public bool IsMoveAheadActive => isMoveAheadActive;
    // // ========== LineForward  State ==========
    // [SerializeField] private GameObject cardPoolLineForward; // สมมติว่าเป็น Parent วาง "การ์ดที่กลับสู่ pool"
    // public bool isLineForwardActive = false;

    // public bool IsLineForwardActive => isLineForwardActive;
    // // ========== BumpRight  State ==========
    // [SyncVar] private bool isBumpRightActive;
    // public bool IsBumpRightActive => isBumpRightActive;
    // // ========== BumpLeft  State ==========
    // [SyncVar] private bool isBumpLeftActive;
    // public bool IsBumpLeftActive => isBumpLeftActive;

    // // ========== TwoBirds State ==========
    // [SyncVar] private bool isTwoBirdsActive;
    // public bool IsTwoBirdsActive => isTwoBirdsActive;

    private NetworkIdentity firstTwoBirdsCard = null;
    private int twoBirdsClickCount = 0;

    // // ========== DoubleBarrel State ==========
    // [SyncVar] private bool isDoubleBarrelActive = false;

    // // ตัวนับว่าเราคลิกการ์ด DoubleBarrel ไปกี่ใบแล้ว (0,1,...)
    private int doubleBarrelClickCount = 0;
    // // เก็บ Card ใบแรกที่คลิก
    private NetworkIdentity firstClickedCard = null;

    // //  ========== Misfire State ==========
    // [SyncVar] private bool isMisfireActive = false;
    // // สำหรับเช็กว่าอยู่ในโหมด MisfireAim หรือเปล่า
    // public bool IsMisfireActive => isMisfireActive;


    // //  ========== Shoot State ==========
    // [SyncVar] bool isShootActive;
    // //  ========== QuickShot State ==========
    // [SyncVar] bool isQuickShotActive;

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
    // private bool isTekeAimActive = false;

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










    // ========================
    //  Core State Logic 
    // ========================

    // (Optional) Hook สำหรับ Client UI 
    void OnSkillModeChanged(SkillMode oldMode, SkillMode newMode)
    {
        // Debug.Log($"[Client] Skill mode changed from {oldMode} to {newMode}");
        // (เช่น UIManager.Instance.HighlightSkillButton(newMode);)
    }

    // Command หลักสำหรับ Client (Local Player) ใช้เปลี่ยนโหมด
    [Command]
    public void CmdSetSkillMode(SkillMode newMode)
    {
        // Server เป็นคนเปลี่ยนค่า SyncVar นี้
        activeSkillMode = newMode;

        // --- 🚀 3.1 (ย้าย Logic สกิลที่ "รันทันที" มาไว้ที่นี่) ---

        bool modeShouldClose = false;

        if (newMode == SkillMode.LineForward)
        {
            CmdActivateLineForward(); // (เรียก Logic เดิม)
            modeShouldClose = true; // ทำงานเสร็จ ปิดโหมด
        }
        else if (newMode == SkillMode.DuckShuffle)
        {
            CmdActivateDuckShuffle(); // (เรียก Logic เดิม)
            modeShouldClose = true; // ทำงานเสร็จ ปิดโหมด
        }
        else if (newMode == SkillMode.GivePeaceAChance)
        {
            CmdActivateGivePeaceAChance(); // (เรียก Logic เดิม)
            modeShouldClose = true; // ทำงานเสร็จ ปิดโหมด
        }
        else if (newMode == SkillMode.Resurrection)
        {
            CmdActivateResurrectionMode(); // (เรียก Logic เดิม)
            modeShouldClose = true; // ทำงานเสร็จ ปิดโหมด
        }

        // (สกิลที่ "ทำงานทันที" อื่นๆ ก็ย้ายมาที่นี่)

        // ถ้าสกิลที่ทำงานทันที ควรปิดโหมดเลย
        if (modeShouldClose)
        {
            activeSkillMode = SkillMode.None;
        }
    }

    // Logic กลางสำหรับ "คลิกเป็ด" (เรียกจาก DuckCard.cs)
    public void HandleDuckCardClick(DuckCard clickedCard)
    {
        if (!isLocalPlayer) return;

        // เช็กแค่ตัวแปรเดียว!
        switch (activeSkillMode)
        {
            case SkillMode.None:
                // ไม่ได้ใช้สกิล
                break;

            // --- 🚀 3.2 (สกิลที่รอคลิกเป็ด) ---

            case SkillMode.Shoot:
                CmdShootCard(clickedCard.netIdentity);
                // (CmdShootCard จะปิดโหมดเอง)
                break;

            case SkillMode.TakeAim:
                CmdSpawnTarget(clickedCard.netIdentity);
                CmdSetSkillMode(SkillMode.None); // TakeAim เป็นสกิลเดียวที่ HandleClick ต้องสั่งปิดโหมดเอง
                break;

            case SkillMode.DoubleBarrel:
                CmdDoubleBarrelClick(clickedCard.netIdentity);
                // (CmdDoubleBarrelClick จะปิดโหมดเองเมื่อครบ)
                break;

            case SkillMode.QuickShot:
                CmdQuickShotCard(clickedCard.netIdentity);
                // (CmdQuickShotCard จะปิดโหมดเอง)
                break;

            case SkillMode.Misfire:
                CmdMisfireClick(clickedCard.netIdentity);
                // (CmdMisfireClick จะปิดโหมดเอง)
                break;

            case SkillMode.TwoBirds:
                CmdTwoBirdsClick(clickedCard.netIdentity);
                // (CmdTwoBirdsClick จะปิดโหมดเองเมื่อครบ)
                break;

            case SkillMode.BumpLeft:
                CmdBumpLeftClick(clickedCard.netIdentity);
                // (CmdBumpLeftClick จะปิดโหมดเอง)
                break;

            case SkillMode.BumpRight:
                CmdBumpRightClick(clickedCard.netIdentity);
                // (CmdBumpRightClick จะปิดโหมดเอง)
                break;

            case SkillMode.MoveAhead:
                CmdMoveAheadClick(clickedCard.netIdentity);
                // (CmdMoveAheadClick จะปิดโหมดเอง)
                break;

            case SkillMode.HangBack:
                CmdHangBackClick(clickedCard.netIdentity);
                // (CmdHangBackClick จะปิดโหมดเอง)
                break;

            case SkillMode.FastForward:
                CmdFastForwardClick(clickedCard.netIdentity);
                // (CmdFastForwardClick จะปิดโหมดเอง)
                break;

            case SkillMode.DisorderlyConduckt:
                CmdDisorderlyClick(clickedCard.netIdentity);
                // (DisorderlyConduckt จะคุม state 2-click เอง และไม่ปิดโหมด)
                break;

            // --- (เคสสำหรับสกิลที่ทำงานทันที) ---
            case SkillMode.LineForward:
            case SkillMode.DuckShuffle:
            case SkillMode.GivePeaceAChance:
            case SkillMode.Resurrection:
                // ไม่ควรเกิดเคสนี้ เพราะสกิลทำงานทันทีใน CmdSetSkillMode
                // แต่ใส่ไว้เผื่อกันเหนียว
                break;

            default:
                Debug.LogWarning($"Unhandled SkillMode in HandleDuckCardClick: {activeSkillMode}");
                break;
        }
    }









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
        // Debug.Log($"[Layout] localSeat={localSeat}, total={total}");
        // foreach (var pm in all) Debug.Log($" [Seat] netId={pm.netId} seat={pm.seatIndex} rel={((pm.seatIndex - localSeat + 6) % 6)}");
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




    // ====(ส่วน server helpers) 

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
        yield return new WaitForSeconds(3f); // รอ 3 วินาทีหลังเริ่มเกม

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

        var spawnedNi = spawnedCard.GetComponent<NetworkIdentity>();
        RpcShowCard(spawnedNi, "Dealt");
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

        if (card.scene.isLoaded)
        {
            // ---------------------------------------------------------
            // 1. (สำคัญ) อัปเดตสถานะ SyncVar บน Server ให้ถูกต้อง
            // ---------------------------------------------------------
            var duck = card.GetComponent<DuckCard>();
            if (duck != null)
            {
                Transform dropZoneT = GetSceneDropZone();
                int newCol = dropZoneT != null ? dropZoneT.childCount : 0;

                // คำสั่งนี้จะเปลี่ยน SyncVar -> Client ทุกคนจะย้าย Parent อัตโนมัติผ่าน Hook
                duck.ServerAssignToZone(ZoneKind.DropZone, 0, newCol);

                // ====================================================
                // 📝 LOG LOGIC: เช็กว่า Server เห็นการ์ดใน DropZone ครบไหม
                // ====================================================
                Debug.Log($"[Server-CmdPlayCard] 📥 Moving '{card.name}' to DropZone at index {newCol}");

                if (dropZoneT != null)
                {
                    string allCardsInDropZone = "";
                    int count = 0;
                    // วนลูปดูลูกทั้งหมดใน DropZone ของ Server
                    foreach (Transform child in dropZoneT)
                    {
                        allCardsInDropZone += $"[{count}] {child.name}, ";
                        count++;
                    }
                    Debug.Log($"[Server-CmdPlayCard] 🧐 Current DropZone Contents ({count} cards): {allCardsInDropZone}");
                }
                else
                {
                    Debug.LogError("[Server-CmdPlayCard] ❌ DropZone Transform is NULL on Server!");
                }
                // ====================================================
            }

            // ---------------------------------------------------------
            // 2. เรียก Rpc เพื่อจัดการ Logic พิเศษ (Flip, Activation)
            // ---------------------------------------------------------
            RpcShowCard(card.GetComponent<NetworkIdentity>(), "Played");
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


    // ========================================================
    // Helpers สำหรับ LineForward/DuckShuffle
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
                // (FIX: ใช้ .zone ตัวเล็ก)
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
            // (FIX: ใช้ .zone ตัวเล็ก)
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
            // (FIX: ใช้ .zone ตัวเล็ก)
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
        yield return null; // รอ layout

        List<DuckCard> ducks = FindDucksInRow(0); // หาเป็ดแถว 0
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
        // (FIX: ใช้วิธีนับที่ reliable และ CardPoolManager ที่ไม่อ้า
        int currentCount = Server_CountCardsInZone(ZoneKind.DuckZone);
        if (currentCount >= 6) return;
        if (!CardPoolManager.HasCards()) return;

        int needed = 6 - currentCount;
        for (int i = 0; i < needed; i++)
        {
            // (FIX: ใช้ DrawRandomCard() ที่ไม่ Obsolete)
            GameObject newCard = CardPoolManager.DrawRandomCard();
            if (newCard == null) break;

            DuckCard dc = newCard.GetComponent<DuckCard>();
            if (dc != null)
            {
                // (FIX: ใช้ .zone ตัวเล็ก)
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
    // TekeAim Logic (เก็บไว้เฉพาะที่จำเป็น)
    // ========================
    // ⛔️ (ลบ CmdActivateTekeAim, RpcEnableTekeAim, CmdDeactivateTekeAim, RpcDeactivateTekeAim)
    // ⛔️ (ลบ isTekeAimActive)

    // (CmdSpawnTarget ถูกเรียกจาก HandleDuckCardClick)
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
            marker.ServerAssignToZone(ZoneKind.TargetZone, 0, dc.ColNet);
            marker.FollowDuckNetId = duckCardIdentity.netId;
        }
        if (tf != null) tf.targetNetId = duckCardIdentity.netId;
        NetworkServer.Spawn(newTarget);
    }

    // (Helper นี้ยังจำเป็น)
    [ClientRpc]
    void RpcSetTargetNetId(NetworkIdentity targetIdentity, NetworkIdentity duckCardIdentity)
    {
        // (โค้ด RpcSetTargetNetId ของคุณ...)
        if (targetIdentity == null || duckCardIdentity == null) return;
        TargetFollow tf = targetIdentity.GetComponent<TargetFollow>();
        if (tf != null)
        {
            tf.targetNetId = duckCardIdentity.netId;
            tf.ResetTargetTransform();
        }
        // (โค้ด RectTransform... ของคุณ)
    }

    // ========================
    // Shoot Logic (เก็บไว้เฉพาะที่จำเป็น)
    // ========================
    // ⛔️ (ลบ CmdActivateShoot, RpcActivateShoot, CmdDeactivateShoot, RpcDeactivateShoot)
    // ⛔️ (ลบ isShootActive)

    // (CmdShootCard ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdShootCard(NetworkIdentity duckCardIdentity)
    {
        // if (!isShootActive) return; // (ไม่ต้องเช็ก bool)
        if (duckCardIdentity == null) return;
        var shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null) return;
        if (!IsCardTargeted(duckCardIdentity)) return;

        int shotRow = shotDuck.RowNet;
        int shotCol = shotDuck.ColNet;
        NetworkServer.Destroy(duckCardIdentity.gameObject);
        Server_DestroyAllTargetsFor(duckCardIdentity.netId);
        Server_ResequenceDuckZoneColumns();

        // FIX: ปิดโหมดโดยตรง
        activeSkillMode = SkillMode.None;

        StartCoroutine(RefillNextFrame());
    }

    [Server]
    IEnumerator RefillNextFrame()
    {
        yield return null;
        RefillDuckZoneIfNeeded();
    }

    // (Helper นี้ยังจำเป็น)
    bool IsCardTargeted(NetworkIdentity duckCardIdentity)
    {
        // (โค้ด IsCardTargeted ของคุณ...)
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
    // DoubleBarrel Logic (เก็บไว้เฉพาะที่จำเป็น)
    // ========================
    // ⛔️ (ลบ CmdActivateDoubleBarrel, RpcEnableDoubleBarrel, CmdDeactivateDoubleBarrel, RpcDisableDoubleBarrel)
    // ⛔️ (ลบ isDoubleBarrelActive)

    // (CmdDoubleBarrelClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdDoubleBarrelClick(NetworkIdentity clickedCard)
    {
        // if (!isDoubleBarrelActive) return; // (ไม่ต้องเช็ก bool)
        if (clickedCard == null) return;

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

            // FIX: ปิดโหมดโดยตรง
            activeSkillMode = SkillMode.None;
            doubleBarrelClickCount = 0;
            firstClickedCard = null;
        }
    }

    [Server]
    private void CmdSpawnTargetDoubleBarrel_Internal(NetworkIdentity duckCardIdentity)
    {
        // (โค้ด CmdSpawnTargetDoubleBarrel_Internal ของคุณ...)
        if (duckCardIdentity == null || targetPrefab == null) return;
        var dc = duckCardIdentity.GetComponent<DuckCard>();
        if (dc == null) return;
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
        // (โค้ด CheckAdjacent ของคุณ...)
        if (card1 == null || card2 == null) return false;
        var duck1 = card1.GetComponent<DuckCard>();
        var duck2 = card2.GetComponent<DuckCard>();
        if (duck1 == null || duck2 == null) return false;
        if (duck1.RowNet != duck2.RowNet) return false;
        int diff = Mathf.Abs(duck1.ColNet - duck2.ColNet);
        return diff == 1;
    }

    // ========================
    // Quick Shot Logic (เก็บไว้เฉพาะที่จำเป็น)
    // ========================
    // ⛔️ (ลบ CmdActivateQuickShot, RpcActivateQuickShot, CmdDeactivateQuickShot, RpcDeactivateQuickShot)
    // ⛔️ (ลบ isQuickShotActive)

    // (CmdQuickShotCard ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdQuickShotCard(NetworkIdentity duckCardIdentity)
    {
        // if (!isQuickShotActive) return; // (ไม่ต้องเช็ก bool)
        if (duckCardIdentity == null) return;
        DuckCard shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null) return;

        int shotRow = shotDuck.RowNet;
        int shotCol = shotDuck.ColNet;
        NetworkServer.Destroy(duckCardIdentity.gameObject);

        // (ทำลายเป้า)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckCardIdentity.netId)
                NetworkServer.Destroy(target.gameObject);
        }

        // (สมมติว่ามี ShiftColumnsDown)
        ShiftColumnsDown(shotRow, shotCol);

        // FIX: ปิดโหมดโดยตรง
        activeSkillMode = SkillMode.None;

        StartCoroutine(RefillNextFrame());
    }

    // ========================
    // Misfire Logic (เก็บไว้เฉพาะที่จำเป็น)
    // ========================
    // ⛔️ (ลบ CmdActivateMisfire, RpcEnableMisfire, CmdDeactivateMisfire, RpcDisableMisfire)
    // ⛔️ (ลบ isMisfireActive)

    // (CmdMisfireClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdMisfireClick(NetworkIdentity clickedCard)
    {
        // if (!isMisfireActive) return; // (ไม่ต้องเช็ก bool)
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        DuckCard duckComp = clickedCard.GetComponent<DuckCard>();
        if (duckComp == null) return;

        int row = duckComp.RowNet;
        int col = duckComp.ColNet;
        List<NetworkIdentity> neighbors = GetAdjacentDuckCards(row, col);
        if (neighbors.Count == 0) return;

        var randomNeighbor = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
        ShootCardDirect(randomNeighbor); // (ใช้ Helper ยิง)

        // (ทำลายเป้าเดิม)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var t in allTargets)
        {
            if (t.targetNetId == clickedCard.netId)
                NetworkServer.Destroy(t.gameObject);
        }

        // FIX: ปิดโหมดโดยตรง
        activeSkillMode = SkillMode.None;

        StartCoroutine(RefillNextFrame());
    }

    private List<NetworkIdentity> GetAdjacentDuckCards(int row, int col)
    {
        // (โค้ด GetAdjacentDuckCards ของคุณ... แต่ควรแก้ให้วน NetworkServer.spawned)
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
        // (โค้ด ShootCardDirect ของคุณ...)
        if (duckNi == null) return;
        NetworkServer.Destroy(duckNi.gameObject);
        // (ทำลายเป้า)
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckNi.netId)
                NetworkServer.Destroy(target.gameObject);
        }
        DuckCard dc = duckNi.GetComponent<DuckCard>();
        if (dc != null)
        {
            ShiftColumnsDown(dc.RowNet, dc.ColNet); // (สมมติว่ามี Helper นี้)
        }
    }

    // ========================
    // TwoBirds Logic (Refactored)
    // ========================
    // ⛔️ (ลบ CmdActivateTwoBirds, RpcEnableTwoBirds, CmdDeactivateTwoBirds, RpcDisableTwoBirds)
    // ⛔️ (ลบ isTwoBirdsActive)

    // (CmdTwoBirdsClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdTwoBirdsClick(NetworkIdentity clickedCard)
    {
        // if (!isTwoBirdsActive) return; // (ไม่ต้องเช็ก bool)
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;

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
                    }
                }
            }

            // FIX: ปิดโหมดโดยตรง
            activeSkillMode = SkillMode.None;
            twoBirdsClickCount = 0;
            firstTwoBirdsCard = null;
        }
    }

    [Server]
    private bool CheckAdjacentTwoBirds(NetworkIdentity card1, NetworkIdentity card2)
    {
        // (โค้ด CheckAdjacentTwoBirds ที่แก้แล้ว...)
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
        // (โค้ด RemoveTargetFromCard ที่แก้แล้ว...)
        if (duckNi == null) return;
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == duckNi.netId)
            {
                NetworkServer.Destroy(tf.gameObject);
                return;
            }
        }
    }

    // ========================
    // BumpLeft Logic (Refactored)
    // ========================
    // ⛔️ (ลบ CmdActivateBumpLeft, RpcEnableBumpLeft, CmdDeactivateBumpLeft, RpcDisableBumpLeft)
    // ⛔️ (ลบ isBumpLeftActive)

    // (CmdBumpLeftClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdBumpLeftClick(NetworkIdentity clickedCard)
    {
        // if (!isBumpLeftActive) return; // (ไม่ต้องเช็ก bool)
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        DuckCard duck = clickedCard.GetComponent<DuckCard>();
        if (duck == null) return;
        int curRow = duck.RowNet, curCol = duck.ColNet;
        DuckCard leftDuck = FindDuckAt(curRow, curCol - 1);
        if (leftDuck == null) return;
        MoveTargetFromTo(clickedCard, leftDuck.GetComponent<NetworkIdentity>());

        // FIX: ปิดโหมดโดยตรง
        activeSkillMode = SkillMode.None;
    }

    // ========================
    // BumpRight Logic (Refactored)
    // ========================
    // ⛔️ (ลบ CmdActivateBumpRight, RpcEnableBumpRight, CmdDeactivateBumpRight, RpcDisableBumpRight)
    // ⛔️ (ลบ isBumpRightActive)

    // (CmdBumpRightClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdBumpRightClick(NetworkIdentity clickedCard)
    {
        // if (!isBumpRightActive) return; // (ไม่ต้องเช็ก bool)
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        DuckCard duck = clickedCard.GetComponent<DuckCard>();
        if (duck == null) return;
        int curRow = duck.RowNet, curCol = duck.ColNet;
        DuckCard rightDuck = FindDuckAt(curRow, curCol + 1);
        if (rightDuck == null) return;
        MoveTargetFromTo(clickedCard, rightDuck.GetComponent<NetworkIdentity>());

        // FIX: ปิดโหมดโดยตรง
        activeSkillMode = SkillMode.None;
    }

    [Server]
    private void MoveTargetFromTo(NetworkIdentity fromCard, NetworkIdentity toCard)
    {
        // (โค้ด MoveTargetFromTo ที่แก้แล้ว...)
        if (fromCard == null || toCard == null) return;
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (tf.targetNetId == fromCard.netId)
            {
                tf.targetNetId = toCard.netId; // (สมมติ targetNetId เป็น SyncVar)
                return;
            }
        }
    }

    [Server]
    private DuckCard FindDuckAt(int row, int col)
    {
        // (โค้ด FindDuckAt ที่แก้แล้ว...)
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
    // ⛔️ (ลบ CmdDeactivateLineForward, RpcDisableLineForward)
    // ⛔️ (ลบ isLineForwardActive)

    // (TryLineForward เรียก CmdSetSkillMode(SkillMode.LineForward))
    // (CmdSetSkillMode จะเรียก CmdActivateLineForward)
    [Command]
    public void CmdActivateLineForward()
    {
        // (โค้ด CmdActivateLineForward ที่แก้แล้ว...)
        var oldTargets = CollectTargetColumns();
        var leftmost = FindLeftmostDuck(0);
        if (leftmost != null)
            NetworkServer.Destroy(leftmost.gameObject);
        RemoveAllTargets();
        StartCoroutine(RefillAndRecreateTargets(oldTargets));
        StartCoroutine(DelayedLog());
        // (CmdSetSkillMode จะปิดโหมดเอง)
    }

    // (Helpers: DelayedLog, CollectTargetColumns, FindLeftmostDuck, RemoveAllTargets, FindDucksInRow, RefillAndRecreateTargets, ... ทั้งหมดอยู่ที่นี่)
    // ( ... โค้ด Helpers ที่แก้แล้วทั้งหมด ... )
    // ... (ละไว้เพื่อความกระชับ แต่ต้องใส่โค้ดที่แก้แล้วทั้งหมด) ...


    // ========================
    // Move Ahead Logic (Refactored)
    // ========================
    // ⛔️ (ลบ CmdActivateMoveAhead, RpcEnableMoveAhead, CmdDeactivateMoveAhead, RpcDisableMoveAhead)
    // ⛔️ (ลบ isMoveAheadActive)

    // (CmdMoveAheadClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdMoveAheadClick(NetworkIdentity clickedCard)
    {
        // (โค้ด CmdMoveAheadClick ที่แก้แล้ว...)
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

        // FIX: ปิดโหมดโดยตรง
        activeSkillMode = SkillMode.None;
    }


    // ========================
    // HangBack Logic (Refactored)
    // ========================
    // ⛔️ (ลบ CmdActivateHangBack, RpcEnableHangBack, CmdDeactivateHangBack, RpcDisableHangBack)
    // ⛔️ (ลบ isHangBackActive)

    // (CmdHangBackClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdHangBackClick(NetworkIdentity clickedCard)
    {
        // (โค้ด CmdHangBackClick ที่แก้แล้ว...)
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

        // FIX: ปิดโหมดโดยตรง
        activeSkillMode = SkillMode.None;
    }


    // ========================
    // FastForward Logic (Refactored)
    // ========================
    // ⛔️ (ลบ CmdActivateFastForward, RpcEnableFastForward, CmdDeactivateFastForward, RpcDisableFastForward)
    // ⛔️ (ลบ isFastForwardActive)

    // (CmdFastForwardClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdFastForwardClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;
        StartCoroutine(FastForwardCoroutine(selectedDuck));

        // FIX: ปิดโหมดโดยตรง
        activeSkillMode = SkillMode.None;
    }

    [Server]
    private IEnumerator FastForwardCoroutine(DuckCard selectedDuck)
    {
        // (โค้ด FastForwardCoroutine ที่แก้แล้ว...)
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
        // (ปิดโหมดใน CmdFastForwardClick ไปแล้ว)
    }

    [Server]
    private DuckCard FindDuckByNetId(uint netId)
    {
        // (โค้ด FindDuckByNetId ที่แก้แล้ว...)
        if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity ni))
            return ni.GetComponent<DuckCard>();
        return null;
    }


    // ========================
    // Disorderly Conduckt Logic (Refactored)
    // ========================
    // ⛔️ (ลบ CmdActivateDisorderlyConduckt, RpcEnableDisorderlyConduckt, CmdDeactivateDisorderlyConduckt, RpcDisableDisorderlyConduckt)
    // ⛔️ (ลบ isDisorderlyConducktActive)

    // (CmdDisorderlyClick ถูกเรียกจาก HandleDuckCardClick)
    [Command(requiresAuthority = false)]
    public void CmdDisorderlyClick(NetworkIdentity clickedCard)
    {
        // (โค้ด CmdDisorderlyClick ที่แก้แล้ว...)
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
        // (โหมดนี้อาจจะอยากให้ Active ค้างไว้ ไม่ต้องปิด)
        // activeSkillMode = SkillMode.None; 
    }

    [Command(requiresAuthority = false)]
    private void CmdSpawnTargetForDuck(uint duckNetId)
    {
        // (โค้ด CmdSpawnTargetForDuck ที่แก้แล้ว...)
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
    // ⛔️ (ลบ CmdDeactivateDuckShuffle, RpcDisableDuckShuffle)
    // ⛔️ (ลบ isDuckShuffleActive)

    // (TryDuckShuffle เรียก CmdSetSkillMode(SkillMode.DuckShuffle))
    // (CmdSetSkillMode จะเรียก CmdActivateDuckShuffle)
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

            // (FIX) เรียก DrawRandomCard() ที่ไม่ Obsolete
            GameObject cardGO = CardPoolManager.DrawRandomCard();
            if (cardGO == null) break;

            // (FIX) ใช้วิธีที่ถูกต้องในการกำหนด Zone/ตำแหน่ง
            var duck = cardGO.GetComponent<DuckCard>();
            if (duck != null)
            {
                // ฟังก์ชันนี้จะเซ็ต zone, RowNet, ColNet ให้เอง
                duck.ServerAssignToZone(ZoneKind.DuckZone, 0, i);
            }

            NetworkServer.Spawn(cardGO);

            // (FIX) ลบ RpcAddCardToDuckZone(cardGO) ทิ้ง
        }

        StartCoroutine(RecreateTargetsAfterShuffle(oldTargets));
        StartCoroutine(DelayedLog());
    }

    [Server]
    private IEnumerator RecreateTargetsAfterShuffle(List<int> oldCols)
    {
        // (โค้ด RecreateTargetsAfterShuffle ที่แก้แล้ว...)
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
        // (โค้ด RemoveAllDucks ที่แก้แล้ว...)
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
    }


    // ========================
    // GivePeaceAChance Logic
    // ========================
    // ⛔️ (ลบ CmdDeactivateGivePeaceAChance, RpcDisableGivePeaceAChance)
    // ⛔️ (ลบ isGivePeaceActive)

    // (TryGivePeaceAChance เรียก CmdSetSkillMode(SkillMode.GivePeaceAChance))
    // (CmdSetSkillMode จะเรียก CmdActivateGivePeaceAChance)
    [Command(requiresAuthority = false)]
    private void CmdActivateGivePeaceAChance()
    {
        RemoveAllTargets();
        // (CmdSetSkillMode จะปิดโหมดเอง)
    }

    // ========================
    // Resurrection  Logic (Refactored)
    // ========================
    // ⛔️ (ลบ CmdDeactivateResurrectionMode, RpcDisableResurrectionMode)
    // ⛔️ (ลบ isResurrectionModeActive)

    // (TryUseResurrection เรียก CmdSetSkillMode(SkillMode.Resurrection))
    // (CmdSetSkillMode จะเรียก CmdActivateResurrectionMode)
    [Command]
    private void CmdActivateResurrectionMode()
    {
        const int maxPerColor = 5;

        // 1. (FIX) เรียก GetTotalDuckCounts (ตัวที่เราเพิ่งแก้)
        var totalCounts = GetTotalDuckCounts();
        var lowColors = new List<string>();

        // 2. (FIX) วนลูปจาก Key ที่ได้มา (ไม่ใช่จากฟังก์ชันที่ไม่มี)
        foreach (string color in totalCounts.Keys)
        {
            // (กันไม่ให้คืนชีพ Marsh)
            if (color == "Marsh") continue;

            int currentCount = totalCounts.GetValueOrDefault(color, 0);
            if (currentCount < maxPerColor)
                lowColors.Add(color);
        }

        if (lowColors.Count > 0)
        {
            int idx = Random.Range(0, lowColors.Count);
            string color = lowColors[idx];

            // 3. (อันนี้ถูกแล้ว)
            CardPoolManager.AddToPool(color);
        }

    }

    [Server]
    private Dictionary<string, int> GetTotalDuckCounts()
    {
        // 1. (FIX) ใช้ชื่อฟังก์ชันที่ถูกต้อง (GetAllPoolCounts)
        Dictionary<string, int> counts = CardPoolManager.GetAllPoolCounts();

        // 2. วนหาเป็ดใน DuckZone
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();

            // (FIX) ใช้ .zone (ตัวเล็ก) และใช้ Helper 'ExtractDuckKeyFromCard' (ที่คุณมีอยู่แล้ว)
            if (card != null && card.zone == ZoneKind.DuckZone)
            {
                string key = ExtractDuckKeyFromCard(card.gameObject); // (ใช้ฟังก์ชันที่คุณมี)
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
    [ClientRpc]
    // FIX 1: ClientRpc ต้องรับ NetworkIdentity
    void RpcShowCard(NetworkIdentity cardIdentity, string type)
    {
        if (cardIdentity == null)
        {
            Debug.LogError("[RpcShowCard] cardIdentity is null!");
            return;
        }
        // Debug.Logโค้ดสำหรับดีบัก
        Debug.Log($"[RpcShowCard] called for {cardIdentity.netId} type={type} isOwned={cardIdentity.isOwned}");
        GameObject card = cardIdentity.gameObject;

        if (type == "Dealt")
        {
            if (cardIdentity.isOwned && PlayerArea != null)
                card.transform.SetParent(PlayerArea.transform, false);
            else if (EnemyArea != null)
            {
                card.transform.SetParent(EnemyArea.transform, false);
                card.GetComponent<CardFlipper>()?.Flip();
            }
        }
        else if (type == "Played")
        {
            if (DropZone != null)
            {
                Debug.Log($"[RpcShowCard] setting parent to DropZone for {card.name}");
                card.transform.SetParent(DropZone.transform, false);
            }

            // บังคับให้การ์ดเปิดและรีคัลคูล UI
            card.SetActive(true);
            Canvas.ForceUpdateCanvases();

            var dropZone = FindObjectOfType<DropZone>();
            if (dropZone != null)
                dropZone.PlaceCard(card);

            if (!cardIdentity.isOwned)
                card.GetComponent<CardFlipper>()?.Flip();

            // FIX 2: ถ้าเราเป็นคนเล่นการ์ดใบนี้ ให้เรียก HandleCardActivation
            if (isLocalPlayer && cardIdentity.isOwned)
            {
                HandleCardActivation(card);
            }
        }
    }

    // (ฟังก์ชันนี้ทำงานบน Client ของคนที่เล่นการ์ด)
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
            // ส่ง Command เปลี่ยน State ไปที่ Server
            CmdSetSkillMode(selectedSkill);
        }
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

