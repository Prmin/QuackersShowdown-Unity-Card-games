using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class DragDrop : NetworkBehaviour
{

    private DuckCard _duck;

    private LocalHandCard _localHandCard;  // รันบน hand ท้องถิ่นหรือไม่
    public bool IsLocalHandCard => _localHandCard != null;

    public GameObject canvasObject;
    public PlayerManager PlayerManager;

    private bool isDragging = false;
    private bool isDraggable = true;
    private Transform startParent;
    private GameObject dropZone;
    private bool isOverDropZone;


    void Awake()
    {
        _duck = GetComponent<DuckCard>();
    }

    void Start()
    {
        canvasObject = GameObject.Find("Main Canvas");

        // ไม่ให้ลากถ้าไม่มี authority
        if (!GetComponent<NetworkIdentity>().isOwned)
        {
            isDraggable = false;
        }
    }

    private bool CanDragNow()
    {
        if (!isDraggable) return false;

        var pm = PlayerManager.localInstance;
        var tm = TurnManager.Instance;
        if (pm == null || tm == null) return false;

        // ต้องเป็นเทิร์นของเรา + Phase A เท่านั้น
        if (tm.CurrentPlayerNetId != pm.netId) return false;
        if (tm.Phase != TurnPhase.PlayActionCard) return false;

        // ถ้าสกิลค้างอยู่ ไม่ให้ลากใบใหม่
        if (pm.activeSkillMode != SkillMode.None) return false;

        // local hand card = ของเราชัวร์ แต่ก็ต้องผ่านกติกาเทิร์นข้างบน
        if (IsLocalHandCard) return true;

        // network card: ต้องเป็นการ์ดในมือของเราเท่านั้น
        if (_duck == null) return false;
        if (_duck.zone != ZoneKind.PlayerArea) return false;
        if (_duck.ownerNetId != pm.netId) return false;

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
        if (!isDraggable) return;
        if (!CanDragNow()) return;

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
        if (!CanDragNow())
        {
            isDragging = false;

            var rt0 = transform as RectTransform;
            var parent = startParent != null ? startParent : transform.parent;

            transform.SetParent(parent, false);
            if (rt0 != null) rt0.anchoredPosition = Vector2.zero;

            ForceParentLayout(parent);
            return;
        }
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
        if (isDragging) return;

        if (!CanDragNow())
        {
            isDragging = false;

            var rt0 = transform as RectTransform;
            var parent = startParent != null ? startParent : transform.parent;

            transform.SetParent(parent, false);
            if (rt0 != null) rt0.anchoredPosition = Vector2.zero;

            ForceParentLayout(parent);
            return;
        }

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f; // keep UI on-plane
        transform.position = mousePos;
        transform.SetParent(canvasObject.transform, true);

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

