using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Setting : MonoBehaviour
{
    private const float MinLinearVolume = 0.0001f;
    private const string BackgroundChoiceKey = "BackgroundChoice";
    private const string BackgroundSpriteNameKey = "BackgroundSpriteName";

    public Slider musicSlider;
    public Slider sfxSlider;
    public TMP_Dropdown backgroundDropdown;
    public Button backButton;

    public AudioMixer audioMixer;
    public AudioSource musicSource;
    public Image backgroundImage;
    public List<Sprite> backgroundSprites;

    [Header("Popup Mode")]
    [SerializeField] private bool usePopupMode = false;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private string backSceneName = "First_Sceme";

    private bool listenersBound;

    private void Start()
    {
        BindEventsOnce();
        DisableLocalPopupMusicSource();
        SetupBackgroundDropdown();
        RefreshControlsFromPrefs();
        UIAudioSfx.RefreshMusicStateFromPrefs();
        ApplyBackground();
    }

    private void OnEnable()
    {
        // Avoid invoking slider callbacks while merely opening the popup.
        DisableLocalPopupMusicSource();
        RefreshControlsFromPrefs();
        SetupBackgroundDropdown();
        ApplyBackground();
    }

    public void SetMusicVolume()
    {
        if (musicSlider == null)
            return;

        float linear = Mathf.Clamp01(musicSlider.value);

        PlayerPrefs.SetFloat("MusicVolume", linear);
        PlayerPrefs.Save();
        UIAudioSfx.RefreshMusicStateFromPrefs();
    }

    public void SetSFXVolume()
    {
        if (sfxSlider == null)
            return;

        float linear = Mathf.Clamp01(sfxSlider.value);
        float safeValue = Mathf.Max(MinLinearVolume, linear);

        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", Mathf.Log10(safeValue) * 20f);

        PlayerPrefs.SetFloat("SFXVolume", linear);
        PlayerPrefs.Save();
    }

    public void SetBackground()
    {
        if (backgroundDropdown == null)
            return;

        int index = Mathf.Max(0, backgroundDropdown.value);
        PlayerPrefs.SetInt(BackgroundChoiceKey, index);
        if (backgroundSprites != null && backgroundSprites.Count > 0)
        {
            int clamped = Mathf.Clamp(index, 0, backgroundSprites.Count - 1);
            Sprite sprite = backgroundSprites[clamped];
            PlayerPrefs.SetString(BackgroundSpriteNameKey, sprite != null ? sprite.name : string.Empty);
        }
        PlayerPrefs.Save();
        if (Settings_Manager.instance != null)
            Settings_Manager.instance.SetBackground(index);
        ApplyBackground();
    }

    private void ApplyBackground()
    {
        TryResolveBackgroundImageIfMissing();

        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Count == 0)
            return;

        int backgroundChoice = ResolveSavedBackgroundIndex(backgroundSprites, 0);
        backgroundChoice = Mathf.Clamp(backgroundChoice, 0, backgroundSprites.Count - 1);
        backgroundImage.sprite = backgroundSprites[backgroundChoice];

        if (backgroundDropdown != null && backgroundDropdown.options.Count > 0)
            backgroundDropdown.SetValueWithoutNotify(backgroundChoice);
    }

    private void GoBack()
    {
        UIAudioSfx.PlayButtonClick();

        if (usePopupMode)
        {
            GameObject root = popupRoot != null ? popupRoot : gameObject;
            root.SetActive(false);
            return;
        }

        SceneManager.LoadScene(backSceneName);
    }

    private void BindEventsOnce()
    {
        if (listenersBound)
            return;

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(delegate { SetMusicVolume(); });

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(delegate { SetSFXVolume(); });

        if (backgroundDropdown != null)
            backgroundDropdown.onValueChanged.AddListener(delegate { SetBackground(); });

        if (backButton != null)
            backButton.onClick.AddListener(GoBack);

        listenersBound = true;
    }

    private void SetupBackgroundDropdown()
    {
        if (backgroundDropdown == null)
            return;

        backgroundDropdown.ClearOptions();
        var options = new List<string> { "Background 1", "Background 2", "Background 3" };
        backgroundDropdown.AddOptions(options);

        int savedBg = Mathf.Clamp(ResolveSavedBackgroundIndex(backgroundSprites, 0), 0, Mathf.Max(0, options.Count - 1));
        backgroundDropdown.SetValueWithoutNotify(savedBg);
    }

    private void RefreshControlsFromPrefs()
    {
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", 1f)));

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", 1f)));
    }

    private void TryResolveBackgroundImageIfMissing()
    {
        if (backgroundImage != null)
            return;

        GameObject byName = GameObject.Find("Background");
        if (byName != null)
            backgroundImage = byName.GetComponent<Image>();
    }

    private static int ResolveSavedBackgroundIndex(List<Sprite> sprites, int defaultIndex)
    {
        int indexFromPrefs = Mathf.Max(0, PlayerPrefs.GetInt(BackgroundChoiceKey, defaultIndex));
        string spriteName = PlayerPrefs.GetString(BackgroundSpriteNameKey, string.Empty);

        if (sprites != null && sprites.Count > 0 && !string.IsNullOrEmpty(spriteName))
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite s = sprites[i];
                if (s != null && s.name == spriteName)
                    return i;
            }
        }

        return indexFromPrefs;
    }

    private void DisableLocalPopupMusicSource()
    {
        if (musicSource == null)
            return;

        // This popup-local source is not part of the gameplay BGM system.
        if (musicSource.transform.IsChildOf(transform))
        {
            musicSource.playOnAwake = false;
            if (musicSource.isPlaying)
                musicSource.Stop();
            musicSource.enabled = false;
        }
    }
}
