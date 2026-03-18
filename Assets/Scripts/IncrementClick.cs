using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class IncrementClick : NetworkBehaviour
{
    public PlayerManager PlayerManager;

    [SyncVar]
    public int NumberOfClicks = 0;

    public void IncrementClicks()
    {
        if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
            return;

        // While targeting ducks for an active action skill, suppress duck click side-effects.
        if (TryGetComponent<DuckCard>(out _))
        {
            PlayerManager localPlayerManager = NetworkClient.connection.identity.GetComponent<PlayerManager>();
            if (localPlayerManager != null && localPlayerManager.activeSkillMode != SkillMode.None)
                return;
        }

        NetworkIdentity networkIdentity = NetworkClient.connection.identity;
        PlayerManager = networkIdentity.GetComponent<PlayerManager>();
        if (PlayerManager == null)
            return;

        PlayerManager.CmdIncrementClick(gameObject);
    }
}

