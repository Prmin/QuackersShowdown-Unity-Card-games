using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class DragDrop : NetworkBehaviour
{
    public GameObject canvasObject;
    public PlayerManager PlayerManager;

    private bool isDragging = false;
    private bool isDraggable = true;
    private GameObject startParent;
    private Vector2 startPosition;
    private GameObject dropZone;
    private bool isOverDropZone;

    void Start()
    {
        canvasObject = GameObject.Find("Main Canvas");

        // แก้ไขการตรวจสอบ authority
        if (!GetComponent<NetworkIdentity>().isOwned)
        {
            isDraggable = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("DropZone"))  // ตรวจสอบด้วยแท็ก
        {
            isOverDropZone = true;
            dropZone = collision.gameObject;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("DropZone"))  // ตรวจสอบด้วยแท็ก
        {
            isOverDropZone = false;
            dropZone = null;
        }
    }

    public void StartDrag()
    {
        if (!isDraggable) return;
        isDragging = true;
        startParent = transform.parent.gameObject;
        startPosition = transform.position;

        Debug.Log("Start Dragging: " + gameObject.name);  // เช็คว่าเข้าสู่ฟังก์ชันนี้หรือไม่
    }

    public void EndDrag()
    {
        if (!isDraggable) return;
        isDragging = false;
        if (isOverDropZone && dropZone != null)
        {
            transform.SetParent(dropZone.transform, false);
            isDraggable = false;

            NetworkIdentity networkIdentity = NetworkClient.connection.identity;
            PlayerManager = networkIdentity.GetComponent<PlayerManager>();

            // ตรวจสอบว่า PlayerManager ไม่เป็น null ก่อนเรียกใช้
            if (PlayerManager != null)
            {
                PlayerManager.PlayCard(gameObject);
            }
        }
        else
        {
            transform.position = startPosition;
            transform.SetParent(startParent.transform, false);
        }
    }

    void Update()
    {
        if (isDragging)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition); // เปลี่ยนตำแหน่งเมาส์ให้เป็นตำแหน่งในโลก
            transform.position = mousePos;
            transform.SetParent(canvasObject.transform, true); // ใช้ canvasObject แทน
        }
    }

}
