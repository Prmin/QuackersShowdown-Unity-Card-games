using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // ===== Disorderly Conduckt =====
    [Command(requiresAuthority = false)]
    public void CmdDisorderlyClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;

        if (firstSelectedDuck == null)
        {
            firstSelectedDuck = selectedDuck;
            return;
        }
        if (firstSelectedDuck == selectedDuck)
        {
            Server_PushDuckZoneOrder(firstSelectedDuck.RowNet);

            firstSelectedDuck = null;
            return;
        }

        DuckCard secondDuck = selectedDuck;
        bool sameRow = firstSelectedDuck.RowNet == secondDuck.RowNet;
        bool adjacentCol = Mathf.Abs(firstSelectedDuck.ColNet - secondDuck.ColNet) == 1;
        if (!sameRow || !adjacentCol)
        {
            firstSelectedDuck = selectedDuck;
            return;
        }

        bool firstHadTarget = IsCardTargeted(firstSelectedDuck.netIdentity);
        bool secondHadTarget = IsCardTargeted(secondDuck.netIdentity);
        if (firstHadTarget) RemoveTargetFromCard(firstSelectedDuck.netIdentity);
        if (secondHadTarget) RemoveTargetFromCard(secondDuck.netIdentity);

        int tempCol = firstSelectedDuck.ColNet;
        firstSelectedDuck.ColNet = secondDuck.ColNet;
        secondDuck.ColNet = tempCol;

        if (firstHadTarget) CmdSpawnTargetForDuck(secondDuck.netId);
        if (secondHadTarget) CmdSpawnTargetForDuck(firstSelectedDuck.netId);

        Server_PushDuckZoneOrder(firstSelectedDuck.RowNet);

        firstSelectedDuck = null;
        activeSkillMode = SkillMode.None; // ถ้าต้องการปิดโหมดทันทีให้ uncomment
    }

    [Command(requiresAuthority = false)]
    private void CmdSpawnTargetForDuck(uint duckNetId)
    {
        if (!NetworkServer.spawned.TryGetValue(duckNetId, out NetworkIdentity duckNi))
            return;
        if (targetPrefab == null) return;

        GameObject newTarget = Object.Instantiate(targetPrefab);
        NetworkServer.Spawn(newTarget);
        NetworkIdentity targetNi = newTarget.GetComponent<NetworkIdentity>();
        RpcSetTargetNetId(targetNi, duckNi);
    }
}
