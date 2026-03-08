using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ShootActionSfx : MonoBehaviour
{
    private const string SfxVolumeKey = "SFXVolume";

    private static ShootActionSfx instance;

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Clips")]
    [SerializeField] private AudioClip aimSkillClip;
    [SerializeField] private AudioClip targetSelectClip;
    [SerializeField] private AudioClip gunShotClip;
    [SerializeField] private AudioClip duckCryClip;
    [SerializeField] private AudioClip marshSplashClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float aimSkillBaseVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float targetSelectBaseVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float gunBaseVolume = 0.95f;
    [SerializeField, Range(0f, 1f)] private float duckCryBaseVolume = 0.95f;
    [SerializeField, Range(0f, 1f)] private float marshSplashBaseVolume = 0.9f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float aimSkillMinIntervalSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float targetSelectMinIntervalSeconds = 0.04f;
    [SerializeField, Min(0.01f)] private float gunMinIntervalSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float duckCryMinIntervalSeconds = 0.06f;
    [SerializeField, Min(0.01f)] private float marshSplashMinIntervalSeconds = 0.06f;
    [SerializeField, Min(0.01f)] private float duckCryChainIntervalSeconds = 0.08f;

    private float _lastAimSkillPlayAt = -999f;
    private int _lastAimSkillPlayFrame = -1;
    private float _lastTargetSelectPlayAt = -999f;
    private int _lastTargetSelectPlayFrame = -1;
    private float _lastGunPlayAt = -999f;
    private int _lastGunPlayFrame = -1;
    private float _lastDuckCryPlayAt = -999f;
    private int _lastDuckCryPlayFrame = -1;
    private float _lastMarshSplashPlayAt = -999f;
    private int _lastMarshSplashPlayFrame = -1;
    private Coroutine _resolveHitChainRoutine;

    private AudioClip _fallbackAimSkillClip;
    private AudioClip _fallbackTargetSelectClip;
    private AudioClip _fallbackGunClip;
    private AudioClip _fallbackDuckCryClip;
    private AudioClip _fallbackMarshSplashClip;

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

    public static void NotifyTargetSelected()
    {
        ShootActionSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlayTargetSelect();
    }

    public static void NotifyAimSkillActivated()
    {
        ShootActionSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlayAimSkill();
    }

    public static void NotifyShotResolved(int duckHitCount = 1, int marshHitCount = 0)
    {
        ShootActionSfx sfx = ResolveInstance();
        if (sfx == null)
            return;

        sfx.TryPlayShotResolved(duckHitCount, marshHitCount);
    }

    private static ShootActionSfx ResolveInstance()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<ShootActionSfx>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("[ShootActionSfx]");
        instance = go.AddComponent<ShootActionSfx>();
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

    private void TryPlayShotResolved(int duckHitCount, int marshHitCount)
    {
        int safeDuck = Mathf.Clamp(duckHitCount, 0, 3);
        int safeMarsh = Mathf.Clamp(marshHitCount, 0, 3);
        int totalHits = safeDuck + safeMarsh;
        if (totalHits <= 0)
            safeDuck = 1;

        TryPlayGun();

        if (_resolveHitChainRoutine != null)
        {
            StopCoroutine(_resolveHitChainRoutine);
            _resolveHitChainRoutine = null;
        }

        if (safeDuck + safeMarsh <= 1)
        {
            if (safeMarsh > 0)
                TryPlayMarshSplash();
            else
                TryPlayDuckCry();
            return;
        }

        _resolveHitChainRoutine = StartCoroutine(CoPlayResolveHitChain(safeDuck, safeMarsh));
    }

    private IEnumerator CoPlayResolveHitChain(int duckCount, int marshCount)
    {
        int total = duckCount + marshCount;
        for (int i = 0; i < total; i++)
        {
            if (marshCount > 0)
            {
                TryPlayMarshSplash();
                marshCount--;
            }
            else
            {
                TryPlayDuckCry();
                duckCount--;
            }

            if (i < total - 1)
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duckCryChainIntervalSeconds));
        }

        _resolveHitChainRoutine = null;
    }

    private void TryPlayTargetSelect()
    {
        if (Time.unscaledTime - _lastTargetSelectPlayAt < targetSelectMinIntervalSeconds)
            return;
        if (_lastTargetSelectPlayFrame == Time.frameCount)
            return;

        AudioClip clip = targetSelectClip != null ? targetSelectClip : GetFallbackTargetSelectClip();
        if (!TryPlayClip(clip, targetSelectBaseVolume))
            return;

        _lastTargetSelectPlayAt = Time.unscaledTime;
        _lastTargetSelectPlayFrame = Time.frameCount;
    }

    private void TryPlayAimSkill()
    {
        if (Time.unscaledTime - _lastAimSkillPlayAt < aimSkillMinIntervalSeconds)
            return;
        if (_lastAimSkillPlayFrame == Time.frameCount)
            return;

        AudioClip clip = aimSkillClip != null ? aimSkillClip : GetFallbackAimSkillClip();
        if (!TryPlayClip(clip, aimSkillBaseVolume))
            return;

        _lastAimSkillPlayAt = Time.unscaledTime;
        _lastAimSkillPlayFrame = Time.frameCount;
    }

    private void TryPlayGun()
    {
        if (Time.unscaledTime - _lastGunPlayAt < gunMinIntervalSeconds)
            return;
        if (_lastGunPlayFrame == Time.frameCount)
            return;

        AudioClip clip = gunShotClip != null ? gunShotClip : GetFallbackGunClip();
        if (!TryPlayClip(clip, gunBaseVolume))
            return;

        _lastGunPlayAt = Time.unscaledTime;
        _lastGunPlayFrame = Time.frameCount;
    }

    private void TryPlayDuckCry()
    {
        if (Time.unscaledTime - _lastDuckCryPlayAt < duckCryMinIntervalSeconds)
            return;
        if (_lastDuckCryPlayFrame == Time.frameCount)
            return;

        AudioClip clip = duckCryClip != null ? duckCryClip : GetFallbackDuckCryClip();
        if (!TryPlayClip(clip, duckCryBaseVolume))
            return;

        _lastDuckCryPlayAt = Time.unscaledTime;
        _lastDuckCryPlayFrame = Time.frameCount;
    }

    private void TryPlayMarshSplash()
    {
        if (Time.unscaledTime - _lastMarshSplashPlayAt < marshSplashMinIntervalSeconds)
            return;
        if (_lastMarshSplashPlayFrame == Time.frameCount)
            return;

        AudioClip clip = marshSplashClip != null ? marshSplashClip : GetFallbackMarshSplashClip();
        if (!TryPlayClip(clip, marshSplashBaseVolume))
            return;

        _lastMarshSplashPlayAt = Time.unscaledTime;
        _lastMarshSplashPlayFrame = Time.frameCount;
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

    private AudioClip GetFallbackGunClip()
    {
        if (_fallbackGunClip != null)
            return _fallbackGunClip;

        _fallbackGunClip = CreateProceduralClip(
            "ShootGun_Fallback",
            0.08f,
            900f,
            240f,
            0.14f
        );
        return _fallbackGunClip;
    }

    private AudioClip GetFallbackAimSkillClip()
    {
        if (_fallbackAimSkillClip != null)
            return _fallbackAimSkillClip;

        _fallbackAimSkillClip = CreateProceduralClip(
            "AimSkill_Fallback",
            0.07f,
            1280f,
            610f,
            0.12f
        );
        return _fallbackAimSkillClip;
    }

    private AudioClip GetFallbackTargetSelectClip()
    {
        if (_fallbackTargetSelectClip != null)
            return _fallbackTargetSelectClip;

        _fallbackTargetSelectClip = CreateProceduralClip(
            "TargetSelect_Fallback",
            0.045f,
            1550f,
            980f,
            0.10f
        );
        return _fallbackTargetSelectClip;
    }

    private AudioClip GetFallbackDuckCryClip()
    {
        if (_fallbackDuckCryClip != null)
            return _fallbackDuckCryClip;

        _fallbackDuckCryClip = CreateProceduralClip(
            "DuckCry_Fallback",
            0.11f,
            520f,
            760f,
            0.16f
        );
        return _fallbackDuckCryClip;
    }

    private AudioClip GetFallbackMarshSplashClip()
    {
        if (_fallbackMarshSplashClip != null)
            return _fallbackMarshSplashClip;

        _fallbackMarshSplashClip = CreateProceduralClip(
            "MarshSplash_Fallback",
            0.11f,
            240f,
            420f,
            0.14f
        );
        return _fallbackMarshSplashClip;
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
            samples[i] = (waveA * 0.65f + waveB * 0.35f) * env * gain;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
