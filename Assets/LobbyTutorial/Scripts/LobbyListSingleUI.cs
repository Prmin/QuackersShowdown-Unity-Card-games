using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LobbyListSingleUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playersText;
    [SerializeField] private TextMeshProUGUI gameModeText;

    private string address;
    private bool isPrivate;

    // cache จาก discovery เพื่อส่งให้ LobbyManager ก่อน Join
    private string cachedName;
    private int cachedCur;
    private int cachedMax;
    private string cachedModeLabel;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            // ✅ อัปเดตพรีวิวให้ฝั่ง Client เห็นหัวล็อบบี้ทันที
            LobbyManager.Instance.SetClientPreview(cachedName, cachedMax, cachedModeLabel);

            if (isPrivate)
            {
                UI_InputWindow.Show_Static(
                    "Enter Password", "",
                    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*_-", 20,
                    () => { /* cancel */ },
                    (string value) =>
                    {
                        LobbyManager.PendingJoinPassword = value ?? "";
                        LobbyManager.Instance.JoinLobbyByAddress(address);
                        UIFlow.I?.ShowLobby(); // ถ้ารหัสผิด เซิร์ฟเวอร์จะเด้งกลับเอง
                    });
            }
            else
            {
                LobbyManager.PendingJoinPassword = "";
                LobbyManager.Instance.JoinLobbyByAddress(address);
                UIFlow.I?.ShowLobby();
            }
        });
    }

    // เดิม (5 พารามิเตอร์) — คงไว้
    public void Set(string lobbyName, string addr, int curPlayers, int maxPlayers, string modeLabel)
    {
        address = addr;
        cachedName = lobbyName;
        cachedCur = curPlayers;
        cachedMax = maxPlayers;
        cachedModeLabel = modeLabel;

        if (lobbyNameText) lobbyNameText.text = lobbyName;
        if (playersText) playersText.text = $"{curPlayers}/{maxPlayers}";
        if (gameModeText) gameModeText.text = modeLabel;
    }

    // ใหม่ (6 พารามิเตอร์) — รับ isPrivate เพิ่ม
    public void Set(string lobbyName, string addr, int curPlayers, int maxPlayers, string modeLabel, bool isPrivate)
    {
        this.isPrivate = isPrivate;
        Set(lobbyName, addr, curPlayers, maxPlayers, modeLabel);
    }
}
