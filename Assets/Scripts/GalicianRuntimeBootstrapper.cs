using UnityEngine;

/// <summary>
/// Inicializador em Tempo de Execução do Plano Mestre Galego (The Forest AAA).
/// Garante que, ao dar Play no Unity Editor ou iniciar a build, todos os sistemas
/// galegos (iluminação lunar anti-breu, clima dinâmico, acústica, orientação horizontal e otimização 60 FPS)
/// estejam ativos e calibrados automaticamente.
/// </summary>
public static class GalicianRuntimeBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeGalicianSystemsOnPlay()
    {
        // 0. Trava a orientação da tela em Horizontal (Landscape) para Mobile / Unity Remote
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // 1. Verifica se os sistemas ja existem na cena ativa
        GameObject systemsRoot = GameObject.Find("[GALICIAN_ENVIRONMENT_SYSTEMS]");
        if (systemsRoot == null)
        {
            systemsRoot = new GameObject("[GALICIAN_ENVIRONMENT_SYSTEMS]");
            Object.DontDestroyOnLoad(systemsRoot);
        }

        // 2. Garante a presenca do Gerenciador de Otimizacao e Visibilidade da Camera
        var optimizationMgr = systemsRoot.GetComponent<GalicianOptimizationManager>();
        if (optimizationMgr == null)
        {
            optimizationMgr = systemsRoot.AddComponent<GalicianOptimizationManager>();
        }
        optimizationMgr.ApplyFramerateAndQualitySettings();
        optimizationMgr.ApplyCameraVisibilityAndCulling();
        optimizationMgr.ApplyURPOptimizations();

        // 3. Garante a presenca do Driver de Iluminacao Atmosferica
        var lightingDriver = systemsRoot.GetComponent<GalicianAtmosphericLightingDriver>();
        if (lightingDriver == null)
        {
            lightingDriver = systemsRoot.AddComponent<GalicianAtmosphericLightingDriver>();
        }
        lightingDriver.FindReferences();
        lightingDriver.ApplyAtmosphericCalibration();

        // 4. Garante a presenca do Motor de Clima Galego
        var weatherEngine = systemsRoot.GetComponent<GalicianWeatherEngine>();
        if (weatherEngine == null)
        {
            weatherEngine = systemsRoot.AddComponent<GalicianWeatherEngine>();
        }
        if (weatherEngine.mainDirectionalLight == null && lightingDriver.directionalLight != null)
        {
            weatherEngine.mainDirectionalLight = lightingDriver.directionalLight;
        }

        // 5. Garante a presenca do Controlador de Audio e Oclusao
        var audioAtmosphere = systemsRoot.GetComponent<GalicianAudioAtmosphere>();
        if (audioAtmosphere == null)
        {
            audioAtmosphere = systemsRoot.AddComponent<GalicianAudioAtmosphere>();
        }

        // 6. Garante a presenca do Detector Fisico de Passos
        var footstepDetector = systemsRoot.GetComponent<GalicianFootstepSurfaceDetector>();
        if (footstepDetector == null)
        {
            footstepDetector = systemsRoot.AddComponent<GalicianFootstepSurfaceDetector>();
        }

        Debug.Log("<color=#44FF88><b>[Fraga AAA]</b> Sistemas de Otimizacao 60 FPS, Visibilidade e Iluminacao Galega ativados com sucesso!</color>");
    }
}
