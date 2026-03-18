using System;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    private static bool IsShootingSkillMode(SkillMode mode)
    {
        switch (mode)
        {
            case SkillMode.Shoot:
            case SkillMode.TakeAim:
            case SkillMode.DoubleBarrel:
            case SkillMode.QuickShot:
            case SkillMode.Misfire:
            case SkillMode.TwoBirds:
            case SkillMode.BumpLeft:
            case SkillMode.BumpRight:
                return true;
            default:
                return false;
        }
    }

    private static bool IsAimSkillMode(SkillMode mode)
    {
        switch (mode)
        {
            case SkillMode.TakeAim:
            case SkillMode.DoubleBarrel:
            case SkillMode.BumpLeft:
            case SkillMode.BumpRight:
                return true;
            default:
                return false;
        }
    }

    [Server]
    private void ServerBroadcastShotResolvedSfx(int duckHitCount, int marshHitCount = 0)
    {
        int safeDuck = Mathf.Clamp(duckHitCount, 0, 3);
        int safeMarsh = Mathf.Clamp(marshHitCount, 0, 3);
        RpcPlayShotResolvedSfx(safeDuck, safeMarsh);
    }

    [ClientRpc]
    private void RpcPlayShotResolvedSfx(int duckHitCount, int marshHitCount)
    {
        if (!NetworkClient.active)
            return;

        ShootActionSfx.NotifyShotResolved(duckHitCount, marshHitCount);
    }

    [Server]
    private void ServerBroadcastAimSkillActivatedSfx()
    {
        RpcPlayAimSkillActivatedSfx();
    }

    [ClientRpc]
    private void RpcPlayAimSkillActivatedSfx()
    {
        if (!NetworkClient.active)
            return;

        ShootActionSfx.NotifyAimSkillActivated();
    }

    [Server]
    private static bool IsMarshShotTarget(GameObject cardObject)
    {
        if (cardObject == null)
            return false;

        string duckKey = ExtractDuckKeyFromCard(cardObject);
        return string.Equals(duckKey, "Marsh", StringComparison.OrdinalIgnoreCase);
    }
}
