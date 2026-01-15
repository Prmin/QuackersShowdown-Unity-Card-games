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
        // (1) GUARD: กันเล่นนอกเทิร์น / นอก Phase A
        if (TurnManager.Instance == null)
        {
            Debug.LogWarning("[CmdPlayActionCard] TurnManager not found");
            return;
        }

        if (!TurnManager.Instance.ServerCanAct(netId, TurnPhase.PlayActionCard))
        {
            Debug.LogWarning($"[CmdPlayActionCard] Not your turn / wrong phase. player={netId} phase={TurnManager.Instance.Phase} current={TurnManager.Instance.CurrentPlayerNetId}");
            return;
        }

        // (2) validate: ต้องมีการ์ดอยู่จริงในมือ server
        if (!_serverActionHand.Contains(cardKey))
        {
            Debug.LogWarning($"[CmdPlayActionCard] {name} tried to play card '{cardKey}' but it is not in server hand");
            return;
        }

        // (3) เอาออกจากมือใน server state
        Server_RemoveActionCardFromHand(cardKey);

        // (4) ให้ระบบเดิมจัดการ “เริ่มสกิล/ตั้ง activeSkillMode/activate”
        Server_ResolveActionCard(this, cardKey);

        // (5) NOTIFY: บอก TurnManager ว่าเล่นแล้ว และต้องเลือกกี่ครั้ง
        int requiredPicks = Server_GetRequiredTargetPicks(cardKey);

        // หมายเหตุ: ตอนนี้การ์ดในมือคุณเป็น string ไม่ใช่ DuckCard netId
        // เลยส่ง actionCardNetId = 0 ไปก่อน (หรือคุณจะขยาย TurnManager ให้ sync cardKey ก็ได้)
        TurnManager.Instance.ServerNotifyActionCardPlayed(netId, 0, requiredPicks);
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

    [Server]
    int Server_GetRequiredTargetPicks(string cardKey)
    {
        // instant = 0
        // ต้องเลือก 1 ครั้ง = 1
        // ต้องเลือก 2 ครั้ง = 2

        switch (cardKey)
        {
            case "Resurrection":
            case "DuckShuffle":
            case "GivePeaceAChance":
            case "LineForward":
                return 0;

            case "Shoot":
            case "TakeAim":
            case "QuickShot":
            case "Misfire":
            case "BumpLeft":
            case "BumpRight":
            case "MoveAhead":
            case "HangBack":
            case "FastForward":
                return 1;

            case "DoubleBarrel":
            case "TwoBirds":
            case "DisorderlyConduckt":
                return 2;

            default:
                return 0; // ไม่รู้ → ถือว่า instant ไปก่อน กันเกมค้าง
        }
    }

}
