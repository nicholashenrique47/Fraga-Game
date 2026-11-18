using UnityEngine;

/// AdvancedSkyboxSystem: Gere a estetica do ceu, incluindo cores procedurais,
/// estrelas cintilantes e uma lua fisica durante a noite.
public class AdvancedSkyboxSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public Transform playerTransform;

    [Header("Sky Colors")]
    public Color daySkyColor = new Color(0.4f, 0.6f, 0.9f);
    public Color dawnDuskSkyColor = new Color(0.8f, 0.4f, 0.2f);
    public Color nightSkyColor = new Color(0.05f, 0.05f, 0.1f);

    private ParticleSystem starParticles;
    private GameObject moonObject;
    private Material skyboxMat;
    private Texture2D softGlowFallback;

    private void Start()
    {
        CreateSkybox();
        CreateStars();
        CreateMoon();
    }

    // Aurora Boreal
    private ParticleSystem auroraParticles;
    public PlayerStatus playerStatusRef; // Para verificar temperatura

    private static Shader FindSafeShader(params string[] candidates)
    {
        foreach (string name in candidates)
        {
            Shader s = Shader.Find(name);
            if (s != null && s.name != "Hidden/InternalErrorShader") return s;
        }
        return Shader.Find("Sprites/Default");
    }

    private Texture2D LoadParticleTexture(string resourceName)
    {
        Texture2D tex = Resources.Load<Texture2D>("Textures/" + resourceName);
        if (tex != null) return tex;

        if (softGlowFallback == null)
        {
            const int size = 64;
            softGlowFallback = new Texture2D(size, size, TextureFormat.RGBA32, false);
            softGlowFallback.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, 2.2f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            softGlowFallback.SetPixels(px);
            softGlowFallback.Apply();
        }
        return softGlowFallback;
    }

    private void CreateSkybox()
    {
        // Tenta carregar o skybox procedural (gradiente)
        Shader skyShader = FindSafeShader("Skybox/Procedural", "Skybox/Solid Color", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Lit", "Sprites/Default");
        if (skyShader != null)
        {
            skyboxMat = new Material(skyShader);
            if (skyShader.name == "Skybox/Procedural")
            {
                skyboxMat.SetFloat("_SunSize", 0.04f);
                skyboxMat.SetFloat("_SunSizeConvergence", 5f);
                skyboxMat.SetFloat("_AtmosphereThickness", 1.2f);
                skyboxMat.SetFloat("_Exposure", 1.35f);
            }
            RenderSettings.skybox = skyboxMat;
            DynamicGI.UpdateEnvironment();
        }

        // Criar aurora boreal
        CreateAuroraBorealis();
    }

    private void CreateAuroraBorealis()
    {
        GameObject auroraObj = new GameObject("VFX_AuroraBorealis");
        auroraObj.transform.SetParent(transform);
        auroraParticles = auroraObj.AddComponent<ParticleSystem>();

        var main = auroraParticles.main;
        main.duration = 20f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 15f);
        main.startSpeed = 0.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(30f, 60f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.1f, 0.8f, 0.3f, 0.04f),  // Verde aurora
            new Color(0.2f, 0.4f, 0.9f, 0.03f)    // Azul aurora
        );
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = auroraParticles.emission;
        emission.rateOverTime = 3f;

        var shape = auroraParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(200f, 5f, 100f);

        // Movimento ondulante
        var noise = auroraParticles.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.1f;
        noise.scrollSpeed = 0.05f;

        var colorOverLifetime = auroraParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.1f, 0.9f, 0.3f), 0f),
                new GradientColorKey(new Color(0.2f, 0.5f, 0.9f), 0.5f),
                new GradientColorKey(new Color(0.5f, 0.2f, 0.8f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.04f, 0.3f),
                new GradientAlphaKey(0.04f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = g;

        // Renderer com textura suave (sem quadrados)
        var renderer = auroraParticles.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = FindSafeShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit", "Universal Render Pipeline/Unlit", "Sprites/Default");
        if (particleShader != null)
        {
            Material auroraMat = new Material(particleShader);
            Texture2D mistTex = LoadParticleTexture("soft_mist_puff");
            if (auroraMat.HasProperty("_BaseMap")) auroraMat.SetTexture("_BaseMap", mistTex);
            auroraMat.mainTexture = mistTex;
            if (auroraMat.HasProperty("_BaseColor")) auroraMat.SetColor("_BaseColor", new Color(0.2f, 0.8f, 0.4f, 0.06f));
            auroraMat.color = new Color(0.2f, 0.8f, 0.4f, 0.06f);
            if (auroraMat.HasProperty("_Surface")) auroraMat.SetFloat("_Surface", 1);
            if (auroraMat.HasProperty("_ZWrite")) auroraMat.SetInt("_ZWrite", 0);
            if (auroraMat.HasProperty("_SrcBlend")) auroraMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (auroraMat.HasProperty("_DstBlend")) auroraMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive blending
            auroraMat.renderQueue = 2900;
            renderer.material = auroraMat;
        }
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        auroraParticles.Stop();
    }

    private void CreateStars()
    {
        GameObject starObj = new GameObject("VFX_Stars");
        starObj.transform.SetParent(transform);
        starParticles = starObj.AddComponent<ParticleSystem>();

        var main = starParticles.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = 10f; // Long lifetime
        main.startSpeed = 0f; // Fixed position
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startColor = new Color(0.95f, 0.97f, 1f, 0.85f);
        main.maxParticles = 800;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;

        var emission = starParticles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 800) });

        var shape = starParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 150f;
        shape.radiusThickness = 0f; // On the edge

        var colorOverLifetime = starParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var renderer = starParticles.GetComponent<ParticleSystemRenderer>();
        Shader starShader = FindSafeShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit", "Universal Render Pipeline/Unlit", "Sprites/Default");
        if (starShader != null)
        {
            Material starMat = new Material(starShader);
            Texture2D glowTex = LoadParticleTexture("soft_particle_glow");
            if (starMat.HasProperty("_BaseMap")) starMat.SetTexture("_BaseMap", glowTex);
            starMat.mainTexture = glowTex;
            if (starMat.HasProperty("_BaseColor")) starMat.SetColor("_BaseColor", Color.white);
            starMat.color = Color.white;
            if (starMat.HasProperty("_Surface")) starMat.SetFloat("_Surface", 1);
            if (starMat.HasProperty("_ZWrite")) starMat.SetInt("_ZWrite", 0);
            if (starMat.HasProperty("_SrcBlend")) starMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (starMat.HasProperty("_DstBlend")) starMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            starMat.renderQueue = 2950;
            renderer.material = starMat;
        }
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private void CreateMoon()
    {
        moonObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moonObject.name = "VFX_Moon";
        moonObject.transform.SetParent(transform);
        moonObject.transform.localScale = new Vector3(20f, 20f, 20f);

        Destroy(moonObject.GetComponent<Collider>());

        Shader moonShader = FindSafeShader("Universal Render Pipeline/Unlit", "Unlit/Texture", "Universal Render Pipeline/Lit", "Sprites/Default");
        if (moonShader != null)
        {
            Material moonMat = new Material(moonShader);

            Texture2D moonTex = Resources.Load<Texture2D>("Textures/moon_surface_albedo");
            if (moonTex != null)
            {
                if (moonMat.HasProperty("_BaseMap")) moonMat.SetTexture("_BaseMap", moonTex);
                moonMat.mainTexture = moonTex;
            }
            if (moonMat.HasProperty("_BaseColor")) moonMat.SetColor("_BaseColor", new Color(0.95f, 0.96f, 1f));
            moonMat.color = new Color(0.95f, 0.96f, 1f); // Branco luar com crateras
            moonObject.GetComponent<Renderer>().material = moonMat;
        }

        // Moon Light
        Light moonLight = moonObject.AddComponent<Light>();
        moonLight.type = LightType.Point;
        moonLight.range = 200f;
        moonLight.intensity = 0.5f;
        moonLight.color = new Color(0.8f, 0.8f, 1f);
        moonLight.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (dayNightCycle == null) return;

        float time = dayNightCycle.currentTime; // 0 to 24

        // 1. Atualizar Skybox Color
        Color targetColor = daySkyColor;

        if (time >= 4f && time < 8f) // Dawn
        {
            float t = (time - 4f) / 4f; // 0 to 1
            targetColor = Color.Lerp(nightSkyColor, dawnDuskSkyColor, t);
            if (t > 0.5f) targetColor = Color.Lerp(dawnDuskSkyColor, daySkyColor, (t - 0.5f) * 2f);
        }
        else if (time >= 8f && time < 17f) // Day
        {
            targetColor = daySkyColor;
        }
        else if (time >= 17f && time < 20f) // Dusk
        {
            float t = (time - 17f) / 3f; // 0 to 1
            targetColor = Color.Lerp(daySkyColor, dawnDuskSkyColor, t * 2f);
            if (t > 0.5f) targetColor = Color.Lerp(dawnDuskSkyColor, nightSkyColor, (t - 0.5f) * 2f);
        }
        else // Night
        {
            targetColor = nightSkyColor;
        }

        bool isNight = time >= 19f || time < 5f;

        if (skyboxMat != null)
        {
            if (skyboxMat.HasProperty("_Color")) skyboxMat.SetColor("_Color", targetColor);
            if (skyboxMat.HasProperty("_Tint")) skyboxMat.SetColor("_Tint", targetColor);

            // Skybox Procedural: Ajustar atmosfera e exposição dinamicamente
            if (skyboxMat.HasProperty("_AtmosphereThickness"))
            {
                float thickness = isNight ? 0.4f : 1.2f;
                if (time >= 17f && time < 20f) thickness = Mathf.Lerp(1.2f, 0.4f, (time - 17f) / 3f);
                else if (time >= 4f && time < 8f) thickness = Mathf.Lerp(0.4f, 1.2f, (time - 4f) / 4f);
                skyboxMat.SetFloat("_AtmosphereThickness", thickness);
            }
            if (skyboxMat.HasProperty("_Exposure"))
            {
                float exposure = isNight ? 0.65f : 1.35f;
                skyboxMat.SetFloat("_Exposure", exposure);
            }
        }

        // 2. Estrelas
        if (starParticles != null)
        {
            if (isNight && !starParticles.isPlaying) starParticles.Play();
            else if (!isNight && starParticles.isPlaying) starParticles.Stop();

            // Fixar estrelas ao centro do jogador mas sem rodar
            if (playerTransform != null)
            {
                starParticles.transform.position = playerTransform.position;
            }
        }

        // 3. Lua (Rotaciona sobre o jogador)
        if (playerTransform != null && moonObject != null)
        {
            if (isNight)
            {
                if (!moonObject.activeSelf) moonObject.SetActive(true);

                // Calcula posicao da lua baseada no tempo (arco noturno Leste -> Oeste)
                float nightProgress = 0f;
                if (time >= 19f) nightProgress = (time - 19f) / 10f;
                else if (time < 5f) nightProgress = (time + 5f) / 10f;

                float angle = Mathf.Lerp(0f, Mathf.PI, nightProgress);
                // Arco no ceu (Z = Leste/Oeste, Y = Cima)
                Vector3 moonOffset = new Vector3(100f, Mathf.Sin(angle) * 150f, Mathf.Cos(angle) * 150f);
                moonObject.transform.position = playerTransform.position + moonOffset;
                moonObject.transform.LookAt(playerTransform);
            }
            else
            {
                if (moonObject.activeSelf) moonObject.SetActive(false);
            }
        }

        // 4. Aurora Boreal (noites frias)
        UpdateAurora(isNight);
    }

    private void UpdateAurora(bool isNight)
    {
        if (auroraParticles == null || playerTransform == null) return;

        // Aurora aparece em noites quando a temperatura é baixa
        bool shouldShowAurora = isNight;

        // Se tivermos referência ao PlayerStatus, verificar temperatura
        if (playerStatusRef != null)
        {
            shouldShowAurora = isNight && playerStatusRef.bodyTemperature < 35f;
        }

        if (shouldShowAurora)
        {
            if (!auroraParticles.isPlaying) auroraParticles.Play();
            // Posicionar a aurora no céu acima do jogador
            auroraParticles.transform.position = playerTransform.position + new Vector3(0, 120f, 50f);
        }
        else
        {
            if (auroraParticles.isPlaying) auroraParticles.Stop();
        }
    }
}
