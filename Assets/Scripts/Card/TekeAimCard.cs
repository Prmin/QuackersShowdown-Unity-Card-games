using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class TekeAimCard : NetworkBehaviour
{
    // (ถ้าอยากอ้างอิงอะไรใน PlayerManager ก็ใส่ได้)
    // public PlayerManager PlayerMgr; // อาจไม่ต้องถ้าเราจะหาเองผ่าน NetworkClient

    // ฟังก์ชันนี้จะถูกเรียก เมื่อเราคลิกการ์ด TekeAim
    public void OnTekeAimCardClicked()
    {
        Debug.Log($"[TekeAimCard] {gameObject.name} was clicked!");

        // 1) หาตัว PlayerManager ของ LocalPlayer (ผ่าน NetworkClient.connection.identity)
        if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
        {
            Debug.LogError("No local player identity found! (NetworkClient.connection.identity is null)");
            return;
        }
        PlayerManager localPlayerManager = NetworkClient.connection.identity.GetComponent<PlayerManager>();
        if (localPlayerManager == null)
        {
            Debug.LogError("Local player identity found, but no PlayerManager component on it!");
            return;
        }

        // 2) สมมติจะเรียก [Command] ที่อยู่ใน PlayerManager
        Debug.Log("[TekeAimCard] Calling CmdActivateTekeAim on the server...");
        localPlayerManager.CmdActivateTekeAim();
    }
}

