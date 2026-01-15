using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
// อย่าลืม using Mirror.Discovery ถ้ายังไม่ได้ใส่
using Mirror.Discovery;

public class DiscoveryBridge : MonoBehaviour
{
    public static DiscoveryBridge I { get; private set; }

    [Header("Refs")]
    public MyNetworkDiscovery discovery;
    public LobbyListUI listUI;

    private readonly HashSet<string> seen = new();

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (discovery)
            discovery.OnServerFound.AddListener(OnFound);
    }

    void OnDisable()
    {
        if (discovery)
            discovery.OnServerFound.RemoveListener(OnFound);
    }

    public void StartClientScan()
    {
        if (!discovery || !listUI)
        {
            Debug.LogWarning("[DiscoveryBridge] Missing refs: discovery or listUI.");
            return;
        }

        seen.Clear();
        listUI.ClearList();

        discovery.StopDiscovery();
        discovery.StartDiscovery();
        // ;

        StartCoroutine(ScanTimeout());
    }

    IEnumerator ScanTimeout()
    {
        yield return new WaitForSeconds(1f);
    }

    public void StopClientScan()
    {
        if (discovery == null) return;

        // 🛡️ ถ้าเป็นโฮสต์ (server active) ห้ามหยุด discovery เพราะจะไปดับโหมด advertise
        if (NetworkServer.active)
        {
            ;
            return;
        }

        discovery.StopDiscovery();
        ;
    }

    public void AdvertiseIfHost()
    {
        if (discovery && NetworkServer.active)
        {
            discovery.AdvertiseServer();
            ;
        }
    }

    // เรียกครั้งเดียวพอ: discovery.OnServerFound.AddListener(OnFound);
    void OnFound(LanDiscoveryResponse resp)
    {
        string ip = resp.EndPoint.Address.ToString();
        string addr = $"{ip}:{resp.port}"; // ✅ ใช้ ip:port

        string modeLabel = LobbyManager.Instance ? LobbyManager.Instance.CurrentGameMode.ToString() : "-";

        // LobbyListSingleUI.Set(name, address, cur, max, mode, isPrivate)
        LobbyListUI.Instance?.AddOrUpdate(
            resp.lobbyName, addr,
            resp.curPlayers, resp.maxPlayers,
            modeLabel, resp.isPrivate
        );
    }

}

