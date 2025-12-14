using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class DragDrop : NetworkBehaviour
{
    private LocalHandCard _localHandCard;  // รันบน hand ท้องถิ่นหรือไม่
    public bool IsLocalHandCard => _localHandCard != null;

    public GameObject canvasObject;
    public PlayerManager PlayerManager;

    private bool isDragging = false;
    private bool isDraggable = true;
    private Transform startParent;
    private GameObject dropZone;
    private bool isOverDropZone;

    void Start()
    {
        canvasObject = GameObject.Find("Main Canvas");

        // ไม่ให้ลากถ้าไม่มี authority
        if (!GetComponent<NetworkIdentity>().isOwned)
        {
            isDraggable = false;
        }
    }

    public void SetLocalHandMode(LocalHandCard localHandCard)
    {
        _localHandCard = localHandCard;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("DropZone"))
        {
            isOverDropZone = true;
            dropZone = collision.gameObject;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("DropZone"))
        {
            isOverDropZone = false;
            dropZone = null;
        }
    }

    public void StartDrag()
    {
        if (!isDraggable) return;
        // ถ้ามีสกิลกำลังทำงานอยู่ ไม่ให้ลากใบใหม่จนกว่าจะจบ
        var pm = PlayerManager.localInstance;
        if (pm != null && pm.activeSkillMode != SkillMode.None)
            return;
        isDragging = true;
        startParent = transform.parent;

        ;
    }

    public void EndDrag()
    {
        if (!isDraggable) return;
        // ถ้ามีสกิลกำลังทำงานอยู่ ไม่ให้ลากใบใหม่จนกว่าจะจบ
        var pm = PlayerManager.localInstance;
        if (pm != null && pm.activeSkillMode != SkillMode.None)
            return;
        isDragging = false;
        var rt = transform as RectTransform;

        if (isOverDropZone && dropZone != null)
        {
            transform.SetParent(dropZone.transform, false);
            isDraggable = false;

            if (rt != null)
            {
                rt.anchoredPosition3D = Vector3.zero;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }

            ForceParentLayout(dropZone.transform);

            if (IsLocalHandCard)
            {
                _localHandCard.OnPlayedFromHand();
                return;
            }

            NetworkIdentity networkIdentity = NetworkClient.connection.identity;
            PlayerManager = networkIdentity.GetComponent<PlayerManager>();

            if (PlayerManager != null)
            {
                PlayerManager.PlayCard(gameObject);
            }
        }
        else
        {
            var parent = startParent != null ? startParent : transform.parent;

            transform.SetParent(parent, false);
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.anchoredPosition3D = new Vector3(rt.anchoredPosition3D.x, rt.anchoredPosition3D.y, 0f);
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }
            transform.SetAsLastSibling();

            ForceParentLayout(parent);
        }
    }

    void Update()
    {
        if (isDragging)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f; // keep UI on-plane
            transform.position = mousePos;
            transform.SetParent(canvasObject.transform, true);
        }
    }

    private void ForceParentLayout(Transform parent)
    {
        var parentRt = parent as RectTransform;
        if (parentRt != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
        }
    }
}

