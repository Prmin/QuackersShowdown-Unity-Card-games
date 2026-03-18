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

    [Header("Background Setting")]
    [SerializeField] private bool enableBackgroundSetting = true;
    [SerializeField] private string disableBackgroundInScene = "LobbyTutorial_Done";

    private bool listenersBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedBackgroundAfterSceneLoad()
    {
        ApplySavedBackgroundToActiveScene();
    }

    private void Start()
    {
        BindEventsOnce();
        DisableLocalPopupMusicSource();
        RefreshBackgroundSettingState();
        SetupBackgroundDropdown();
        RefreshControlsFromPrefs();
        ApplySavedAudioLocally();
        UIAudioSfx.RefreshMusicStateFromPrefs();
        ApplyBackground();
    }

    public static void ApplySavedBackgroundToActiveScene()
    {
        Image sceneBackground = ResolveConfiguredBackgroundImage();
        if (sceneBackground == null)
            sceneBackground = FindBestSceneBackgroundImage();
        if (sceneBackground == null)
            return;

        List<Sprite> sprites = ResolveBackgroundSpriteList();
        if (sprites == null || sprites.Count == 0)
            return;

        int index = ResolveSavedBackgroundIndex(sprites, 0);
        index = Mathf.Clamp(index, 0, sprites.Count - 1);
        sceneBackground.sprite = sprites[index];
    }

    private void OnEnable()
    {
        // Avoid invoking slider callbacks while merely opening the popup.
        DisableLocalPopupMusicSource();
        RefreshBackgroundSettingState();
        RefreshControlsFromPrefs();
        ApplySavedAudioLocally();
        SetupBackgroundDropdown();
        ApplyBackground();
    }

    public void SetMusicVolume()
    {
        if (musicSlider == null)
            return;

        float linear = Mathf.Clamp01(musicSlider.value);

        if (musicSource != null && musicSource.enabled)
            musicSource.volume = linear;

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
        if (!IsBackgroundSettingEnabled())
            return;

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
        if (!IsBackgroundSettingEnabled())
            return;

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
        if (!IsBackgroundSettingEnabled())
            return;

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
        Scene activeScene = SceneManager.GetActiveScene();
        bool hasValidAssignedImage =
            backgroundImage != null &&
            backgroundImage.gameObject.scene.IsValid() &&
            backgroundImage.gameObject.scene == activeScene &&
            backgroundImage.GetComponentInParent<Setting>(true) == null &&
            backgroundImage.GetComponentInParent<TMP_Dropdown>(true) == null &&
            backgroundImage.GetComponentInParent<Dropdown>(true) == null;

        if (hasValidAssignedImage)
            return;

        backgroundImage = ResolveConfiguredBackgroundImage();
        if (backgroundImage == null)
            backgroundImage = FindBestSceneBackgroundImage();
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

    public static Image FindBestSceneBackgroundImage()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Image[] images = Resources.FindObjectsOfTypeAll<Image>();

        Image best = null;
        float bestArea = -1f;

        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null || !img.gameObject.activeInHierarchy)
                continue;

            GameObject go = img.gameObject;
            if (!go.scene.IsValid() || go.scene != activeScene)
                continue;
            if (!LooksLikeBackgroundName(go.name))
                continue;
            if (go.GetComponentInParent<Setting>(true) != null)
                continue;
            if (go.GetComponentInParent<TMP_Dropdown>(true) != null)
                continue;
            if (go.GetComponentInParent<Dropdown>(true) != null)
                continue;

            RectTransform rt = go.transform as RectTransform;
            float area = rt != null ? Mathf.Abs(rt.rect.width * rt.rect.height) : 0f;
            if (best == null || area > bestArea)
            {
                best = img;
                bestArea = area;
            }
        }

        return best;
    }

    private static Image ResolveConfiguredBackgroundImage()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        Setting[] settings = Resources.FindObjectsOfTypeAll<Setting>();
        for (int i = 0; i < settings.Length; i++)
        {
            Setting setting = settings[i];
            if (setting == null)
                continue;
            if (!setting.gameObject.scene.IsValid() || setting.gameObject.scene != activeScene)
                continue;
            if (IsUsableBackgroundReference(setting.backgroundImage, activeScene))
                return setting.backgroundImage;
        }

        Settings_Manager[] managers = Resources.FindObjectsOfTypeAll<Settings_Manager>();
        for (int i = 0; i < managers.Length; i++)
        {
            Settings_Manager manager = managers[i];
            if (manager == null)
                continue;
            if (!manager.gameObject.scene.IsValid() || manager.gameObject.scene != activeScene)
                continue;
            if (IsUsableBackgroundReference(manager.backgroundImage, activeScene))
                return manager.backgroundImage;
        }

        return null;
    }

    private static bool IsUsableBackgroundReference(Image image, Scene activeScene)
    {
        return
            image != null &&
            image.gameObject.scene.IsValid() &&
            image.gameObject.scene == activeScene;
    }

    private static bool LooksLikeBackgroundName(string objectName)
    {
        return
            string.Equals(objectName, "Background", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(objectName, "bg", System.StringComparison.OrdinalIgnoreCase);
    }

    private static List<Sprite> ResolveBackgroundSpriteList()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        Setting[] settings = Resources.FindObjectsOfTypeAll<Setting>();
        for (int i = 0; i < settings.Length; i++)
        {
            Setting s = settings[i];
            if (s == null)
                continue;
            if (!s.gameObject.scene.IsValid() || s.gameObject.scene != activeScene)
                continue;
            if (s.backgroundSprites != null && s.backgroundSprites.Count > 0)
                return s.backgroundSprites;
        }

        Settings_Manager[] managers = Resources.FindObjectsOfTypeAll<Settings_Manager>();
        for (int i = 0; i < managers.Length; i++)
        {
            Settings_Manager m = managers[i];
            if (m == null)
                continue;
            if (!m.gameObject.scene.IsValid() || m.gameObject.scene != activeScene)
                continue;
            if (m.backgroundSprites != null && m.backgroundSprites.Count > 0)
                return m.backgroundSprites;
        }

        return null;
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

    private void ApplySavedAudioLocally()
    {
        float musicLinear = Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", 1f));

        if (musicSource != null && musicSource.enabled)
            musicSource.volume = musicLinear;

        float sfxLinear = Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", 1f));
        float sfxSafe = Mathf.Max(MinLinearVolume, sfxLinear);
        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxSafe) * 20f);
    }

    private void RefreshBackgroundSettingState()
    {
        if (backgroundDropdown == null)
            return;

        backgroundDropdown.gameObject.SetActive(IsBackgroundSettingEnabled());
    }

    private bool IsBackgroundSettingEnabled()
    {
        if (!enableBackgroundSetting)
            return false;

        if (string.IsNullOrWhiteSpace(disableBackgroundInScene))
            return true;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return true;

        return !string.Equals(
            activeScene.name,
            disableBackgroundInScene,
            System.StringComparison.OrdinalIgnoreCase
        );
    }
}
