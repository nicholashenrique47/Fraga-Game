using UnityEngine;
using UnityEditor;

/// <summary>
/// ProceduralMaterialGenerator: Cria materiais procedurais PBR com shaders URP Lit e texturas 1024x1024
/// com Alpha Cutout botânico real para eliminar a aparência tropical e replicar a fidelidade de The Forest.
/// </summary>
public class ProceduralMaterialGenerator : MonoBehaviour
{
    [MenuItem("Fraga Native/2. Gerar Materiais PBR e Texturas")]
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

        // === BIOMA: Solo Florestal Boreal/Atlântico (The Forest) ===
        CreateForestGroundMaterial(matDir, "Mat_DarkForest_Ground");
            
        CreateBarkMaterial(matDir, "Mat_DarkForest_Bark",
            new Color(0.42f, 0.30f, 0.18f), // Casca de carvalho/pinheiro maduro
            new Color(0.24f, 0.52f, 0.16f), // Musgo galego vibrante
            1.6f);
            
        CreateBarkMaterial(matDir, "Wood_Bark",
            new Color(0.45f, 0.33f, 0.20f), // Madeira de tronco caído
            new Color(0.22f, 0.48f, 0.15f),
            1.4f);
            
        CreateBiomeMaterial(matDir, "Mat_DarkForest_Moss",
            new Color(0.22f, 0.46f, 0.15f), // Manto de musgo denso e luminoso
            0.65f, 0.02f);

        // === VEGETAÇÃO / FOLIAGE COM SSS & ALPHA CUTOUT (THE FOREST) ===
        // 1. Agulhas de Pinheiro Conífero (Pinheiro-Bravo / Douglas Fir)
        CreatePineNeedleMaterial(matDir, "Mat_PineNeedle",
            new Color(0.20f, 0.44f, 0.16f), 0.65f);

        // 2. Folhagem Decídua de Carvalho e Faias (Carballo)
        CreateOakFoliageMaterial(matDir, "Mat_OakFoliage",
            new Color(0.24f, 0.50f, 0.18f), 0.70f);

        // 3. Suporte aos nomes legados e arbustos
        CreateOakFoliageMaterial(matDir, "Mat_FoliageSway", 
            new Color(0.24f, 0.50f, 0.18f), 0.70f);
            
        CreateOakFoliageMaterial(matDir, "Bush_Leaves", 
            new Color(0.20f, 0.42f, 0.14f), 0.60f);

        // === BIOMA: Prado e Clareiras ===
        CreateBiomeMaterial(matDir, "Mat_Meadow_Grass",
            new Color(0.32f, 0.52f, 0.18f), // Erva verdejante
            0.8f, 0f);
            
        CreateBiomeMaterial(matDir, "Mat_Meadow_Wildflower",
            new Color(0.68f, 0.42f, 0.58f),
            0.6f, 0.01f);

        // === BIOMA: Pântano e Águas ===
        CreateBiomeMaterial(matDir, "Mat_Swamp_Mud",
            new Color(0.28f, 0.22f, 0.14f),
            0.95f, 0.15f);
            
        CreateWaterMaterial(matDir, "Mat_Swamp_Water");
        CreateWaterMaterial(matDir, "Water_Surface");

        // === BIOMA: Ruínas Celtas & Granito Natural Claro ===
        CreateGraniteMaterial(matDir, "Mat_Ruins_Stone",
            new Color(0.62f, 0.62f, 0.65f),
            new Color(0.82f, 0.85f, 0.72f),
            new Color(0.24f, 0.50f, 0.16f));
            
        CreateGraniteMaterial(matDir, "Ruin_Stones",
            new Color(0.65f, 0.65f, 0.68f),
            new Color(0.84f, 0.86f, 0.74f),
            new Color(0.24f, 0.50f, 0.16f));

        CreateGraniteMaterial(matDir, "Celtic_Stone",
            new Color(0.60f, 0.60f, 0.64f),
            new Color(0.82f, 0.85f, 0.72f),
            new Color(0.24f, 0.50f, 0.16f));

        CreateBiomeMaterial(matDir, "Mat_Ruins_Iron",
            new Color(0.40f, 0.38f, 0.36f),
            0.45f, 0.7f);

        // === EMISSIVOS PROCEDURAIS ===
        CreateEmissiveMaterial(matDir, "Mat_Emissive_RuneGlow",
            new Color(0.04f, 0.25f, 0.22f),
            new Color(0.1f, 0.95f, 0.75f),
            2.5f);
            
        CreateEmissiveMaterial(matDir, "Mat_Emissive_MushroomGlow",
            new Color(0.08f, 0.30f, 0.18f),
            new Color(0.20f, 0.98f, 0.65f),
            2.5f);
            
        CreateEmissiveMaterial(matDir, "Mat_Emissive_VultoEyes",
            new Color(0.15f, 0f, 0f),
            new Color(1f, 0.1f, 0.05f),
            3.5f);
            
        CreateEmissiveMaterial(matDir, "Mat_Emissive_Campfire",
            new Color(0.45f, 0.18f, 0.04f),
            new Color(1f, 0.60f, 0.12f),
            3.5f);
            
        CreateEmissiveMaterial(matDir, "Mat_Emissive_HolyWater",
            new Color(0.08f, 0.14f, 0.35f),
            new Color(0.45f, 0.75f, 1f),
            2.5f);

        // === BIOMA: Materiais de Fauna / Pelagem PBR (The Forest Standard) ===
        CreateBiomeMaterial(matDir, "Wolf_Fur", new Color(0.24f, 0.24f, 0.26f), 0.35f, 0.05f);
        CreateBiomeMaterial(matDir, "Deer_Pelt", new Color(0.48f, 0.32f, 0.18f), 0.30f, 0.02f);
        CreateBiomeMaterial(matDir, "Bear_Fur", new Color(0.22f, 0.16f, 0.12f), 0.25f, 0.04f);
        CreateBiomeMaterial(matDir, "Boar_Bristles", new Color(0.26f, 0.20f, 0.16f), 0.28f, 0.04f);
        CreateBiomeMaterial(matDir, "Rabbit_Fur", new Color(0.55f, 0.52f, 0.48f), 0.20f, 0.01f);
        CreateEmissiveMaterial(matDir, "Animal_GlowingEyes", new Color(0.4f, 0.2f, 0.0f), new Color(1.0f, 0.85f, 0.2f), 3.0f);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ProceduralMaterialGenerator] Todos os materiais ultra-realistas (1024x1024) com Alpha Cutout The Forest gerados!");
    }

    private static void CreateForestGroundMaterial(string dir, string name)
    {
        Shader shader = Shader.Find("Fraga/ForestGround_PBR_Wetness");
        if (shader == null) shader = Shader.Find("Fraga/ForestGround_Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_BaseSmoothness", 0.12f);
        mat.SetFloat("_WetSmoothness", 0.88f);
        mat.SetFloat("_RockSlopeThreshold", 0.52f);
        mat.SetFloat("_PuddleDepthThreshold", 0.35f);
        mat.SetFloat("_BumpScale", 1.35f);
        mat.SetFloat("_DetailNormalScale", 0.8f);
        mat.SetFloat("_DetailTiling", 3.4f);
        mat.enableInstancing = true;

        Texture2D groundTex = GenerateForestGroundTexture(1024);
        Texture2D normalTex = GenerateForestGroundNormal(1024, 0.80f);
        mat.SetTexture("_BaseMap", groundTex);
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");

        Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Ruin_Stones_Tex.asset");
        Texture2D rockNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Ruin_Stones_Normal.asset");
        Texture2D detailNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Mat_Meadow_Grass_Normal.asset");

        if (rockTex != null) mat.SetTexture("_RockMap", rockTex);
        if (rockNormal != null) mat.SetTexture("_RockNormalMap", rockNormal);
        if (detailNormal != null) mat.SetTexture("_DetailNormalMap", detailNormal);

        AssetDatabase.CreateAsset(groundTex, dir + "/" + name + "_Tex.asset");
        AssetDatabase.CreateAsset(normalTex, dir + "/" + name + "_Normal.asset");
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }

    private static void CreateBarkMaterial(string dir, string name, Color baseColor, Color mossColor, float mossThreshold)
    {
        Shader shader = Shader.Find("Fraga/Bark_DetailNormal_Moss");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_MossColor", new Color(0.13f, 0.28f, 0.08f, 1.0f));
        mat.SetFloat("_MossHeightCutoff", 2.4f);
        mat.SetFloat("_MossHeightTransition", 0.8f);
        mat.SetFloat("_BumpScale", 1.4f);
        mat.SetFloat("_Smoothness", 0.15f);
        mat.SetFloat("_MossSmoothness", 0.08f);
        mat.enableInstancing = true;

        Texture2D barkTex = GenerateBarkTexture(1024, baseColor);
        Texture2D normalTex = GenerateBarkNormalMap(1024, 0.90f);
        mat.SetTexture("_BaseMap", barkTex);
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");

        AssetDatabase.CreateAsset(barkTex, dir + "/" + name + "_Tex.asset");
        AssetDatabase.CreateAsset(normalTex, dir + "/" + name + "_Normal.asset");
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }

    private static void CreateGraniteMaterial(string dir, string name, Color stoneColor, Color lichenColor, Color mossColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", stoneColor);
        mat.SetFloat("_Smoothness", 0.15f);
        mat.enableInstancing = true;

        Texture2D graniteTex = GenerateGraniteTexture(1024, stoneColor);
        Texture2D graniteNormal = GenerateGraniteNormalMap(1024, 0.85f);
        mat.SetTexture("_BaseMap", graniteTex);
        mat.SetTexture("_BumpMap", graniteNormal);
        mat.EnableKeyword("_NORMALMAP");

        AssetDatabase.CreateAsset(graniteTex, dir + "/" + name + "_Tex.asset");
        AssetDatabase.CreateAsset(graniteNormal, dir + "/" + name + "_Normal.asset");
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }

    private static void CreatePineNeedleMaterial(string dir, string name, Color baseColor, float translucency)
    {
        Shader shader = Shader.Find("Fraga/Foliage_TheForest_SSS");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_TranslucencyColor", new Color(0.66f, 0.78f, 0.16f, 1.0f));
        mat.SetFloat("_TranslucencyPower", 2.1f);
        mat.SetFloat("_Cutoff", 0.35f);
        mat.SetFloat("_WindBoughSpeed", 1.5f);
        mat.SetFloat("_WindLeafFlutter", 7.0f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.SetFloat("_Cull", 0f); // Double-sided
        mat.SetFloat("_Smoothness", 0.15f);
        mat.enableInstancing = true;

        Texture2D needleTex = GeneratePineNeedleTexture(1024);
        Texture2D needleNormal = GenerateFoliageNormal(needleTex, 2.0f);
        mat.SetTexture("_BaseMap", needleTex);
        mat.SetTexture("_BumpMap", needleNormal);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_NormalScale", 1.35f);
        mat.SetFloat("_Smoothness", 0.24f);

        AssetDatabase.CreateAsset(needleTex, dir + "/" + name + "_Tex.asset");
        AssetDatabase.CreateAsset(needleNormal, dir + "/" + name + "_Normal.asset");
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }

    private static void CreateOakFoliageMaterial(string dir, string name, Color baseColor, float translucency)
    {
        Shader shader = Shader.Find("Fraga/Foliage_TheForest_SSS");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_TranslucencyColor", new Color(0.75f, 0.87f, 0.22f, 1.0f));
        mat.SetFloat("_TranslucencyPower", 2.4f);
        mat.SetFloat("_Cutoff", 0.35f);
        mat.SetFloat("_WindBoughSpeed", 1.8f);
        mat.SetFloat("_WindLeafFlutter", 8.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.SetFloat("_Cull", 0f); // Double-sided
        mat.SetFloat("_Smoothness", 0.15f);
        mat.enableInstancing = true;

        Texture2D oakTex = GenerateOakFoliageTexture(1024);
        Texture2D oakNormal = GenerateFoliageNormal(oakTex, 2.4f);
        mat.SetTexture("_BaseMap", oakTex);
        mat.SetTexture("_BumpMap", oakNormal);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_NormalScale", 1.45f);
        mat.SetFloat("_Smoothness", 0.28f);

        AssetDatabase.CreateAsset(oakTex, dir + "/" + name + "_Tex.asset");
        AssetDatabase.CreateAsset(oakNormal, dir + "/" + name + "_Normal.asset");
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }

    private static void CreateWaterMaterial(string dir, string name)
    {
        Shader shader = Shader.Find("Fraga/GalicianWater_Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_DeepWaterColor", new Color(0.02f, 0.08f, 0.13f, 0.95f));
        mat.SetColor("_ShallowWaterColor", new Color(0.09f, 0.25f, 0.30f, 0.78f));
        mat.SetColor("_FoamColor", new Color(0.88f, 0.94f, 0.96f, 1f));
        mat.SetFloat("_WaveSpeed", 0.85f);
        mat.SetFloat("_WaveScale", 2.2f);
        mat.SetFloat("_WaveHeight", 0.035f);
        mat.SetFloat("_Smoothness", 0.94f);
        mat.enableInstancing = true;

        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }
    
    private static void CreateBiomeMaterial(string dir, string name, Color baseColor, float roughness, float metallic)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null) mat = new Material(Shader.Find("Standard"));
        
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 1f - roughness);
        mat.SetFloat("_Metallic", metallic);
        mat.enableInstancing = true;
        
        Texture2D tex = GenerateNoiseTexture(512, baseColor, 0.15f);
        mat.SetTexture("_BaseMap", tex);
        
        Texture2D normalTex = GenerateNormalMap(512, 0.5f);
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");
        
        AssetDatabase.CreateAsset(tex, dir + "/" + name + "_Tex.asset");
        AssetDatabase.CreateAsset(normalTex, dir + "/" + name + "_Normal.asset");
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }
    
    private static void CreateEmissiveMaterial(string dir, string name, Color baseColor, Color emissionColor, float intensity)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null) mat = new Material(Shader.Find("Standard"));
        
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 0.4f);
        mat.SetFloat("_Metallic", 0.1f);
        mat.enableInstancing = true;
        
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor * intensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        
        AssetDatabase.CreateAsset(mat, dir + "/" + name + ".mat");
    }

    private static Texture2D GenerateForestGroundTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];

        Color soilDark = new Color(0.28f, 0.22f, 0.15f);
        Color soilMedium = new Color(0.42f, 0.34f, 0.24f);
        Color pineNeedle = new Color(0.55f, 0.34f, 0.16f);
        Color mossPatch = new Color(0.28f, 0.52f, 0.18f);
        Color rottenWood = new Color(0.35f, 0.26f, 0.18f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float macro = Mathf.PerlinNoise(x * 0.012f, y * 0.012f);
                float needles = Mathf.PerlinNoise(x * 0.16f + 50f, y * 0.035f + 50f);
                float needles2 = Mathf.PerlinNoise(x * 0.035f + 80f, y * 0.16f + 80f);
                float pebbles = Mathf.PerlinNoise(x * 0.45f + 200f, y * 0.45f + 200f);
                float moss = Mathf.PerlinNoise(x * 0.028f + 120f, y * 0.028f + 120f);
                float microHumus = Mathf.PerlinNoise(x * 0.3f + 10f, y * 0.3f + 10f) * 0.2f;

                Color c = Color.Lerp(soilDark, soilMedium, macro + microHumus);

                if (needles > 0.52f || needles2 > 0.60f)
                {
                    float nWeight = Mathf.Max((needles - 0.52f) * 2.8f, (needles2 - 0.60f) * 3.0f);
                    c = Color.Lerp(c, pineNeedle, Mathf.Clamp01(nWeight));
                }

                if (moss > 0.55f)
                {
                    c = Color.Lerp(c, mossPatch, Mathf.Clamp01((moss - 0.55f) * 2.8f));
                }

                if (macro < 0.22f)
                {
                    c = Color.Lerp(c, rottenWood, (0.22f - macro) * 3f);
                }

                if (pebbles > 0.78f)
                {
                    c = Color.Lerp(c, new Color(0.65f, 0.65f, 0.68f), (pebbles - 0.78f) * 4.5f);
                }

                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    private static Texture2D GenerateForestGroundNormal(int size, float strength)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];

        float[] heightMap = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float h = Mathf.PerlinNoise(x * 0.025f, y * 0.025f);
                float needles = Mathf.PerlinNoise(x * 0.16f + 50f, y * 0.035f + 50f) * 0.45f;
                float pebbles = Mathf.PerlinNoise(x * 0.45f + 200f, y * 0.45f + 200f) * 0.35f;
                heightMap[y * size + x] = h + needles + pebbles;
            }
        }

        for (int y = 0; y < size; y++)
        {
            int ym1 = (y - 1 + size) % size;
            int yp1 = (y + 1) % size;

            for (int x = 0; x < size; x++)
            {
                int xm1 = (x - 1 + size) % size;
                int xp1 = (x + 1) % size;

                float left = heightMap[y * size + xm1];
                float right = heightMap[y * size + xp1];
                float up = heightMap[yp1 * size + x];
                float down = heightMap[ym1 * size + x];

                float dx = (right - left) * strength;
                float dy = (up - down) * strength;

                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(dx * 0.5f + 0.5f),
                    Mathf.Clamp01(dy * 0.5f + 0.5f),
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

    private static Texture2D GeneratePineNeedleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];

        Color darkNeedle = new Color(0.14f, 0.32f, 0.12f);
        Color lightNeedle = new Color(0.26f, 0.54f, 0.20f);
        Color stemColor = new Color(0.38f, 0.26f, 0.15f);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float distToCenter = Mathf.Abs(u - 0.5f);

                // Silhueta cônica do ramo de pinheiro (mais largo na base, estreito na ponta)
                float maxSpread = (1.0f - v * 0.72f) * 0.45f;

                // Haste lenhosa central
                if (distToCenter < 0.022f)
                {
                    pixels[y * size + x] = stemColor;
                    continue;
                }

                if (distToCenter > maxSpread)
                {
                    pixels[y * size + x] = new Color(0, 0, 0, 0); // Transparente (Cutout)
                    continue;
                }

                // Agulhas projetadas para fora e para cima a 40 graus
                float needleAngle = v - distToCenter * 0.58f;
                float needleWave = Mathf.Sin(needleAngle * 190.0f);
                float microFuzz = Mathf.PerlinNoise(u * 25.0f, v * 25.0f);

                if (needleWave > 0.15f && microFuzz > 0.25f)
                {
                    float tipFactor = distToCenter / maxSpread;
                    Color c = Color.Lerp(darkNeedle, lightNeedle, tipFactor);
                    c.a = 1.0f;
                    pixels[y * size + x] = c;
                }
                else
                {
                    pixels[y * size + x] = new Color(0, 0, 0, 0); // Espaço entre agulhas
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static Texture2D GenerateOakFoliageTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];

        Color darkUnderleaf = new Color(0.18f, 0.38f, 0.14f);
        Color sunnyLeaf = new Color(0.32f, 0.60f, 0.22f);
        Color veinColor = new Color(0.42f, 0.72f, 0.28f);
        Color stemColor = new Color(0.38f, 0.26f, 0.16f);

        Vector2[] leafCenters = new Vector2[]
        {
            new Vector2(0.50f, 0.85f),
            new Vector2(0.32f, 0.68f),
            new Vector2(0.68f, 0.68f),
            new Vector2(0.24f, 0.48f),
            new Vector2(0.76f, 0.48f),
            new Vector2(0.30f, 0.28f),
            new Vector2(0.70f, 0.28f),
            new Vector2(0.50f, 0.42f)
        };
        float[] leafRadii = new float[] { 0.14f, 0.16f, 0.16f, 0.17f, 0.17f, 0.16f, 0.16f, 0.15f };

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;

                // Caule central
                if (Mathf.Abs(u - 0.5f) < 0.018f && v < 0.75f)
                {
                    pixels[y * size + x] = stemColor;
                    continue;
                }

                bool insideAnyLeaf = false;
                Color leafPixel = Color.clear;

                for (int l = 0; l < leafCenters.Length; l++)
                {
                    Vector2 diff = new Vector2(u - leafCenters[l].x, v - leafCenters[l].y);
                    float dist = diff.magnitude;
                    float angle = Mathf.Atan2(diff.y, diff.x);

                    // Contorno recortado/lobado de folha de carvalho europeu
                    float lobe = 1.0f + 0.22f * Mathf.Cos(angle * 5.0f);
                    float targetRadius = leafRadii[l] * lobe;

                    if (dist < targetRadius)
                    {
                        insideAnyLeaf = true;
                        float grad = dist / targetRadius;
                        Color c = Color.Lerp(sunnyLeaf, darkUnderleaf, grad);

                        // Nervuras centrais do lóbulo
                        float vein = Mathf.Abs(Mathf.Sin(angle * 5.0f));
                        if (vein < 0.18f && dist < targetRadius * 0.75f)
                        {
                            c = Color.Lerp(c, veinColor, 0.6f);
                        }

                        c.a = 1.0f;
                        leafPixel = c;
                        break;
                    }
                }

                pixels[y * size + x] = insideAnyLeaf ? leafPixel : new Color(0, 0, 0, 0);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static Texture2D GenerateFoliageNormal(Texture2D sourceTex, float strength)
    {
        int size = sourceTex.width;
        Texture2D normalTex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] srcPixels = sourceTex.GetPixels();
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            int ym1 = Mathf.Max(0, y - 1);
            int yp1 = Mathf.Min(size - 1, y + 1);

            for (int x = 0; x < size; x++)
            {
                int xm1 = Mathf.Max(0, x - 1);
                int xp1 = Mathf.Min(size - 1, x + 1);

                Color cCenter = srcPixels[y * size + x];
                if (cCenter.a < 0.1f)
                {
                    pixels[y * size + x] = new Color(0.5f, 0.5f, 1.0f, 1.0f);
                    continue;
                }

                float hL = srcPixels[y * size + xm1].a * (0.6f + srcPixels[y * size + xm1].grayscale * 0.4f);
                float hR = srcPixels[y * size + xp1].a * (0.6f + srcPixels[y * size + xp1].grayscale * 0.4f);
                float hD = srcPixels[ym1 * size + x].a * (0.6f + srcPixels[ym1 * size + x].grayscale * 0.4f);
                float hU = srcPixels[yp1 * size + x].a * (0.6f + srcPixels[yp1 * size + x].grayscale * 0.4f);

                float dx = (hR - hL) * strength;
                float dy = (hU - hD) * strength;

                Vector3 n = new Vector3(-dx, -dy, 1.0f).normalized;

                pixels[y * size + x] = new Color(
                    n.x * 0.5f + 0.5f,
                    n.y * 0.5f + 0.5f,
                    n.z * 0.5f + 0.5f,
                    1.0f
                );
            }
        }

        normalTex.SetPixels(pixels);
        normalTex.Apply(true);
        normalTex.wrapMode = TextureWrapMode.Clamp;
        return normalTex;
    }

    private static Texture2D GenerateNoiseTexture(int size, Color baseColor, float variation)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];
        
        for(int y = 0; y < size; y++)
        {
            for(int x = 0; x < size; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.04f, y * 0.04f);
                float detailNoise = Mathf.PerlinNoise(x * 0.2f + 100, y * 0.2f + 100) * 0.5f;
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

    private static Texture2D GenerateBarkTexture(int size, Color baseColor)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];

        Color deepFurrow = baseColor * 0.65f;
        Color outerBark = baseColor * 1.30f;
        Color mossTint = new Color(0.24f, 0.50f, 0.16f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float verticalFissure = Mathf.PerlinNoise(x * 0.18f, y * 0.022f);
                float secondaryFurrow = Mathf.PerlinNoise(x * 0.45f + 80, y * 0.05f + 80) * 0.4f;
                float microGrain = Mathf.PerlinNoise(x * 0.8f + 150, y * 0.15f + 150) * 0.25f;
                float combined = verticalFissure + secondaryFurrow + microGrain;

                Color c = Color.Lerp(deepFurrow, outerBark, combined);

                float lichenSpot = Mathf.PerlinNoise(x * 0.06f + 300, y * 0.06f + 300);
                if (lichenSpot > 0.68f)
                {
                    c = Color.Lerp(c, mossTint, (lichenSpot - 0.68f) * 2.8f);
                }

                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    private static Texture2D GenerateBarkNormalMap(int size, float strength)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];

        float[] heightMap = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float vFurrow = Mathf.PerlinNoise(x * 0.18f, y * 0.022f);
                float vSub = Mathf.PerlinNoise(x * 0.45f + 80, y * 0.05f + 80) * 0.4f;
                heightMap[y * size + x] = vFurrow + vSub;
            }
        }

        for (int y = 0; y < size; y++)
        {
            int ym1 = (y - 1 + size) % size;
            int yp1 = (y + 1) % size;

            for (int x = 0; x < size; x++)
            {
                int xm1 = (x - 1 + size) % size;
                int xp1 = (x + 1) % size;

                float left = heightMap[y * size + xm1];
                float right = heightMap[y * size + xp1];
                float up = heightMap[yp1 * size + x];
                float down = heightMap[ym1 * size + x];

                float dx = (right - left) * strength;
                float dy = (up - down) * strength * 0.25f;

                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(dx * 0.5f + 0.5f),
                    Mathf.Clamp01(dy * 0.5f + 0.5f),
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

    private static Texture2D GenerateGraniteTexture(int size, Color stoneColor)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float macro = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                float flecks = Mathf.PerlinNoise(x * 0.4f + 200, y * 0.4f + 200);
                float quartz = Mathf.PerlinNoise(x * 0.75f + 350, y * 0.75f + 350);

                Color c = Color.Lerp(stoneColor * 0.85f, stoneColor * 1.18f, macro);

                if (flecks > 0.65f)
                {
                    c = Color.Lerp(c, new Color(0.20f, 0.20f, 0.22f), (flecks - 0.65f) * 2.2f);
                }

                if (quartz > 0.70f)
                {
                    c = Color.Lerp(c, new Color(0.92f, 0.92f, 0.95f), (quartz - 0.70f) * 2.5f);
                }

                c.a = 1f;
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    private static Texture2D GenerateGraniteNormalMap(int size, float strength)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];

        float[] heightMap = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float macro = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                float chips = Mathf.PerlinNoise(x * 0.25f + 100, y * 0.25f + 100) * 0.5f;
                float micro = Mathf.PerlinNoise(x * 0.6f + 250, y * 0.6f + 250) * 0.25f;
                heightMap[y * size + x] = macro + chips + micro;
            }
        }

        for (int y = 0; y < size; y++)
        {
            int ym1 = (y - 1 + size) % size;
            int yp1 = (y + 1) % size;

            for (int x = 0; x < size; x++)
            {
                int xm1 = (x - 1 + size) % size;
                int xp1 = (x + 1) % size;

                float left = heightMap[y * size + xm1];
                float right = heightMap[y * size + xp1];
                float up = heightMap[yp1 * size + x];
                float down = heightMap[ym1 * size + x];

                float dx = (right - left) * strength;
                float dy = (up - down) * strength;

                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(dx * 0.5f + 0.5f),
                    Mathf.Clamp01(dy * 0.5f + 0.5f),
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
    
    private static Texture2D GenerateNormalMap(int size, float strength)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];
        
        float[] heightMap = new float[size * size];
        for(int y = 0; y < size; y++)
        {
            for(int x = 0; x < size; x++)
            {
                heightMap[y * size + x] = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
            }
        }
        
        for(int y = 0; y < size; y++)
        {
            int ym1 = (y - 1 + size) % size;
            int yp1 = (y + 1) % size;

            for(int x = 0; x < size; x++)
            {
                int xm1 = (x - 1 + size) % size;
                int xp1 = (x + 1) % size;

                float left = heightMap[y * size + xm1];
                float right = heightMap[y * size + xp1];
                float up = heightMap[yp1 * size + x];
                float down = heightMap[ym1 * size + x];
                
                float dx = (right - left) * strength;
                float dy = (up - down) * strength;
                
                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(dx * 0.5f + 0.5f),
                    Mathf.Clamp01(dy * 0.5f + 0.5f),
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
