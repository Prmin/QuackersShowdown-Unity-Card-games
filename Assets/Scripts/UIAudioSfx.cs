using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class UIAudioSfx : MonoBehaviour
{
    private const float MinLinearVolume = 0.0001f;

    private static UIAudioSfx instance;
    private static bool hasPersistedMusicState;
    private static string persistedMusicClipName;
    private static float persistedMusicTimeSeconds;
    private static bool persistedMusicWasPlaying;

    [Header("Button Click")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField, Range(0f, 1f)] private float buttonClickBaseVolume = 1f;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool createFallbackClickIfMissing = true;

    private AudioSource musicAudioSource;
    private AudioSource clickAudioSource;
    private AudioClip fallbackClickClip;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        musicAudioSource = GetComponent<AudioSource>();
        bool shouldAutoPlay = false;
        if (musicAudioSource != null)
        {
            musicAudioSource.spatialBlend = 0f;
            musicAudioSource.loop = true;
            shouldAutoPlay = musicAudioSource.playOnAwake;
            musicAudioSource.playOnAwake = false;
            RestorePersistedMusicState();
        }

        EnsureClickAudioSource();
        ApplySavedSfxMixerVolume();

        if (shouldAutoPlay)
            SyncBackgroundPlaybackWithSavedMusicVolume(startIfNeeded: true);

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        CacheMusicState();

        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void LateUpdate()
    {
        CacheMusicState();
    }

    public static void PlayButtonClick()
    {
        UIAudioSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.PlayButtonClickInternal();
    }

    public static void RefreshMusicStateFromPrefs()
    {
        UIAudioSfx sfx = instance != null ? instance : FindObjectOfType<UIAudioSfx>();
        if (sfx == null)
            return;

        sfx.ApplySavedSfxMixerVolume();
        sfx.SyncBackgroundPlaybackWithSavedMusicVolume(startIfNeeded: true);
    }

    private static UIAudioSfx ResolveInstance()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<UIAudioSfx>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("[UIAudioSfx]");
        instance = go.AddComponent<UIAudioSfx>();
        return instance;
    }

    private void PlayButtonClickInternal()
    {
        EnsureClickAudioSource();

        AudioClip clip = buttonClickClip;
        if (clip == null && createFallbackClickIfMissing)
            clip = GetFallbackClickClip();

        if (clip == null || clickAudioSource == null)
            return;

        float sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", 1f));
        clickAudioSource.PlayOneShot(clip, buttonClickBaseVolume * sfxVolume);
    }

    private void SyncBackgroundPlaybackWithSavedMusicVolume(bool startIfNeeded)
    {
        if (musicAudioSource == null || musicAudioSource.clip == null)
            return;

        float musicLinear = Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", 1f));
        musicAudioSource.volume = musicLinear;

        if (musicLinear <= MinLinearVolume)
        {
            if (musicAudioSource.isPlaying)
                musicAudioSource.Pause();
            return;
        }

        if (!startIfNeeded)
            return;

        if (!musicAudioSource.isPlaying)
        {
            RestorePersistedMusicState();
            musicAudioSource.UnPause();
            if (!musicAudioSource.isPlaying)
                musicAudioSource.Play();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncBackgroundPlaybackWithSavedMusicVolume(startIfNeeded: true);
    }

    private void ApplySavedSfxMixerVolume()
    {
        if (musicAudioSource == null)
            return;

        AudioMixerGroup outputGroup = musicAudioSource.outputAudioMixerGroup;
        AudioMixer mixer = outputGroup != null ? outputGroup.audioMixer : null;
        if (mixer == null)
            return;

        // Keep master neutral; music is now controlled per-source.
        mixer.SetFloat("MusicVolume", 0f);

        float sfxLinear = Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", 1f));
        mixer.SetFloat("SFXVolume", LinearToDb(sfxLinear));
    }

    private void EnsureClickAudioSource()
    {
        if (clickAudioSource != null)
            return;

        clickAudioSource = gameObject.AddComponent<AudioSource>();
        clickAudioSource.playOnAwake = false;
        clickAudioSource.loop = false;
        clickAudioSource.spatialBlend = 0f;
        clickAudioSource.volume = 1f;

        AudioMixerGroup sfxGroup = ResolveSfxOutputGroup();
        if (sfxGroup != null)
            clickAudioSource.outputAudioMixerGroup = sfxGroup;
    }

    private AudioMixerGroup ResolveSfxOutputGroup()
    {
        if (musicAudioSource == null)
            return null;

        AudioMixerGroup currentGroup = musicAudioSource.outputAudioMixerGroup;
        AudioMixer mixer = currentGroup != null ? currentGroup.audioMixer : null;
        if (mixer == null)
            return null;

        AudioMixerGroup[] sfxGroups = mixer.FindMatchingGroups("New Group");
        if (sfxGroups != null && sfxGroups.Length > 0)
            return sfxGroups[0];

        return currentGroup;
    }

    private void CacheMusicState()
    {
        if (musicAudioSource == null || musicAudioSource.clip == null)
            return;

        hasPersistedMusicState = true;
        persistedMusicClipName = musicAudioSource.clip.name;
        persistedMusicTimeSeconds = Mathf.Clamp(musicAudioSource.time, 0f, musicAudioSource.clip.length);
        persistedMusicWasPlaying = musicAudioSource.isPlaying;
    }

    private void RestorePersistedMusicState()
    {
        if (!hasPersistedMusicState || musicAudioSource == null || musicAudioSource.clip == null)
            return;

        if (!string.Equals(musicAudioSource.clip.name, persistedMusicClipName, System.StringComparison.Ordinal))
            return;

        if (musicAudioSource.clip.length <= 0f)
            return;

        float clampedTime = Mathf.Clamp(persistedMusicTimeSeconds, 0f, Mathf.Max(0f, musicAudioSource.clip.length - 0.01f));
        if (Mathf.Abs(musicAudioSource.time - clampedTime) > 0.05f)
            musicAudioSource.time = clampedTime;

        if (persistedMusicWasPlaying && !musicAudioSource.isPlaying)
            musicAudioSource.UnPause();
    }

    private static float LinearToDb(float linear)
    {
        return Mathf.Log10(Mathf.Max(MinLinearVolume, linear)) * 20f;
    }

    private AudioClip GetFallbackClickClip()
    {
        if (fallbackClickClip != null)
            return fallbackClickClip;

        const int sampleRate = 44100;
        const float durationSeconds = 0.045f;
        int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-55f * t);
            float waveA = Mathf.Sin(2f * Mathf.PI * 1100f * t);
            float waveB = Mathf.Sin(2f * Mathf.PI * 1800f * t);
            samples[i] = (waveA * 0.6f + waveB * 0.4f) * envelope * 0.24f;
        }

        fallbackClickClip = AudioClip.Create("UIButtonClick_Fallback", sampleCount, 1, sampleRate, false);
        fallbackClickClip.SetData(samples, 0);
        return fallbackClickClip;
    }
}
