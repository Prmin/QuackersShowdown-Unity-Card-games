using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // =========================================================
    // Patch 6: TurnSkillBridge
    // - TurnManager (server) เรียก 2 hook นี้เพื่อ auto-play/auto-pick
    // - ใช้ Cmd เดิมของ (Shoot/Misfire/TwoBirds/DoubleBarrel/... )
    // - และเป็นคน "notify" ให้ TurnManager ว่ามี pick เกิดขึ้นแล้ว
    // =========================================================

    // Hook (จาก PlayerManager.Turn.cs / TurnManager)
    protected partial void ServerTurn_OnActionCardAutoPlayed(DuckCard actionCard)
    {
        if (!isServer) return;

        // พยายามเดา key จากชื่อ prefab/object
        string key = ServerTurn_ExtractActionKeyFrom(actionCard);
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[TurnSkillBridge] AutoPlayed actionCard but cannot extract action key/name.");
            return;
        }

        // เปิดโหมดสกิลบน server ทันที (ไม่ผ่าน client)
        if (ServerTurn_TryMapActionKeyToSkillMode(key, out var mode))
        {
            ServerTurn_SetSkillMode_Server(mode);
        }
        else
        {
            Debug.LogWarning($"[TurnSkillBridge] Unknown action key '{key}' -> cannot map to SkillMode.");
        }
    }

    // Hook (จาก PlayerManager.Turn.cs / TurnManager)
    // suggestedTarget: TurnManager อาจส่งตัวเลือกมาให้ แต่ bridge จะ validate + หาใหม่ได้เอง
    protected partial bool ServerTurn_ApplyAutoPick(DuckCard suggestedTarget, int pickIndex)
    {
        if (!isServer) return false;

        var tm = TurnManager.Instance;
        if (tm == null) return false;

        // กันหลุดเฟส/หลุดเทิร์น
        if (!tm.ServerCanAct(netId, TurnPhase.ResolveAbility))
            return false;

        // ถ้าไม่มีโหมดสกิลแล้ว แต่ยังโดนสั่งให้ pick -> กันค้างด้วยการจบ ability
        if (activeSkillMode == SkillMode.None)
        {
            tm.ServerNotifyAbilityResolved(netId);
            return true;
        }

        // เลือกเป้าตามสกิล (ถ้า suggestedTarget ใช้ไม่ได้ จะหาใหม่)
        DuckCard target = ServerTurn_PickValidTargetForActiveSkill(suggestedTarget, pickIndex);

        // ถ้าหาไม่ได้จริง ๆ -> จบ ability กันเกมค้าง
        if (target == null)
        {
            tm.ServerNotifyAbilityResolved(netId);
            return true;
        }

        // “จำลองการคลิก” ด้วยการเรียก Cmd เดิมของนาย
        // แล้วค่อย notify TurnManager ว่า pick เกิดขึ้นแล้ว
        switch (activeSkillMode)
        {
            case SkillMode.Shoot:
                CmdShootCard(target.netIdentity);
                tm.ServerNotifyTargetPicked(netId);
                return true;

            case SkillMode.TakeAim:
                // CmdSpawnTarget ไม่ได้ปิด mode เองในโค้ดที่มีอยู่ -> ปิดให้ด้วย
                CmdSpawnTarget(target.netIdentity);
                activeSkillMode = SkillMode.None;
                tm.ServerNotifyTargetPicked(netId);
                return true;

            case SkillMode.QuickShot:
                CmdQuickShotCard(target.netIdentity);
                tm.ServerNotifyTargetPicked(netId);
                return true;

            case SkillMode.Misfire:
                CmdMisfireClick(target.netIdentity);
                tm.ServerNotifyTargetPicked(netId);
                return true;

            case SkillMode.BumpLeft:
                CmdBumpLeftClick(target.netIdentity);
                tm.ServerNotifyTargetPicked(netId);
                return true;

            case SkillMode.BumpRight:
                CmdBumpRightClick(target.netIdentity);
                tm.ServerNotifyTargetPicked(netId);
                return true;

            case SkillMode.MoveAhead:
                CmdMoveAheadClick(target.netIdentity);
                tm.ServerNotifyTargetPicked(netId);
                return true;

            case SkillMode.HangBack:
                CmdHangBackClick(target.netIdentity);
                tm.ServerNotifyTargetPicked(netId);
                return true;

            case SkillMode.FastForward:
                // FastForward เป็น coroutine -> เราจะ “รอให้มันจบ” แล้วค่อย notify
                activeSkillMode = SkillMode.None;
                StartCoroutine(ServerTurn_FastForwardAndNotify(target));
                return true;

            case SkillMode.DoubleBarrel:
                // ต้อง pick 2 ครั้ง: pickIndex=0 เลือกตัวแรก, pickIndex=1 เลือกตัวที่ติดกัน
                if (pickIndex == 0)
                {
                    CmdDoubleBarrelClick(target.netIdentity);
                    tm.ServerNotifyTargetPicked(netId);
                    return true;
                }
                else
                {
                    // ใช้ firstClickedCard ที่ระบบเดิมเก็บไว้
                    var second = ServerTurn_PickAdjacentTo(firstClickedCard);
                    if (second == null)
                    {
                        tm.ServerNotifyAbilityResolved(netId);
                        return true;
                    }

                    CmdDoubleBarrelClick(second);
                    tm.ServerNotifyTargetPicked(netId);
                    return true;
                }

            case SkillMode.TwoBirds:
                if (pickIndex == 0)
                {
                    CmdTwoBirdsClick(target.netIdentity);
                    tm.ServerNotifyTargetPicked(netId);
                    return true;
                }
                else
                {
                    var second = ServerTurn_PickAdjacentTo(firstTwoBirdsCard);
                    if (second == null)
                    {
                        tm.ServerNotifyAbilityResolved(netId);
                        return true;
                    }

                    CmdTwoBirdsClick(second);
                    tm.ServerNotifyTargetPicked(netId);
                    return true;
                }

            case SkillMode.DisorderlyConduckt:
                if (pickIndex == 0)
                {
                    CmdDisorderlyClick(target.netIdentity);
                    tm.ServerNotifyTargetPicked(netId);
                    return true;
                }
                else
                {
                    // ต้องเป็นตัว “คนละตัว” กับ firstSelectedDuck ถ้ามี
                    if (firstSelectedDuck != null && target == firstSelectedDuck)
                        target = ServerTurn_PickAnyDuckZoneDuck(exclude: firstSelectedDuck);

                    if (target == null)
                    {
                        tm.ServerNotifyAbilityResolved(netId);
                        return true;
                    }

                    CmdDisorderlyClick(target.netIdentity);
                    tm.ServerNotifyTargetPicked(netId);
                    return true;
                }

            default:
                // สกิลอื่น ๆ ถ้าเข้ามาถึงนี่ แปลว่า TurnManager คิดว่าต้อง pick แต่เราไม่รองรับ
                tm.ServerNotifyAbilityResolved(netId);
                return true;
        }
    }

    // -----------------------------
    // Server helpers
    // -----------------------------

    [Server]
    void ServerTurn_SetSkillMode_Server(SkillMode newMode)
    {
        activeSkillMode = newMode;

        bool closeNow = false;

        // instant skills: ทำเลย แล้วปิดโหมด
        if (newMode == SkillMode.LineForward)
        {
            CmdActivateLineForward();
            closeNow = true;
        }
        else if (newMode == SkillMode.DuckShuffle)
        {
            CmdActivateDuckShuffle();
            closeNow = true;
        }
        else if (newMode == SkillMode.GivePeaceAChance)
        {
            CmdActivateGivePeaceAChance();
            closeNow = true;
        }
        else if (newMode == SkillMode.Resurrection)
        {
            Server_ActivateResurrectionMode();
            closeNow = true;
        }

        if (closeNow)
            activeSkillMode = SkillMode.None;
    }

    [Server]
    string ServerTurn_ExtractActionKeyFrom(DuckCard actionCard)
    {
        if (actionCard == null) return null;

        // ใช้ชื่อเกมอ็อบเจ็กต์เป็น key (ตัด "(Clone)")
        string raw = actionCard.gameObject != null ? actionCard.gameObject.name : actionCard.name;
        if (string.IsNullOrEmpty(raw)) return null;

        string key = raw.Replace("(Clone)", "").Trim();
        return key;
    }

    [Server]
    bool ServerTurn_TryMapActionKeyToSkillMode(string actionKey, out SkillMode mode)
    {
        // map ให้ตรงกับชื่อการ์ดจริงของนาย (key ที่ส่งมักจะเป็นชื่อ prefab)
        switch (actionKey)
        {
            case "Shoot": mode = SkillMode.Shoot; return true;
            case "TakeAim": mode = SkillMode.TakeAim; return true;
            case "DoubleBarrel": mode = SkillMode.DoubleBarrel; return true;
            case "QuickShot": mode = SkillMode.QuickShot; return true;
            case "Misfire": mode = SkillMode.Misfire; return true;
            case "TwoBirds": mode = SkillMode.TwoBirds; return true;

            case "BumpLeft": mode = SkillMode.BumpLeft; return true;
            case "BumpRight": mode = SkillMode.BumpRight; return true;

            case "MoveAhead": mode = SkillMode.MoveAhead; return true;
            case "HangBack": mode = SkillMode.HangBack; return true;

            case "FastForward": mode = SkillMode.FastForward; return true;
            case "DisorderlyConduckt": mode = SkillMode.DisorderlyConduckt; return true;

            case "LineForward": mode = SkillMode.LineForward; return true;
            case "DuckShuffle": mode = SkillMode.DuckShuffle; return true;
            case "GivePeaceAChance": mode = SkillMode.GivePeaceAChance; return true;
            case "Resurrection": mode = SkillMode.Resurrection; return true;

            default:
                mode = SkillMode.None;
                return false;
        }
    }

    [Server]
    DuckCard ServerTurn_PickValidTargetForActiveSkill(DuckCard suggested, int pickIndex)
    {
        // helper: ใช้ suggested ถ้ามัน valid
        if (ServerTurn_IsValidTargetForActiveSkill(suggested, pickIndex))
            return suggested;

        // ไม่ valid -> หาใหม่ตามกติกาของแต่ละสกิล
        switch (activeSkillMode)
        {
            case SkillMode.Shoot:
            case SkillMode.Misfire:
                return ServerTurn_PickRandomTargetedDuck();

            case SkillMode.TakeAim:
                return ServerTurn_PickRandomUntargetedDuck();

            case SkillMode.BumpLeft:
                return ServerTurn_PickRandomTargetedDuckWithNeighbor(deltaCol: -1);

            case SkillMode.BumpRight:
                return ServerTurn_PickRandomTargetedDuckWithNeighbor(deltaCol: +1);

            case SkillMode.MoveAhead:
                return ServerTurn_PickRandomDuckWithNeighbor(deltaCol: -1);

            case SkillMode.HangBack:
                return ServerTurn_PickRandomDuckWithNeighbor(deltaCol: +1);

            case SkillMode.FastForward:
                // ต้องมีซ้ายให้ไหลได้
                return ServerTurn_PickRandomDuckWithNeighbor(deltaCol: -1);

            case SkillMode.QuickShot:
                return ServerTurn_PickAnyDuckZoneDuck();

            case SkillMode.DoubleBarrel:
            case SkillMode.TwoBirds:
                if (pickIndex == 0)
                    return ServerTurn_PickDuckThatHasAnyAdjacent();
                // pickIndex == 1 -> จะใช้ neighbor ของ first* ใน switch หลัก
                return ServerTurn_PickAnyDuckZoneDuck();

            case SkillMode.DisorderlyConduckt:
                return ServerTurn_PickAnyDuckZoneDuck();

            default:
                return null;
        }
    }

    [Server]
    bool ServerTurn_IsValidTargetForActiveSkill(DuckCard t, int pickIndex)
    {
        if (t == null) return false;
        if (t.zone != ZoneKind.DuckZone) return false;

        switch (activeSkillMode)
        {
            case SkillMode.Shoot:
            case SkillMode.Misfire:
            case SkillMode.BumpLeft:
            case SkillMode.BumpRight:
                return IsCardTargeted(t.netIdentity);

            default:
                return true;
        }
    }

    [Server]
    DuckCard ServerTurn_PickAnyDuckZoneDuck(DuckCard exclude = null)
    {
        var list = ServerTurn_GetDuckZoneDucks();
        if (exclude != null)
            list.Remove(exclude);

        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    [Server]
    List<DuckCard> ServerTurn_GetDuckZoneDucks()
    {
        var all = FindObjectsOfType<DuckCard>();
        var ducks = new List<DuckCard>();
        foreach (var dc in all)
        {
            if (dc != null && dc.zone == ZoneKind.DuckZone)
                ducks.Add(dc);
        }
        return ducks;
    }

    [Server]
    DuckCard ServerTurn_PickRandomTargetedDuck()
    {
        var ducks = ServerTurn_GetDuckZoneDucks();
        var targeted = new List<DuckCard>();
        foreach (var d in ducks)
            if (IsCardTargeted(d.netIdentity))
                targeted.Add(d);

        if (targeted.Count == 0) return null;
        return targeted[Random.Range(0, targeted.Count)];
    }

    [Server]
    DuckCard ServerTurn_PickRandomUntargetedDuck()
    {
        var ducks = ServerTurn_GetDuckZoneDucks();
        var list = new List<DuckCard>();
        foreach (var d in ducks)
            if (!IsCardTargeted(d.netIdentity))
                list.Add(d);

        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    [Server]
    DuckCard ServerTurn_PickRandomDuckWithNeighbor(int deltaCol)
    {
        var ducks = ServerTurn_GetDuckZoneDucks();
        var list = new List<DuckCard>();

        foreach (var d in ducks)
        {
            if (d == null) continue;
            DuckCard neighbor = FindDuckAt(d.RowNet, d.ColNet + deltaCol);
            if (neighbor != null)
                list.Add(d);
        }

        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    [Server]
    DuckCard ServerTurn_PickRandomTargetedDuckWithNeighbor(int deltaCol)
    {
        var ducks = ServerTurn_GetDuckZoneDucks();
        var list = new List<DuckCard>();

        foreach (var d in ducks)
        {
            if (d == null) continue;
            if (!IsCardTargeted(d.netIdentity)) continue;

            DuckCard neighbor = FindDuckAt(d.RowNet, d.ColNet + deltaCol);
            if (neighbor != null)
                list.Add(d);
        }

        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    [Server]
    DuckCard ServerTurn_PickDuckThatHasAnyAdjacent()
    {
        var ducks = ServerTurn_GetDuckZoneDucks();
        var list = new List<DuckCard>();

        foreach (var d in ducks)
        {
            if (d == null) continue;

            bool hasLeft = FindDuckAt(d.RowNet, d.ColNet - 1) != null;
            bool hasRight = FindDuckAt(d.RowNet, d.ColNet + 1) != null;
            if (hasLeft || hasRight)
                list.Add(d);
        }

        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    [Server]
    NetworkIdentity ServerTurn_PickAdjacentTo(NetworkIdentity firstNi)
    {
        if (firstNi == null) return null;

        var dc = firstNi.GetComponent<DuckCard>();
        if (dc == null) return null;

        var choices = new List<NetworkIdentity>();

        DuckCard left = FindDuckAt(dc.RowNet, dc.ColNet - 1);
        if (left != null) choices.Add(left.netIdentity);

        DuckCard right = FindDuckAt(dc.RowNet, dc.ColNet + 1);
        if (right != null) choices.Add(right.netIdentity);

        if (choices.Count == 0) return null;
        return choices[Random.Range(0, choices.Count)];
    }

    [Server]
    IEnumerator ServerTurn_FastForwardAndNotify(DuckCard duck)
    {
        if (duck == null)
        {
            TurnManager.Instance?.ServerNotifyAbilityResolved(netId);
            yield break;
        }

        // เรียก coroutine เดิมของนาย แล้วค่อย notify ทีหลัง
        yield return FastForwardCoroutine(duck);

        TurnManager.Instance?.ServerNotifyTargetPicked(netId);
    }
}
