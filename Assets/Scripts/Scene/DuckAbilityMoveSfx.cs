using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DuckAbilityMoveSfx : MonoBehaviour
{
    private const string SfxVolumeKey = "SFXVolume";

    private static DuckAbilityMoveSfx instance;

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Wing Flap")]
    [SerializeField] private AudioClip wingFlapClip;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.88f;
    [SerializeField, Min(0.01f)] private float minIntervalSeconds = 0.07f;
    [SerializeField, Min(0.01f)] private float chainIntervalSeconds = 0.08f;

    private float _lastPlayAt = -999f;
    private int _lastPlayFrame = -1;
    private Coroutine _chainRoutine;
    private AudioClip _fallbackClip;

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

    public static void NotifyWingFlap(int flapCount = 1)
    {
        DuckAbilityMoveSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlay(Mathf.Clamp(flapCount, 1, 4));
    }

    private static DuckAbilityMoveSfx ResolveInstance()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<DuckAbilityMoveSfx>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("[DuckAbilityMoveSfx]");
        instance = go.AddComponent<DuckAbilityMoveSfx>();
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

    private void TryPlay(int flapCount)
    {
        if (_chainRoutine != null)
        {
            StopCoroutine(_chainRoutine);
            _chainRoutine = null;
        }

        if (flapCount <= 1)
        {
            TryPlayOnce();
            return;
        }

        _chainRoutine = StartCoroutine(CoPlayChain(flapCount));
    }

    private IEnumerator CoPlayChain(int flapCount)
    {
        for (int i = 0; i < flapCount; i++)
        {
            TryPlayOnce();
            if (i < flapCount - 1)
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, chainIntervalSeconds));
        }

        _chainRoutine = null;
    }

    private void TryPlayOnce()
    {
        if (Time.unscaledTime - _lastPlayAt < minIntervalSeconds)
            return;
        if (_lastPlayFrame == Time.frameCount)
            return;

        EnsureAudioSource();
        if (audioSource == null || !audioSource.enabled)
            return;

        AudioClip clip = wingFlapClip != null ? wingFlapClip : GetFallbackClip();
        if (clip == null)
            return;

        float sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        if (sfxVolume <= 0.0001f)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(baseVolume) * sfxVolume);
        _lastPlayAt = Time.unscaledTime;
        _lastPlayFrame = Time.frameCount;
    }

    private AudioClip GetFallbackClip()
    {
        if (_fallbackClip != null)
            return _fallbackClip;

        _fallbackClip = CreateProceduralClip(
            "WingFlap_Fallback",
            0.075f,
            720f,
            1120f,
            0.12f
        );
        return _fallbackClip;
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
            float env = Mathf.Exp(-30f * t);
            float waveA = Mathf.Sin(2f * Mathf.PI * freqA * t);
            float waveB = Mathf.Sin(2f * Mathf.PI * freqB * t);
            samples[i] = (waveA * 0.55f + waveB * 0.45f) * env * gain;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
