using UnityEngine;
using UnityEngine.EventSystems; // ต้องใช้สำหรับ IDropHandler
using Mirror;

public class DropZone : MonoBehaviour, IDropHandler
{
    private GameObject currentCard;

    // ถ้าอยากให้ DropZone ทำลายการ์ดเก่าก่อนใส่การ์ดใหม่
    // ตามเดิมในฟังก์ชัน PlaceCard
    public void PlaceCard(GameObject newCard)
    {
        // ลบการ์ดเก่า
        if (currentCard != null)
        {
            Destroy(currentCard);
        }
        // ตั้งการ์ดใหม่
        currentCard = newCard;
        currentCard.transform.SetParent(transform, false);
    }

    // -------------------------------------------
    // ส่วนสำคัญ: ตรวจจับการ "ลาก/ปล่อย" การ์ดลง DropZone
    // -------------------------------------------
    public void OnDrop(PointerEventData eventData)
    {
        // eventData.pointerDrag คือ GameObject การ์ดที่ถูกลากมาวาง
        GameObject droppedCard = eventData.pointerDrag;
        if (droppedCard == null) return;

        // 1) เอาการ์ดนี้มาวางบน DropZone (ตามหลักการที่มีอยู่แล้ว)
        PlaceCard(droppedCard);

        // 2) เรียกให้ PlayerManager ทำ PlayCard เพื่อเปลี่ยนสถานะเป็น "Played"
        //    อ้างอิง localPlayer ของ Mirror
        if (NetworkClient.localPlayer != null)
        {
            var playerManager = NetworkClient.localPlayer.GetComponent<PlayerManager>();
            if (playerManager != null)
            {
                playerManager.PlayCard(droppedCard);
            }
        }
    }
}
