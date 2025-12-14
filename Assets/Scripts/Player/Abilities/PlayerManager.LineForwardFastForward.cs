using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // ===== LineForward =====
    [Command(requiresAuthority = false)]
    public void CmdActivateLineForward()
    {
        // Debug.Log("[LineForward] Activate (CmdActivateLineForward)");

        // ✅ เก็บ “ตำแหน่งเป้า (slot/col)” ไว้ก่อน แล้วค่อยลบเป้าทิ้ง
        // เป้าจะไม่วิ่งตามการ์ดตอนคอลัมน์เปลี่ยน
        var oldTargets = CollectTargetColumns();
        RemoveAllTargets();

        // ✅ ใบหัวแถว (ซ้ายสุด) กลับเข้า “ใต้กองจั่ว” (pool) แล้วค่อยทำลาย object ในสนาม
        var leftmost = FindLeftmostDuck(0);
        if (leftmost != null)
        {
            string key = ExtractDuckKeyFromCard(leftmost.gameObject) ?? leftmost.gameObject.name;
            int before = Server_GetDuckPoolRemaining();

            CardPoolManager.ReturnCard(leftmost.gameObject);

            // ✅ pool counts แยกตามสี
            var poolCounts = CardPoolManager.GetAllPoolCounts(); // Dictionary<string,int>

            // ✅ zone counts (เฉพาะ DuckZone แถว 0) แยกตามสี
            uint returningId = leftmost.netId;

            var zoneCounts = new Dictionary<string, int>();
            foreach (var d in FindDucksInRow(0))
            {
                if (d.netId == returningId) continue; // ✅ ไม่นับใบที่เพิ่งคืนเข้ากอง

                string k = ExtractDuckKeyFromCard(d.gameObject) ?? d.gameObject.name.Replace("(Clone)", "").Trim();
                if (!zoneCounts.ContainsKey(k)) zoneCounts[k] = 0;
                zoneCounts[k] += 1;
            }


            // ✅ total counts = pool + zone
            var totalCounts = new Dictionary<string, int>();
            foreach (var kv in poolCounts) totalCounts[kv.Key] = kv.Value;
            foreach (var kv in zoneCounts)
            {
                if (!totalCounts.ContainsKey(kv.Key)) totalCounts[kv.Key] = 0;
                totalCounts[kv.Key] += kv.Value;
            }

            // ✅ ทำ string ให้อ่านง่าย
            string poolByColor = FormatDuckCounts(poolCounts);
            string zoneByColor = FormatDuckCounts(zoneCounts);
            string totalByColor = FormatDuckCounts(totalCounts);


            int after = Server_GetDuckPoolRemaining();
            int totalInPool = after;
            int totalOnBoard = FindDucksInRow(0).Count - 1; // -1 เพราะเอาใบที่คืนกองออกไปแล้ว
            int totalAllRemaining = totalInPool + totalOnBoard;

            //         Debug.Log(
            //   $"[LineForward][ReturnToPool] netId={leftmost.netId} card={leftmost.gameObject.name} key={key} " +
            //   $"| poolRemaining={before}->{after} | totalInPoolNow={totalInPool} | onBoardNow={totalOnBoard} | totalAllRemaining={totalAllRemaining} " +
            //   $"| poolByColor={poolByColor} | zoneByColor={zoneByColor} | totalByColor={totalByColor}"
            // );


            NetworkServer.Destroy(leftmost.gameObject);

            // ✅ ให้การ์ดที่เหลือ “เดินหน้า” 1 ช่อง (compaction) — UI/GridLayoutGroup จะจัดตำแหน่งเอง
            Server_ResequenceDuckZoneColumns();
        }

        // ✅ เติมใบใหม่ที่หางแถว + สร้างเป้ากลับมา “ที่คอลัมน์เดิม”
        StartCoroutine(RefillAndRecreateTargets(oldTargets));
        StartCoroutine(DelayedLog());
    }

    // ===== FastForward =====
    [Command(requiresAuthority = false)]
    public void CmdFastForwardClick(NetworkIdentity clickedCard)
    {
        if (clickedCard == null) return;
        DuckCard selectedDuck = clickedCard.GetComponent<DuckCard>();
        if (selectedDuck == null) return;
        StartCoroutine(FastForwardCoroutine(selectedDuck));
        activeSkillMode = SkillMode.None;
    }

    [Server]
    private IEnumerator FastForwardCoroutine(DuckCard selectedDuck)
    {
        float delay = 0.3f;
        int curRow = selectedDuck.RowNet;
        List<int> originalTargetColumns = new List<int>();
        List<TargetFollow> targetsToDestroy = new List<TargetFollow>();
        TargetFollow[] allTargets = FindObjectsOfType<TargetFollow>();
        foreach (var tf in allTargets)
        {
            DuckCard duck = FindDuckByNetId(tf.targetNetId);
            if (duck != null && duck.RowNet == curRow)
            {
                if (!originalTargetColumns.Contains(duck.ColNet))
                    originalTargetColumns.Add(duck.ColNet);
                targetsToDestroy.Add(tf);
            }
        }
        foreach (var tf in targetsToDestroy)
            NetworkServer.Destroy(tf.gameObject);
        while (selectedDuck.ColNet > 0)
        {
            int currentCol = selectedDuck.ColNet;
            int targetCol = currentCol - 1;
            DuckCard targetDuck = FindDuckAt(curRow, targetCol);
            if (targetDuck == null) break;
            selectedDuck.ColNet = targetCol;
            targetDuck.ColNet = currentCol;
            yield return new WaitForSeconds(delay);
        }
        yield return null;
        foreach (int originalCol in originalTargetColumns)
        {
            DuckCard newDuckAtCol = FindDuckAt(curRow, originalCol);
            if (newDuckAtCol != null)
                CmdSpawnTargetForDuck(newDuckAtCol.netId);
        }
    }


    [Server]
    private int Server_GetDuckPoolRemaining()
    {
        int total = 0;
        var counts = CardPoolManager.GetAllPoolCounts();
        if (counts != null)
        {
            foreach (var kv in counts)
                total += kv.Value;
        }
        return total;
    }


    [Server]
    private DuckCard FindDuckByNetId(uint netId)
    {
        if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity ni))
            return ni.GetComponent<DuckCard>();
        return null;
    }

    private string FormatDuckCounts(Dictionary<string, int> counts)
    {
        // เรียง key ให้อ่านง่าย (ไม่ใช้ LINQ)
        var keys = new List<string>(counts.Keys);
        keys.Sort();

        string s = "";
        for (int i = 0; i < keys.Count; i++)
        {
            var k = keys[i];
            if (i > 0) s += ", ";
            s += $"{k}={counts[k]}";
        }
        return s;
    }
}
