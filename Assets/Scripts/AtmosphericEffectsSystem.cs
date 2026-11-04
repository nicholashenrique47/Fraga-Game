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

    private void Start()
    {
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
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new Color(1f, 0.95f, 0.8f, 0.3f);
        main.maxParticles = maxDustMotes;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.01f; // Levemente flutuante para cima

        var emission = dustParticles.emission;
        emission.rateOverTime = 30f;

        var shape = dustParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 8f, 20f);

        // Movimento errático (turbulência)
        var noise = dustParticles.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.2f;

        // Fade in/out
        var colorOverLifetime = dustParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.25f, 0.2f),
                new GradientAlphaKey(0.25f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = g;

        SetupParticleRenderer(dustParticles, new Color(1f, 0.95f, 0.8f, 0.3f));
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
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new Color(0.8f, 1f, 0.3f, 0.8f); // Verde-amarelo bioluminescente
        main.maxParticles = maxFireflies;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = fireflyParticles.emission;
        emission.rateOverTime = 10f;

        var shape = fireflyParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(25f, 3f, 25f);

        // Movimento errático mais agressivo (vaga-lumes ziguezagueiam)
        var noise = fireflyParticles.noise;
        noise.enabled = true;
        noise.strength = 1.0f;
        noise.frequency = 2f;
        noise.scrollSpeed = 0.5f;

        // Piscar (alpha oscilante)
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

        SetupParticleRenderer(fireflyParticles, new Color(0.8f, 1f, 0.3f, 0.9f));
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
        main.startSize = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startColor = new Color(0.7f, 0.75f, 0.8f, 0.08f); // Muito sutil
        main.maxParticles = maxFogParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.005f; // Sobe lentamente

        var emission = groundFogParticles.emission;
        emission.rateOverTime = 20f;

        var shape = groundFogParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 0.5f, 40f); // Rente ao chão

        var noise = groundFogParticles.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.2f;

        // Fade
        var colorOverLifetime = groundFogParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.7f, 0.75f, 0.8f), 0f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.06f, 0.3f),
                new GradientAlphaKey(0.06f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = g;

        SetupParticleRenderer(groundFogParticles, new Color(0.7f, 0.75f, 0.8f, 0.1f));
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
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.25f, 0.1f, 0.8f),   // Folha seca castanha
            new Color(0.15f, 0.25f, 0.05f, 0.8f)   // Folha verde escura
        );
        main.maxParticles = maxLeaves;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.08f; // Cai lentamente
        main.startRotation3D = true;

        var emission = leavesParticles.emission;
        emission.rateOverTime = 5f;

        var shape = leavesParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 10f, 30f);

        // Rotação ao cair
        var rotOverLifetime = leavesParticles.rotationOverLifetime;
        rotOverLifetime.enabled = true;
        rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        var noise = leavesParticles.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.8f;

        SetupParticleRenderer(leavesParticles, new Color(0.3f, 0.2f, 0.1f, 0.8f));
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
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.3f, 0f, 0.9f),    // Laranja
            new Color(1f, 0.7f, 0.1f, 0.9f)    // Amarelo
        );
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.15f; // Sobem

        var emission = embersParticles.emission;
        emission.rateOverTime = 15f;

        var shape = embersParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.5f;

        var noise = embersParticles.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 1.5f;

        // Fade out
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

        SetupParticleRenderer(embersParticles, new Color(1f, 0.5f, 0.1f, 0.9f));
        embersParticles.Stop();
    }

    // =================== RENDERER HELPER ===================
    private void SetupParticleRenderer(ParticleSystem ps, Color tint)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (mat.shader == null) mat = new Material(Shader.Find("Particles/Standard Unlit"));

        mat.color = tint;
        // Ativar transparência
        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
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

        // Posicionar partículas ao redor do jogador
        if (playerTransform != null)
        {
            Vector3 playerPos = playerTransform.position;

            if (dustParticles != null) dustParticles.transform.position = playerPos + Vector3.up * 3f;
            if (fireflyParticles != null) fireflyParticles.transform.position = playerPos + Vector3.up * 1f;
            if (groundFogParticles != null) groundFogParticles.transform.position = playerPos + Vector3.down * 0.5f;
            if (leavesParticles != null) leavesParticles.transform.position = playerPos + Vector3.up * 5f;
        }

        // Controlar ativação por período do dia
        ManageParticleState(dustParticles, !isNight && !isDawn);  // Poeira durante o dia
        ManageParticleState(fireflyParticles, isNight);            // Vaga-lumes à noite
        ManageParticleState(groundFogParticles, isDawn);           // Névoa na madrugada

        // Ajustar velocidade das folhas com o vento
        if (leavesParticles != null && weatherSystem != null)
        {
            var velocityOverLifetime = leavesParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            float wind = weatherSystem.currentWindSpeed;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(wind * 0.3f, wind * 0.8f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.8f, -0.2f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        }

        // Brasas: Ativar perto de fogueiras
        UpdateEmbers();
    }

    private void UpdateEmbers()
    {
        if (embersParticles == null || playerTransform == null) return;

        // Verifica se o jogador está perto de uma fogueira
        PlayerStatus ps = playerTransform.GetComponent<PlayerStatus>();
        if (ps != null && ps.isNearBonfire)
        {
            if (!embersParticles.isPlaying) embersParticles.Play();
            // Posicionar brasas na fogueira mais próxima (estimativa via posição do jogador)
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
