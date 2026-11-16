using UnityEngine;

/// <summary>
/// SanitySystem: Governa a sanidade mental, combustivel do lampiao e protecao contra as trevas galegas.
/// Unifica deteccao de fogueiras fisicas (BonfireEntity) e grid nativo, sincronizando PlayerStatus.isNearBonfire.
/// </summary>
public class SanitySystem : MonoBehaviour
{
    [Header("Stats")]
    [Range(0f, 100f)] public float sanity = 100f;
    [Range(0f, 100f)] public float oil = 100f;

    [Header("Depletion Rates (Calibrado The Forest AAA)")]
    public float oilDepletionRate = 0.035f;
    public float sanityDepletionRateDark = 0.18f;
    public float sanityRecoveryRateLight = 1.6f;

    [Header("Light Component")]
    public Light lampLight;
    public float maxLightRange = 18f;
    public float maxLightIntensity = 2.8f;
    public float minOilThresholdForLight = 1f;
    public bool isLampOn = true;

    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public bool isNearBonfire = false;

    private bool isDepleted = false;
    private float zeroSanityTimer = 0f;
    private PlayerStatus playerStatusCache;
    private GameManager gameManagerCache;
    private PostProcessingManager ppManagerCache;
    private InventoryAdvanced inventoryCache;
    private float nextBonfireCheckTime = 0f;
    private BonfireEntity[] bonfireCache;
    private float nextBonfireCacheTime = 0f;

    private void Awake()
    {
        sanity = 100f;
        oil = 100f;
        isLampOn = true;
        isDepleted = false;
        zeroSanityTimer = 0f;
        playerStatusCache = GetComponent<PlayerStatus>();
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
    }

    private void Start()
    {
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        if (playerStatusCache == null) playerStatusCache = GetComponent<PlayerStatus>();
        gameManagerCache = FindAnyObjectByType<GameManager>();
        ppManagerCache = FindAnyObjectByType<PostProcessingManager>();
        inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
    }

    private void Update()
    {
        UpdateOil();
        UpdateLight();
        UpdateSanity();
    }

    public void ToggleLamp()
    {
        if (oil <= minOilThresholdForLight)
        {
            isLampOn = false;
        }
        else
        {
            isLampOn = !isLampOn;
        }

        if (lampLight != null)
        {
            lampLight.enabled = isLampOn;
        }
    }

    public void RefillOil(float amount)
    {
        oil = Mathf.Clamp(oil + amount, 0f, 100f);
        if (oil > minOilThresholdForLight && !isLampOn)
        {
            isLampOn = true;
            if (lampLight != null) lampLight.enabled = true;
        }
    }

    private void UpdateSanity()
    {
        if (Time.time >= nextBonfireCheckTime)
        {
            nextBonfireCheckTime = Time.time + 0.35f;
            CheckBonfireProximity();
        }

        if (isNearBonfire)
        {
            sanity += (sanityRecoveryRateLight * 2.0f) * Time.deltaTime;
        }

        bool isDaytime = (dayNightCycle != null) ? (dayNightCycle.currentTime >= 6f && dayNightCycle.currentTime < 19.5f) : true;
        bool hasOffHandLight = inventoryCache != null && (inventoryCache.equippedOffHandId == 303 || inventoryCache.equippedOffHandId == 304);
        bool hasLantern = (lampLight != null && lampLight.enabled && isLampOn && oil > minOilThresholdForLight) || hasOffHandLight;
        bool isInLight = isDaytime || hasLantern || isNearBonfire;

        if (isInLight)
        {
            sanity += sanityRecoveryRateLight * Time.deltaTime;
            zeroSanityTimer = 0f;
        }
        else
        {
            sanity -= sanityDepletionRateDark * Time.deltaTime;
        }
        sanity = Mathf.Clamp(sanity, 0f, 100f);

        if (sanity <= 0f)
        {
            zeroSanityTimer += Time.deltaTime;
            if (playerStatusCache != null)
            {
                playerStatusCache.TakeDamagePassive(2.5f * Time.deltaTime);
            }

            if (zeroSanityTimer > 35f && !isDepleted)
            {
                isDepleted = true;
                HandleSanityDepleted();
            }
        }
        else
        {
            isDepleted = false;
            zeroSanityTimer = 0f;
        }
    }

    private void UpdateOil()
    {
        if (lampLight != null && lampLight.enabled && isLampOn && oil > 0f)
        {
            oil -= oilDepletionRate * Time.deltaTime;
            oil = Mathf.Clamp(oil, 0f, 100f);
        }
        else if (!isLampOn)
        {
            if (dayNightCycle != null && !dayNightCycle.IsNight() && oil < 40f)
            {
                oil += 0.01f * Time.deltaTime;
            }
        }
    }

    private void UpdateLight()
    {
        if (lampLight == null) return;

        if (oil < minOilThresholdForLight)
        {
            isLampOn = false;
            lampLight.enabled = false;
            return;
        }

        lampLight.enabled = isLampOn;
        float oilNormalized = oil / 100f;
        lampLight.range = Mathf.Lerp(8f, maxLightRange, oilNormalized);
        lampLight.intensity = Mathf.Lerp(1.2f, maxLightIntensity, oilNormalized);
    }

    private void CheckBonfireProximity()
    {
        isNearBonfire = false;

        if (Time.time >= nextBonfireCacheTime)
        {
            nextBonfireCacheTime = Time.time + 1.5f;
            bonfireCache = FindObjectsByType<BonfireEntity>();
        }

        if (bonfireCache != null)
        {
            Vector3 pPos = transform.position;
            for (int i = 0; i < bonfireCache.Length; i++)
            {
                BonfireEntity bf = bonfireCache[i];
                if (bf != null && bf.isLit && (bf.transform.position - pPos).sqrMagnitude <= 25f)
                {
                    isNearBonfire = true;
                    break;
                }
            }
        }

        if (!isNearBonfire)
        {
            if (gameManagerCache == null) gameManagerCache = FindAnyObjectByType<GameManager>();
            if (gameManagerCache != null && gameManagerCache.mapDataPtr != System.IntPtr.Zero)
            {
                int pX = Mathf.RoundToInt(transform.position.x);
                int pY = Mathf.RoundToInt(transform.position.z);

                for (int y = pY - 3; y <= pY + 3 && !isNearBonfire; y++)
                {
                    for (int x = pX - 3; x <= pX + 3; x++)
                    {
                        if (x >= 0 && x < gameManagerCache.mapWidth && y >= 0 && y < gameManagerCache.mapHeight)
                        {
                            int tileID = ResourceManager.GetMapCell(gameManagerCache.mapDataPtr, gameManagerCache.mapWidth, gameManagerCache.mapHeight, x, y);
                            if (tileID == 4)
                            {
                                isNearBonfire = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        if (playerStatusCache != null)
        {
            playerStatusCache.isNearBonfire = isNearBonfire;
        }
    }

    public float GetLightRadius()
    {
        float radius = 0f;
        if (lampLight != null && lampLight.enabled && isLampOn)
        {
            radius = lampLight.range;
        }
        if (inventoryCache != null && (inventoryCache.equippedOffHandId == 303 || inventoryCache.equippedOffHandId == 304))
        {
            radius = Mathf.Max(radius, 12f);
        }
        if (isNearBonfire)
        {
            radius = Mathf.Max(radius, 10f);
        }
        return radius;
    }

    private void HandleSanityDepleted()
    {
        if (ppManagerCache != null)
        {
            ppManagerCache.StartDeathSequence();
        }
        else if (gameManagerCache != null)
        {
            gameManagerCache.TriggerGameOver();
        }

        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayDeathVFX(transform.position);
        }
    }
}
