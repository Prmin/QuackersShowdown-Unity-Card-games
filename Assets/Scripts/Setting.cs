using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Setting : MonoBehaviour
{
    private const float MinLinearVolume = 0.0001f;

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
        SetupBackgroundDropdown();
        RefreshControlsFromPrefs();
        ApplyBackground();
    }

    private void OnEnable()
    {
        // Avoid invoking slider callbacks while merely opening the popup.
        RefreshControlsFromPrefs();
        ApplyBackground();
    }

    public void SetMusicVolume()
    {
        if (musicSlider == null)
            return;

        float linear = Mathf.Clamp01(musicSlider.value);
        float safeValue = Mathf.Max(MinLinearVolume, linear);

        if (audioMixer != null)
            audioMixer.SetFloat("MusicVolume", Mathf.Log10(safeValue) * 20f);

        if (musicSource != null)
            musicSource.volume = linear;

        PlayerPrefs.SetFloat("MusicVolume", linear);
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
    }

    public void SetBackground()
    {
        if (backgroundDropdown == null)
            return;

        PlayerPrefs.SetInt("BackgroundChoice", backgroundDropdown.value);
        ApplyBackground();
    }

    private void ApplyBackground()
    {
        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Count == 0)
            return;

        int backgroundChoice = PlayerPrefs.GetInt("BackgroundChoice", 0);
        backgroundChoice = Mathf.Clamp(backgroundChoice, 0, backgroundSprites.Count - 1);
        backgroundImage.sprite = backgroundSprites[backgroundChoice];
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

        int savedBg = Mathf.Clamp(PlayerPrefs.GetInt("BackgroundChoice", 0), 0, Mathf.Max(0, options.Count - 1));
        backgroundDropdown.SetValueWithoutNotify(savedBg);
    }

    private void RefreshControlsFromPrefs()
    {
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", 1f)));

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", 1f)));
    }
}
