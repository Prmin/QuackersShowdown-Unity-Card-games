using System.Collections;
using System.Collections.Generic;
using Mirror;
using Mirror.Discovery;
using UnityEngine;

public class DiscoveryBridge : MonoBehaviour
{
    public static DiscoveryBridge I { get; private set; }

    [Header("Refs")]
    public MyNetworkDiscovery discovery;
    public LobbyListUI listUI;

    private readonly HashSet<string> seen = new();

    void Awake()
    {
        if (I && I != this)
        {
            Destroy(gameObject);
            return;
        }

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

        StartCoroutine(ScanTimeout());
    }

    IEnumerator ScanTimeout()
    {
        yield return new WaitForSeconds(1f);
    }

    public void StopClientScan()
    {
        if (discovery == null)
            return;

        // Keep host advertising alive; stop only client-side scanning.
        if (NetworkServer.active)
            return;

        discovery.StopDiscovery();
    }

    public void AdvertiseIfHost()
    {
        if (discovery && NetworkServer.active)
            discovery.AdvertiseServer();
    }

    void OnFound(LanDiscoveryResponse resp)
    {
        string ip = resp.EndPoint.Address.ToString();
        string addr = $"{ip}:{resp.port}";
        string serverKey = resp.serverId != 0 ? $"server:{resp.serverId}" : addr;

        seen.Add(serverKey);

        LobbyListUI ui = ResolveListUI();
        if (!ui)
            return;

        if (resp.isInGameplay)
        {
            ui.Remove(serverKey);
            return;
        }

        string modeLabel = LobbyManager.Instance ? LobbyManager.Instance.CurrentGameMode.ToString() : "-";
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
