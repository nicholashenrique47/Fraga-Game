#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Integrador e Executor Automatizado do Plano Mestre Galego (The Forest AAA).
/// Adiciona e calibra todos os sistemas de iluminacao, clima, audio e otimizacao
/// diretamente na cena ativa de jogo (GameScene.unity).
/// </summary>
public static class GalicianMasterPlanIntegrator
{
    private const string TargetScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Fraga/Executar Plano Mestre Galego (The Forest AAA)")]
    public static void ExecuteGalicianMasterPlan()
    {
        Debug.Log("<color=#44FF88><b>[Fraga Master Plan]</b> Iniciando execucao do plano mestre galego...</color>");

        // 1. Abre a cena principal se necessario
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != TargetScenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                currentScene = EditorSceneManager.OpenScene(TargetScenePath);
            }
        }

        // 2. Localiza ou cria o objeto raiz de sistemas galegos
        GameObject systemsRoot = GameObject.Find("[GALICIAN_ENVIRONMENT_SYSTEMS]");
        if (systemsRoot == null)
        {
            systemsRoot = new GameObject("[GALICIAN_ENVIRONMENT_SYSTEMS]");
            Undo.RegisterCreatedObjectUndo(systemsRoot, "Criar Sistemas Galegos");
        }

        // 3. Integra o Driver de Iluminacao Atmosferica
        var lightingDriver = systemsRoot.GetComponent<GalicianAtmosphericLightingDriver>();
        if (lightingDriver == null)
        {
            lightingDriver = systemsRoot.AddComponent<GalicianAtmosphericLightingDriver>();
        }
        lightingDriver.FindReferences();
        lightingDriver.ApplyAtmosphericCalibration();
        Debug.Log("[Fraga Master Plan] Driver de Iluminacao Atmosferica calibrado com sucesso.");

        // 4. Integra o Motor de Clima Dinamico Galego
        var weatherEngine = systemsRoot.GetComponent<GalicianWeatherEngine>();
        if (weatherEngine == null)
        {
            weatherEngine = systemsRoot.AddComponent<GalicianWeatherEngine>();
        }
        if (weatherEngine.mainDirectionalLight == null && lightingDriver.directionalLight != null)
        {
            weatherEngine.mainDirectionalLight = lightingDriver.directionalLight;
        }
        weatherEngine.ApplyInstantWeather(GalicianWeatherState.Orballo);
        Debug.Log("[Fraga Master Plan] Motor de Clima Dinamico Galego integrado.");

        // 5. Integra a Oclusao de Copas e Audio Acustico
        var audioAtmosphere = systemsRoot.GetComponent<GalicianAudioAtmosphere>();
        if (audioAtmosphere == null)
        {
            audioAtmosphere = systemsRoot.AddComponent<GalicianAudioAtmosphere>();
        }
        Debug.Log("[Fraga Master Plan] Acustica e Oclusao de Copas configuradas.");

        // 6. Integra o Detector Fisico de Passos
        var footstepDetector = systemsRoot.GetComponent<GalicianFootstepSurfaceDetector>();
        if (footstepDetector == null)
        {
            footstepDetector = systemsRoot.AddComponent<GalicianFootstepSurfaceDetector>();
        }
        Debug.Log("[Fraga Master Plan] Detector Fisico de Passos ativo.");

        // 7. Integra o Gerenciador de Otimizacao Extrema
        var optimizationMgr = systemsRoot.GetComponent<GalicianOptimizationManager>();
        if (optimizationMgr == null)
        {
            optimizationMgr = systemsRoot.AddComponent<GalicianOptimizationManager>();
        }
        optimizationMgr.ApplyCullingDistances();
        Debug.Log("[Fraga Master Plan] Otimizacao e Culling Distances aplicados.");

        // 8. Salva a cena modificada
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);

        Debug.Log("<color=#00D9FF><b>[Fraga Master Plan]</b> Execucao concluida com 100% de sucesso! Cena GameScene.unity atualizada.</color>");
    }
}
#endif
