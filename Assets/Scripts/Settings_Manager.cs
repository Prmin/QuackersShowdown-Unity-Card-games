using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class Settings_Manager : MonoBehaviour
{
    private const float MinLinearVolume = 0.0001f;
    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SFXVolume";
    private const string BackgroundChoiceKey = "BackgroundChoice";
    private const string BackgroundSpriteNameKey = "BackgroundSpriteName";

    public static Settings_Manager instance = null;

    public AudioMixer audioMixer;
    public AudioSource musicSource;
    public float musicVolume = 1.0f;
    public float sfxVolume = 1.0f;
    public int backgroundChoice = 0;
    public Image backgroundImage;
    public List<Sprite> backgroundSprites;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        LoadSettings();
    }

    private void Start()
    {
        ApplySettings();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        PlayerPrefs.Save();

        UIAudioSfx.RefreshMusicStateFromPrefs();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", LinearToDb(sfxVolume));

        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.Save();
    }

    public void SetBackground(int choice)
    {
        backgroundChoice = Mathf.Max(0, choice);
        PlayerPrefs.SetInt(BackgroundChoiceKey, backgroundChoice);
        if (backgroundSprites != null && backgroundSprites.Count > 0)
        {
            int clamped = Mathf.Clamp(backgroundChoice, 0, backgroundSprites.Count - 1);
            Sprite sprite = backgroundSprites[clamped];
            PlayerPrefs.SetString(BackgroundSpriteNameKey, sprite != null ? sprite.name : string.Empty);
        }
        PlayerPrefs.Save();

        TryResolveBackgroundImageInActiveScene();
        ApplyBackground(backgroundImage);
    }

    private void LoadSettings()
    {
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1.0f));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1.0f));
        backgroundChoice = ResolveSavedBackgroundIndex(backgroundSprites, 0);
    }

    public void ApplySettings()
    {
        LoadSettings();

        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", LinearToDb(sfxVolume));

        backgroundImage = null;
        TryResolveBackgroundImageInActiveScene();
        ApplyBackground(backgroundImage);
        Setting.ApplySavedBackgroundToActiveScene();

        UIAudioSfx.RefreshMusicStateFromPrefs();
    }

    public void ApplyBackground(Image image)
    {
        if (image == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Count == 0)
            return;

        int index = Mathf.Clamp(backgroundChoice, 0, backgroundSprites.Count - 1);
        image.sprite = backgroundSprites[index];
    }

    private void TryResolveBackgroundImageInActiveScene()
    {
        backgroundImage = Setting.FindBestSceneBackgroundImage();
    }

    private static float LinearToDb(float linear)
    {
        return Mathf.Log10(Mathf.Max(MinLinearVolume, linear)) * 20f;
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
}
