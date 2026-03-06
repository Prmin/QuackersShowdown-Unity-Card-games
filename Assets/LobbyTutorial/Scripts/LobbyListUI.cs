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

    private sealed class LobbyRowState
    {
        public LobbyListSingleUI ui;
        public string address;
        public float lastSeenAt;
    }

    [Header("List")]
    [SerializeField] private Transform lobbySingleTemplate;
    [SerializeField] private Transform container;

    [Header("Top Buttons")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField, Min(1f)] private float staleTimeoutSeconds = 4f;
    [SerializeField, Min(0.25f)] private float stalePruneIntervalSeconds = 1f;

    // Keep created rows keyed by a stable server key (serverId preferred).
    private readonly Dictionary<string, LobbyRowState> rows = new Dictionary<string, LobbyRowState>();
    private float nextPruneAt;

    private void Awake()
    {
        Instance = this;

        if (lobbySingleTemplate != null)
            lobbySingleTemplate.gameObject.SetActive(false);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshRequested);

        if (createLobbyButton != null)
            createLobbyButton.onClick.AddListener(ShowLobbyCreate);

        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(RefreshRequested);

        if (createLobbyButton != null)
            createLobbyButton.onClick.RemoveListener(ShowLobbyCreate);

        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.RemoveListener(GoToMainMenu);
    }

    public void ClearList()
    {
        nextPruneAt = Time.unscaledTime + stalePruneIntervalSeconds;

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
        => AddOrUpdate(address, lobbyName, address, curPlayers, maxPlayers, modeLabel, isPrivate);

    public void AddOrUpdate(string serverKey, string lobbyName, string address, int curPlayers, int maxPlayers, string modeLabel, bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(address) || container == null || lobbySingleTemplate == null)
            return;
        if (string.IsNullOrWhiteSpace(serverKey))
            serverKey = address;

        float now = Time.unscaledTime;

        if (rows.TryGetValue(serverKey, out LobbyRowState state))
        {
            if (state == null || state.ui == null)
            {
                rows.Remove(serverKey);
            }
            else
            {
                string chosenAddress = ChooseBetterAddress(state.address, address);
                state.address = chosenAddress;
                state.lastSeenAt = now;
                state.ui.Set(lobbyName, chosenAddress, curPlayers, maxPlayers, modeLabel, isPrivate);
            }
            return;
        }

        Transform t = Instantiate(lobbySingleTemplate, container);
        t.gameObject.SetActive(true);

        LobbyListSingleUI uiNew = t.GetComponent<LobbyListSingleUI>();
        if (uiNew == null)
            uiNew = t.gameObject.AddComponent<LobbyListSingleUI>();

        uiNew.Set(lobbyName, address, curPlayers, maxPlayers, modeLabel, isPrivate);
        rows[serverKey] = new LobbyRowState
        {
            ui = uiNew,
            address = address,
            lastSeenAt = now
        };
    }

    private void Update()
    {
        if (rows.Count == 0)
            return;

        float now = Time.unscaledTime;
        if (now < nextPruneAt)
            return;

        nextPruneAt = now + stalePruneIntervalSeconds;
        PruneStaleRows(now);
    }

    private void RefreshRequested()
    {
        ClearList();
        DiscoveryBridge.I?.StartClientScan();
    }

    private void ShowLobbyCreate()
    {
        UIFlow.I?.ShowLobbyCreate();
    }

    private void GoToMainMenu()
    {
        DiscoveryBridge.I?.StopClientScan();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void PruneStaleRows(float now)
    {
        if (rows.Count == 0)
            return;

        List<string> removeKeys = null;
        foreach (KeyValuePair<string, LobbyRowState> kv in rows)
        {
            LobbyRowState state = kv.Value;
            if (state == null || state.ui == null || now - state.lastSeenAt > staleTimeoutSeconds)
            {
                if (state != null && state.ui != null)
                    Destroy(state.ui.gameObject);

                removeKeys ??= new List<string>();
                removeKeys.Add(kv.Key);
            }
        }

        if (removeKeys == null)
            return;

        foreach (string key in removeKeys)
            rows.Remove(key);
    }

    private static string ChooseBetterAddress(string current, string incoming)
    {
        if (string.IsNullOrWhiteSpace(current))
            return incoming;
        if (string.IsNullOrWhiteSpace(incoming))
            return current;

        int currentScore = ScoreAddress(current);
        int incomingScore = ScoreAddress(incoming);
        return incomingScore > currentScore ? incoming : current;
    }

    private static int ScoreAddress(string address)
    {
        int split = address.LastIndexOf(':');
        if (split <= 0)
            return 0;

        string ipRaw = address.Substring(0, split);
        if (!System.Net.IPAddress.TryParse(ipRaw, out var ip))
            return 0;

        if (System.Net.IPAddress.IsLoopback(ip))
            return -100;

        byte[] b = ip.GetAddressBytes();
        if (b.Length != 4)
            return -10;

        if (b[0] == 169 && b[1] == 254)
            return -50;

        if (b[0] == 10)
            return 100;

        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            return 100;

        if (b[0] == 192 && b[1] == 168)
            return 100;

        return 10;
    }
}
