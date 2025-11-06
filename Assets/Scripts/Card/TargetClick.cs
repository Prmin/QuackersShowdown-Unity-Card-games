using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using Mirror;

public class TargetClick : NetworkBehaviour
{
    // ไม่ต้อง Assign ใน Inspector ก็ได้
    // เพราะเราจะหา PlayerManager ของ Local Player ตอนกด
    // public PlayerManager PlayerManager;

    /// <summary>
    /// เรียกจาก UI Event หรือ EventTrigger เป็นต้น
    /// </summary>
    public void OnTargetClick()
    {
        // 1) ตรวจว่ามี local connection หรือเปล่า
        if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
        {
            Debug.LogWarning("[TargetClick] No local player identity found (maybe not connected?).");
            return;
        }

        // 2) ดึง PlayerManager ของ Local Player
        PlayerManager localPM = NetworkClient.connection.identity.GetComponent<PlayerManager>();
        if (localPM == null)
        {
            Debug.LogWarning("[TargetClick] Local PlayerManager not found!");
            return;
        }

        // 3) ดูว่า Object นี้เป็นของใคร
        //    - isOwned=true แปลว่า Local Player เป็นเจ้าของ (owner)
        //    - ถ้า false แสดงว่าเป็นของผู้เล่นอื่น หรือไม่มีการ Assign Authority ให้
        Debug.Log($"[TargetClick] OnTargetClick => isOwned={isOwned}, gameObject={gameObject.name}");

        // 4) เรียก Command ฝั่ง Server 
        //    *หมายเหตุ* ต้องแน่ใจว่าใน PlayerManager คุณเขียน:
        //      [Command(requiresAuthority = false)] 
        //    ในทั้งสองเมธอด CmdTargetSelfCard / CmdTargetOtherCard
        if (isOwned)
        {
            localPM.CmdTargetSelfCard();
        }
        else
        {
            localPM.CmdTargetOtherCard(gameObject);
        }
    }
}
