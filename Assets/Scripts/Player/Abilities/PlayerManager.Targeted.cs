using System.Collections;
using Mirror;
using UnityEngine;

public partial class PlayerManager
{
    // ===== TakeAim / Target =====
    [Command(requiresAuthority = false)]
    public void CmdSpawnTarget(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null || targetPrefab == null) return;
        var dc = duckCardIdentity.GetComponent<DuckCard>();
        if (dc == null) return;
        if (dc.zone != ZoneKind.DuckZone)
        {
            Debug.LogWarning($"[TakeAim] Ignore target spawn: {duckCardIdentity.name} is in zone {dc.zone}");
            return;
        }
        RemoveTargetFromCard(duckCardIdentity); // ลบ Target เดิมก่อนสร้างใหม่
        GameObject newTarget = Instantiate(targetPrefab);
        var marker = newTarget.GetComponent<TargetMarker>();
        var tf = newTarget.GetComponent<TargetFollow>();
        if (marker != null)
        {
            marker.ServerAssignToZone(ZoneKind.TargetZone, 0, dc.ColNet);
            marker.FollowDuckNetId = duckCardIdentity.netId;
        }
        if (tf != null) tf.targetNetId = duckCardIdentity.netId;
        NetworkServer.Spawn(newTarget);
    }

    [ClientRpc]
    void RpcSetTargetNetId(NetworkIdentity targetIdentity, NetworkIdentity duckCardIdentity)
    {
        // กัน null/วัตถุหายฝั่ง client
        if (!NetworkClient.active) return;
        if (targetIdentity == null || duckCardIdentity == null || duckCardIdentity.gameObject == null)
        {
            Debug.LogWarning("[RpcSetTargetNetId] target หรือการ์ดเป้าหมายเป็น null ข้ามการตั้งค่า");
            return;
        }

        try
        {
            TargetFollow tf = targetIdentity.GetComponent<TargetFollow>();
            if (tf != null)
            {
                tf.targetNetId = duckCardIdentity.netId;
                tf.ResetTargetTransform();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RpcSetTargetNetId] ขัดข้อง: {ex}");
        }
    }

    // ===== Shoot =====
    [Command(requiresAuthority = false)]
    public void CmdShootCard(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null) return;
        var shotDuck = duckCardIdentity.GetComponent<DuckCard>();
        if (shotDuck == null) return;
        if (!IsCardTargeted(duckCardIdentity)) return;

        int shotRow = shotDuck.RowNet;
        int shotCol = shotDuck.ColNet;
        NetworkServer.Destroy(duckCardIdentity.gameObject);
        Server_DestroyAllTargetsFor(duckCardIdentity.netId);
        Server_ResequenceDuckZoneColumns();

        activeSkillMode = SkillMode.None;
        StartCoroutine(RefillNextFrame());
    }

    [Server]
    IEnumerator RefillNextFrame()
    {
        yield return null;
        RefillDuckZoneIfNeeded();
    }

    [Server]
    private void Server_DestroyAllTargetsFor(uint duckNetId)
    {
        // ลบ TargetMarker
        var markers = FindObjectsOfType<TargetMarker>();
        foreach (var m in markers)
            if (m != null && m.FollowDuckNetId == duckNetId)
                NetworkServer.Destroy(m.gameObject);

        // สำรอง: TargetFollow
        var follows = FindObjectsOfType<TargetFollow>();
        foreach (var f in follows)
            if (f != null && f.targetNetId == duckNetId)
                NetworkServer.Destroy(f.gameObject);
    }

    // ===== Target helper =====
    bool IsCardTargeted(NetworkIdentity duckCardIdentity)
    {
        if (duckCardIdentity == null) return false;
        uint duckId = duckCardIdentity.netId;
        var markers = FindObjectsOfType<TargetMarker>();
        foreach (var m in markers)
            if (m != null && m.FollowDuckNetId == duckId)
                return true;
        var follows = FindObjectsOfType<TargetFollow>();
        foreach (var f in follows)
            if (f != null && f.targetNetId == duckId)
                return true;
        return false;
    }
}
