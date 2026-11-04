using UnityEngine;

public class WeatherSystem : MonoBehaviour
{
    [Header("Wind Settings")]
    public float baseWindSpeed = 1.0f;
    public float windGustFrequency = 0.5f; // Probabilidade de uma rajada forte por segundo
    public float maxWindSpeed = 4.0f;

    [Header("Weather States")]
    public bool isRaining = false;
    public float rainIntensity = 0f;
    [Range(0f, 1f)] public float rainChance = 0.3f;
    
    public bool isFoggy = false;
    [Range(0f, 1f)] public float fogChance = 0.15f;

    [Header("Timers")]
    public float weatherChangeInterval = 120f; // Tenta mudar o clima a cada 2 minutos
    private float weatherTimer = 0f;

    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public AudioManager audioManager;
    public Transform playerTransform;
    public PlayerStatus playerStatus;

    [Header("Visuals")]
    public ParticleSystem rainParticles;
    public ParticleSystem firefliesParticles;
    public ParticleSystem snowParticles;
    public ParticleSystem leavesParticles;
    public Light lightningLight;

    [Header("Audio")]
    public AudioSource rainAudioSource;
    public AudioClip thunderClip;
    private float lightningTimer = 0f;

    private int cachedSampleRate = 48000;
    private System.Random sysRand = new System.Random();
    private BiomeManager biomeManager;

    // Controlador de material global (afeta o FoliageSway.shader)
    public float currentWindSpeed;
    private float targetWindSpeed;
    
    // Variaveis de sintese de audio
    private AudioSource windAudioSource;
    private float windPhase = 0f;
    private float windLowPassTarget = 1000f;
    private float currentWindLowPass = 1000f;

    private void Start()
    {
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
        }
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();

        biomeManager = FindAnyObjectByType<BiomeManager>();
        cachedSampleRate = AudioSettings.outputSampleRate;
        currentWindSpeed = baseWindSpeed;
        targetWindSpeed = baseWindSpeed;
        
        Shader.SetGlobalFloat("_GlobalWindSpeed", currentWindSpeed);
        
        weatherTimer = weatherChangeInterval;
        CreateParticleSystems();
        
        // Criar synth base para o vento
        windAudioSource = gameObject.AddComponent<AudioSource>();
        windAudioSource.spatialBlend = 0f; // Global 2D
        windAudioSource.loop = true;
        windAudioSource.volume = 0f; // Controlado dinamicamente
        windAudioSource.clip = AudioClip.Create("DummyWind", 1, 1, 44100, false);
        windAudioSource.Play();
    }

    private void CreateParticleSystems()
    {
        // ==========================================
        // 1. Fireflies (Pirilampos)
        // ==========================================
        GameObject firefliesObj = new GameObject("VFX_Fireflies");
        firefliesObj.transform.SetParent(transform);
        firefliesParticles = firefliesObj.AddComponent<ParticleSystem>();

        var mainF = firefliesParticles.main;
        mainF.duration = 5f;
        mainF.loop = true;
        mainF.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
        mainF.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        mainF.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        mainF.startColor = new Color(0.6f, 1.0f, 0.2f, 0.8f); // Verde brilhante
        mainF.maxParticles = 100;
        mainF.simulationSpace = ParticleSystemSimulationSpace.World; // Para nao seguir o player rigidamente

        var emissionF = firefliesParticles.emission;
        emissionF.rateOverTime = 0f; // Controlado via script

        var shapeF = firefliesParticles.shape;
        shapeF.shapeType = ParticleSystemShapeType.Sphere;
        shapeF.radius = 15f;

        var noiseF = firefliesParticles.noise;
        noiseF.enabled = true;
        noiseF.strength = 0.5f;
        noiseF.frequency = 1f;

        var rendererF = firefliesParticles.GetComponent<ParticleSystemRenderer>();
        Material fireflyMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        rendererF.material = fireflyMat;

        // ==========================================
        // 2. Rain (Chuva)
        // ==========================================
        GameObject rainObj = new GameObject("VFX_Rain");
        rainObj.transform.SetParent(transform);
        rainObj.transform.localPosition = new Vector3(0, 10f, 0); // Acima do jogador
        rainParticles = rainObj.AddComponent<ParticleSystem>();

        var mainR = rainParticles.main;
        mainR.duration = 5f;
        mainR.loop = true;
        mainR.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
        mainR.startSpeed = new ParticleSystem.MinMaxCurve(15f, 20f);
        mainR.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        mainR.startColor = new Color(0.8f, 0.9f, 1.0f, 0.6f);
        mainR.maxParticles = 2000;
        mainR.simulationSpace = ParticleSystemSimulationSpace.World;
        mainR.gravityModifier = 1f;

        var emissionR = rainParticles.emission;
        emissionR.rateOverTime = 0f;

        var shapeR = rainParticles.shape;
        shapeR.shapeType = ParticleSystemShapeType.Box;
        shapeR.scale = new Vector3(20f, 1f, 20f);

        var rendererR = rainParticles.GetComponent<ParticleSystemRenderer>();
        Material rainMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        rendererR.material = rainMat;
        rendererR.renderMode = ParticleSystemRenderMode.Stretch;
        rendererR.lengthScale = 5f; // Alongar as gotas
        rendererR.velocityScale = 0.1f;

        // Audio
        rainAudioSource = rainObj.AddComponent<AudioSource>();
        rainAudioSource.loop = true;
        rainAudioSource.volume = 0f;

        // ==========================================
        // 3. Snow (Neve)
        // ==========================================
        GameObject snowObj = new GameObject("VFX_Snow");
        snowObj.transform.SetParent(transform);
        snowObj.transform.localPosition = new Vector3(0, 15f, 0);
        snowParticles = snowObj.AddComponent<ParticleSystem>();
        
        var mainS = snowParticles.main;
        mainS.duration = 5f;
        mainS.loop = true;
        mainS.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        mainS.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        mainS.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        mainS.startColor = new Color(1f, 1f, 1f, 0.8f);
        mainS.maxParticles = 1000;
        mainS.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emissionS = snowParticles.emission;
        emissionS.rateOverTime = 0f;
        
        var shapeS = snowParticles.shape;
        shapeS.shapeType = ParticleSystemShapeType.Box;
        shapeS.scale = new Vector3(25f, 1f, 25f);

        var velS = snowParticles.velocityOverLifetime;
        velS.enabled = true;
        velS.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
        velS.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        var rendererS = snowParticles.GetComponent<ParticleSystemRenderer>();
        rendererS.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

        // ==========================================
        // 4. Leaves (Folhas)
        // ==========================================
        GameObject leavesObj = new GameObject("VFX_Leaves");
        leavesObj.transform.SetParent(transform);
        leavesObj.transform.localPosition = new Vector3(0, 5f, 0);
        leavesParticles = leavesObj.AddComponent<ParticleSystem>();

        var mainL = leavesParticles.main;
        mainL.duration = 5f;
        mainL.loop = true;
        mainL.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        mainL.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
        mainL.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        mainL.startColor = new Color(0.3f, 0.2f, 0.05f, 0.9f);
        mainL.maxParticles = 300;
        mainL.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionL = leavesParticles.emission;
        emissionL.rateOverTime = 0f;

        var shapeL = leavesParticles.shape;
        shapeL.shapeType = ParticleSystemShapeType.Box;
        shapeL.scale = new Vector3(25f, 8f, 25f);

        var rendererL = leavesParticles.GetComponent<ParticleSystemRenderer>();
        rendererL.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        
        // ==========================================
        // 5. Lightning Light
        // ==========================================
        GameObject lightObj = new GameObject("VFX_Lightning");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = new Vector3(0, 20f, 0);
        lightningLight = lightObj.AddComponent<Light>();
        lightningLight.type = LightType.Directional;
        lightningLight.color = new Color(0.9f, 0.9f, 1f);
        lightningLight.intensity = 0f;
        lightningLight.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (playerTransform != null)
        {
            Vector3 targetPos = playerTransform.position;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 5f);
        }

        UpdateWind();
        SyncToShader();
        UpdateWeatherState();
        UpdateVisuals();
        UpdatePlayerEffects();
    }

    private void UpdateWind()
    {
        bool isNight = dayNightCycle != null && (dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f);
        float currentBase = isNight ? baseWindSpeed * 1.5f : baseWindSpeed;

        if (Random.value < windGustFrequency * Time.deltaTime)
        {
            targetWindSpeed = Random.Range(currentBase, isNight ? maxWindSpeed * 1.5f : maxWindSpeed);
        }

        if (targetWindSpeed > currentBase)
        {
            targetWindSpeed = Mathf.Lerp(targetWindSpeed, currentBase, Time.deltaTime * 0.5f);
        }

        currentWindSpeed = Mathf.Lerp(currentWindSpeed, targetWindSpeed, Time.deltaTime * 2f);
        
        // Sincronizar audio sintetizado
        if (windAudioSource != null)
        {
            float windRatio = currentWindSpeed / maxWindSpeed;
            windAudioSource.volume = Mathf.Lerp(windAudioSource.volume, windRatio * 0.4f, Time.deltaTime);
            windLowPassTarget = Mathf.Lerp(400f, 3500f, windRatio); // Vento mais forte = frequencias mais altas
            currentWindLowPass = Mathf.Lerp(currentWindLowPass, windLowPassTarget, Time.deltaTime * 5f);
        }
    }

    private void SyncToShader()
    {
        Shader.SetGlobalFloat("_GlobalWindSpeed", currentWindSpeed);
        // Aplica o vento extremo ao shader de foliage
        float swaySpeed = 1f + (currentWindSpeed * 0.8f);
        float swayAmount = 0.05f + (currentWindSpeed * 0.08f);
        Shader.SetGlobalFloat("_SwaySpeed", swaySpeed);
        Shader.SetGlobalFloat("_SwayAmount", swayAmount);
        
        // Wetness Dinamico (The Forest Style): aumenta quando chove ou ha tempestade
        float targetWetness = (isRaining || rainIntensity > 0.1f) ? 1.0f : 0.0f;
        float currentWetness = Shader.GetGlobalFloat("_GlobalRainWetness");
        float newWetness = Mathf.Lerp(currentWetness, targetWetness, Time.deltaTime * 0.5f);
        Shader.SetGlobalFloat("_GlobalRainWetness", newWetness);
    }

    private void UpdateWeatherState()
    {
        weatherTimer -= Time.deltaTime;
        if (weatherTimer <= 0f)
        {
            weatherTimer = weatherChangeInterval;

            float r = Random.value;
            if (r < rainChance)
            {
                isRaining = true;
                isFoggy = false;
                rainIntensity = Random.Range(0.3f, 1.0f);
                Debug.Log($"[Weather] Comecou a chover! (Intensidade: {rainIntensity:F2})");
            }
            else if (r < rainChance + fogChance)
            {
                isRaining = false;
                rainIntensity = 0f;
                isFoggy = true;
                Debug.Log("[Weather] Um nevoeiro denso aproxima-se...");
            }
            else
            {
                if (isRaining) Debug.Log("[Weather] A chuva parou.");
                if (isFoggy) Debug.Log("[Weather] O nevoeiro dissipou-se.");
                isRaining = false;
                isFoggy = false;
                rainIntensity = 0f;
            }
        }
    }

    private void UpdateVisuals()
    {
        // 1. Chuva
        if (rainParticles != null)
        {
            var em = rainParticles.emission;
            float targetRain = isRaining ? rainIntensity * 1500f : 0f;
            em.rateOverTime = Mathf.Lerp(em.rateOverTime.constant, targetRain, Time.deltaTime);

            if (rainAudioSource != null)
            {
                if (isRaining && !rainAudioSource.isPlaying) rainAudioSource.Play();
                rainAudioSource.volume = Mathf.Lerp(rainAudioSource.volume, isRaining ? rainIntensity * 0.5f : 0f, Time.deltaTime);
                if (!isRaining && rainAudioSource.volume < 0.01f) rainAudioSource.Stop();
            }
        }

        // 2. Pirilampos (Apenas ao entardecer e de noite, e sem chuva forte)
        if (firefliesParticles != null && dayNightCycle != null)
        {
            bool isNightOrEvening = dayNightCycle.currentTime > 18f || dayNightCycle.currentTime < 5f;
            bool canShowFireflies = isNightOrEvening && rainIntensity < 0.5f;

            var emF = firefliesParticles.emission;
            float targetFireflies = canShowFireflies ? 20f : 0f;
            emF.rateOverTime = Mathf.Lerp(emF.rateOverTime.constant, targetFireflies, Time.deltaTime);
        }

        // 3. Nevoeiro global (Fog) - Calibrado The Forest (Luminoso, atmosférico e nunca preto)
        if (dayNightCycle != null)
        {
            float targetFogDensity = 0.0035f; // Suave estilo The Forest (visibilidade ampla e cristalina)
            
            if (dayNightCycle.currentTime > 22f || dayNightCycle.currentTime < 5.5f) targetFogDensity = 0.0065f;
            if (isRaining) targetFogDensity += rainIntensity * 0.004f;
            if (isFoggy) targetFogDensity = 0.012f; // Nevoeiro moderado apenas quando explicitamente foggy

            if (biomeManager != null && !isFoggy) targetFogDensity *= biomeManager.GetFogDensity();

            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 0.2f);
            
            // Penalidade de Sanidade durante o Nevoeiro
            if (isFoggy && playerStatus != null)
            {
                SanitySystem sanity = playerStatus.GetComponent<SanitySystem>();
                if (sanity != null) sanity.sanity -= Time.deltaTime * 0.5f; // Dreno extra de Sanidade
            }

            // COR DO FOG THE FOREST:
            // De dia: azul-celeste luminoso e claro (0.72f, 0.82f, 0.90f)
            // Ao amanhecer/entardecer: dourado suave (0.82f, 0.70f, 0.58f)
            // De noite: luar azulado perfeitamente visivel (0.22f, 0.28f, 0.40f)
            // Chuva: cinza prateado suave (0.55f, 0.62f, 0.70f)
            Color targetFogColor;
            float ct = dayNightCycle.currentTime;
            if (isRaining)
            {
                targetFogColor = new Color(0.55f, 0.62f, 0.70f);
            }
            else if (ct >= 6f && ct <= 18f)
            {
                // Pleno dia The Forest
                targetFogColor = new Color(0.72f, 0.82f, 0.90f);
            }
            else if ((ct >= 4.5f && ct < 6f) || (ct > 18f && ct <= 19.5f))
            {
                // Alvorada / Crepúsculo dourado
                targetFogColor = new Color(0.82f, 0.70f, 0.58f);
            }
            else
            {
                // Noite misteriosa mas nítida
                targetFogColor = new Color(0.22f, 0.28f, 0.40f);
            }

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * 0.5f);
        }

        // 4. Neve
        if (snowParticles != null && playerStatus != null)
        {
            var emS = snowParticles.emission;
            // Nevar quando temperatura < 5
            bool isSnowing = playerStatus.bodyTemperature < 5f && rainIntensity > 0f;
            float targetSnow = isSnowing ? 500f : 0f;
            emS.rateOverTime = Mathf.Lerp(emS.rateOverTime.constant, targetSnow, Time.deltaTime * 2f);
            
            // Se estiver a nevar, reduzir a chuva
            if (isSnowing && rainParticles != null)
            {
                var emR = rainParticles.emission;
                emR.rateOverTime = Mathf.Lerp(emR.rateOverTime.constant, 0f, Time.deltaTime * 2f);
            }
        }

        // 5. Folhas
        if (leavesParticles != null)
        {
            var emL = leavesParticles.emission;
            bool highWind = currentWindSpeed > 2.5f;
            float targetLeaves = highWind ? (currentWindSpeed - 2.5f) * 100f : 0f;
            emL.rateOverTime = Mathf.Lerp(emL.rateOverTime.constant, targetLeaves, Time.deltaTime);
            
            // Atualizar velocidade das folhas para match com o vento
            var velL = leavesParticles.velocityOverLifetime;
            velL.enabled = true;
            velL.x = currentWindSpeed * 2f;
        }

        // 6. Relampagos (Lightning)
        if (isRaining && rainIntensity > 0.7f)
        {
            lightningTimer -= Time.deltaTime;
            if (lightningTimer <= 0f)
            {
                // Disparar relampago
                if (lightningLight != null) lightningLight.intensity = Random.Range(1.5f, 3f);
                if (audioManager != null && thunderClip != null)
                {
                    // Delay para o trovao
                    Invoke("PlayThunder", Random.Range(0.2f, 1f));
                }
                lightningTimer = Random.Range(10f, 30f);
            }
        }
        
        // Decair relampago
        if (lightningLight != null && lightningLight.intensity > 0f)
        {
            lightningLight.intensity = Mathf.Lerp(lightningLight.intensity, 0f, Time.deltaTime * 15f);
        }
    }

    private void PlayThunder()
    {
        if (audioManager != null && thunderClip != null)
        {
            audioManager.PlaySFX(thunderClip, transform.position, 1f, 150f);
        }
    }

    private void TriggerLightning()
    {
        lightningLight.intensity = Random.Range(3f, 6f);
        if (rainAudioSource != null && thunderClip != null)
        {
            rainAudioSource.PlayOneShot(thunderClip, Random.Range(0.6f, 1f));
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (currentWindSpeed < 0.1f)
        {
            for (int i = 0; i < data.Length; i++) data[i] = 0f;
            return;
        }

        int sampleRate = cachedSampleRate > 0 ? cachedSampleRate : 48000;
        float RC = 1.0f / (currentWindLowPass * 2f * Mathf.PI);
        float dt = 1.0f / sampleRate;
        float alpha = dt / (RC + dt);

        float lastSample = 0f;

        for (int i = 0; i < data.Length; i += channels)
        {
            // Ruido branco puro seguro para thread de audio
            float noise = (float)(sysRand.NextDouble() * 2.0 - 1.0);
            
            // Filtro Low-Pass IIR Simples para o efeito "whoosh"
            float filteredNoise = lastSample + alpha * (noise - lastSample);
            lastSample = filteredNoise;
            
            // Adicionar modulacao de LFO lento para dar emocao ao vento
            windPhase += 0.5f / sampleRate;
            if (windPhase > 1f) windPhase -= 1f;
            float lfo = Mathf.Sin(2f * Mathf.PI * windPhase) * 0.5f + 0.5f; // 0 a 1

            float output = filteredNoise * (0.5f + lfo * 0.5f);

            for (int c = 0; c < channels; c++)
            {
                data[i + c] = output;
            }
        }
    }

    private void UpdatePlayerEffects()
    {
        if (isRaining && playerStatus != null && !playerStatus.isNearBonfire)
        {
            playerStatus.bodyTemperature -= rainIntensity * 2f * Time.deltaTime;
        }
    }
}
