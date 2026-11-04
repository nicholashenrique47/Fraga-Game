using UnityEngine;

/// <summary>
/// SanitySystem: Governa a sanidade mental, combustível do lampião e proteção contra as trevas galegas.
/// Calibrado para sobrevivência imersiva e justa:
/// - A escuridão drena sanidade gradualmente (nunca morte instantânea súbita).
/// - Sanidade baixa causa advertências visuais e delírios sem travar o jogo.
/// - Quando a sanidade chega a 0, entra em estado de agonia drenando vida aos poucos,
///   concedendo tempo real para o jogador acender uma fogueira ou tocha.
/// </summary>
public class SanitySystem : MonoBehaviour
{
    [Header("Stats")]
    [Range(0f, 100f)] public float sanity = 100f;
    [Range(0f, 100f)] public float oil = 100f;

    [Header("Depletion Rates (Calibrado The Forest AAA)")]
    public float oilDepletionRate = 0.035f;        // ~48 minutos de lampião contínuo
    public float sanityDepletionRateDark = 0.18f;  // ~9.5 minutos na escuridão plena antes de esgotar
    public float sanityRecoveryRateLight = 1.6f;   // Recuperação satisfatória sob o sol ou fogueira

    [Header("Light Component")]
    public Light lampLight; // Componente Light do Unity URP
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

    private void Awake()
    {
        sanity = 100f;
        oil = 100f;
        isLampOn = true;
        isDepleted = false;
        zeroSanityTimer = 0f;
        playerStatusCache = GetComponent<PlayerStatus>();
        if (dayNightCycle == null)
        {
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        }
    }

    private void Start()
    {
        if (dayNightCycle == null)
        {
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        }
        if (playerStatusCache == null)
        {
            playerStatusCache = GetComponent<PlayerStatus>();
        }
    }

    private void Update()
    {
        // Toggle do lampião no PC pela tecla F
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleLamp();
        }

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
        CheckBonfireProximity();

        // 1. Verifica se é dia (entre 06:00 e 19:30)
        if (dayNightCycle == null)
        {
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        }
        bool isDaytime = (dayNightCycle != null) ? (dayNightCycle.currentTime >= 6f && dayNightCycle.currentTime < 19.5f) : true;

        // 2. Verifica se o jogador está iluminado (Dia, Lampião aceso com óleo ou perto de fogueira)
        bool hasLantern = (lampLight != null && lampLight.enabled && isLampOn && oil > minOilThresholdForLight);
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

        // 3. Estado de Agonia / Delírio quando a sanidade zera
        if (sanity <= 0f)
        {
            zeroSanityTimer += Time.deltaTime;

            // Dano gradual na vida em vez de morte súbita instantânea
            if (playerStatusCache != null)
            {
                playerStatusCache.TakeDamagePassive(2.5f * Time.deltaTime);
            }

            // Apenas aciona a sequência de colapso mental se ficar mais de 35s ininterruptos em sanidade zero sem luz
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
            // Recuperação lenta natural de gordura/óleo durante o dia enquanto o lampião repousa
            if (dayNightCycle != null && !dayNightCycle.IsNight() && oil < 40f)
            {
                oil += 0.01f * Time.deltaTime;
            }
        }
    }

    private void UpdateLight()
    {
        if (lampLight == null) return;

        // Se o óleo acabar, apaga o lampião
        if (oil < minOilThresholdForLight)
        {
            isLampOn = false;
            lampLight.enabled = false;
            return;
        }

        lampLight.enabled = isLampOn;

        // Normalizar valor do óleo de 0.0 a 1.0 para mapeamento suave
        float oilNormalized = oil / 100f;
        lampLight.range = Mathf.Lerp(8f, maxLightRange, oilNormalized);
        lampLight.intensity = Mathf.Lerp(1.2f, maxLightIntensity, oilNormalized);
    }

    private void CheckBonfireProximity()
    {
        isNearBonfire = false;
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm == null || gm.mapDataPtr == System.IntPtr.Zero) return;

        int pX = Mathf.RoundToInt(transform.position.x);
        int pY = Mathf.RoundToInt(transform.position.z);

        // Raio de 3 tiles em volta do jogador
        for (int y = pY - 3; y <= pY + 3; y++)
        {
            for (int x = pX - 3; x <= pX + 3; x++)
            {
                int tileID = ResourceManager.GetMapCell(gm.mapDataPtr, gm.mapWidth, gm.mapHeight, x, y);
                if (tileID == 4) // Fogueira encontrada nas redondezas
                {
                    isNearBonfire = true;
                    sanity += (sanityRecoveryRateLight * 2.0f) * Time.deltaTime;
                    return;
                }
            }
        }
    }

    public float GetLightRadius()
    {
        float radius = 0f;
        if (lampLight != null && lampLight.enabled && isLampOn)
        {
            radius = lampLight.range;
        }
        if (isNearBonfire)
        {
            radius = Mathf.Max(radius, 10f);
        }
        return radius;
    }

    private void HandleSanityDepleted()
    {
        Debug.Log("[SanitySystem] Sanidade esgotada após agonia prolongada. O jogador sucumbiu à escuridão da Fraga.");
        
        PostProcessingManager ppManager = FindAnyObjectByType<PostProcessingManager>();
        if (ppManager != null)
        {
            ppManager.StartDeathSequence();
        }
        else
        {
            GameManager gm = FindAnyObjectByType<GameManager>();
            if (gm != null) gm.TriggerGameOver();
        }

        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayDeathVFX(transform.position);
        }
    }
}
