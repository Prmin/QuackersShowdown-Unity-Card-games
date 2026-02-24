using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class CardZoom : NetworkBehaviour
{
    public GameObject Canvas;
    public GameObject ZoomCard;

    private GameObject zoomCard;
    private Sprite zoomSprite;
    private const float ZoomScale = 3f; // was 1.5f
    private static readonly Vector2 ZoomBaseSize = new Vector2(180f, 258f);

    public void Awake()
    {
        // หาวัตถุ Main Canvas ในซีน
        Canvas = GameObject.Find("Main Canvas");
        // ดึง sprite ของการ์ดจากวัตถุ Image ที่เกี่ยวข้อง
        zoomSprite = gameObject.GetComponent<Image>().sprite;
    }

    // ฟังก์ชันเมื่อเม้าส์ชี้ไปที่การ์ด
    public void OnHoverEnter()
    {

        // กันเคสถูก UnityEvent เรียกทั้งๆที่ component ถูกปิด
        if (!isActiveAndEnabled) return;

        // ✅ กันซูมถ้าการ์ดอยู่ใน DropZone (เช็คจาก parent จริง)
        if (GetComponentInParent<DropZone>() != null) return;

        // (เสริม) ถ้าอยากเช็คด้วย zone ด้วยก็ได้ แต่ zone อาจมาไม่ทัน
        var dc = GetComponent<DuckCard>();
        if (dc != null && dc.zone == ZoneKind.DropZone) return;

        // เช็คว่า client นี้เป็นเจ้าของออบเจกต์หรือไม่ ถ้าไม่ใช่ ก็ return ออกไป
        NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
        if (!networkIdentity.isOwned) return;

        // ตรวจสอบว่าการ์ดซูมถูกสร้างหรือยัง ถ้าถูกสร้างแล้วก็ไม่ต้องสร้างซ้ำ
        if (zoomCard != null) return;

        if (Canvas == null)
            Canvas = GameObject.Find("Main Canvas");
        if (Canvas == null || ZoomCard == null) return;

        // Spawn as child immediately so positioning uses canvas-local coordinates.
        zoomCard = Instantiate(ZoomCard, Canvas.transform, false);

        Image zoomImage = zoomCard.GetComponent<Image>();
        if (zoomImage != null)
            zoomImage.sprite = zoomSprite;

        // Center of screen + larger scale
        RectTransform rect = zoomCard.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ZoomBaseSize;
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * ZoomScale;
        }
    }

    // ฟังก์ชันเมื่อเม้าส์เลิกชี้การ์ด
    public void OnHoverExit()
    {
        // ตรวจสอบว่า zoomCard ถูกสร้างขึ้นหรือไม่
        if (zoomCard != null)
        {
            // ถ้ามี zoomCard ให้ทำลาย
            Destroy(zoomCard);
            zoomCard = null;  // รีเซ็ตให้เป็น null หลังทำลายการ์ด
        }


    }

    private void OnDisable()
    {
        OnHoverExit();
    }

    private void OnDestroy()
    {
        OnHoverExit();
    }

}
