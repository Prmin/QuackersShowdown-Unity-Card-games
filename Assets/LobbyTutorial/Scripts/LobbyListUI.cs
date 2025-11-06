using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// แสดงรายการล็อบบี้ให้ "กดเข้ารายการ" ได้เลย (ไม่มี Manual Join)
/// ใช้คู่กับ LobbyListSingleUI ซึ่งเป็นปุ่มที่เรียก Join โดย address ภายใน
/// </summary>
public class LobbyListUI : MonoBehaviour
{
    public static LobbyListUI Instance { get; private set; }

    [Header("List")]
    [SerializeField] private Transform lobbySingleTemplate;  // เทมเพลตแถว (ต้อง setActive(false))
    [SerializeField] private Transform container;            // พาเรนต์ของรายการ

    [Header("Top Buttons")]
    [SerializeField] private Button refreshButton;           // ปุ่มรีเฟรชรายการ
    [SerializeField] private Button createLobbyButton;       // ปุ่มเปิดหน้า Create

    // เก็บรายการที่ถูกสร้างแล้วเพื่ออัปเดตซ้ำ (คีย์เป็น address)
    private readonly Dictionary<string, LobbyListSingleUI> rows = new();

    private void Awake()
    {
        Instance = this;

        if (lobbySingleTemplate) lobbySingleTemplate.gameObject.SetActive(false);

        if (refreshButton) refreshButton.onClick.AddListener(RefreshRequested);
        if (createLobbyButton) createLobbyButton.onClick.AddListener(() => UIFlow.I?.ShowLobbyCreate());
    }

    /// <summary>ล้างทั้งหมด</summary>
    public void ClearList()
    {
        rows.Clear();
        if (!container) return;
        foreach (Transform child in container)
            if (child != lobbySingleTemplate) Destroy(child.gameObject);
    }

    /// <summary>เมธอดเดิม (คงไว้) — จะถือว่าเป็น Public โดยอัตโนมัติ</summary>
    public void AddOrUpdate(string lobbyName, string address, int curPlayers, int maxPlayers, string modeLabel)
        => AddOrUpdate(lobbyName, address, curPlayers, maxPlayers, modeLabel, false);

    /// <summary>เพิ่มหรืออัปเดตรายการล็อบบี้หนึ่งแถว (รองรับ Private)</summary>
    public void AddOrUpdate(string lobbyName, string address, int curPlayers, int maxPlayers, string modeLabel, bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(address) || !container || !lobbySingleTemplate) return;

        if (rows.TryGetValue(address, out var ui))
        {
            ui.Set(lobbyName, address, curPlayers, maxPlayers, modeLabel, isPrivate);
            return;
        }

        // สร้างแถวใหม่
        var t = Instantiate(lobbySingleTemplate, container);
        t.gameObject.SetActive(true);

        var uiNew = t.GetComponent<LobbyListSingleUI>();
        if (!uiNew) uiNew = t.gameObject.AddComponent<LobbyListSingleUI>();

        uiNew.Set(lobbyName, address, curPlayers, maxPlayers, modeLabel, isPrivate);
        rows[address] = uiNew;
    }

    /// <summary>ให้ปุ่ม Refresh เรียก—คุณจะไปต่อ Mirror Discovery ที่นี่ได้</summary>
    private void RefreshRequested()
    {
        ClearList();
        // เริ่มสแกน LAN ใหม่
        DiscoveryBridge.I?.StartClientScan();
        Debug.Log("[LobbyListUI] Refresh requested → scanning LAN.");
    }
}
