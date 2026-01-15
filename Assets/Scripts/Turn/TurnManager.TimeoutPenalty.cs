using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEngine;

public partial class TurnManager
{
    // ======== Penalty Rules ========
    // 1) ทำลาย "การ์ดเป็ดของคนนั้น" ที่อยู่ในกอง/เด็ค ก่อน 1 ใบ
    // 2) ถ้าไม่มีแล้ว ค่อยทำลาย "การ์ดเป็ดของคนนั้น" ที่อยู่ใน DuckZone
    // 3) log: เทิร์นใคร / สีอะไร / โดนทำลายสีไหน / เหลือทั้งหมดกี่ใบ (รวมเด็ค+DuckZone)

    [Server]
    private void ServerApplyTimeoutPenalty(uint offenderNetId)
    {
        var offender = FindPlayerByNetId(offenderNetId);
        if (offender == null)
        {
            Debug.LogWarning($"[TurnManager][PENALTY] offender netId={offenderNetId} not found");
            return;
        }

        var offenderColor = GetDuckColorLabel(offender);

        // รวบรวม "การ์ดเป็ด" ของ offender ทั้งหมด (รวม inactive) จาก scene
        var ownedDucks = GetAllDuckCardsInScene()
            .Where(c => c != null && c.ownerNetId == offenderNetId)
            .Where(IsDuckCard) // กันเผลอไปโดน action card
            .ToList();

        // เด็ค/กอง: นิยามด้วย "ไม่อยู่ DuckZone หรือ inactive"
        var deckCandidates = ownedDucks
            .Where(c => c.zone != ZoneKind.DuckZone || !c.gameObject.activeInHierarchy)
            .ToList();

        // DuckZone: อยู่ในสนามจริง
        var zoneCandidates = ownedDucks
            .Where(c => c.zone == ZoneKind.DuckZone && c.gameObject.activeInHierarchy)
            .ToList();

        DuckCard victim = null;
        string from = "";

        if (deckCandidates.Count > 0)
        {
            victim = deckCandidates[Random.Range(0, deckCandidates.Count)];
            from = "Deck";
        }
        else if (zoneCandidates.Count > 0)
        {
            victim = zoneCandidates[Random.Range(0, zoneCandidates.Count)];
            from = "DuckZone";
        }
        else
        {
            Debug.LogWarning($"[TurnManager][PENALTY] offender seat={offender.SeatIndex} netId={offenderNetId} color={offenderColor} -> no owned duck left to destroy");
            return;
        }

        // ทำลายแบบ server-authoritative
        DestroyDuckCardServer(victim);

        // นับจำนวนที่เหลือของ "สี offender" รวมเด็ค+DuckZone (หลังทำลาย)
        int remaining = GetAllDuckCardsInScene()
            .Count(c => c != null
                     && c.ownerNetId == offenderNetId
                     && IsDuckCard(c)
                     && c != victim);

        Debug.Log(
            $"[TurnManager][PENALTY] offender seat={offender.SeatIndex} netId={offenderNetId} color={offenderColor} " +
            $"=> destroyed 1 duck from {from}. remaining({offenderColor})={remaining}"
        );
    }

    // ======== Helpers ========

    [Server]
    private PlayerManager FindPlayerByNetId(uint netId)
    {
        // ถ้าคุณมี _playersByNetId อยู่แล้ว ใช้อันนั้นก็ได้
        // แต่ทำแบบหาใน scene จะไม่พังแม้ dict ยังไม่ครบ
        return FindObjectsOfType<PlayerManager>().FirstOrDefault(p => p != null && p.netId == netId);
    }

    [Server]
    private static IEnumerable<DuckCard> GetAllDuckCardsInScene()
    {
        // รวม inactive ด้วย แต่กรองให้เหลือเฉพาะ object ที่อยู่ใน scene (กัน prefab asset)
        return Resources.FindObjectsOfTypeAll<DuckCard>()
            .Where(c => c != null && c.gameObject != null && c.gameObject.scene.IsValid());
    }

    [Server]
    private static bool IsDuckCard(DuckCard c)
    {
        // เกมนี้ "เป็ด" อยู่ DuckZone เป็นหลัก
        if (c.zone == ZoneKind.DuckZone) return true;

        // อะไรที่อยู่ PlayerArea/DropZone/TargetZone มักเป็น action/target
        if (c.zone == ZoneKind.PlayerArea || c.zone == ZoneKind.DropZone || c.zone == ZoneKind.TargetZone)
            return false;

        // ถ้าไม่แน่ใจ = ไม่ทำลาย (กันเผลอลบ action card)
        return false;
    }

    [Server]
    private static void DestroyDuckCardServer(DuckCard victim)
    {
        if (victim == null) return;

        // ถ้าเป็น network object ที่ถูก spawn แล้วจริง -> NetworkServer.Destroy
        if (NetworkServer.active)
        {
            var ni = victim.netIdentity;
            if (ni != null && ni.netId != 0 && NetworkServer.spawned.ContainsKey(ni.netId))
            {
                NetworkServer.Destroy(victim.gameObject);
                return;
            }
        }

        // ไม่ได้เป็น spawned network object -> destroy ปกติ
        Object.Destroy(victim.gameObject);
    }

    [Server]
    private static string GetDuckColorLabel(PlayerManager pm)
    {
        if (pm == null) return "UnknownColor";

        // พยายามหา field/property ที่พอจะบอกสีได้ (กันชื่อไม่ตรงในแต่ละไฟล์)
        string[] candidates = { "duckColor", "duckColorIndex", "playerColor", "playerColorIndex", "DuckColor", "ColorIndex" };
        var t = pm.GetType();

        foreach (var name in candidates)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                var v = f.GetValue(pm);
                if (v != null) return v.ToString();
            }

            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null)
            {
                var v = p.GetValue(pm);
                if (v != null) return v.ToString();
            }
        }

        // fallback: ใช้ seat เป็นตัวระบุแทนก่อน
        return $"Seat{pm.SeatIndex}";
    }
}
