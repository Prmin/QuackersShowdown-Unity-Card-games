using System;
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
    [SerializeField] private string drawTitle = "Draw";
    [SerializeField] private string drawSubtitle = "All action cards are exhausted";

    private static readonly string[] DuckKeysByIndex =
    {
        "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    };

    private bool _isReturning;
    private bool _statsRecorded;
    private bool _sceneLoadHooked;

    private void Awake()
    {
        winnerNamePrefix = "Winner: ";
        countPrefix = "Remaining: ";
        clickHint = "Click anywhere to return to lobby";
        drawTitle = "Draw";
        drawSubtitle = "All action cards are exhausted";
    }

    public void Initialize(string winnerDuckKey, int remainingCount, string reason)
    {
        TryRecordLocalMatchStats(winnerDuckKey);

        bool isDraw = string.Equals(winnerDuckKey, "Draw", System.StringComparison.OrdinalIgnoreCase);
        int colorIndex = DuckKeyToColorIndex(winnerDuckKey);

        if (winnerDuckImage != null)
        {
            if (isDraw)
            {
                winnerDuckImage.enabled = false;
            }
            else
            {
                winnerDuckImage.enabled = true;
                Sprite sprite = (colorIndex >= 0 && colorIndex < duckColorSprites.Length) ? duckColorSprites[colorIndex] : null;
                if (sprite != null)
                    winnerDuckImage.sprite = sprite;
            }
        }

        if (winnerNameText != null)
            winnerNameText.text = isDraw ? drawTitle : winnerNamePrefix + HumanizeDuckKey(winnerDuckKey);

        if (winnerCountText != null)
            winnerCountText.text = isDraw ? drawSubtitle : countPrefix + Mathf.Max(0, remainingCount).ToString();

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
        UIFlow.I?.HideAllForGameplay();

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

        if (!_sceneLoadHooked)
        {
            SceneManager.sceneLoaded += OnSceneLoadedShowLobbyList;
            _sceneLoadHooked = true;
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    private void OnSceneLoadedShowLobbyList(Scene scene, LoadSceneMode mode)
    {
        string expectedName = lobbySceneName;
        int slash = expectedName.LastIndexOf('/');
        if (slash >= 0 && slash < expectedName.Length - 1)
            expectedName = expectedName.Substring(slash + 1);

        int dot = expectedName.LastIndexOf('.');
        if (dot > 0)
            expectedName = expectedName.Substring(0, dot);

        if (!string.Equals(scene.name, expectedName, StringComparison.OrdinalIgnoreCase))
            return;

        SceneManager.sceneLoaded -= OnSceneLoadedShowLobbyList;
        _sceneLoadHooked = false;
        UIFlow.I?.ShowLobbyList();
    }

    private void OnDestroy()
    {
        if (_sceneLoadHooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedShowLobbyList;
            _sceneLoadHooked = false;
        }
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

    private void TryRecordLocalMatchStats(string winnerDuckKey)
    {
        if (_statsRecorded)
            return;

        _statsRecorded = true;

        if (string.Equals(winnerDuckKey, "Draw", System.StringComparison.OrdinalIgnoreCase))
        {
            LocalMatchStats.Record(MatchResult.Draw);
            return;
        }

        if (!TryGetLocalDuckKey(out string localDuckKey))
            return;

        bool isWin = string.Equals(localDuckKey, winnerDuckKey, System.StringComparison.OrdinalIgnoreCase);
        LocalMatchStats.Record(isWin ? MatchResult.Win : MatchResult.Loss);
    }

    private static bool TryGetLocalDuckKey(out string duckKey)
    {
        duckKey = null;

        PlayerManager local = PlayerManager.localInstance;
        if (local == null || !local.isLocalPlayer)
            return false;

        int colorIndex = local.duckColorIndex;
        if (colorIndex < 0 || colorIndex >= DuckKeysByIndex.Length)
            return false;

        duckKey = DuckKeysByIndex[colorIndex];
        return !string.IsNullOrWhiteSpace(duckKey);
    }
}
