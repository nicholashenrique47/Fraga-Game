using UnityEngine;

public class SanitySystem : MonoBehaviour
{
    [Header("Stats")]
    [Range(0f, 100f)] public float sanity = 100f;
    [Range(0f, 100f)] public float oil = 100f;

    [Header("Depletion Rates")]
    public float oilDepletionRate = 1f; // Óleo consumido por segundo
    public float sanityDepletionRateDark = 5f; // Sanidade perdida por segundo na escuridão
    public float sanityRecoveryRateLight = 1f; // Sanidade recuperada na luz

    [Header("Light Component")]
    public Light lampLight; // Componente Light do Unity URP
    public float maxLightRange = 10f;
    public float maxLightIntensity = 2f;
    public float minOilThresholdForLight = 2f;

    private void Update()
    {
        UpdateOil();
        UpdateLight();
        UpdateSanity();
    }

    private void UpdateSanity()
    {
        if (oil > minOilThresholdForLight)
        {
            sanity += sanityRecoveryRateLight * Time.deltaTime;
        }
        else
        {
            sanity -= sanityDepletionRateDark * Time.deltaTime;
        }
        sanity = Mathf.Clamp(sanity, 0f, 100f);

        if (sanity <= 0f)
        {
            HandleSanityDepleted();
        }
        
        CheckBonfireProximity();
    }

    private void UpdateOil()
    {
        if (oil > 0f)
        {
            oil -= oilDepletionRate * Time.deltaTime;
            oil = Mathf.Clamp(oil, 0f, 100f);
        }
    }

    private void UpdateLight()
    {
        if (lampLight == null) return;

        // Normalizar valor do óleo de 0.0 a 1.0 para mapeamento proporcional
        float oilNormalized = oil / 100f;

        // Reduz gradualmente o raio (Range) e a intensidade à medida que o óleo acaba
        lampLight.range = Mathf.Lerp(0f, maxLightRange, oilNormalized);
        lampLight.intensity = Mathf.Lerp(0f, maxLightIntensity, oilNormalized);

        // Desliga a luz se o óleo estiver num nível crítico
        lampLight.enabled = oil >= minOilThresholdForLight;
    }

    private void CheckBonfireProximity()
    {
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm == null || gm.mapDataPtr == System.IntPtr.Zero) return;

        int pX = Mathf.RoundToInt(transform.position.x);
        int pY = Mathf.RoundToInt(transform.position.z);

        // Raio de 2 tiles em volta do jogador
        for (int y = pY - 2; y <= pY + 2; y++)
        {
            for (int x = pX - 2; x <= pX + 2; x++)
            {
                int tileID = ResourceManager.GetMapCell(gm.mapDataPtr, gm.mapWidth, gm.mapHeight, x, y);
                if (tileID == 4) // Fogueira encontrada nas redondezas
                {
                    sanity += (sanityRecoveryRateLight * 1.5f) * Time.deltaTime; // Fogueira regenera mais rápido que o lampião
                    return; // Basta uma
                }
            }
        }
    }

    public float GetLightRadius()
    {
        float radius = 0f;
        if (lampLight != null && lampLight.enabled)
        {
            radius = lampLight.range; // Raio atual da luz afasta Vultos
        }
        
        // Fogueiras também servem de raio de repulsão? Na arquitetura do Flow Field seria complexo,
        // mas podemos fingir que a Fogueira é o próprio jogador se ele estiver lá perto.
        return radius;
    }

    private void HandleSanityDepleted()
    {
        Debug.Log("[SanitySystem] Sanidade esgotada. O jogador sucumbiu a escuridao da Fraga.");
        
        // Acionar sequencia cinematica de morte (Post-Processing fade-out)
        PostProcessingManager ppManager = FindAnyObjectByType<PostProcessingManager>();
        if (ppManager != null)
        {
            ppManager.StartDeathSequence(); // O PPManager chama TriggerGameOver ao terminar
        }
        else
        {
            // Fallback direto se o PPManager nao existir
            GameManager gm = FindAnyObjectByType<GameManager>();
            if (gm != null) gm.TriggerGameOver();
        }

        // VFX de morte (particulas de escuridao)
        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayDeathVFX(transform.position);
        }
    }
}
