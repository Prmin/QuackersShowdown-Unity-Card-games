using System.Collections.Generic;
using Mirror;
using UnityEngine;

public partial class PlayerManager : NetworkBehaviour
{
    public int SeatIndex => seatIndex; // สมมติมีตัวแปร seat เก็บเลขที่นั่ง

    // ====== Server-hand state ======
    // ใช้เก็บคีย์การ์ดจริงบนมือบนเซิร์ฟเวอร์เท่านั้น (client ไม่ได้ใช้ตรง ๆ)
    readonly List<string> _serverActionHand = new List<string>();

    // จำนวนการ์ดบนมือ sync ให้ทุก client
    [SyncVar(hook = nameof(OnActionHandCountChanged))]
    int _actionHandCount;

    public int ActionHandCount => _actionHandCount;

    // ====== Server API ======

    // เรียกจากโค้ดเดิมที่เคย "จั่วการ์ดแอคชั่น" แทนที่จะไป spawn การ์ด network
    [Server]
    public void Server_AddActionCardToHand(string cardKey)
    {
        _serverActionHand.Add(cardKey);
        _actionHandCount = _serverActionHand.Count;

        if (connectionToClient != null)
        {
            // ส่งให้เจ้าของจั่วการ์ดในมือ (local-only)
            TargetRpc_ReceiveActionCard(connectionToClient, cardKey);
        }
    }

    [Server]
    public void Server_RemoveActionCardFromHand(string cardKey)
    {
        _serverActionHand.Remove(cardKey);
        _actionHandCount = _serverActionHand.Count;
    }

    // เวลาเล่นการ์ดจากมือ
    [Command]
    public void CmdPlayActionCard(string cardKey)
    {
        if (!_serverActionHand.Contains(cardKey))
        {
            Debug.LogWarning($"[CmdPlayActionCard] {name} tried to play card '{cardKey}' but it is not in server hand");
            return;
        }

        // เอาออกจากมือใน server state
        Server_RemoveActionCardFromHand(cardKey);

        // ให้ระบบเดิมจัดการเอฟเฟ็กต์การ์ด
        Server_ResolveActionCard(this, cardKey);
    }

    // TODO: ผูกระบบเอฟเฟ็กต์ของนายเอง
    [Server]
    void Server_ResolveActionCard(PlayerManager owner, string cardKey)
    {
        // ตรงนี้ไป map cardKey -> enum/skill แล้วเรียก logic เดิมที่มีอยู่
        // ตัวอย่าง:
        // SkillSystem.Instance.Server_PlayActionCard(owner, cardKey);
    }

    // ====== TargetRpc: ให้เจ้าของสร้างการ์ดบนมือ local-only ======

    [TargetRpc]
    void TargetRpc_ReceiveActionCard(NetworkConnection target, string cardKey)
    {
        // แค่เจ้าของเท่านั้นที่ได้มาถึงจุดนี้
        if (!isLocalPlayer) return;

        // ให้ ActionHandUI จัดการสร้างการ์ดบนมือฝั่ง local
        ActionHandUI.Instance.SpawnLocalHandCard(this, cardKey);
    }

    // ====== SyncVar hook – อัปเดต UI จำนวนการ์ด ======

    void OnActionHandCountChanged(int oldValue, int newValue)
    {
        // ส่งไปให้ UI กลางช่วยอัปเดต (ทั้งฝั่งเรา + ฝั่งศัตรู)
        if (ActionHandUI.Instance != null)
        {
            ActionHandUI.Instance.UpdateHandCountUI(this, newValue);
        }
    }
}
