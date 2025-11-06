using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.EventSystems;

public class DuckCard : NetworkBehaviour, IPointerClickHandler
{
    // ====== สถานะการ์ดฝั่งคลิก ======
    private bool hasShot = false;

    // ====== สถานะบนเครือข่าย ======
    [SyncVar(hook = nameof(OnZoneChanged))] public ZoneKind zone = ZoneKind.None;
    [SyncVar(hook = nameof(OnRowChanged))] public int RowNet;
    [SyncVar(hook = nameof(OnColChanged))] public int ColNet;

    // ====== สะพานให้โค้ดเก่า (ยังใช้ dc.Row / dc.Column ได้) ======
    public int Row
    {
        get => RowNet;
        set
        {
            if (!isServer) { Debug.LogWarning("[DuckCard] Row set on client ignored"); return; }
            RowNet = value;
        }
    }

    public int Column
    {
        get => ColNet;
        set
        {
            if (!isServer) { Debug.LogWarning("[DuckCard] Column set on client ignored"); return; }
            ColNet = value;
        }
    }

    // ============== ชีวิตฝั่ง Client ==============
    public override void OnStartClient()
    {
        base.OnStartClient();
        AdoptToZone();   // จัด parent ให้ตรงโซนปัจจุบัน
        ApplyLayout();   // วางตำแหน่งตาม RowNet/ColNet
        ApplySiblingIndex(); // ให้ลำดับตรงกับ ColNet
    }

    // ============== หา Transform ของโซนจากซีน ==============
    private Transform ResolveZoneTransform(ZoneKind z)
    {
        switch (z)
        {
            case ZoneKind.DuckZone: return GameObject.Find("DuckZone")?.transform;
            case ZoneKind.DropZone: return GameObject.Find("DropZone")?.transform;
            case ZoneKind.PlayerArea: return GameObject.Find("PlayerArea")?.transform;
            default: return null;
        }
    }

    // ย้ายเข้า parent ของโซน (client-side; idempotent)
    private void AdoptToZone()
    {
        var parent = ResolveZoneTransform(zone);
        if (parent != null && transform.parent != parent)
            transform.SetParent(parent, false);
    }

    // จัดตำแหน่ง UI จาก RowNet/ColNet
    private void ApplyLayout()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        const float spacingX = 150f; // ปรับตาม UI ของคุณ
        const float spacingY = 0f;
        rt.anchoredPosition = new Vector2(ColNet * spacingX, RowNet * spacingY);
    }

    private void ApplySiblingIndex()
    {
        var parent = transform.parent;
        if (parent != null)
        {
            int idx = Mathf.Clamp(ColNet, 0, parent.childCount);
            transform.SetSiblingIndex(idx);
        }
    }

    // ============== Hooks ของ SyncVar ==============
    private void OnZoneChanged(ZoneKind oldZ, ZoneKind newZ)
    {
        AdoptToZone();
        ApplyLayout();
        ApplySiblingIndex();
    }

    private void OnRowChanged(int oldRow, int newRow)
    {
        ApplyLayout();
    }

    private void OnColChanged(int oldCol, int newCol)
    {
        ApplyLayout();
        ApplySiblingIndex();
    }

    // ============== ฝั่ง Server: ตั้งโซน/พิกัดแบบถูกหลัก Mirror ==============
    [Server]
    public void ServerAssignToZone(ZoneKind newZone, int row, int col)
    {
        zone = newZone;  // SyncVar → replicate ไปทุก client (รวม late-joiner)
        RowNet = row;
        ColNet = col;

        // ให้เซิร์ฟเวอร์จัด parent/sibling เพื่อคงลำดับเดียวกัน
        var parent = ResolveZoneTransform(zone);
        if (parent != null)
        {
            transform.SetParent(parent, false);
            transform.SetSiblingIndex(Mathf.Clamp(col, 0, parent.childCount));
        }
    }

    // ============== Interaction ==============
    public void OnPointerClick(PointerEventData eventData)
    {
        var localPM = NetworkClient.connection?.identity?.GetComponent<PlayerManager>();
        if (localPM == null)
        {
            Debug.LogWarning("[DuckCard] No local PlayerManager found, can't click.");
            return;
        }

        // Shoot
        if (localPM.IsShootActive)
        {
            if (hasShot)
            {
                Debug.Log("[DuckCard] Already shot once, ignore double click!");
                return;
            }

            var cardIdentity = GetComponent<NetworkIdentity>();
            if (cardIdentity != null)
            {
                localPM.CmdShootCard(cardIdentity);
                Debug.Log($"[DuckCard] Requesting to shoot card: {cardIdentity.name}");
                hasShot = true;
            }
            return;
        }

        // QuickShot
        if (localPM.IsQuickShotActive)
        {
            if (hasShot)
            {
                Debug.Log("[DuckCard] Already shot once, ignore double click!");
                return;
            }

            var cardIdentity = GetComponent<NetworkIdentity>();
            if (cardIdentity != null)
            {
                localPM.CmdQuickShotCard(cardIdentity);
                Debug.Log($"[DuckCard] Requesting to quick-shoot card: {cardIdentity.name}");
                hasShot = true;
            }
            return;
        }

        // TekeAim
        if (localPM.IsTekeAimActive)
        {
            var cardIdentity = GetComponent<NetworkIdentity>();
            if (cardIdentity != null)
            {
                localPM.CmdSpawnTarget(cardIdentity);
                localPM.CmdDeactivateTekeAim();
                Debug.Log($"[DuckCard] TekeAim -> Spawn target above card: {cardIdentity.name}");
            }
            return;
        }

        // DoubleBarrel
        if (localPM.IsDoubleBarrelActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdDoubleBarrelClick(cardId);
            return;
        }

        // Misfire
        if (localPM.IsMisfireActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdMisfireClick(cardId);
            return;
        }

        // TwoBirds
        if (localPM.IsTwoBirdsActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdTwoBirdsClick(cardId);
            return;
        }

        // BumpLeft
        if (localPM.IsBumpLeftActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdBumpLeftClick(cardId);
            return;
        }

        // BumpRight
        if (localPM.IsBumpRightActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdBumpRightClick(cardId);
            return;
        }

        // MoveAhead
        if (localPM.IsMoveAheadActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdMoveAheadClick(cardId);
            return;
        }

        // HangBack
        if (localPM.IsHangBackActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdHangBackClick(cardId);
            return;
        }

        // FastForward
        if (localPM.IsFastForwardActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdFastForwardClick(cardId);
            return;
        }

        // Disorderly Conduckt
        if (localPM.IsDisorderlyConducktActive)
        {
            var cardId = GetComponent<NetworkIdentity>();
            if (cardId != null) localPM.CmdDisorderlyClick(cardId);
            return;
        }

        Debug.Log("[DuckCard] Clicked card outside of action modes, ignoring...");
    }
}
