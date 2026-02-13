using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDuckStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image duckColorBadgeImage;
    [SerializeField] private TMP_Text ownedDuckCountText;
    [SerializeField] private string countPrefix = "x";

    [Header("Color Sprites (index 0..5)")]
    [SerializeField] private Sprite[] duckColorSprites = new Sprite[6];

    [Header("Color Materials Fallback (index 0..5)")]
    [SerializeField] private Material[] duckColorMaterials = new Material[6];

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    private PlayerManager _localPlayer;
    private float _nextRefreshAt;
    private int _lastDuckColorIndex = int.MinValue;
    private int _lastOwnedCount = int.MinValue;
    private Sprite _defaultBadgeSprite;

    private void Awake()
    {
        if (duckColorBadgeImage != null)
            _defaultBadgeSprite = duckColorBadgeImage.sprite;
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
        if (_localPlayer == null || !_localPlayer.isLocalPlayer)
            _localPlayer = PlayerManager.localInstance;

        if (_localPlayer == null)
            return;

        int duckColorIndex = _localPlayer.duckColorIndex;
        int ownedCount = Mathf.Max(0, _localPlayer.OwnedDuckCount);

        if (duckColorIndex != _lastDuckColorIndex)
        {
            _lastDuckColorIndex = duckColorIndex;
            ApplyDuckColorVisual(duckColorIndex);
        }

        if (ownedCount != _lastOwnedCount)
        {
            _lastOwnedCount = ownedCount;
            ApplyOwnedCountText(ownedCount);
        }
    }

    private void ApplyDuckColorVisual(int duckColorIndex)
    {
        if (duckColorBadgeImage == null)
            return;

        Sprite sprite = null;
        if (duckColorIndex >= 0 && duckColorIndex < duckColorSprites.Length)
            sprite = duckColorSprites[duckColorIndex];

        if (sprite != null)
        {
            duckColorBadgeImage.sprite = sprite;
            duckColorBadgeImage.material = null;
            return;
        }

        Material mat = null;
        if (duckColorIndex >= 0 && duckColorIndex < duckColorMaterials.Length)
            mat = duckColorMaterials[duckColorIndex];

        if (_defaultBadgeSprite != null)
            duckColorBadgeImage.sprite = _defaultBadgeSprite;

        duckColorBadgeImage.material = mat;
    }

    private void ApplyOwnedCountText(int ownedCount)
    {
        if (ownedDuckCountText == null)
            return;

        ownedDuckCountText.text = $"{countPrefix}{ownedCount}";
    }
}
