using UnityEngine;
using System.Collections.Generic;

/// SceneryLightingSystem: Adiciona PointLights dinamicos a elementos do cenario.
/// Cogumelos brilham verde, fogueiras emitem luz quente, cruzes celtas emanam aura.
public class SceneryLightingSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public MapRenderer mapRenderer;
    public DayNightCycle dayNightCycle;
    
    [Header("Light Config")]
    public float mushroomLightRange = 5f;
    public float campfireLightRange = 8f;
    public float crossLightRange = 6f;
    
    private List<Light> mushroomLights = new List<Light>();
    private List<Light> campfireLights = new List<Light>();
    private List<Light> crossLights = new List<Light>();
    
    private bool initialized = false;
    
    private void Update()
    {
        if (!initialized && mapRenderer != null && mapRenderer.activeInstances != null && mapRenderer.activeInstances.Count > 0)
        {
            InitializeLights();
            initialized = true;
        }
        
        UpdateLightEffects();
    }
    
    private void InitializeLights()
    {
        if (mapRenderer == null || mapRenderer.activeInstances == null) return;
        
        foreach(var kvp in mapRenderer.activeInstances)
        {
            if (kvp.Value == null) continue;
            
            // Determinar tipo de tile pelo nome do prefab
            string name = kvp.Value.name.ToLower();
            
            if (name.Contains("glowmushroom") || name.Contains("mushroom"))
            {
                Light l = CreatePointLight(kvp.Value.transform, 
                    new Color(0.1f, 0.9f, 0.7f), mushroomLightRange, 0.6f);
                mushroomLights.Add(l);
            }
            else if (name.Contains("campfire") || name.Contains("fogueira"))
            {
                Light l = CreatePointLight(kvp.Value.transform, 
                    new Color(1f, 0.6f, 0.2f), campfireLightRange, 1.2f);
                campfireLights.Add(l);
            }
            else if (name.Contains("celticcross") || name.Contains("cruz"))
            {
                Light l = CreatePointLight(kvp.Value.transform, 
                    new Color(0.8f, 0.85f, 1f), crossLightRange, 0.4f);
                crossLights.Add(l);
            }
        }
        
        Debug.Log($"[SceneryLighting] Inicializadas {mushroomLights.Count} luzes de cogumelo, {campfireLights.Count} luzes de fogueira, {crossLights.Count} luzes de cruz.");
    }
    
    private Light CreatePointLight(Transform parent, Color color, float range, float intensity)
    {
        GameObject lightObj = new GameObject("SceneryLight");
        lightObj.transform.SetParent(parent);
        lightObj.transform.localPosition = new Vector3(0, 0.5f, 0);
        
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None; // Performance mobile
        light.renderMode = LightRenderMode.Auto;
        
        return light;
    }
    
    private void UpdateLightEffects()
    {
        float time = Time.time;
        
        // Cogumelos: pulsam suavemente
        foreach(Light l in mushroomLights)
        {
            if (l != null)
            {
                l.intensity = 0.4f + Mathf.Sin(time * 1.5f) * 0.2f;
                
                // Mais brilhantes a noite
                if (dayNightCycle != null)
                {
                    bool isNight = dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f;
                    l.range = isNight ? mushroomLightRange * 1.5f : mushroomLightRange;
                }
            }
        }
        
        // Fogueiras: flickering realista
        foreach(Light l in campfireLights)
        {
            if (l != null)
            {
                float flicker = 0.8f + Random.Range(-0.2f, 0.3f);
                l.intensity = Mathf.Lerp(l.intensity, flicker * 1.2f, Time.deltaTime * 10f);
                l.range = campfireLightRange + Mathf.Sin(time * 3f) * 0.5f;
            }
        }
        
        // Cruzes: aura constante e serena
        foreach(Light l in crossLights)
        {
            if (l != null)
            {
                l.intensity = 0.3f + Mathf.Sin(time * 0.5f) * 0.1f;
            }
        }
    }
}
