using System.Collections.Generic;
using Mirror;
using UnityEngine;

public partial class PlayerManager : NetworkBehaviour
{
  // ===============
  // Register/Unregister (เรียกจาก OnStartServer/OnStopServer ของไฟล์เดิม)
  // ===============
  [Server]
  public void ServerTurn_RegisterMe()
  {
    if (TurnManager.Instance != null)
      TurnManager.Instance.ServerRegisterPlayer(this);
  }

  [Server]
  public void ServerTurn_UnregisterMe()
  {
    if (TurnManager.Instance != null)
      TurnManager.Instance.ServerUnregisterPlayer(this);
  }

  // ===============
  // Client-side helper (เอาไปล็อค input ง่าย ๆ)
  // ===============
  public bool IsMyTurnLocal
  {
    get
    {
      if (TurnManager.Instance == null) return false;
      return TurnManager.Instance.CurrentPlayerNetId == netId;
    }
  }

  // ===============
  // Server: Auto Play (Phase A timeout)
  // ข้อกำหนด: เมื่อ “เล่นสำเร็จจริง” ให้ไปเรียก
  // TurnManager.Instance.ServerNotifyActionCardPlayed(netId, actionCard.netId, requiredTargetPicks)
  // ===============
  [Server]
  public bool ServerTurn_AutoPlayActionCard()
  {
    // หา action card ในมือของผู้เล่นคนนี้ (ZoneKind.PlayerArea)
    var hand = ServerTurn_FindCards(ownerNetId: netId, zone: ZoneKind.PlayerArea);
    if (hand.Count == 0) return false;

    var pick = hand[Random.Range(0, hand.Count)];

    // เล่นการ์ดแบบ “server-authoritative”
    // NOTE: เรา set SyncVar ตรง ๆ เพื่อไม่ผูกกับเมธอดเดิมของคุณ
    pick.zone = ZoneKind.DropZone;
    pick.zoneIndex = 0;
    pick.RowNet = 0;
    pick.ColNet = 0;

    // จุดเชื่อม: ถ้าระบบเดิมคุณ “activate เมื่อเข้า DropZone” อยู่แล้ว ก็จบ
    // ถ้าไม่ได้ activate เอง -> ให้ implement partial hook ข้างล่างเพื่อเรียก logic เดิมของคุณ
    ServerTurn_OnActionCardAutoPlayed(pick);

    return true;
  }

  // จุดเชื่อมให้เรียก logic เดิม (activate สกิล / set activeSkillMode / ฯลฯ)
  protected partial void ServerTurn_OnActionCardAutoPlayed(DuckCard actionCard);

  // ===============
  // Server: Auto Resolve (Phase B timeout)
  // ข้อกำหนด: ต้องทำให้ effect เกิดจริง แล้วค่อย notify
  // ===============
  [Server]
  public bool ServerTurn_AutoResolveAbility(int picksNeeded)
  {
    // ถ้าคุณยังไม่ wire auto-resolve ของสกิลทั้งหมด แนะนำให้ “คืน false”
    // เพื่อให้ TurnManager ข้ามเทิร์นแทน (กันเกมค้างแบบมั่ว ๆ)
    if (!ServerTurn_CanAutoResolveNow()) return false;

    for (int i = 0; i < picksNeeded; i++)
    {
      var validTargets = ServerTurn_CollectValidTargetsForActiveSkill();
      if (validTargets.Count == 0) break;

      var target = validTargets[Random.Range(0, validTargets.Count)];

      // จุดเชื่อม: ให้คุณเอา “logic server ของการคลิกเลือกเป็ด” มาใส่ตรงนี้
      // แล้วใน logic นั้น เมื่อรับ pick สำเร็จ -> ให้เรียก TurnManager.Instance.ServerNotifyTargetPicked(netId)
      if (!ServerTurn_ApplyAutoPick(target, i))
        return false;
    }

    // ถ้าสกิลบางอัน “ทำ effect หลัง pick ครบ” คุณก็ไปเรียก Resolved เองใน logic นั้น
    return true;
  }

  // ป้องกัน auto มั่วตอนยังไม่มี activeSkillMode/ยังไม่อยู่ใน Phase B
  [Server]
  private bool ServerTurn_CanAutoResolveNow()
  {
    if (TurnManager.Instance == null) return false;
    if (!TurnManager.Instance.ServerIsPlayersTurn(netId)) return false;
    if (TurnManager.Instance.Phase != TurnPhase.ResolveAbility) return false;
    return true;
  }

  // Default valid targets: DuckZone ทั้งหมด (คุณ override ได้ด้วย partial ถ้าสกิลมีเงื่อนไขซับซ้อน)
  [Server]
  protected virtual List<DuckCard> ServerTurn_CollectValidTargetsForActiveSkill()
  {
    return ServerTurn_FindCards(ownerNetId: 0, zone: ZoneKind.DuckZone); // ownerNetId=0 คือไม่กรอง owner
  }

  // จุดเชื่อม: ทำ “1 pick” ให้เหมือนผู้เล่นคลิกเลือกเป็ด (บน server)
  // return true = pick สำเร็จ / false = ทำไม่ได้ (ให้ TurnManager ข้ามเทิร์น)
  protected partial bool ServerTurn_ApplyAutoPick(DuckCard target, int pickIndex);

  // ======================
  // Shared helper: หา DuckCard จาก NetworkServer.spawned
  // ======================
  [Server]
  protected List<DuckCard> ServerTurn_FindCards(uint ownerNetId, ZoneKind zone)
  {
    var result = new List<DuckCard>();

    foreach (var kv in NetworkServer.spawned)
    {
      var ni = kv.Value;
      if (ni == null) continue;

      if (!ni.TryGetComponent<DuckCard>(out var card)) continue;
      if (card == null) continue;

      if (card.zone != zone) continue;
      if (ownerNetId != 0 && card.ownerNetId != ownerNetId) continue;

      result.Add(card);
    }

    return result;
  }
}
