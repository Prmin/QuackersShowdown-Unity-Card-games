using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class DragDrop : NetworkBehaviour
{
    private DuckCard _duck;

    private LocalHandCard _localHandCard;
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

    void Awake()
    {
        _duck = GetComponent<DuckCard>();
    }

    // ====== ตรวจสอบว่าลากได้หรือไม่ ตามเทิร์น =====
    private bool CanDragByTurn()
    {
        var tm = TurnManager.Instance;
        var pm = PlayerManager.localInstance;

        if (tm == null || pm == null) return false;

        // ต้องเป็นเทิร์นของเรา + ต้องอยู่ Phase A เท่านั้น
        if (tm.CurrentPlayerNetId != pm.netId) return false;
        if (tm.Phase != TurnPhase.PlayActionCard) return false;

        // ถ้ามีสกิลค้าง ไม่ให้ลากใบใหม่
        if (pm.activeSkillMode != SkillMode.None) return false;

        return true;
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
        if (!CanDragNow()) return;
        if (!CanDragByTurn()) return;

        if (!isDraggable) return;
        // ถ้ามีสกิลกำลังทำงานอยู่ ไม่ให้ลากใบใหม่จนกว่าจะจบ
        var pm = PlayerManager.localInstance;
        if (pm != null && pm.activeSkillMode != SkillMode.None)
            return;
        isDragging = true;
        startParent = transform.parent;

    }

    public void EndDrag()
    {
        if (!isDraggable) return;

        // ถ้าปล่อยตอนผิดเทิร์น ก็ให้เด้งกลับ
        if (!CanDragByTurn())
        {
            isDragging = false;

            if (startParent != null)
                transform.SetParent(startParent, false);

            var rt0 = transform as RectTransform;
            if (rt0 != null) rt0.anchoredPosition = Vector2.zero;

            return;
        }

        // ถ้ามีสกิลกำลังทำงานอยู่ ไม่ให้ลากใบใหม่จนกว่าจะจบ
        var pm = PlayerManager.localInstance;
        if (pm != null && pm.activeSkillMode != SkillMode.None)
            return;
        isDragging = false;
        var rt = transform as RectTransform;

        if (isOverDropZone && dropZone != null)
        {
            transform.SetParent(dropZone.transform, false);
            GetComponent<CardZoom>()?.OnHoverExit();
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
            // ถ้าระหว่างลากอยู่ แล้วไม่ใช่เทิร์นเราแล้ว เด้งกลับ
            if (!CanDragByTurn())
            {
                isDragging = false;

                if (startParent != null)
                    transform.SetParent(startParent, false);

                // คืนตำแหน่งให้เรียบร้อย
                var rt = transform as RectTransform;
                if (rt != null) rt.anchoredPosition = Vector2.zero;

                return;
            }

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

    // ===== ตรวจสอบว่าลากได้หรือไม่ =====
    private bool CanDragNow()
    {
        if (!isDraggable) return false;

        // ถ้ามีสกิลกำลังทำงานอยู่ ไม่ให้ลากใบใหม่จนกว่าจะจบ
        var pm = PlayerManager.localInstance;
        if (pm != null && pm.activeSkillMode != SkillMode.None)
            return false;

        //  ต้องเป็นเทิร์นของเรา  ต้องอยู่ Phase A (PlayActionCard) เท่านั้นถึงลากได้
        var tm = TurnManager.Instance;
        if (tm == null) return false; // ยังไม่เริ่มเกมก็ไม่ให้ลาก

        if (pm == null) return false;
        if (tm.CurrentPlayerNetId != pm.netId) return false;
        if (tm.Phase != TurnPhase.PlayActionCard) return false;

        //  กันลากการ์ดของคนอื่นแบบชัวร์ (ไม่พึ่ง isOwned)
        if (!IsLocalHandCard && _duck != null)
        {
            // ต้องเป็นการ์ดในมือเท่านั้นถึงลากได้
            if (_duck.zone != ZoneKind.PlayerArea) return false;

            // ต้องเป็นเจ้าของการ์ดใบนี้
            if (pm == null) return false;
            if (_duck.ownerNetId != pm.netId) return false;
        }

        return true;
    }

}

