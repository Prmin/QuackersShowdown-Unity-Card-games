using UnityEngine;

[DisallowMultipleComponent]
public class DuckZoneMoveSfx : MonoBehaviour
{
    private const string SfxVolumeKey = "SFXVolume";

    private static DuckZoneMoveSfx instance;

    [Header("Sound")]
    [SerializeField] private AudioClip moveClip;
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.8f;
    [SerializeField, Min(0.01f)] private float minIntervalSeconds = 0.08f;

    private float _lastPlayAt = -999f;
    private int _lastPlayFrame = -1;
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

    public static void NotifyDuckMovedInZone()
    {
        DuckZoneMoveSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlay();
    }

    private static DuckZoneMoveSfx ResolveInstance()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<DuckZoneMoveSfx>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("[DuckZoneMoveSfx]");
        instance = go.AddComponent<DuckZoneMoveSfx>();
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

    private void TryPlay()
    {
        if (Time.unscaledTime - _lastPlayAt < minIntervalSeconds)
            return;

        if (_lastPlayFrame == Time.frameCount)
            return;

        EnsureAudioSource();
        if (audioSource == null || !audioSource.enabled)
            return;

        AudioClip clip = moveClip != null ? moveClip : GetFallbackClip();
        if (clip == null)
            return;

        float sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        if (sfxVolume <= 0.0001f)
            return;

        audioSource.PlayOneShot(clip, baseVolume * sfxVolume);
        _lastPlayAt = Time.unscaledTime;
        _lastPlayFrame = Time.frameCount;
    }

    private AudioClip GetFallbackClip()
    {
        if (_fallbackClip != null)
            return _fallbackClip;

        const int sampleRate = 44100;
        const float duration = 0.06f;
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Exp(-38f * t);
            float wave = Mathf.Sin(2f * Mathf.PI * 720f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 1220f * t);
            data[i] = wave * env * 0.12f;
        }

        _fallbackClip = AudioClip.Create("DuckZoneMove_Fallback", count, 1, sampleRate, false);
        _fallbackClip.SetData(data, 0);
        return _fallbackClip;
    }
}
