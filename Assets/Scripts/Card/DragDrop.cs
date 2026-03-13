using System;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using UnityEngine.EventSystems;

public class DragDrop : NetworkBehaviour
{
    public static event Action<bool> LocalDragStateChanged;

    private LocalHandCard _localHandCard;
    public bool IsLocalHandCard => _localHandCard != null;
    public bool IsDragging => isDragging;

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

        if (!GetComponent<NetworkIdentity>().isOwned)
            isDraggable = false;
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
        if (!CanDragNow())
            return;

        isDragging = true;
        startParent = transform.parent;
        CardZoneMoveSfx.NotifyPlayerAreaMove();
        NotifyLocalDragState(true);
    }

    // EventTrigger BeginDrag(BaseEventData) support
    public void StartDrag(BaseEventData _)
    {
        StartDrag();
    }

    public void EndDrag()
    {
        if (!isDraggable)
            return;

        if (!isDragging)
            return;

        if (!CanDragNow())
        {
            CancelDragAndRestore();
            return;
        }

        isDragging = false;
        NotifyLocalDragState(false);
        var rt = transform as RectTransform;

        if (isOverDropZone && dropZone != null)
        {
            if (IsLocalHandCard)
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
                CardZoneMoveSfx.NotifyDropZonePlaced();
                _localHandCard.OnPlayedFromHand();
                return;
            }

            // Keep network card in hand locally until server accepts CmdPlayCard.
            CancelDragAndRestore();

            NetworkIdentity networkIdentity = NetworkClient.connection?.identity;
            PlayerManager = networkIdentity != null ? networkIdentity.GetComponent<PlayerManager>() : null;

            if (PlayerManager != null)
                PlayerManager.PlayCard(gameObject);

            return;
        }

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

    // EventTrigger EndDrag(BaseEventData) support
    public void EndDrag(BaseEventData _)
    {
        EndDrag();
    }

    void Update()
    {
        if (!isDragging)
            return;

        if (!CanDragNow())
        {
            CancelDragAndRestore();
            return;
        }

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        transform.position = mousePos;
        transform.SetParent(canvasObject.transform, true);
    }

    private bool CanDragNow()
    {
        if (!isDraggable)
            return false;

        var pm = PlayerManager.localInstance;
        if (pm != null && pm.activeSkillMode != SkillMode.None)
            return false;

        var tm = TurnManager.Instance;
        if (tm == null)
            return false;

        if (tm.isMatchEnded)
            return false;

        uint localNetId = PlayerManager.LocalPlayerNetId;
        if (localNetId == 0)
            return false;

        uint turnNetId = tm.currentTurnNetId;
        if (turnNetId == 0)
            return false;

        return turnNetId == localNetId;
    }

    public bool CanPlayNow()
    {
        return CanDragNow();
    }

    private void CancelDragAndRestore()
    {
        isDragging = false;
        NotifyLocalDragState(false);

        var parent = startParent != null ? startParent : transform.parent;
        if (parent == null)
            return;

        transform.SetParent(parent, false);

        var rt = transform as RectTransform;
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

    private void OnDisable()
    {
        if (isDragging)
        {
            isDragging = false;
            NotifyLocalDragState(false);
        }
    }

    private void OnDestroy()
    {
        if (isDragging)
        {
            isDragging = false;
            NotifyLocalDragState(false);
        }
    }

    private void NotifyLocalDragState(bool dragging)
    {
        var ni = GetComponent<NetworkIdentity>();
        if (ni == null || !ni.isOwned)
            return;

        LocalDragStateChanged?.Invoke(dragging);
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
