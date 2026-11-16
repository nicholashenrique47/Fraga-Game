using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Gerenciador de Otimizacao Hibrida PC + Mobile 60 FPS (The Forest AAA Standard).
/// Suporta presets persistentes Baixo / Medio / Alto via PlayerPrefs ("Fraga_QualityTier").
/// </summary>
[ExecuteAlways]
public class GalicianOptimizationManager : MonoBehaviour
{
    public static GalicianOptimizationManager Instance { get; private set; }

    public enum QualityTier
    {
        Baixo = 0,
        Medio = 1,
        Alto = 2
    }

    public const string QualityPrefKey = "Fraga_QualityTier";

    [Header("Taxa de Quadros")]
    public int lowTargetFrameRate = 30;
    public int targetFrameRate = 60;
    public int pcTargetFrameRate = 120;
    public bool disableVSyncForLowLatency = true;

    [Header("Texture Streaming")]
    public bool enableTextureStreaming = true;
    public int lowTextureStreamingMemoryPoolMB = 512;
    public int textureStreamingMemoryPoolMB = 768;
    public int pcTextureStreamingMemoryPoolMB = 2048;

    [Header("Sombras Mobile (Performance)")]
    public float lowShadowDistance = 35.0f;
    public float mobileShadowDistance = 55.0f;
    public int mobileShadowCascades = 2;

    [Header("Sombras PC (Alta Fidelidade)")]
    public float pcShadowDistance = 115.0f;
    public int pcShadowCascades = 4;

    [Header("Culling de Camadas (Base Medio)")]
    public float smallFoliageCullDist = 45.0f;
    public float propsCullDist = 55.0f;
    public float wildlifeCullDist = 70.0f;
    public float largeTreesCullDist = 310.0f;

    public QualityTier CurrentQualityTier { get; private set; } = QualityTier.Medio;
    private readonly float[] cachedCullDistances = new float[32];

    void Awake()
    {
        if (Instance == null) Instance = this;

        int defaultTier = Application.isMobilePlatform ? (int)QualityTier.Medio : (int)QualityTier.Alto;
        int savedTier = PlayerPrefs.GetInt(QualityPrefKey, defaultTier);
        CurrentQualityTier = (QualityTier)Mathf.Clamp(savedTier, 0, 2);

        ApplyFramerateAndQualitySettings();
        ApplyCameraVisibilityAndCulling();
        ApplyURPOptimizations();
    }

    void Start()
    {
        ApplyCameraVisibilityAndCulling();
        ApplyURPOptimizations();
    }

    public void ApplyQualityPreset(int tierIndex)
    {
        CurrentQualityTier = (QualityTier)Mathf.Clamp(tierIndex, 0, 2);
        PlayerPrefs.SetInt(QualityPrefKey, (int)CurrentQualityTier);
        PlayerPrefs.Save();

        ApplyFramerateAndQualitySettings();
        ApplyCameraVisibilityAndCulling();
        ApplyURPOptimizations();
    }

    public void ApplyFramerateAndQualitySettings()
    {
        int fps = CurrentQualityTier == QualityTier.Baixo ? lowTargetFrameRate
            : (CurrentQualityTier == QualityTier.Medio ? targetFrameRate : pcTargetFrameRate);
        Application.targetFrameRate = fps;
        QualitySettings.vSyncCount = disableVSyncForLowLatency ? 0 : 1;
        QualitySettings.lodBias = CurrentQualityTier == QualityTier.Baixo ? 0.75f
            : (CurrentQualityTier == QualityTier.Medio ? 1.0f : 1.5f);

        if (enableTextureStreaming)
        {
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = CurrentQualityTier == QualityTier.Baixo
                ? lowTextureStreamingMemoryPoolMB
                : (CurrentQualityTier == QualityTier.Medio ? textureStreamingMemoryPoolMB : pcTextureStreamingMemoryPoolMB);
            QualitySettings.streamingMipmapsMaxLevelReduction = CurrentQualityTier == QualityTier.Baixo ? 3
                : (CurrentQualityTier == QualityTier.Medio ? 2 : 0);
            QualitySettings.streamingMipmapsAddAllCameras = true;
        }

        Time.fixedDeltaTime = 0.02f;
    }

    public void ApplyCullingDistances()
    {
        ApplyCameraVisibilityAndCulling();
    }

    public void ApplyCameraVisibilityAndCulling()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null) return;

        cam.orthographic = false;
        cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, 55f, 75f);
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.45f, 0.62f, 0.85f, 1f);
        cam.nearClipPlane = 0.15f;

        float desiredFar = CurrentQualityTier == QualityTier.Baixo ? 380f
            : (CurrentQualityTier == QualityTier.Medio ? 480f : 720f);
        if (cam.farClipPlane < 360f || Application.isPlaying) cam.farClipPlane = desiredFar;

        for (int i = 0; i < 32; i++)
        {
            cachedCullDistances[i] = 0f;
        }

        float mult = CurrentQualityTier == QualityTier.Baixo ? 0.80f
            : (CurrentQualityTier == QualityTier.Medio ? 1.0f : 1.65f);
        cachedCullDistances[9] = smallFoliageCullDist * mult;
        cachedCullDistances[10] = propsCullDist * mult;
        cachedCullDistances[11] = wildlifeCullDist * mult;
        cachedCullDistances[12] = largeTreesCullDist * mult;

        cam.layerCullDistances = cachedCullDistances;
    }

    public void ApplyURPOptimizations()
    {
        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        urp.shadowDistance = CurrentQualityTier == QualityTier.Baixo ? lowShadowDistance
            : (CurrentQualityTier == QualityTier.Medio ? mobileShadowDistance : pcShadowDistance);
        urp.shadowCascadeCount = CurrentQualityTier == QualityTier.Baixo ? 1
            : (CurrentQualityTier == QualityTier.Medio ? mobileShadowCascades : pcShadowCascades);
        urp.supportsCameraDepthTexture = true;
        urp.supportsCameraOpaqueTexture = CurrentQualityTier == QualityTier.Alto;
    }
}
