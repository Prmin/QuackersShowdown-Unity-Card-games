// SceneLoadingOverlay.cs
using UnityEngine;
using UnityEngine.UI;

public class SceneLoadingOverlay : MonoBehaviour
{
    private static SceneLoadingOverlay _instance;
    private Canvas _canvas;
    private Text _label;
    private Image _bg;

    public static void EnsureShown(string msg = "Loading...")
    {
        if (_instance == null)
        {
            var go = new GameObject("[LoadingOverlay]");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneLoadingOverlay>();
            _instance.Build();
        }
        _instance.SetMessage(msg);
        _instance.SetVisible(true);
    }

    public static void SetProgress(int ready, int total)
    {
        if (_instance == null) return;
        _instance.SetMessage($"Waiting for players ({ready}/{total})...");
    }

    public static void Hide()
    {
        if (_instance == null) return;
        _instance.SetVisible(false);
    }

    private void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767; // ลอยบนสุดเสมอ
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(transform, false);
        _bg = bgGO.AddComponent<Image>();
        _bg.color = new Color(0f, 0f, 0f, 0.75f);
        var bgRt = _bg.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(transform, false);
        _label = textGO.AddComponent<Text>();
        _label.alignment = TextAnchor.MiddleCenter;
        _label.font = GetSafeRuntimeFont();
        _label.fontSize = 28;
        _label.color = Color.white;
        var tRt = _label.rectTransform;
        tRt.anchorMin = new Vector2(0.1f, 0.1f);
        tRt.anchorMax = new Vector2(0.9f, 0.9f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        SetVisible(false);
    }

    private void SetMessage(string msg)
    {
        if (_label != null) _label.text = msg;
    }

    private void SetVisible(bool v)
    {
        if (_canvas != null) _canvas.enabled = v;
        if (_bg != null) _bg.enabled = v;
        if (_label != null) _label.enabled = v;
    }

    // ===== ฟอนต์แบบปลอดภัยสำหรับ Unity รุ่นใหม่ =====
    private static Font GetSafeRuntimeFont()
    {
        // 1) Unity รุ่นใหม่: LegacyRuntime.ttf (แนะนำ)
        try
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) return f;
        }
        catch { /* ignore */ }

        // 2) เผื่อโปรเจกต์ที่ยังมี Arial.ttf เป็น builtin (บางเวอร์ชันยังพอใช้ได้)
        try
        {
            var f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) return f;
        }
        catch { /* ignore */ }

        // 3) ดึงจากฟอนต์ของระบบปฏิบัติการ (เดสก์ท็อป/แอนดรอยด์ส่วนใหญ่มี)
        try
        {
            return Font.CreateDynamicFontFromOSFont("Arial", 28);
        }
        catch { /* ignore */ }

        // 4) ฟอลแบ็กสุดท้าย: พยายาม LegacyRuntime อีกครั้ง (กัน null)
        try
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) return f;
        }
        catch { /* ignore */ }

        // ถ้าจริง ๆ หาไม่ได้เลย ให้สร้าง Text ได้โดยไม่ตั้งฟอนต์ (Unity จะพยายามจัดการเอง)
        return null;
    }
}
