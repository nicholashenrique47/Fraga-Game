using UnityEngine;

/// <summary>
/// SanityHallucinationDriver: Motor dinâmico de Alucinações Auditivas, Visuais e Pânico Psicológico.
/// </summary>
public class SanityHallucinationDriver : MonoBehaviour
{
    public static SanityHallucinationDriver Instance { get; private set; }

    [Header("Dependencies")]
    public SanitySystem sanitySystem;
    public Camera playerCamera;
    public SantaCompanaSystem santaCompanaSystem;

    private float nextHallucinationTime = 0f;
    private AudioSource audioSource;
    private AudioClip twigSnapClip;
    private AudioClip whisperClip;

    private float baseFOV = 68f;
    private float panicFovPulse = 0f;

    private void Awake()
    {
        Instance = this;
        SetupAudio();
    }

    private void Start()
    {
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (santaCompanaSystem == null) santaCompanaSystem = FindAnyObjectByType<SantaCompanaSystem>();

        if (playerCamera != null) baseFOV = playerCamera.fieldOfView;
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.75f;
        audioSource.volume = 0.75f;

        twigSnapClip = GenerateTwigSnapSound();
        whisperClip = GenerateWhisperSound();
    }

    private void Update()
    {
        if (sanitySystem == null) return;
        float sanity = sanitySystem.sanity;

        // Se estiver protegido pelo Círculo de Sal, alivia o estresse mental
        if (IsInsideSaltCircle())
        {
            sanitySystem.sanity = Mathf.Min(100f, sanitySystem.sanity + Time.deltaTime * 2.5f);
            if (playerCamera != null) playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, baseFOV, Time.deltaTime * 4f);
            return;
        }

        // === 1. TIER 1 (< 60% Sanidade): Estalos falsos de galhos nas costas ===
        if (sanity < 60f && Time.time > nextHallucinationTime)
        {
            PlayAuditoryHallucination();
            float delay = Mathf.Lerp(6f, 22f, sanity / 60f);
            nextHallucinationTime = Time.time + delay;
        }

        // === 2. TIER 2 (< 35% Sanidade): Distorção de FOV e Pulsação de Pânico ===
        if (sanity < 35f && playerCamera != null)
        {
            panicFovPulse += Time.deltaTime * 4.5f;
            float targetFOV = baseFOV + Mathf.Sin(panicFovPulse) * 7.5f;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * 5f);
        }
        else if (playerCamera != null)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, baseFOV, Time.deltaTime * 3f);
        }

        // === 3. TIER 3 (< 15% Sanidade à noite): Chance de invocar a Santa Compaña ===
        if (sanity < 15f && DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight())
        {
            if (santaCompanaSystem != null && Random.value < 0.001f) // Chance por frame
            {
                Vector3 startPos = transform.position + transform.forward * 25f + transform.right * 10f;
                Vector3 endPos = transform.position - transform.forward * 25f - transform.right * 10f;
                santaCompanaSystem.TriggerEvent(startPos, endPos);
            }
        }
    }

    private void PlayAuditoryHallucination()
    {
        if (audioSource == null) return;

        // Spawna o som em uma posição fantasma atrás ou dos lados do jogador
        Vector3 phantomPos = transform.position - transform.forward * Random.Range(3f, 8f) + transform.right * Random.Range(-5f, 5f);
        audioSource.transform.position = phantomPos;

        if (Random.value > 0.5f && twigSnapClip != null)
        {
            audioSource.pitch = Random.Range(0.85f, 1.15f);
            audioSource.PlayOneShot(twigSnapClip, 0.8f);
        }
        else if (whisperClip != null)
        {
            audioSource.pitch = Random.Range(0.75f, 0.95f);
            audioSource.PlayOneShot(whisperClip, 0.65f);
        }
    }

    private bool IsInsideSaltCircle()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 2.5f);
        foreach (var c in hits)
        {
            string n = c.gameObject.name.ToLower();
            if (n.Contains("salt") || n.Contains("sal") || n.Contains("402"))
            {
                return true;
            }
        }
        return false;
    }

    // =================== SÍNTESE PROCEDURAL DE ÁUDIO ===================

    private AudioClip GenerateTwigSnapSound()
    {
        int sampleRate = 44100;
        float dur = 0.16f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float crack = Random.Range(-0.8f, 0.8f) * Mathf.Exp(-28f * t);
            samples[i] = crack;
        }

        AudioClip clip = AudioClip.Create("TwigSnap_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateWhisperSound()
    {
        int sampleRate = 44100;
        float dur = 0.85f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float noise = Random.Range(-0.4f, 0.4f) * Mathf.Sin(t * Mathf.PI);
            float formant = Mathf.Sin(2f * Mathf.PI * 450f * ((float)i / sampleRate)) * 0.25f;
            samples[i] = (noise + formant) * Mathf.Sin(t * Mathf.PI);
        }

        AudioClip clip = AudioClip.Create("Whisper_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}