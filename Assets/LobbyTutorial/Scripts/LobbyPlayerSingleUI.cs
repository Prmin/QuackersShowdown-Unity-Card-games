using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerSingleUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image characterImage;
    [SerializeField] private Image profileImage;
    [SerializeField] private Button kickPlayerButton;

    [Header("Ready State Display")]
    [SerializeField] private Image readyStateImage;
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite notReadySprite;
    [SerializeField] private bool tintSprite = true;
    [SerializeField] private Color readyTint = new Color32(46, 204, 113, 255);
    [SerializeField] private Color notReadyTint = new Color32(231, 76, 60, 255);

    [Header("Stats Text (Optional)")]
    [SerializeField] private TextMeshProUGUI playedText;
    [SerializeField] private TextMeshProUGUI winText;
    [SerializeField] private TextMeshProUGUI lossText;
    [SerializeField] private TextMeshProUGUI drawText;
    [SerializeField] private TextMeshProUGUI duckShotText;

    private uint targetNetId;

    private void Awake()
    {
        if (kickPlayerButton != null)
            kickPlayerButton.onClick.AddListener(KickPlayer);
    }

    public void SetKickPlayerButtonVisible(bool visible)
    {
        if (kickPlayerButton != null)
            kickPlayerButton.gameObject.SetActive(visible);
    }

    public void UpdatePlayer(LobbyRoomPlayer lp)
    {
        if (lp == null)
            return;

        targetNetId = lp.netId;

        string name = string.IsNullOrWhiteSpace(lp.displayName) ? "Player" : lp.displayName;
        if (playerNameText != null)
            playerNameText.text = name;

        if (characterImage != null)
        {
            // Keep original behavior: character image follows duck color.
            characterImage.sprite = LobbyAssets.Instance ? LobbyAssets.Instance.GetDuckSpriteByIndex(lp.duckColorIndex) : null;
            characterImage.preserveAspect = true;
        }

        if (profileImage != null)
        {
            // New separate profile avatar image.
            Sprite avatar = LobbyAssets.Instance ? LobbyAssets.Instance.GetProfileAvatarSpriteByIndex(lp.profileAvatarIndex) : null;
            profileImage.sprite = avatar;
            profileImage.enabled = avatar != null;
            if (avatar != null)
                profileImage.preserveAspect = true;
        }

        ApplyReadyVisual(lp.readyToBegin);
        ApplyStatsVisual(lp.statsPlayed, lp.statsWin, lp.statsLoss, lp.statsDraw, lp.statsDuckShots);

        if (readyStateImage != null)
            readyStateImage.gameObject.SetActive(!lp.isHost);
    }

    private void ApplyReadyVisual(bool isReady)
    {
        if (readyStateImage == null)
            return;

        if (readySprite != null && notReadySprite != null)
        {
            readyStateImage.sprite = isReady ? readySprite : notReadySprite;
            readyStateImage.enabled = true;
            readyStateImage.color = tintSprite ? (isReady ? readyTint : notReadyTint) : Color.white;
            return;
        }

        readyStateImage.enabled = true;
        readyStateImage.color = isReady ? readyTint : notReadyTint;
    }

    private void ApplyStatsVisual(int played, int win, int loss, int draw, int duckShots)
    {
        if (playedText != null) playedText.text = $"P:{Mathf.Max(0, played)}";
        if (winText != null) winText.text = $"W:{Mathf.Max(0, win)}";
        if (lossText != null) lossText.text = $"L:{Mathf.Max(0, loss)}";
        if (drawText != null) drawText.text = $"D:{Mathf.Max(0, draw)}";
        if (duckShotText != null) duckShotText.text = $"S:{Mathf.Max(0, duckShots)}";
    }

    private void KickPlayer()
    {
        if (targetNetId == 0)
            return;

        UIAudioSfx.PlayButtonClick();

        LobbyRoomPlayer me = LobbyRoomPlayer.Local;
        if (me != null)
            me.CmdKickPlayer(targetNetId);
    }
}
