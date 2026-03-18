using UnityEngine;

public class LocalDragDropHintUI : MonoBehaviour
{
    [SerializeField] private GameObject hintRoot;
    [SerializeField] private bool hideOnStart = true;
    private bool isSubscribed;

    private void Awake()
    {
        if (hintRoot == null)
            hintRoot = gameObject;

        SubscribeIfNeeded();

        if (hideOnStart && hintRoot != null)
            hintRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        UnsubscribeIfNeeded();
    }

    private void SubscribeIfNeeded()
    {
        if (isSubscribed)
            return;

        DragDrop.LocalDragStateChanged += OnLocalDragStateChanged;
        isSubscribed = true;
    }

    private void UnsubscribeIfNeeded()
    {
        if (!isSubscribed)
            return;

        DragDrop.LocalDragStateChanged -= OnLocalDragStateChanged;
        isSubscribed = false;
    }

    private void OnLocalDragStateChanged(bool isDragging)
    {
        if (hintRoot == null)
            return;

        hintRoot.SetActive(isDragging);
    }
}
