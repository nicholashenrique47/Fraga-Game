using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// ProceduralMaterialGenerator: Configura materiais PBR para Fraga: A Noite Esquecida
/// usando texturas PNG 1024x1024 de alta definição (Assets/Art/Textures) com normal maps
/// calibrados via TextureImporter, fallbacks procedurais e shaders customizados URP (Forward & Forward+).
/// </summary>
public class ProceduralMaterialGenerator : MonoBehaviour
{
    private const string TEX_DIR = "Assets/Art/Textures";

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
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Textures"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Textures");
        }

        AssetDatabase.Refresh();

        // Copia texturas usadas em runtime (partículas suaves e superfície lunar) para Assets/Resources/Textures
        CopyRuntimeResourceTextures();

        // === BIOMA: Solo Florestal Boreal/Atlântico (The Forest) ===
        CreateForestGroundMaterial(matDir, "Mat_DarkForest_Ground");

        CreateBarkMaterial(matDir, "Mat_DarkForest_Bark",
            "oak_bark_albedo.png", "oak_bark_normal.png",
            new Color(0.92f, 0.90f, 0.87f),
            new Color(0.15f, 0.33f, 0.10f),
            3.0f);

        CreateBarkMaterial(matDir, "Wood_Bark",
            "pine_bark_albedo.png", "pine_bark_normal.png",
            new Color(0.94f, 0.90f, 0.86f),
            new Color(0.15f, 0.32f, 0.10f),
            2.2f);

        CreateMossMaterial(matDir, "Mat_DarkForest_Moss");

        // === VEGETAÇÃO / FOLIAGE COM SSS & ALPHA CUTOUT (THE FOREST) ===
        CreatePineNeedleMaterial(matDir, "Mat_PineNeedle",
            new Color(0.94f, 0.98f, 0.92f), 0.72f);

        CreateOakFoliageMaterial(matDir, "Mat_OakFoliage",
            "oak_foliage_albedo.png", "oak_foliage_normal.png",
            new Color(0.92f, 0.96f, 0.88f), 0.75f);

        CreateOakFoliageMaterial(matDir, "Mat_FoliageSway",
            "oak_foliage_albedo.png", "oak_foliage_normal.png",
            new Color(0.90f, 0.95f, 0.86f), 0.72f);

        CreateOakFoliageMaterial(matDir, "Bush_Leaves",
            "oak_foliage_albedo.png", "oak_foliage_normal.png",
            new Color(0.84f, 0.90f, 0.80f), 0.65f);

        CreateOakFoliageMaterial(matDir, "Mat_Fern_Frond",
            "fern_frond_albedo.png", "fern_frond_normal.png",
            new Color(0.94f, 0.98f, 0.90f), 0.80f, 0.30f, 0.14f);

        CreateOakFoliageMaterial(matDir, "Mat_Grass_Tuft",
            "grass_tuft_albedo.png", "grass_tuft_normal.png",
            new Color(0.92f, 0.96f, 0.88f), 0.82f, 0.26f, 0.16f);

        // === BIOMA: Prado e Clareiras ===
        CreateBiomeMaterial(matDir, "Mat_Meadow_Grass",
            new Color(0.32f, 0.52f, 0.18f), 0.8f, 0f);

        CreateBiomeMaterial(matDir, "Mat_Meadow_Wildflower",
            new Color(0.68f, 0.42f, 0.58f), 0.6f, 0.01f);

        CreateBiomeMaterial(matDir, "Mat_Herb_Leaf",
            new Color(0.22f, 0.52f, 0.17f), 0.58f, 0.0f);

        // Flores de Tojo galego e bagas silvestres
        CreateToxoBloomMaterial(matDir, "Mat_Toxo_Bloom");
        CreateBiomeMaterial(matDir, "Mat_Berry_Fruit",
            new Color(0.16f, 0.04f, 0.26f), 0.22f, 0.02f);

        // === BIOMA: Pântano e Águas ===
        CreateBiomeMaterial(matDir, "Mat_Swamp_Mud",
            new Color(0.28f, 0.22f, 0.14f), 0.95f, 0.15f);

        CreateWaterMaterial(matDir, "Mat_Swamp_Water");
        CreateWaterMaterial(matDir, "Water_Surface");

        // === BIOMA: Ruínas Celtas & Granito Galego com Musgo/Líquenes ===
        CreateGraniteMaterial(matDir, "Mat_Ruins_Stone",
            new Color(0.88f, 0.89f, 0.91f),
            new Color(0.72f, 0.78f, 0.64f),
            new Color(0.15f, 0.34f, 0.11f));

        CreateGraniteMaterial(matDir, "Ruin_Stones",
            new Color(0.90f, 0.91f, 0.93f),
            new Color(0.74f, 0.80f, 0.66f),
            new Color(0.15f, 0.34f, 0.11f));

        CreateGraniteMaterial(matDir, "Celtic_Stone",
            new Color(0.86f, 0.88f, 0.90f),
            new Color(0.70f, 0.76f, 0.62f),
            new Color(0.16f, 0.35f, 0.11f));

        // === FASE 2: Materiais PBR HD para Armas, Ferramentas, Estruturas e Personagens ===
        CreatePBRTextureMaterial(matDir, "Mat_Ruins_Iron",
            "forged_iron_albedo.png", "forged_iron_normal.png",
            new Color(0.82f, 0.84f, 0.87f), 0.56f, 0.86f, 512, 1.25f);

        CreatePBRTextureMaterial(matDir, "Mat_Brass_Lantern",
            "forged_iron_albedo.png", "forged_iron_normal.png",
            new Color(0.78f, 0.58f, 0.26f), 0.62f, 0.88f, 512, 1.0f);

        CreateLanternGlassMaterial(matDir, "Mat_Lantern_Glass");

        CreatePBRTextureMaterial(matDir, "Mat_Wood_Carved",
            "carved_wood_albedo.png", "carved_wood_normal.png",
            new Color(0.94f, 0.90f, 0.86f), 0.34f, 0.02f, 512, 1.25f);

        CreatePBRTextureMaterial(matDir, "Mat_Leather_Wrap",
            "leather_wrap_albedo.png", "leather_wrap_normal.png",
            new Color(0.92f, 0.88f, 0.84f), 0.32f, 0.03f, 512, 1.20f);

        CreatePBRTextureMaterial(matDir, "Mat_Linen_Fabric",
            "linen_fabric_albedo.png", "linen_fabric_normal.png",
            new Color(0.95f, 0.94f, 0.90f), 0.18f, 0.0f, 512, 1.15f);

        CreatePBRTextureMaterial(matDir, "Mat_Player_DarkCloth",
            "linen_fabric_albedo.png", "linen_fabric_normal.png",
            new Color(0.24f, 0.26f, 0.23f), 0.18f, 0.0f, 512, 1.15f);

        CreateBiomeMaterial(matDir, "Mat_Player_Skin",
            new Color(0.78f, 0.58f, 0.46f), 0.72f, 0.0f);

        CreatePBRTextureMaterial(matDir, "Mat_Farmland_Soil",
            "farmland_soil_albedo.png", "farmland_soil_normal.png",
            Color.white, 0.28f, 0.0f, 512, 1.35f);

        CreatePBRTextureMaterial(matDir, "Mat_Bone_Horn",
            "bone_horn_albedo.png", "bone_horn_normal.png",
            Color.white, 0.36f, 0.02f, 512, 1.20f);

        // === EMISSIVOS & ENTIDADES SOMBRIAS ===
        CreateVultoShadowMaterial(matDir, "Mat_Vulto_Cloak");

        CreateEmissiveMaterial(matDir, "Mat_Emissive_RuneGlow",
            new Color(0.04f, 0.25f, 0.22f),
            new Color(0.10f, 0.95f, 0.75f), 2.5f);

        CreateEmissiveMaterial(matDir, "Mat_Emissive_MushroomGlow",
            new Color(0.08f, 0.30f, 0.18f),
            new Color(0.20f, 0.98f, 0.65f), 2.5f);

        CreateEmissiveMaterial(matDir, "Mat_Emissive_VultoEyes",
            new Color(0.15f, 0f, 0f),
            new Color(1f, 0.1f, 0.05f), 3.8f);

        CreateEmissiveMaterial(matDir, "Mat_Stalker_Eyes",
            new Color(0.85f, 0.92f, 1.0f),
            new Color(0.90f, 0.96f, 1.0f), 4.0f);

        CreateEmissiveMaterial(matDir, "Mat_Emissive_Campfire",
            new Color(0.45f, 0.18f, 0.04f),
            new Color(1f, 0.60f, 0.12f), 3.5f);

        CreateEmissiveMaterial(matDir, "Mat_Emissive_HolyWater",
            new Color(0.08f, 0.14f, 0.35f),
            new Color(0.45f, 0.75f, 1f), 2.5f);

        CreateEmissiveMaterial(matDir, "Mat_Emissive_BeaconGlow",
            new Color(0.12f, 0.32f, 0.45f),
            new Color(0.25f, 0.82f, 1.0f), 3.2f);

        // === BIOMA: Materiais de Fauna / Pelagem PBR HD (The Forest Standard) ===
        CreatePBRTextureMaterial(matDir, "Wolf_Fur",
            "wolf_fur_albedo.png", "wolf_fur_normal.png",
            Color.white, 0.28f, 0.02f, 512, 1.25f);

        CreatePBRTextureMaterial(matDir, "Deer_Pelt",
            "deer_pelt_albedo.png", "deer_pelt_normal.png",
            new Color(1.0f, 0.97f, 0.94f), 0.30f, 0.01f, 512, 1.20f);

        CreatePBRTextureMaterial(matDir, "Bear_Fur",
            "bear_fur_albedo.png", "bear_fur_normal.png",
            Color.white, 0.28f, 0.02f, 512, 1.30f);

        CreatePBRTextureMaterial(matDir, "Boar_Bristles",
            "boar_bristles_albedo.png", "boar_bristles_normal.png",
            Color.white, 0.26f, 0.02f, 512, 1.35f);

        CreatePBRTextureMaterial(matDir, "Rabbit_Fur",
            "rabbit_fur_albedo.png", "rabbit_fur_normal.png",
            Color.white, 0.22f, 0.0f, 512, 1.15f);

        CreatePBRTextureMaterial(matDir, "Crow_Feathers",
            "crow_feathers_albedo.png", "crow_feathers_normal.png",
            Color.white, 0.42f, 0.08f, 512, 1.35f);

        CreatePBRTextureMaterial(matDir, "Eagle_Plumage",
            "eagle_plumage_albedo.png", "eagle_plumage_normal.png",
            Color.white, 0.34f, 0.03f, 512, 1.30f);

        CreateEmissiveMaterial(matDir, "Animal_GlowingEyes",
            new Color(0.4f, 0.2f, 0.0f), new Color(1.0f, 0.85f, 0.2f), 3.0f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ProceduralMaterialGenerator] Todos os materiais PBR HD (Ambiente + Entidades + Fauna + Armas + Props) gerados!");
    }

    private static void CopyRuntimeResourceTextures()
    {
        string[] files = { "soft_particle_glow.png", "soft_mist_puff.png", "moon_surface_albedo.png" };
        bool copiedAny = false;
        foreach (string f in files)
        {
            string src = $"{TEX_DIR}/{f}";
            string dst = $"Assets/Resources/Textures/{f}";
            if (File.Exists(src))
            {
                File.Copy(src, dst, true);
                copiedAny = true;
            }
        }
        if (copiedAny) AssetDatabase.Refresh();

        foreach (string f in files)
        {
            string dst = $"Assets/Resources/Textures/{f}";
            if (!File.Exists(dst)) continue;
            TextureImporter importer = AssetImporter.GetAtPath(dst) as TextureImporter;
            if (importer == null) continue;

            bool isParticle = f != "moon_surface_albedo.png";
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; dirty = true; }
            if (importer.maxTextureSize != 256) { importer.maxTextureSize = 256; dirty = true; }
            if (isParticle)
            {
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            }
            if (dirty) importer.SaveAndReimport();
        }
    }

    private static void CreatePBRTextureMaterial(string dir, string name, string albedoPng, string normalPng, Color tint, float smoothness, float metallic, int maxSize = 512, float bumpScale = 1.2f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_BumpScale", bumpScale);
        mat.enableInstancing = true;

        Texture2D albedo = LoadPBRTexture(albedoPng, false, false, maxSize);
        Texture2D normal = LoadPBRTexture(normalPng, true, false, maxSize);
        if (albedo == null)
        {
            albedo = GenerateNoiseTexture(512, tint, 0.12f);
            AssetDatabase.CreateAsset(albedo, $"{dir}/{name}_Tex.asset");
        }
        if (normal == null)
        {
            normal = GenerateNormalMap(512, 0.6f);
            AssetDatabase.CreateAsset(normal, $"{dir}/{name}_Normal.asset");
        }

        mat.SetTexture("_BaseMap", albedo);
        mat.SetTexture("_BumpMap", normal);
        mat.EnableKeyword("_NORMALMAP");

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateLanternGlassMaterial(string dir, string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.98f, 0.93f, 0.80f, 0.36f));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.SetFloat("_Smoothness", 0.94f);
        mat.SetFloat("_Metallic", 0.05f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.48f, 0.30f, 0.08f));
        mat.enableInstancing = true;

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateVultoShadowMaterial(string dir, string name)
    {
        Shader shader = Shader.Find("Fraga/VultoShadow_Instanced");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.03f, 0.02f, 0.04f, 0.92f));
        if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", new Color(0.18f, 0.14f, 0.26f, 0.85f));
        if (mat.HasProperty("_EyeColor")) mat.SetColor("_EyeColor", new Color(2.2f, 0.14f, 0.06f, 1.0f));
        if (mat.HasProperty("_JitterSpeed")) mat.SetFloat("_JitterSpeed", 9.0f);
        if (mat.HasProperty("_JitterAmount")) mat.SetFloat("_JitterAmount", 0.025f);
        if (mat.HasProperty("_WispSpeed")) mat.SetFloat("_WispSpeed", 1.8f);
        if (mat.HasProperty("_FresnelPower")) mat.SetFloat("_FresnelPower", 2.6f);
        mat.enableInstancing = true;

        SaveMaterialAsset(mat, dir, name);
    }

    private static Texture2D SaveProceduralTextureAsPNG(Texture2D tempTex, string fileName, bool isNormalMap, int maxSize = 512)
    {
        if (tempTex == null) return null;
        string path = $"{TEX_DIR}/{fileName}";
        if (!File.Exists(path))
        {
            if (!Directory.Exists(TEX_DIR)) Directory.CreateDirectory(TEX_DIR);
            File.WriteAllBytes(path, tempTex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        DestroyImmediate(tempTex);
        return LoadPBRTexture(fileName, isNormalMap, false, maxSize);
    }

    private static Texture2D LoadPBRTexture(string fileName, bool isNormalMap, bool isCutoutAlpha, int maxSize = 1024)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        string path = $"{TEX_DIR}/{fileName}";
        if (!System.IO.File.Exists(path)) return null;

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }

        if (importer != null)
        {
            bool dirty = false;
            TextureImporterType desiredType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != desiredType) { importer.textureType = desiredType; dirty = true; }
            if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; dirty = true; }
            if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; dirty = true; }
            if (!importer.streamingMipmaps) { importer.streamingMipmaps = true; dirty = true; }

            if (isCutoutAlpha)
            {
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            }
            else
            {
                if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                if (importer.anisoLevel < 4) { importer.anisoLevel = 4; dirty = true; }
            }

            TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
            int androidMax = Mathf.Min(maxSize, 512);
            if (!androidSettings.overridden || androidSettings.maxTextureSize > androidMax || androidSettings.textureCompression != TextureImporterCompression.Compressed)
            {
                androidSettings.name = "Android";
                androidSettings.overridden = true;
                androidSettings.maxTextureSize = androidMax;
                androidSettings.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(androidSettings);
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void SaveMaterialAsset(Material mat, string dir, string name)
    {
        string matPath = $"{dir}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null)
        {
            existing.shader = mat.shader;
            existing.CopyPropertiesFromMaterial(mat);
            EditorUtility.SetDirty(existing);
            DestroyImmediate(mat);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, matPath);
        }
    }

    private static void CreateForestGroundMaterial(string dir, string name)
    {
        Shader shader = Shader.Find("Fraga/ForestGround_PBR_Wetness");
        if (shader == null) shader = Shader.Find("Fraga/ForestGround_Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_BaseSmoothness", 0.14f);
        mat.SetFloat("_WetSmoothness", 0.90f);
        mat.SetFloat("_RockSlopeThreshold", 0.42f);
        mat.SetFloat("_PuddleDepthThreshold", 0.35f);
        mat.SetFloat("_RockTriplanarScale", 0.35f);
        mat.SetFloat("_BumpScale", 1.35f);
        mat.SetFloat("_DetailNormalScale", 0.85f);
        mat.SetFloat("_DetailTiling", 3.4f);
        mat.enableInstancing = true;

        Texture2D groundTex = LoadPBRTexture("forest_ground_albedo.png", false, false, 1024);
        Texture2D normalTex = LoadPBRTexture("forest_ground_normal.png", true, false, 1024);
        if (groundTex == null)
        {
            groundTex = GenerateForestGroundTexture(1024);
            AssetDatabase.CreateAsset(groundTex, $"{dir}/{name}_Tex.asset");
        }
        if (normalTex == null)
        {
            normalTex = GenerateForestGroundNormal(1024, 0.80f);
            AssetDatabase.CreateAsset(normalTex, $"{dir}/{name}_Normal.asset");
        }

        mat.SetTexture("_BaseMap", groundTex);
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");

        Texture2D rockTex = LoadPBRTexture("granite_albedo.png", false, false, 1024);
        Texture2D rockNormal = LoadPBRTexture("granite_normal.png", true, false, 1024);
        Texture2D detailNormal = LoadPBRTexture("moss_normal.png", true, false, 512);

        if (rockTex != null) mat.SetTexture("_RockMap", rockTex);
        if (rockNormal != null) mat.SetTexture("_RockNormalMap", rockNormal);
        if (detailNormal != null) mat.SetTexture("_DetailNormalMap", detailNormal);

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateBarkMaterial(string dir, string name, string albedoPng, string normalPng, Color baseColor, Color mossColor, float mossThreshold)
    {
        Shader shader = Shader.Find("Fraga/Bark_DetailNormal_Moss");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_MossColor", mossColor);
        mat.SetFloat("_MossHeightCutoff", mossThreshold);
        mat.SetFloat("_MossHeightTransition", 1.1f);
        mat.SetFloat("_BumpScale", 1.45f);
        mat.SetFloat("_Smoothness", 0.14f);
        mat.SetFloat("_MossSmoothness", 0.08f);
        mat.SetFloat("_WindSway", name == "Wood_Bark" ? 0.0f : 0.035f);
        mat.enableInstancing = true;

        Texture2D barkTex = LoadPBRTexture(albedoPng, false, false, 1024);
        Texture2D normalTex = LoadPBRTexture(normalPng, true, false, 1024);
        if (barkTex == null)
        {
            barkTex = GenerateBarkTexture(1024, baseColor);
            AssetDatabase.CreateAsset(barkTex, $"{dir}/{name}_Tex.asset");
        }
        if (normalTex == null)
        {
            normalTex = GenerateBarkNormalMap(1024, 0.90f);
            AssetDatabase.CreateAsset(normalTex, $"{dir}/{name}_Normal.asset");
        }

        mat.SetTexture("_BaseMap", barkTex);
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateMossMaterial(string dir, string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.88f, 0.95f, 0.85f));
        mat.SetFloat("_Smoothness", 0.14f);
        mat.SetFloat("_Metallic", 0.02f);
        mat.enableInstancing = true;

        Texture2D tex = LoadPBRTexture("moss_albedo.png", false, false, 512);
        Texture2D normalTex = LoadPBRTexture("moss_normal.png", true, false, 512);
        if (tex == null)
        {
            tex = GenerateNoiseTexture(512, new Color(0.22f, 0.46f, 0.15f), 0.15f);
            AssetDatabase.CreateAsset(tex, $"{dir}/{name}_Tex.asset");
        }
        if (normalTex == null)
        {
            normalTex = GenerateNormalMap(512, 0.6f);
            AssetDatabase.CreateAsset(normalTex, $"{dir}/{name}_Normal.asset");
        }

        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateGraniteMaterial(string dir, string name, Color stoneColor, Color lichenColor, Color mossColor)
    {
        Shader shader = Shader.Find("Fraga/RealisticGranite_Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", stoneColor);
        mat.SetColor("_LichenColor", lichenColor);
        mat.SetColor("_MossColor", mossColor);
        mat.SetFloat("_BumpScale", 1.45f);
        mat.SetFloat("_MossThreshold", 0.75f);
        mat.SetFloat("_Smoothness", 0.16f);
        mat.SetFloat("_WetSmoothness", 0.84f);
        mat.SetFloat("_TriplanarBlend", 0.65f);
        mat.enableInstancing = true;

        Texture2D graniteTex = LoadPBRTexture("granite_albedo.png", false, false, 1024);
        Texture2D graniteNormal = LoadPBRTexture("granite_normal.png", true, false, 1024);
        if (graniteTex == null)
        {
            graniteTex = GenerateGraniteTexture(1024, stoneColor);
            AssetDatabase.CreateAsset(graniteTex, $"{dir}/{name}_Tex.asset");
        }
        if (graniteNormal == null)
        {
            graniteNormal = GenerateGraniteNormalMap(1024, 0.85f);
            AssetDatabase.CreateAsset(graniteNormal, $"{dir}/{name}_Normal.asset");
        }

        mat.SetTexture("_BaseMap", graniteTex);
        if (mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", graniteNormal);
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", graniteNormal);
        mat.EnableKeyword("_NORMALMAP");

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreatePineNeedleMaterial(string dir, string name, Color baseColor, float translucency)
    {
        Shader shader = Shader.Find("Fraga/Foliage_TheForest_SSS");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_TranslucencyColor", new Color(0.66f, 0.82f, 0.18f, 1.0f));
        mat.SetFloat("_TranslucencyPower", 2.1f);
        mat.SetFloat("_Cutoff", 0.34f);
        mat.SetFloat("_WindBoughSpeed", 1.5f);
        mat.SetFloat("_WindLeafFlutter", 7.0f);
        mat.SetFloat("_WindStrength", 0.10f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.SetFloat("_Cull", 0f);
        mat.enableInstancing = true;

        Texture2D needleTex = LoadPBRTexture("pine_needle_albedo.png", false, true, 1024);
        Texture2D needleNormal = LoadPBRTexture("pine_needle_normal.png", true, true, 1024);
        if (needleTex == null)
        {
            needleTex = GeneratePineNeedleTexture(1024);
            AssetDatabase.CreateAsset(needleTex, $"{dir}/{name}_Tex.asset");
        }
        if (needleNormal == null)
        {
            needleNormal = GenerateFoliageNormal(needleTex, 2.0f);
            AssetDatabase.CreateAsset(needleNormal, $"{dir}/{name}_Normal.asset");
        }

        mat.SetTexture("_BaseMap", needleTex);
        mat.SetTexture("_BumpMap", needleNormal);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_NormalScale", 1.30f);
        mat.SetFloat("_Smoothness", 0.24f);

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateOakFoliageMaterial(string dir, string name, string albedoPng, string normalPng, Color baseColor, float translucency, float cutoff = 0.34f, float windStrength = 0.12f)
    {
        Shader shader = Shader.Find("Fraga/Foliage_TheForest_SSS");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_TranslucencyColor", new Color(0.75f, 0.88f, 0.22f, 1.0f));
        mat.SetFloat("_TranslucencyPower", 2.3f);
        mat.SetFloat("_Cutoff", cutoff);
        mat.SetFloat("_WindBoughSpeed", 1.8f);
        mat.SetFloat("_WindLeafFlutter", 8.5f);
        mat.SetFloat("_WindStrength", windStrength);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        mat.SetFloat("_Cull", 0f);
        mat.enableInstancing = true;

        int maxSz = (name.Contains("Fern") || name.Contains("Grass")) ? 512 : 1024;
        Texture2D oakTex = LoadPBRTexture(albedoPng, false, true, maxSz);
        Texture2D oakNormal = LoadPBRTexture(normalPng, true, true, maxSz);
        if (oakTex == null)
        {
            oakTex = GenerateOakFoliageTexture(1024);
            AssetDatabase.CreateAsset(oakTex, $"{dir}/{name}_Tex.asset");
        }
        if (oakNormal == null)
        {
            oakNormal = GenerateFoliageNormal(oakTex, 2.4f);
            AssetDatabase.CreateAsset(oakNormal, $"{dir}/{name}_Normal.asset");
        }

        mat.SetTexture("_BaseMap", oakTex);
        mat.SetTexture("_BumpMap", oakNormal);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_NormalScale", 1.35f);
        mat.SetFloat("_Smoothness", 0.26f);

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateToxoBloomMaterial(string dir, string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.96f, 0.78f, 0.08f));
        mat.SetFloat("_Smoothness", 0.38f);
        mat.SetFloat("_Metallic", 0.0f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.32f, 0.24f, 0.02f));
        mat.enableInstancing = true;

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateWaterMaterial(string dir, string name)
    {
        Shader shader = Shader.Find("Fraga/GalicianWater_Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_DeepWaterColor", new Color(0.02f, 0.08f, 0.13f, 0.94f));
        mat.SetColor("_ShallowWaterColor", new Color(0.09f, 0.25f, 0.30f, 0.74f));
        mat.SetColor("_FoamColor", new Color(0.88f, 0.94f, 0.96f, 1f));
        mat.SetFloat("_WaveSpeed", 0.85f);
        mat.SetFloat("_WaveScale", 2.4f);
        mat.SetFloat("_WaveHeight", 0.035f);
        mat.SetFloat("_Smoothness", 0.94f);
        mat.enableInstancing = true;

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateBiomeMaterial(string dir, string name, Color baseColor, float roughness, float metallic)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null) mat = new Material(Shader.Find("Standard"));

        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 1f - roughness);
        mat.SetFloat("_Metallic", metallic);
        mat.enableInstancing = true;

        string slug = name.StartsWith("Mat_") ? name.Substring(4).ToLowerInvariant() : name.ToLowerInvariant();
        Texture2D tex = SaveProceduralTextureAsPNG(GenerateNoiseTexture(512, baseColor, 0.15f), $"{slug}_albedo.png", false, 512);
        mat.SetTexture("_BaseMap", tex);

        Texture2D normalTex = SaveProceduralTextureAsPNG(GenerateNormalMap(512, 0.5f), $"{slug}_normal.png", true, 512);
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");

        SaveMaterialAsset(mat, dir, name);
    }

    private static void CreateEmissiveMaterial(string dir, string name, Color baseColor, Color emissionColor, float intensity)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null) mat = new Material(Shader.Find("Standard"));

        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 0.45f);
        mat.SetFloat("_Metallic", 0.1f);
        mat.enableInstancing = true;

        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor * intensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        SaveMaterialAsset(mat, dir, name);
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
                float maxSpread = (1.0f - v * 0.72f) * 0.45f;

                if (distToCenter < 0.022f)
                {
                    pixels[y * size + x] = stemColor;
                    continue;
                }

                if (distToCenter > maxSpread)
                {
                    pixels[y * size + x] = new Color(0, 0, 0, 0);
                    continue;
                }

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
                    pixels[y * size + x] = new Color(0, 0, 0, 0);
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

                    float lobe = 1.0f + 0.22f * Mathf.Cos(angle * 5.0f);
                    float targetRadius = leafRadii[l] * lobe;

                    if (dist < targetRadius)
                    {
                        insideAnyLeaf = true;
                        float grad = dist / targetRadius;
                        Color c = Color.Lerp(sunnyLeaf, darkUnderleaf, grad);

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

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
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
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                heightMap[y * size + x] = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
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
}
