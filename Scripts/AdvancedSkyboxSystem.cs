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

    private void Start()
    {
        CreateSkybox();
        CreateStars();
        CreateMoon();
    }

    private void CreateSkybox()
    {
        skyboxMat = new Material(Shader.Find("Skybox/Solid Color"));
        if (skyboxMat.shader == null) skyboxMat = new Material(Shader.Find("Standard"));
        
        RenderSettings.skybox = skyboxMat;
        DynamicGI.UpdateEnvironment();
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
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startColor = new Color(1f, 1f, 1f, 0.8f);
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
        Material starMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (starMat.shader == null) starMat = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material = starMat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private void CreateMoon()
    {
        moonObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moonObject.name = "VFX_Moon";
        moonObject.transform.SetParent(transform);
        moonObject.transform.localScale = new Vector3(20f, 20f, 20f);
        
        Destroy(moonObject.GetComponent<Collider>());
        
        Material moonMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (moonMat.shader == null) moonMat = new Material(Shader.Find("Unlit/Color"));
        
        moonMat.color = new Color(0.95f, 0.95f, 1f); // Branco luar
        moonObject.GetComponent<Renderer>().material = moonMat;
        
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

        if (skyboxMat != null)
        {
            if (skyboxMat.HasProperty("_Color")) skyboxMat.SetColor("_Color", targetColor);
            if (skyboxMat.HasProperty("_Tint")) skyboxMat.SetColor("_Tint", targetColor);
        }

        // 2. Estrelas
        bool isNight = time >= 19f || time < 5f;
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
            }
            else
            {
                if (moonObject.activeSelf) moonObject.SetActive(false);
            }
        }
    }
}
