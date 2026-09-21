using UnityEngine;
using UnityEngine.Rendering;

public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    public float timeScale = 10f; // Multiplicador de velocidade do tempo
    [Range(0f, 24f)] public float currentTime = 8f; // InÃ­cio padrÃ£o: 8h da manhÃ£
    public int currentDay = 1;

    [Header("Lighting (URP)")]
    public Light directionalLight;
    public float maxIntensity = 1.2f;
    public float minIntensity = 0.05f;

    [Header("Atmosphere")]
        [Header("Weather (AAA)")]
    public float rainProbability = 0.3f;
    private bool isRaining = false;
    private ParticleSystem rainParticles;
    private float nextWeatherChangeTime;

    private void Start()
    {
        nextWeatherChangeTime = currentTime + Random.Range(2f, 5f);
        CreateRainSystem();
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
            main.startSpeed = 30f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new Color(0.6f, 0.7f, 0.8f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;

            var emission = rainParticles.emission;
            emission.rateOverTime = 1500f;

            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 30f, 1f);

            var renderer = rainParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 15f;
            renderer.velocityScale = 0.5f;

            rainParticles.Stop();
        }
    }

    private void UpdateWeather()
    {
        if (currentTime > nextWeatherChangeTime)
        {
            nextWeatherChangeTime = currentTime + Random.Range(3f, 8f);
            // Chuva ǟ mais provǟvel de noite
            float prob = (currentTime >= 19f || currentTime < 6f) ? rainProbability * 2f : rainProbability;
            
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

    [Header("Events")]
    public SantaCompanaSystem santaCompanaSystem;
    private bool santaCompanaTriggered = false;

    private void UpdateTime()
    {
        // 3600 segundos = 1 hora no jogo (afetado pelo timeScale)
        currentTime += (Time.deltaTime * timeScale) / 3600f;
        
        if (currentTime >= 3f && currentTime < 4f && !santaCompanaTriggered)
        {
            if (santaCompanaSystem != null)
            {
                // Cruza o mapa de Norte a Sul
                Vector3 start = new Vector3(Random.Range(10f, 90f), 0f, 100f);
                Vector3 end = new Vector3(Random.Range(10f, 90f), 0f, -10f);
                santaCompanaSystem.TriggerEvent(start, end);
                santaCompanaTriggered = true;
            }
        }

        if (currentTime >= 24f)
        {
            currentTime = 0f; // Reseta a meia-noite
            currentDay++; // Avanca um dia
            santaCompanaTriggered = false; // Reset para a proxima noite
            Debug.Log($"[DayNightCycle] Sobreviveste a mais uma noite. Inicia o Dia {currentDay}.");
        }
    }

    private void UpdateLightingAndFog()
    {
        if (directionalLight == null) return;

        // Calcula a percentagem atual do dia (0.0 a 1.0)
        float timePercent = currentTime / 24f;

        // RotaÃ§Ã£o da Directional Light: -90Âº Ã  meia-noite, 90Âº ao meio-dia, 270Âº (ou -90Âº) Ã  meia-noite
        float angle = (timePercent * 360f) - 90f;
        directionalLight.transform.rotation = Quaternion.Euler(angle, 170f, 0f);

        // Cores ambientes
        Color dayAmbient = new Color(0.8f, 0.8f, 0.9f);
        Color nightAmbient = new Color(0.05f, 0.05f, 0.1f); // Azul muito escuro

        // Ajuste dinÃ¢mico de intensidade e nevoeiro baseado no ciclo
        if (currentTime >= 6f && currentTime <= 18f)
        {
            // PerÃ­odo Diurno (6h Ã s 18h)
            float t = (currentTime - 6f) / 12f; // Normaliza entre 0 e 1 durante o dia
            
            // Curva sinoidal simples para atingir o pico de intensidade ao meio-dia (t=0.5)
            float intensityMult = Mathf.Sin(t * Mathf.PI);
            directionalLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, intensityMult);
            
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, intensityMult);

            if (globalFogVolume != null)
            {
                // Nevoeiro diminui com o sol alto
                globalFogVolume.weight = Mathf.Lerp(maxFogWeight, 0f, intensityMult);
            }
        }
        else
        {
            // PerÃ­odo Noturno / Entardecer profundo
            directionalLight.intensity = minIntensity;
            RenderSettings.ambientLight = nightAmbient;
            
            if (globalFogVolume != null)
            {
                // Nevoeiro no mÃ¡ximo para isolar a visÃ£o do jogador
                globalFogVolume.weight = maxFogWeight;
            }
        }
    }
}
