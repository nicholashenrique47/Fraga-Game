using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Gerenciador de Otimização Extrema Mobile 60 FPS (The Forest AAA Standard).
/// Configura:
/// - Crava 60 FPS estáveis com VSync desativado para latência mínima no touch
/// - Pool de Texture Streaming inteligente (768 MB) para evitar estouro de RAM em dispositivos móveis
/// - Calibração de sombras URP de alto desempenho (55m de distância, 2 cascatas)
/// - Culling de camadas com array válido de 32 elementos (zero metros na camada 0 para terreno 100% visível)
/// - Garantia anti-breu da câmera (Modo Perspectiva, Skybox ativo, FarClip de 350m)
/// </summary>
[ExecuteAlways]
public class GalicianOptimizationManager : MonoBehaviour
{
    public static GalicianOptimizationManager Instance { get; private set; }

    [Header("Taxa de Quadros (Mobile 60 FPS)")]
    public int targetFrameRate = 60;
    public bool disableVSyncForLowLatency = true;

    [Header("Texture Streaming")]
    public bool enableTextureStreaming = true;
    public int textureStreamingMemoryPoolMB = 768; // Seguro para Android/iOS

    [Header("Sombras Mobile (Performance)")]
    public float mobileShadowDistance = 55.0f;
    public int mobileShadowCascades = 2;

    [Header("Culling de Camadas (Distâncias Seguras)")]
    public float smallFoliageCullDist = 45.0f;
    public float propsCullDist = 55.0f;
    public float wildlifeCullDist = 70.0f;
    public float largeTreesCullDist = 200.0f;

    void Awake()
    {
        if (Instance == null) Instance = this;

        ApplyFramerateAndQualitySettings();
        ApplyCameraVisibilityAndCulling();
        ApplyURPOptimizations();
    }

    void Start()
    {
        ApplyCameraVisibilityAndCulling();
        ApplyURPOptimizations();
    }

    public void ApplyFramerateAndQualitySettings()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = disableVSyncForLowLatency ? 0 : 1;

        if (enableTextureStreaming)
        {
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = textureStreamingMemoryPoolMB;
            QualitySettings.streamingMipmapsMaxLevelReduction = 2;
            QualitySettings.streamingMipmapsAddAllCameras = true;
        }

        // Intervalo de física fixo sincronizado a 50Hz (20ms) - fluidez perfeita sem sobrecarga de CPU
        Time.fixedDeltaTime = 0.02f;
    }

    public void ApplyCullingDistances()
    {
        ApplyCameraVisibilityAndCulling();
    }

    public void ApplyCameraVisibilityAndCulling()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null) return;

        // 1. GARANTIA ANTI-BREU: A Câmera NUNCA pode ter SolidColor preto ou projeção ortográfica cortada
        cam.orthographic = false; // Perspectiva cinematográfica fluida
        cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, 55f, 75f);
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.45f, 0.62f, 0.85f, 1f); // Azul céu luminoso de fallback
        cam.nearClipPlane = 0.15f;
        if (cam.farClipPlane < 300f) cam.farClipPlane = 350f;

        // 2. CULLING SEGURO: A API da Unity EXIGE rigorosamente um array de exatamente 32 floats.
        // O valor 0 significa que a camada usará o FarClipPlane padrão da câmera (350m).
        float[] cullDistances = new float[32];
        for (int i = 0; i < 32; i++)
        {
            cullDistances[i] = 0f; // Padrão: 100% visível até o horizonte
        }

        // Camadas customizadas (se definidas no projeto):
        // Layer 0 (Default / Terreno / Árvores Core) = 0f (sem culling prematuro!)
        // Layer 9 (Pequena vegetação / Fentos) = 45m
        // Layer 10 (Props / Frutos / Cogumelos) = 55m
        // Layer 11 (Fauna / Animais) = 70m
        // Layer 12 (Árvores Distantes) = 200m
        cullDistances[9] = smallFoliageCullDist;
        cullDistances[10] = propsCullDist;
        cullDistances[11] = wildlifeCullDist;
        cullDistances[12] = largeTreesCullDist;

        cam.layerCullDistances = cullDistances;
    }

    public void ApplyURPOptimizations()
    {
        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        urp.shadowDistance = mobileShadowDistance;
        urp.shadowCascadeCount = mobileShadowCascades;
        urp.supportsCameraDepthTexture = true;
        urp.supportsCameraOpaqueTexture = false; // Desativa cópia de opacos para economizar GPU bandwidth no mobile
    }
}
