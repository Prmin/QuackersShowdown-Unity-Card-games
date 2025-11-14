using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class TekeAimCard : NetworkBehaviour
{
    public void OnTekeAimCardClicked()
    {
        Debug.Log($"[TekeAimCard] {gameObject.name} was clicked!");

        // 1) หา PlayerManager ของเรา
        if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
        {
            Debug.LogError("No local player identity found!");
            return;
        }
        PlayerManager localPlayerManager = NetworkClient.connection.identity.GetComponent<PlayerManager>();
        if (localPlayerManager == null)
        {
            Debug.LogError("Local player identity found, but no PlayerManager component on it!");
            return;
        }

        // 2) (FIX) เรียกใช้ระบบ SkillMode ใหม่
        Debug.Log("[TekeAimCard] Calling CmdSetSkillMode(SkillMode.TakeAim) on the server...");
        localPlayerManager.CmdSetSkillMode(SkillMode.TakeAim);
    }
}