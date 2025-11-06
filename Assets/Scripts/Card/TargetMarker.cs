using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class TargetMarker : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnZoneChanged))] public ZoneKind zone = ZoneKind.TargetZone;
    [SyncVar(hook = nameof(OnRowChanged))] public int RowNet;
    [SyncVar(hook = nameof(OnColChanged))] public int ColNet;

    // ให้เป้า “รู้” ว่าจะตามการ์ดใบไหน
    [SyncVar(hook = nameof(OnFollowChanged))] public uint FollowDuckNetId;

    public override void OnStartClient()
    {
        base.OnStartClient();
        AdoptToZone();
        ApplySibling();
    }

    Transform ResolveZoneTransform(ZoneKind z)
    {
        switch (z)
        {
            case ZoneKind.TargetZone: return GameObject.Find("TargetZone")?.transform;
            case ZoneKind.DuckZone:   return GameObject.Find("DuckZone")?.transform;
            case ZoneKind.DropZone:   return GameObject.Find("DropZone")?.transform;
            case ZoneKind.PlayerArea: return GameObject.Find("PlayerArea")?.transform;
            default: return null;
        }
    }

    void AdoptToZone()
    {
        var p = ResolveZoneTransform(zone);
        if (p != null && transform.parent != p)
            transform.SetParent(p, false);
    }

    void ApplySibling()
    {
        var p = transform.parent;
        if (p != null)
        {
            int idx = Mathf.Clamp(ColNet, 0, p.childCount);
            transform.SetSiblingIndex(idx);
        }
    }

    void OnZoneChanged(ZoneKind o, ZoneKind n) { AdoptToZone(); ApplySibling(); }
    void OnRowChanged(int o, int n)            { /* เผื่ออนาคต */ }
    void OnColChanged(int o, int n)            { ApplySibling(); }

    void OnFollowChanged(uint oldId, uint newId)
    {
        // ส่งค่าให้ TargetFollow ผ่าน SyncVar (ไม่ต้อง RPC)
        var tf = GetComponent<TargetFollow>();
        if (tf != null)
        {
            tf.targetNetId = newId;   // จะเรียก hook ใน TargetFollow เอง (ดูข้อ 2)
            tf.ResetTargetTransform();
        }
    }

    [Server]
    public void ServerAssignToZone(ZoneKind z, int row, int col)
    {
        zone = z; RowNet = row; ColNet = col;

        var p = ResolveZoneTransform(z);
        if (p != null)
        {
            transform.SetParent(p, false);
            transform.SetSiblingIndex(Mathf.Clamp(col, 0, p.childCount));
        }
    }
}
