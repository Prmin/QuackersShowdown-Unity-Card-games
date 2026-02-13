using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MatchEndOverlayUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image winnerDuckImage;
    [SerializeField] private TMP_Text winnerNameText;
    [SerializeField] private TMP_Text winnerCountText;
    [SerializeField] private TMP_Text clickHintText;

    [Header("Duck Sprites (index 0..5)")]
    [SerializeField] private Sprite[] duckColorSprites = new Sprite[6];

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "LobbyTutorial_Done";

    [Header("Text")]
    [SerializeField] private string winnerNamePrefix = "Winner: ";
    [SerializeField] private string countPrefix = "Remaining: ";
    [SerializeField] private string clickHint = "Click anywhere to return to lobby";

    private static readonly string[] DuckKeysByIndex =
    {
        "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    };

    private bool _isReturning;

    public void Initialize(string winnerDuckKey, int remainingCount, string reason)
    {
        int colorIndex = DuckKeyToColorIndex(winnerDuckKey);

        if (winnerDuckImage != null)
        {
            Sprite sprite = (colorIndex >= 0 && colorIndex < duckColorSprites.Length) ? duckColorSprites[colorIndex] : null;
            if (sprite != null)
                winnerDuckImage.sprite = sprite;
        }

        if (winnerNameText != null)
            winnerNameText.text = winnerNamePrefix + HumanizeDuckKey(winnerDuckKey);

        if (winnerCountText != null)
            winnerCountText.text = countPrefix + Mathf.Max(0, remainingCount).ToString();

        if (clickHintText != null)
            clickHintText.text = clickHint;

        Debug.Log($"[MatchEndOverlayUI] Show winner={winnerDuckKey} remaining={remainingCount} reason={reason}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ReturnToLobby();
    }

    public void OnTapAnywhere()
    {
        ReturnToLobby();
    }

    private void ReturnToLobby()
    {
        if (_isReturning)
            return;

        _isReturning = true;

        NetworkManager manager = NetworkManager.singleton;
        if (manager != null)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
                manager.StopHost();
            else if (NetworkClient.isConnected)
                manager.StopClient();
            else if (NetworkServer.active)
                manager.StopServer();
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    private static int DuckKeyToColorIndex(string duckKey)
    {
        if (string.IsNullOrWhiteSpace(duckKey))
            return -1;

        for (int i = 0; i < DuckKeysByIndex.Length; i++)
        {
            if (string.Equals(DuckKeysByIndex[i], duckKey, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string HumanizeDuckKey(string duckKey)
    {
        if (string.IsNullOrWhiteSpace(duckKey))
            return "-";

        string key = duckKey.Trim();
        if (key.StartsWith("Duck", System.StringComparison.OrdinalIgnoreCase))
            key = key.Substring(4);

        return key;
    }
}
