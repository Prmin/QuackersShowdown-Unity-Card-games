using System.Collections.Generic;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // ===== DoubleBarrel =====
    [Command(requiresAuthority = false)]
    public void CmdDoubleBarrelClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        var dc = clickedCard.GetComponent<DuckCard>();
        if (dc == null || dc.zone != ZoneKind.DuckZone)
        {
            Debug.LogWarning($"[DoubleBarrel] Ignore click: {clickedCard.name} is not in DuckZone (zone={dc?.zone})");
            return;
        }

        if (doubleBarrelClickCount == 0)
        {
            firstClickedCard = clickedCard;
            doubleBarrelClickCount = 1;
        }
        else if (doubleBarrelClickCount == 1)
        {
            if (firstClickedCard == null)
            {
                doubleBarrelClickCount = 0;
                return;
            }
            if (!CheckAdjacent(firstClickedCard, clickedCard))
            {
                return;
            }
            CmdSpawnTargetDoubleBarrel_Internal(firstClickedCard);
            CmdSpawnTargetDoubleBarrel_Internal(clickedCard);

            activeSkillMode = SkillMode.None;
            doubleBarrelClickCount = 0;
            firstClickedCard = null;
        }
    }

    [Server]
    private void CmdSpawnTargetDoubleBarrel_Internal(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null || targetPrefab == null) return;
        var dc = duckCardIdentity.GetComponent<DuckCard>();
        if (dc == null) return;
        RemoveTargetFromCard(duckCardIdentity); // ถ้ามี Target เดิมอยู่แล้ว เอาออกก่อน
        GameObject newTarget = Instantiate(targetPrefab);
        var marker = newTarget.GetComponent<TargetMarker>();
        if (marker != null)
        {
            marker.ServerAssignToZone(ZoneKind.TargetZone, 0, dc.ColNet);
            marker.FollowDuckNetId = duckCardIdentity.netId;
        }
        NetworkServer.Spawn(newTarget);
    }

    [Server]
    private bool CheckAdjacent(NetworkIdentity card1, NetworkIdentity card2)
    {
        if (card1 == null || card2 == null) return false;
        var duck1 = card1.GetComponent<DuckCard>();
        var duck2 = card2.GetComponent<DuckCard>();
        if (duck1 == null || duck2 == null) return false;
        if (duck1.RowNet != duck2.RowNet) return false;
        int diff = Mathf.Abs(duck1.ColNet - duck2.ColNet);
        return diff == 1;
    }

    // ===== QuickShot =====
    [Command(requiresAuthority = false)]
    public void CmdQuickShotCard(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null) return;
        DuckCard shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null) return;

        int shotRow = shotDuck.RowNet;
        int shotCol = shotDuck.ColNet;
        NetworkServer.Destroy(duckCardIdentity.gameObject);

        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var target in allTargets)
        {
            if (target.targetNetId == duckCardIdentity.netId)
                NetworkServer.Destroy(target.gameObject);
        }

        ShiftColumnsDown(shotRow, shotCol);

        activeSkillMode = SkillMode.None;
        StartCoroutine(RefillNextFrame());
    }
}
