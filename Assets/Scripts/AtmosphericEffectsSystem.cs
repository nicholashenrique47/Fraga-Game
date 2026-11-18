using UnityEngine;

/// AtmosphericEffectsSystem: Partículas ambientais premium para imersão estilo The Forest.
/// Gera poeira flutuante de dia, vaga-lumes à noite, névoa rasteira na madrugada,
/// e partículas de folhas ao vento. Todos os efeitos são procedurais e sem prefabs.
public class AtmosphericEffectsSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public WeatherSystem weatherSystem;
    public Transform playerTransform;

    [Header("Dust Motes (Dia)")]
    public int maxDustMotes = 200;
    private ParticleSystem dustParticles;

    [Header("Fireflies (Noite)")]
    public int maxFireflies = 80;
    private ParticleSystem fireflyParticles;

    [Header("Ground Fog (Madrugada)")]
    public int maxFogParticles = 300;
    private ParticleSystem groundFogParticles;

    [Header("Falling Leaves")]
    public int maxLeaves = 50;
    private ParticleSystem leavesParticles;

    [Header("Embers (Perto de Fogueira)")]
    private ParticleSystem embersParticles;

    private bool isNight;
    private bool isDawn;
    private float currentTime;
    private Texture2D softGlowFallback;
    private PlayerStatus cachedPlayerStatus;

    private void Start()
    {
        if (Application.isMobilePlatform)
        {
            maxDustMotes = Mathf.Min(maxDustMotes, 50);
            maxFireflies = Mathf.Min(maxFireflies, 35);
            maxFogParticles = Mathf.Min(maxFogParticles, 60);
            maxLeaves = Mathf.Min(maxLeaves, 25);
        }

        if (playerTransform != null)
        {
            cachedPlayerStatus = playerTransform.GetComponent<PlayerStatus>();
        }

        CreateDustMotes();
        CreateFireflies();
        CreateGroundFog();
        CreateFallingLeaves();
        CreateEmbers();
    }

    // =================== POEIRA FLUTUANTE (DIA) ===================
    private void CreateDustMotes()
    {
        GameObject obj = new GameObject("VFX_DustMotes");
        obj.transform.SetParent(transform);
        dustParticles = obj.AddComponent<ParticleSystem>();

        var main = dustParticles.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
        main.startColor = new Color(1f, 0.95f, 0.8f, 0.35f);
        main.maxParticles = maxDustMotes;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.01f;

        var emission = dustParticles.emission;
        emission.rateOverTime = Application.isMobilePlatform ? 10f : 30f;

        var shape = dustParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 8f, 20f);

        var noise = dustParticles.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;

        var colorOverLifetime = dustParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.28f, 0.2f),
                new GradientAlphaKey(0.28f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = g;

        SetupParticleRenderer(dustParticles, new Color(1f, 0.95f, 0.8f, 0.35f), isMist: false, additive: false);
        dustParticles.Stop();
    }

    // =================== VAGA-LUMES (NOITE) ===================
    private void CreateFireflies()
    {
        GameObject obj = new GameObject("VFX_Fireflies");
        obj.transform.SetParent(transform);
        fireflyParticles = obj.AddComponent<ParticleSystem>();

        var main = fireflyParticles.main;
        main.duration = 15f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new Color(0.8f, 1f, 0.3f, 0.85f);
        main.maxParticles = maxFireflies;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = fireflyParticles.emission;
        emission.rateOverTime = Application.isMobilePlatform ? 5f : 10f;

        var shape = fireflyParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(25f, 3f, 25f);

        var noise = fireflyParticles.noise;
        noise.enabled = true;
        noise.strength = 1.0f;
        noise.frequency = 2f;
        noise.scrollSpeed = 0.5f;

        var colorOverLifetime = fireflyParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.6f, 1f, 0.2f), 0f),
                new GradientColorKey(new Color(0.9f, 1f, 0.4f), 0.5f),
                new GradientColorKey(new Color(0.6f, 1f, 0.2f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.15f),
                new GradientAlphaKey(0.2f, 0.4f),
                new GradientAlphaKey(0.9f, 0.6f),
                new GradientAlphaKey(0.2f, 0.85f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = g;

        SetupParticleRenderer(fireflyParticles, new Color(0.8f, 1f, 0.3f, 0.9f), isMist: false, additive: true);
        fireflyParticles.Stop();
    }

    // =================== NÉVOA RASTEIRA (MADRUGADA) ===================
    private void CreateGroundFog()
    {
        GameObject obj = new GameObject("VFX_GroundFog");
        obj.transform.SetParent(transform);
        groundFogParticles = obj.AddComponent<ParticleSystem>();

        var main = groundFogParticles.main;
        main.duration = 20f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = Application.isMobilePlatform
            ? new ParticleSystem.MinMaxCurve(1.6f, 3.0f)
            : new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startColor = new Color(0.7f, 0.75f, 0.8f, 0.12f);
        main.maxParticles = maxFogParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.005f;

        var emission = groundFogParticles.emission;
        emission.rateOverTime = Application.isMobilePlatform ? 5f : 20f;

        var shape = groundFogParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 0.5f, 40f);

        var noise = groundFogParticles.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.2f;

        var colorOverLifetime = groundFogParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.7f, 0.75f, 0.8f), 0f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.08f, 0.3f),
                new GradientAlphaKey(0.08f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = g;

        SetupParticleRenderer(groundFogParticles, new Color(0.7f, 0.75f, 0.8f, 0.14f), isMist: true, additive: false);
        groundFogParticles.Stop();
    }

    // =================== FOLHAS AO VENTO ===================
    private void CreateFallingLeaves()
    {
        GameObject obj = new GameObject("VFX_FallingLeaves");
        obj.transform.SetParent(transform);
        leavesParticles = obj.AddComponent<ParticleSystem>();

        var main = leavesParticles.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.25f, 0.1f, 0.85f),
            new Color(0.15f, 0.25f, 0.05f, 0.85f)
        );
        main.maxParticles = maxLeaves;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.08f;
        main.startRotation3D = true;

        var emission = leavesParticles.emission;
        emission.rateOverTime = Application.isMobilePlatform ? 2.5f : 5f;

        var shape = leavesParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 10f, 30f);

        var rotOverLifetime = leavesParticles.rotationOverLifetime;
        rotOverLifetime.enabled = true;
        rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        var noise = leavesParticles.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.8f;

        SetupParticleRenderer(leavesParticles, new Color(0.35f, 0.24f, 0.12f, 0.85f), isMist: true, additive: false);
        leavesParticles.Play();
    }

    // =================== BRASAS (PERTO DE FOGUEIRA) ===================
    private void CreateEmbers()
    {
        GameObject obj = new GameObject("VFX_Embers");
        obj.transform.SetParent(transform);
        embersParticles = obj.AddComponent<ParticleSystem>();

        var main = embersParticles.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.3f, 0f, 0.9f),
            new Color(1f, 0.7f, 0.1f, 0.9f)
        );
        main.maxParticles = Application.isMobilePlatform ? 25 : 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.15f;

        var emission = embersParticles.emission;
        emission.rateOverTime = Application.isMobilePlatform ? 8f : 15f;

        var shape = embersParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.5f;

        var noise = embersParticles.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 1.5f;

        var colorOverLifetime = embersParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0f),
                new GradientColorKey(new Color(1f, 0.2f, 0f), 0.7f),
                new GradientColorKey(new Color(0.3f, 0.1f, 0f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.1f),
                new GradientAlphaKey(0.6f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = g;

        SetupParticleRenderer(embersParticles, new Color(1f, 0.5f, 0.1f, 0.95f), isMist: false, additive: true);
        embersParticles.Stop();
    }

    // =================== RENDERER HELPER ===================
    private Texture2D GetParticleTexture(bool isMist)
    {
        string resName = isMist ? "Textures/soft_mist_puff" : "Textures/soft_particle_glow";
        Texture2D tex = Resources.Load<Texture2D>(resName);
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
                    float a = Mathf.Pow(1f - d, 2.0f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            softGlowFallback.SetPixels(px);
            softGlowFallback.Apply();
        }
        return softGlowFallback;
    }

    private void SetupParticleRenderer(ParticleSystem ps, Color tint, bool isMist = false, bool additive = false)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default");
        if (shader == null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return;
        }

        Material mat = new Material(shader);

        Texture2D tex = GetParticleTexture(isMist);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        mat.color = tint;

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)(additive
            ? UnityEngine.Rendering.BlendMode.One
            : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        mat.renderQueue = 3000;

        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    // =================== UPDATE ===================
    private void Update()
    {
        if (dayNightCycle == null) return;

        currentTime = dayNightCycle.currentTime;
        isNight = currentTime >= 19f || currentTime < 6f;
        isDawn = currentTime >= 3f && currentTime < 7f;

        if (playerTransform != null)
        {
            Vector3 playerPos = playerTransform.position;

            if (dustParticles != null) dustParticles.transform.position = playerPos + Vector3.up * 3f;
            if (fireflyParticles != null) fireflyParticles.transform.position = playerPos + Vector3.up * 1f;
            if (groundFogParticles != null) groundFogParticles.transform.position = playerPos + Vector3.down * 0.5f;
            if (leavesParticles != null) leavesParticles.transform.position = playerPos + Vector3.up * 5f;
        }

        ManageParticleState(dustParticles, !isNight && !isDawn);
        ManageParticleState(fireflyParticles, isNight);
        ManageParticleState(groundFogParticles, isDawn);

        if (leavesParticles != null && weatherSystem != null)
        {
            var velocityOverLifetime = leavesParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            float wind = weatherSystem.currentWindSpeed;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(wind * 0.3f, wind * 0.8f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.8f, -0.2f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        }

        UpdateEmbers();
    }

    private void UpdateEmbers()
    {
        if (embersParticles == null || playerTransform == null) return;

        if (cachedPlayerStatus == null)
            cachedPlayerStatus = playerTransform.GetComponent<PlayerStatus>();

        if (cachedPlayerStatus != null && cachedPlayerStatus.isNearBonfire)
        {
            if (!embersParticles.isPlaying) embersParticles.Play();
            embersParticles.transform.position = playerTransform.position + playerTransform.forward * 1.5f;
        }
        else
        {
            if (embersParticles.isPlaying) embersParticles.Stop();
        }
    }

    private void ManageParticleState(ParticleSystem ps, bool shouldPlay)
    {
        if (ps == null) return;
        if (shouldPlay && !ps.isPlaying) ps.Play();
        else if (!shouldPlay && ps.isPlaying) ps.Stop();
    }
}
