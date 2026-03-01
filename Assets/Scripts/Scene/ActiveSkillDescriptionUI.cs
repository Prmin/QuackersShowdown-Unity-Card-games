using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ActiveSkillDescriptionUI : MonoBehaviour
{
    [Serializable]
    private struct SkillDescriptionEntry
    {
        public SkillMode mode;
        [TextArea(2, 5)] public string description;
    }

    public static ActiveSkillDescriptionUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Timing")]
    [SerializeField] private float refreshIntervalSeconds = 0.1f;
    [SerializeField] private float hideDelayAfterResolvedSeconds = 2f;

    [Header("Text")]
    [SerializeField] private string defaultDescription = "ต้องใช้ความสามารถของการ์ดให้จบก่อน เทิร์นถึงจะจบได้";

    [Header("Optional Overrides")]
    [SerializeField] private SkillDescriptionEntry[] descriptionOverrides;

    private readonly Dictionary<SkillMode, string> _overrideByMode = new Dictionary<SkillMode, string>();
    private Coroutine _hideRoutine;
    private float _nextRefreshAt;
    private SkillMode _lastObservedMode = SkillMode.None;
    private bool _hasShownAtLeastOnce;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (root == null)
            root = gameObject;

        RebuildOverrideMap();
        SetVisible(false);
    }

    private void OnEnable()
    {
        _nextRefreshAt = 0f;
        ForceRefresh();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshAt)
            return;

        _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshIntervalSeconds);
        ForceRefresh();
    }

    public static void NotifySkillTriggered(SkillMode mode)
    {
        if (mode == SkillMode.None)
            return;

        Instance?.HandleSkillTriggered(mode);
    }

    public static void NotifySkillModeChanged(SkillMode mode)
    {
        Instance?.HandleSkillModeChanged(mode);
    }

    public void ForceRefresh()
    {
        PlayerManager pm = PlayerManager.localInstance;
        if (pm == null || !pm.isLocalPlayer)
            return;

        HandleSkillModeChanged(pm.activeSkillMode);
    }

    private void HandleSkillTriggered(SkillMode mode)
    {
        ShowSkill(mode);

        PlayerManager pm = PlayerManager.localInstance;
        if (pm == null || pm.activeSkillMode == SkillMode.None)
            StartHideDelay();
    }

    private void HandleSkillModeChanged(SkillMode mode)
    {
        if (mode != SkillMode.None)
        {
            ShowSkill(mode);
            _lastObservedMode = mode;
            return;
        }

        if (_lastObservedMode != SkillMode.None)
        {
            _lastObservedMode = SkillMode.None;
            StartHideDelay();
        }
    }

    private void ShowSkill(SkillMode mode)
    {
        if (mode == SkillMode.None)
            return;

        CancelHideDelay();
        _hasShownAtLeastOnce = true;

        string resolvedTitle = HumanizeSkillName(mode);
        string resolvedDescription = ResolveDescription(mode);

        if (titleText != null)
            titleText.text = resolvedTitle;

        if (descriptionText != null)
            descriptionText.text = string.IsNullOrWhiteSpace(resolvedDescription) ? defaultDescription : resolvedDescription;

        SetVisible(true);
    }

    private void StartHideDelay()
    {
        if (!_hasShownAtLeastOnce)
            return;

        CancelHideDelay();
        _hideRoutine = StartCoroutine(CoHideAfterDelay());
    }

    private void CancelHideDelay()
    {
        if (_hideRoutine == null)
            return;

        StopCoroutine(_hideRoutine);
        _hideRoutine = null;
    }

    private IEnumerator CoHideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, hideDelayAfterResolvedSeconds));
        _hideRoutine = null;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    private void RebuildOverrideMap()
    {
        _overrideByMode.Clear();
        if (descriptionOverrides == null)
            return;

        foreach (SkillDescriptionEntry entry in descriptionOverrides)
        {
            if (entry.mode == SkillMode.None || string.IsNullOrWhiteSpace(entry.description))
                continue;

            _overrideByMode[entry.mode] = entry.description.Trim();
        }
    }

    private string ResolveDescription(SkillMode mode)
    {
        if (_overrideByMode.TryGetValue(mode, out string overrideText) && !string.IsNullOrWhiteSpace(overrideText))
            return overrideText;

        switch (mode)
        {
            case SkillMode.Shoot: return "เลือกเป็ดเป้าหมาย 1 ใบ แล้วใช้ Shoot";
            case SkillMode.TakeAim: return "เลือกเป็ดเป้าหมาย 1 ใบ เพื่อติดเป้า Take Aim";
            case SkillMode.DoubleBarrel: return "เลือกเป็ดเป้าหมาย 2 ใบที่ติดกันอยู่ เพื่อใช้ Double Barrel";
            case SkillMode.QuickShot: return "เลือกเป็ดเป้าหมาย 1 ใบ เพื่อใช้ Quick Shot";
            case SkillMode.Misfire: return "เลือกเป็ดเป้าหมาย 1 ใบ เพื่อติดเป้า Misfire";
            case SkillMode.TwoBirds: return "เลือกเป็ดเป้าหมาย 2 ใบ ที่มีเป้าอยู่เพื่อใช้ Two Birds หรือเลือกเป็ดเป้าหมาย 1 ใบ ที่มีเป้าอยู่เพื่อใช้ยิงซ้ำด้วยการคลิก 2 ครั้ง";
            case SkillMode.BumpLeft: return "เลือกเป็ดเพื่อย้ายเป้าไปทางซ้าย";
            case SkillMode.BumpRight: return "เลือกเป็ดเพื่อย้ายเป้าไปทางขวา";
            case SkillMode.LineForward: return "เมื่อใช้ Line Forward เป็ดทุกตัวจะเลื่อนไปข้างหน้า 1 ช่อง และเป็ดที่อยู่ในช่องหน้าสุดจะกลับเข้ากองการ์ด";
            case SkillMode.MoveAhead: return "เลือกเป็ด 1 ใบ ให้สลัลกับเป็ดที่อยู่ข้างหน้า";
            case SkillMode.HangBack: return "เลือกเป็ด 1 ใบ ให้สลัลกับเป็ดที่อยู่ข้างหลัง";
            case SkillMode.FastForward: return "เลือกเป็ด 1 ใบ ให้เลื่อนไปอยู่หน้าสุดของแถว";
            case SkillMode.DisorderlyConduckt: return "เลือกเป็ด 2 ใบที่อยู่ติดกัน เพื่อสลับตำแหน่งกัน";
            case SkillMode.DuckShuffle: return "นำเป็ดทั้งหมดในแถวกลับเข้ากองการ์ด แล้วจั่วเป็ดใหม่ขึ้นมาเท่าจำนวนที่กลับเข้ากองการ์ด";
            case SkillMode.GivePeaceAChance: return "ทำลายเป้าทั้งหมดบนสนาม";
            case SkillMode.Resurrection: return "คืนชีพเป็ดของเรา 1 ใบ (ไม่เกินจำนวนสูงสุดในพูล)";
            default: return defaultDescription;
        }
    }

    private static string HumanizeSkillName(SkillMode mode)
    {
        string text = mode.ToString();
        if (string.IsNullOrEmpty(text))
            return "-";

        List<char> chars = new List<char>(text.Length + 4);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(text[i - 1]))
                chars.Add(' ');

            chars.Add(c);
        }

        return new string(chars.ToArray());
    }

}
