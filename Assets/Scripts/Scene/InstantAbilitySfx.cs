using UnityEngine;

[DisallowMultipleComponent]
public class InstantAbilitySfx : MonoBehaviour
{
    private const string SfxVolumeKey = "SFXVolume";

    private static InstantAbilitySfx instance;

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Ability Clips")]
    [SerializeField] private AudioClip duckShuffleClip;
    [SerializeField] private AudioClip givePeaceAChanceClip;
    [SerializeField] private AudioClip resurrectionClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float duckShuffleBaseVolume = 0.92f;
    [SerializeField, Range(0f, 1f)] private float givePeaceBaseVolume = 0.88f;
    [SerializeField, Range(0f, 1f)] private float resurrectionBaseVolume = 0.9f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float minIntervalSeconds = 0.08f;

    private float _lastPlayAt = -999f;
    private int _lastPlayFrame = -1;
    private AudioClip _fallbackDuckShuffleClip;
    private AudioClip _fallbackGivePeaceClip;
    private AudioClip _fallbackResurrectionClip;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureAudioSource();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void NotifyActivated(SkillMode mode)
    {
        InstantAbilitySfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlay(mode);
    }

    private static InstantAbilitySfx ResolveInstance()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<InstantAbilitySfx>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("[InstantAbilitySfx]");
        instance = go.AddComponent<InstantAbilitySfx>();
        return instance;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
    }

    private void TryPlay(SkillMode mode)
    {
        if (Time.unscaledTime - _lastPlayAt < minIntervalSeconds)
            return;
        if (_lastPlayFrame == Time.frameCount)
            return;

        AudioClip clip;
        float baseVolume;

        switch (mode)
        {
            case SkillMode.DuckShuffle:
                clip = duckShuffleClip != null ? duckShuffleClip : GetFallbackDuckShuffleClip();
                baseVolume = duckShuffleBaseVolume;
                break;
            case SkillMode.GivePeaceAChance:
                clip = givePeaceAChanceClip != null ? givePeaceAChanceClip : GetFallbackGivePeaceClip();
                baseVolume = givePeaceBaseVolume;
                break;
            case SkillMode.Resurrection:
                clip = resurrectionClip != null ? resurrectionClip : GetFallbackResurrectionClip();
                baseVolume = resurrectionBaseVolume;
                break;
            default:
                return;
        }

        if (!TryPlayClip(clip, baseVolume))
            return;

        _lastPlayAt = Time.unscaledTime;
        _lastPlayFrame = Time.frameCount;
    }

    private bool TryPlayClip(AudioClip clip, float baseVolume)
    {
        EnsureAudioSource();
        if (audioSource == null || !audioSource.enabled)
            return false;
        if (clip == null)
            return false;

        float sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        if (sfxVolume <= 0.0001f)
            return false;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(baseVolume) * sfxVolume);
        return true;
    }

    private AudioClip GetFallbackDuckShuffleClip()
    {
        if (_fallbackDuckShuffleClip != null)
            return _fallbackDuckShuffleClip;

        _fallbackDuckShuffleClip = CreateProceduralClip(
            "DuckShuffle_Fallback",
            0.12f,
            560f,
            980f,
            0.14f
        );
        return _fallbackDuckShuffleClip;
    }

    private AudioClip GetFallbackGivePeaceClip()
    {
        if (_fallbackGivePeaceClip != null)
            return _fallbackGivePeaceClip;

        _fallbackGivePeaceClip = CreateProceduralClip(
            "GivePeace_Fallback",
            0.1f,
            420f,
            740f,
            0.12f
        );
        return _fallbackGivePeaceClip;
    }

    private AudioClip GetFallbackResurrectionClip()
    {
        if (_fallbackResurrectionClip != null)
            return _fallbackResurrectionClip;

        _fallbackResurrectionClip = CreateProceduralClip(
            "Resurrection_Fallback",
            0.13f,
            690f,
            1240f,
            0.13f
        );
        return _fallbackResurrectionClip;
    }

    private static AudioClip CreateProceduralClip(
        string clipName,
        float durationSeconds,
        float freqA,
        float freqB,
        float gain)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * Mathf.Max(0.02f, durationSeconds));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Exp(-28f * t);
            float waveA = Mathf.Sin(2f * Mathf.PI * freqA * t);
            float waveB = Mathf.Sin(2f * Mathf.PI * freqB * t);
            samples[i] = (waveA * 0.6f + waveB * 0.4f) * env * gain;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
