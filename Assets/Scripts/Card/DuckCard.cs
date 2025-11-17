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

    //SyncVar นี้จะบอก Client ทุกคนว่า "PlayerManager netId ไหน" เป็นเจ้าของการ์ดใบนี้
    [SyncVar]
    public uint ownerPlayerManagerNetId;

    private Coroutine _adoptCoroutine;


    // Helper เพื่อหา PlayerManager ที่ "เป็นเจ้าของ" การ์ดใบนี้
    private PlayerManager GetOwnerPlayerManager()
    {
        if (ownerPlayerManagerNetId == 0) return null;
        if (NetworkClient.spawned.TryGetValue(ownerPlayerManagerNetId, out NetworkIdentity ownerIdentity))
        {
            return ownerIdentity.GetComponent<PlayerManager>();
        }
        return null;
    }

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

        if (_adoptCoroutine != null)
            StopCoroutine(_adoptCoroutine);
        _adoptCoroutine = StartCoroutine(AdoptToZoneRoutine());

        HandleTransformUpdate();
    }

    // =================== หา Transform ของโซนจากซีน ==============
    private Transform ResolveZoneTransform(ZoneKind z)
    {
        var localManager = PlayerManager.localInstance;
        var ownerManager = GetOwnerPlayerManager();
        bool belongsToLocal = localManager != null && ownerManager != null && ownerManager.netId == localManager.netId;

        switch (z)
        {
            case ZoneKind.DuckZone:
                if (localManager != null && localManager.DuckZone != null)
                    return localManager.DuckZone.transform;
                return FindZoneRecursive(z);

            case ZoneKind.DropZone:
                if (localManager != null && localManager.DropZone != null)
                    return localManager.DropZone.transform;
                return FindZoneRecursive(z);

            case ZoneKind.TargetZone:
                if (localManager != null && localManager.TargetZone != null)
                    return localManager.TargetZone.transform;
                return FindZoneRecursive(z);

            case ZoneKind.PlayerArea:
                if (localManager == null || ownerManager == null)
                {
                    // ยังไม่มีข้อมูลเพียงพอ ให้ fallback ชั่วคราว
                    return FindZoneRecursive(ZoneKind.PlayerArea, preferEnemy: !belongsToLocal);
                }

                if (belongsToLocal)
                {
                    if (localManager.PlayerArea != null)
                        return localManager.PlayerArea.transform;
                    return FindZoneRecursive(ZoneKind.PlayerArea, preferEnemy: false);
                }

                if (ownerManager.EnemyArea != null)
                    return ownerManager.EnemyArea.transform;

                if (localManager.EnemyArea != null)
                    return localManager.EnemyArea.transform;

                return FindZoneRecursive(ZoneKind.PlayerArea, preferEnemy: true);
        }

        return null;
    }

    // Helper: fallback recursive search on Main Canvas/Image
    private Transform FindZoneRecursive(ZoneKind z, bool preferEnemy = false)
    {
        Transform mainCanvas = GameObject.Find("Main Canvas")?.transform;
        if (mainCanvas == null) return null;

        Transform uiRoot = FindChildRecursive(mainCanvas, "Image");
        if (uiRoot == null) return null;

        string zoneName = "";
        switch (z)
        {
            case ZoneKind.DuckZone: zoneName = "DuckZone"; break;
            case ZoneKind.DropZone: zoneName = "DropZone"; break;
            case ZoneKind.TargetZone: zoneName = "TargetZone"; break;
            case ZoneKind.PlayerArea: zoneName = preferEnemy ? "EnemyArea" : "PlayerArea"; break;
        }

        return FindChildRecursive(uiRoot, zoneName);
    }

    // Helper: ค้นหาลูกแบบ Recursive
    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }


    // ย้ายเข้า parent ของโซน (client-side; idempotent)
    private bool AdoptToZone()
    {
        var parentTransform = ResolveZoneTransform(zone);
        if (parentTransform == null)
            return false;

        if (transform.parent != parentTransform)
            transform.SetParent(parentTransform, false);

        SetLayerRecursively(gameObject, parentTransform.gameObject.layer);
        return true;
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
        HandleTransformUpdate();
    }

    private void OnRowChanged(int oldRow, int newRow)
    {
        HandleTransformUpdate();
    }

    private void OnColChanged(int oldCol, int newCol)
    {
        HandleTransformUpdate();
    }

    private void HandleTransformUpdate()
    {
        // ถ้า Client ยังไม่พร้อม (ยังไม่ Active) อย่าเพิ่งทำ
        if (!NetworkClient.active) return;

        // พยายามย้าย Parent และจัดตำแหน่ง
        // ถ้าล้มเหลว (เพราะ Parent ยังโหลดไม่เสร็จ) ให้ลองใหม่
        if (AdoptToZone())
        {
            ApplyLayout();
            ApplySiblingIndex();
        }
        else
        {
            if (_adoptCoroutine != null)
                StopCoroutine(_adoptCoroutine);
            _adoptCoroutine = StartCoroutine(AdoptToZoneRoutine());
        }
    }

    // (เพิ่มฟังก์ชันนี้)
    // Coroutine ที่จะ "พยายามใหม่" ทุกเฟรมจนกว่าจะหา Parent เจอ
    private IEnumerator AdoptToZoneRoutine()
    {
        const int attempts = 30;
        var wait = new WaitForSeconds(0.1f);

        for (int i = 0; i < attempts; i++)
        {
            if (AdoptToZone())
            {
                ApplyLayout();
                ApplySiblingIndex();
                _adoptCoroutine = null;
                yield break;
            }

            yield return wait;
        }

        Debug.LogWarning($"[DuckCard] {name} (netId={netId}) could not find its Parent Zone ({zone}) after {attempts} attempts!");
        _adoptCoroutine = null;
    }

    // ============== Server: zone assignment helpers for Mirror ==============
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
