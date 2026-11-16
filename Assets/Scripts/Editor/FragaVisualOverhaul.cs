using UnityEngine;
using UnityEditor;

public class FragaVisualOverhaul : EditorWindow
{
    [MenuItem("Fraga/Overhaul Visual (Atmosfera Terror)")]
    public static void ApplyOverhaul()
    {
        Debug.Log("[Fraga Visual] Iniciando o Overhaul de Atmosfera...");
        
        SetupLightingAndFog();
        
        Debug.Log("[Fraga Visual] Concluido! Recomendado: Adicione um Global Volume com Vignette, Film Grain e Contrast no Unity.");
    }

    private static void SetupLightingAndFog()
    {
        // Nevoeiro Denso (Terror Atmosférico)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.08f;
        RenderSettings.fogColor = new Color(0.02f, 0.04f, 0.06f); // Azul noite esverdeado (Sickly Night)
        
        // Iluminação Ambiente (Extremamente Escura)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.01f, 0.01f, 0.02f); // Quase breu

        // Lua Direcional Fria
        Light[] lights = FindObjectsByType<Light>();
        Light dirLight = null;
        foreach(var l in lights)
        {
            if (l.type == LightType.Directional) { dirLight = l; break; }
        }

        if (dirLight == null)
        {
            GameObject lightObj = new GameObject("Moonlight");
            dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.transform.rotation = Quaternion.Euler(45, -30, 0);
        }

        dirLight.color = new Color(0.4f, 0.6f, 0.9f); // Azul lunar frio
        dirLight.intensity = 0.3f; // Fraca para manter a escuridão
        dirLight.shadows = LightShadows.Soft;
        dirLight.shadowStrength = 0.9f;
        
        // Assegurar que a câmara principal tem o far plane curto para o nevoeiro fazer sentido
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.backgroundColor = RenderSettings.fogColor;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.farClipPlane = 40f; // Claustrofobia
        }
        
        Debug.Log("[Fraga Visual] Nevoeiro Exponencial, Ambiente, Luz Lunar e Câmara Claustrofóbica configurados com sucesso!");
    }
}
