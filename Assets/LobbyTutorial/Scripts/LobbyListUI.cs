using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows LAN lobby list entries and allows quick join by clicking each row.
/// </summary>
public class LobbyListUI : MonoBehaviour
{
    public static LobbyListUI Instance { get; private set; }

    [Header("List")]
    [SerializeField] private Transform lobbySingleTemplate;
    [SerializeField] private Transform container;

    [Header("Top Buttons")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // Keep created rows keyed by address for update-in-place behavior.
    private readonly Dictionary<string, LobbyListSingleUI> rows = new Dictionary<string, LobbyListSingleUI>();

    private void Awake()
    {
        Instance = this;

        if (lobbySingleTemplate != null)
            lobbySingleTemplate.gameObject.SetActive(false);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshRequested);

        if (createLobbyButton != null)
            createLobbyButton.onClick.AddListener(() => UIFlow.I?.ShowLobbyCreate());

        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    public void ClearList()
    {
        rows.Clear();
        if (container == null)
            return;

        foreach (Transform child in container)
        {
            if (child != lobbySingleTemplate)
                Destroy(child.gameObject);
        }
    }

    public void AddOrUpdate(string lobbyName, string address, int curPlayers, int maxPlayers, string modeLabel)
        => AddOrUpdate(lobbyName, address, curPlayers, maxPlayers, modeLabel, false);

    public void AddOrUpdate(string lobbyName, string address, int curPlayers, int maxPlayers, string modeLabel, bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(address) || container == null || lobbySingleTemplate == null)
            return;

        if (rows.TryGetValue(address, out LobbyListSingleUI ui))
        {
            ui.Set(lobbyName, address, curPlayers, maxPlayers, modeLabel, isPrivate);
            return;
        }

        Transform t = Instantiate(lobbySingleTemplate, container);
        t.gameObject.SetActive(true);

        LobbyListSingleUI uiNew = t.GetComponent<LobbyListSingleUI>();
        if (uiNew == null)
            uiNew = t.gameObject.AddComponent<LobbyListSingleUI>();

        uiNew.Set(lobbyName, address, curPlayers, maxPlayers, modeLabel, isPrivate);
        rows[address] = uiNew;
    }

    private void RefreshRequested()
    {
        ClearList();
        DiscoveryBridge.I?.StartClientScan();
    }

    private void GoToMainMenu()
    {
        DiscoveryBridge.I?.StopClientScan();
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
