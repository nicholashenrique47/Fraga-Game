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
    private BiomeManager biomeManager;

    // Controlador de material global (afeta o FoliageSway.shader)
    public float currentWindSpeed;
    private float targetWindSpeed;

    // Audio sintetizado de vento em filho dedicado (evita erro multi-AudioSource OnAudioFilterRead)
    private AudioSource windAudioSource;
    private float windLowPassTarget = 1000f;
    internal float currentWindLowPass = 1000f;
    internal int CachedSampleRate => cachedSampleRate;

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
        cachedSampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        currentWindSpeed = baseWindSpeed;
        targetWindSpeed = baseWindSpeed;

        Shader.SetGlobalFloat("_GlobalWindSpeed", currentWindSpeed);

        weatherTimer = weatherChangeInterval;
        CreateParticleSystems();

        // Criar synth base para o vento em GameObject filho dedicado (1 AudioSource exclusivo)
        GameObject windObj = new GameObject("WindSynth_Audio");
        windObj.transform.SetParent(transform, false);
        windAudioSource = windObj.AddComponent<AudioSource>();
        windAudioSource.spatialBlend = 0f;
        windAudioSource.loop = true;
        windAudioSource.volume = 0f;
        windAudioSource.clip = AudioClip.Create("DummyWind", 1, 1, cachedSampleRate, false);
        windAudioSource.Play();

        WindSynthNode synth = windObj.AddComponent<WindSynthNode>();
        synth.Configure(this);
    }

    private static Material CreateSafeParticleMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }

    private void CreateParticleSystems()
    {
        bool isMobile = Application.isMobilePlatform;

        // 1. Fireflies (Pirilampos)
        GameObject firefliesObj = new GameObject("VFX_Fireflies");
        firefliesObj.transform.SetParent(transform);
        firefliesParticles = firefliesObj.AddComponent<ParticleSystem>();

        var mainF = firefliesParticles.main;
        mainF.duration = 5f;
        mainF.loop = true;
        mainF.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
        mainF.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        mainF.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        mainF.startColor = new Color(0.6f, 1.0f, 0.2f, 0.8f);
        mainF.maxParticles = isMobile ? 40 : 100;
        mainF.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionF = firefliesParticles.emission;
        emissionF.rateOverTime = 0f;

        var shapeF = firefliesParticles.shape;
        shapeF.shapeType = ParticleSystemShapeType.Sphere;
        shapeF.radius = 15f;

        var noiseF = firefliesParticles.noise;
        noiseF.enabled = true;
        noiseF.strength = 0.5f;
        noiseF.frequency = 1f;

        var rendererF = firefliesParticles.GetComponent<ParticleSystemRenderer>();
        Material fireflyMat = CreateSafeParticleMaterial();
        if (fireflyMat != null) rendererF.material = fireflyMat;

        // 2. Rain (Chuva)
        GameObject rainObj = new GameObject("VFX_Rain");
        rainObj.transform.SetParent(transform);
        rainObj.transform.localPosition = new Vector3(0, 10f, 0);
        rainParticles = rainObj.AddComponent<ParticleSystem>();

        var mainR = rainParticles.main;
        mainR.duration = 5f;
        mainR.loop = true;
        mainR.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
        mainR.startSpeed = new ParticleSystem.MinMaxCurve(15f, 20f);
        mainR.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        mainR.startColor = new Color(0.8f, 0.9f, 1.0f, 0.6f);
        mainR.maxParticles = isMobile ? 450 : 2000;
        mainR.simulationSpace = ParticleSystemSimulationSpace.World;
        mainR.gravityModifier = 1f;

        var emissionR = rainParticles.emission;
        emissionR.rateOverTime = 0f;

        var shapeR = rainParticles.shape;
        shapeR.shapeType = ParticleSystemShapeType.Box;
        shapeR.scale = new Vector3(20f, 1f, 20f);

        var rendererR = rainParticles.GetComponent<ParticleSystemRenderer>();
        Material rainMat = CreateSafeParticleMaterial();
        if (rainMat != null) rendererR.material = rainMat;
        rendererR.renderMode = ParticleSystemRenderMode.Stretch;
        rendererR.lengthScale = 5f;
        rendererR.velocityScale = 0.1f;

        rainAudioSource = rainObj.AddComponent<AudioSource>();
        rainAudioSource.loop = true;
        rainAudioSource.volume = 0f;

        // 3. Snow (Neve)
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
        mainS.maxParticles = isMobile ? 250 : 1000;
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
        Material snowMat = CreateSafeParticleMaterial();
        if (snowMat != null) rendererS.material = snowMat;

        // 4. Leaves (Folhas)
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
        mainL.maxParticles = isMobile ? 80 : 300;
        mainL.simulationSpace = ParticleSystemSimulationSpace.World;

        var emissionL = leavesParticles.emission;
        emissionL.rateOverTime = 0f;

        var shapeL = leavesParticles.shape;
        shapeL.shapeType = ParticleSystemShapeType.Box;
        shapeL.scale = new Vector3(25f, 8f, 25f);

        var rendererL = leavesParticles.GetComponent<ParticleSystemRenderer>();
        Material leavesMat = CreateSafeParticleMaterial();
        if (leavesMat != null) rendererL.material = leavesMat;

        // 5. Lightning Light
        GameObject lightObj = new GameObject("VFX_Lightning");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = new Vector3(0, 20f, 0);
        lightningLight = lightObj.AddComponent<Light>();
        lightningLight.type = LightType.Directional;
        lightningLight.color = new Color(0.9f, 0.9f, 1f);
        lightningLight.intensity = 0f;
        lightningLight.shadows = LightShadows.None;
        lightningLight.enabled = false;
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

        if (windAudioSource != null)
        {
            float windRatio = currentWindSpeed / maxWindSpeed;
            windAudioSource.volume = Mathf.Lerp(windAudioSource.volume, windRatio * 0.4f, Time.deltaTime);
            windLowPassTarget = Mathf.Lerp(400f, 3500f, windRatio);
            currentWindLowPass = Mathf.Lerp(currentWindLowPass, windLowPassTarget, Time.deltaTime * 5f);
        }
    }

    private void SyncToShader()
    {
        Shader.SetGlobalFloat("_GlobalWindSpeed", currentWindSpeed);
        float swaySpeed = 1f + (currentWindSpeed * 0.8f);
        float swayAmount = 0.05f + (currentWindSpeed * 0.08f);
        Shader.SetGlobalFloat("_SwaySpeed", swaySpeed);
        Shader.SetGlobalFloat("_SwayAmount", swayAmount);

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
        bool isMobile = Application.isMobilePlatform;

        if (rainParticles != null)
        {
            var em = rainParticles.emission;
            float rainScale = isMobile ? 350f : 1500f;
            float targetRain = isRaining ? rainIntensity * rainScale : 0f;
            em.rateOverTime = Mathf.Lerp(em.rateOverTime.constant, targetRain, Time.deltaTime);

            if (rainAudioSource != null)
            {
                if (isRaining && !rainAudioSource.isPlaying) rainAudioSource.Play();
                rainAudioSource.volume = Mathf.Lerp(rainAudioSource.volume, isRaining ? rainIntensity * 0.5f : 0f, Time.deltaTime);
                if (!isRaining && rainAudioSource.volume < 0.01f) rainAudioSource.Stop();
            }
        }

        if (firefliesParticles != null && dayNightCycle != null)
        {
            bool isNightOrEvening = dayNightCycle.currentTime > 18f || dayNightCycle.currentTime < 5f;
            bool canShowFireflies = isNightOrEvening && rainIntensity < 0.5f;

            var emF = firefliesParticles.emission;
            float targetFireflies = canShowFireflies ? (isMobile ? 8f : 20f) : 0f;
            emF.rateOverTime = Mathf.Lerp(emF.rateOverTime.constant, targetFireflies, Time.deltaTime);
        }

        if (dayNightCycle != null)
        {
            float targetFogDensity = 0.0035f;

            if (dayNightCycle.currentTime > 22f || dayNightCycle.currentTime < 5.5f) targetFogDensity = 0.0065f;
            if (isRaining) targetFogDensity += rainIntensity * 0.004f;
            if (isFoggy) targetFogDensity = 0.012f;

            if (biomeManager != null && !isFoggy) targetFogDensity *= biomeManager.GetFogDensity();

            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 0.2f);

            if (isFoggy && playerStatus != null)
            {
                SanitySystem sanity = playerStatus.GetComponent<SanitySystem>();
                if (sanity != null) sanity.sanity -= Time.deltaTime * 0.5f;
            }

            Color targetFogColor;
            float ct = dayNightCycle.currentTime;
            if (isRaining)
            {
                targetFogColor = new Color(0.55f, 0.62f, 0.70f);
            }
            else if (ct >= 6f && ct <= 18f)
            {
                targetFogColor = new Color(0.72f, 0.82f, 0.90f);
            }
            else if ((ct >= 4.5f && ct < 6f) || (ct > 18f && ct <= 19.5f))
            {
                targetFogColor = new Color(0.82f, 0.70f, 0.58f);
            }
            else
            {
                targetFogColor = new Color(0.22f, 0.28f, 0.40f);
            }

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * 0.5f);
        }

        if (snowParticles != null && playerStatus != null)
        {
            var emS = snowParticles.emission;
            bool isSnowing = playerStatus.bodyTemperature < 5f && rainIntensity > 0f;
            float targetSnow = isSnowing ? (isMobile ? 120f : 500f) : 0f;
            emS.rateOverTime = Mathf.Lerp(emS.rateOverTime.constant, targetSnow, Time.deltaTime * 2f);

            if (isSnowing && rainParticles != null)
            {
                var emR = rainParticles.emission;
                emR.rateOverTime = Mathf.Lerp(emR.rateOverTime.constant, 0f, Time.deltaTime * 2f);
            }
        }

        if (leavesParticles != null)
        {
            var emL = leavesParticles.emission;
            bool highWind = currentWindSpeed > 2.5f;
            float leafScale = isMobile ? 30f : 100f;
            float targetLeaves = highWind ? (currentWindSpeed - 2.5f) * leafScale : 0f;
            emL.rateOverTime = Mathf.Lerp(emL.rateOverTime.constant, targetLeaves, Time.deltaTime);

            var velL = leavesParticles.velocityOverLifetime;
            velL.enabled = true;
            velL.x = currentWindSpeed * 2f;
        }

        if (isRaining && rainIntensity > 0.7f)
        {
            lightningTimer -= Time.deltaTime;
            if (lightningTimer <= 0f)
            {
                if (lightningLight != null)
                {
                    lightningLight.intensity = Random.Range(1.5f, 3f);
                    lightningLight.enabled = true;
                }
                if (audioManager != null && thunderClip != null)
                {
                    Invoke("PlayThunder", Random.Range(0.2f, 1f));
                }
                lightningTimer = Random.Range(10f, 30f);
            }
        }

        if (lightningLight != null)
        {
            if (lightningLight.intensity > 0f)
            {
                lightningLight.intensity = Mathf.Lerp(lightningLight.intensity, 0f, Time.deltaTime * 15f);
                if (lightningLight.intensity <= 0.01f)
                {
                    lightningLight.intensity = 0f;
                    lightningLight.enabled = false;
                }
            }
            else if (lightningLight.enabled)
            {
                lightningLight.enabled = false;
            }
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
        if (lightningLight != null)
        {
            lightningLight.intensity = Random.Range(3f, 6f);
            lightningLight.enabled = true;
        }
        if (rainAudioSource != null && thunderClip != null)
        {
            rainAudioSource.PlayOneShot(thunderClip, Random.Range(0.6f, 1f));
        }
    }

    private void UpdatePlayerEffects()
    {
        if (isRaining && playerStatus != null && !playerStatus.isNearBonfire)
        {
            playerStatus.bodyTemperature -= rainIntensity * 2f * Time.deltaTime;
        }
    }

    private sealed class WindSynthNode : MonoBehaviour
    {
        private WeatherSystem owner;
        private System.Random sysRand = new System.Random();
        private float windPhase = 0f;
        private float lastSample = 0f;

        public void Configure(WeatherSystem ownerRef)
        {
            owner = ownerRef;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (owner == null || owner.currentWindSpeed < 0.1f)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            int sampleRate = owner.CachedSampleRate > 0 ? owner.CachedSampleRate : 48000;
            float lpFreq = Mathf.Max(80f, owner.currentWindLowPass);
            float RC = 1.0f / (lpFreq * 2f * Mathf.PI);
            float dt = 1.0f / sampleRate;
            float alpha = dt / (RC + dt);

            for (int i = 0; i < data.Length; i += channels)
            {
                float noise = (float)(sysRand.NextDouble() * 2.0 - 1.0);
                float filteredNoise = lastSample + alpha * (noise - lastSample);
                lastSample = filteredNoise;

                windPhase += 0.5f / sampleRate;
                if (windPhase > 1f) windPhase -= 1f;
                float lfo = Mathf.Sin(2f * Mathf.PI * windPhase) * 0.5f + 0.5f;

                float output = filteredNoise * (0.5f + lfo * 0.5f);

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = output;
                }
            }
        }
    }
}
