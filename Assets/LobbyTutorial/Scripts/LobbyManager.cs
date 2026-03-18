using System.Collections;
using Mirror;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public const string KEY_PLAYER_NAME = "PlayerName";
    public const string KEY_DUCK_COLOR = "DuckColor";
    public const string KEY_GAME_MODE = "GameMode";

    public enum GameMode { CaptureTheFlag, Conquest }
    public enum DuckColor { Blue = 0, Orange = 1, Pink = 2, Green = 3, Yellow = 4, Purple = 5 }

    public string CurrentLobbyName { get; private set; } = "Lobby";
    public GameMode CurrentGameMode { get; private set; } = GameMode.CaptureTheFlag;

    // Ã¢Ëœâ€¦ Ã Â¹â‚¬Ã Â¸Å¾Ã Â¸Â´Ã Â¹Ë†Ã Â¸Â¡Ã Â¸ÂªÃ Â¸â€“Ã Â¸Â²Ã Â¸â„¢Ã Â¸Â°Ã Â¸â€žÃ Â¸Â§Ã Â¸Â²Ã Â¸Â¡Ã Â¹â‚¬Ã Â¸â€ºÃ Â¹â€¡Ã Â¸â„¢ Private + Ã Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸ÂªÃ Â¸Å“Ã Â¹Ë†Ã Â¸Â²Ã Â¸â„¢ (Ã Â¸â€ºÃ Â¸Â£Ã Â¸Â°Ã Â¸ÂÃ Â¸Â²Ã Â¸Â¨/Ã Â¹â‚¬Ã Â¸Å Ã Â¹â€¡Ã Â¸â€žÃ Â¸â€¢Ã Â¸Â­Ã Â¸â„¢Ã Â¹â‚¬Ã Â¸â€šÃ Â¹â€°Ã Â¸Â²Ã Â¸Â«Ã Â¹â€°Ã Â¸Â­Ã Â¸â€¡)
    public bool CurrentIsPrivate { get; private set; } = false;
    public string CurrentLobbyPassword { get; private set; } = "";

    // Ã¢Ëœâ€¦ Ã Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸ÂªÃ Â¸â€”Ã Â¸ÂµÃ Â¹Ë† client Ã Â¸Ë†Ã Â¸Â°Ã Â¸ÂªÃ Â¹Ë†Ã Â¸â€¡Ã Â¸â€¢Ã Â¸Â­Ã Â¸â„¢ Join (Ã Â¸â€¢Ã Â¸Â±Ã Â¹â€°Ã Â¸â€¡Ã Â¸Ë†Ã Â¸Â²Ã Â¸Â UI Ã Â¸ÂÃ Â¹Ë†Ã Â¸Â­Ã Â¸â„¢Ã Â¹â‚¬Ã Â¸Â£Ã Â¸Â´Ã Â¹Ë†Ã Â¸Â¡Ã Â¹â‚¬Ã Â¸Å Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¡Ã Â¸â€¢Ã Â¹Ë†Ã Â¸Â­)
    public static string PendingJoinPassword = "";

    // Ã¢Ëœâ€¦ Ã Â¸â€šÃ Â¹â€°Ã Â¸Â­Ã Â¸â€žÃ Â¸Â§Ã Â¸Â²Ã Â¸Â¡Ã Â¹Æ’Ã Â¸Å Ã Â¹â€°Ã Â¸ÂªÃ Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸ÂªÃ Â¸Â²Ã Â¸Â£Ã Â¸Â£Ã Â¸Â°Ã Â¸Â«Ã Â¸Â§Ã Â¹Ë†Ã Â¸Â²Ã Â¸â€¡ client/server Ã Â¹â‚¬Ã Â¸Å¾Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¹â‚¬Ã Â¸Å Ã Â¹â€¡Ã Â¸â€žÃ Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸ÂªÃ Â¸Å“Ã Â¹Ë†Ã Â¸Â²Ã Â¸â„¢
    public struct JoinPasswordMsg : NetworkMessage { public string password; }
    public struct JoinPasswordResultMsg : NetworkMessage { public bool ok; public string reason; }

    public int LastKnownMaxPlayers { get; private set; } = 0;

    public int HostPort { get; private set; } = 7777;
    public void SetHostPort(int port)
    {
        HostPort = Mathf.Clamp(port, 1024, 65535);
    }


    LobbyNetworkManager M => NetworkManager.singleton as LobbyNetworkManager;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        AutoAssignHostPortIfUnset();
    }

    void AutoAssignHostPortIfUnset()
    {
        // Ã Â¹Æ’Ã Â¸Å Ã Â¹â€°Ã Â¸â€žÃ Â¹Ë†Ã Â¸Â²Ã Â¸Å¾Ã Â¸Â­Ã Â¸Â£Ã Â¹Å’Ã Â¸â€¢Ã Â¸Ë†Ã Â¸Â²Ã Â¸Â Transport Ã Â¹â‚¬Ã Â¸â€ºÃ Â¹â€¡Ã Â¸â„¢Ã Â¸ÂÃ Â¸Â²Ã Â¸â„¢ (Ã Â¹â‚¬Ã Â¸Å Ã Â¹Ë†Ã Â¸â„¢ 7777), Ã Â¹ÂÃ Â¸Â¥Ã Â¹â€°Ã Â¸Â§Ã Â¸ÂÃ Â¸Â£Ã Â¸Â°Ã Â¸Ë†Ã Â¸Â²Ã Â¸Â¢Ã Â¸â€Ã Â¹â€°Ã Â¸Â§Ã Â¸Â¢ PID
        int basePort = 7777;
        var kcp = NetworkManager.singleton ? NetworkManager.singleton.transport as kcp2k.KcpTransport : null;
        if (kcp != null) basePort = kcp.Port;

        if (!PlayerPrefs.HasKey("HostPort"))
        {
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            int port = Mathf.Clamp(basePort + (pid % 16), 1024, 65535); // Ã Â¸ÂÃ Â¸Â£Ã Â¸Â°Ã Â¸Ë†Ã Â¸Â²Ã Â¸Â¢ 16 Ã Â¸Å Ã Â¹Ë†Ã Â¸Â­Ã Â¸â€¡
            PlayerPrefs.SetInt("HostPort", port);
            PlayerPrefs.Save();
            ;
        }
    }

    // Ã¢Ëœâ€¦ Ã Â¹Æ’Ã Â¸Â«Ã Â¹â€° UI Ã Â¹â‚¬Ã Â¸Â£Ã Â¸ÂµÃ Â¸Â¢Ã Â¸ÂÃ Â¸ÂÃ Â¹Ë†Ã Â¸Â­Ã Â¸â„¢Ã Â¸ÂªÃ Â¸Â£Ã Â¹â€°Ã Â¸Â²Ã Â¸â€¡Ã Â¸Â«Ã Â¹â€°Ã Â¸Â­Ã Â¸â€¡ (Ã Â¸Â«Ã Â¸Â£Ã Â¸Â·Ã Â¸Â­Ã Â¸Ë†Ã Â¸Â°Ã Â¸Å¾Ã Â¸Â¶Ã Â¹Ë†Ã Â¸â€¡Ã Â¸Å¾Ã Â¸Â² param isPrivate Ã Â¸â€šÃ Â¸Â­Ã Â¸â€¡ CreateLobby Ã Â¸ÂÃ Â¹â€¡Ã Â¹â€žÃ Â¸â€Ã Â¹â€°)
    public void SetLobbyPrivacy(bool isPrivate, string password)
    {
        CurrentIsPrivate = isPrivate;
        CurrentLobbyPassword = isPrivate ? (password ?? "") : "";
    }

    // ===== Helpers =====
    kcp2k.KcpTransport GetKcp()
    {
        // Ã Â¹Æ’Ã Â¸Å Ã Â¹â€° Transport Ã Â¸Å¡Ã Â¸â„¢ NetworkManager Ã Â¹â€šÃ Â¸â€Ã Â¸Â¢Ã Â¸â€¢Ã Â¸Â£Ã Â¸â€¡ (Ã Â¸â€¢Ã Â¸Â±Ã Â¸Â§Ã Â¸Ë†Ã Â¸Â£Ã Â¸Â´Ã Â¸â€¡Ã Â¸â€”Ã Â¸ÂµÃ Â¹Ë†Ã Â¸Ë†Ã Â¸Â°Ã Â¸â€“Ã Â¸Â¹Ã Â¸ÂÃ Â¹Æ’Ã Â¸Å Ã Â¹â€°Ã Â¸â€¢Ã Â¸Â­Ã Â¸â„¢ StartHost/Client)
        return NetworkManager.singleton
            ? NetworkManager.singleton.transport as kcp2k.KcpTransport
            : null;
    }

    bool IsUdpPortFree(int port)
    {
        try { using (var c = new System.Net.Sockets.UdpClient(port)) { } return true; }
        catch { return false; }
    }

    int FindFreeUdpPortStartingAt(int startPort, int attempts = 16)
    {
        startPort = Mathf.Clamp(startPort, 1024, 65535);
        for (int i = 0; i < attempts; i++)
        {
            int p = startPort + i;
            if (p > 65535) break;
            if (IsUdpPortFree(p)) return p;
        }
        return -1;
    }

    // --- Host / Join / Leave ---
    // ===== CreateLobby: Ã Â¸â€¢Ã Â¸Â±Ã Â¹â€°Ã Â¸â€¡Ã Â¸Å¾Ã Â¸Â­Ã Â¸Â£Ã Â¹Å’Ã Â¸â€¢Ã Â¸Å¡Ã Â¸â„¢ KcpTransport Ã Â¸â€šÃ Â¸Â­Ã Â¸â€¡ NetworkManager Ã Â¹ÂÃ Â¸Â¥Ã Â¹â€°Ã Â¸Â§Ã Â¸â€žÃ Â¹Ë†Ã Â¸Â­Ã Â¸Â¢ StartHost =====
    public void CreateLobby(string lobbyName, int maxPlayers, bool isPrivate, GameMode mode)
    {
        // Ã Â¹â€šÃ Â¸â€ºÃ Â¸Â£Ã Â¹â‚¬Ã Â¸â€¹Ã Â¸ÂªÃ Â¹â‚¬Ã Â¸â€Ã Â¸ÂµÃ Â¸Â¢Ã Â¸Â§ Ã¢â‚¬Å“Ã Â¹â€šÃ Â¸Â®Ã Â¸ÂªÃ Â¸â€¢Ã Â¹Å’Ã Â¹â€žÃ Â¸â€Ã Â¹â€°Ã Â¸â€”Ã Â¸ÂµÃ Â¸Â¥Ã Â¸Â°Ã Â¸Â«Ã Â¹â€°Ã Â¸Â­Ã Â¸â€¡Ã¢â‚¬Â Ã Â¹â‚¬Ã Â¸â€”Ã Â¹Ë†Ã Â¸Â²Ã Â¸â„¢Ã Â¸Â±Ã Â¹â€°Ã Â¸â„¢
        if (NetworkServer.active)
        {
            Debug.LogWarning("[Lobby] This process is already hosting. Run another app instance for a second room (or StopHost first).");
            return;
        }

        CurrentLobbyName = string.IsNullOrWhiteSpace(lobbyName) ? "Lobby" : lobbyName.Trim();
        CurrentGameMode = mode;
        CurrentIsPrivate = isPrivate;

        if (!M) { Debug.LogError("[Lobby] NetworkManager missing"); return; }

        M.maxConnections = Mathf.Clamp(maxPlayers, 3, 6);
        M.minPlayers = M.maxConnections;
        LastKnownMaxPlayers = M.maxConnections;

        var kcp = GetKcp();
        if (kcp == null)
        {
            Debug.LogError("[KCP] KcpTransport not found on NetworkManager.");
            UIFlow.I?.ShowLobbyList();
            return;
        }

        // Ã Â¹â‚¬Ã Â¸Â£Ã Â¸Â´Ã Â¹Ë†Ã Â¸Â¡Ã Â¸Ë†Ã Â¸Â²Ã Â¸ÂÃ Â¸Å¾Ã Â¸Â­Ã Â¸Â£Ã Â¹Å’Ã Â¸â€¢Ã Â¸ÂÃ Â¸Â²Ã Â¸â„¢ (PlayerPrefs Ã Â¸Â«Ã Â¸Â£Ã Â¸Â·Ã Â¸Â­Ã Â¸â€žÃ Â¹Ë†Ã Â¸Â²Ã Â¸â€ºÃ Â¸Â±Ã Â¸Ë†Ã Â¸Ë†Ã Â¸Â¸Ã Â¸Å¡Ã Â¸Â±Ã Â¸â„¢) Ã Â¹ÂÃ Â¸Â¥Ã Â¹â€°Ã Â¸Â§Ã Â¸Â¥Ã Â¸Â­Ã Â¸â€¡Ã Â¹â‚¬Ã Â¸Â¥Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸â„¢Ã Â¸â€šÃ Â¸Â¶Ã Â¹â€°Ã Â¸â„¢Ã Â¹â€žÃ Â¸â€ºÃ Â¹â‚¬Ã Â¸Â£Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¢Ã Â¹â€ 
        int basePort = PlayerPrefs.GetInt("HostPort", kcp.Port);
        const int MAX_TRIES = 24;
        bool started = false;
        for (int i = 0; i < MAX_TRIES; i++)
        {
            int candidate = Mathf.Clamp(basePort + i, 1024, 65535);
            kcp.Port = (ushort)candidate;
            try
            {
                NetworkManager.singleton.StartHost();
                started = true;
                PlayerPrefs.SetInt("HostPort", candidate);
                ;
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KCP] Port {candidate} busy Ã¢â€ â€™ {ex.Message}");
                if (NetworkServer.active || NetworkClient.active)
                {
                    try { NetworkManager.singleton.StopHost(); } catch { }
                }
            }
        }
        if (!started) { Debug.LogError("[KCP] No free UDP port found for hosting."); UIFlow.I?.ShowLobbyList(); return; }


        // ---- Ã Â¸Â¡Ã Â¸Â²Ã Â¸â€“Ã Â¸Â¶Ã Â¸â€¡Ã Â¸â„¢Ã Â¸ÂµÃ Â¹Ë†Ã Â¸â€žÃ Â¸Â·Ã Â¸Â­Ã Â¹â€šÃ Â¸Â®Ã Â¸ÂªÃ Â¸â€¢Ã Â¹Å’Ã Â¸â€šÃ Â¸Â¶Ã Â¹â€°Ã Â¸â„¢Ã Â¹ÂÃ Â¸Â¥Ã Â¹â€°Ã Â¸Â§ ----

        // handler Ã Â¸â€¢Ã Â¸Â£Ã Â¸Â§Ã Â¸Ë†Ã Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸Âª (Ã Â¸Â«Ã Â¹â€°Ã Â¸Â­Ã Â¸â€¡ Private)
        NetworkServer.RegisterHandler<JoinPasswordMsg>(OnJoinPasswordMsg, false);

        // Ã Â¸â€¢Ã Â¸Â±Ã Â¹â€°Ã Â¸â€¡Ã Â¸Å Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­/Ã Â¸ÂªÃ Â¸ÂµÃ Â¸Ë†Ã Â¸Â²Ã Â¸ÂÃ Â¸â€žÃ Â¹Ë†Ã Â¸Â²Ã Â¸â€”Ã Â¸ÂµÃ Â¹Ë†Ã Â¸Å¡Ã Â¸Â±Ã Â¸â„¢Ã Â¸â€”Ã Â¸Â¶Ã Â¸ÂÃ Â¹â€žÃ Â¸Â§Ã Â¹â€°
        var nm = PlayerPrefs.GetString(KEY_PLAYER_NAME, "Player");
        if (LobbyRoomPlayer.Local) LobbyRoomPlayer.Local.CmdSetName(nm);
        int saved = PlayerPrefs.GetInt(KEY_DUCK_COLOR, 0);
        if (LobbyRoomPlayer.Local) LobbyRoomPlayer.Local.CmdSetDuckColor(saved);

        // Ã Â¹â€šÃ Â¸â€ Ã Â¸Â©Ã Â¸â€œÃ Â¸Â² IP:Port Ã Â¸Ë†Ã Â¸Â£Ã Â¸Â´Ã Â¸â€¡
        DiscoveryBridge.I?.AdvertiseIfHost();

        // Ã Â¹â€žÃ Â¸â€ºÃ Â¸Â«Ã Â¸â„¢Ã Â¹â€°Ã Â¸Â² Lobby
        UIFlow.I?.ShowLobby();
    }
    public void SetClientPreview(string lobbyName, int maxPlayers, string modeLabel)
    {
        // Ã Â¸Å Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­
        CurrentLobbyName = string.IsNullOrWhiteSpace(lobbyName) ? "Lobby" : lobbyName.Trim();

        // Max players Ã Â¸â€”Ã Â¸ÂµÃ Â¹Ë†Ã Â¸â€ºÃ Â¸Â£Ã Â¸Â°Ã Â¸ÂÃ Â¸Â²Ã Â¸Â¨Ã Â¸Ë†Ã Â¸Â²Ã Â¸Â discovery (Ã Â¹â‚¬Ã Â¸Å Ã Â¹Ë†Ã Â¸â„¢ 2..6)
        LastKnownMaxPlayers = Mathf.Clamp(maxPlayers, 1, 100);

        // Ã Â¹â€šÃ Â¸Â«Ã Â¸Â¡Ã Â¸â€Ã Â¹â‚¬Ã Â¸ÂÃ Â¸Â¡Ã Â¸Å¾Ã Â¸Â¢Ã Â¸Â²Ã Â¸Â¢Ã Â¸Â²Ã Â¸Â¡ parse Ã Â¸Ë†Ã Â¸Â²Ã Â¸Â label (Ã Â¸â€“Ã Â¹â€°Ã Â¸Â²Ã Â¹â€žÃ Â¸Â¡Ã Â¹Ë†Ã Â¸â€¢Ã Â¸Â£Ã Â¸â€¡ enum Ã Â¸ÂÃ Â¹â€¡Ã Â¸â€ºÃ Â¸Â¥Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¢Ã Â¸â€žÃ Â¹Ë†Ã Â¸Â²Ã Â¹â‚¬Ã Â¸â€Ã Â¸Â´Ã Â¸Â¡)
        if (!string.IsNullOrWhiteSpace(modeLabel))
        {
            // Ã Â¹â‚¬Ã Â¸Å“Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¡Ã Â¸Âµ prefix Ã Â¸Â­Ã Â¸Â¢Ã Â¹Ë†Ã Â¸Â²Ã Â¸â€¡ Ã°Å¸â€â€™ (Ã Â¸â€“Ã Â¹â€°Ã Â¸Â²Ã Â¹Æ’Ã Â¸Å Ã Â¹â€°Ã Â¸Â Ã Â¸Â²Ã Â¸Â¢Ã Â¸Â«Ã Â¸Â¥Ã Â¸Â±Ã Â¸â€¡)
            var pure = (modeLabel ?? "").Replace("\U0001F512", "").Trim();
            if (System.Enum.TryParse(pure, out GameMode parsed))
                CurrentGameMode = parsed;
        }
    }


    public void LeaveLobby()
    {
        if (NetworkServer.active && NetworkClient.active) NetworkManager.singleton.StopHost();
        else if (NetworkClient.active) NetworkManager.singleton.StopClient();

        UIFlow.I?.ShowLobbyList();
        DiscoveryBridge.I?.StartClientScan();
    }

    public bool IsLobbyHost() => NetworkServer.active && NetworkClient.active;

    // --- Profile ---
    public void UpdatePlayerName(string playerName)
    {
        PlayerPrefs.SetString(KEY_PLAYER_NAME, playerName);
        if (LobbyRoomPlayer.Local) LobbyRoomPlayer.Local.CmdSetName(playerName);
    }

    // --- Duck Color ---
    public void UpdateDuckColor(DuckColor color)
    {
        PlayerPrefs.SetInt(KEY_DUCK_COLOR, (int)color);
        if (LobbyRoomPlayer.Local) LobbyRoomPlayer.Local.CmdSetDuckColor((int)color);
    }

    // --- Game mode (local only label; sync Ã Â¸Ë†Ã Â¸Â£Ã Â¸Â´Ã Â¸â€¡Ã Â¸â€žÃ Â¹Ë†Ã Â¸Â­Ã Â¸Â¢Ã Â¹â‚¬Ã Â¸Å¾Ã Â¸Â´Ã Â¹Ë†Ã Â¸Â¡ RoomState) ---
    public void ChangeGameMode()
    {
        CurrentGameMode = CurrentGameMode == GameMode.CaptureTheFlag ? GameMode.Conquest : GameMode.CaptureTheFlag;
    }

    // Ã¢Ëœâ€¦ Ã Â¸ÂÃ Â¸Â±Ã Â¹Ë†Ã Â¸â€¡ client: Ã Â¸ÂªÃ Â¸Â¡Ã Â¸Â±Ã Â¸â€žÃ Â¸Â£ handler Ã Â¸Â£Ã Â¸Â±Ã Â¸Å¡Ã Â¸Å“Ã Â¸Â¥Ã Â¸Â¥Ã Â¸Â±Ã Â¸Å¾Ã Â¸ËœÃ Â¹Å’ Ã Â¹ÂÃ Â¸Â¥Ã Â¸Â°Ã Â¸ÂªÃ Â¹Ë†Ã Â¸â€¡Ã Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸ÂªÃ Â¸Â«Ã Â¸Â¥Ã Â¸Â±Ã Â¸â€¡Ã Â¹â‚¬Ã Â¸Å Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¡Ã Â¸â€¢Ã Â¹Ë†Ã Â¸Â­Ã Â¸ÂªÃ Â¸Â³Ã Â¹â‚¬Ã Â¸Â£Ã Â¹â€¡Ã Â¸Ë†
    // ===== JoinLobbyByAddress: Ã Â¸Â£Ã Â¸Â­Ã Â¸â€¡Ã Â¸Â£Ã Â¸Â±Ã Â¸Å¡ "ip:port" Ã Â¹ÂÃ Â¸Â¥Ã Â¸Â°Ã Â¸â€¢Ã Â¸Â±Ã Â¹â€°Ã Â¸â€¡Ã Â¸Å¾Ã Â¸Â­Ã Â¸Â£Ã Â¹Å’Ã Â¸â€¢Ã Â¸Å¡Ã Â¸â„¢ KcpTransport Ã Â¸ÂÃ Â¹Ë†Ã Â¸Â­Ã Â¸â„¢ StartClient =====
    public void JoinLobbyByAddress(string address)
    {
        // Ã Â¸â€“Ã Â¹â€°Ã Â¸Â²Ã Â¸ÂÃ Â¸Â³Ã Â¸Â¥Ã Â¸Â±Ã Â¸â€¡Ã Â¹â€šÃ Â¸Â®Ã Â¸ÂªÃ Â¸â€¢Ã Â¹Å’/Ã Â¸â€¢Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â­Ã Â¸Â¢Ã Â¸Â¹Ã Â¹Ë† Ã Â¹Æ’Ã Â¸Â«Ã Â¹â€°Ã Â¸â€ºÃ Â¸Â´Ã Â¸â€Ã Â¸ÂÃ Â¹Ë†Ã Â¸Â­Ã Â¸â„¢
        if (NetworkServer.active && NetworkClient.active) NetworkManager.singleton.StopHost();
        else if (NetworkClient.active) NetworkManager.singleton.StopClient();
        else if (NetworkServer.active) NetworkManager.singleton.StopServer();

        if (!string.IsNullOrWhiteSpace(address))
        {
            string ip = address.Trim();
            int port = -1;

            int colon = ip.LastIndexOf(':');
            if (colon > 0 && colon < ip.Length - 1 && int.TryParse(ip.Substring(colon + 1), out var parsed))
            {
                port = parsed;
                ip = ip.Substring(0, colon);
            }

            // Ã¢Å“â€¦ Ã Â¸â€¢Ã Â¸Â±Ã Â¹â€°Ã Â¸â€¡Ã Â¸Å¾Ã Â¸Â­Ã Â¸Â£Ã Â¹Å’Ã Â¸â€¢Ã Â¹Æ’Ã Â¸Â«Ã Â¹â€° kcp Ã Â¹â‚¬Ã Â¸Å¾Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¹â‚¬Ã Â¸Å Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¡Ã Â¹â€žÃ Â¸â€ºÃ Â¸Â¢Ã Â¸Â±Ã Â¸â€¡Ã Â¸â€ºÃ Â¸Â¥Ã Â¸Â²Ã Â¸Â¢Ã Â¸â€”Ã Â¸Â²Ã Â¸â€¡
            var nm = NetworkManager.singleton;
            var kcp = nm ? nm.transport as kcp2k.KcpTransport : null;
            if (kcp != null && port > 0) kcp.Port = (ushort)Mathf.Clamp(port, 1024, 65535);

            nm.networkAddress = ip;
        }

        // handler Ã Â¸Å“Ã Â¸Â¥Ã Â¸â€¢Ã Â¸Â£Ã Â¸Â§Ã Â¸Ë†Ã Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸Âª
        NetworkClient.RegisterHandler<JoinPasswordResultMsg>(OnJoinPasswordResult, false);

        if (!NetworkClient.active)
            NetworkManager.singleton.StartClient();

        // Ã Â¸ÂªÃ Â¹Ë†Ã Â¸â€¡Ã Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸Âª (Ã Â¸ÂÃ Â¸Â£Ã Â¸â€œÃ Â¸ÂµÃ Â¸Â«Ã Â¹â€°Ã Â¸Â­Ã Â¸â€¡ private) Ã Â¹â‚¬Ã Â¸Â¡Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¹â‚¬Ã Â¸Å Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¡Ã Â¸ÂªÃ Â¸Â³Ã Â¹â‚¬Ã Â¸Â£Ã Â¹â€¡Ã Â¸Ë†
        StartCoroutine(SendPasswordWhenConnected());
    }

    
    IEnumerator SendPasswordWhenConnected()
    {
        while (!NetworkClient.isConnected) yield return null;

        var pass = PendingJoinPassword ?? "";
        NetworkClient.Send(new JoinPasswordMsg { password = pass });

        // Ã Â¹â‚¬Ã Â¸â€žÃ Â¸Â¥Ã Â¸ÂµÃ Â¸Â¢Ã Â¸Â£Ã Â¹Å’Ã Â¹â‚¬Ã Â¸Å¾Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸â€žÃ Â¸Â§Ã Â¸Â²Ã Â¸Â¡Ã Â¸â€ºÃ Â¸Â¥Ã Â¸Â­Ã Â¸â€Ã Â¸Â Ã Â¸Â±Ã Â¸Â¢
        PendingJoinPassword = "";
    }

    // Ã¢Ëœâ€¦ Ã Â¸ÂÃ Â¸Â±Ã Â¹Ë†Ã Â¸â€¡Ã Â¹â‚¬Ã Â¸â€¹Ã Â¸Â´Ã Â¸Â£Ã Â¹Å’Ã Â¸Å¸Ã Â¹â‚¬Ã Â¸Â§Ã Â¸Â­Ã Â¸Â£Ã Â¹Å’: Ã Â¸â€¢Ã Â¸Â£Ã Â¸Â§Ã Â¸Ë†Ã Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸Âª
    void OnJoinPasswordMsg(NetworkConnectionToClient conn, JoinPasswordMsg msg)
    {
        bool ok = !CurrentIsPrivate || msg.password == CurrentLobbyPassword;
        if (ok)
        {
            conn.Send(new JoinPasswordResultMsg { ok = true, reason = "" });
            return;
        }

        // Ã Â¸Å“Ã Â¸Â´Ã Â¸â€Ã Â¸Â£Ã Â¸Â«Ã Â¸Â±Ã Â¸Âª Ã¢â€ â€™ Ã Â¹ÂÃ Â¸Ë†Ã Â¹â€°Ã Â¸â€¡Ã Â¸Å“Ã Â¸Â¥Ã Â¹ÂÃ Â¸Â¥Ã Â¸Â°Ã Â¸â€¢Ã Â¸Â±Ã Â¸â€Ã Â¸ÂÃ Â¸Â²Ã Â¸Â£Ã Â¹â‚¬Ã Â¸Å Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¡Ã Â¸â€¢Ã Â¹Ë†Ã Â¸Â­
        conn.Send(new JoinPasswordResultMsg { ok = false, reason = "Wrong password" });
        conn.Disconnect();
    }

    // Ã¢Ëœâ€¦ Ã Â¸ÂÃ Â¸Â±Ã Â¹Ë†Ã Â¸â€¡Ã Â¹â€žÃ Â¸â€žÃ Â¸Â¥Ã Â¹â‚¬Ã Â¸Â­Ã Â¸â„¢Ã Â¸â€¢Ã Â¹Å’: Ã Â¸Â£Ã Â¸Â±Ã Â¸Å¡Ã Â¸Å“Ã Â¸Â¥Ã Â¸â€¢Ã Â¸Â£Ã Â¸Â§Ã Â¸Ë†
    void OnJoinPasswordResult(JoinPasswordResultMsg res)
    {
        if (res.ok) return; // Ã Â¸Å“Ã Â¹Ë†Ã Â¸Â²Ã Â¸â„¢Ã Â¹ÂÃ Â¸Â¥Ã Â¹â€°Ã Â¸Â§ Ã Â¸Â­Ã Â¸Â¢Ã Â¸Â¹Ã Â¹Ë†Ã Â¹Æ’Ã Â¸â„¢Ã Â¸Â«Ã Â¹â€°Ã Â¸Â­Ã Â¸â€¡Ã Â¸â€¢Ã Â¹Ë†Ã Â¸Â­

        // Ã Â¹â€žÃ Â¸Â¡Ã Â¹Ë†Ã Â¸Å“Ã Â¹Ë†Ã Â¸Â²Ã Â¸â„¢ Ã¢â€ â€™ Ã Â¹â‚¬Ã Â¸Â¥Ã Â¸Â´Ã Â¸ÂÃ Â¹â‚¬Ã Â¸Å Ã Â¸Â·Ã Â¹Ë†Ã Â¸Â­Ã Â¸Â¡Ã Â¸â€¢Ã Â¹Ë†Ã Â¸Â­Ã Â¹ÂÃ Â¸Â¥Ã Â¸Â°Ã Â¸Â¢Ã Â¹â€°Ã Â¸Â­Ã Â¸â„¢Ã Â¸ÂÃ Â¸Â¥Ã Â¸Â±Ã Â¸Å¡Ã Â¸Â¥Ã Â¸Â´Ã Â¸ÂªÃ Â¸â€¢Ã Â¹Å’
        if (NetworkClient.isConnected) NetworkManager.singleton.StopClient();
        Debug.LogWarning($"[Lobby] Join rejected: {res.reason}");

        UIFlow.I?.ShowLobbyList();
        DiscoveryBridge.I?.StartClientScan();
    }
}


