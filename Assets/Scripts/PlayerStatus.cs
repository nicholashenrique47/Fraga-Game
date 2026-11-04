using UnityEngine;

/// PlayerStatus: Sistema de status vitais do personagem (Stamina, Fome, Temperatura).
/// Opera em paralelo com o SanitySystem existente.
/// A fome drena ao longo do tempo. A temperatura desce a noite e sobe perto de fogueiras.
/// A stamina regenera parado e consome ao correr.
public class PlayerStatus : MonoBehaviour
{
    [Header("Stamina")]
    [Range(0f, 100f)] public float stamina = 100f;
    public float staminaDrainRate = 15f;   // Por segundo enquanto corre
    public float staminaRegenRate = 8f;    // Por segundo parado
    public float sprintSpeedMultiplier = 1.6f;

    [Header("Fome e Sede")]
    [Range(0f, 100f)] public float hunger = 100f;
    [Range(0f, 100f)] public float thirst = 100f;
    public float hungerDrainRate = 0.04f;   // Por segundo (lento)
    public float thirstDrainRate = 0.06f;   // Sede desce mais rapido
    public float starvationDamage = 2f;    // Dano na sanidade por segundo quando fome = 0
    public float dehydrationDamage = 3f;   // Dano na sanidade por segundo quando sede = 0

    [Header("Vida Fisica")]
    [Range(0f, 100f)] public float health = 100f;
    public event System.Action OnDeath;
    private bool isDead = false;

    [Header("Temperatura")]
    [Range(-20f, 40f)] public float bodyTemperature = 37f;
    public float ambientDayTemp = 18f;
    public float ambientNightTemp = 2f;
    public float bonfireWarmth = 15f;      // Bonus de temperatura perto de fogueira
    public float coldThreshold = 35f;      // Abaixo disto, sofre efeitos de frio (sincronizado com TIER1)
    public float coldSanityDamageRate = 1.5f; // Dano na sanidade por segundo
    public float coldHealthDamageRate = 0.5f; // Dano na vida fisica (Hipotermia)

    [Header("Status Effects")]
    public bool isSprinting = false;
    public bool isNearBonfire = false;
    public bool isStarving = false;
    public bool isFreezing = false;
    public bool isPoisoned = false;
    public float poisonTimer = 0f;
    public float poisonDamageRate = 3f;
    public bool isBleeding = false;
    public bool isFractured = false;

    [Header("Dependencies")]
    public PlayerController playerController;
    public SanitySystem sanitySystem;
    public DayNightCycle dayNightCycle;

    private float basePlayerSpeed;
    private float lastBreathingTime = 0f;
    private InventoryAdvanced inventoryCache;
    private PostProcessingManager ppManagerCache;
    private HapticManager hapticCache;
    private GameManager gameManagerCache;

    private void Awake()
    {
        health = 100f;
        stamina = 100f;
        hunger = 100f;
        thirst = 100f;
        bodyTemperature = 37.0f;
        isDead = false;
        isStarving = false;
        isFreezing = false;
        isPoisoned = false;
        isBleeding = false;
        isFractured = false;
    }

    private void Start()
    {
        inventoryCache = FindAnyObjectByType<InventoryAdvanced>();
        ppManagerCache = FindAnyObjectByType<PostProcessingManager>();
        hapticCache = FindAnyObjectByType<HapticManager>();
        gameManagerCache = FindAnyObjectByType<GameManager>();

        if (playerController != null)
        {
            basePlayerSpeed = playerController.moveSpeed;
        }
    }

    private void Update()
    {
        // --- Heavy Breathing (Immersion) ---
        if ((stamina < 30f || sanitySystem != null && sanitySystem.sanity < 30f) && Time.time - lastBreathingTime > 4f)
        {
            lastBreathingTime = Time.time;
            AudioManager am = FindAnyObjectByType<AudioManager>();
            if (am != null) am.PlayHorrorSting(); // Reutilizando canal de horror para respiracao pesada
            
            // Camera shake subtil
            FirstPersonCamera cam = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;
            if (cam != null) {
                cam.transform.localRotation *= Quaternion.Euler(Random.Range(-2f, 2f), Random.Range(-2f, 2f), 0);
            }
        }
        if (isDead) return;
        UpdateHungerAndThirst();
        UpdateTemperature();
        UpdateStamina();
        UpdatePoison();
        ApplyStatusEffects();
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        // Processamento tático de bloqueio e Parry Perfeito (The Forest Melee)
        if (PlayerMeleeCombatDefense.Instance != null)
        {
            amount = PlayerMeleeCombatDefense.Instance.ProcessIncomingDamage(amount);
            if (amount <= 0f) return; // Parry Perfeito anulou 100% do dano!
        }

        health -= amount;
        Debug.Log($"[PlayerStatus] Levou {amount} de dano! Vida: {health}");

        // Juice e Feedback de Dano
        if (ppManagerCache != null)
        {
            ppManagerCache.TriggerDamageFlash();
        }

        if (hapticCache != null)
        {
            hapticCache.TriggerHeavyVibration(); // Feedback fisico no celular
        }

        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        health = 0f;
        OnDeath?.Invoke();

        Debug.Log("[PlayerStatus] Morte fisica!");
        
        if (ppManagerCache != null)
        {
            ppManagerCache.StartDeathSequence();
        }
        else
        {
            if (gameManagerCache != null) gameManagerCache.TriggerGameOver();
        }
    }

    public void Revive()
    {
        isDead = false;
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

        if (BodyTemperatureSystem.Instance != null)
        {
            BodyTemperatureSystem.Instance.ResetTemperature();
        }
    }

    private void UpdateHungerAndThirst()
    {
        hunger -= hungerDrainRate * Time.deltaTime;
        thirst -= thirstDrainRate * Time.deltaTime;
        
        hunger = Mathf.Clamp(hunger, 0f, 100f);
        thirst = Mathf.Clamp(thirst, 0f, 100f);

        isStarving = hunger <= 0f;
        bool isDehydrated = thirst <= 0f;

        if (sanitySystem != null)
        {
            if (isStarving) sanitySystem.sanity -= starvationDamage * Time.deltaTime;
            if (isDehydrated) sanitySystem.sanity -= dehydrationDamage * Time.deltaTime;
        }
    }

    private void UpdateTemperature()
    {
        // Se BodyTemperatureSystem estiver ativo, ele governa a temperatura corporal central autoritativamente
        if (BodyTemperatureSystem.Instance != null)
        {
            bodyTemperature = BodyTemperatureSystem.Instance.currentTemperature;
            isFreezing = bodyTemperature <= BodyTemperatureSystem.TIER1_SHIVER_TEMP;
            return;
        }

        // Fallback legado caso BodyTemperatureSystem não esteja presente
        float targetTemp = ambientDayTemp;
        if (dayNightCycle != null)
        {
            bool isNight = dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f;
            targetTemp = isNight ? ambientNightTemp : ambientDayTemp;
        }

        // --- Isolamento Termico (Equipamento Passivo na Mochila) ---
        bool hasAnimalSkin = inventoryCache != null && inventoryCache.HasItem(105); // 105 = Pele de Animal

        if (hasAnimalSkin && targetTemp < 5f)
        {
            targetTemp = 6f;
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
    
    // Metodo separado para nao engatilhar a vibracao e o flash a cada frame
    public void TakeDamagePassive(float amount)
    {
        if (isDead) return;
        health -= amount;
        if (health <= 0) Die();
    }

    private void UpdateStamina()
    {
        // Suporte a Sprint no PC via tecla Shift
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (stamina > 5f && playerController != null && playerController.IsMoving)
            {
                isSprinting = true;
            }
        }
        else if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
        {
            isSprinting = false;
        }

        if (isSprinting && stamina > 0f)
        {
            stamina -= staminaDrainRate * Time.deltaTime;

            if (playerController != null)
            {
                playerController.moveSpeed = basePlayerSpeed * sprintSpeedMultiplier;
            }
        }
        else
        {
            isSprinting = false;
            stamina += staminaRegenRate * Time.deltaTime;

            if (playerController != null)
            {
                playerController.moveSpeed = basePlayerSpeed;
            }
        }

        stamina = Mathf.Clamp(stamina, 0f, 100f);

        // Sem stamina = nao pode correr
        if (stamina <= 0f)
        {
            isSprinting = false;
        }
    }

    private void UpdatePoison()
    {
        if (!isPoisoned) return;

        poisonTimer -= Time.deltaTime;
        if (poisonTimer <= 0f)
        {
            isPoisoned = false;
            poisonTimer = 0f;
            Debug.Log("[PlayerStatus] Veneno dissipou-se.");
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
        Debug.Log($"[PlayerStatus] Envenenado por {duration}s!");
    }

    public void Eat(float hungerRestore, float sanityRestore)
    {
        hunger = Mathf.Clamp(hunger + hungerRestore, 0f, 100f);
        if (sanitySystem != null)
        {
            sanitySystem.sanity = Mathf.Clamp(sanitySystem.sanity + sanityRestore, 0f, 100f);
        }
        Debug.Log($"[PlayerStatus] Comeu! Fome +{hungerRestore}, Sanidade +{sanityRestore}");
    }
    
    public void Drink(float thirstRestore)
    {
        thirst = Mathf.Clamp(thirst + thirstRestore, 0f, 100f);
        Debug.Log($"[PlayerStatus] Bebeu! Sede +{thirstRestore}");
    }
    
    public void Heal(float healthRestore)
    {
        health = Mathf.Clamp(health + healthRestore, 0f, 100f);
        Debug.Log($"[PlayerStatus] Curou! Vida +{healthRestore}");
    }

    public void StartSprint()
    {
        if (stamina > 10f) isSprinting = true;
    }

    public void StopSprint()
    {
        isSprinting = false;
    }

    private void ApplyStatusEffects()
    {
        // Fome baixa reduz velocidade de regeneracao de stamina
        if (hunger < 20f)
        {
            staminaRegenRate = 3f; // Metade da regeneracao base
        }
        else
        {
            staminaRegenRate = 8f;
        }

        // Frio e Fome extrema penalizam fisicamente a velocidade do personagem
        if (playerController != null)
        {
            float speedPenalty = 1f;
            if (isStarving) speedPenalty -= 0.3f;
            if (isFreezing) speedPenalty -= 0.3f;
            
            // Impede penalizacoes negativas ou nulas (minimo 40% da velocidade)
            speedPenalty = Mathf.Max(speedPenalty, 0.4f);

            if (!isSprinting)
            {
                playerController.moveSpeed = basePlayerSpeed * speedPenalty;
            }
            else
            {
                playerController.moveSpeed = (basePlayerSpeed * sprintSpeedMultiplier) * speedPenalty;
            }
        }
    }
}
