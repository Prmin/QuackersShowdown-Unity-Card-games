using Mirror;

public partial class PlayerManager
{
    // ===== MoveAhead =====
    [Command(requiresAuthority = false)]
    public void CmdMoveAheadClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        int curRow = selectedDuck.RowNet;
        int curCol = selectedDuck.ColNet;
        int targetCol = curCol - 1;
        DuckCard targetDuck = FindDuckAt(curRow, targetCol);
        if (targetDuck == null) return;

        bool selectedHadTarget = IsCardTargeted(selectedDuck.netIdentity);
        bool targetHadTarget = IsCardTargeted(targetDuck.netIdentity);
        if (selectedHadTarget) RemoveTargetFromCard(selectedDuck.netIdentity);
        if (targetHadTarget) RemoveTargetFromCard(targetDuck.netIdentity);

        selectedDuck.ColNet = targetCol;
        targetDuck.ColNet = curCol;

        if (selectedHadTarget) CmdSpawnTargetForDuck(targetDuck.netId);
        if (targetHadTarget) CmdSpawnTargetForDuck(selectedDuck.netId);

        Server_PushDuckZoneOrder(curRow);

        activeSkillMode = SkillMode.None;
    }

    // ===== HangBack =====
    [Command(requiresAuthority = false)]
    public void CmdHangBackClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        int curRow = selectedDuck.RowNet;
        int curCol = selectedDuck.ColNet;
        int targetCol = curCol + 1;
        DuckCard targetDuck = FindDuckAt(curRow, targetCol);
        if (targetDuck == null) return;

        bool selectedHadTarget = IsCardTargeted(selectedDuck.netIdentity);
        bool targetHadTarget = IsCardTargeted(targetDuck.netIdentity);
        if (selectedHadTarget) RemoveTargetFromCard(selectedDuck.netIdentity);
        if (targetHadTarget) RemoveTargetFromCard(targetDuck.netIdentity);

        selectedDuck.ColNet = targetCol;
        targetDuck.ColNet = curCol;

        if (selectedHadTarget) CmdSpawnTargetForDuck(targetDuck.netId);
        if (targetHadTarget) CmdSpawnTargetForDuck(selectedDuck.netId);

        Server_PushDuckZoneOrder(curRow);

        activeSkillMode = SkillMode.None;
    }
}
