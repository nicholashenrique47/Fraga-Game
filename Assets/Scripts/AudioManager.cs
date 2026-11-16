using UnityEngine;

/// <summary>
/// AudioManager: Sistema central de audio adaptativo (Dia/Noite, Sussurros de Sanidade, Horrores e Pool 3D Zero-GC).
/// Gera clipes procedurais de fallback caso o Inspector esteja vazio para garantir atmosfera completa em PC e Mobile.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource ambientSource;
    public AudioSource sanityWhisperSource;
    public AudioSource vultoSwarmSource;
    public AudioSource srcEnvironment;
    public AudioSource rainAmbientSource;

    [Header("Clips")]
    public AudioClip dayAmbient;
    public AudioClip nightAmbient;
    public AudioClip lowSanityWhispers;
    public AudioClip vultoSwarmScreech;
    public AudioClip horrorClip;
    public AudioClip pickupClip;
    public AudioClip rainAmbientClip;

    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public SanitySystem sanitySystem;
    public VultoManager vultoManager;

    private AudioLowPassFilter ambientLPF;
    private AudioSource[] sfxPool;
    private int sfxPoolIndex = 0;
    private const int SFX_POOL_SIZE = 8;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Fraga_MasterVolume", 1.0f));

        EnsureAudioSources();
        EnsureProceduralClips();
        InitSpatialSfxPool();
    }

    public void SetMasterVolume(float volume)
    {
        float clamped = Mathf.Clamp01(volume);
        AudioListener.volume = clamped;
        PlayerPrefs.SetFloat("Fraga_MasterVolume", clamped);
        PlayerPrefs.Save();
    }

    private void EnsureAudioSources()
    {
        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.spatialBlend = 0f;
            ambientSource.volume = 0.24f;
        }
        if (sanityWhisperSource == null)
        {
            sanityWhisperSource = gameObject.AddComponent<AudioSource>();
            sanityWhisperSource.spatialBlend = 0f;
            sanityWhisperSource.volume = 0f;
        }
        if (vultoSwarmSource == null)
        {
            vultoSwarmSource = gameObject.AddComponent<AudioSource>();
            vultoSwarmSource.spatialBlend = 0.35f;
            vultoSwarmSource.volume = 0.65f;
        }
        if (srcEnvironment == null)
        {
            srcEnvironment = gameObject.AddComponent<AudioSource>();
            srcEnvironment.spatialBlend = 0f;
            srcEnvironment.volume = 0.70f;
        }
        if (rainAmbientSource == null)
        {
            rainAmbientSource = gameObject.AddComponent<AudioSource>();
            rainAmbientSource.spatialBlend = 0f;
            rainAmbientSource.volume = 0f;
        }
    }

    private void EnsureProceduralClips()
    {
        if (dayAmbient == null) dayAmbient = GenerateDayAmbient();
        if (nightAmbient == null) nightAmbient = GenerateNightAmbient();
        if (lowSanityWhispers == null) lowSanityWhispers = GenerateWhisperLoop();
        if (vultoSwarmScreech == null) vultoSwarmScreech = GenerateVultoScreech();
        if (horrorClip == null) horrorClip = GenerateHorrorStingClip();
        if (pickupClip == null) pickupClip = GeneratePickupClip();
        if (rainAmbientClip == null) rainAmbientClip = GenerateRainAmbientClip();
    }

    private void InitSpatialSfxPool()
    {
        sfxPool = new AudioSource[SFX_POOL_SIZE];
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            GameObject child = new GameObject($"PooledSFX_{i}");
            child.transform.SetParent(transform, false);
            AudioSource src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            sfxPool[i] = src;
        }
    }

    private void Start()
    {
        if (dayNightCycle == null) dayNightCycle = DayNightCycle.Instance ?? FindAnyObjectByType<DayNightCycle>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (vultoManager == null) vultoManager = VultoManager.Instance ?? FindAnyObjectByType<VultoManager>();

        if (ambientSource != null)
        {
            ambientSource.loop = true;
            ambientSource.clip = dayAmbient;

            ambientLPF = ambientSource.GetComponent<AudioLowPassFilter>();
            if (ambientLPF == null) ambientLPF = ambientSource.gameObject.AddComponent<AudioLowPassFilter>();
            ambientLPF.cutoffFrequency = 22000f;

            if (ambientSource.clip != null) ambientSource.Play();
        }

        if (sanityWhisperSource != null)
        {
            sanityWhisperSource.loop = true;
            sanityWhisperSource.clip = lowSanityWhispers;
            sanityWhisperSource.volume = 0f;
            if (sanityWhisperSource.clip != null) sanityWhisperSource.Play();
        }

        if (rainAmbientSource != null && rainAmbientClip != null)
        {
            rainAmbientSource.loop = true;
            rainAmbientSource.clip = rainAmbientClip;
            rainAmbientSource.volume = 0f;
            rainAmbientSource.Play();
        }
    }

    private void Update()
    {
        UpdateAmbientSound();
        UpdateSanityWhispers();
    }

    private void UpdateAmbientSound()
    {
        if (dayNightCycle == null || ambientSource == null) return;

        bool isNight = dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f;
        AudioClip targetClip = isNight ? nightAmbient : dayAmbient;

        if (targetClip != null && ambientSource.clip != targetClip)
        {
            ambientSource.clip = targetClip;
            ambientSource.Play();
        }

        if (sanitySystem != null)
        {
            float sanity = sanitySystem.sanity;
            float t = 1f - Mathf.Clamp01(sanity / 100f);

            ambientSource.pitch = Mathf.Lerp(1.0f, 0.78f, t);

            if (ambientLPF != null)
            {
                ambientLPF.cutoffFrequency = Mathf.Lerp(22000f, 1600f, t * t);
            }
        }

        float threatBoost = (vultoManager != null && vultoManager.ActiveVultoCount > 0) ? 0.08f : 0f;
        ambientSource.volume = Mathf.Lerp(ambientSource.volume, 0.24f + threatBoost, Time.deltaTime * 1.5f);

        if (rainAmbientSource != null)
        {
            bool raining = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining;
            float wetness = GalicianWeatherEngine.Instance != null ? GalicianWeatherEngine.Instance.globalWetness : 0f;
            bool sheltered = GalicianAudioAtmosphere.Instance != null && GalicianAudioAtmosphere.Instance.isSheltered;
            float targetRainVol = raining ? (wetness * (sheltered ? 0.11f : 0.24f)) : 0f;
            rainAmbientSource.volume = Mathf.Lerp(rainAmbientSource.volume, targetRainVol, Time.deltaTime * 1.8f);
        }
    }

    private void UpdateSanityWhispers()
    {
        if (sanitySystem == null || sanityWhisperSource == null) return;

        float sanity = sanitySystem.sanity;
        if (sanity < 40f)
        {
            float targetVolume = (1f - (sanity / 40f)) * 0.45f;
            sanityWhisperSource.volume = Mathf.Lerp(sanityWhisperSource.volume, targetVolume, Time.deltaTime);
        }
        else
        {
            sanityWhisperSource.volume = Mathf.Lerp(sanityWhisperSource.volume, 0f, Time.deltaTime);
        }
    }

    public void PlayVultoAttackSound()
    {
        if (vultoSwarmSource != null && vultoSwarmScreech != null && !vultoSwarmSource.isPlaying)
        {
            vultoSwarmSource.pitch = Random.Range(0.90f, 1.12f);
            vultoSwarmSource.PlayOneShot(vultoSwarmScreech, 0.65f);
        }
    }

    public void PlayNightHorror()
    {
        if (horrorClip != null && srcEnvironment != null)
            srcEnvironment.PlayOneShot(horrorClip, 0.50f);
    }

    public void PlayPickupSound()
    {
        if (srcEnvironment != null && pickupClip != null)
            srcEnvironment.PlayOneShot(pickupClip, 0.45f);
    }

    public void PlayHorrorSting()
    {
        if (srcEnvironment != null && horrorClip != null)
        {
            srcEnvironment.pitch = Random.Range(0.92f, 1.06f);
            srcEnvironment.PlayOneShot(horrorClip, 0.48f);
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && srcEnvironment != null)
        {
            srcEnvironment.PlayOneShot(clip);
        }
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float volume, float maxDistance)
    {
        if (clip == null || sfxPool == null || sfxPool.Length == 0) return;

        AudioSource source = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;

        source.transform.position = position;
        source.volume = volume;
        source.maxDistance = Mathf.Max(4f, maxDistance);
        source.pitch = Random.Range(0.94f, 1.06f);
        source.PlayOneShot(clip, volume);
    }

    public void PlayMusic(AudioClip music)
    {
        if (ambientSource != null && music != null)
        {
            ambientSource.clip = music;
            if (!ambientSource.isPlaying) ambientSource.Play();
        }
    }

    public void StopMusic()
    {
        if (ambientSource != null)
        {
            ambientSource.Stop();
        }
    }

    // =================== SINTESE PROCEDURAL DE ATMOSFERA ===================

    private AudioClip GenerateDayAmbient()
    {
        int sr = 22050;
        int len = (int)(sr * 2.5f);
        float[] samples = new float[len];
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float env = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.4f * t);
            float white = Random.Range(-1f, 1f);
            lp = Mathf.Lerp(lp, white, 0.04f);
            samples[i] = lp * env * 0.28f;
        }
        AudioClip clip = AudioClip.Create("DayForestBreeze_Proc", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateNightAmbient()
    {
        int sr = 22050;
        int len = (int)(sr * 2.5f);
        float[] samples = new float[len];
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float wind = Random.Range(-1f, 1f);
            lp = Mathf.Lerp(lp, wind, 0.025f);
            float drone = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.14f + Mathf.Sin(2f * Mathf.PI * 78f * t) * 0.08f;
            float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 1.6f * t)), 8f) * Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.06f;
            samples[i] = Mathf.Clamp(lp * 0.25f + drone + pulse, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("NightForestEerie_Proc", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateWhisperLoop()
    {
        int sr = 22050;
        int len = sr * 2;
        float[] samples = new float[len];
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float mod = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2.5f * t);
            lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.14f);
            samples[i] = lp * mod * 0.35f;
        }
        AudioClip clip = AudioClip.Create("SanityWhispers_Proc", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateVultoScreech()
    {
        int sr = 22050;
        int len = (int)(sr * 0.55f);
        float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float sec = (float)i / sr;
            float freq = Mathf.Lerp(640f, 220f, t);
            float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-2.2f * t);
            float rasp = Mathf.Sin(2f * Mathf.PI * freq * sec) * 0.55f + Random.Range(-0.4f, 0.4f) * 0.45f;
            samples[i] = Mathf.Clamp(rasp * env, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("VultoScreech_Proc", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateHorrorStingClip()
    {
        int sr = 22050;
        int len = (int)(sr * 0.90f);
        float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float sec = (float)i / sr;
            float env = Mathf.Sin(Mathf.Min(1f, t * 4f) * Mathf.PI * 0.5f) * Mathf.Exp(-3.0f * t);
            float sub = Mathf.Sin(2f * Mathf.PI * 58f * sec) * 0.6f;
            float dissonant = Mathf.Sin(2f * Mathf.PI * 233f * sec) * 0.25f + Mathf.Sin(2f * Mathf.PI * 247f * sec) * 0.25f;
            samples[i] = Mathf.Clamp((sub + dissonant) * env, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("HorrorSting_Proc", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GeneratePickupClip()
    {
        int sr = 22050;
        int len = (int)(sr * 0.11f);
        float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float sec = (float)i / sr;
            float freq = Mathf.Lerp(320f, 560f, t);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * sec) * Mathf.Exp(-18f * t) * 0.55f;
        }
        AudioClip clip = AudioClip.Create("PickupPop_Proc", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateRainAmbientClip()
    {
        int sr = 22050;
        int len = sr * 2;
        float[] samples = new float[len];
        float lp = 0f;
        float hp = 0f;
        for (int i = 0; i < len; i++)
        {
            float w = Random.Range(-1f, 1f);
            lp = Mathf.Lerp(lp, w, 0.28f);
            hp = lp - Mathf.Lerp(hp, lp, 0.06f);
            float droplet = (i % 731 == 0) ? 0.18f : 0f;
            samples[i] = Mathf.Clamp(hp * 0.28f + droplet, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("GalicianRainLoop_Proc", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
