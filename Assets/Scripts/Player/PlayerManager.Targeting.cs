using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // ===== Helpers: Target เก็บ/คืนตำแหน่ง =====
    [Server]
    private List<int> CollectTargetColumns()
    {
        List<int> targetColumns = new List<int>();
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            if (NetworkServer.spawned.TryGetValue(tf.targetNetId, out NetworkIdentity duckNi))
            {
                DuckCard duck = duckNi.GetComponent<DuckCard>();
                if (duck != null && duck.zone == ZoneKind.DuckZone && !targetColumns.Contains(duck.ColNet))
                {
                    targetColumns.Add(duck.ColNet);
                }
            }
        }
        targetColumns.Sort();
        return targetColumns;
    }

    [Server]
    private DuckCard FindLeftmostDuck(int row)
    {
        DuckCard result = null;
        int minCol = int.MaxValue;
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard d = netId.GetComponent<DuckCard>();
            if (d != null && d.zone == ZoneKind.DuckZone && d.RowNet == row)
            {
                if (d.ColNet < minCol)
                {
                    minCol = d.ColNet;
                    result = d;
                }
            }
        }
        return result;
    }

    [Server]
    private void RemoveAllTargets()
    {
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            NetworkServer.Destroy(tf.gameObject);
        }
    }

    [Server]
    private List<DuckCard> FindDucksInRow(int row)
    {
        List<DuckCard> list = new List<DuckCard>();
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard d = netId.GetComponent<DuckCard>();
            if (d != null && d.zone == ZoneKind.DuckZone && d.RowNet == row)
            {
                list.Add(d);
            }
        }
        return list;
    }

    [Server]
    private IEnumerator RefillAndRecreateTargets(List<int> oldTargetColumns)
    {
        yield return StartCoroutine(RefillNextFrameLineForward());
        yield return null; // ปล่อยเฟรมให้ layout ทำงาน

        List<DuckCard> ducks = FindDucksInRow(0); // ทำงานแถว 0
        foreach (int col in oldTargetColumns)
        {
            DuckCard duckAtCol = ducks.Find(d => d.ColNet == col);
            if (duckAtCol != null)
            {
                CmdSpawnTargetForDuck(duckAtCol.netId);
            }
        }
    }

    [Server]
    private IEnumerator RefillNextFrameLineForward()
    {
        yield return null;
        RefillDuckZoneIfNeededLineForward();
    }

    [Server]
    private void RefillDuckZoneIfNeededLineForward()
    {
        int currentCount = Server_CountCardsInZone(ZoneKind.DuckZone);
        if (currentCount >= 6) return;
        if (!CardPoolManager.HasCards()) return;

        int needed = 6 - currentCount;
        for (int i = 0; i < needed; i++)
        {
            GameObject newCard = CardPoolManager.DrawRandomCard();
            if (newCard == null) break;

            DuckCard dc = newCard.GetComponent<DuckCard>();
            if (dc != null)
            {
                int nextCol = currentCount + i;
                dc.ServerAssignToZone(ZoneKind.DuckZone, 0, nextCol);
            }

            NetworkServer.Spawn(newCard);
        }
    }
}
