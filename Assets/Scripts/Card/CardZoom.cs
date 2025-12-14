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

        // สร้างการ์ดซูมขึ้นมา
        zoomCard = Instantiate(ZoomCard, new Vector2(Input.mousePosition.x, Input.mousePosition.y + 250), Quaternion.identity);
        zoomCard.GetComponent<Image>().sprite = zoomSprite;

        // ตั้งการ์ดให้เป็นลูกของ Canvas
        zoomCard.transform.SetParent(Canvas.transform, true);

        // ปรับขนาดการ์ดที่ซูมให้ใหญ่ขึ้น
        RectTransform rect = zoomCard.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180, 258);

        // ปรับตำแหน่งให้เป็นตามหน้าจอในโหมด Screen Space - Camera
        rect.anchoredPosition = new Vector2(Input.mousePosition.x - Screen.width / 2, Input.mousePosition.y - Screen.height / 2 + 250);

        // ปรับขนาดโดยใช้ localScale เพื่อให้การ์ดไม่เล็กเกินไป
        zoomCard.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
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
