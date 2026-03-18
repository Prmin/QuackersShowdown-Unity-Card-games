using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    [SerializeField] private bool applyLeft = true;
    [SerializeField] private bool applyRight = true;
    [SerializeField] private bool applyTop = true;
    [SerializeField] private bool applyBottom = true;

    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private bool isApplying;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        ApplySafeArea(force: true);
    }

    private void Start()
    {
        ApplySafeArea(force: true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ApplySafeArea(force: false);
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplySafeArea(force: false);
    }

    [ContextMenu("Apply Safe Area Now")]
    public void ApplySafeAreaNow()
    {
        ApplySafeArea(force: true);
    }

    private void ApplySafeArea(bool force)
    {
        if (isApplying)
        {
            return;
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
        {
            return;
        }

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Rect parentBounds = parentRect.rect;
        if (parentBounds.width <= 0f || parentBounds.height <= 0f)
        {
            return;
        }

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= parentBounds.width;
        anchorMin.y /= parentBounds.height;
        anchorMax.x /= parentBounds.width;
        anchorMax.y /= parentBounds.height;

        if (!applyLeft)
        {
            anchorMin.x = 0f;
        }

        if (!applyBottom)
        {
            anchorMin.y = 0f;
        }

        if (!applyRight)
        {
            anchorMax.x = 1f;
        }

        if (!applyTop)
        {
            anchorMax.y = 1f;
        }

        bool anchorsAlreadyApplied =
            Approximately(rectTransform.anchorMin, anchorMin) &&
            Approximately(rectTransform.anchorMax, anchorMax) &&
            Approximately(rectTransform.offsetMin, Vector2.zero) &&
            Approximately(rectTransform.offsetMax, Vector2.zero);

        if (!anchorsAlreadyApplied)
        {
            isApplying = true;
            try
            {
                rectTransform.anchorMin = anchorMin;
                rectTransform.anchorMax = anchorMax;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }
            finally
            {
                isApplying = false;
            }
        }

        lastSafeArea = safeArea;
        lastScreenSize = screenSize;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f &&
               Mathf.Abs(a.y - b.y) < 0.0001f;
    }
}
