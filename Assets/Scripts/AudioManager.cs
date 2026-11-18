using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AudioManager: Sistema central de audio adaptativo (Dia/Noite, Sussurros de Sanidade, Horrores, Pool 3D Zero-GC e Banco de Foley AAA).
/// Prioriza clipes externos em Resources/Audio/ quando presentes e utiliza sintese multi-banda filtrada como fallback de alta definicao.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum FoleyEventType
    {
        PickupGeneric = 0,
        PickupWood = 1,
        PickupStone = 2,
        PickupHerb = 3,
        PickupWater = 4,
        EatCrunch = 5,
        DrinkGulp = 6,
        BandageWrap = 7,
        SplintSnap = 8,
        LighterFlick = 9,
        FireIgnite = 10,
        FireCrackleLoop = 11,
        CookingSizzle = 12,
        EquipWeapon = 13,
        WeaponInspect = 14,
        DoorWoodCreak = 15,
        ChestWoodLatch = 16,
        TrapMetalSnap = 17,
        ChopWood = 18,
        TreeFall = 19,
        StrikeRock = 20,
        HitFlesh = 21,
        HitShadow = 22,
        SwingLight = 23,
        SwingHeavy = 24,
        ShieldBlock = 25,
        WaterSplashIn = 26,
        SwimStroke = 27,
        ClimbScrape = 28,
        PainGrunt = 29,
        ArrowImpact = 30
    }

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
    private const int SFX_POOL_SIZE = 12;

    private readonly Dictionary<FoleyEventType, AudioClip> foleyClipCache = new Dictionary<FoleyEventType, AudioClip>(32);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
            return;
        }
        Instance = this;
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Fraga_MasterVolume", 1.0f));

        EnsureAudioSources();
        EnsureProceduralClips();
        InitSpatialSfxPool();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
            srcEnvironment.volume = 0.78f;
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
        if (dayAmbient == null) dayAmbient = Resources.Load<AudioClip>("Audio/Ambient/DayAmbient") ?? GenerateDayAmbient();
        if (nightAmbient == null) nightAmbient = Resources.Load<AudioClip>("Audio/Ambient/NightAmbient") ?? GenerateNightAmbient();
        if (lowSanityWhispers == null) lowSanityWhispers = Resources.Load<AudioClip>("Audio/Ambient/SanityWhispers") ?? GenerateWhisperLoop();
        if (vultoSwarmScreech == null) vultoSwarmScreech = Resources.Load<AudioClip>("Audio/SFX/VultoScreech") ?? GenerateVultoScreech();
        if (horrorClip == null) horrorClip = Resources.Load<AudioClip>("Audio/SFX/HorrorSting") ?? GenerateHorrorStingClip();
        if (pickupClip == null) pickupClip = GetFoleyClip(FoleyEventType.PickupGeneric);
        if (rainAmbientClip == null) rainAmbientClip = Resources.Load<AudioClip>("Audio/Ambient/RainLoop") ?? GenerateRainAmbientClip();
    }

    private void InitSpatialSfxPool()
    {
        if (sfxPool != null && sfxPool.Length == SFX_POOL_SIZE) return;

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
            src.maxDistance = 32f;
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

    // =================== API PUBLICA DE FOLEY & POOL 3D (ZERO-GC) ===================

    public int PooledSfxCount => sfxPool != null ? sfxPool.Length : 0;

    public AudioClip GetFoleyClip(FoleyEventType kind)
    {
        if (foleyClipCache.TryGetValue(kind, out AudioClip cached) && cached != null)
            return cached;

        AudioClip loaded = Resources.Load<AudioClip>("Audio/Foley/" + kind.ToString());
        AudioClip resolved = loaded != null ? loaded : SynthesizeFoleyClip(kind);
        foleyClipCache[kind] = resolved;
        return resolved;
    }

    public AudioClip PlayFoley(FoleyEventType kind, Vector3? worldPosition = null, float volume = 0.80f, float pitchMin = 0.93f, float pitchMax = 1.07f, float maxDistance = 26f)
    {
        AudioClip clip = GetFoleyClip(kind);
        if (clip == null) return null;

        float clampedVol = Mathf.Clamp01(volume);
        float pitch = pitchMax > pitchMin ? Random.Range(pitchMin, pitchMax) : pitchMin;

        if (worldPosition.HasValue && sfxPool != null && sfxPool.Length > 0)
        {
            AudioSource source = sfxPool[sfxPoolIndex];
            sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;
            if (source != null)
            {
                source.transform.position = worldPosition.Value;
                source.spatialBlend = 1f;
                source.minDistance = 2f;
                source.maxDistance = Mathf.Max(5f, maxDistance);
                source.volume = clampedVol;
                source.pitch = pitch;
                source.PlayOneShot(clip, clampedVol);
                return clip;
            }
        }

        if (srcEnvironment != null)
        {
            srcEnvironment.pitch = pitch;
            srcEnvironment.PlayOneShot(clip, clampedVol);
        }
        return clip;
    }

    public static FoleyEventType ResolvePickupFoleyForItem(int itemId)
    {
        switch (itemId)
        {
            case 100: // Madeira
            case 209: // Tala de madeira
                return FoleyEventType.PickupWood;
            case 101: // Pedra
            case 106: // Sal
            case 107: // Ferro velho
            case 120: // Osso
            case 122: // Carvao
                return FoleyEventType.PickupStone;
            case 103: // Ervas
            case 112: // Sementes
            case 114: // Pena
            case 116: // Pagina
            case 118: // Corda
            case 121: // Tecido
            case 202: // Bagas
            case 203: // Cogumelo
                return FoleyEventType.PickupHerb;
            case 109: // Agua limpa
            case 123: // Agua turva
            case 204: // Agua benta
            case 210: // Cantil
                return FoleyEventType.PickupWater;
            default:
                return FoleyEventType.PickupGeneric;
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
        {
            srcEnvironment.pitch = 1f;
            srcEnvironment.PlayOneShot(horrorClip, 0.50f);
        }
    }

    public void PlayPickupSound()
    {
        PlayFoley(FoleyEventType.PickupGeneric, null, 0.55f, 0.94f, 1.08f);
    }

    public void PlayPickupSoundForItem(int itemId, Vector3? worldPos = null)
    {
        PlayFoley(ResolvePickupFoleyForItem(itemId), worldPos, 0.62f, 0.93f, 1.08f);
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
            srcEnvironment.pitch = 1f;
            srcEnvironment.PlayOneShot(clip);
        }
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float volume, float maxDistance)
    {
        if (clip == null || sfxPool == null || sfxPool.Length == 0) return;

        AudioSource source = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;

        source.transform.position = position;
        source.spatialBlend = 1f;
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

    // =================== MOTOR DSP MULTI-BANDA PROCEDURAL (FALLBACK AAA) ===================

    private static float NextSignedNoise(ref uint seed)
    {
        seed = seed * 1664525u + 1013904223u;
        return ((seed >> 16) / 65535f) * 2f - 1f;
    }

    private AudioClip SynthesizeFoleyClip(FoleyEventType kind)
    {
        const int sr = 44100;
        float dur;
        switch (kind)
        {
            case FoleyEventType.FireCrackleLoop: dur = 2.4f; break;
            case FoleyEventType.TreeFall: dur = 1.30f; break;
            case FoleyEventType.BandageWrap: dur = 0.56f; break;
            case FoleyEventType.DrinkGulp: dur = 0.48f; break;
            case FoleyEventType.EatCrunch: dur = 0.44f; break;
            case FoleyEventType.DoorWoodCreak: dur = 0.52f; break;
            case FoleyEventType.WaterSplashIn: dur = 0.46f; break;
            case FoleyEventType.CookingSizzle: dur = 0.42f; break;
            case FoleyEventType.FireIgnite: dur = 0.45f; break;
            case FoleyEventType.WeaponInspect: dur = 0.34f; break;
            case FoleyEventType.ClimbScrape: dur = 0.32f; break;
            case FoleyEventType.SwimStroke: dur = 0.34f; break;
            case FoleyEventType.PainGrunt: dur = 0.28f; break;
            case FoleyEventType.ChestWoodLatch: dur = 0.28f; break;
            case FoleyEventType.ChopWood: dur = 0.24f; break;
            case FoleyEventType.SwingHeavy: dur = 0.24f; break;
            case FoleyEventType.EquipWeapon: dur = 0.22f; break;
            case FoleyEventType.TrapMetalSnap: dur = 0.22f; break;
            case FoleyEventType.SplintSnap: dur = 0.26f; break;
            case FoleyEventType.ShieldBlock: dur = 0.22f; break;
            case FoleyEventType.StrikeRock: dur = 0.20f; break;
            case FoleyEventType.LighterFlick: dur = 0.19f; break;
            case FoleyEventType.HitShadow: dur = 0.24f; break;
            case FoleyEventType.HitFlesh: dur = 0.16f; break;
            case FoleyEventType.SwingLight: dur = 0.16f; break;
            case FoleyEventType.ArrowImpact: dur = 0.18f; break;
            default: dur = 0.15f; break;
        }

        int len = Mathf.Max(512, Mathf.RoundToInt(sr * dur));
        float[] samples = new float[len];
        uint seed = (uint)(7919 + (int)kind * 4999);
        float lp1 = 0f;
        float lp2 = 0f;
        float bpPrev = 0f;

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float sec = (float)i / sr;
            float w = NextSignedNoise(ref seed);
            lp1 = Mathf.Lerp(lp1, w, 0.16f);
            lp2 = Mathf.Lerp(lp2, lp1, 0.06f);
            float bandMid = lp1 - lp2;
            float high = w - lp1;
            bpPrev = Mathf.Lerp(bpPrev, bandMid, 0.25f);

            float s = 0f;
            switch (kind)
            {
                case FoleyEventType.PickupWood:
                {
                    float env = Mathf.Exp(-19f * t);
                    float hollow = Mathf.Sin(2f * Mathf.PI * 210f * sec) * 0.55f + Mathf.Sin(2f * Mathf.PI * 430f * sec) * 0.28f;
                    s = (hollow + bandMid * 0.45f) * env;
                    break;
                }
                case FoleyEventType.PickupStone:
                {
                    float env = Mathf.Exp(-26f * t);
                    float clink = Mathf.Sin(2f * Mathf.PI * 960f * sec) * 0.48f + Mathf.Sin(2f * Mathf.PI * 1720f * sec) * 0.26f;
                    s = (clink + high * 0.22f) * env;
                    break;
                }
                case FoleyEventType.PickupHerb:
                {
                    float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-4.5f * t);
                    float rustle = bandMid * 0.85f + high * 0.45f * (0.6f + 0.4f * Mathf.Sin(t * 42f));
                    s = rustle * env;
                    break;
                }
                case FoleyEventType.PickupWater:
                {
                    float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-5.2f * t);
                    float freq = Mathf.Lerp(260f, 640f, t);
                    float bubble = Mathf.Sin(2f * Mathf.PI * freq * sec) * 0.45f;
                    s = (bubble + bandMid * 0.42f) * env;
                    break;
                }
                case FoleyEventType.EatCrunch:
                {
                    float pulseA = Mathf.Exp(-34f * Mathf.Abs(t - 0.16f));
                    float pulseB = Mathf.Exp(-34f * Mathf.Abs(t - 0.52f));
                    float pulseC = Mathf.Exp(-40f * Mathf.Abs(t - 0.80f));
                    float crisp = bandMid * 0.72f + high * 0.65f;
                    float jaw = Mathf.Sin(2f * Mathf.PI * 125f * sec) * 0.22f;
                    s = (crisp + jaw) * (pulseA + pulseB * 0.85f + pulseC * 0.65f);
                    break;
                }
                case FoleyEventType.DrinkGulp:
                {
                    float g1 = Mathf.Exp(-28f * Mathf.Abs(t - 0.22f));
                    float g2 = Mathf.Exp(-28f * Mathf.Abs(t - 0.58f));
                    float sweep = Mathf.Lerp(190f, 440f, (t * 2.2f) % 1f);
                    float liquid = Mathf.Sin(2f * Mathf.PI * sweep * sec) * 0.62f + lp2 * 0.35f;
                    s = liquid * (g1 + g2 * 0.88f);
                    break;
                }
                case FoleyEventType.BandageWrap:
                {
                    float wrap1 = Mathf.Exp(-14f * Mathf.Abs(t - 0.25f));
                    float wrap2 = Mathf.Exp(-14f * Mathf.Abs(t - 0.70f));
                    float cloth = bandMid * 0.85f + high * 0.35f;
                    s = cloth * (wrap1 + wrap2 * 0.90f);
                    break;
                }
                case FoleyEventType.SplintSnap:
                {
                    float env = Mathf.Exp(-20f * t);
                    float woodTie = Mathf.Sin(2f * Mathf.PI * 240f * sec) * 0.52f + bandMid * 0.65f;
                    float click2 = Mathf.Exp(-36f * Mathf.Abs(t - 0.58f)) * (Mathf.Sin(2f * Mathf.PI * 520f * sec) * 0.45f + high * 0.35f);
                    s = woodTie * env + click2;
                    break;
                }
                case FoleyEventType.LighterFlick:
                {
                    float flint = Mathf.Exp(-45f * t) * (high * 0.85f + Mathf.Sin(2f * Mathf.PI * 2450f * sec) * 0.45f);
                    float whoosh = Mathf.Exp(-18f * Mathf.Abs(t - 0.45f)) * (bandMid * 0.65f + lp1 * 0.35f);
                    s = flint + whoosh;
                    break;
                }
                case FoleyEventType.FireIgnite:
                {
                    float attack = Mathf.Clamp01(t * 7f);
                    float env = attack * Mathf.Exp(-5.5f * t);
                    float roar = lp1 * 0.72f + bandMid * 0.48f;
                    float crackle = (seed % 97 == 0) ? high * 0.9f : 0f;
                    s = (roar + crackle) * env;
                    break;
                }
                case FoleyEventType.FireCrackleLoop:
                {
                    float warmth = lp2 * 0.45f + lp1 * 0.22f;
                    bool popGate = (seed % 211 == 0) || (seed % 389 == 0);
                    float pop = popGate ? (high * 0.85f + Mathf.Sin(2f * Mathf.PI * 980f * sec) * 0.32f) : 0f;
                    s = warmth + pop * 0.55f;
                    break;
                }
                case FoleyEventType.CookingSizzle:
                {
                    float env = Mathf.Sin(t * Mathf.PI);
                    float fizz = high * 0.65f + bandMid * 0.42f;
                    float pop = (seed % 83 == 0) ? 0.55f : 0f;
                    s = (fizz + pop) * env * 0.78f;
                    break;
                }
                case FoleyEventType.EquipWeapon:
                {
                    float env = Mathf.Exp(-14f * t);
                    float sheath = bandMid * 0.72f + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 520f, t) * sec) * 0.24f;
                    s = sheath * env;
                    break;
                }
                case FoleyEventType.WeaponInspect:
                {
                    float env = Mathf.Sin(t * Mathf.PI) * 0.65f;
                    float shine = high * 0.28f + bandMid * 0.52f + Mathf.Sin(2f * Mathf.PI * 1240f * sec) * Mathf.Exp(-18f * t) * 0.16f;
                    s = shine * env;
                    break;
                }
                case FoleyEventType.DoorWoodCreak:
                {
                    float env = Mathf.Sin(t * Mathf.PI);
                    float squeakFreq = Mathf.Lerp(210f, 340f, Mathf.Sin(t * Mathf.PI * 2.5f) * 0.5f + 0.5f);
                    float creak = Mathf.Sin(2f * Mathf.PI * squeakFreq * sec) * 0.38f
                                + Mathf.Sin(2f * Mathf.PI * squeakFreq * 2.02f * sec) * 0.18f;
                    float thud = Mathf.Exp(-30f * Mathf.Abs(t - 0.86f)) * Mathf.Sin(2f * Mathf.PI * 95f * sec) * 0.65f;
                    s = (creak + bandMid * 0.25f) * env + thud;
                    break;
                }
                case FoleyEventType.ChestWoodLatch:
                {
                    float click = Mathf.Exp(-38f * t) * (Mathf.Sin(2f * Mathf.PI * 1350f * sec) * 0.45f + high * 0.4f);
                    float lid = Mathf.Exp(-14f * Mathf.Max(0f, t - 0.22f)) * (Mathf.Sin(2f * Mathf.PI * 165f * sec) * 0.55f + bandMid * 0.38f);
                    s = click + lid;
                    break;
                }
                case FoleyEventType.TrapMetalSnap:
                {
                    float env = Mathf.Exp(-24f * t);
                    float spring = Mathf.Sin(2f * Mathf.PI * 1420f * sec) * 0.55f + Mathf.Sin(2f * Mathf.PI * 2680f * sec) * 0.32f;
                    float jaw = Mathf.Sin(2f * Mathf.PI * 185f * sec) * Mathf.Exp(-18f * t) * 0.50f;
                    s = (spring + jaw + high * 0.35f) * env;
                    break;
                }
                case FoleyEventType.ChopWood:
                {
                    float env = Mathf.Exp(-16f * t);
                    float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(145f, 76f, t) * sec) * 0.68f;
                    float crack = (bandMid * 0.72f + high * 0.32f) * Mathf.Exp(-22f * t);
                    s = (thud + crack) * env;
                    break;
                }
                case FoleyEventType.TreeFall:
                {
                    float creakPhase = t < 0.38f ? Mathf.Sin((t / 0.38f) * Mathf.PI) : 0f;
                    float creak = (Mathf.Sin(2f * Mathf.PI * 140f * sec) * 0.38f + bandMid * 0.45f) * creakPhase;
                    float crashPhase = t >= 0.36f ? Mathf.Exp(-4.2f * (t - 0.36f)) : 0f;
                    float crash = (Mathf.Sin(2f * Mathf.PI * 52f * sec) * 0.65f + lp1 * 0.78f + high * 0.22f) * crashPhase;
                    s = creak + crash;
                    break;
                }
                case FoleyEventType.StrikeRock:
                {
                    float env = Mathf.Exp(-22f * t);
                    float ring = Mathf.Sin(2f * Mathf.PI * 1080f * sec) * 0.42f + Mathf.Sin(2f * Mathf.PI * 2140f * sec) * 0.24f;
                    float impact = Mathf.Sin(2f * Mathf.PI * 180f * sec) * 0.38f + bandMid * 0.45f;
                    s = (ring + impact) * env;
                    break;
                }
                case FoleyEventType.HitFlesh:
                {
                    float env = Mathf.Exp(-17f * t);
                    float meat = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(155f, 68f, t) * sec) * 0.72f + lp1 * 0.46f;
                    s = meat * env;
                    break;
                }
                case FoleyEventType.HitShadow:
                {
                    float env = Mathf.Exp(-10f * t);
                    float dark = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 110f, t) * sec) * 0.55f + bandMid * 0.58f;
                    s = dark * env;
                    break;
                }
                case FoleyEventType.SwingLight:
                {
                    float env = Mathf.Sin(t * Mathf.PI);
                    float airMod = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI);
                    s = (bandMid * 0.62f + high * 0.28f * airMod) * env * 0.72f;
                    break;
                }
                case FoleyEventType.SwingHeavy:
                {
                    float env = Mathf.Sin(t * Mathf.PI);
                    s = (lp1 * 0.58f + bandMid * 0.65f) * env * 0.85f;
                    break;
                }
                case FoleyEventType.ShieldBlock:
                {
                    float env = Mathf.Exp(-18f * t);
                    float woodRing = Mathf.Sin(2f * Mathf.PI * 280f * sec) * 0.58f + Mathf.Sin(2f * Mathf.PI * 840f * sec) * 0.35f;
                    s = (woodRing + bandMid * 0.35f) * env;
                    break;
                }
                case FoleyEventType.WaterSplashIn:
                case FoleyEventType.SwimStroke:
                {
                    float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-3.8f * t);
                    float bubble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(280f, 110f, t) * sec) * 0.28f;
                    s = (bandMid * 0.78f + high * 0.35f + bubble) * env;
                    break;
                }
                case FoleyEventType.ClimbScrape:
                {
                    float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-4.5f * t);
                    s = (bandMid * 0.72f + lp2 * 0.42f) * env;
                    break;
                }
                case FoleyEventType.PainGrunt:
                {
                    float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-6f * t);
                    float voiceFreq = Mathf.Lerp(155f, 92f, t);
                    float vocal = Mathf.Sin(2f * Mathf.PI * voiceFreq * sec) * 0.52f + Mathf.Sin(2f * Mathf.PI * voiceFreq * 2f * sec) * 0.24f;
                    s = (vocal + lp1 * 0.28f) * env;
                    break;
                }
                case FoleyEventType.ArrowImpact:
                {
                    float env = Mathf.Exp(-20f * t);
                    float thwack = Mathf.Sin(2f * Mathf.PI * 220f * sec) * 0.65f + bandMid * 0.48f;
                    s = thwack * env;
                    break;
                }
                default:
                {
                    float env = Mathf.Exp(-20f * t);
                    float pop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(280f, 520f, t) * sec) * 0.42f + bandMid * 0.32f;
                    s = pop * env;
                    break;
                }
            }

            samples[i] = Mathf.Clamp(s * 0.75f, -0.96f, 0.96f);
        }

        AudioClip clip = AudioClip.Create($"Foley_{kind}_DSP", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // =================== SINTESE PROCEDURAL DE ATMOSFERA ===================

    private AudioClip GenerateDayAmbient()
    {
        int sr = 22050;
        int len = (int)(sr * 2.5f);
        float[] samples = new float[len];
        float lp = 0f;
        uint seed = 104729u;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float env = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.4f * t);
            float white = NextSignedNoise(ref seed);
            lp = Mathf.Lerp(lp, white, 0.035f);
            float chirpEnv = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 0.8f * t)), 28f);
            float chirp = Mathf.Sin(2f * Mathf.PI * (2400f + 500f * Mathf.Sin(t * 18f)) * t) * chirpEnv * 0.06f;
            samples[i] = Mathf.Clamp(lp * env * 0.26f + chirp, -1f, 1f);
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
        uint seed = 224737u;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float wind = NextSignedNoise(ref seed);
            lp = Mathf.Lerp(lp, wind, 0.024f);
            float drone = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.14f + Mathf.Sin(2f * Mathf.PI * 78f * t) * 0.08f;
            float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 1.6f * t)), 8f) * Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.05f;
            samples[i] = Mathf.Clamp(lp * 0.24f + drone + pulse, -1f, 1f);
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
        uint seed = 350377u;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float mod = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2.5f * t);
            lp = Mathf.Lerp(lp, NextSignedNoise(ref seed), 0.12f);
            samples[i] = lp * mod * 0.34f;
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
        uint seed = 479909u;
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float sec = (float)i / sr;
            float freq = Mathf.Lerp(640f, 220f, t);
            float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-2.2f * t);
            lp = Mathf.Lerp(lp, NextSignedNoise(ref seed), 0.22f);
            float rasp = Mathf.Sin(2f * Mathf.PI * freq * sec) * 0.55f + lp * 0.45f;
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

    private AudioClip GenerateRainAmbientClip()
    {
        int sr = 22050;
        int len = sr * 2;
        float[] samples = new float[len];
        float lp = 0f;
        float hp = 0f;
        uint seed = 611953u;
        for (int i = 0; i < len; i++)
        {
            float w = NextSignedNoise(ref seed);
            lp = Mathf.Lerp(lp, w, 0.26f);
            hp = lp - Mathf.Lerp(hp, lp, 0.06f);
            float droplet = (i % 731 == 0) ? 0.16f : 0f;
            samples[i] = Mathf.Clamp(hp * 0.28f + droplet, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("GalicianRainLoop_Proc", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
