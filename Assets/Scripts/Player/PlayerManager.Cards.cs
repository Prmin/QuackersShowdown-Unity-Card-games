using System.Collections;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    [Server]
    private static NetworkConnectionToClient ServerResolveConnectionByPlayerNetId(uint ownerPMNetId)
    {
        if (ownerPMNetId != 0 &&
            NetworkServer.spawned.TryGetValue(ownerPMNetId, out NetworkIdentity ownerNi) &&
            ownerNi != null &&
            ownerNi.connectionToClient != null)
        {
            return ownerNi.connectionToClient;
        }

        // Host-mode player can be on localConnection (not always present in NetworkServer.connections).
        if (ownerPMNetId != 0 &&
            NetworkServer.localConnection != null &&
            NetworkServer.localConnection.identity != null &&
            NetworkServer.localConnection.identity.netId == ownerPMNetId)
        {
            return NetworkServer.localConnection;
        }

        foreach (var kv in NetworkServer.connections)
        {
            NetworkConnectionToClient conn = kv.Value;
            if (conn?.identity != null && conn.identity.netId == ownerPMNetId)
                return conn;
        }

        return null;
    }

    [Server]
    private static int ServerCountActionCardsInHandByOwner(uint ownerPMNetId)
    {
        int count = 0;
        foreach (DuckCard dc in FindObjectsOfType<DuckCard>())
        {
            if (dc == null || dc.zone != ZoneKind.PlayerArea)
                continue;
            if (dc.ownerNetId != ownerPMNetId)
                continue;

            count++;
        }

        return count;
    }

    [Server]
    private bool Server_DrawActionCardFor(NetworkConnectionToClient conn, uint ownerPMNetId)
    {
        if (conn == null)
        {
            conn = ServerResolveConnectionByPlayerNetId(ownerPMNetId);
            if (conn == null)
            {
                Debug.LogWarning($"[ActionPool] Missing connection for ownerNetId={ownerPMNetId}, skip draw.");
                return false;
            }
        }

        string cardName = GetRandomActionCardFromPool();
        if (string.IsNullOrEmpty(cardName))
        {
            Debug.LogWarning("[ActionPool] No action cards left in shared pool.");
            return false;
        }

        GameObject prefab = FindCardPrefabByName(cardName);
        if (prefab == null)
        {
            Debug.LogError($"[ActionPool] Missing action prefab for card={cardName}");
            return false;
        }

        GameObject spawnedCard = Instantiate(prefab);

        DuckCard dc = spawnedCard.GetComponent<DuckCard>();
        if (dc != null)
        {
            dc.ownerNetId = ownerPMNetId;
            int handCount = ServerCountActionCardsInHandByOwner(ownerPMNetId);
            dc.ServerAssignToZone(ZoneKind.PlayerArea, 0, handCount);
        }

        NetworkServer.Spawn(spawnedCard, conn);

        NetworkIdentity spawnedNi = spawnedCard.GetComponent<NetworkIdentity>();
        RpcShowCard(spawnedNi, "Dealt");
        return true;
    }

    public void DrawActionCard()
    {
        if (isLocalPlayer)
            CmdDrawActionCard();
    }

    [Command]
    public void CmdDrawActionCard()
    {
        Server_DrawActionCardFor(connectionToClient, netId);
    }

    [Server]
    private IEnumerator DrawNextCardCoroutine(NetworkConnectionToClient conn, uint ownerPMNetId)
    {
        yield return null;

        NetworkConnectionToClient resolvedConn = conn ?? ServerResolveConnectionByPlayerNetId(ownerPMNetId);
        if (resolvedConn == null)
        {
            Debug.LogWarning($"[ActionPool] DrawNextCard aborted, connection missing for ownerNetId={ownerPMNetId}");
            yield break;
        }

        int cardsInHand = ServerCountActionCardsInHandByOwner(ownerPMNetId);
        while (cardsInHand < 3 && ServerGetSharedActionPoolRemaining() > 0)
        {
            bool drew = Server_DrawActionCardFor(resolvedConn, ownerPMNetId);
            if (!drew)
                break;

            cardsInHand++;
            yield return null;
        }
    }

    [Server]
    private void RemoveCardFromGame(GameObject card)
    {
        if (card == null)
            return;

        NetworkServer.Destroy(card);
    }
}
