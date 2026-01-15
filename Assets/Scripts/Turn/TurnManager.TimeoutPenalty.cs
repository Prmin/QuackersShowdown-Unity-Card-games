using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public partial class TurnManager
{
    // ลงโทษ: ทำลาย "เป็ดของคนนั้น" 1 ใบ
    // ลำดับ: Deck/กอง (inactive) ก่อน -> ถ้าไม่มีแล้วค่อย DuckZone
    // log: ใครโดน, สีอะไร, ทำลายจากไหน, เหลือสีนี้รวม (deck+duckzone) กี่ใบ

    [Server]
    private void ServerApplyTimeoutPenalty(uint offenderNetId, TurnPhase phaseWhenTimeout)
    {
        var offender = FindObjectsOfType<PlayerManager>().FirstOrDefault(p => p != null && p.netId == offenderNetId);
        if (offender == null)
        {
            Debug.LogWarning($"[TurnManager][PENALTY] offender netId={offenderNetId} not found");
            return;
        }

        string color = offender.ServerTurn_GetDuckColorLabel();

        // หา duck cards ทั้งหมดของคนนี้ (รวม inactive) แต่กรองให้เป็น object ใน scene จริง
        var allOwned = Resources.FindObjectsOfTypeAll<DuckCard>()
            .Where(c => c != null && c.gameObject != null && c.gameObject.scene.IsValid())
            .Where(c => c.ownerNetId == offenderNetId)
            .ToList();

        // “เด็ค/กอง” = inactive เท่านั้น (ปลอดภัย ไม่ไปโดน action card ที่โชว์อยู่)
        var deck = allOwned.Where(c => !c.gameObject.activeInHierarchy).ToList();

        // “ในสนาม” = อยู่ DuckZone และ active
        var zone = allOwned.Where(c => c.gameObject.activeInHierarchy && c.zone == ZoneKind.DuckZone).ToList();

        // รวมที่เหลือของสีนี้ (deck+duckzone) ก่อนทำลาย
        int totalBefore = deck.Count + zone.Count;

        if (totalBefore <= 0)
        {
            Debug.LogWarning($"[TurnManager][PENALTY] offender seat={offender.SeatIndex} netId={offenderNetId} color={color} -> no duck left");
            return;
        }

        DuckCard victim = null;
        string from = "";

        if (deck.Count > 0)
        {
            victim = deck[Random.Range(0, deck.Count)];
            from = "Deck";
        }
        else
        {
            victim = zone[Random.Range(0, zone.Count)];
            from = "DuckZone";
        }

        DestroyNetworkObjectSafe(victim.gameObject);

        int remaining = Mathf.Max(0, totalBefore - 1);

        Debug.Log(
            $"[TurnManager][PENALTY] TIMEOUT({phaseWhenTimeout}) offender seat={offender.SeatIndex} netId={offenderNetId} color={color} " +
            $"-> destroyed 1 from {from}. remaining({color})={remaining} (Deck+DuckZone)"
        );
    }

    [Server]
    private static void DestroyNetworkObjectSafe(GameObject go)
    {
        if (go == null) return;

        if (NetworkServer.active)
        {
            var ni = go.GetComponent<NetworkIdentity>();
            if (ni != null && ni.netId != 0 && NetworkServer.spawned.ContainsKey(ni.netId))
            {
                NetworkServer.Destroy(go);
                return;
            }
        }

        Object.Destroy(go);
    }
}
