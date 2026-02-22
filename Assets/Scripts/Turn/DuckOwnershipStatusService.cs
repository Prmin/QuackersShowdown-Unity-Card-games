using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class DuckOwnershipStatusService : NetworkBehaviour
{
    public static DuckOwnershipStatusService Instance { get; private set; }

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.5f;

    private double _nextRefreshAt;

    private static readonly string[] DuckKeysByIndex =
    {
        "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerForceRefreshNow("OnStartServer");
    }

    [ServerCallback]
    private void Update()
    {
        if (NetworkTime.time < _nextRefreshAt)
            return;

        _nextRefreshAt = NetworkTime.time + Mathf.Max(0.1f, refreshIntervalSeconds);
        ServerRefreshOwnedDuckCounts();
    }

    [Server]
    public void ServerForceRefreshNow(string reason = null)
    {
        _nextRefreshAt = 0d;
        ServerRefreshOwnedDuckCounts();

        if (!string.IsNullOrWhiteSpace(reason))
            Debug.Log($"[DuckOwnershipStatusService] ForceRefresh reason={reason}");
    }

    [Server]
    private void ServerRefreshOwnedDuckCounts()
    {
        Dictionary<string, int> totalsByKey = ServerBuildTotalDuckCounts();

        foreach (var kv in NetworkServer.connections)
        {
            NetworkConnectionToClient conn = kv.Value;
            if (conn == null || conn.identity == null)
                continue;

            PlayerManager pm = conn.identity.GetComponent<PlayerManager>();
            if (pm == null || !pm.isActiveAndEnabled || pm.SeatIndex < 0)
                continue;

            string key = DuckKeyFromColorIndex(pm.duckColorIndex);
            int count = 0;
            if (!string.IsNullOrEmpty(key))
                totalsByKey.TryGetValue(key, out count);

            pm.ServerSetOwnedDuckCount(count);
        }

        TurnManager tm = TurnManager.Instance;
        if (tm != null)
            tm.ServerEvaluateMatchEnd(totalsByKey, "DuckOwnershipRefresh");
    }

    [Server]
    private static Dictionary<string, int> ServerBuildTotalDuckCounts()
    {
        Dictionary<string, int> totals = CardPoolManager.GetAllPoolCounts();

        foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
        {
            if (ni == null || !ni.TryGetComponent(out DuckCard dc))
                continue;
            if (dc.zone != ZoneKind.DuckZone)
                continue;

            string key = ExtractDuckKeyFromName(dc.name);
            if (string.IsNullOrEmpty(key))
                continue;

            if (!totals.ContainsKey(key))
                totals[key] = 0;

            totals[key] += 1;
        }

        return totals;
    }

    private static string DuckKeyFromColorIndex(int idx)
    {
        return (idx >= 0 && idx < DuckKeysByIndex.Length) ? DuckKeysByIndex[idx] : null;
    }

    private static string ExtractDuckKeyFromName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return null;

        string cleanName = rawName.Replace("(Clone)", string.Empty).Trim();
        foreach (string key in DuckKeysByIndex)
        {
            if (cleanName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                return key;
        }

        return null;
    }
}
