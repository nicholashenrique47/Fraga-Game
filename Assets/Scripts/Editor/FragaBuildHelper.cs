#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Utilitario de Build e Checklist Pre-Exportacao (PC Windows 64 + Android APK).
/// Valida cenas no EditorBuildSettings, Render Pipeline URP e plugins nativos FragaNative.
/// </summary>
public static class FragaBuildHelper
{
    private const string MainScenePath = "Assets/Scenes/GameScene.unity";
    private const string WinPluginPath = "Assets/Plugins/FragaNative.dll";
    private const string AndroidPluginPath = "Assets/Plugins/Android/libFragaNative.so";

    [MenuItem("Fraga/Build/Verificar Cenas, Plugins e URP")]
    public static void ValidateBuildSetupInteractive()
    {
        string report = RunChecklist(fixMissingMainScene: true, out bool allOk);
        Debug.Log($"[FragaBuildHelper]\n{report}");
        EditorUtility.DisplayDialog(
            allOk ? "Fraga Build Checklist: PRONTO" : "Fraga Build Checklist: ATENCAO",
            report,
            "OK");
    }

    [MenuItem("Fraga/Build/Build PC (Windows 64-bit)")]
    public static void BuildWindows64()
    {
        string report = RunChecklist(fixMissingMainScene: true, out bool allOk);
        if (!allOk)
        {
            Debug.LogWarning($"[FragaBuildHelper] Checklist com avisos antes do build PC:\n{report}");
        }

        string[] scenes = CollectEnabledScenePaths();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build PC Cancelado", "Nenhuma cena habilitada no EditorBuildSettings.", "OK");
            return;
        }

        string outDir = "Builds/Windows";
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "FragaGame.exe");

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport res = BuildPipeline.BuildPlayer(opts);
        if (res.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[FragaBuildHelper] Build PC concluido com sucesso em {outPath} ({res.summary.totalSize / (1024 * 1024)} MB).");
        }
        else
        {
            Debug.LogError($"[FragaBuildHelper] Falha no Build PC: {res.summary.result} ({res.summary.totalErrors} erros).");
        }
    }

    [MenuItem("Fraga/Build/Build Android APK (Staging)")]
    public static void BuildAndroidApk()
    {
        ConfigureNativePluginsForBuild();
        EnsureSafeAndroidGraphicsSettings();

        string report = RunChecklist(fixMissingMainScene: true, out bool allOk);
        if (!allOk)
        {
            Debug.LogWarning($"[FragaBuildHelper] Checklist com avisos antes do build Android:\n{report}");
        }

        string[] scenes = CollectEnabledScenePaths();
        if (scenes.Length == 0)
        {
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Build Android Cancelado", "Nenhuma cena habilitada no EditorBuildSettings.", "OK");
            return;
        }

        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

        string outDir = "Builds/Android";
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "FragaGame.apk");

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport res = BuildPipeline.BuildPlayer(opts);
        if (res.summary.result == BuildResult.Succeeded)
        {
            string fullOutPath = Path.GetFullPath(outPath);
            Debug.Log($"[FragaBuildHelper] Build Android APK concluido com sucesso em {fullOutPath} ({res.summary.totalSize / (1024 * 1024)} MB).");
            if (!Application.isBatchMode)
            {
                EditorUtility.RevealInFinder(fullOutPath);
                EditorUtility.DisplayDialog("Build Android APK Concluido", $"APK gerado com sucesso em:\n{fullOutPath}", "OK");
            }
        }
        else
        {
            Debug.LogError($"[FragaBuildHelper] Falha no Build Android: {res.summary.result} ({res.summary.totalErrors} erros).");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Falha no Build Android", $"Resultado: {res.summary.result}\nErros: {res.summary.totalErrors}\nVeja o Console da Unity para detalhes.", "OK");
            }
        }
    }

    private static void EnsureSafeAndroidGraphicsSettings()
    {
        // Mali/Android 11 Vulkan + PreTransform + URP RenderGraph pode gerar tela preta com faixas de 8px.
        // Forcar OpenGLES3 garante renderizacao completa em dispositivos mobile.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        GraphicsDeviceType[] currentApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        if (currentApis == null || currentApis.Length != 1 || currentApis[0] != GraphicsDeviceType.OpenGLES3)
        {
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        }
    }

    private static void ConfigureNativePluginsForBuild()
    {
        if (AssetImporter.GetAtPath(AndroidPluginPath) is PluginImporter androidSo)
        {
            bool dirty = false;
            if (androidSo.GetCompatibleWithAnyPlatform())
            {
                androidSo.SetCompatibleWithAnyPlatform(false);
                dirty = true;
            }
            if (!androidSo.GetCompatibleWithPlatform(BuildTarget.Android))
            {
                androidSo.SetCompatibleWithPlatform(BuildTarget.Android, true);
                dirty = true;
            }
            if (androidSo.GetPlatformData(BuildTarget.Android, "CPU") != "ARM64")
            {
                androidSo.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
                dirty = true;
            }
            if (dirty) androidSo.SaveAndReimport();
        }

        if (AssetImporter.GetAtPath(WinPluginPath) is PluginImporter winDll)
        {
            bool dirty = false;
            if (winDll.GetCompatibleWithAnyPlatform())
            {
                winDll.SetCompatibleWithAnyPlatform(false);
                dirty = true;
            }
            if (!winDll.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64))
            {
                winDll.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
                dirty = true;
            }
            if (winDll.GetCompatibleWithPlatform(BuildTarget.Android))
            {
                winDll.SetCompatibleWithPlatform(BuildTarget.Android, false);
                dirty = true;
            }
            if (dirty) winDll.SaveAndReimport();
        }
    }

    private static string RunChecklist(bool fixMissingMainScene, out bool allOk)
    {
        bool ok = true;
        List<string> lines = new List<string>();

        bool mainSceneExists = File.Exists(MainScenePath);
        lines.Add(mainSceneExists ? $"[OK] Cena principal encontrada: {MainScenePath}" : $"[FALHA] Cena nao encontrada: {MainScenePath}");
        if (!mainSceneExists) ok = false;

        if (fixMissingMainScene && mainSceneExists)
        {
            EnsureSceneEnabled(MainScenePath);
        }

        string[] enabled = CollectEnabledScenePaths();
        lines.Add(enabled.Length > 0 ? $"[OK] Cenas no Build Settings: {string.Join(", ", enabled)}" : "[FALHA] Nenhuma cena ativa no EditorBuildSettings");
        if (enabled.Length == 0) ok = false;

        bool hasUrp = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
        lines.Add(hasUrp ? "[OK] URP Asset ativo em GraphicsSettings" : "[AVISO] GraphicsSettings sem UniversalRenderPipelineAsset ativo");
        if (!hasUrp) ok = false;

        GraphicsDeviceType[] androidApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        bool hasVulkan = false;
        if (androidApis != null)
        {
            foreach (GraphicsDeviceType api in androidApis)
            {
                if (api == GraphicsDeviceType.Vulkan) hasVulkan = true;
            }
        }
        lines.Add(!hasVulkan
            ? "[OK] Android Graphics API sem Vulkan (usando OpenGLES3)"
            : "[AVISO] Android Graphics API contem Vulkan (pode causar tela preta em Mali/Android 11; recomendado OpenGLES3)");

        bool hasWinDll = File.Exists(WinPluginPath);
        bool hasAndroidSo = File.Exists(AndroidPluginPath);
        lines.Add(hasWinDll ? $"[OK] Plugin PC: {WinPluginPath}" : $"[FALHA] Ausente: {WinPluginPath}");
        lines.Add(hasAndroidSo ? $"[OK] Plugin Android: {AndroidPluginPath}" : $"[FALHA] Ausente: {AndroidPluginPath}");
        if (!hasWinDll || !hasAndroidSo) ok = false;

        allOk = ok;
        return string.Join("\n", lines);
    }

    private static void EnsureSceneEnabled(string scenePath)
    {
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i].path == scenePath)
            {
                if (!existing[i].enabled)
                {
                    existing[i].enabled = true;
                    EditorBuildSettings.scenes = existing;
                }
                return;
            }
        }

        List<EditorBuildSettingsScene> updated = new List<EditorBuildSettingsScene>(existing);
        updated.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = updated.ToArray();
    }

    private static string[] CollectEnabledScenePaths()
    {
        List<string> paths = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
            {
                paths.Add(s.path);
            }
        }
        return paths.ToArray();
    }
}
#endif
