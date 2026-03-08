using UnityEngine;

[DisallowMultipleComponent]
public class CardZoneMoveSfx : MonoBehaviour
{
    private const string SfxVolumeKey = "SFXVolume";

    private static CardZoneMoveSfx instance;

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("PlayerArea Move")]
    [SerializeField] private AudioClip playerAreaMoveClip;
    [SerializeField, Range(0f, 1f)] private float playerAreaBaseVolume = 0.65f;
    [SerializeField, Min(0.01f)] private float playerAreaMinIntervalSeconds = 0.08f;

    [Header("DropZone Place")]
    [SerializeField] private AudioClip dropZonePlaceClip;
    [SerializeField, Range(0f, 1f)] private float dropZoneBaseVolume = 0.9f;
    [SerializeField, Min(0.01f)] private float dropZoneMinIntervalSeconds = 0.06f;

    private float _lastPlayerAreaPlayAt = -999f;
    private int _lastPlayerAreaPlayFrame = -1;
    private float _lastDropZonePlayAt = -999f;
    private int _lastDropZonePlayFrame = -1;

    private AudioClip _fallbackPlayerAreaClip;
    private AudioClip _fallbackDropZoneClip;

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

    public static void NotifyPlayerAreaMove()
    {
        CardZoneMoveSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlayPlayerArea();
    }

    public static void NotifyDropZonePlaced()
    {
        CardZoneMoveSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlayDropZone();
    }

    private static CardZoneMoveSfx ResolveInstance()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<CardZoneMoveSfx>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("[CardZoneMoveSfx]");
        instance = go.AddComponent<CardZoneMoveSfx>();
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

    private void TryPlayPlayerArea()
    {
        if (Time.unscaledTime - _lastPlayerAreaPlayAt < playerAreaMinIntervalSeconds)
            return;
        if (_lastPlayerAreaPlayFrame == Time.frameCount)
            return;

        AudioClip clip = playerAreaMoveClip != null ? playerAreaMoveClip : GetFallbackPlayerAreaClip();
        if (!TryPlayClip(clip, playerAreaBaseVolume))
            return;

        _lastPlayerAreaPlayAt = Time.unscaledTime;
        _lastPlayerAreaPlayFrame = Time.frameCount;
    }

    private void TryPlayDropZone()
    {
        if (Time.unscaledTime - _lastDropZonePlayAt < dropZoneMinIntervalSeconds)
            return;
        if (_lastDropZonePlayFrame == Time.frameCount)
            return;

        AudioClip clip = dropZonePlaceClip != null ? dropZonePlaceClip : GetFallbackDropZoneClip();
        if (!TryPlayClip(clip, dropZoneBaseVolume))
            return;

        _lastDropZonePlayAt = Time.unscaledTime;
        _lastDropZonePlayFrame = Time.frameCount;
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

    private AudioClip GetFallbackPlayerAreaClip()
    {
        if (_fallbackPlayerAreaClip != null)
            return _fallbackPlayerAreaClip;

        _fallbackPlayerAreaClip = CreateProceduralClip(
            "CardMovePA_Fallback",
            0.05f,
            780f,
            1300f,
            0.11f
        );
        return _fallbackPlayerAreaClip;
    }

    private AudioClip GetFallbackDropZoneClip()
    {
        if (_fallbackDropZoneClip != null)
            return _fallbackDropZoneClip;

        _fallbackDropZoneClip = CreateProceduralClip(
            "CardDropZone_Fallback",
            0.07f,
            520f,
            920f,
            0.16f
        );
        return _fallbackDropZoneClip;
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
            float env = Mathf.Exp(-40f * t);
            float waveA = Mathf.Sin(2f * Mathf.PI * freqA * t);
            float waveB = Mathf.Sin(2f * Mathf.PI * freqB * t);
            samples[i] = (waveA * 0.65f + waveB * 0.35f) * env * gain;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
