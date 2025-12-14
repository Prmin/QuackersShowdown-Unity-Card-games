using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // ===== DuckShuffle =====
    [Command(requiresAuthority = false)]
    public void CmdActivateDuckShuffle()
    {
        var oldTargets = CollectTargetColumns();
        RemoveAllDucks();
        RemoveAllTargets();
        if (DuckZone == null) return;

        int needed = 6;
        for (int i = 0; i < needed; i++)
        {
            if (!CardPoolManager.HasCards()) break;
            GameObject cardGO = CardPoolManager.DrawRandomCard();
            if (cardGO == null) break;

            if (cardGO.TryGetComponent(out DuckCard duck))
            {
                duck.ServerAssignToZone(ZoneKind.DuckZone, 0, i);
            }
            NetworkServer.Spawn(cardGO);
        }
        StartCoroutine(RecreateTargetsAfterShuffle(oldTargets));
        StartCoroutine(DelayedLog());
    }

    [Server]
    private IEnumerator RecreateTargetsAfterShuffle(List<int> oldCols)
    {
        yield return null;
        Server_PushDuckZoneOrder(0);
        List<DuckCard> ducks = FindDucksInRow(0);
        foreach (int col in oldCols)
        {
            var duckAtCol = ducks.Find(d => d.ColNet == col);
            if (duckAtCol != null)
                CmdSpawnTargetForDuck(duckAtCol.netId);
        }
    }

    [Server]
    private void RemoveAllDucks()
    {
        List<GameObject> ducksToDestroy = new List<GameObject>();
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            if (netId.TryGetComponent(out DuckCard duck) && duck.zone == ZoneKind.DuckZone)
                ducksToDestroy.Add(duck.gameObject);
        }
        foreach (var duckGO in ducksToDestroy)
        {
            CardPoolManager.ReturnCard(duckGO);
            NetworkServer.Destroy(duckGO);
        }
        RefillDuckZoneIfNeeded();
    }

    // ===== GivePeaceAChance =====
    [Command(requiresAuthority = false)]
    private void CmdActivateGivePeaceAChance()
    {
        RemoveAllTargets();
    }


    // ===== Resurrection =====
    [Server]
    private void Server_ActivateResurrectionMode()
    {
        const int maxPerColor = 5;

        string myColor = ColorIndexToDuckKey(duckColorIndex);

        // pool ก่อน
        int poolBefore = CardPoolManager.GetAllPoolCounts().GetValueOrDefault(myColor, 0);

        // zone ก่อน (นับเฉพาะแถว 0 + สีตัวเอง)
        int zoneBefore = 0;
        foreach (var d in FindDucksInRow(0))
        {
            string key = ExtractDuckKeyFromCard(d.gameObject);
            if (key == myColor) zoneBefore++;
        }

        int totalBefore = poolBefore + zoneBefore;

        if (totalBefore >= maxPerColor)
        {
            Debug.Log(
                $"[Resurrection] no effect color={myColor} " +
                $"| total {totalBefore}->{totalBefore} (max {maxPerColor}) " +
                $"| pool {poolBefore}->{poolBefore} | zone {zoneBefore}->{zoneBefore} " +
                $"| from connId={connectionToClient?.connectionId} pmNetId={netId}"
            );
            return;
        }

        CardPoolManager.AddToPool(myColor);

        int poolAfter = CardPoolManager.GetAllPoolCounts().GetValueOrDefault(myColor, 0);
        int zoneAfter = zoneBefore;
        int totalAfter = poolAfter + zoneAfter;

        Debug.Log(
            $"[Resurrection] revived color={myColor} " +
            $"| total {totalBefore}->{totalAfter} (max {maxPerColor}) " +
            $"| pool {poolBefore}->{poolAfter} | zone {zoneBefore}->{zoneAfter} " +
            $"| from connId={connectionToClient?.connectionId} pmNetId={netId}"
        );
    }



    [TargetRpc]
    private void TargetResurrectionLog(NetworkConnectionToClient conn, string msg)
    {
        Debug.Log(msg);
    }



    [Server]
    private Dictionary<string, int> GetTotalDuckCounts()
    {
        Dictionary<string, int> counts = CardPoolManager.GetAllPoolCounts();
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard card = netId.GetComponent<DuckCard>();
            if (card != null && card.zone == ZoneKind.DuckZone)
            {
                string key = ExtractDuckKeyFromCard(card.gameObject);
                if (string.IsNullOrEmpty(key)) continue;
                if (!counts.ContainsKey(key))
                    counts[key] = 0;
                counts[key]++;
            }
        }
        return counts;
    }
}
