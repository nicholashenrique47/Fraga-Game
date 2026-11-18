using UnityEngine;

/// PlayerStatus: Sistema de status vitais do personagem (Stamina, Fome, Temperatura).
/// Aplica defesa de armadura equipada (Casaco de Pele / Armadura de Ossos / Escudo) e sincroniza corrida via PlayerController/FragaInputRouter.
public class PlayerStatus : MonoBehaviour
{
    public static PlayerStatus Instance { get; private set; }

    [Header("Stamina")]
    [Range(0f, 100f)] public float stamina = 100f;
    public float staminaDrainRate = 15f;
    public float staminaRegenRate = 8f;
    public float sprintSpeedMultiplier = 1.6f;

    [Header("Fome e Sede")]
    [Range(0f, 100f)] public float hunger = 100f;
    [Range(0f, 100f)] public float thirst = 100f;
    public float hungerDrainRate = 0.065f;
    public float thirstDrainRate = 0.095f;
    public float starvationDamage = 2f;
    public float dehydrationDamage = 3f;
    public float starvationHealthDamage = 0.45f;
    public float dehydrationHealthDamage = 0.65f;
    public float bleedingHealthDamage = 0.55f;

    [Header("Vida Fisica")]
    [Range(0f, 100f)] public float health = 100f;
    public event System.Action OnDeath;
    private bool isDead = false;
    public bool IsDead => isDead;
    public bool IsComfortRegenActive { get; private set; }

    [Header("Temperatura")]
    [Range(-20f, 40f)] public float bodyTemperature = 37f;
    public float ambientDayTemp = 18f;
    public float ambientNightTemp = 2f;
    public float bonfireWarmth = 15f;
    public float coldThreshold = 35f;
    public float coldSanityDamageRate = 1.5f;
    public float coldHealthDamageRate = 0.5f;

    [Header("Status Effects")]
    public bool isSprinting = false;
    public bool isExhausted = false;
    public bool isNearBonfire = false;
    public bool isStarving = false;
    public bool isFreezing = false;
    public bool isPoisoned = false;
    public float poisonTimer = 0f;
    public float poisonDamageRate = 3f;
    public bool isBleeding = false;
    public bool isFractured = false;
    public bool isSick = false;
    public float sicknessTimer = 0f;

    [Header("Dependencies")]
    public PlayerController playerController;
    public SanitySystem sanitySystem;
    public DayNightCycle dayNightCycle;

    private float basePlayerSpeed = 5f;
    private float lastBreathingTime = 0f;
    private float lastHorrorCueTime = -15f;
    private float nextStatusAlertTime = 0f;
    private bool alertedLowHunger = false;
    private bool alertedLowThirst = false;
    private bool alertedStarving = false;
    private bool alertedDehydrated = false;
    private bool wasBleeding = false;
    private bool wasPoisoned = false;
    private bool wasFractured = false;
    private bool wasSick = false;
    private InventoryAdvanced inventoryCache;
    private PostProcessingManager ppManagerCache;
    private HapticManager hapticCache;
    private GameManager gameManagerCache;
    private AudioManager audioManagerCache;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        health = 100f;
        stamina = 100f;
        hunger = 100f;
        thirst = 100f;
        bodyTemperature = 37.0f;
        isDead = false;
        IsComfortRegenActive = false;
        isExhausted = false;
        isStarving = false;
        isFreezing = false;
        isPoisoned = false;
        poisonTimer = 0f;
        isBleeding = false;
        isFractured = false;
        isSick = false;
        sicknessTimer = 0f;
        alertedLowHunger = false;
        alertedLowThirst = false;
        alertedStarving = false;
        alertedDehydrated = false;
        wasBleeding = false;
        wasPoisoned = false;
        wasFractured = false;
        wasSick = false;
    }

    private void Start()
    {
        inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        ppManagerCache = FindAnyObjectByType<PostProcessingManager>();
        hapticCache = FindAnyObjectByType<HapticManager>();
        gameManagerCache = FindAnyObjectByType<GameManager>();
        audioManagerCache = AudioManager.Instance ?? FindAnyObjectByType<AudioManager>();
        if (playerController == null) playerController = GetComponent<PlayerController>() ?? FindAnyObjectByType<PlayerController>();
        if (sanitySystem == null) sanitySystem = GetComponent<SanitySystem>() ?? FindAnyObjectByType<SanitySystem>();
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        if (playerController != null)
        {
            basePlayerSpeed = playerController.moveSpeed > 0.1f ? playerController.moveSpeed : 5f;
        }
    }

    private void Update()
    {
        if (isDead) return;

        if (stamina < 25f && Time.time - lastBreathingTime > 5f)
        {
            lastBreathingTime = Time.time;
            if (FirstPersonCamera.Instance != null)
                FirstPersonCamera.Instance.AddShake(0.022f);
        }

        if (sanitySystem != null && sanitySystem.sanity < 30f && Time.time - lastHorrorCueTime > 14f)
        {
            lastHorrorCueTime = Time.time;
            if (audioManagerCache != null) audioManagerCache.PlayHorrorSting();
            if (FirstPersonCamera.Instance != null)
                FirstPersonCamera.Instance.AddShake(0.03f);
        }

        UpdateHungerAndThirst();
        UpdateTemperature();
        UpdatePoison();
        UpdateSickness();
        ApplyStatusEffects();
        UpdateStamina();
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        // 1. Bloqueio / Parry
        if (PlayerMeleeCombatDefense.Instance != null)
        {
            amount = PlayerMeleeCombatDefense.Instance.ProcessIncomingDamage(amount);
            if (amount <= 0f) return;
        }

        // 2. Reducao de Armadura (Casaco de Pele / Armadura de Ossos + Escudo passivo)
        if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;
        if (inventoryCache != null)
        {
            float armorDef = inventoryCache.GetTotalArmorDefense();
            amount *= (1f - armorDef);
        }

        health -= amount;
        if (amount > 0.5f && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PainGrunt, transform.position, Mathf.Clamp(0.45f + amount * 0.01f, 0.45f, 0.88f), 0.90f, 1.08f);
        }

        if (ppManagerCache != null)
        {
            ppManagerCache.TriggerDamageFlash();
        }

        if (hapticCache != null)
        {
            hapticCache.TriggerHeavyVibration();
        }

        if (health <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        health = 0f;
        IsComfortRegenActive = false;
        OnDeath?.Invoke();

        if (ppManagerCache != null)
        {
            ppManagerCache.StartDeathSequence();
        }
        else if (gameManagerCache != null)
        {
            gameManagerCache.TriggerGameOver();
        }
    }

    public void Revive()
    {
        isDead = false;
        IsComfortRegenActive = false;
        isExhausted = false;
        health = 100f;
        stamina = 100f;
        hunger = 100f;
        thirst = 100f;
        bodyTemperature = 37.0f;
        isStarving = false;
        isFreezing = false;
        isPoisoned = false;
        isBleeding = false;
        isFractured = false;
        isSick = false;
        sicknessTimer = 0f;
        alertedLowHunger = false;
        alertedLowThirst = false;
        alertedStarving = false;
        alertedDehydrated = false;
        wasBleeding = false;
        wasPoisoned = false;
        wasFractured = false;
        wasSick = false;

        if (BodyTemperatureSystem.Instance != null)
        {
            BodyTemperatureSystem.Instance.ResetTemperature();
        }
    }

    private void UpdateHungerAndThirst()
    {
        bool isSwimming = playerController != null && playerController.isSwimming;
        float activityMult = isSprinting ? 1.45f : (isSwimming ? 1.35f : 1.0f);
        hunger -= hungerDrainRate * activityMult * Time.deltaTime;
        thirst -= thirstDrainRate * activityMult * Time.deltaTime;

        hunger = Mathf.Clamp(hunger, 0f, 100f);
        thirst = Mathf.Clamp(thirst, 0f, 100f);

        isStarving = hunger <= 0f;
        bool isDehydrated = thirst <= 0f;

        if (sanitySystem != null)
        {
            if (isStarving) sanitySystem.sanity -= starvationDamage * Time.deltaTime;
            if (isDehydrated) sanitySystem.sanity -= dehydrationDamage * Time.deltaTime;
        }

        if (isStarving) TakeDamagePassive(starvationHealthDamage * Time.deltaTime);
        if (isDehydrated) TakeDamagePassive(dehydrationHealthDamage * Time.deltaTime);

        bool comfortReady = !isDead && hunger >= 75f && thirst >= 75f && bodyTemperature >= 36.2f
            && !isBleeding && !isPoisoned && !isSick && !isFreezing;
        IsComfortRegenActive = comfortReady && health < 100f;
        if (IsComfortRegenActive)
        {
            float regenHp = isNearBonfire ? 0.80f : 0.35f;
            health = Mathf.Min(100f, health + regenHp * Time.deltaTime);
        }

        if (hunger > 30f) alertedLowHunger = false;
        if (hunger > 5f) alertedStarving = false;
        if (thirst > 30f) alertedLowThirst = false;
        if (thirst > 5f) alertedDehydrated = false;

        if (Time.time >= nextStatusAlertTime && HUDSurvivalOverlay.Instance != null)
        {
            if (isStarving && !alertedStarving)
            {
                alertedStarving = true;
                nextStatusAlertTime = Time.time + 5f;
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF5544>INANICAO: Coma algo urgente ou perdera vida!</color>");
            }
            else if (isDehydrated && !alertedDehydrated)
            {
                alertedDehydrated = true;
                nextStatusAlertTime = Time.time + 5f;
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF5544>DESIDRATACAO: Beba agua ou bagas imediatamente!</color>");
            }
            else if (hunger <= 25f && !alertedLowHunger)
            {
                alertedLowHunger = true;
                nextStatusAlertTime = Time.time + 5f;
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FFBB44>Fome baixa! Cace, colha bagas ou cozinhe na fogueira.</color>");
            }
            else if (thirst <= 25f && !alertedLowThirst)
            {
                alertedLowThirst = true;
                nextStatusAlertTime = Time.time + 5f;
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#66CCFF>Sede baixa! Use o Cantil ou colete agua.</color>");
            }
        }
    }

    private void UpdateTemperature()
    {
        if (BodyTemperatureSystem.Instance != null)
        {
            bodyTemperature = BodyTemperatureSystem.Instance.currentTemperature;
            isFreezing = bodyTemperature <= BodyTemperatureSystem.TIER1_SHIVER_TEMP;
            return;
        }

        float targetTemp = ambientDayTemp;
        if (dayNightCycle != null)
        {
            bool isNight = dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f;
            targetTemp = isNight ? ambientNightTemp : ambientDayTemp;
        }

        float ins = inventoryCache != null ? inventoryCache.GetColdInsulation() : 0f;
        if (ins > 0f && targetTemp < 10f)
        {
            targetTemp = Mathf.Lerp(targetTemp, 12f, ins);
        }

        if (isNearBonfire)
        {
            targetTemp += bonfireWarmth;
        }

        bodyTemperature = Mathf.Lerp(bodyTemperature, targetTemp, 0.5f * Time.deltaTime);
        isFreezing = bodyTemperature < 5f;

        if (isFreezing)
        {
            if (sanitySystem != null) sanitySystem.sanity -= coldSanityDamageRate * Time.deltaTime;
            TakeDamagePassive(coldHealthDamageRate * Time.deltaTime);
        }
    }

    public void TakeDamagePassive(float amount)
    {
        if (isDead) return;
        health -= amount;
        if (health <= 0f) Die();
    }

    public float EffectiveMaxStamina
    {
        get
        {
            float max = 100f;
            if (bodyTemperature <= BodyTemperatureSystem.TIER2_FATIGUE_TEMP) max -= 40f;
            if (isSick) max -= 15f;
            if (thirst <= 10f) max -= 15f;
            return Mathf.Clamp(max, 45f, 100f);
        }
    }

    private void UpdateStamina()
    {
        float maxStam = EffectiveMaxStamina;
        // Sincronizado exclusivamente com PlayerController / FragaInputRouter (PC + Mobile)
        if (playerController != null)
        {
            isSprinting = playerController.isSprinting && playerController.IsMoving && !isExhausted && stamina > 0f;
        }

        if (isSprinting && stamina > 0f)
        {
            stamina -= staminaDrainRate * Time.deltaTime;
        }
        else
        {
            isSprinting = false;
            stamina += staminaRegenRate * Time.deltaTime;
        }

        stamina = Mathf.Clamp(stamina, 0f, maxStam);

        if (stamina <= 0.5f)
        {
            isExhausted = true;
        }
        else if (isExhausted && stamina >= 18f)
        {
            isExhausted = false;
        }

        if (stamina <= 0f || isExhausted)
        {
            isSprinting = false;
        }
    }

    public bool CanSpendStamina(float minRequired = 0.5f)
    {
        return !isExhausted && stamina >= minRequired;
    }

    public void ApplyBleeding()
    {
        isBleeding = true;
    }

    public void ApplyFracture()
    {
        isFractured = true;
    }

    private void UpdatePoison()
    {
        if (!isPoisoned) return;

        poisonTimer -= Time.deltaTime;
        if (poisonTimer <= 0f)
        {
            isPoisoned = false;
            poisonTimer = 0f;
            return;
        }

        if (sanitySystem != null)
        {
            sanitySystem.sanity -= poisonDamageRate * Time.deltaTime;
        }
    }

    public void ApplyPoison(float duration)
    {
        isPoisoned = true;
        poisonTimer = duration;
    }

    public void ApplySickness(float duration)
    {
        isSick = true;
        sicknessTimer = Mathf.Max(sicknessTimer, duration);
    }

    private void UpdateSickness()
    {
        if (!isSick) return;
        float recoverRate = isNearBonfire ? 2.5f : 1.0f;
        sicknessTimer -= Time.deltaTime * recoverRate;
        thirst = Mathf.Max(0f, thirst - 0.08f * Time.deltaTime);
        if (sicknessTimer <= 0f)
        {
            isSick = false;
            sicknessTimer = 0f;
        }
    }

    public void Eat(float hungerRestore, float sanityRestore)
    {
        if (isDead) return;
        hunger = Mathf.Clamp(hunger + hungerRestore, 0f, 100f);
        if (sanitySystem != null)
        {
            sanitySystem.sanity = Mathf.Clamp(sanitySystem.sanity + sanityRestore, 0f, 100f);
        }
    }

    public void Drink(float thirstRestore)
    {
        if (isDead) return;
        thirst = Mathf.Clamp(thirst + thirstRestore, 0f, 100f);
    }

    public void Heal(float healthRestore)
    {
        if (isDead) return;
        health = Mathf.Clamp(health + healthRestore, 0f, 100f);
    }

    public void RestoreStamina(float amount)
    {
        if (isDead) return;
        stamina = Mathf.Clamp(stamina + amount, 0f, EffectiveMaxStamina);
    }

    public void StartSprint()
    {
        if (!isExhausted && stamina > 10f) isSprinting = true;
    }

    public void StopSprint()
    {
        isSprinting = false;
    }

    private void ApplyStatusEffects()
    {
        staminaRegenRate = (hunger < 20f) ? 3f : 8f;
        if (isSick) staminaRegenRate *= 0.65f;
        if (isNearBonfire && !isStarving && !isFreezing) staminaRegenRate *= 1.15f;
        if (PlayerMeleeCombatDefense.Instance != null && PlayerMeleeCombatDefense.Instance.isBlocking)
        {
            staminaRegenRate *= 0.15f;
        }

        if (isBleeding)
        {
            TakeDamagePassive(bleedingHealthDamage * Time.deltaTime);
            if (!wasBleeding && HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF4444>SANGRAMENTO: Use Ataduras ou Pomada de Resina!</color>");
        }
        wasBleeding = isBleeding;

        if (isPoisoned && !wasPoisoned && HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#88FF55>ENVENENADO: Tome Sopa/Cha de Ervas ou Pomada!</color>");
        wasPoisoned = isPoisoned;

        if (isFractured && !wasFractured && HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FFAA44>FRATURA: Fabrique e use Tala de Madeira!</color>");
        wasFractured = isFractured;

        if (isSick && !wasSick && HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#AADD44>MAL-ESTAR: Beba Cha de Ervas Quente ou Sopa de Ervas!</color>");
        wasSick = isSick;

        if (playerController != null)
        {
            float speedPenalty = 1f;
            if (isStarving) speedPenalty -= 0.3f;
            if (isFreezing) speedPenalty -= 0.3f;
            if (isFractured) speedPenalty -= 0.30f;
            speedPenalty = Mathf.Max(speedPenalty, 0.4f);

            if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;
            if (inventoryCache != null)
            {
                speedPenalty *= inventoryCache.GetMoveSpeedMultiplier();
            }

            playerController.moveSpeed = basePlayerSpeed * Mathf.Max(0.32f, speedPenalty);
        }
    }
}
