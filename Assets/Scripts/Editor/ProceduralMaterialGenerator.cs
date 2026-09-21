using UnityEngine;
using UnityEditor;

/// ProceduralMaterialGenerator: Cria materiais procedurais com texturas unicas
/// para cada bioma e elemento do mundo (floresta, pantano, ruinas, emissivos).
public class ProceduralMaterialGenerator : MonoBehaviour
{
    [MenuItem("Fraga Native/1. Gerar Materiais Procedurais")]
    public static void GenerateMaterials()
    {
        string matDir = "Assets/Resources/Materials";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Materials");
        }
        
        // === BIOMA: Floresta Escura ===
        CreateBiomeMaterial(matDir, "Mat_DarkForest_Ground", 
            new Color(0.12f, 0.1f, 0.06f), // Solo escuro
            0.85f, 0.05f);
            
        CreateBiomeMaterial(matDir, "Mat_DarkForest_Bark",
            new Color(0.2f, 0.13f, 0.06f), // Casca de arvore
            0.95f, 0f);
            
        CreateBiomeMaterial(matDir, "Mat_DarkForest_Moss",
            new Color(0.08f, 0.25f, 0.05f), // Musgo humido
            0.7f, 0.02f);
        
        // === BIOMA: Prado ===
        CreateBiomeMaterial(matDir, "Mat_Meadow_Grass",
            new Color(0.25f, 0.4f, 0.1f), // Erva verde
            0.8f, 0f);
            
        CreateBiomeMaterial(matDir, "Mat_Meadow_Wildflower",
            new Color(0.6f, 0.3f, 0.5f), // Flores silvestres
            0.6f, 0.01f);
            
        CreateFoliageMaterial(matDir, "Mat_FoliageSway", new Color(0.1f, 0.35f, 0.15f));
        
        // === BIOMA: Pantano ===
        CreateBiomeMaterial(matDir, "Mat_Swamp_Mud",
            new Color(0.15f, 0.12f, 0.05f), // Lama
            0.95f, 0.15f); // Metallic = Wet look
            
        CreateBiomeMaterial(matDir, "Mat_Swamp_Water",
            new Color(0.05f, 0.15f, 0.1f), // Agua turva
            0.3f, 0.8f);
        
        // === BIOMA: Ruinas ===
        CreateBiomeMaterial(matDir, "Mat_Ruins_Stone",
            new Color(0.4f, 0.38f, 0.35f), // Pedra antiga
            0.9f, 0f);
            
        CreateBiomeMaterial(matDir, "Mat_Ruins_Iron",
            new Color(0.3f, 0.28f, 0.25f), // Ferro oxidado
            0.5f, 0.6f); // Alto metallic
        
        // === EMISSIVOS ===
        CreateEmissiveMaterial(matDir, "Mat_Emissive_RuneGlow",
            new Color(0.05f, 0.3f, 0.25f), // Base teal escuro
            new Color(0.1f, 0.9f, 0.7f), // Emissao teal brilhante
            2.0f);
            
        CreateEmissiveMaterial(matDir, "Mat_Emissive_MushroomGlow",
            new Color(0.05f, 0.2f, 0.15f), // Base verde escuro
            new Color(0.1f, 0.85f, 0.6f), // Emissao verde bioluminescente
            1.5f);
            
        CreateEmissiveMaterial(matDir, "Mat_Emissive_VultoEyes",
            new Color(0.1f, 0f, 0f), // Base vermelho escuro
            new Color(1f, 0.15f, 0.05f), // Emissao vermelho sinistro
            3.0f);
            
        CreateEmissiveMaterial(matDir, "Mat_Emissive_Campfire",
            new Color(0.3f, 0.1f, 0.02f), // Base laranja escuro
            new Color(1f, 0.5f, 0.1f), // Emissao fogo
            2.5f);
            
        CreateEmissiveMaterial(matDir, "Mat_Emissive_HolyWater",
            new Color(0.05f, 0.05f, 0.2f), // Base azul escuro
            new Color(0.3f, 0.5f, 1f), // Emissao azul sagrado
            1.8f);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ProceduralMaterialGenerator] Todos os materiais procedurais foram gerados com sucesso!");
    }
    
    private static void CreateBiomeMaterial(string dir, string name, Color baseColor, float roughness, float metallic)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null) mat = new Material(Shader.Find("Standard")); // Fallback
        
        mat.color = baseColor;
        mat.SetFloat("_Smoothness", 1f - roughness);
        mat.SetFloat("_Metallic", metallic);
        
        // Gerar textura procedural
        Texture2D tex = GenerateNoiseTexture(128, baseColor, 0.15f);
        mat.mainTexture = tex;
        
        // Normal map procedural
        Texture2D normalTex = GenerateNormalMap(128, 0.3f);
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");
        
        AssetDatabase.CreateAsset(tex, dir + "/" + name + "_Tex.asset");
        AssetDatabase.CreateAsset(normalTex, dir + "/" + name + "_Normal.asset");
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }
    
    private static void CreateEmissiveMaterial(string dir, string name, Color baseColor, Color emissionColor, float intensity)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null) mat = new Material(Shader.Find("Standard")); // Fallback
        
        mat.color = baseColor;
        mat.SetFloat("_Smoothness", 0.4f);
        mat.SetFloat("_Metallic", 0.1f);
        
        // Emissao
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor * intensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }
    
    private static void CreateFoliageMaterial(string dir, string name, Color baseColor)
    {
        Material mat = new Material(Shader.Find("Fraga/FoliageSway_Instanced"));
        if (mat.shader == null) mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); // Fallback
        
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_SwaySpeed", 2.0f);
        mat.SetFloat("_SwayAmount", 0.15f);
        mat.enableInstancing = true;
        
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }
    
    private static Texture2D GenerateNoiseTexture(int size, Color baseColor, float variation)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];
        
        for(int y=0; y<size; y++)
        {
            for(int x=0; x<size; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                float detailNoise = Mathf.PerlinNoise(x * 0.3f + 100, y * 0.3f + 100) * 0.5f;
                float combined = noise + detailNoise;
                
                Color c = baseColor;
                c.r += (combined - 0.5f) * variation;
                c.g += (combined - 0.5f) * variation;
                c.b += (combined - 0.5f) * variation;
                c.r = Mathf.Clamp01(c.r);
                c.g = Mathf.Clamp01(c.g);
                c.b = Mathf.Clamp01(c.b);
                c.a = 1f;
                
                pixels[y * size + x] = c;
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }
    
    private static Texture2D GenerateNormalMap(int size, float strength)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];
        
        float[] heightMap = new float[size * size];
        for(int y=0; y<size; y++)
        {
            for(int x=0; x<size; x++)
            {
                heightMap[y * size + x] = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
            }
        }
        
        for(int y=1; y<size-1; y++)
        {
            for(int x=1; x<size-1; x++)
            {
                float left = heightMap[y * size + (x-1)];
                float right = heightMap[y * size + (x+1)];
                float up = heightMap[(y+1) * size + x];
                float down = heightMap[(y-1) * size + x];
                
                float dx = (right - left) * strength;
                float dy = (up - down) * strength;
                
                // Encode to normal map (tangent space)
                pixels[y * size + x] = new Color(
                    dx * 0.5f + 0.5f,
                    dy * 0.5f + 0.5f,
                    1f,
                    1f
                );
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }
}
