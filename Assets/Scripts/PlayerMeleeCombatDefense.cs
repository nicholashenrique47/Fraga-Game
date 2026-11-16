using UnityEngine;

/// <summary>
/// PlayerMeleeCombatDefense: Bloqueio tatico, Aparo Perfeito (Parry), sinergia com Escudo (309) e Hitstop seguro via GameFeelManager.
/// Entrada controlada centralmente pelo EquipmentSystem / FragaInputRouter (sem conflito com KeyCode.Q).
/// </summary>
public class PlayerMeleeCombatDefense : MonoBehaviour
{
    public static PlayerMeleeCombatDefense Instance { get; private set; }

    [Header("Defense Settings")]
    public float blockDamageReduction = 0.70f;
    public float shieldDamageReduction = 0.85f;
    public float blockStaminaCost = 18f;
    public float parryWindow = 0.18f;
    public float shieldParryWindow = 0.25f;
    public float counterMultiplier = 2.50f;
    public float parryBuffDuration = 1.8f;

    [Header("Dependencies")]
    public PlayerStatus playerStatus;

    public bool isBlocking { get; private set; }
    public bool hasParryBonus { get; private set; }

    private float blockStartTime = -10f;
    private float parryBonusTimer = 0f;
    private AudioSource audioSource;
    private AudioClip blockClip;
    private AudioClip parryClip;
    private HapticManager hapticCache;
    private InventoryAdvanced inventoryCache;

    private void Awake()
    {
        Instance = this;
        SetupAudio();
    }

    private void Start()
    {
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? GetComponent<PlayerStatus>() ?? FindAnyObjectByType<PlayerStatus>();
        hapticCache = FindAnyObjectByType<HapticManager>();
        inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
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
        if (playerStatus != null && playerStatus.stamina < 8f) return;

        isBlocking = true;
        blockStartTime = Time.time;

        if (FirstPersonViewmodelController.Instance != null)
        {
            FirstPersonViewmodelController.Instance.SetGuardPose(true);
        }
    }

    public void StopBlock()
    {
        if (!isBlocking) return;
        isBlocking = false;
        if (FirstPersonViewmodelController.Instance != null)
        {
            FirstPersonViewmodelController.Instance.SetGuardPose(false);
        }
    }

    public void ConsumeParryBonus()
    {
        hasParryBonus = false;
        parryBonusTimer = 0f;
    }

    public float ProcessIncomingDamage(float rawDamage)
    {
        if (!isBlocking) return rawDamage;

        if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;
        bool hasShield = inventoryCache != null && inventoryCache.equippedOffHandId == 309;
        float effectiveParryWindow = hasShield ? shieldParryWindow : parryWindow;
        float effectiveReduction = hasShield ? shieldDamageReduction : blockDamageReduction;

        float timeSinceBlock = Time.time - blockStartTime;

        // 1. PARRY PERFEITO
        if (timeSinceBlock <= effectiveParryWindow)
        {
            hasParryBonus = true;
            parryBonusTimer = parryBuffDuration;

            if (FirstPersonViewmodelController.Instance != null)
                FirstPersonViewmodelController.Instance.TriggerParrySnap();

            if (audioSource != null && parryClip != null)
            {
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(parryClip, 1.0f);
            }

            if (FirstPersonCamera.Instance != null)
                FirstPersonCamera.Instance.AddShake(0.14f);

            if (hapticCache != null) hapticCache.TriggerLightVibration();

            if (GameFeelManager.Instance != null)
                GameFeelManager.Instance.TriggerHitstop(0.055f, 0.06f);

            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFDD>APARO PERFEITO (PARRY)! Contra-ataque 2.5x</color>");

            return 0f;
        }

        // 2. BLOQUEIO NORMAL
        float staminaDrain = hasShield ? blockStaminaCost * 0.65f : blockStaminaCost;
        if (playerStatus != null)
        {
            playerStatus.stamina = Mathf.Max(0f, playerStatus.stamina - staminaDrain);
            if (playerStatus.stamina <= 0f)
            {
                StopBlock();
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF5555>Guarda Quebrada (Sem Estamina)!</color>");
            }
        }

        if (audioSource != null && blockClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(blockClip, 0.8f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(0.08f);

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.TriggerBlockImpact();

        return rawDamage * (1f - effectiveReduction);
    }

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
