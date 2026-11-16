using UnityEngine;

/// <summary>
/// BodyTemperatureSystem: Termorregulacao corporal, umidade das roupas e isolamento por equipamento termico.
/// Zero-GC no Update (cache de GameManager e fogueiras fisicas BonfireEntity a 2 Hz).
/// </summary>
public class BodyTemperatureSystem : MonoBehaviour
{
    public static BodyTemperatureSystem Instance { get; private set; }

    [Header("Temperature & Wetness")]
    public float currentTemperature = 37.0f;
    public float clothesWetness = 0.0f;

    [Header("Hypothermia Tiers")]
    public const float TIER1_SHIVER_TEMP = 35.0f;
    public const float TIER2_FATIGUE_TEMP = 33.5f;
    public const float TIER3_DAMAGE_TEMP = 31.5f;

    [Header("Dependencies")]
    public PlayerStatus playerStatus;
    public GalicianWeatherEngine weatherEngine;

    private InventoryAdvanced inventoryCache;
    private GameManager gmCache;
    private BonfireEntity[] bonfireCache;
    private float nextFireScanTime = 0f;
    private float nextCacheRefreshTime = 0f;
    private bool cachedNearFire = false;
    private float shiverTimer = 0f;
    private int lastHypoTierAlert = 0;
    private bool alertedWet = false;

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
        if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        gmCache = FindAnyObjectByType<GameManager>();
    }

    private void Update()
    {
        bool isRaining = weatherEngine != null && weatherEngine.isRaining;
        bool isNight = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight();

        if (Time.time >= nextFireScanTime)
        {
            nextFireScanTime = Time.time + 0.45f;
            cachedNearFire = CheckNearCampfire();
            if (playerStatus != null && cachedNearFire)
            {
                playerStatus.isNearBonfire = true;
            }
        }

        bool isNearFire = cachedNearFire || (playerStatus != null && playerStatus.isNearBonfire);
        float insulation = inventoryCache != null ? inventoryCache.GetColdInsulation() : 0f;

        if (isNearFire)
        {
            clothesWetness = Mathf.Max(0f, clothesWetness - Time.deltaTime * 0.20f);
        }
        else if (isRaining)
        {
            clothesWetness = Mathf.Min(1.0f, clothesWetness + Time.deltaTime * 0.035f * (1f - insulation * 0.5f));
        }
        else
        {
            float dryRate = isNight ? 0.015f : 0.05f;
            clothesWetness = Mathf.Max(0f, clothesWetness - Time.deltaTime * dryRate);
        }

        if (isNearFire)
        {
            currentTemperature = Mathf.Min(37.0f, currentTemperature + Time.deltaTime * 0.60f);
        }
        else if (!isNight && !isRaining)
        {
            if (currentTemperature < 37.0f)
            {
                currentTemperature = Mathf.Min(37.0f, currentTemperature + Time.deltaTime * 0.25f);
            }
        }
        else if (isNight)
        {
            float coolingRate = (0.012f + clothesWetness * 0.035f) * (1f - insulation);
            currentTemperature = Mathf.Max(28.0f, currentTemperature - Time.deltaTime * coolingRate);
        }
        else if (isRaining)
        {
            if (clothesWetness > 0.4f)
            {
                float wetCooling = (clothesWetness - 0.4f) * 0.008f * (1f - insulation);
                currentTemperature = Mathf.Max(33.0f, currentTemperature - Time.deltaTime * wetCooling);
            }
            else if (currentTemperature < 37.0f)
            {
                currentTemperature = Mathf.Min(37.0f, currentTemperature + Time.deltaTime * 0.10f);
            }
        }

        HandleHypothermiaEffects();
    }

    private void HandleHypothermiaEffects()
    {
        int hypoTier = 0;
        if (currentTemperature <= TIER3_DAMAGE_TEMP) hypoTier = 3;
        else if (currentTemperature <= TIER2_FATIGUE_TEMP) hypoTier = 2;
        else if (currentTemperature <= TIER1_SHIVER_TEMP) hypoTier = 1;

        if (hypoTier > lastHypoTierAlert && HUDSurvivalOverlay.Instance != null)
        {
            lastHypoTierAlert = hypoTier;
            if (hypoTier == 1)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#77CCFF>Frio intenso! Aqueca-se na fogueira ou vista Casaco.</color>");
            else if (hypoTier == 2)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55AAFF>Hipotermia: Estamina limitada pelo frio!</color>");
            else if (hypoTier == 3)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF5555>HIPOTERMIA SEVERA: Perdendo vida! Acenda fogo ou tome Cha!</color>");
        }
        else if (hypoTier == 0 && currentTemperature > 35.8f)
        {
            lastHypoTierAlert = 0;
        }

        if (clothesWetness >= 0.72f && !alertedWet && !cachedNearFire && HUDSurvivalOverlay.Instance != null)
        {
            alertedWet = true;
            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#66BBFF>Roupas encharcadas! Seque perto de uma fogueira.</color>");
        }
        else if (clothesWetness < 0.35f)
        {
            alertedWet = false;
        }

        if (currentTemperature <= TIER1_SHIVER_TEMP)
        {
            shiverTimer += Time.deltaTime * 18f;
            if (FirstPersonCamera.Instance != null)
            {
                float shiverAmount = Mathf.Lerp(0.005f, 0.025f, (TIER1_SHIVER_TEMP - currentTemperature) / 3.5f);
                FirstPersonCamera.Instance.AddShake(Mathf.Sin(shiverTimer) * shiverAmount * Time.deltaTime);
            }
        }

        if (currentTemperature <= TIER2_FATIGUE_TEMP && playerStatus != null)
        {
            playerStatus.stamina = Mathf.Min(playerStatus.stamina, 40f);
        }

        if (currentTemperature <= TIER3_DAMAGE_TEMP && playerStatus != null)
        {
            playerStatus.TakeDamagePassive(0.40f * Time.deltaTime);
        }
    }

    public void WarmUp(float deltaDegrees)
    {
        if (deltaDegrees <= 0f) return;
        currentTemperature = Mathf.Min(37.5f, currentTemperature + deltaDegrees);
        clothesWetness = Mathf.Max(0f, clothesWetness - deltaDegrees * 0.08f);
        if (playerStatus != null) playerStatus.bodyTemperature = currentTemperature;
    }

    public void ResetTemperature()
    {
        currentTemperature = 37.0f;
        clothesWetness = 0.0f;
        shiverTimer = 0f;
        lastHypoTierAlert = 0;
        alertedWet = false;
    }

    private bool CheckNearCampfire()
    {
        if (Time.time >= nextCacheRefreshTime)
        {
            nextCacheRefreshTime = Time.time + 1.5f;
            bonfireCache = FindObjectsByType<BonfireEntity>();
        }

        if (bonfireCache != null)
        {
            Vector3 pos = transform.position;
            for (int i = 0; i < bonfireCache.Length; i++)
            {
                BonfireEntity bf = bonfireCache[i];
                if (bf != null && bf.isLit && (bf.transform.position - pos).sqrMagnitude <= 25f)
                {
                    return true;
                }
            }
        }

        if (gmCache == null) gmCache = FindAnyObjectByType<GameManager>();
        if (gmCache != null && gmCache.mapDataPtr != System.IntPtr.Zero)
        {
            int px = Mathf.RoundToInt(transform.position.x);
            int pz = Mathf.RoundToInt(transform.position.z);

            for (int x = px - 3; x <= px + 3; x++)
            {
                for (int z = pz - 3; z <= pz + 3; z++)
                {
                    if (x >= 0 && x < gmCache.mapWidth && z >= 0 && z < gmCache.mapHeight)
                    {
                        int tile = ResourceManager.GetMapCell(gmCache.mapDataPtr, gmCache.mapWidth, gmCache.mapHeight, x, z);
                        if (tile == 4 || tile == 15) return true;
                    }
                }
            }
        }
        return false;
    }
}
