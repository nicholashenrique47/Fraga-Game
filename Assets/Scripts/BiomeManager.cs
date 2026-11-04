using UnityEngine;

/// BiomeManager: Define zonas de bioma no mapa e ajusta propriedades visuais.
/// 4 biomas: Floresta Escura (0), Prado (1), Pantano (2), Ruinas (3).
public class BiomeManager : MonoBehaviour
{
    public enum BiomeType { DarkForest, Meadow, Swamp, Ruins }
    
    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public GameManager gameManager;
    public Transform playerTransform;
    
    [Header("Biome Grid")]
    public int biomeGridSize = 25; // Cada bioma cobre ~25x25 tiles
    
    // Current biome o jogador esta
    public BiomeType currentBiome { get; private set; } = BiomeType.DarkForest;
    
    // Propriedades por bioma (Paleta The Forest: luminosa, natural e vibrante)
    private static readonly Color[] biomeAmbientColors = {
        new Color(0.65f, 0.75f, 0.62f),   // DarkForest - verde floresta The Forest
        new Color(0.78f, 0.88f, 0.70f),   // Meadow - prado verdejante
        new Color(0.58f, 0.68f, 0.55f),   // Swamp - verde musgo natural
        new Color(0.65f, 0.70f, 0.75f)    // Ruins - pedra ancestral iluminada
    };
    
    private static readonly float[] biomeFogDensity = {
        0.5f,   // DarkForest
        0.2f,   // Meadow
        0.7f,   // Swamp
        0.4f    // Ruins
    };
    
    private static readonly float[] biomeTemperatureModifier = {
        -2f,    // DarkForest - frio
        2f,     // Meadow - temperado
        -1f,    // Swamp - humido e frio
        -3f     // Ruins - gelado sobrenatural
    };
    
    private BiomeType lastBiome = BiomeType.DarkForest;
    
    private void Start()
    {
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;
        
        // Determinar bioma baseado na posicao do jogador
        int px = Mathf.FloorToInt(playerTransform.position.x);
        int pz = Mathf.FloorToInt(playerTransform.position.z);
        
        currentBiome = GetBiomeAt(px, pz);
        
        if (currentBiome != lastBiome)
        {
            OnBiomeChanged(currentBiome);
            lastBiome = currentBiome;
        }
        
        // Modulação harmoniosa da cor de solo sem sobrepor a luz solar de DayNightCycle
        Color targetColor = biomeAmbientColors[(int)currentBiome];
        if (dayNightCycle != null && (dayNightCycle.currentTime >= 6f && dayNightCycle.currentTime < 18f))
        {
            targetColor = Color.Lerp(targetColor, new Color(0.85f, 0.90f, 0.82f), 0.5f);
        }
        else
        {
            targetColor = Color.Lerp(targetColor, new Color(0.25f, 0.32f, 0.45f), 0.6f);
        }
        RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, targetColor * 0.8f, Time.deltaTime * 0.5f);
    }
    
    public BiomeType GetBiomeAt(int x, int z)
    {
        // Dividir o mapa em quadrantes
        int quadX = x / biomeGridSize;
        int quadZ = z / biomeGridSize;
        
        // Hash simples para bioma deterministico
        int hash = (quadX * 7 + quadZ * 13) % 4;
        if (hash < 0) hash += 4;
        
        return (BiomeType)hash;
    }
    
    public float GetTemperatureModifier()
    {
        return biomeTemperatureModifier[(int)currentBiome];
    }
    
    public float GetFogDensity()
    {
        return biomeFogDensity[(int)currentBiome];
    }
    
    private void OnBiomeChanged(BiomeType newBiome)
    {
        Debug.Log($"[Biome] Entrou no bioma: {newBiome}");
        
        UIManager ui = FindAnyObjectByType<UIManager>();
        if (ui != null)
        {
            string biomeName = newBiome switch
            {
                BiomeType.DarkForest => "Floresta Sombria",
                BiomeType.Meadow => "Prado Aberto",
                BiomeType.Swamp => "Pantano Tóxico",
                BiomeType.Ruins => "Ruínas Ancestrais",
                _ => "Desconhecido"
            };
            ui.SetLootTooltip($"Bioma: {biomeName}");
        }
    }
}
