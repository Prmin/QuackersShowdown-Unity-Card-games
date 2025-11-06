using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Mirror;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerManager))]
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerTurnSeatingBinder : NetworkBehaviour
{
    // ===== ผังช่อง EnemyArea ที่ “เปิดใช้” ตามจำนวนผู้เล่น =====
    // index คือจำนวนผู้เล่นทั้งหมด (2..6)
    // ค่าภายในคือหมายเลข EnemyArea# (1..5)
    private static readonly int[][] s_slotPriorityByCount = new int[][]
    {
        null, // 0
        null, // 1
        new[] { 2 },               // 2 players -> EA2
        new[] { 1, 3 },            // 3 players -> EA1, EA3
        new[] { 1, 2, 3 },         // 4 players -> EA1, EA2, EA3
        new[] { 1, 2, 3, 4 },      // 5 players -> EA1..EA4
        new[] { 1, 2, 3, 4, 5 },   // 6 players -> EA1..EA5
    };

    // ====== Reflection: แตะฟิลด์ private static ใน PlayerManager อย่างปลอดภัย ======
    private static FieldInfo _fEnemySlots;
    private static FieldInfo _fRemoteSlotIndex;
    private static bool _refBound;

    private static void BindReflection()
    {
        if (_refBound) return;
        var t = typeof(PlayerManager);
        // private static Transform[] s_enemySlots
        _fEnemySlots = t.GetField("s_enemySlots", BindingFlags.NonPublic | BindingFlags.Static);
        // private static readonly Dictionary<uint,int> s_remoteSlotIndex
        _fRemoteSlotIndex = t.GetField("s_remoteSlotIndex", BindingFlags.NonPublic | BindingFlags.Static);
        _refBound = true;
    }

    private static Transform[] GetEnemySlotsViaPM()
    {
        BindReflection();
        return _fEnemySlots?.GetValue(null) as Transform[];
    }

    private static Dictionary<uint, int> GetRemoteMapViaPM()
    {
        BindReflection();
        return _fRemoteSlotIndex?.GetValue(null) as Dictionary<uint, int>;
    }

    // ===== Utilities =====
    private static int SeatDelta(int seat, int localSeat, int total)
    {
        if (total <= 0 || seat < 0) return int.MaxValue - 100 + seat;
        int d = (seat - localSeat) % total;
        if (d < 0) d += total;
        return (d == 0) ? int.MaxValue : d; // 0 = local → ดันไปท้ายสุด
    }

    private static List<Transform> GetActiveSlotsForCount(int playerCount, Transform[] slots)
    {
        var result = new List<Transform>();
        if (slots == null) return result;

        int idx = Mathf.Clamp(playerCount, 0, s_slotPriorityByCount.Length - 1);
        var plan = s_slotPriorityByCount[idx];
        if (plan == null)
        {
            // เผื่อกรณีไม่อยู่ในผัง: เปิด EnemyArea1.. ตามจำนวนผู้เล่น-1
            for (int i = 0; i < Mathf.Min(playerCount - 1, slots.Length); i++)
                if (slots[i] != null) result.Add(slots[i]);
            return result;
        }

        foreach (int num in plan)
        {
            int sIndex = num - 1; // 1-based -> 0-based
            if (sIndex >= 0 && sIndex < slots.Length && slots[sIndex] != null)
                result.Add(slots[sIndex]);
        }
        return result;
    }

    // ===== แกนหลัก: คำนวณและ "เขียนทับ" แผนที่นั่งของ PlayerManager =====
    public static void RecomputeAndApplyLayout()
    {
        try
        {
            var slots = GetEnemySlotsViaPM();
            var map = GetRemoteMapViaPM();
            if (map == null)
            {
                Debug.LogWarning("[TurnSeatingBinder] remoteSlotIndex map not found (reflection).");
                return;
            }

            if (slots == null || slots.Any(t => t == null))
            {
                // ยังไม่มี EnemyArea1..5 ในซีน → เคลียร์ map ให้ PM เดิม fallback
                map.Clear();
                return;
            }

            var all = GameObject.FindObjectsOfType<PlayerManager>();
            int total = Mathf.Clamp(all.Length, 1, 6);

            // หา local (อิง NetworkIdentity.isOwned)
            var local = all.FirstOrDefault(pm =>
            {
                var ni = pm.GetComponent<NetworkIdentity>();
                return ni != null && ni.isOwned;
            });
            int localSeat = (local != null && local.seatIndex >= 0) ? local.seatIndex : 0;

            // ศัตรูเรียงตาม “ระยะที่นั่ง” จ่อจากเราไปตามลำดับ (รอบวง)
            var remotes = all.Where(pm =>
            {
                var ni = pm.GetComponent<NetworkIdentity>();
                return ni != null && !ni.isOwned;
            })
            .OrderBy(pm => SeatDelta(pm.seatIndex, localSeat, total))
            .ThenBy(pm => pm.netId) // กันเท่ากัน
            .ToList();

            var activeSlots = GetActiveSlotsForCount(total, slots);

            map.Clear();
            for (int i = 0; i < remotes.Count && i < activeSlots.Count; i++)
            {
                // แปลง Transform -> index ของ s_enemySlots
                int sIndex = Array.IndexOf(slots, activeSlots[i]);
                if (sIndex >= 0)
                    map[remotes[i].netId] = sIndex; // เก็บ index 0..4 ให้ PlayerManager เดิมใช้งาน
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TurnSeatingBinder] Recompute failed: {ex}");
        }
    }

    // ===== ไลฟ์ไซเคิล: ให้วิ่ง “หลัง” PlayerManager.OnStartClient เพื่อเขียนทับ mapping =====
    public override void OnStartClient()
    {
        base.OnStartClient();
        // หน่วง 1 เฟรม เพื่อให้ PlayerManager.CacheEnemySlotsFromScene ทำงานก่อน
        StartCoroutine(DelayThenRecompute());
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        RecomputeAndApplyLayout();
    }

    private System.Collections.IEnumerator DelayThenRecompute()
    {
        yield return null; // 1 เฟรม
        RecomputeAndApplyLayout();
    }

    private void OnDestroy()
    {
        // ใครออกจากซีน → คำนวณใหม่
        if (!NetworkClient.active) return;
        RecomputeAndApplyLayout();
    }

    // Utility สาธารณะ (เผื่ออยากเรียกจากที่อื่น)
    public static void ForceRecompute() => RecomputeAndApplyLayout();
}
