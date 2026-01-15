using System;
using System.Reflection;
using Mirror;
using UnityEngine;

public partial class PlayerManager : NetworkBehaviour
{
    static readonly string[] s_colorMemberNames =
    {
        "duckColor", "duckColorIndex", "colorIndex", "playerColor", "playerColorIndex"
    };

    public string ServerTurn_GetDuckColorLabel()
    {
        // ไม่ต้องรู้โครงจริง ใช้ reflection หาค่าที่มีอยู่
        var t = GetType();

        foreach (var name in s_colorMemberNames)
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

        // fallback
        return "UnknownColor";
    }
}
