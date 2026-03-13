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

        // ✅ ใช้ playerPrefab (รองรับทุกเวอร์ชันของ Mirror)
        // ตั้งใน Inspector ของ LobbyNetworkManager: Player Prefab = Gameplay Player (มี PlayerManager)
        GameObject gamePlayer = Instantiate(this.playerPrefab, pos, rot);

        // คัดลอก "สี" จาก RoomPlayer → GamePlayer
        var rp = roomPlayer.GetComponent<LobbyRoomPlayer>();
        var pm = gamePlayer.GetComponent<PlayerManager>();
        if (rp != null && pm != null)
        {
            pm.duckColorIndex = rp.duckColorIndex;
            pm.SetDisplayName(rp.displayName);
            pm.SetProfileAvatarIndex(rp.profileAvatarIndex);
            // ถ้าต้องการก๊อปชื่อด้วยก็ทำที่นี่ เช่น:
        }

        return gamePlayer; // Mirror จะ spawn และ sync vars ให้เอง
    }

    // ====== เพิ่ม helper ปิด UI ทั้งหมดของเมนู ======
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

        // ซีนปัจจุบันชื่ออะไร
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
        if (numPlayers < minPlayers)
        {
            reason = $"ต้องการอย่างน้อย {minPlayers} คน (ปัจจุบัน {numPlayers})";
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

    public bool CanStartGameNow() => CanStartGameNow(out _);


    [Server]
    public void StartGameIfReady()
    {
        if (CanStartGameNow(out var reason))
        {
            // ✅ ปิด Lobby UI สำหรับทุกเครื่องไว้ก่อน (โฮสต์เครื่องตัวเองเห็นผลทันที)
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
        // กลับหน้า LobbyList แล้วเริ่มสแกนใหม่
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
