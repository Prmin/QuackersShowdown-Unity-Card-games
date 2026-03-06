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
        if (!discovery)
        {
            Debug.LogWarning("[DiscoveryBridge] Missing ref: discovery.");
            return;
        }

        seen.Clear();
        ResolveListUI()?.ClearList();

        discovery.StopDiscovery();
        discovery.StartDiscovery();
        // ;

        StartCoroutine(ScanTimeout());
    }

    IEnumerator ScanTimeout()
    {
        yield return new WaitForSeconds(1f);
        // if (seen.Count == 0)
        //     Debug.LogWarning("[DiscoveryBridge] No servers found in scan.");
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
        string serverKey = resp.serverId != 0 ? $"server:{resp.serverId}" : addr;

        seen.Add(serverKey);

        string modeLabel = LobbyManager.Instance ? LobbyManager.Instance.CurrentGameMode.ToString() : "-";

        // LobbyListSingleUI.Set(name, address, cur, max, mode, isPrivate)
        LobbyListUI ui = ResolveListUI();
        if (ui)
            ui.AddOrUpdate(serverKey, resp.lobbyName, addr, resp.curPlayers, resp.maxPlayers, modeLabel, resp.isPrivate);
    }

    LobbyListUI ResolveListUI()
    {
        if (listUI)
            return listUI;

        if (LobbyListUI.Instance)
        {
            listUI = LobbyListUI.Instance;
            return listUI;
        }

        listUI = FindObjectOfType<LobbyListUI>(true);
        return listUI;
    }
}

