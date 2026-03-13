using UnityEngine;
using UnityEngine.SceneManagement;

public class UIFlow : MonoBehaviour
{
    public static UIFlow I { get; private set; }
    [SerializeField] private string lobbySceneName = "LobbyTutorial_Done";

    [Header("Screens")]
    public GameObject authenticatePanel;  // ใส่ชื่อ → ปุ่มยืนยัน
    public GameObject lobbyListPanel;     // รายการห้อง
    public GameObject lobbyCreatePanel;   // สร้างห้อง
    public GameObject lobbyPanel;         // ในห้อง (รอเริ่มเกม)

    [Header("Extra Panels")]
    public GameObject editPlayerNamePanel; // ← กล่องแก้ชื่อ (ต้องโชว์ตลอดจนกว่าจะเริ่มเกม)

    void Awake()
    {
        if (I && I != this)
        {
            I.CopyRefsFrom(this);
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureRefs();
    }

    private void OnDestroy()
    {
        if (I == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        UIAudioSfx.RefreshMusicStateFromPrefs();

        bool hasName = !string.IsNullOrWhiteSpace(
            PlayerPrefs.GetString(LobbyManager.KEY_PLAYER_NAME, "")
        );
        if (hasName) ShowLobbyList();
        else ShowAuthenticate();
    }

    // หา reference อัตโนมัติถ้ายังไม่ได้ลาก (กันพลาด)
    void EnsureRefs()
    {
        if (authenticatePanel == null)
        {
            var auth = FindObjectOfType<AuthenticateUI>(true);
            if (auth) authenticatePanel = auth.gameObject;
        }

        if (lobbyListPanel == null)
        {
            var list = FindObjectOfType<LobbyListUI>(true);
            if (list) lobbyListPanel = list.gameObject;
        }

        if (lobbyCreatePanel == null)
        {
            var create = FindObjectOfType<LobbyCreateUI>(true);
            if (create) lobbyCreatePanel = create.gameObject;
        }

        if (lobbyPanel == null)
        {
            var lobby = FindObjectOfType<LobbyUI>(true);
            if (lobby) lobbyPanel = lobby.gameObject;
        }

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
        EnsureRefs();

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
        EnsureRefs();
        ShowOnly(authenticatePanel);
        DiscoveryBridge.I?.StopClientScan();
    }

    public void ShowLobbyList()
    {
        EnsureRefs();
        ShowOnly(lobbyListPanel);
        DiscoveryBridge.I?.StartClientScan();
    }

    public void ShowLobbyCreate()
    {
        EnsureRefs();
        ShowOnly(lobbyCreatePanel);
        DiscoveryBridge.I?.StopClientScan();
    }

    public void ShowLobby()
    {
        EnsureRefs();
        ShowOnly(lobbyPanel);
        DiscoveryBridge.I?.StopClientScan();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsLobbyScene(scene))
            return;

        UIAudioSfx.RefreshMusicStateFromPrefs();

        ClearDestroyedRefs();
        EnsureRefs();

        bool hasName = !string.IsNullOrWhiteSpace(
            PlayerPrefs.GetString(LobbyManager.KEY_PLAYER_NAME, "")
        );
        if (hasName) ShowLobbyList();
        else ShowAuthenticate();
    }

    private bool IsLobbyScene(Scene scene)
    {
        if (string.IsNullOrWhiteSpace(lobbySceneName))
            return false;

        if (string.Equals(scene.name, lobbySceneName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        string byPath = System.IO.Path.GetFileNameWithoutExtension(lobbySceneName);
        return string.Equals(scene.name, byPath, System.StringComparison.OrdinalIgnoreCase);
    }

    private void ClearDestroyedRefs()
    {
        if (authenticatePanel == null) authenticatePanel = null;
        if (lobbyListPanel == null) lobbyListPanel = null;
        if (lobbyCreatePanel == null) lobbyCreatePanel = null;
        if (lobbyPanel == null) lobbyPanel = null;
        if (editPlayerNamePanel == null) editPlayerNamePanel = null;
    }

    private void CopyRefsFrom(UIFlow other)
    {
        if (other == null)
            return;

        if (authenticatePanel == null) authenticatePanel = other.authenticatePanel;
        if (lobbyListPanel == null) lobbyListPanel = other.lobbyListPanel;
        if (lobbyCreatePanel == null) lobbyCreatePanel = other.lobbyCreatePanel;
        if (lobbyPanel == null) lobbyPanel = other.lobbyPanel;
        if (editPlayerNamePanel == null) editPlayerNamePanel = other.editPlayerNamePanel;
    }
}
