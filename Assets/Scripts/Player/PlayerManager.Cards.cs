using System.Collections;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // ===== การจัดการการ์ดในมือ/จั่ว =====

    [Server]
    private void Server_DrawActionCardFor(NetworkConnectionToClient conn, uint ownerPMNetId)
    {
        string cardName = GetRandomActionCardFromPool();
        if (string.IsNullOrEmpty(cardName))
        {
            Debug.LogWarning("ไม่มีการ์ดแอคชั่นเหลือในกอง");
            return;
        }

        GameObject prefab = FindCardPrefabByName(cardName);
        if (prefab == null)
        {
            Debug.LogError($"หา prefab การ์ดไม่เจอ: {cardName}");
            return;
        }

        GameObject spawnedCard = Instantiate(prefab);

        var dc = spawnedCard.GetComponent<DuckCard>();
        if (dc != null)
        {
            dc.ownerNetId = ownerPMNetId;
            int handCount = Server_CountCardsInZone(ZoneKind.PlayerArea, conn);
            dc.ServerAssignToZone(ZoneKind.PlayerArea, 0, handCount);
        }

        // ✅ Spawn หลัง set owner/zone เสมอ
        NetworkServer.Spawn(spawnedCard, conn);


        var spawnedNi = spawnedCard.GetComponent<NetworkIdentity>();
        RpcShowCard(spawnedNi, "Dealt");
    }

    // เรียกจาก client (local)
    public void DrawActionCard()
    {
        if (isLocalPlayer)
        {
            CmdDrawActionCard();
        }
    }

    [Command]
    public void CmdDrawActionCard()
    {
        Server_DrawActionCardFor(connectionToClient, netId);
    }

    [Server]
    private IEnumerator DrawNextCardCoroutine(NetworkConnectionToClient conn)
    {
        // รอ 1 เฟรมให้การ์ดที่เพิ่งเล่นอัปเดต zone เสร็จก่อน
        yield return null;

        int cardsInHand = Server_CountCardsInZone(ZoneKind.PlayerArea, conn);
        while (cardsInHand < 3 && CardPoolManager.HasCards())
        {
            uint ownerPMNetId = conn.identity.netId;
            Server_DrawActionCardFor(conn, ownerPMNetId);
            cardsInHand++;
            yield return null; // ปล่อยให้ Spawn/SyncVar กระจายก่อนจั่วถัดไป
        }
    }

    [Server]
    private void RemoveCardFromGame(GameObject card)
    {
        if (card == null) return;
        NetworkServer.Destroy(card);
        ;
    }
}

