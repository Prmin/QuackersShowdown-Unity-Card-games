using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    [Server]
    private void ServerBroadcastDuckMoveAbilitySfx(int flapCount = 1)
    {
        RpcPlayDuckMoveAbilitySfx(Mathf.Clamp(flapCount, 1, 4));
    }

    [ClientRpc]
    private void RpcPlayDuckMoveAbilitySfx(int flapCount)
    {
        if (!NetworkClient.active)
            return;

        DuckAbilityMoveSfx.NotifyWingFlap(flapCount);
    }
}
