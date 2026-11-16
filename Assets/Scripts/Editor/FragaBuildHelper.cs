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
        string report = RunChecklist(fixMissingMainScene: true, out bool allOk);
        if (!allOk)
        {
            Debug.LogWarning($"[FragaBuildHelper] Checklist com avisos antes do build Android:\n{report}");
        }

        string[] scenes = CollectEnabledScenePaths();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build Android Cancelado", "Nenhuma cena habilitada no EditorBuildSettings.", "OK");
            return;
        }

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
            Debug.Log($"[FragaBuildHelper] Build Android APK concluido com sucesso em {outPath} ({res.summary.totalSize / (1024 * 1024)} MB).");
        }
        else
        {
            Debug.LogError($"[FragaBuildHelper] Falha no Build Android: {res.summary.result} ({res.summary.totalErrors} erros).");
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
