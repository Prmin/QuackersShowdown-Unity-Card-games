using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.EventSystems;

public class DuckCard : NetworkBehaviour, IPointerClickHandler
{
    // ====== สถานะการ์ดฝั่งคลิก ======
    // private bool hasShot = false;

    // ====== สถานะบนเครือข่าย ======
    [SyncVar(hook = nameof(OnZoneChanged))] public ZoneKind zone = ZoneKind.None;
    [SyncVar(hook = nameof(OnRowChanged))] public int RowNet;
    [SyncVar(hook = nameof(OnColChanged))] public int ColNet;



    void Update()
    {
        // เช็กเฉพาะตอนอยู่ DropZone
        if (zone == ZoneKind.DropZone && transform.parent != null)
        {
            var rt = GetComponent<RectTransform>();

            // 1. เช็กและแก้ Z
            if (Mathf.Abs(rt.anchoredPosition3D.z) > 0.01f)
            {
                Debug.LogWarning($"[Fixing] {name} Z-Pos was {rt.anchoredPosition3D.z}");
                var p = rt.anchoredPosition3D;
                p.z = 0;
                rt.anchoredPosition3D = p;
            }

            // 2. เช็กและแก้ Scale (สำคัญ! บางที Mirror Spawn มาแล้ว Scale เป็น 0)
            if (transform.localScale.x < 0.1f)
            {
                Debug.LogWarning($"[Fixing] {name} Scale was too small ({transform.localScale})");
                transform.localScale = Vector3.one;
            }

            // 3. เช็ก Rotation (ถ้าหมุน 90 องศา มันจะบางจนมองไม่เห็น)
            if (transform.localRotation != Quaternion.identity)
            {
                Debug.LogWarning($"[Fixing] {name} Rotation was {transform.localRotation}");
                transform.localRotation = Quaternion.identity;
            }
        }
    }



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
        var parentTransform = ResolveZoneTransform(zone);

        // กันเหนียว: ถ้าหา DropZone ไม่เจอ ให้หาใหม่
        if (parentTransform == null && zone == ZoneKind.DropZone)
        {
            var go = GameObject.Find("DropZone");
            if (go != null) parentTransform = go.transform;
        }

        if (parentTransform != null)
        {
            // ตรวจสอบว่า Parent เปลี่ยนหรือไม่
            if (transform.parent != parentTransform)
            {
                // ★ worldPositionStays = false คือหัวใจสำคัญ
                // มันจะพยายามรีเซ็ต local transform ให้เกาะติด parent ทันที
                transform.SetParent(parentTransform, false);
            }

            // ★ บังคับ Layer ให้ตรงกับ Parent (แก้ปัญหามองไม่เห็นข้าม Layer)
            SetLayerRecursively(gameObject, parentTransform.gameObject.layer);
        }
    }

    // ฟังก์ชันช่วยเปลี่ยน Layer ทั้งตัวและลูกๆ
    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }


    // จัดตำแหน่ง UI จาก RowNet/ColNet
    private void ApplyLayout()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // 1. บังคับ Scale เป็น 1 เสมอ
        rt.localScale = Vector3.one;

        // 2. หมุนให้ตรง
        rt.localRotation = Quaternion.identity;

        // 3. ★ สำคัญมากสำหรับ Screen Space Camera: ต้องรีเซ็ต Z เป็น 0
        // ใช้ anchoredPosition3D เพื่อเข้าถึงแกน Z
        Vector3 currentPos = rt.anchoredPosition3D;
        currentPos.z = 0;
        rt.anchoredPosition3D = currentPos;

        // 4. Logic ตำแหน่ง X, Y
        if (zone == ZoneKind.DropZone)
        {
            // อยู่กลางกอง DropZone
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // ★ บังคับ Z=0 อีกครั้งในบรรทัดเดียว
            rt.anchoredPosition3D = Vector3.zero;
        }
        else
        {
            // เรียงใน DuckZone/PlayerArea
            const float spacingX = 150f;

            // ใช้ anchoredPosition3D แทน anchoredPosition เฉยๆ
            rt.anchoredPosition3D = new Vector3(ColNet * spacingX, 0f, 0f);
        }
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
        Debug.Log($"[DuckCard {netId}] OnZoneChanged: {oldZ} -> {newZ} parent={transform.parent?.name}");
        AdoptToZone();
        ApplyLayout();
        ApplySiblingIndex();


        // บังคับเปิด ไม่น่าจะเป็นสาเหตุหลัก แต่กันเหนียว
        gameObject.SetActive(true);

        // บังคับให้ Canvas จะรีคัลคูลเลย์เอาต์ (ช่วยในกรณี race)
        Canvas.ForceUpdateCanvases();
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
        // 1. หา PlayerManager ของเรา
        var localPM = NetworkClient.connection?.identity?.GetComponent<PlayerManager>();
        if (localPM == null)
        {
            Debug.LogWarning("[DuckCard] No local PlayerManager found, can't click.");
            return;
        }

        // 2. (โค้ดใหม่) ส่ง "ตัวเราเอง" (this) ไปให้ PlayerManager
        // PlayerManager จะใช้ switch(activeSkillMode) จัดการเอง
        localPM.HandleDuckCardClick(this);

        Debug.Log("[DuckCard] Clicked card outside of action modes, ignoring...");
    }
}
