using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Mirror;
using Mirror.Discovery;
using UnityEngine;

public struct LanDiscoveryRequest : NetworkMessage { }

public struct LanDiscoveryResponse : NetworkMessage
{
    public IPEndPoint EndPoint { get; set; }
    public long serverId;

    public string lobbyName;
    public int curPlayers;
    public int maxPlayers;
    public bool isPrivate;
    public ushort port;
}

public class MyNetworkDiscovery : NetworkDiscoveryBase<LanDiscoveryRequest, LanDiscoveryResponse>
{
    [SerializeField] LobbyNetworkManager lobbyManager;

    [Header("LAN Compatibility")]
    [SerializeField] bool useDirectedBroadcast = true;
    [SerializeField, Min(0.25f)] float directedBroadcastInterval = 1f;

    // Keep this stable so Editor and Android builds can discover each other.
    const long StableSecretHandshake = 0x51534C414E444953;

    readonly List<IPEndPoint> directedEndpoints = new List<IPEndPoint>();
    float nextDirectedRefreshAt;

    void Awake()
    {
        secretHandshake = StableSecretHandshake;
    }

    protected override LanDiscoveryRequest GetRequest() => new LanDiscoveryRequest();

    public new void StartDiscovery()
    {
        secretHandshake = StableSecretHandshake;
        base.StartDiscovery();

        CancelInvoke(nameof(SendDirectedDiscoveryRequests));
        if (!useDirectedBroadcast) return;

        RebuildDirectedEndpoints();
        InvokeRepeating(nameof(SendDirectedDiscoveryRequests), 0.2f, directedBroadcastInterval);
    }

    public new void StopDiscovery()
    {
        CancelInvoke(nameof(SendDirectedDiscoveryRequests));
        base.StopDiscovery();
    }

    void SendDirectedDiscoveryRequests()
    {
        if (clientUdpClient == null)
        {
            CancelInvoke(nameof(SendDirectedDiscoveryRequests));
            return;
        }

        if (NetworkClient.isConnected)
            return;

        if (Time.unscaledTime >= nextDirectedRefreshAt)
            RebuildDirectedEndpoints();

        if (directedEndpoints.Count == 0)
            return;

        using (NetworkWriterPooled writer = NetworkWriterPool.Get())
        {
            writer.WriteLong(secretHandshake);
            writer.Write(GetRequest());

            ArraySegment<byte> data = writer.ToArraySegment();
            foreach (IPEndPoint endPoint in directedEndpoints)
            {
                try
                {
                    clientUdpClient.SendAsync(data.Array, data.Count, endPoint);
                }
                catch
                {
                    // Ignore failures per endpoint.
                }
            }
        }
    }

    void RebuildDirectedEndpoints()
    {
        directedEndpoints.Clear();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        AddEndpoint(IPAddress.Broadcast, seen);

        if (!string.IsNullOrWhiteSpace(BroadcastAddress) && IPAddress.TryParse(BroadcastAddress, out IPAddress configured))
            AddEndpoint(configured, seen);

        NetworkInterface[] nics;
        try
        {
            nics = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch
        {
            nics = Array.Empty<NetworkInterface>();
        }

        foreach (NetworkInterface nic in nics)
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                continue;

            IPInterfaceProperties props;
            try
            {
                props = nic.GetIPProperties();
            }
            catch
            {
                continue;
            }

            foreach (UnicastIPAddressInformation unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (unicast.IPv4Mask == null)
                    continue;

                AddEndpoint(CalcBroadcast(unicast.Address, unicast.IPv4Mask), seen);
            }
        }

        nextDirectedRefreshAt = Time.unscaledTime + 10f;
    }

    void AddEndpoint(IPAddress address, HashSet<string> seen)
    {
        string key = address + ":" + serverBroadcastListenPort;
        if (!seen.Add(key))
            return;

        directedEndpoints.Add(new IPEndPoint(address, serverBroadcastListenPort));
    }

    static IPAddress CalcBroadcast(IPAddress ip, IPAddress mask)
    {
        byte[] ipBytes = ip.GetAddressBytes();
        byte[] maskBytes = mask.GetAddressBytes();
        byte[] result = new byte[4];

        for (int i = 0; i < 4; i++)
            result[i] = (byte)(ipBytes[i] | (byte)~maskBytes[i]);

        return new IPAddress(result);
    }

    void GetCounts(out int cur, out int max)
    {
        cur = 0;
        max = 0;

        LobbyNetworkManager lm = lobbyManager ? lobbyManager : NetworkManager.singleton as LobbyNetworkManager;
        if (!lm) return;

        cur = lm.roomSlots != null ? lm.roomSlots.Count(s => s != null) : 0;
        if (cur == 0 && NetworkServer.active) cur = 1;

        max = lm.maxConnections;
    }

    protected override LanDiscoveryResponse ProcessRequest(LanDiscoveryRequest request, IPEndPoint endpoint)
    {
        NetworkManager nm = NetworkManager.singleton;
        kcp2k.KcpTransport kcp = nm ? nm.transport as kcp2k.KcpTransport : null;

        GetCounts(out int cur, out int max);

        return new LanDiscoveryResponse
        {
            serverId = ServerId,
            lobbyName = LobbyManager.Instance ? LobbyManager.Instance.CurrentLobbyName : Application.productName,
            curPlayers = cur,
            maxPlayers = max,
            isPrivate = LobbyManager.Instance ? LobbyManager.Instance.CurrentIsPrivate : false,
            port = (ushort)(kcp != null ? kcp.Port : 7777)
        };
    }

    protected override void ProcessResponse(LanDiscoveryResponse response, IPEndPoint endpoint)
    {
        response.EndPoint = endpoint;
        OnServerFound.Invoke(response);
    }
}
