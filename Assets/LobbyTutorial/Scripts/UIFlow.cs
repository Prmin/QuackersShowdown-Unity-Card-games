using UnityEngine;

public class UIFlow : MonoBehaviour
{
    public static UIFlow I { get; private set; }

    [Header("Screens")]
    public GameObject authenticatePanel;  // ใส่ชื่อ → ปุ่มยืนยัน
    public GameObject lobbyListPanel;     // รายการห้อง
    public GameObject lobbyCreatePanel;   // สร้างห้อง
    public GameObject lobbyPanel;         // ในห้อง (รอเริ่มเกม)

    [Header("Extra Panels")]
    public GameObject editPlayerNamePanel; // ← กล่องแก้ชื่อ (ต้องโชว์ตลอดจนกว่าจะเริ่มเกม)

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        EnsureRefs();
    }

    void Start()
    {
        bool hasName = !string.IsNullOrWhiteSpace(
            PlayerPrefs.GetString(LobbyManager.KEY_PLAYER_NAME, "")
        );
        if (hasName) ShowLobbyList();
        else ShowAuthenticate();
    }

    // หา reference อัตโนมัติถ้ายังไม่ได้ลาก (กันพลาด)
    void EnsureRefs()
    {
        if (editPlayerNamePanel == null)
        {
            var ep = FindObjectOfType<EditPlayerName>(true);
            if (ep) editPlayerNamePanel = ep.gameObject;
        }
    }

    // แสดง overlay (เช่น EditPlayerName) สำหรับช่วงเมนู
    void ShowOverlaysForMenus()
    {
        EnsureRefs();
        if (editPlayerNamePanel) editPlayerNamePanel.SetActive(true);
    }

    // ปิดทุกอย่างตอนเข้า Gameplay
    public void HideAllForGameplay()
    {
        EnsureRefs();
        if (authenticatePanel) authenticatePanel.SetActive(false);
        if (lobbyListPanel) lobbyListPanel.SetActive(false);
        if (lobbyCreatePanel) lobbyCreatePanel.SetActive(false);
        if (lobbyPanel) lobbyPanel.SetActive(false);
        if (editPlayerNamePanel) editPlayerNamePanel.SetActive(false); // ✅ ปิดเฉพาะตอนจะเข้าเกม
    }

    // ซ่อนเฉพาะสกรีนหลัก ไม่ยุ่ง overlay
    void ShowOnly(GameObject target)
    {
        if (authenticatePanel) authenticatePanel.SetActive(false);
        if (lobbyListPanel) lobbyListPanel.SetActive(false);
        if (lobbyCreatePanel) lobbyCreatePanel.SetActive(false);
        if (lobbyPanel) lobbyPanel.SetActive(false);

        if (target) target.SetActive(true);

        // ✅ ทุกหน้าช่วงเมนู เปิด overlay กลับมาเสมอ
        ShowOverlaysForMenus();
    }

    public void ShowAuthenticate()
    {
        ShowOnly(authenticatePanel);
        DiscoveryBridge.I?.StopClientScan();
    }

    public void ShowLobbyList()
    {
        ShowOnly(lobbyListPanel);
        DiscoveryBridge.I?.StartClientScan();
    }

    public void ShowLobbyCreate()
    {
        ShowOnly(lobbyCreatePanel);
        DiscoveryBridge.I?.StopClientScan();
    }

    public void ShowLobby()
    {
        ShowOnly(lobbyPanel);
        DiscoveryBridge.I?.StopClientScan();
    }
}
