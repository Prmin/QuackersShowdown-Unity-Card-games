using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mirror; // ✅ ใช้เช็คสถานะ NetworkClient/Server

public class LobbyCreateUI : MonoBehaviour
{
    public static LobbyCreateUI Instance { get; private set; }

    [SerializeField] private Button createButton;
    [SerializeField] private Button lobbyNameButton;
    [SerializeField] private Button publicPrivateButton;

    // ⬇️ Dropdown เลือกจำนวนผู้เล่น 2..6
    [Header("Max Players (2..6)")]
    [SerializeField] private TMP_Dropdown maxPlayersDropdown;

    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI publicPrivateText;
    [SerializeField] private TextMeshProUGUI maxPlayersText;

    // ✅ ปุ่ม Leave (เพิ่มใหม่)
    [SerializeField] private Button leaveLobbyButton;

    private string lobbyName;
    private bool isPrivate;
    private int maxPlayers;

    // รหัสห้องสำหรับ Private
    private string lobbyPassword = "";

    private void Awake()
    {
        Instance = this;

        // ปุ่มสร้าง
        createButton.onClick.AddListener(() =>
        {
            // ตั้ง privacy ก่อน
            LobbyManager.Instance.SetLobbyPrivacy(isPrivate, isPrivate ? lobbyPassword : "");

            // โหมดเกมใช้ค่าปัจจุบันจาก LobbyManager (หน้านี้ไม่มีให้เลือก)
            var mode = LobbyManager.Instance ? LobbyManager.Instance.CurrentGameMode : LobbyManager.GameMode.CaptureTheFlag;

            if (isPrivate && string.IsNullOrEmpty(lobbyPassword))
            {
                UI_InputWindow.Show_Static(
                    "Set Password", "",
                    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*_-", 20,
                    () => { /* cancel */ },
                    (string value) =>
                    {
                        lobbyPassword = value ?? "";
                        LobbyManager.Instance.SetLobbyPrivacy(true, lobbyPassword);
                        LobbyManager.Instance.CreateLobby(lobbyName, maxPlayers, true, mode);
                    });
            }
            else
            {
                LobbyManager.Instance.CreateLobby(lobbyName, maxPlayers, isPrivate, mode);
            }
        });

        // ตั้งชื่อห้อง
        lobbyNameButton.onClick.AddListener(() =>
        {
            UI_InputWindow.Show_Static("Lobby Name", lobbyName,
                "abcdefghijklmnopqrstuvxywzABCDEFGHIJKLMNOPQRSTUVXYWZ .-", 20,
                () => { },
                (string value) => { lobbyName = value; UpdateText(); });
        });

        // Public / Private
        publicPrivateButton.onClick.AddListener(() =>
        {
            isPrivate = !isPrivate;
            UpdateText();

            // เพิ่งสลับเป็น Private → ยังไม่มีรหัส ก็ถามไว้ล่วงหน้า (ไม่บังคับ)
            if (isPrivate && string.IsNullOrEmpty(lobbyPassword))
            {
                UI_InputWindow.Show_Static(
                    "Set Password", "",
                    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*_-", 20,
                    () => { },
                    (string value) => { lobbyPassword = value ?? ""; });
            }
        });

        // เตรียม Dropdown เลข 2..6
        SetupMaxPlayersDropdown();

        // ✅ ปุ่ม Leave: ถ้าอยู่ในเซสชันให้ LeaveLobby(), ถ้าไม่ได้อยู่ให้กลับ LobbyList
        if (leaveLobbyButton)
        {
            leaveLobbyButton.onClick.AddListener(() =>
            {
                if (NetworkServer.active || NetworkClient.active)
                    LobbyManager.Instance.LeaveLobby();
                else
                    UIFlow.I?.ShowLobbyList();
            });
        }
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(lobbyName)) lobbyName = "MyLobby";
        if (maxPlayers <= 0) maxPlayers = 3;

        // sync ค่าเริ่มต้นไปยัง dropdown
        if (maxPlayersDropdown)
        {
            int idx = Mathf.Clamp(maxPlayers - 2, 0, 4); // 2..6 -> index 0..4
            maxPlayersDropdown.SetValueWithoutNotify(idx);
        }

        UpdateText();
    }

    private void SetupMaxPlayersDropdown()
    {
        if (!maxPlayersDropdown) return;

        maxPlayersDropdown.ClearOptions();
        var opts = new System.Collections.Generic.List<string>();
        for (int p = 2; p <= 6; p++) opts.Add(p.ToString());
        maxPlayersDropdown.AddOptions(opts);

        // ค่าเปลี่ยน → อัปเดต maxPlayers + ข้อความ
        maxPlayersDropdown.onValueChanged.RemoveAllListeners();
        maxPlayersDropdown.onValueChanged.AddListener(idx =>
        {
            maxPlayers = Mathf.Clamp(idx + 2, 2, 6);
            UpdateText();
        });
    }

    private void UpdateText()
    {
        if (lobbyNameText) lobbyNameText.text = lobbyName;
        if (publicPrivateText) publicPrivateText.text = isPrivate ? "Private" : "Public";
        if (maxPlayersText) maxPlayersText.text = (maxPlayers > 0 ? maxPlayers : 3).ToString();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (string.IsNullOrEmpty(lobbyName)) lobbyName = "MyLobby";
        if (maxPlayers <= 0) maxPlayers = 3;

        // sync ค่า dropdown เมื่อเปิดผ่าน Show()
        if (maxPlayersDropdown)
        {
            int idx = Mathf.Clamp(maxPlayers - 2, 0, 4);
            maxPlayersDropdown.SetValueWithoutNotify(idx);
        }

        UpdateText();
    }
}
