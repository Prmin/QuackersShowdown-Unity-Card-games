using Mirror;
using UnityEngine;
using Mirror.Discovery;
using UnityEngine.SceneManagement;

public class LobbyNetworkManager : NetworkRoomManager
{
    [Header("Player Limits")]
    [Range(2, 6)] public int maxPlayersAllowed = 6;

    [Header("Discovery (optional)")]
    public MyNetworkDiscovery discovery;

    [Header("UI Flow")]
    [SerializeField] private string lobbySceneName = "LobbyTutorial_Done";
    private bool _disconnectOverlayShownInGameplay;

    public override void OnStartHost()
    {
        base.OnStartHost();
        if (discovery)
        {
            discovery.AdvertiseServer();
            ;
        }
        else
        {
            Debug.LogWarning("[Discovery] No NetworkDiscovery assigned on LobbyNetworkManager.");
        }
    }

    public override GameObject OnRoomServerCreateGamePlayer(NetworkConnectionToClient conn, GameObject roomPlayer)
    {
        Transform start = GetStartPosition()?.transform;
        Vector3 pos = start ? start.position : Vector3.zero;
        Quaternion rot = start ? start.rotation : Quaternion.identity;

        // âœ… à¹ƒà¸Šà¹‰ playerPrefab (à¸£à¸­à¸‡à¸£à¸±à¸šà¸—à¸¸à¸à¹€à¸§à¸­à¸£à¹Œà¸Šà¸±à¸™à¸‚à¸­à¸‡ Mirror)
        // à¸•à¸±à¹‰à¸‡à¹ƒà¸™ Inspector à¸‚à¸­à¸‡ LobbyNetworkManager: Player Prefab = Gameplay Player (à¸¡à¸µ PlayerManager)
        GameObject gamePlayer = Instantiate(this.playerPrefab, pos, rot);

        // à¸„à¸±à¸”à¸¥à¸­à¸ "à¸ªà¸µ" à¸ˆà¸²à¸ RoomPlayer â†’ GamePlayer
        var rp = roomPlayer.GetComponent<LobbyRoomPlayer>();
        var pm = gamePlayer.GetComponent<PlayerManager>();
        if (rp != null && pm != null)
        {
            pm.duckColorIndex = rp.duckColorIndex;
            pm.SetDisplayName(rp.displayName);
            pm.SetProfileAvatarIndex(rp.profileAvatarIndex);
            // à¸–à¹‰à¸²à¸•à¹‰à¸­à¸‡à¸à¸²à¸£à¸à¹Šà¸­à¸›à¸Šà¸·à¹ˆà¸­à¸”à¹‰à¸§à¸¢à¸à¹‡à¸—à¸³à¸—à¸µà¹ˆà¸™à¸µà¹ˆ à¹€à¸Šà¹ˆà¸™:
        }

        return gamePlayer; // Mirror à¸ˆà¸° spawn à¹à¸¥à¸° sync vars à¹ƒà¸«à¹‰à¹€à¸­à¸‡
    }

    // ====== à¹€à¸žà¸´à¹ˆà¸¡ helper à¸›à¸´à¸” UI à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸”à¸‚à¸­à¸‡à¹€à¸¡à¸™à¸¹ ======
    void HideAllMenuUI()
    {
        UIFlow flow = UIFlow.I;
        if (flow == null)
            return;

        // Use UIFlow internal safe path (EnsureRefs + null-safe checks)
        // instead of touching serialized panel refs directly from here.
        flow.HideAllForGameplay();
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        // à¸‹à¸µà¸™à¸›à¸±à¸ˆà¸ˆà¸¸à¸šà¸±à¸™à¸Šà¸·à¹ˆà¸­à¸­à¸°à¹„à¸£
        string activePath = SceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(GameplayScene) && activePath == GameplayScene)
        {
            _disconnectOverlayShownInGameplay = false;
            HideAllMenuUI();
            ;
        }
    }

    public override void OnStopHost()
    {
        if (discovery)
        {
            discovery.StopDiscovery();
            ;
        }
        base.OnStopHost();
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        bool isGameplayScene = IsSceneMatch(SceneManager.GetActiveScene(), GameplayScene);
        bool isHostConnection = conn == NetworkServer.localConnection;

        uint departingNetId = 0;
        int departingDuckColorIndex = -1;
        uint preferredCurrentTurnNetId = 0;

        TurnManager tm = TurnManager.Instance;
        if (isGameplayScene &&
            !isHostConnection &&
            conn?.identity != null &&
            conn.identity.TryGetComponent(out PlayerManager pm))
        {
            departingNetId = pm.netId;
            departingDuckColorIndex = pm.duckColorIndex;

            if (tm != null)
                preferredCurrentTurnNetId = tm.ServerGetPreferredCurrentTurnNetIdAfterDisconnect(departingNetId);
        }

        base.OnServerDisconnect(conn);

        if (isGameplayScene && !isHostConnection && departingNetId != 0)
        {
            if (tm != null)
            {
                tm.ServerHandlePlayerDisconnect(
                    departingNetId,
                    departingDuckColorIndex,
                    preferredCurrentTurnNetId,
                    "ClientDisconnected");
            }
            else
            {
                Debug.LogWarning(
                    $"[LobbyNetworkManager] TurnManager missing during client disconnect cleanup. netId={departingNetId}"
                );
            }
        }
    }

    public override void Awake()
    {
        base.Awake();
        minPlayers = 2;
        maxConnections = Mathf.Clamp(maxConnections, 1, maxPlayersAllowed);
    }

    public override void OnRoomServerConnect(NetworkConnectionToClient conn)
    {
        if (numPlayers >= maxConnections) { conn.Disconnect(); return; }
        base.OnRoomServerConnect(conn);
    }

    public override void OnRoomServerPlayersReady() { /* no auto-start */ }

    public bool CanStartGameNow(out string reason)
    {
        int requiredPlayers = GetRequiredPlayersToStart();

        if (numPlayers < requiredPlayers)
        {
            reason = $"ต้องรอผู้เล่นให้ครบ {requiredPlayers} คน (ปัจจุบัน {numPlayers})";
            return false;
        }

        foreach (var rp in roomSlots)
        {
            if (rp == null) continue;

            // ข้ามโฮสต์: โฮสต์ถือว่า Ready เสมอ
            if (rp.connectionToClient == NetworkServer.localConnection)
                continue;

            if (!rp.readyToBegin)
            {
                reason = "ยังมีผู้เล่นที่ไม่ Ready";
                return false;
            }
        }

        reason = null;
        return true;
    }

    private int GetRequiredPlayersToStart()
    {
        int configured = Mathf.Clamp(maxConnections, 3, maxPlayersAllowed);

        if (minPlayers != configured)
            minPlayers = configured;

        return configured;
    }

    public bool CanStartGameNow() => CanStartGameNow(out _);



    [Server]
    public void StartGameIfReady()
    {
        if (CanStartGameNow(out var reason))
        {
            // âœ… à¸›à¸´à¸” Lobby UI à¸ªà¸³à¸«à¸£à¸±à¸šà¸—à¸¸à¸à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹„à¸§à¹‰à¸à¹ˆà¸­à¸™ (à¹‚à¸®à¸ªà¸•à¹Œà¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸•à¸±à¸§à¹€à¸­à¸‡à¹€à¸«à¹‡à¸™à¸œà¸¥à¸—à¸±à¸™à¸—à¸µ)
            HideAllMenuUI();

            ServerChangeScene(GameplayScene);
        }
        else
        {
            ;
        }
    }

    public override bool OnRoomServerSceneLoadedForPlayer(
        NetworkConnectionToClient conn, GameObject roomPlayer, GameObject gamePlayer)
    {
        bool result = base.OnRoomServerSceneLoadedForPlayer(conn, roomPlayer, gamePlayer);

        var lp = roomPlayer ? roomPlayer.GetComponent<LobbyRoomPlayer>() : null;
        var pm = gamePlayer ? gamePlayer.GetComponent<PlayerManager>() : null;

        if (lp != null && pm != null)
        {
            pm.SetDisplayName(lp.displayName);
            pm.SetProfileAvatarIndex(lp.profileAvatarIndex);
        }

        return result;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        // à¸à¸¥à¸±à¸šà¸«à¸™à¹‰à¸² LobbyList à¹à¸¥à¹‰à¸§à¹€à¸£à¸´à¹ˆà¸¡à¸ªà¹à¸à¸™à¹ƒà¸«à¸¡à¹ˆ
        HandleDisconnectedClientUIFlow();
        // ;
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        HandleDisconnectedClientUIFlow();
    }


    private void HandleDisconnectedClientUIFlow()
    {
        UIFlow flow = UIFlow.I;
        if (flow == null)
            return;

        Scene active = SceneManager.GetActiveScene();
        if (IsSceneMatch(active, GameplayScene))
        {
            // Keep lobby UI hidden while gameplay scene is still active.
            flow.HideAllForGameplay();

            if (!NetworkServer.active && !_disconnectOverlayShownInGameplay)
            {
                if (FindObjectOfType<MatchEndOverlayUI>() != null)
                    return;

                _disconnectOverlayShownInGameplay = true;

                TurnManager tm = TurnManager.Instance;
                if (tm != null)
                    tm.ClientShowMatchCancelledOverlay("HostDisconnected");
                else
                    flow.ShowLobbyList();
            }
            return;
        }

        _disconnectOverlayShownInGameplay = false;
        flow.ShowLobbyList();
    }

    private static bool IsSceneMatch(Scene scene, string configuredPathOrName)
    {
        if (string.IsNullOrWhiteSpace(configuredPathOrName))
            return false;

        if (string.Equals(scene.path, configuredPathOrName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        string expectedName = System.IO.Path.GetFileNameWithoutExtension(configuredPathOrName);
        if (string.IsNullOrWhiteSpace(expectedName))
            expectedName = configuredPathOrName;

        return string.Equals(scene.name, expectedName, System.StringComparison.OrdinalIgnoreCase);
    }
}
