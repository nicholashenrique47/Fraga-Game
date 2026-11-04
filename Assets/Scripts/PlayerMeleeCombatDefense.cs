using UnityEngine;

/// <summary>
/// PlayerMeleeCombatDefense: Sistema tático de Bloqueio, Aparo Perfeito (Parry) e Hitstop cinematográfico.
/// </summary>
public class PlayerMeleeCombatDefense : MonoBehaviour
{
    public static PlayerMeleeCombatDefense Instance { get; private set; }

    [Header("Defense Settings")]
    public float blockDamageReduction = 0.70f;
    public float blockStaminaCost = 18f;
    public float parryWindow = 0.18f;
    public float counterMultiplier = 2.50f;
    public float parryBuffDuration = 1.6f;

    [Header("Dependencies")]
    public PlayerStatus playerStatus;
    public WeaponMotionSway weaponSway;

    public bool isBlocking { get; private set; }
    public bool hasParryBonus { get; private set; }

    private float blockStartTime = -10f;
    private float parryBonusTimer = 0f;
    private AudioSource audioSource;
    private AudioClip blockClip;
    private AudioClip parryClip;

    private void Awake()
    {
        Instance = this;
        SetupAudio();
    }

    private void Start()
    {
        if (playerStatus == null) playerStatus = GetComponent<PlayerStatus>();
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();
        if (weaponSway == null) weaponSway = FindAnyObjectByType<WeaponMotionSway>();
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.2f;
        audioSource.volume = 0.85f;

        blockClip = GenerateBlockSound();
        parryClip = GenerateParrySound();
    }

    private void Update()
    {
        // Entrada PC: Botão Direito do Mouse mantém a guarda levantada
        // Se estiver com arco em mãos, o arco usa o botão direito, caso contrário entra em guarda
        BowAndArrowSystem bow = BowAndArrowSystem.Instance != null ? BowAndArrowSystem.Instance : FindAnyObjectByType<BowAndArrowSystem>();
        bool hasBowAim = bow != null && Input.GetMouseButton(1);

        if (!hasBowAim)
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q))
            {
                StartBlock();
            }
            else if (Input.GetMouseButtonUp(1) || Input.GetKeyUp(KeyCode.Q))
            {
                StopBlock();
            }
        }

        if (hasParryBonus)
        {
            parryBonusTimer -= Time.deltaTime;
            if (parryBonusTimer <= 0f)
            {
                hasParryBonus = false;
            }
        }
    }

    public void StartBlock()
    {
        if (isBlocking) return;
        if (playerStatus != null && playerStatus.stamina < 10f) return;

        isBlocking = true;
        blockStartTime = Time.time;

        if (weaponSway != null)
        {
            weaponSway.TriggerAttackMotion(); // Postura defensiva elevada
        }
    }

    public void StopBlock()
    {
        isBlocking = false;
    }

    public void ConsumeParryBonus()
    {
        hasParryBonus = false;
        parryBonusTimer = 0f;
    }

    /// <summary>
    /// Processa o dano recebido verificando se houve bloqueio ou Parry perfeito.
    /// Retorna o valor de dano final reduzido ou 0 se aparado.
    /// </summary>
    public float ProcessIncomingDamage(float rawDamage)
    {
        if (!isBlocking) return rawDamage;

        float timeSinceBlock = Time.time - blockStartTime;

        // === 1. PARRY PERFEITO (Dentro da janela de 0.18s) ===
        if (timeSinceBlock <= parryWindow)
        {
            hasParryBonus = true;
            parryBonusTimer = parryBuffDuration;

            if (audioSource != null && parryClip != null)
            {
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(parryClip, 1.0f);
            }

            if (FirstPersonCamera.Instance != null)
            {
                FirstPersonCamera.Instance.AddShake(0.14f);
            }

            HapticManager haptic = FindAnyObjectByType<HapticManager>();
            if (haptic != null) haptic.TriggerLightVibration();

            // Congelamento temporal cinematográfico (Hitstop de 0.04s)
            StartCoroutine(PerformHitstop(0.04f));

            HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
            if (hud != null) hud.ShowLootTooltip("<color=#00FFCC>★ APARO PERFEITO (PARRY)! Contra-ataque 2.5x</color>");

            return 0f; // Dano zero
        }

        // === 2. BLOQUEIO NORMAL (Redução de 70%) ===
        if (playerStatus != null)
        {
            playerStatus.stamina = Mathf.Max(0, playerStatus.stamina - blockStaminaCost);
            if (playerStatus.stamina <= 0f)
            {
                // Quebra de guarda por falta de estamina
                StopBlock();
                HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
                if (hud != null) hud.ShowLootTooltip("<color=#FF4444>Guarda Quebrada (Sem Estamina)!</color>");
            }
        }

        if (audioSource != null && blockClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(blockClip, 0.8f);
        }

        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.08f);
        }

        return rawDamage * (1f - blockDamageReduction);
    }

    private System.Collections.IEnumerator PerformHitstop(float duration)
    {
        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = prevTimeScale;
    }

    // =================== SÍNTESE PROCEDURAL DE ÁUDIO ===================

    private AudioClip GenerateBlockSound()
    {
        int sampleRate = 44100;
        float dur = 0.22f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float thud = Mathf.Sin(2f * Mathf.PI * 120f * ((float)i / sampleRate)) * Mathf.Exp(-16f * t) * 0.7f;
            float scrape = Random.Range(-0.3f, 0.3f) * Mathf.Exp(-10f * t);
            samples[i] = thud + scrape;
        }

        AudioClip clip = AudioClip.Create("BlockImpact_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateParrySound()
    {
        int sampleRate = 44100;
        float dur = 0.35f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            // Choque agudo e ressonância metálica
            float ring1 = Mathf.Sin(2f * Mathf.PI * 880f * ((float)i / sampleRate)) * Mathf.Exp(-7f * t) * 0.5f;
            float ring2 = Mathf.Sin(2f * Mathf.PI * 1760f * ((float)i / sampleRate)) * Mathf.Exp(-12f * t) * 0.35f;
            float burst = Random.Range(-0.4f, 0.4f) * Mathf.Exp(-35f * t) * 0.4f;
            samples[i] = ring1 + ring2 + burst;
        }

        AudioClip clip = AudioClip.Create("ParryClink_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}