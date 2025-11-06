using System.Collections.Generic;
using TMPro;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization; // ✅ ใช้สำหรับ [FormerlySerializedAs]

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [SerializeField] private Transform playerSingleTemplate;
    [SerializeField] private Transform container;
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI gameModeText;

    // ✅ ปุ่มเลือกเป็ด 6 สี
    [Header("Duck Color Buttons")]
    [SerializeField] private Button duckBlueBtn;   // index 0
    [SerializeField] private Button duckOrangeBtn; // index 1
    [SerializeField] private Button duckPinkBtn;   // index 2
    [SerializeField] private Button duckGreenBtn;  // index 3
    [SerializeField] private Button duckYellowBtn; // index 4
    [SerializeField] private Button duckPurpleBtn; // index 5

    [Header("Primary Action Button (Start/Ready)")]
    [FormerlySerializedAs("changeGameModeButton")]
    [SerializeField] private Button startReadyButton;              // ← เปลี่ยนชื่อฟิลด์
    [SerializeField] private TextMeshProUGUI startReadyLabelText;  // ← ข้อความบนปุ่ม

    [SerializeField] private Button leaveLobbyButton;

    LobbyNetworkManager manager;
    Button[] _colorBtns;

    void Awake()
    {
        Instance = this;
        manager = NetworkManager.singleton as LobbyNetworkManager;

        if (playerSingleTemplate) playerSingleTemplate.gameObject.SetActive(false);

        // ผูกปุ่มเลือกสี
        if (duckBlueBtn) duckBlueBtn.onClick.AddListener(() => LobbyManager.Instance.UpdateDuckColor(LobbyManager.DuckColor.Blue));
        if (duckOrangeBtn) duckOrangeBtn.onClick.AddListener(() => LobbyManager.Instance.UpdateDuckColor(LobbyManager.DuckColor.Orange));
        if (duckPinkBtn) duckPinkBtn.onClick.AddListener(() => LobbyManager.Instance.UpdateDuckColor(LobbyManager.DuckColor.Pink));
        if (duckGreenBtn) duckGreenBtn.onClick.AddListener(() => LobbyManager.Instance.UpdateDuckColor(LobbyManager.DuckColor.Green));
        if (duckYellowBtn) duckYellowBtn.onClick.AddListener(() => LobbyManager.Instance.UpdateDuckColor(LobbyManager.DuckColor.Yellow));
        if (duckPurpleBtn) duckPurpleBtn.onClick.AddListener(() => LobbyManager.Instance.UpdateDuckColor(LobbyManager.DuckColor.Purple));

        if (leaveLobbyButton) leaveLobbyButton.onClick.AddListener(() => LobbyManager.Instance.LeaveLobby());

        // ปุ่มหลัก: เปลี่ยนบทบาทตาม host/client
        if (startReadyButton) startReadyButton.onClick.AddListener(OnClickPrimaryAction);

        _colorBtns = new Button[6] { duckBlueBtn, duckOrangeBtn, duckPinkBtn, duckGreenBtn, duckYellowBtn, duckPurpleBtn };
    }

    void OnEnable() => InvokeRepeating(nameof(RefreshAll), 0f, 0.25f);
    void OnDisable() => CancelInvoke(nameof(RefreshAll));

    void RefreshAll()
    {
        manager = manager ?? (NetworkManager.singleton as LobbyNetworkManager);
        RefreshHeader();
        RefreshPlayers();
        RefreshColorLocks();
        RefreshPrimaryActionButton(); // ← อัปเดตปุ่ม Start/Ready
    }

    void RefreshHeader()
    {
        if (lobbyNameText) lobbyNameText.text = string.IsNullOrWhiteSpace(LobbyManager.Instance?.CurrentLobbyName)
            ? "Lobby" : LobbyManager.Instance.CurrentLobbyName;

        if (gameModeText) gameModeText.text = LobbyManager.Instance
            ? LobbyManager.Instance.CurrentGameMode.ToString() : "-";

        if (playerCountText)
        {
            int cur = 0, max = 0;

            if (NetworkServer.active)
            {
                cur = manager ? manager.numPlayers : 0;
                max = manager ? manager.maxConnections : 0;
            }
            else
            {
                var players = GameObject.FindObjectsOfType<LobbyRoomPlayer>();
                cur = players != null ? players.Length : 0;

                if (LobbyManager.Instance && LobbyManager.Instance.LastKnownMaxPlayers > 0)
                    max = LobbyManager.Instance.LastKnownMaxPlayers;
                else
                    max = manager ? manager.maxConnections : 0;
            }

            playerCountText.text = $"{cur}/{max}";
        }
    }

    // ปุ่มหลัก: Host = Start Game, Client = Ready/Unready
    void RefreshPrimaryActionButton()
    {
        if (!startReadyButton) return;

        bool isHost = LobbyManager.Instance && LobbyManager.Instance.IsLobbyHost();

        if (isHost)
        {
            if (startReadyLabelText) startReadyLabelText.text = "Start Game";

            string reason;
            bool canStart = manager && manager.CanStartGameNow(out reason);
            startReadyButton.interactable = canStart;
            // ถ้าอยากแสดง reason บน UI เพิ่มเติม สามารถต่อข้อความไว้ใต้ปุ่มได้
        }
        else
        {
            bool isReady = LobbyRoomPlayer.Local ? LobbyRoomPlayer.Local.readyToBegin : false;
            if (startReadyLabelText) startReadyLabelText.text = isReady ? "Unready" : "Ready";
            // เปิดปุ่มได้เมื่อมี Local และเป็นเจ้าของจริง
            startReadyButton.interactable = LobbyRoomPlayer.Local &&
                                            LobbyRoomPlayer.Local.netIdentity &&
                                            LobbyRoomPlayer.Local.netIdentity.isOwned;
        }
    }

    void OnClickPrimaryAction()
    {
        bool isHost = LobbyManager.Instance && LobbyManager.Instance.IsLobbyHost();

        if (isHost)
        {
            if (manager)
            {
                if (manager.CanStartGameNow(out var reason))
                    manager.StartGameIfReady();
                else
                    Debug.Log($"[Lobby] เริ่มเกมไม่ได้: {reason}");
            }
        }
        else
        {
            var me = LobbyRoomPlayer.Local;
            if (me && me.netIdentity && me.netIdentity.isOwned)
                me.ClientToggleReady();
            else
                Debug.LogWarning("[Lobby] Local RoomPlayer ยังไม่เป็นเจ้าของ (isOwned=false) หรือยังไม่พร้อมใช้งาน");
        }

    }


    void RefreshPlayers()
    {
        if (!manager || !container || !playerSingleTemplate) return;

        foreach (Transform child in container)
            if (child != playerSingleTemplate) Destroy(child.gameObject);

        bool isHost = LobbyManager.Instance && LobbyManager.Instance.IsLobbyHost();

        foreach (var slot in manager.roomSlots)
        {
            if (!slot) continue;

            var t = Instantiate(playerSingleTemplate, container);
            t.gameObject.SetActive(true);

            var ui = t.GetComponent<LobbyPlayerSingleUI>();
            var lp = slot.GetComponent<LobbyRoomPlayer>();
            if (ui) ui.UpdatePlayer(lp);

            bool isSelf = lp && lp.isLocalPlayer;
            if (ui) ui.SetKickPlayerButtonVisible(isHost && !isSelf);
        }
    }

    // ====== ล็อก/ปลดล็อกปุ่มสีตามสีที่ถูกใช้จริงในห้อง ======
    public void RefreshColorLocks()
    {
        if (_colorBtns == null || _colorBtns.Length != 6)
        {
            _colorBtns = new Button[6] { duckBlueBtn, duckOrangeBtn, duckPinkBtn, duckGreenBtn, duckYellowBtn, duckPurpleBtn };
        }

        bool[] used = new bool[6];
        var all = GameObject.FindObjectsOfType<LobbyRoomPlayer>();
        foreach (var p in all)
        {
            if (!p) continue;
            int idx = p.duckColorIndex;
            if (idx >= 0 && idx < 6) used[idx] = true;
        }

        int myIdx = LobbyRoomPlayer.Local ? LobbyRoomPlayer.Local.duckColorIndex : -1;

        for (int i = 0; i < 6; i++)
        {
            var btn = _colorBtns[i];
            if (!btn) continue;

            bool takenByOther = used[i] && i != myIdx;
            btn.interactable = !takenByOther && i != myIdx;
        }
    }
}
