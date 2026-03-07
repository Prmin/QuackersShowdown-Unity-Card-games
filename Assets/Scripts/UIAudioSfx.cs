using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class UIAudioSfx : MonoBehaviour
{
    private static UIAudioSfx instance;

    [Header("Button Click")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField, Range(0f, 1f)] private float buttonClickBaseVolume = 1f;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool createFallbackClickIfMissing = true;

    private AudioSource audioSource;
    private AudioClip fallbackClickClip;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    public static void PlayButtonClick()
    {
        UIAudioSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.PlayButtonClickInternal();
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
        AudioClip clip = buttonClickClip;
        if (clip == null && createFallbackClickIfMissing)
            clip = GetFallbackClickClip();

        if (clip == null)
            return;

        float sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SFXVolume", 1f));
        audioSource.PlayOneShot(clip, buttonClickBaseVolume * sfxVolume);
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
