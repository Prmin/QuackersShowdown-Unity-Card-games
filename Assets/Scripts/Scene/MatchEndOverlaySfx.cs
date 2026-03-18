using UnityEngine;

public class MatchEndOverlaySfx : MonoBehaviour
{
    private const string SfxVolumeKey = "SFXVolume";

    public enum Outcome
    {
        Win = 0,
        Loss = 1,
        Draw = 2,
        Problem = 3
    }

    private static MatchEndOverlaySfx instance;

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Result Clips")]
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip lossClip;
    [SerializeField] private AudioClip drawClip;
    [SerializeField] private AudioClip problemClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float winBaseVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float lossBaseVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float drawBaseVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float problemBaseVolume = 0.92f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float minIntervalSeconds = 0.08f;

    private float _lastPlayAt = -999f;
    private int _lastPlayFrame = -1;
    private AudioClip _fallbackWinClip;
    private AudioClip _fallbackLossClip;
    private AudioClip _fallbackDrawClip;
    private AudioClip _fallbackProblemClip;

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

    public static void Notify(Outcome outcome)
    {
        MatchEndOverlaySfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlay(outcome);
    }

    private static MatchEndOverlaySfx ResolveInstance()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<MatchEndOverlaySfx>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("[MatchEndOverlaySfx]");
        instance = go.AddComponent<MatchEndOverlaySfx>();
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

    private void TryPlay(Outcome outcome)
    {
        if (Time.unscaledTime - _lastPlayAt < minIntervalSeconds)
            return;
        if (_lastPlayFrame == Time.frameCount)
            return;

        AudioClip clip;
        float baseVolume;
        switch (outcome)
        {
            case Outcome.Win:
                clip = winClip != null ? winClip : GetFallbackWinClip();
                baseVolume = winBaseVolume;
                break;
            case Outcome.Loss:
                clip = lossClip != null ? lossClip : GetFallbackLossClip();
                baseVolume = lossBaseVolume;
                break;
            case Outcome.Draw:
                clip = drawClip != null ? drawClip : GetFallbackDrawClip();
                baseVolume = drawBaseVolume;
                break;
            default:
                clip = problemClip != null ? problemClip : GetFallbackProblemClip();
                baseVolume = problemBaseVolume;
                break;
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

    private AudioClip GetFallbackWinClip()
    {
        if (_fallbackWinClip != null)
            return _fallbackWinClip;

        _fallbackWinClip = CreateProceduralClip("MatchWin_Fallback", 0.2f, 660f, 990f, 0.15f);
        return _fallbackWinClip;
    }

    private AudioClip GetFallbackLossClip()
    {
        if (_fallbackLossClip != null)
            return _fallbackLossClip;

        _fallbackLossClip = CreateProceduralClip("MatchLoss_Fallback", 0.22f, 330f, 190f, 0.16f);
        return _fallbackLossClip;
    }

    private AudioClip GetFallbackDrawClip()
    {
        if (_fallbackDrawClip != null)
            return _fallbackDrawClip;

        _fallbackDrawClip = CreateProceduralClip("MatchDraw_Fallback", 0.2f, 420f, 540f, 0.13f);
        return _fallbackDrawClip;
    }

    private AudioClip GetFallbackProblemClip()
    {
        if (_fallbackProblemClip != null)
            return _fallbackProblemClip;

        _fallbackProblemClip = CreateProceduralClip("MatchProblem_Fallback", 0.18f, 240f, 120f, 0.18f);
        return _fallbackProblemClip;
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
            float env = Mathf.Exp(-22f * t);
            float waveA = Mathf.Sin(2f * Mathf.PI * freqA * t);
            float waveB = Mathf.Sin(2f * Mathf.PI * freqB * t);
            samples[i] = (waveA * 0.6f + waveB * 0.4f) * env * gain;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
