using Mirror;
using UnityEngine;
using System.Reflection;
using Mirror.Discovery;
using UnityEngine.SceneManagement;

public class LobbyNetworkManager : NetworkRoomManager
{
    [Header("Player Limits")]
    [Range(2, 6)] public int maxPlayersAllowed = 6;

    [Header("Discovery (optional)")]
    public MyNetworkDiscovery discovery;

    public override void OnStartHost()
    {
        base.OnStartHost();
        if (discovery)
        {
            discovery.AdvertiseServer();
            Debug.Log("[Discovery] Host started → AdvertiseServer()");
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
            // ถ้าต้องการก๊อปชื่อด้วยก็ทำที่นี่ เช่น:
            // pm.displayName = rp.displayName;
        }

        return gamePlayer; // Mirror จะ spawn และ sync vars ให้เอง
    }

    // ====== เพิ่ม helper ปิด UI ทั้งหมดของเมนู ======
    void HideAllMenuUI()
    {
        if (UIFlow.I == null) return;
        UIFlow.I.authenticatePanel?.SetActive(false);
        UIFlow.I.lobbyListPanel?.SetActive(false);
        UIFlow.I.lobbyCreatePanel?.SetActive(false);
        UIFlow.I.lobbyPanel?.SetActive(false);
        UIFlow.I.editPlayerNamePanel?.SetActive(false);
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        // ซีนปัจจุบันชื่ออะไร
        string activePath = SceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(GameplayScene) && activePath == GameplayScene)
        {
            HideAllMenuUI();
            Debug.Log("[UI] Entered Gameplay → hide all lobby/menu UI");
        }
    }

    public override void OnStopHost()
    {
        if (discovery)
        {
            discovery.StopDiscovery();
            Debug.Log("[Discovery] Host stopped → StopDiscovery()");
        }
        base.OnStopHost();
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
            Debug.Log($"เริ่มเกมไม่ได้: {reason}");
        }
    }

    public override bool OnRoomServerSceneLoadedForPlayer(
        NetworkConnectionToClient conn, GameObject roomPlayer, GameObject gamePlayer)
    {
        bool result = base.OnRoomServerSceneLoadedForPlayer(conn, roomPlayer, gamePlayer);

        var lp = roomPlayer ? roomPlayer.GetComponent<LobbyRoomPlayer>() : null;
        var pm = gamePlayer ? gamePlayer.GetComponent<PlayerManager>() : null;

        if (lp != null && pm != null && !string.IsNullOrWhiteSpace(lp.displayName))
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var field = pm.GetType().GetField("displayName", flags);
            if (field != null && field.FieldType == typeof(string))
                field.SetValue(pm, lp.displayName);

            var prop = pm.GetType().GetProperty("DisplayName", flags);
            if (prop != null && prop.PropertyType == typeof(string) && prop.CanWrite)
                prop.SetValue(pm, lp.displayName);

            var method = pm.GetType().GetMethod("SetDisplayName", flags, null, new[] { typeof(string) }, null);
            if (method != null)
                method.Invoke(pm, new object[] { lp.displayName });
        }

        return result;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        // กลับหน้า LobbyList แล้วเริ่มสแกนใหม่
        UIFlow.I?.ShowLobbyList();
        DiscoveryBridge.I?.StartClientScan();
        Debug.Log("[Lobby] Client stopped → back to LobbyList.");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        // โดนเตะ/โฮสต์ปิด → เด้งกลับ LobbyList แล้วสแกนใหม่
        UIFlow.I?.ShowLobbyList();
        DiscoveryBridge.I?.StartClientScan();
        Debug.Log("[Lobby] Disconnected by server/host → back to LobbyList.");
    }

}
