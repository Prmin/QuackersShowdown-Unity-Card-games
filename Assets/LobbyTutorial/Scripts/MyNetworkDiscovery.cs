using System.Linq;
using System.Net;
using Mirror;
using Mirror.Discovery;
using UnityEngine;

// ===== messages =====
public struct LanDiscoveryRequest : NetworkMessage { }

public struct LanDiscoveryResponse : NetworkMessage
{
    public IPEndPoint EndPoint { get; set; } // will be filled on client

    public string lobbyName;
    public int    curPlayers;
    public int    maxPlayers;
    public bool   isPrivate;
    public ushort port; // KCP port of host
}

public class MyNetworkDiscovery :
    NetworkDiscoveryBase<LanDiscoveryRequest, LanDiscoveryResponse>
{
    [SerializeField] LobbyNetworkManager lobbyManager;

    protected override LanDiscoveryRequest GetRequest() => new LanDiscoveryRequest { };

    // นับคนแบบเชื่อถือได้จาก roomSlots
    void GetCounts(out int cur, out int max)
    {
        cur = 0; max = 0;

        // เอา LobbyNetworkManager จาก field หรือจาก singleton (กันพลาด)
        var lm = lobbyManager ? lobbyManager : NetworkManager.singleton as LobbyNetworkManager;
        if (!lm) return;

        // roomSlots อาจเป็น HashSet/List → ใช้ Linq ปลอดภัย
        cur = lm.roomSlots != null ? lm.roomSlots.Count(s => s != null) : 0;

        // กันพลาด: โฮสต์ลำพังยังไม่กด Ready ก็ถือว่ามี 1
        if (cur == 0 && NetworkServer.active) cur = 1;

        max = lm.maxConnections;
    }

    protected override LanDiscoveryResponse ProcessRequest(LanDiscoveryRequest request, IPEndPoint endpoint)
    {
        var nm  = NetworkManager.singleton;
        var kcp = nm ? nm.transport as kcp2k.KcpTransport : null;

        GetCounts(out var cur, out var max);

        return new LanDiscoveryResponse
        {
            lobbyName  = LobbyManager.Instance ? LobbyManager.Instance.CurrentLobbyName : Application.productName,
            curPlayers = cur,
            maxPlayers = max,
            isPrivate  = LobbyManager.Instance ? LobbyManager.Instance.CurrentIsPrivate : false,
            port       = (ushort)(kcp != null ? kcp.Port : 7777)
        };
    }

    protected override void ProcessResponse(LanDiscoveryResponse response, IPEndPoint endpoint)
    {
        response.EndPoint = endpoint;          // ต้นทางจริง (IP)
        OnServerFound.Invoke(response);        // ส่งให้ DiscoveryBridge
    }
}
