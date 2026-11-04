using UnityEngine;

/// <summary>
/// BodyTemperatureSystem: Termorregulação corporal realista, umidade das roupas e hipotermia atlântica.
/// Mantém 37°C naturalmente durante o dia límpido em terra através do metabolismo basal.
/// Hipotermia só ocorre durante noites gélidas sem abrigo/fogo, imersão em água fria ou sob chuvas severas prolongadas.
/// </summary>
public class BodyTemperatureSystem : MonoBehaviour
{
    public static BodyTemperatureSystem Instance { get; private set; }

    [Header("Temperature & Wetness")]
    public float currentTemperature = 37.0f; // 37°C ideal
    public float clothesWetness = 0.0f;      // 0 a 1 (100% encharcado)

    [Header("Hypothermia Tiers")]
    public const float TIER1_SHIVER_TEMP = 35.0f;
    public const float TIER2_FATIGUE_TEMP = 33.5f;
    public const float TIER3_DAMAGE_TEMP = 31.5f;

    [Header("Dependencies")]
    public PlayerStatus playerStatus;
    public GalicianWeatherEngine weatherEngine;

    private InventoryAdvanced inventoryCache;
    private float shiverTimer = 0f;

    private void Awake()
    {
        Instance = this;
        currentTemperature = 37.0f;
        clothesWetness = 0.0f;
    }

    private void Start()
    {
        if (playerStatus == null) playerStatus = GetComponent<PlayerStatus>();
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();
        if (weatherEngine == null) weatherEngine = FindAnyObjectByType<GalicianWeatherEngine>();
        if (inventoryCache == null) inventoryCache = FindAnyObjectByType<InventoryAdvanced>();
    }

    private void Update()
    {
        bool isRaining = weatherEngine != null && weatherEngine.isRaining;
        bool isNight = DayNightCycle.Instance != null ? DayNightCycle.Instance.IsNight() : false;
        bool isNearFire = CheckNearCampfire();
        bool hasAnimalFur = inventoryCache != null && inventoryCache.HasItem(105);

        // 1. Acúmulo ou secagem de umidade nas roupas
        if (isNearFire)
        {
            // Seca roupas rapidamente próximo ao fogo
            clothesWetness = Mathf.Max(0f, clothesWetness - Time.deltaTime * 0.20f);
        }
        else if (isRaining)
        {
            // Encharca gradualmente na chuva
            clothesWetness = Mathf.Min(1.0f, clothesWetness + Time.deltaTime * 0.035f);
        }
        else
        {
            // Secagem natural ao ar (muito mais rápida com calor solar do dia)
            float dryRate = isNight ? 0.015f : 0.05f;
            clothesWetness = Mathf.Max(0f, clothesWetness - Time.deltaTime * dryRate);
        }

        // 2. Termorregulação Corporal Realista
        if (isNearFire)
        {
            // Próximo à fogueira: aquece confortavelmente até os 37°C
            currentTemperature = Mathf.Min(37.0f, currentTemperature + Time.deltaTime * 0.60f);
        }
        else if (!isNight && !isRaining)
        {
            // Dia límpido e temperado na Galícia (18°C a 24°C):
            // O metabolismo humano mantém e recupera a temperatura corporal normal de 37°C.
            if (currentTemperature < 37.0f)
            {
                currentTemperature = Mathf.Min(37.0f, currentTemperature + Time.deltaTime * 0.25f);
            }
        }
        else if (isNight)
        {
            // Noite fria atlântica: resfriamento gradual se desabrigado e sem fogo
            float coolingRate = 0.012f + (clothesWetness * 0.035f);
            if (hasAnimalFur)
            {
                coolingRate *= 0.35f; // Pele de animal reduz perda térmica em 65%
            }
            currentTemperature = Mathf.Max(28.0f, currentTemperature - Time.deltaTime * coolingRate);
        }
        else if (isRaining)
        {
            // Chuva diurna: só resfria se as roupas ficarem substancialmente molhadas
            if (clothesWetness > 0.4f)
            {
                float wetCooling = (clothesWetness - 0.4f) * 0.008f;
                currentTemperature = Mathf.Max(33.0f, currentTemperature - Time.deltaTime * wetCooling);
            }
            else if (currentTemperature < 37.0f)
            {
                currentTemperature = Mathf.Min(37.0f, currentTemperature + Time.deltaTime * 0.10f);
            }
        }

        // 3. Efeitos fisiológicos de Hipotermia
        HandleHypothermiaEffects();
    }

    private void HandleHypothermiaEffects()
    {
        if (currentTemperature <= TIER1_SHIVER_TEMP)
        {
            // Tremor involuntário na câmera (Dente batendo / Shivering)
            shiverTimer += Time.deltaTime * 18f;
            if (FirstPersonCamera.Instance != null)
            {
                float shiverAmount = Mathf.Lerp(0.005f, 0.025f, (TIER1_SHIVER_TEMP - currentTemperature) / 3.5f);
                FirstPersonCamera.Instance.AddShake(Mathf.Sin(shiverTimer) * shiverAmount * Time.deltaTime);
            }
        }

        if (currentTemperature <= TIER2_FATIGUE_TEMP)
        {
            // Drena estamina do jogador e impede corrida
            if (playerStatus != null)
            {
                playerStatus.stamina = Mathf.Min(playerStatus.stamina, 40f);
            }
        }

        if (currentTemperature <= TIER3_DAMAGE_TEMP)
        {
            // Dano de hipotermia atenuado (permite tempo hábil de sobrevivência para construir abrigo ou fogueira)
            if (playerStatus != null)
            {
                playerStatus.TakeDamagePassive(0.40f * Time.deltaTime);
            }
        }
    }

    public void ResetTemperature()
    {
        currentTemperature = 37.0f;
        clothesWetness = 0.0f;
        shiverTimer = 0f;
    }

    private bool CheckNearCampfire()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 5.0f);
        foreach (var c in hits)
        {
            string n = c.gameObject.name.ToLower();
            if (n.Contains("campfire") || n.Contains("bonfire") || n.Contains("fogueira"))
            {
                return true;
            }
        }
        return false;
    }
}
