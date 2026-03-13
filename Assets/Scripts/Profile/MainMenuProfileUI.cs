using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuProfileUI : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Button editNameButton;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Button avatarButton;
    [SerializeField] private Sprite[] avatarSprites;

    [Header("Avatar Picker")]
    [SerializeField] private ProfileAvatarPickerUI avatarPickerPrefab;
    [SerializeField] private Transform popupParent;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI playedText;
    [SerializeField] private TextMeshProUGUI winText;
    [SerializeField] private TextMeshProUGUI lossText;
    [SerializeField] private TextMeshProUGUI drawText;
    [SerializeField] private TextMeshProUGUI duckShotText;

    [Header("Name Input")]
    [SerializeField] private string nameInputTitle = "Player Name";
    [SerializeField] private string nameAllowedChars = "";
    [SerializeField] private int nameMaxLength = 20;

    private ProfileAvatarPickerUI _pickerInstance;

    private void Awake()
    {
        if (editNameButton != null)
            editNameButton.onClick.AddListener(OnEditNameClicked);

        if (avatarButton != null)
            avatarButton.onClick.AddListener(OnAvatarClicked);
    }

    private void OnDestroy()
    {
        if (editNameButton != null)
            editNameButton.onClick.RemoveListener(OnEditNameClicked);

        if (avatarButton != null)
            avatarButton.onClick.RemoveListener(OnAvatarClicked);
    }

    private void OnEditNameClicked()
    {
        UIAudioSfx.PlayButtonClick();
        OpenNameInput();
    }

    private void OnAvatarClicked()
    {
        UIAudioSfx.PlayButtonClick();
        OpenAvatarPicker();
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshName();
        RefreshAvatar();
        RefreshStats();
    }

    private void RefreshName()
    {
        if (playerNameText != null)
            playerNameText.text = LocalProfileData.GetPlayerName("Player");
    }

    private void RefreshAvatar()
    {
        if (avatarImage == null)
            return;

        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            avatarImage.sprite = null;
            return;
        }

        int idx = LocalProfileData.GetAvatarIndex(avatarSprites.Length, 0);
        if (idx < 0 || idx >= avatarSprites.Length)
            idx = 0;

        avatarImage.sprite = avatarSprites[idx];
    }

    private void RefreshStats()
    {
        LocalMatchStats.Snapshot snap = LocalMatchStats.Get();
        if (playedText != null) playedText.text = $"P:{Mathf.Max(0, snap.played)}";
        if (winText != null) winText.text = $"W:{Mathf.Max(0, snap.win)}";
        if (lossText != null) lossText.text = $"L:{Mathf.Max(0, snap.loss)}";
        if (drawText != null) drawText.text = $"D:{Mathf.Max(0, snap.draw)}";
        if (duckShotText != null) duckShotText.text = $"S:{Mathf.Max(0, snap.duckShots)}";
    }

    private void OpenNameInput()
    {
        string current = LocalProfileData.GetPlayerName("Player");
        if (FindObjectOfType<UI_InputWindow>(true) == null)
        {
            Debug.LogWarning("[MainMenuProfileUI] UI_InputWindow is missing in this scene.");
            return;
        }

        UI_InputWindow.Show_Static(
            nameInputTitle,
            current,
            string.Empty,
            nameMaxLength,
            () => { },
            newName =>
            {
                LocalProfileData.SetPlayerName(newName);
                RefreshName();

                LobbyRoomPlayer me = LobbyRoomPlayer.Local;
                if (me != null)
                    me.CmdSetName(LocalProfileData.GetPlayerName("Player"));
            });
    }

    private void OpenAvatarPicker()
    {
        if (avatarSprites == null || avatarSprites.Length == 0)
            return;

        EnsurePickerInstance();
        if (_pickerInstance == null)
            return;

        int selected = LocalProfileData.GetAvatarIndex(avatarSprites.Length, 0);
        _pickerInstance.Open(avatarSprites, selected, OnAvatarSelected);
    }

    private void EnsurePickerInstance()
    {
        if (_pickerInstance != null)
            return;

        if (avatarPickerPrefab == null)
        {
            Debug.LogWarning("[MainMenuProfileUI] avatarPickerPrefab is not assigned.");
            return;
        }

        Transform parent = popupParent;
        if (parent == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            parent = canvas != null ? canvas.transform : transform;
        }

        _pickerInstance = Instantiate(avatarPickerPrefab, parent);
        _pickerInstance.gameObject.SetActive(false);
    }

    private void OnAvatarSelected(int index)
    {
        LocalProfileData.SetAvatarIndex(index);
        RefreshAvatar();
    }
}
