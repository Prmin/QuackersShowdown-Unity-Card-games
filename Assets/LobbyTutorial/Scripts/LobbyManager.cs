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

    // ★ เพิ่มสถานะความเป็น Private + รหัสผ่าน (ประกาศ/เช็คตอนเข้าห้อง)
    public bool CurrentIsPrivate { get; private set; } = false;
    public string CurrentLobbyPassword { get; private set; } = "";

    // ★ รหัสที่ client จะส่งตอน Join (ตั้งจาก UI ก่อนเริ่มเชื่อมต่อ)
    public static string PendingJoinPassword = "";

    // ★ ข้อความใช้สื่อสารระหว่าง client/server เพื่อเช็ครหัสผ่าน
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
        // ใช้ค่าพอร์ตจาก Transport เป็นฐาน (เช่น 7777), แล้วกระจายด้วย PID
        int basePort = 7777;
        var kcp = NetworkManager.singleton ? NetworkManager.singleton.transport as kcp2k.KcpTransport : null;
        if (kcp != null) basePort = kcp.Port;

        if (!PlayerPrefs.HasKey("HostPort"))
        {
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            int port = Mathf.Clamp(basePort + (pid % 16), 1024, 65535); // กระจาย 16 ช่อง
            PlayerPrefs.SetInt("HostPort", port);
            PlayerPrefs.Save();
            Debug.Log($"[KCP] Auto HostPort={port} for this process (pid={pid})");
        }
    }

    // ★ ให้ UI เรียกก่อนสร้างห้อง (หรือจะพึ่งพา param isPrivate ของ CreateLobby ก็ได้)
    public void SetLobbyPrivacy(bool isPrivate, string password)
    {
        CurrentIsPrivate = isPrivate;
        CurrentLobbyPassword = isPrivate ? (password ?? "") : "";
    }

    // ===== Helpers =====
    kcp2k.KcpTransport GetKcp()
    {
        // ใช้ Transport บน NetworkManager โดยตรง (ตัวจริงที่จะถูกใช้ตอน StartHost/Client)
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
    // ===== CreateLobby: ตั้งพอร์ตบน KcpTransport ของ NetworkManager แล้วค่อย StartHost =====
    public void CreateLobby(string lobbyName, int maxPlayers, bool isPrivate, GameMode mode)
    {
        // โปรเซสเดียว “โฮสต์ได้ทีละห้อง” เท่านั้น
        if (NetworkServer.active)
        {
            Debug.LogWarning("[Lobby] This process is already hosting. Run another app instance for a second room (or StopHost first).");
            return;
        }

        CurrentLobbyName = string.IsNullOrWhiteSpace(lobbyName) ? "Lobby" : lobbyName.Trim();
        CurrentGameMode = mode;
        CurrentIsPrivate = isPrivate;

        if (!M) { Debug.LogError("[Lobby] NetworkManager missing"); return; }

        M.maxConnections = Mathf.Clamp(maxPlayers, 2, 6);
        LastKnownMaxPlayers = M.maxConnections;

        var kcp = GetKcp();
        if (kcp == null)
        {
            Debug.LogError("[KCP] KcpTransport not found on NetworkManager.");
            UIFlow.I?.ShowLobbyList();
            return;
        }

        // เริ่มจากพอร์ตฐาน (PlayerPrefs หรือค่าปัจจุบัน) แล้วลองเลื่อนขึ้นไปเรื่อยๆ
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
                Debug.Log($"[KCP] Host started on port {candidate}");
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KCP] Port {candidate} busy → {ex.Message}");
                if (NetworkServer.active || NetworkClient.active)
                {
                    try { NetworkManager.singleton.StopHost(); } catch { }
                }
            }
        }
        if (!started) { Debug.LogError("[KCP] No free UDP port found for hosting."); UIFlow.I?.ShowLobbyList(); return; }


        // ---- มาถึงนี่คือโฮสต์ขึ้นแล้ว ----

        // handler ตรวจรหัส (ห้อง Private)
        NetworkServer.RegisterHandler<JoinPasswordMsg>(OnJoinPasswordMsg, false);

        // ตั้งชื่อ/สีจากค่าที่บันทึกไว้
        var nm = PlayerPrefs.GetString(KEY_PLAYER_NAME, "Player");
        if (LobbyRoomPlayer.Local) LobbyRoomPlayer.Local.CmdSetName(nm);
        int saved = PlayerPrefs.GetInt(KEY_DUCK_COLOR, 0);
        if (LobbyRoomPlayer.Local) LobbyRoomPlayer.Local.CmdSetDuckColor(saved);

        // โฆษณา IP:Port จริง
        DiscoveryBridge.I?.AdvertiseIfHost();

        // ไปหน้า Lobby
        UIFlow.I?.ShowLobby();
    }
    public void SetClientPreview(string lobbyName, int maxPlayers, string modeLabel)
    {
        // ชื่อ
        CurrentLobbyName = string.IsNullOrWhiteSpace(lobbyName) ? "Lobby" : lobbyName.Trim();

        // Max players ที่ประกาศจาก discovery (เช่น 2..6)
        LastKnownMaxPlayers = Mathf.Clamp(maxPlayers, 1, 100);

        // โหมดเกมพยายาม parse จาก label (ถ้าไม่ตรง enum ก็ปล่อยค่าเดิม)
        if (!string.IsNullOrWhiteSpace(modeLabel))
        {
            // เผื่อมี prefix อย่าง 🔒 (ถ้าใช้ภายหลัง)
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

    // --- Game mode (local only label; sync จริงค่อยเพิ่ม RoomState) ---
    public void ChangeGameMode()
    {
        CurrentGameMode = CurrentGameMode == GameMode.CaptureTheFlag ? GameMode.Conquest : GameMode.CaptureTheFlag;
    }

    // ★ ฝั่ง client: สมัคร handler รับผลลัพธ์ และส่งรหัสหลังเชื่อมต่อสำเร็จ
    // ===== JoinLobbyByAddress: รองรับ "ip:port" และตั้งพอร์ตบน KcpTransport ก่อน StartClient =====
    public void JoinLobbyByAddress(string address)
    {
        // ถ้ากำลังโฮสต์/ต่ออยู่ ให้ปิดก่อน
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

            // ✅ ตั้งพอร์ตให้ kcp เพื่อเชื่อมไปยังปลายทาง
            var nm = NetworkManager.singleton;
            var kcp = nm ? nm.transport as kcp2k.KcpTransport : null;
            if (kcp != null && port > 0) kcp.Port = (ushort)Mathf.Clamp(port, 1024, 65535);

            nm.networkAddress = ip;
        }

        // handler ผลตรวจรหัส
        NetworkClient.RegisterHandler<JoinPasswordResultMsg>(OnJoinPasswordResult, false);

        if (!NetworkClient.active)
            NetworkManager.singleton.StartClient();

        // ส่งรหัส (กรณีห้อง private) เมื่อเชื่อมสำเร็จ
        StartCoroutine(SendPasswordWhenConnected());
    }

    
    IEnumerator SendPasswordWhenConnected()
    {
        while (!NetworkClient.isConnected) yield return null;

        var pass = PendingJoinPassword ?? "";
        NetworkClient.Send(new JoinPasswordMsg { password = pass });

        // เคลียร์เพื่อความปลอดภัย
        PendingJoinPassword = "";
    }

    // ★ ฝั่งเซิร์ฟเวอร์: ตรวจรหัส
    void OnJoinPasswordMsg(NetworkConnectionToClient conn, JoinPasswordMsg msg)
    {
        bool ok = !CurrentIsPrivate || msg.password == CurrentLobbyPassword;
        if (ok)
        {
            conn.Send(new JoinPasswordResultMsg { ok = true, reason = "" });
            return;
        }

        // ผิดรหัส → แจ้งผลและตัดการเชื่อมต่อ
        conn.Send(new JoinPasswordResultMsg { ok = false, reason = "Wrong password" });
        conn.Disconnect();
    }

    // ★ ฝั่งไคลเอนต์: รับผลตรวจ
    void OnJoinPasswordResult(JoinPasswordResultMsg res)
    {
        if (res.ok) return; // ผ่านแล้ว อยู่ในห้องต่อ

        // ไม่ผ่าน → เลิกเชื่อมต่อและย้อนกลับลิสต์
        if (NetworkClient.isConnected) NetworkManager.singleton.StopClient();
        Debug.LogWarning($"[Lobby] Join rejected: {res.reason}");

        UIFlow.I?.ShowLobbyList();
        DiscoveryBridge.I?.StartClientScan();
    }
}
