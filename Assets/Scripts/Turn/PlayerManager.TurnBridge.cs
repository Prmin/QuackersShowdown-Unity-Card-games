using System.Reflection;
using Mirror;
using UnityEngine;

public partial class PlayerManager : NetworkBehaviour
{
    //  ใช้ตอน timeout/penalty เพื่อกัน skill mode ค้างแล้วล็อกคนทั้งเกม
    [Server]
    public void ServerTurn_CancelPendingAbility()
    {
        // ตัวนี้มาจากของเดิมที่นายใช้ใน DragDrop อยู่แล้ว
        activeSkillMode = SkillMode.None;

        // เคลียร์พวกตัวแปร click count / first selected ที่อาจค้าง
        // ทำแบบ reflection เพื่อไม่ผูกชื่อ field แบบตายตัว
        string[] intFields = { "doubleBarrelClickCount", "twoBirdsClickCount", "disorderlyClickCount" };
        string[] objFields = { "firstClickedCard", "firstTwoBirdsCard", "firstSelectedDuck" };

        var t = GetType();

        foreach (var f in intFields)
        {
            var fi = t.GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null && fi.FieldType == typeof(int)) fi.SetValue(this, 0);
        }

        foreach (var f in objFields)
        {
            var fi = t.GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null) fi.SetValue(this, null);
        }
    }

    //  ใช้ log ว่าเจ้าของ “สีอะไร”
    public string ServerTurn_GetDuckColorLabel()
    {
        var t = GetType();
        string[] candidates = { "duckColor", "duckColorIndex", "playerColor", "playerColorIndex", "DuckColor", "ColorIndex" };

        foreach (var name in candidates)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                var v = f.GetValue(this);
                if (v != null) return v.ToString();
            }

            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null)
            {
                var v = p.GetValue(this);
                if (v != null) return v.ToString();
            }
        }

        // fallback: ยังไงก็มี seat
        return $"Seat{SeatIndex}";
    }
}
