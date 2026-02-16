using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrentTurnDuckStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image turnDuckBadgeImage;
    [SerializeField] private TMP_Text turnDuckText;
    [SerializeField] private TMP_Text turnRemainingText;
    [SerializeField] private string turnTextPrefix = "Turn: ";
    [SerializeField] private string yourTurnText = "Your Turn";
    [SerializeField] private string noTurnText = "-";
    [SerializeField] private string remainingPrefix = "Time: ";
    [SerializeField] private string remainingSuffix = "s";

    [Header("Color Sprites (index 0..5)")]
    [SerializeField] private Sprite[] duckColorSprites = new Sprite[6];

    [Header("Color Materials Fallback (index 0..5)")]
    [SerializeField] private Material[] duckColorMaterials = new Material[6];

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    private float _nextRefreshAt;
    private uint _lastTurnNetId = uint.MaxValue;
    private int _lastDuckColorIndex = int.MinValue;
    private string _lastTurnDuckLabel;
    private int _lastRemainingSeconds = int.MinValue;
    private Sprite _defaultBadgeSprite;

    private void Awake()
    {
        if (turnDuckBadgeImage != null)
            _defaultBadgeSprite = turnDuckBadgeImage.sprite;
    }

    private void OnEnable()
    {
        _nextRefreshAt = 0f;
        ForceRefresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshAt)
            return;

        _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshIntervalSeconds);
        ForceRefresh();
    }

    public void ForceRefresh()
    {
        if (!NetworkClient.active)
            return;

        TurnManager tm = TurnManager.Instance;
        uint turnNetId = tm != null ? tm.currentTurnNetId : 0u;
        int remainingSeconds = tm != null ? Mathf.Max(0, tm.currentTurnRemainingSeconds) : 0;
        uint localNetId = PlayerManager.LocalPlayerNetId;
        bool isYourTurn = turnNetId != 0 && localNetId != 0 && turnNetId == localNetId;

        int duckColorIndex = -1;
        string turnDuckLabel = noTurnText;

        if (turnNetId != 0 &&
            NetworkClient.spawned.TryGetValue(turnNetId, out NetworkIdentity ni) &&
            ni != null &&
            ni.TryGetComponent(out PlayerManager turnPlayer))
        {
            duckColorIndex = turnPlayer.duckColorIndex;
            turnDuckLabel = HumanizeDuckKey(TurnManager.DuckKeyFromIndex(duckColorIndex));
        }

        bool turnOrColorChanged = turnNetId != _lastTurnNetId || duckColorIndex != _lastDuckColorIndex;
        if (turnOrColorChanged)
        {
            _lastTurnNetId = turnNetId;
            _lastDuckColorIndex = duckColorIndex;
            ApplyDuckColorVisual(duckColorIndex);
        }

        string displayTurnText = isYourTurn ? yourTurnText : $"{turnTextPrefix}{turnDuckLabel}";
        if (displayTurnText != _lastTurnDuckLabel)
        {
            _lastTurnDuckLabel = displayTurnText;
            ApplyTurnText(displayTurnText);
        }

        if (remainingSeconds != _lastRemainingSeconds || turnOrColorChanged)
        {
            _lastRemainingSeconds = remainingSeconds;
            ApplyRemainingText(remainingSeconds, turnNetId != 0);
        }
    }

    private void ApplyDuckColorVisual(int duckColorIndex)
    {
        if (turnDuckBadgeImage == null)
            return;

        Sprite sprite = null;
        if (duckColorIndex >= 0 && duckColorIndex < duckColorSprites.Length)
            sprite = duckColorSprites[duckColorIndex];

        if (sprite != null)
        {
            turnDuckBadgeImage.sprite = sprite;
            turnDuckBadgeImage.material = null;
            return;
        }

        Material mat = null;
        if (duckColorIndex >= 0 && duckColorIndex < duckColorMaterials.Length)
            mat = duckColorMaterials[duckColorIndex];

        if (_defaultBadgeSprite != null)
            turnDuckBadgeImage.sprite = _defaultBadgeSprite;

        turnDuckBadgeImage.material = mat;
    }

    private void ApplyTurnText(string text)
    {
        if (turnDuckText == null)
            return;

        turnDuckText.text = string.IsNullOrWhiteSpace(text) ? noTurnText : text;
    }

    private void ApplyRemainingText(int remainingSeconds, bool hasTurn)
    {
        if (turnRemainingText == null)
            return;

        turnRemainingText.text = hasTurn
            ? $"{remainingPrefix}{Mathf.Max(0, remainingSeconds)}{remainingSuffix}"
            : $"{remainingPrefix}{noTurnText}";
    }

    private static string HumanizeDuckKey(string duckKey)
    {
        if (string.IsNullOrWhiteSpace(duckKey) || duckKey == "-")
            return "-";

        string key = duckKey.Trim();
        if (key.StartsWith("Duck", System.StringComparison.OrdinalIgnoreCase))
            key = key.Substring(4);

        return key;
    }
}
