using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    private static bool IsInstantDropResolveSkillMode(SkillMode mode)
    {
        switch (mode)
        {
            case SkillMode.DuckShuffle:
            case SkillMode.GivePeaceAChance:
            case SkillMode.Resurrection:
                return true;
            default:
                return false;
        }
    }

    [Server]
    private void ServerBroadcastInstantAbilitySfx(SkillMode mode)
    {
        RpcPlayInstantAbilitySfx((int)mode);
    }

    [ClientRpc]
    private void RpcPlayInstantAbilitySfx(int modeValue)
    {
        if (!NetworkClient.active)
            return;

        SkillMode mode = (SkillMode)modeValue;
        InstantAbilitySfx.NotifyActivated(mode);
    }
}
