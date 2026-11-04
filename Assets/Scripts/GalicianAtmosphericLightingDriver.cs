using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Driver de calibracao atmosferica e iluminacao galega (The Forest AAA).
/// Garante que a cena mantenha legibilidade noturna permanente com luz lunar prateada,
/// cascatas de sombra suaves 2-split (mobile 60 FPS) e volume ACES tonemapping calibrado.
/// Em Play Mode, respeita a autoridade do DayNightCycle caso este esteja ativo.
/// </summary>
[ExecuteAlways]
public class GalicianAtmosphericLightingDriver : MonoBehaviour
{
    [Header("Referencias Principais")]
    public Light directionalLight;
    public Volume globalVolume;
    public UniversalRenderPipelineAsset urpAsset;

    [Header("Configuracao Lunar (Zero Breu Cego)")]
    public Color moonLightColor = new Color(0.38f, 0.52f, 0.72f, 1.0f); // Luar prateado visível
    public float moonIntensity = 0.70f;
    public Color nightSkyAmbient = new Color(0.25f, 0.32f, 0.42f, 1.0f);
    public Color nightEquatorAmbient = new Color(0.18f, 0.24f, 0.30f, 1.0f);
    public Color nightGroundAmbient = new Color(0.12f, 0.16f, 0.20f, 1.0f);

    [Header("Configuracao Diurna (Orballo / Sol Galego)")]
    public Color daySunColor = new Color(0.92f, 0.94f, 0.98f, 1.0f);
    public float dayIntensity = 1.6f;

    [Header("Cascatas de Sombra URP (Mobile 60 FPS)")]
    public float maxShadowDistance = 55.0f;
    public int shadowCascadeCount = 2;

    [Header("Controle de Tempo e Modo")]
    public bool isNightTime = false;
    public bool autoFindReferences = true;

    void Awake()
    {
        if (autoFindReferences)
        {
            FindReferences();
        }
        ApplyAtmosphericCalibration();
    }

    void OnValidate()
    {
        ApplyAtmosphericCalibration();
    }

    public void FindReferences()
    {
        if (DayNightCycle.Instance != null && DayNightCycle.Instance.directionalLight != null)
        {
            directionalLight = DayNightCycle.Instance.directionalLight;
        }

        if (directionalLight == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.enabled)
                {
                    directionalLight = l;
                    break;
                }
            }
        }

        if (globalVolume == null)
        {
            globalVolume = FindFirstObjectByType<Volume>();
        }

        if (urpAsset == null)
        {
            urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        }
    }

    public void ApplyAtmosphericCalibration()
    {
        // 1. Sombras URP Otimizadas para Mobile 60 FPS
        if (urpAsset != null)
        {
            urpAsset.shadowDistance = maxShadowDistance;
            urpAsset.shadowCascadeCount = shadowCascadeCount;
        }

        // 2. Volume Global - ACES Tonemapping e Color Adjustments
        if (globalVolume != null && globalVolume.profile != null)
        {
            if (globalVolume.profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping.mode.overrideState = true;
                tonemapping.mode.value = TonemappingMode.ACES;
            }

            if (globalVolume.profile.TryGet(out ColorAdjustments colorAdj))
            {
                colorAdj.postExposure.overrideState = true;
                colorAdj.postExposure.value = 0.35f; // Leve clareamento natural
                colorAdj.contrast.overrideState = true;
                colorAdj.contrast.value = 14f;
                colorAdj.saturation.overrideState = true;
                colorAdj.saturation.value = 0f;
            }
        }

        // Se DayNightCycle estiver ativo e em Play Mode, deixamos o ciclo de 24min controlar a luz e atmosfera
        if (Application.isPlaying && DayNightCycle.Instance != null)
        {
            return;
        }

        // 3. Configuracao da Luz Direcional (fallback / edit mode)
        if (directionalLight != null)
        {
            if (isNightTime)
            {
                directionalLight.color = moonLightColor;
                directionalLight.intensity = moonIntensity;
                directionalLight.shadows = LightShadows.Soft;
            }
            else
            {
                directionalLight.color = daySunColor;
                directionalLight.intensity = dayIntensity;
                directionalLight.shadows = LightShadows.Soft;
            }
        }

        // 4. Luz Ambiente Trilight (Garante que silhuetas fiquem sempre legíveis)
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 280f;

        if (isNightTime)
        {
            RenderSettings.ambientSkyColor = nightSkyAmbient;
            RenderSettings.ambientEquatorColor = nightEquatorAmbient;
            RenderSettings.ambientGroundColor = nightGroundAmbient;
            RenderSettings.ambientLight = nightSkyAmbient;
            RenderSettings.fogColor = new Color(0.18f, 0.25f, 0.36f, 1.0f);
        }
        else
        {
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.72f, 0.82f, 1.0f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.55f, 0.48f, 1.0f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.34f, 0.28f, 1.0f);
            RenderSettings.ambientLight = new Color(0.65f, 0.72f, 0.82f, 1.0f);
            RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.88f, 1.0f);
        }
    }
}
