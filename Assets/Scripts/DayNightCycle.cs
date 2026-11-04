using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sistema de Ciclo Dia e Noite e Clima Dinâmico (The Forest AAA).
/// Configurado exatamente para 24 minutos reais por dia completo no jogo:
/// - 1 dia in-game (24 horas) = 24 minutos da vida real (1.440 segundos reais).
/// - 1 hora in-game = 1 minuto da vida real (60 segundos reais).
/// - 1 minuto in-game = 1 segundo da vida real.
/// Iluminação garantida sem breu (Linear Fog suave, luz lunar visível, céu nítido).
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Time Settings (24 min vida real = 1 dia no jogo)")]
    [Tooltip("Duração total de um dia completo no jogo (24h) em minutos da vida real. Padrão The Forest: 24 min.")]
    public float dayDurationRealMinutes = 24f;
    public float timeScale { get { return 1440f / Mathf.Max(1f, dayDurationRealMinutes); } set { } } // Retrocompatibilidade

    [Range(0f, 24f)] public float currentTime = 10f; // Início padrão: 10h da manhã (pleno sol límpido)
    public int currentDay = 1;

    public float timeNormalized => currentTime / 24f;
    public bool isNight => (currentTime >= 20.0f || currentTime < 5.0f);
    public bool IsNight() => isNight;

    [Header("Lighting (URP)")]
    public Light directionalLight;
    public float maxIntensity = 2.2f;  // Brilho pleno de dia (The Forest)
    public float minIntensity = 0.70f; // Luar suave e perfeitamente visível à noite (zero breu cego)

    [Header("Atmosphere")]
    public Volume globalFogVolume;
    public float maxFogWeight = 1.0f;

    [Header("Weather (AAA)")]
    public float rainProbability = 0.25f;
    private bool isRaining = false;
    private ParticleSystem rainParticles;
    private float nextWeatherChangeTime;

    [Header("Events")]
    public SantaCompanaSystem santaCompanaSystem;
    private bool santaCompanaTriggered = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Auto-sanitização de valores legados desserializados de cenas antigas do Unity
        if (dayDurationRealMinutes < 5f) dayDurationRealMinutes = 24f;
        if (minIntensity < 0.60f) minIntensity = 0.70f;
        if (maxIntensity < 1.8f) maxIntensity = 2.2f;
    }

    private void OnValidate()
    {
        if (dayDurationRealMinutes < 1f) dayDurationRealMinutes = 24f;
        if (minIntensity < 0.2f) minIntensity = 0.70f;
    }

    private void Start()
    {
        ConsolidateDirectionalLight();

        if (directionalLight != null)
        {
            directionalLight.shadows = LightShadows.Soft;
            directionalLight.shadowStrength = 0.65f;
            RenderSettings.sun = directionalLight;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 280f;

        nextWeatherChangeTime = currentTime + Random.Range(3f, 7f);
        CreateRainSystem();
        UpdateLightingAndFog();
    }

    /// <summary>
    /// Garante que haja apenas UMA luz direcional ativa e autoritativa no projeto.
    /// Desativa luzes direcionais duplicadas para evitar estouro do shadow atlas e conflito de sombreamento.
    /// </summary>
    private void ConsolidateDirectionalLight()
    {
        Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light bestLight = directionalLight;

        foreach (var l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                if (bestLight == null)
                {
                    bestLight = l;
                }
                else if (l != bestLight)
                {
                    // Desativa directional lights secundárias concorrentes
                    Debug.Log($"[DayNightCycle] Desativando luz direcional concorrente '{l.name}' para preservar shadow atlas.");
                    l.enabled = false;
                }
            }
        }

        directionalLight = bestLight;
        if (directionalLight != null)
        {
            directionalLight.enabled = true;
        }
    }

    private void CreateRainSystem()
    {
        GameObject rainObj = new GameObject("Dynamic_Rain_System");
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            rainObj.transform.SetParent(mainCam.transform);
            rainObj.transform.localPosition = new Vector3(0, 10f, 5f);
            rainObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

            rainParticles = rainObj.AddComponent<ParticleSystem>();
            var main = rainParticles.main;
            main.duration = 5f;
            main.loop = true;
            main.startSpeed = 26f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startColor = new Color(0.6f, 0.7f, 0.8f, 0.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1200; // Otimizado para mobile 60 FPS

            var emission = rainParticles.emission;
            emission.rateOverTime = 600f;

            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(25f, 25f, 1f);

            var renderer = rainParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 12f;
            renderer.velocityScale = 0.4f;

            rainParticles.Stop();
        }
    }

    private void UpdateWeather()
    {
        if (currentTime > nextWeatherChangeTime)
        {
            nextWeatherChangeTime = currentTime + Random.Range(4f, 9f);
            float prob = (currentTime >= 19f || currentTime < 6f) ? rainProbability * 1.5f : rainProbability;
            
            isRaining = Random.value < prob;

            if (rainParticles != null)
            {
                if (isRaining && !rainParticles.isPlaying) rainParticles.Play();
                else if (!isRaining && rainParticles.isPlaying) rainParticles.Stop();
            }
        }
    }

    private void Update()
    {
        UpdateTime();
        UpdateLightingAndFog();
        UpdateWeather();
    }

    private void UpdateTime()
    {
        // =========================================================================
        // MATEMÁTICA PRECISA: 24 MINUTOS DA VIDA REAL = 1 DIA COMPLETO (24H) NO JOGO
        // -------------------------------------------------------------------------
        // 1 dia in-game = 24 horas = 1.440 minutos in-game
        // 24 minutos reais = 1.440 segundos reais
        // Logo:
        // -> 1 segundo na vida real = exatamente 1 minuto no jogo (60 segundos in-game)
        // -> 60 segundos na vida real = exatamente 1 hora no jogo (60 minutos in-game)
        // -> 1.440 segundos na vida real (24 min) = exatamente 24 horas no jogo (1 dia)
        // =========================================================================
        float safeMinutes = (dayDurationRealMinutes >= 1.0f) ? dayDurationRealMinutes : 24.0f;
        float totalRealSecondsInDay = safeMinutes * 60.0f; // 1.440s
        float hoursAdvancePerSecond = 24.0f / totalRealSecondsInDay; // 24 / 1440 = 1 / 60h por segundo real
        
        currentTime += Time.deltaTime * hoursAdvancePerSecond;

        // Evento mítico da Santa Compaña entre as 03:00 e 04:00 da madrugada
        if (currentTime >= 3f && currentTime < 4f && !santaCompanaTriggered)
        {
            if (santaCompanaSystem != null)
            {
                Vector3 start = new Vector3(Random.Range(15f, 85f), 0f, 95f);
                Vector3 end = new Vector3(Random.Range(15f, 85f), 0f, 5f);
                santaCompanaSystem.TriggerEvent(start, end);
                santaCompanaTriggered = true;
            }
        }

        // Passagem de ciclo ao atingir 24:00 (Meia-noite)
        if (currentTime >= 24f)
        {
            currentTime = Mathf.Repeat(currentTime, 24f); // Preserva fração contínua de tempo sem truncar
            currentDay++;
            santaCompanaTriggered = false;
            Debug.Log($"[DayNightCycle] Sobreviveste a mais uma noite na Galiza. Inicia o Dia {currentDay}.");
        }
    }

    private void UpdateLightingAndFog()
    {
        if (directionalLight == null) return;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 280f;

        // =========================================================================
        // TRANSIÇÃO ORGÂNICA CONTÍNUA (SEM CORTES OU PULOS EM SEGUNDOS)
        // -------------------------------------------------------------------------
        // Alvorada / Amanhecer: 05:00 às 06:45 (Transição suave de 1h45)
        // Pleno Dia:            06:45 às 18:15 (11h30 de sol brilhante)
        // Crepúsculo / Pôr Sol: 18:15 às 20:30 (Transição suave de 2h15)
        // Plena Noite:          20:30 às 05:00 (8h30 de luar acolhedor e visível)
        // =========================================================================
        float dayWeight;
        if (currentTime >= 5.0f && currentTime < 6.75f)
        {
            dayWeight = Mathf.SmoothStep(0f, 1f, (currentTime - 5.0f) / 1.75f);
        }
        else if (currentTime >= 6.75f && currentTime <= 18.25f)
        {
            dayWeight = 1f;
        }
        else if (currentTime > 18.25f && currentTime <= 20.5f)
        {
            dayWeight = Mathf.SmoothStep(1f, 0f, (currentTime - 18.25f) / 2.25f);
        }
        else
        {
            dayWeight = 0f;
        }

        // Fator de Golden Hour (pico no meio do nascer e do pôr do sol)
        float goldenHourFactor = Mathf.Sin(dayWeight * Mathf.PI);

        // 1. Intensidade contínua da Directional Light
        float noonArc = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01((currentTime - 6f) / 12f) * Mathf.PI));
        float targetDayIntensity = Mathf.Lerp(1.5f, maxIntensity, noonArc);
        directionalLight.intensity = Mathf.Lerp(minIntensity, targetDayIntensity, dayWeight);
        if (!directionalLight.enabled) directionalLight.enabled = true;

        // 2. Cores da Directional Light
        Color daySunColor = new Color(1.0f, 0.96f, 0.90f);
        Color goldenHourColor = new Color(1.0f, 0.68f, 0.42f);
        Color blendedSunColor = Color.Lerp(daySunColor, goldenHourColor, goldenHourFactor * 0.75f);
        Color moonlightColor = new Color(0.55f, 0.72f, 0.95f); // Prateado lunar azul límpido
        directionalLight.color = Color.Lerp(moonlightColor, blendedSunColor, dayWeight);

        // 3. Órbita contínua do Sol e da Lua
        // Órbita solar diurna (Leste -> Sul -> Oeste)
        float sunT = Mathf.Clamp01((currentTime - 5.0f) / 15.5f);
        float sunPitch = Mathf.Lerp(20f, 75f, Mathf.Sin(sunT * Mathf.PI));
        float sunYaw = Mathf.Lerp(70f, 265f, sunT);
        Quaternion sunRot = Quaternion.Euler(sunPitch, sunYaw, 0f);

        // Órbita lunar noturna (Leste -> Sul -> Oeste)
        float moonT = (currentTime >= 20.0f) ? (currentTime - 20.0f) / 9.5f : (currentTime + 4.0f) / 9.5f;
        float moonPitch = Mathf.Lerp(25f, 65f, Mathf.Sin(Mathf.Clamp01(moonT) * Mathf.PI));
        float moonYaw = Mathf.Lerp(75f, 265f, Mathf.Clamp01(moonT));
        Quaternion moonRot = Quaternion.Euler(moonPitch, moonYaw, 0f);

        // Interpolação esférica suave entre sol e lua
        directionalLight.transform.rotation = Quaternion.Slerp(moonRot, sunRot, dayWeight);

        // 4. Luz Ambiente Trilight contínua (Céu / Equador / Solo)
        Color daySky = new Color(0.85f, 0.92f, 1.0f) * 1.30f;
        Color dayEquator = new Color(0.70f, 0.80f, 0.72f) * 1.20f;
        Color dayGround = new Color(0.50f, 0.56f, 0.46f) * 1.10f;

        Color duskSky = new Color(0.85f, 0.62f, 0.52f);
        Color duskEquator = new Color(0.65f, 0.48f, 0.42f);
        Color duskGround = new Color(0.38f, 0.30f, 0.28f);

        Color nightSky = new Color(0.36f, 0.46f, 0.62f);
        Color nightEquator = new Color(0.26f, 0.33f, 0.42f);
        Color nightGround = new Color(0.18f, 0.22f, 0.26f);

        Color targetSky = Color.Lerp(nightSky, Color.Lerp(daySky, duskSky, goldenHourFactor * 0.7f), dayWeight);
        Color targetEquator = Color.Lerp(nightEquator, Color.Lerp(dayEquator, duskEquator, goldenHourFactor * 0.7f), dayWeight);
        Color targetGround = Color.Lerp(nightGround, Color.Lerp(dayGround, duskGround, goldenHourFactor * 0.7f), dayWeight);

        RenderSettings.ambientSkyColor = targetSky;
        RenderSettings.ambientEquatorColor = targetEquator;
        RenderSettings.ambientGroundColor = targetGround;
        RenderSettings.ambientLight = targetSky;

        // 5. Nevoeiro Linear Atmosférico (Nunca breu preto!)
        Color dayFog = new Color(0.70f, 0.78f, 0.88f);
        Color goldenFog = new Color(0.88f, 0.68f, 0.52f);
        Color nightFog = new Color(0.18f, 0.25f, 0.36f); // Luar nebuloso límpido
        RenderSettings.fogColor = Color.Lerp(nightFog, Color.Lerp(dayFog, goldenFog, goldenHourFactor * 0.75f), dayWeight);

        // 6. Volume de post-processing global mantido ativo
        if (globalFogVolume != null)
        {
            globalFogVolume.weight = 1.0f;
        }
    }
}
