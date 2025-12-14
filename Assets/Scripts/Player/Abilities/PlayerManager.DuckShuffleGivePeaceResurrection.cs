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

    [Command]
    private void CmdActivateResurrectionMode()
    {
        const int maxPerColor = 5;

        string myColor = ColorIndexToDuckKey(duckColorIndex);
        if (string.IsNullOrEmpty(myColor) || myColor == "Marsh")
        {
            string m = $"[Resurrection] no effect (invalid myColor) duckColorIndex={duckColorIndex}";
            Debug.Log(m);
            TargetResurrectionLog(connectionToClient, m);
            return;
        }

        // pool count ก่อน
        int poolBefore = 0;
        var poolCounts = CardPoolManager.GetAllPoolCounts();
        if (poolCounts != null && poolCounts.TryGetValue(myColor, out int pb))
            poolBefore = pb;

        // zone count ก่อน (นับเฉพาะ DuckZone แถว 0 ที่เป็นสีของเรา)
        int zoneBefore = 0;
        var ducks = FindDucksInRow(0);
        foreach (var d in ducks)
        {
            string key = ExtractDuckKeyFromCard(d.gameObject);
            if (key == myColor) zoneBefore++;
        }

        int totalBefore = poolBefore + zoneBefore;

        // ถ้าครบแล้ว ไม่ทำอะไร
        if (totalBefore >= maxPerColor)
        {
            string m =
                $"[Resurrection] no effect color={myColor} (already {totalBefore}/{maxPerColor}) " +
                $"| pool {poolBefore}->{poolBefore} | zone {zoneBefore}->{zoneBefore}";
            Debug.Log(m);
            TargetResurrectionLog(connectionToClient, m);
            return;
        }

        // คืนชีพ 1 ใบ (เข้า pool)
        CardPoolManager.AddToPool(myColor);

        // pool count หลัง
        int poolAfter = poolBefore;
        poolCounts = CardPoolManager.GetAllPoolCounts();
        if (poolCounts != null && poolCounts.TryGetValue(myColor, out int pa))
            poolAfter = pa;

        int zoneAfter = zoneBefore; // zone ไม่เปลี่ยนจากการ add เข้า pool
        int totalAfter = poolAfter + zoneAfter;

        string msg =
            $"[Resurrection] revived color={myColor} " +
            $"| total {totalBefore}->{totalAfter} (max {maxPerColor}) " +
            $"| pool {poolBefore}->{poolAfter} | zone {zoneBefore}->{zoneAfter}";

        Debug.Log(msg);
        TargetResurrectionLog(connectionToClient, msg);
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
