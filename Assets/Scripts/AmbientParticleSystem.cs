using UnityEngine;

/// AmbientParticleSystem: Gere particulas atmosfericas dinamicas baseadas no ciclo dia/noite.
/// Pollen durante o dia, cinzas/esporos a noite, partículas de nevoeiro em vales.
public class AmbientParticleSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public Transform playerTransform;
    
    private ParticleSystem pollenParticles;
    private ParticleSystem ashParticles;
    private ParticleSystem sporeParticles;
    private ParticleSystem mistParticles;
    
    private void Awake()
    {
        CreatePollenSystem();
        CreateAshSystem();
        CreateSporeSystem();
        CreateMistSystem();
    }
    
    private void CreatePollenSystem()
    {
        GameObject obj = new GameObject("VFX_Pollen");
        obj.transform.SetParent(transform);
        pollenParticles = obj.AddComponent<ParticleSystem>();
        
        var main = pollenParticles.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new Color(1f, 0.95f, 0.6f, 0.6f); // Amarelo dourado
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = pollenParticles.emission;
        emission.rateOverTime = 30f;
        
        var shape = pollenParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 4f, 20f);
        
        var vel = pollenParticles.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.05f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        
        pollenParticles.Stop();
    }
    
    private void CreateAshSystem()
    {
        GameObject obj = new GameObject("VFX_Ash");
        obj.transform.SetParent(transform);
        ashParticles = obj.AddComponent<ParticleSystem>();
        
        var main = ashParticles.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startColor = new Color(0.4f, 0.4f, 0.45f, 0.5f); // Cinzento
        main.maxParticles = 150;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = ashParticles.emission;
        emission.rateOverTime = 20f;
        
        var shape = ashParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(15f, 5f, 15f);
        
        var vel = ashParticles.velocityOverLifetime;
        vel.enabled = true;
        vel.y = new ParticleSystem.MinMaxCurve(-0.3f, -0.05f); // Cinzas caem
        
        ashParticles.Stop();
    }
    
    private void CreateSporeSystem()
    {
        GameObject obj = new GameObject("VFX_Spores");
        obj.transform.SetParent(transform);
        sporeParticles = obj.AddComponent<ParticleSystem>();
        
        var main = sporeParticles.main;
        main.duration = 8f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new Color(0.2f, 0.8f, 0.5f, 0.4f); // Verde esporal
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = sporeParticles.emission;
        emission.rateOverTime = 15f;
        
        var shape = sporeParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 3f;
        
        var vel = sporeParticles.velocityOverLifetime;
        vel.enabled = true;
        vel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.3f); // Esporos sobem
        
        sporeParticles.Play();
    }
    
    private void CreateMistSystem()
    {
        GameObject obj = new GameObject("VFX_Mist");
        obj.transform.SetParent(transform);
        mistParticles = obj.AddComponent<ParticleSystem>();
        
        var main = mistParticles.main;
        main.duration = 15f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startColor = new Color(0.6f, 0.6f, 0.7f, 0.12f); // Nevoeiro translucido
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = mistParticles.emission;
        emission.rateOverTime = 5f;
        
        var shape = mistParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 0.5f, 30f);
        
        mistParticles.Stop();
    }
    
    private void Update()
    {
        if (playerTransform == null) return;
        
        // Seguir o jogador
        transform.position = playerTransform.position;
        
        if (dayNightCycle == null) return;
        
        float time = dayNightCycle.currentTime;
        bool isDay = time >= 6f && time < 19f;
        bool isDusk = time >= 17f && time < 20f;
        bool isNight = time >= 20f || time < 5f;
        bool isDawn = time >= 5f && time < 7f;
        
        // Pollen: ativo durante o dia
        if (pollenParticles != null)
        {
            if (isDay && !pollenParticles.isPlaying) pollenParticles.Play();
            else if (!isDay && pollenParticles.isPlaying) pollenParticles.Stop();
        }
        
        // Cinzas: ativas a noite
        if (ashParticles != null)
        {
            if (isNight && !ashParticles.isPlaying) ashParticles.Play();
            else if (!isNight && ashParticles.isPlaying) ashParticles.Stop();
        }
        
        // Nevoeiro: ativo ao entardecer e ao amanhecer
        if (mistParticles != null)
        {
            if ((isDusk || isDawn || isNight) && !mistParticles.isPlaying) mistParticles.Play();
            else if (!isDusk && !isDawn && !isNight && mistParticles.isPlaying) mistParticles.Stop();
        }
    }
}
