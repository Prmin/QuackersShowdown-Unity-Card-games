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

    [Header("State Images")]
    [SerializeField] private Image[] normalStateImages = new Image[2];
    [SerializeField] private Image[] drawStateImages = new Image[2];
    [SerializeField] private Image[] problemStateImages = new Image[2];

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
    [SerializeField] private string hostDisconnectedTitle = "Match Ended";
    [SerializeField] private string hostDisconnectedSubtitle = "Host left the match";

    private static readonly string[] DuckKeysByIndex =
    {
        "DuckBlue", "DuckOrange", "DuckPink", "DuckGreen", "DuckYellow", "DuckPurple"
    };

    private enum MatchEndVisualState
    {
        Normal,
        Draw,
        Problem
    }

    private bool _isReturning;
    private bool _statsRecorded;
    private bool _sceneLoadHooked;
    private bool _endSfxPlayed;

    private void Awake()
    {
        winnerNamePrefix = "Winner: ";
        countPrefix = "Remaining: ";
        clickHint = "Click anywhere to return to lobby";
        drawTitle = "Draw";
        drawSubtitle = "All action cards are exhausted";
        hostDisconnectedTitle = "Match Ended";
        hostDisconnectedSubtitle = "Host left the match";
    }

    public void Initialize(string winnerDuckKey, int remainingCount, string reason)
    {
        bool matchProblem = IsProblemReason(reason);
        bool isDraw = string.Equals(winnerDuckKey, "Draw", System.StringComparison.OrdinalIgnoreCase);
        ApplyStateImages(matchProblem ? MatchEndVisualState.Problem : (isDraw ? MatchEndVisualState.Draw : MatchEndVisualState.Normal));

        if (!matchProblem)
            TryRecordLocalMatchStats(winnerDuckKey);

        int colorIndex = DuckKeyToColorIndex(winnerDuckKey);

        if (matchProblem)
        {
            if (winnerDuckImage != null)
                winnerDuckImage.enabled = false;

            if (winnerNameText != null)
                winnerNameText.text = hostDisconnectedTitle;

            if (winnerCountText != null)
                winnerCountText.text = hostDisconnectedSubtitle;

            if (clickHintText != null)
                clickHintText.text = clickHint;

            TryPlayEndSfx(MatchEndOverlaySfx.Outcome.Problem);
            Debug.Log($"[MatchEndOverlayUI] Show host disconnect reason={reason}");
            return;
        }

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

        TryPlayEndSfx(ResolveOutcomeSfx(winnerDuckKey));
        Debug.Log($"[MatchEndOverlayUI] Show winner={winnerDuckKey} remaining={remainingCount} reason={reason}");
    }

    private void ApplyStateImages(MatchEndVisualState state)
    {
        SetImageGroupVisible(normalStateImages, state == MatchEndVisualState.Normal);
        SetImageGroupVisible(drawStateImages, state == MatchEndVisualState.Draw);
        SetImageGroupVisible(problemStateImages, state == MatchEndVisualState.Problem);
    }

    private static void SetImageGroupVisible(Image[] images, bool isVisible)
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            image.gameObject.SetActive(isVisible);
        }
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

    private void TryPlayEndSfx(MatchEndOverlaySfx.Outcome outcome)
    {
        if (_endSfxPlayed)
            return;

        _endSfxPlayed = true;
        MatchEndOverlaySfx.Notify(outcome);
    }

    private static MatchEndOverlaySfx.Outcome ResolveOutcomeSfx(string winnerDuckKey)
    {
        if (string.Equals(winnerDuckKey, "Draw", StringComparison.OrdinalIgnoreCase))
            return MatchEndOverlaySfx.Outcome.Draw;

        if (TryGetLocalDuckKey(out string localDuckKey) &&
            string.Equals(localDuckKey, winnerDuckKey, StringComparison.OrdinalIgnoreCase))
        {
            return MatchEndOverlaySfx.Outcome.Win;
        }

        return MatchEndOverlaySfx.Outcome.Loss;
    }

    private static bool IsProblemReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return false;

        return reason.IndexOf("HostDisconnected", StringComparison.OrdinalIgnoreCase) >= 0 ||
               reason.IndexOf("Disconnected", StringComparison.OrdinalIgnoreCase) >= 0 ||
               reason.IndexOf("Cancelled", StringComparison.OrdinalIgnoreCase) >= 0 ||
               reason.IndexOf("Problem", StringComparison.OrdinalIgnoreCase) >= 0 ||
               reason.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
