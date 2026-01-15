using Mirror;
using UnityEngine;

public partial class PlayerManager : NetworkBehaviour
{
    // server-only: จำว่า "การ์ดแอคชั่นใบล่าสุดที่เล่นลง DropZone" คือใบไหน
    uint _serverLastPlayedActionCardNetId = 0;
    string _serverLastPlayedActionCardKey = "";

    [Server]
    void ServerTurn_SetLastPlayedAction(DuckCard actionCard)
    {
        if (actionCard == null) return;
        _serverLastPlayedActionCardNetId = actionCard.netId;
        _serverLastPlayedActionCardKey = actionCard.gameObject.name.Replace("(Clone)", "").Trim();
    }

    [Server]
    int ServerTurn_RequiredPicksForSkill(SkillMode mode)
    {
        // 0 = instant, 1 = ต้องเลือก 1 ครั้ง, 2 = ต้องเลือก 2 ครั้ง
        switch (mode)
        {
            case SkillMode.Shoot: return 1;
            case SkillMode.TakeAim: return 1;
            case SkillMode.QuickShot: return 1;
            case SkillMode.Misfire: return 1;
            case SkillMode.BumpLeft: return 1;
            case SkillMode.BumpRight: return 1;
            case SkillMode.MoveAhead: return 1;
            case SkillMode.HangBack: return 1;
            case SkillMode.FastForward: return 1;

            case SkillMode.DoubleBarrel: return 2;
            case SkillMode.TwoBirds: return 2;
            case SkillMode.DisorderlyConduckt: return 2;

            // instant
            case SkillMode.LineForward:
            case SkillMode.DuckShuffle:
            case SkillMode.GivePeaceAChance:
            case SkillMode.Resurrection:
                return 0;

            default:
                return 0;
        }
    }

    [Server]
    public void ServerTurn_NotifyActionPlayed(SkillMode mode)
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;

        // ต้องเป็นเทิร์นเรา + อยู่ Phase A เท่านั้น
        if (!tm.ServerCanAct(netId, TurnPhase.PlayActionCard)) return;

        if (_serverLastPlayedActionCardNetId == 0)
        {
            Debug.LogWarning($"[TurnBridge] no last action card netId for pm={netId}, ignore notify");
            return;
        }

        int picks = ServerTurn_RequiredPicksForSkill(mode);
        tm.ServerNotifyActionCardPlayed(netId, _serverLastPlayedActionCardNetId, picks, _serverLastPlayedActionCardKey);
    }

    // เรียกเมื่อ “เลือกเป้าสำเร็จ 1 ครั้ง” (Phase B)
    [Server]
    public void ServerTurn_ConsumePick()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;
        if (!tm.ServerCanAct(netId, TurnPhase.ResolveAbility)) return;

        tm.ServerNotifyTargetPicked(netId);
    }

    // เวลา timeout penalty อยากล้าง state ค้าง
    [Server]
    public void ServerTurn_CancelPendingAbility()
    {
        activeSkillMode = SkillMode.None;

        // reset click state ที่มีในไฟล์หลัก
        firstSelectedDuck = null;
        firstTwoBirdsCard = null;
        twoBirdsClickCount = 0;
        doubleBarrelClickCount = 0;
        firstClickedCard = null;

        targetedDuckNetId = 0;
    }
}
