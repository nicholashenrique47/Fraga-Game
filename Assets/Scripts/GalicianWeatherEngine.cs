using System.Collections;
using UnityEngine;

public enum GalicianWeatherState
{
    Orballo,        // Chuva miuda persistente
    NeboaMeiga,     // Nevoa densa rasteira nos vales
    Trevoada,       // Tempestade atlantica com vento forte
    SolDeOutono,    // Luz suave e clara filtrada pelas copas
    LuarEspectral   // Noite clara com luar prateado (Zero breu cego)
}

/// <summary>
/// Motor de Clima Dinâmico e Micrometeorologia Galega (The Forest AAA).
/// Controla transições ópticas, físicas e acústicas de umidade, névoa e luz solar/lunar.
/// </summary>
[ExecuteAlways]
public class GalicianWeatherEngine : MonoBehaviour
{
    public static GalicianWeatherEngine Instance { get; private set; }

    [Header("Referencias de Iluminacao e Efeitos")]
    public Light mainDirectionalLight;
    public ParticleSystem rainParticles;
    public ParticleSystem fogParticles;

    [Header("Estado Atual")]
    public GalicianWeatherState currentState = GalicianWeatherState.Orballo;
    [Range(0f, 1f)] public float globalWetness = 0.5f;
    public float transitionDuration = 6.0f;
    public float autoWeatherInterval = 110f;

    public bool isRaining => currentState == GalicianWeatherState.Orballo || currentState == GalicianWeatherState.Trevoada;

    // Shader Global Property IDs
    private static readonly int GlobalWetnessID = Shader.PropertyToID("_GlobalWetness");
    private static readonly int GlobalRainWetnessID = Shader.PropertyToID("_GlobalRainWetness");
    private static readonly int WindTurbulenceID = Shader.PropertyToID("_WindTurbulence");

    private Coroutine activeTransitionCoroutine;
    private float nextAutoWeatherTime = 95f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        ApplyInstantWeather(currentState);
        nextAutoWeatherTime = Time.time + autoWeatherInterval;
    }

    void Update()
    {
        // Propaga a variável global de molhado para todos os shaders PBR de terreno, rocha e folhagem
        Shader.SetGlobalFloat(GlobalWetnessID, globalWetness);
        Shader.SetGlobalFloat(GlobalRainWetnessID, globalWetness);

        if (!Application.isPlaying) return;

        if (Time.time >= nextAutoWeatherTime)
        {
            nextAutoWeatherTime = Time.time + autoWeatherInterval;
            bool isNight = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight();
            float r = Random.value;
            GalicianWeatherState next = isNight
                ? (r < 0.38f ? GalicianWeatherState.LuarEspectral : (r < 0.68f ? GalicianWeatherState.NeboaMeiga : (r < 0.86f ? GalicianWeatherState.Orballo : GalicianWeatherState.Trevoada)))
                : (r < 0.42f ? GalicianWeatherState.SolDeOutono : (r < 0.72f ? GalicianWeatherState.Orballo : (r < 0.88f ? GalicianWeatherState.NeboaMeiga : GalicianWeatherState.Trevoada)));

            if (next != currentState)
            {
                TransitionTo(next);
                if (HUDSurvivalOverlay.Instance != null)
                {
                    string label = next == GalicianWeatherState.Trevoada ? "Tempestade Atlantica se aproxima!"
                        : (next == GalicianWeatherState.NeboaMeiga ? "Nevoeiro denso cobre a Fraga..."
                        : (next == GalicianWeatherState.Orballo ? "Chuva fina comecou na floresta."
                        : (next == GalicianWeatherState.SolDeOutono ? "O ceu abriu em Sol de Outono." : "Luar espectral ilumina a noite.")));
                    HUDSurvivalOverlay.Instance.ShowLootTooltip(label);
                }
            }
        }

        if ((currentState == GalicianWeatherState.Trevoada || currentState == GalicianWeatherState.NeboaMeiga) &&
            PlayerStatus.Instance != null && !PlayerStatus.Instance.isNearBonfire)
        {
            var san = PlayerStatus.Instance.sanitySystem;
            if (san != null) san.sanity = Mathf.Max(0f, san.sanity - 0.22f * Time.deltaTime);
        }
    }

    public void TransitionTo(GalicianWeatherState newState)
    {
        if (!Application.isPlaying)
        {
            ApplyInstantWeather(newState);
            return;
        }

        if (activeTransitionCoroutine != null)
        {
            StopCoroutine(activeTransitionCoroutine);
        }

        activeTransitionCoroutine = StartCoroutine(TransitionRoutine(newState));
    }

    private IEnumerator TransitionRoutine(GalicianWeatherState targetState)
    {
        float timer = 0f;
        float startWetness = globalWetness;
        float targetWetness = (targetState == GalicianWeatherState.Orballo) ? 0.85f :
                              (targetState == GalicianWeatherState.Trevoada) ? 1.0f :
                              (targetState == GalicianWeatherState.NeboaMeiga) ? 0.60f :
                              (targetState == GalicianWeatherState.SolDeOutono) ? 0.20f : 0.40f;

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;
            globalWetness = Mathf.Lerp(startWetness, targetWetness, t);
            ApplyLerpedWeather(targetState, t);
            yield return null;
        }

        globalWetness = targetWetness;
        currentState = targetState;
        ApplyInstantWeather(targetState);
        activeTransitionCoroutine = null;
    }

    public void ApplyInstantWeather(GalicianWeatherState state)
    {
        currentState = state;
        if (mainDirectionalLight == null)
        {
            Light[] lights = FindObjectsByType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    mainDirectionalLight = l;
                    break;
                }
            }
        }

        switch (state)
        {
            case GalicianWeatherState.Orballo:
                if (mainDirectionalLight != null)
                {
                    mainDirectionalLight.color = new Color(0.82f, 0.86f, 0.90f);
                    mainDirectionalLight.intensity = 1.15f;
                }
                RenderSettings.fogDensity = 0.0055f;
                RenderSettings.fogColor = new Color(0.63f, 0.67f, 0.71f);
                Shader.SetGlobalFloat(WindTurbulenceID, 1.2f);
                globalWetness = 0.85f;
                if (rainParticles != null && !rainParticles.isPlaying) rainParticles.Play();
                break;

            case GalicianWeatherState.NeboaMeiga:
                if (mainDirectionalLight != null)
                {
                    mainDirectionalLight.color = new Color(0.48f, 0.52f, 0.55f);
                    mainDirectionalLight.intensity = 0.65f;
                }
                RenderSettings.fogDensity = 0.0090f; // Clamped seguro
                RenderSettings.fogColor = new Color(0.45f, 0.48f, 0.51f);
                Shader.SetGlobalFloat(WindTurbulenceID, 0.8f);
                globalWetness = 0.60f;
                if (fogParticles != null && !fogParticles.isPlaying) fogParticles.Play();
                break;

            case GalicianWeatherState.Trevoada:
                if (mainDirectionalLight != null)
                {
                    mainDirectionalLight.color = new Color(0.29f, 0.33f, 0.38f);
                    mainDirectionalLight.intensity = 0.45f;
                }
                RenderSettings.fogDensity = 0.0080f;
                RenderSettings.fogColor = new Color(0.27f, 0.31f, 0.35f);
                Shader.SetGlobalFloat(WindTurbulenceID, 4.5f);
                globalWetness = 1.0f;
                if (rainParticles != null && !rainParticles.isPlaying) rainParticles.Play();
                break;

            case GalicianWeatherState.SolDeOutono:
                if (mainDirectionalLight != null)
                {
                    mainDirectionalLight.color = new Color(0.99f, 0.91f, 0.78f);
                    mainDirectionalLight.intensity = 1.40f;
                }
                RenderSettings.fogDensity = 0.0028f;
                RenderSettings.fogColor = new Color(0.77f, 0.80f, 0.83f);
                Shader.SetGlobalFloat(WindTurbulenceID, 1.0f);
                globalWetness = 0.20f;
                if (rainParticles != null && rainParticles.isPlaying) rainParticles.Stop();
                break;

            case GalicianWeatherState.LuarEspectral:
                if (mainDirectionalLight != null)
                {
                    mainDirectionalLight.color = new Color(0.33f, 0.47f, 0.65f); // #5577A6
                    mainDirectionalLight.intensity = 0.35f;
                }
                RenderSettings.fogDensity = 0.0050f;
                RenderSettings.fogColor = new Color(0.12f, 0.16f, 0.22f);
                Shader.SetGlobalFloat(WindTurbulenceID, 0.9f);
                globalWetness = 0.40f;
                if (rainParticles != null && rainParticles.isPlaying) rainParticles.Stop();
                break;
        }
    }

    private void ApplyLerpedWeather(GalicianWeatherState state, float t)
    {
        if (mainDirectionalLight == null) return;

        Color targetLightColor = mainDirectionalLight.color;
        float targetIntensity = mainDirectionalLight.intensity;
        float targetFogDensity = RenderSettings.fogDensity;
        Color targetFogColor = RenderSettings.fogColor;

        switch (state)
        {
            case GalicianWeatherState.Orballo:
                targetLightColor = new Color(0.82f, 0.86f, 0.90f);
                targetIntensity = 1.15f;
                targetFogDensity = 0.0055f;
                targetFogColor = new Color(0.63f, 0.67f, 0.71f);
                break;
            case GalicianWeatherState.NeboaMeiga:
                targetLightColor = new Color(0.48f, 0.52f, 0.55f);
                targetIntensity = 0.65f;
                targetFogDensity = 0.0090f;
                targetFogColor = new Color(0.45f, 0.48f, 0.51f);
                break;
            case GalicianWeatherState.Trevoada:
                targetLightColor = new Color(0.29f, 0.33f, 0.38f);
                targetIntensity = 0.45f;
                targetFogDensity = 0.0080f;
                targetFogColor = new Color(0.27f, 0.31f, 0.35f);
                break;
            case GalicianWeatherState.SolDeOutono:
                targetLightColor = new Color(0.99f, 0.91f, 0.78f);
                targetIntensity = 1.40f;
                targetFogDensity = 0.0028f;
                targetFogColor = new Color(0.77f, 0.80f, 0.83f);
                break;
            case GalicianWeatherState.LuarEspectral:
                targetLightColor = new Color(0.33f, 0.47f, 0.65f);
                targetIntensity = 0.35f;
                targetFogDensity = 0.0050f;
                targetFogColor = new Color(0.12f, 0.16f, 0.22f);
                break;
        }

        mainDirectionalLight.color = Color.Lerp(mainDirectionalLight.color, targetLightColor, t);
        mainDirectionalLight.intensity = Mathf.Lerp(mainDirectionalLight.intensity, targetIntensity, t);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, t);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, t);
    }
}
