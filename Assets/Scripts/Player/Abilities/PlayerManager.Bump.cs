using Mirror;

public partial class PlayerManager
{
    // ===== BumpLeft =====
    [Command(requiresAuthority = false)]
    public void CmdBumpLeftClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        DuckCard duck = clickedCard.GetComponent<DuckCard>();
        if (duck == null) return;

        int curRow = duck.RowNet;
        int curCol = duck.ColNet;
        DuckCard leftDuck = FindDuckAt(curRow, curCol - 1);
        if (leftDuck == null) return;

        MoveTargetFromTo(clickedCard, leftDuck.GetComponent<NetworkIdentity>());
        activeSkillMode = SkillMode.None;
    }

    // ===== BumpRight =====
    [Command(requiresAuthority = false)]
    public void CmdBumpRightClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        if (!IsCardTargeted(clickedCard)) return;
        DuckCard duck = clickedCard.GetComponent<DuckCard>();
        if (duck == null) return;

        int curRow = duck.RowNet;
        int curCol = duck.ColNet;
        DuckCard rightDuck = FindDuckAt(curRow, curCol + 1);
        if (rightDuck == null) return;

        MoveTargetFromTo(clickedCard, rightDuck.GetComponent<NetworkIdentity>());
        activeSkillMode = SkillMode.None;
    }
}
