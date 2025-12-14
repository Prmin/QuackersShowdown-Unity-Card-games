using System.Collections.Generic;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // ===== Misfire =====
    [Command(requiresAuthority = false)]
    public void CmdMisfireClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;

        DuckCard duckComp = clickedCard.GetComponent<DuckCard>();
        if (duckComp == null) return;

        int row = duckComp.RowNet;
        int col = duckComp.ColNet;
        List<NetworkIdentity> neighbors = GetAdjacentDuckCards(row, col);
        if (neighbors.Count == 0) return;

        var randomNeighbor = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
        uint clickedId = clickedCard.netId;
        uint shotId = randomNeighbor.netId;

        ShootCardDirect(randomNeighbor);

        // ✅ ลบเป้าของ "ใบที่โดนทำลายจริงๆ"
        Server_DestroyAllTargetsFor(shotId);

        // (เหมือนเดิม) ลบเป้าของใบที่คลิก เพื่อ consume การเล็ง
        Server_DestroyAllTargetsFor(clickedId);

        // แนะนำ: กันคอลัมน์เป็นรูหลังยิง
        Server_ResequenceDuckZoneColumns();

        activeSkillMode = SkillMode.None;
        StartCoroutine(RefillNextFrame());
    }

    private List<NetworkIdentity> GetAdjacentDuckCards(int row, int col)
    {
        List<NetworkIdentity> results = new List<NetworkIdentity>();
        foreach (NetworkIdentity netId in NetworkServer.spawned.Values)
        {
            DuckCard duck = netId.GetComponent<DuckCard>();
            if (duck == null || duck.zone != ZoneKind.DuckZone) continue;
            if (duck.RowNet == row && Mathf.Abs(duck.ColNet - col) == 1)
            {
                results.Add(netId);
            }
        }
        return results;
    }

    private void ShootCardDirect(NetworkIdentity duckNi)
    {
        if (duckNi == null) return;
        NetworkServer.Destroy(duckNi.gameObject);
    }

    // ===== TwoBirds =====
    [Command(requiresAuthority = false)]
    public void CmdTwoBirdsClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;

        var dcClicked = clickedCard.GetComponent<DuckCard>();
        if (dcClicked == null || dcClicked.zone != ZoneKind.DuckZone)
        {
            Debug.LogWarning($"[TwoBirds] Ignore click: {clickedCard.name} not in DuckZone (zone={dcClicked?.zone})");
            return;
        }

        if (twoBirdsClickCount == 0)
        {
            firstTwoBirdsCard = clickedCard;
            twoBirdsClickCount = 1;
        }
        else if (twoBirdsClickCount == 1)
        {
            bool canShootBoth = false;
            if (firstTwoBirdsCard != null)
                canShootBoth = CheckAdjacentTwoBirds(firstTwoBirdsCard, clickedCard);

            if (canShootBoth)
            {
                DuckCard dc1 = firstTwoBirdsCard.GetComponent<DuckCard>();
                DuckCard dc2 = clickedCard.GetComponent<DuckCard>();
                if (dc1 == null || dc2 == null) { /* ... */ }
                int row1 = dc1.RowNet, col1 = dc1.ColNet;
                int row2 = dc2.RowNet, col2 = dc2.ColNet;
                NetworkServer.Destroy(firstTwoBirdsCard.gameObject);
                NetworkServer.Destroy(clickedCard.gameObject);
                RemoveTargetFromCard(firstTwoBirdsCard);
                RemoveTargetFromCard(clickedCard);
                if (col1 > col2) { ShiftColumnsDown(row1, col1); ShiftColumnsDown(row2, col2); }
                else { ShiftColumnsDown(row2, col2); ShiftColumnsDown(row1, col1); }
                StartCoroutine(RefillNextFrame());
            }
            else
            {
                if (firstTwoBirdsCard != null)
                {
                    DuckCard dc1 = firstTwoBirdsCard.GetComponent<DuckCard>();
                    if (dc1 != null)
                    {
                        int row1 = dc1.RowNet, col1 = dc1.ColNet;
                        NetworkServer.Destroy(firstTwoBirdsCard.gameObject);
                        RemoveTargetFromCard(firstTwoBirdsCard);
                        ShiftColumnsDown(row1, col1);
                        StartCoroutine(RefillNextFrame());
                    }
                }
            }

            activeSkillMode = SkillMode.None;
            twoBirdsClickCount = 0;
            firstTwoBirdsCard = null;
        }
    }

    [Server]
    private bool CheckAdjacentTwoBirds(NetworkIdentity card1, NetworkIdentity card2)
    {
        DuckCard dc1 = card1.GetComponent<DuckCard>();
        DuckCard dc2 = card2.GetComponent<DuckCard>();
        if (dc1 == null || dc2 == null) return false;
        if (dc1.RowNet == dc2.RowNet && Mathf.Abs(dc1.ColNet - dc2.ColNet) == 1)
            return true;
        return false;
    }
}
