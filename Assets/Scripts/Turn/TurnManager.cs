using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance { get; private set; }

    // Authoritative turn order (netId list) replicated to clients.
    public readonly SyncList<uint> TurnOrder = new SyncList<uint>();

    [SyncVar(hook = nameof(OnCurrentTurnIndexChanged))]
    public int currentTurnIndex = -1;

    [SyncVar(hook = nameof(OnCurrentTurnNetIdChanged))]
    public uint currentTurnNetId = 0;

    private static readonly string[] DuckKeysByIndex =
    {
        "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    };

    public static string DuckKeyFromIndex(int idx)
    {
        return (idx >= 0 && idx < DuckKeysByIndex.Length) ? DuckKeysByIndex[idx] : "-";
    }

    private static string DuckKeyFromCardName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
            return null;

        string name = cardName.Replace("(Clone)", string.Empty).Trim();
        if (name.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Marsh";

        foreach (string key in DuckKeysByIndex)
        {
            if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                return key;
        }

        return null;
    }

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
        ServerRebuildTurnOrder("OnStartServer");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        TurnOrder.Callback += OnTurnOrderChangedClient;
        PlayerManager.RequestTurnOrderLayoutRefresh("TurnManager.OnStartClient");
    }

    public override void OnStopClient()
    {
        TurnOrder.Callback -= OnTurnOrderChangedClient;
        base.OnStopClient();
    }

    private void OnCurrentTurnIndexChanged(int oldValue, int newValue)
    {
        if (!NetworkClient.active) return;
        PlayerManager.RequestTurnOrderLayoutRefresh($"TurnManager.CurrentTurnIndex {oldValue}->{newValue}");
    }

    private void OnCurrentTurnNetIdChanged(uint oldValue, uint newValue)
    {
        if (!NetworkClient.active) return;
        PlayerManager.RequestTurnOrderLayoutRefresh($"TurnManager.CurrentTurnNetId {oldValue}->{newValue}");
    }

    private void OnTurnOrderChangedClient(SyncList<uint>.Operation op, int itemIndex, uint oldItem, uint newItem)
    {
        if (!NetworkClient.active) return;
        PlayerManager.RequestTurnOrderLayoutRefresh($"TurnManager.TurnOrder.{op}");
    }

    [Server]
    public void ServerRebuildTurnOrder(string reason = null)
    {
        uint keepNetId = ServerGetCurrentTurnNetId_Internal();

        var players = new List<PlayerManager>();
        foreach (var kv in NetworkServer.connections)
        {
            NetworkConnectionToClient conn = kv.Value;
            if (conn == null || conn.identity == null) continue;

            PlayerManager pm = conn.identity.GetComponent<PlayerManager>();
            if (pm == null) continue;
            if (pm.SeatIndex < 0) continue;
            if (!pm.isActiveAndEnabled) continue;

            players.Add(pm);
        }

        players.Sort((a, b) =>
        {
            int seatCompare = a.SeatIndex.CompareTo(b.SeatIndex);
            if (seatCompare != 0) return seatCompare;
            return a.netId.CompareTo(b.netId);
        });

        List<int> duplicateSeats = players
            .GroupBy(p => p.SeatIndex)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateSeats.Count > 0)
            Debug.LogWarning($"[TurnManager] Duplicate SeatIndex detected: {string.Join(", ", duplicateSeats)} (tie-break by netId)");

        TurnOrder.Clear();
        foreach (PlayerManager pm in players)
            TurnOrder.Add(pm.netId);

        if (TurnOrder.Count == 0)
        {
            currentTurnIndex = -1;
            currentTurnNetId = 0;
        }
        else
        {
            int idx = keepNetId != 0 ? TurnOrder.IndexOf(keepNetId) : -1;
            if (idx < 0) idx = 0;
            currentTurnIndex = idx;
            currentTurnNetId = TurnOrder[currentTurnIndex];
        }

        ServerLogTurnOrder(reason);
        ServerRequestClientLayoutRefresh($"Rebuild:{reason ?? "-"}");
    }

    [Server]
    public void ServerPickStarterFromDuckZoneAndRotate(string reason = null)
    {
        if (TurnOrder.Count == 0)
        {
            Debug.LogWarning($"[TurnManager] Skip rotate because TurnOrder is empty. reason={reason}");
            return;
        }

        if (!ServerFindStarterNetIdFromDuckZone(out uint starterNetId, out DuckCard frontDuck, out string starterBy, out string frontDuckKey))
        {
            Debug.LogWarning($"[TurnManager] Starter not found from DuckZone. reason={reason}");
            return;
        }

        int starterIndex = TurnOrder.IndexOf(starterNetId);
        if (starterIndex < 0)
        {
            Debug.LogWarning($"[TurnManager] Starter netId={starterNetId} not found in TurnOrder. reason={reason}");
            return;
        }

        if (starterIndex != 0)
        {
            var rotated = new List<uint>(TurnOrder.Count);
            for (int i = 0; i < TurnOrder.Count; i++)
                rotated.Add(TurnOrder[(starterIndex + i) % TurnOrder.Count]);

            TurnOrder.Clear();
            foreach (uint id in rotated)
                TurnOrder.Add(id);
        }

        currentTurnIndex = 0;
        currentTurnNetId = TurnOrder[0];

        string starterSeat = "-1";
        string starterDuckKey = "-";
        if (NetworkServer.spawned.TryGetValue(starterNetId, out NetworkIdentity starterNi) &&
            starterNi != null &&
            starterNi.TryGetComponent(out PlayerManager starterPm))
        {
            starterSeat = starterPm.SeatIndex.ToString();
            starterDuckKey = DuckKeyFromIndex(starterPm.duckColorIndex);
        }

        Debug.Log(
            $"[TurnManager] Starter picked reason={reason ?? "-"} by={starterBy} " +
            $"frontDuckNetId={(frontDuck != null ? frontDuck.netId.ToString() : "-")} frontDuckKey={frontDuckKey ?? "-"} " +
            $"starterNetId={starterNetId} starterSeatIndex={starterSeat} starterDuckKey={starterDuckKey}"
        );

        ServerLogTurnOrder($"Rotate:{reason ?? "-"}");
        ServerRequestClientLayoutRefresh($"Rotate:{reason ?? "-"}");
    }

    [Server]
    public void ServerRequestClientLayoutRefresh(string reason = null)
    {
        RpcRequestClientLayoutRefresh(reason ?? "-");
    }

    [ClientRpc]
    private void RpcRequestClientLayoutRefresh(string reason)
    {
        if (!NetworkClient.active) return;
        PlayerManager.RequestTurnOrderLayoutRefresh($"TurnManagerRpc:{reason}");
    }

    [Server]
    private bool ServerFindStarterNetIdFromDuckZone(out uint starterNetId, out DuckCard frontDuck, out string starterBy, out string frontDuckKey)
    {
        starterNetId = 0;
        frontDuck = null;
        starterBy = "-";
        frontDuckKey = "-";

        var ducksInDuckZone = new List<DuckCard>();
        foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
        {
            if (ni == null) continue;
            if (!ni.TryGetComponent(out DuckCard dc)) continue;
            if (dc.zone != ZoneKind.DuckZone) continue;
            ducksInDuckZone.Add(dc);
        }

        ducksInDuckZone.Sort((a, b) =>
        {
            int col = a.ColNet.CompareTo(b.ColNet); // Front-most first
            if (col != 0) return col;
            int zoneIdx = a.zoneIndex.CompareTo(b.zoneIndex);
            if (zoneIdx != 0) return zoneIdx;
            return a.netId.CompareTo(b.netId);
        });

        foreach (DuckCard dc in ducksInDuckZone)
        {
            frontDuck = dc;
            frontDuckKey = DuckKeyFromCardName(dc.name) ?? "-";

            if (dc.ownerNetId != 0 && TurnOrder.Contains(dc.ownerNetId))
            {
                starterNetId = dc.ownerNetId;
                starterBy = "ownerNetId";
                return true;
            }

            uint byDuckKey = ServerFindPlayerNetIdByDuckKey(frontDuckKey);
            if (byDuckKey != 0 && TurnOrder.Contains(byDuckKey))
            {
                starterNetId = byDuckKey;
                starterBy = "duckKey";
                return true;
            }
        }

        return false;
    }

    [Server]
    private static uint ServerFindPlayerNetIdByDuckKey(string duckKey)
    {
        if (string.IsNullOrWhiteSpace(duckKey) || duckKey == "-")
            return 0;

        foreach (var kv in NetworkServer.connections)
        {
            NetworkConnectionToClient conn = kv.Value;
            if (conn == null || conn.identity == null) continue;
            if (!conn.identity.TryGetComponent(out PlayerManager pm)) continue;
            if (!pm.isActiveAndEnabled || pm.SeatIndex < 0) continue;

            if (DuckKeyFromIndex(pm.duckColorIndex) == duckKey)
                return pm.netId;
        }

        return 0;
    }

    [Server]
    public uint ServerGetCurrentTurnNetId() => ServerGetCurrentTurnNetId_Internal();

    [Server]
    public PlayerManager ServerGetCurrentTurnPlayer()
    {
        uint id = ServerGetCurrentTurnNetId_Internal();
        if (id == 0) return null;

        return NetworkServer.spawned.TryGetValue(id, out NetworkIdentity ni) ? ni.GetComponent<PlayerManager>() : null;
    }

    [Server]
    public int ServerGetCurrentTurnSeatIndex()
    {
        PlayerManager pm = ServerGetCurrentTurnPlayer();
        return pm != null ? pm.SeatIndex : -1;
    }

    [Server]
    private uint ServerGetCurrentTurnNetId_Internal()
    {
        if (currentTurnIndex < 0 || currentTurnIndex >= TurnOrder.Count) return 0;
        return TurnOrder[currentTurnIndex];
    }

    [Server]
    private void ServerLogTurnOrder(string reason)
    {
        var lines = new List<string>
        {
            $"[TurnManager] TurnOrder reason={reason ?? "-"} count={TurnOrder.Count} currentTurnIndex={currentTurnIndex} currentTurnNetId={currentTurnNetId}"
        };

        for (int i = 0; i < TurnOrder.Count; i++)
        {
            uint id = TurnOrder[i];
            int seatIndex = -1;
            int duckColorIndex = -1;
            string duckKey = "-";

            if (NetworkServer.spawned.TryGetValue(id, out NetworkIdentity ni) &&
                ni != null &&
                ni.TryGetComponent(out PlayerManager pm))
            {
                seatIndex = pm.SeatIndex;
                duckColorIndex = pm.duckColorIndex;
                duckKey = DuckKeyFromIndex(duckColorIndex);
            }

            lines.Add(
                $"  Turn#{i + 1} | seatIndex={seatIndex} | netId={id} | duckColorIndex={duckColorIndex} | duckKey={duckKey}"
            );
        }

        Debug.Log(string.Join("\n", lines));
    }
}
