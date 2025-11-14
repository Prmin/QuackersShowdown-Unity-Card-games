using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class LobbyRoomPlayer : NetworkRoomPlayer
{
    public static LobbyRoomPlayer Local { get; private set; }

    [SyncVar] public string displayName;
    [SyncVar] public bool isHost;

    // 0=น้ำเงิน,1=ส้ม,2=ชมพู,3=เขียว,4=เหลือง,5=ม่วง
    [SyncVar(hook = nameof(OnDuckColorChanged))] public int duckColorIndex;

    public override void OnStartServer()
    {
        base.OnStartServer();

        // ถ้าไม่เคยตั้งสี หรือสีชนแล้ว → สุ่มสีที่ว่างให้
        if (duckColorIndex < 0 || duckColorIndex > 5 || !IsColorAvailable(duckColorIndex, this))
            duckColorIndex = PickFreeColor();

        isHost = (connectionToClient == NetworkServer.localConnection);
    }

    [Client]
    public void ClientToggleReady()
    {
        // ❗ ต้องเป็นอ็อบเจ็กต์ที่ไคลเอนต์นี้เป็นเจ้าของ
        if (!netIdentity || !netIdentity.isOwned) return;

        // เรียก Command ของ NetworkRoomPlayer โดยตรง (ถูกกติกา)
        CmdChangeReadyState(!readyToBegin);
    }

    // ===== กันซ้ำเมื่อมีการเปลี่ยนสีจาก client (เหลือแค่อันเดียว) =====
    [Command]
    public void CmdSetDuckColor(int index)
    {
        index = Mathf.Clamp(index, 0, 5);

        if (!IsColorAvailable(index, this))
        {
            // แจ้งกลับ client ว่าปัดตก (ป้องกัน UI เข้าใจผิด)
            TargetColorDenied(connectionToClient, index);
            return;
        }

        duckColorIndex = index; // SyncVar จะ sync ไปทุกคน
    }

    // แจ้ง client เมื่อเลือกสีที่ถูกใช้แล้ว
    [TargetRpc]
    void TargetColorDenied(NetworkConnection target, int index)
    {
        // Debug.LogWarning($"[Lobby] สี {index} ถูกใช้แล้ว เลือกไม่ได้");
        // TODO: ถ้ามี popup/toast เรียกแสดงที่นี่
    }

    // ===== Hook ของ SyncVar (ชื่อเดียวกับที่กำหนดใน attribute) =====
    void OnDuckColorChanged(int oldV, int newV)
    {
        // อัปเดตสถานะปุ่มสีใน UI ให้ล็อก/ปลดล็อกตามจริง
        LobbyUI.Instance?.RefreshColorLocks();

        // ถ้าต้องอัปเดต UI รายชื่อผู้เล่น เพิ่มที่นี่ได้
        // (ตอนนี้ LobbyUI.RefreshPlayers() ถูกเรียกวนอยู่แล้ว)
    }

    // ตรวจว่าสีว่างไหม (ยกเว้นตัวเอง)
    bool IsColorAvailable(int index, LobbyRoomPlayer requester)
    {
        var all = GameObject.FindObjectsOfType<LobbyRoomPlayer>();
        foreach (var p in all)
        {
            if (!p) continue;
            if (p == requester) continue;
            if (p.duckColorIndex == index) return false;
        }
        return true;
    }

    // เลือกสีที่ว่างแบบสุ่ม
    int PickFreeColor()
    {
        bool[] used = new bool[6];
        var all = GameObject.FindObjectsOfType<LobbyRoomPlayer>();
        foreach (var p in all)
        {
            if (!p) continue;
            int dx = p.duckColorIndex;
            if (dx >= 0 && dx < 6) used[dx] = true;
        }

        List<int> free = new List<int>();
        for (int i = 0; i < 6; i++) if (!used[i]) free.Add(i);
        if (free.Count == 0) return 0; // กันไว้ (ปกติไม่เกิด เพราะ maxPlayers ≤ 6)

        return free[Random.Range(0, free.Count)];
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Local = this;

        var nm = PlayerPrefs.GetString(LobbyManager.KEY_PLAYER_NAME, $"Player {Random.Range(100, 999)}");
        CmdSetName(nm);

        int saved = PlayerPrefs.GetInt(LobbyManager.KEY_DUCK_COLOR, 0);
        CmdSetDuckColor(saved); // ถ้าชน สีจะไม่เปลี่ยน และมี RPC แจ้งเตือน
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer && Local == this) Local = null;
        base.OnStopClient();
    }

    [Command]
    public void CmdSetName(string name)
    {
        displayName = string.IsNullOrWhiteSpace(name) ? $"Player {Random.Range(100, 999)}" : name.Trim();
    }

    // เตะผู้เล่น (โฮสต์เท่านั้น)
    [Command(requiresAuthority = false)]
    public void CmdKickPlayer(uint targetNetId, NetworkConnectionToClient sender = null)
    {
        if (sender != NetworkServer.localConnection) return; // รับเฉพาะโฮสต์
        if (NetworkServer.spawned.TryGetValue(targetNetId, out var id))
        {
            var conn = id.connectionToClient;
            if (conn != null) conn.Disconnect();
        }
    }

}
