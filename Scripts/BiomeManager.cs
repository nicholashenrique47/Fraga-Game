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
    
    // Propriedades por bioma
    private static readonly Color[] biomeAmbientColors = {
        new Color(0.03f, 0.04f, 0.06f),   // DarkForest - muito escuro
        new Color(0.15f, 0.2f, 0.1f),     // Meadow - verde suave
        new Color(0.06f, 0.08f, 0.04f),   // Swamp - verde turvo
        new Color(0.08f, 0.06f, 0.1f)     // Ruins - roxo escuro
    };
    
    private static readonly float[] biomeFogDensity = {
        0.8f,   // DarkForest
        0.3f,   // Meadow
        0.95f,  // Swamp
        0.6f    // Ruins
    };
    
    private static readonly float[] biomeTemperatureModifier = {
        -2f,    // DarkForest - frio
        2f,     // Meadow - temperado
        -1f,    // Swamp - humido e frio
        -3f     // Ruins - gelado sobrenatural
    };
    
    private BiomeType lastBiome = BiomeType.DarkForest;
    
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
        
        // Smooth transition da ambient light
        Color targetColor = biomeAmbientColors[(int)currentBiome];
        if (dayNightCycle != null && (dayNightCycle.currentTime >= 6f && dayNightCycle.currentTime < 18f))
        {
            // De dia, clarear o bioma
            targetColor = Color.Lerp(targetColor, new Color(0.5f, 0.5f, 0.55f), 0.6f);
        }
        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetColor, Time.deltaTime * 0.5f);
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
