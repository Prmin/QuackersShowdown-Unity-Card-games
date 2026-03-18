using UnityEngine;
using UnityEngine.EventSystems;

public class SettingsPopupOutsideClose : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private float ignoreClickAfterOpenSeconds = 0.12f;

    private float openedAtUnscaled;

    private void OnEnable()
    {
        openedAtUnscaled = Time.unscaledTime;
        if (popupRoot == null)
            popupRoot = gameObject;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Time.unscaledTime - openedAtUnscaled < ignoreClickAfterOpenSeconds)
            return;

        if (contentRoot == null)
        {
            ClosePopup();
            return;
        }

        bool clickedInsideContent = RectTransformUtility.RectangleContainsScreenPoint(
            contentRoot,
            eventData.position,
            eventData.pressEventCamera
        );

        if (!clickedInsideContent)
            ClosePopup();
    }

    public void ClosePopup()
    {
        GameObject root = popupRoot != null ? popupRoot : gameObject;
        root.SetActive(false);
    }
}
