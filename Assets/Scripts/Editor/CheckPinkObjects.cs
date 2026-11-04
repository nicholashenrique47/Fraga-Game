using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

[InitializeOnLoad]
public static class CheckPinkObjects
{
    static CheckPinkObjects()
    {
        EditorApplication.delayCall += RunDiagnosis;
    }

    [MenuItem("Fraga Native/Diagnosticar Objetos Rosa")]
    public static void RunDiagnosis()
    {
        Debug.Log("=== [DIAGNÓSTICO DE MATERIAIS / SHADERS / ROSA] ===");

        // 1. Verificar todos os materiais em Assets/Resources/Materials
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Resources/Materials" });
        foreach (var guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogError($"[DIAG MAT] Material nulo em: {path}");
                continue;
            }
            if (mat.shader == null)
            {
                Debug.LogError($"[DIAG MAT ROSA] Material SEM SHADER (Rosa!): {path}");
            }
            else if (mat.shader.name.Contains("Error") || mat.shader.name == "Hidden/InternalErrorShader")
            {
                Debug.LogError($"[DIAG MAT ROSA] Material com Shader de Erro: {path} -> {mat.shader.name}");
            }
            else
            {
                Debug.Log($"[DIAG MAT OK] {mat.name} -> Shader: {mat.shader.name}");
            }
        }

        // 2. Verificar todos os prefabs em Assets/Resources/Prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs" });
        foreach (var guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Renderer[] rends = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r.sharedMaterials == null || r.sharedMaterials.Length == 0)
                {
                    Debug.LogError($"[DIAG PREFAB ROSA] Prefab {prefab.name} -> {r.name} NÃO TEM MATERIAIS!");
                }
                else
                {
                    for (int i = 0; i < r.sharedMaterials.Length; i++)
                    {
                        var m = r.sharedMaterials[i];
                        if (m == null)
                        {
                            Debug.LogError($"[DIAG PREFAB ROSA] Prefab {prefab.name} -> {r.name} tem MATERIAL NULO no slot {i}!");
                        }
                        else if (m.shader == null || m.shader.name.Contains("Error") || m.shader.name == "Hidden/InternalErrorShader")
                        {
                            Debug.LogError($"[DIAG PREFAB ROSA] Prefab {prefab.name} -> {r.name} slot {i} tem SHADER COM ERRO: {m.name} ({m.shader?.name})");
                        }
                    }
                }
            }
        }

        // 3. Verificar o que existe na cena GameScene.unity
        var scene = EditorSceneManager.GetActiveScene();
        Debug.Log($"[DIAG SCENE] Cena ativa: {scene.name} (Path: {scene.path})");
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (var root in rootObjects)
        {
            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r.sharedMaterials == null || r.sharedMaterials.Length == 0)
                {
                    Debug.LogError($"[DIAG CENA ROSA] {root.name}/{r.name} NÃO TEM MATERIAIS!");
                }
                else
                {
                    for (int i = 0; i < r.sharedMaterials.Length; i++)
                    {
                        var m = r.sharedMaterials[i];
                        if (m == null)
                        {
                            Debug.LogError($"[DIAG CENA ROSA] {root.name}/{r.name} MATERIAL NULO no slot {i}!");
                        }
                        else if (m.shader == null || m.shader.name.Contains("Error") || m.shader.name == "Hidden/InternalErrorShader")
                        {
                            Debug.LogError($"[DIAG CENA ROSA] {root.name}/{r.name} slot {i} SHADER COM ERRO ({m.name})!");
                        }
                    }
                }
            }
        }
        Debug.Log("=== [FIM DO DIAGNÓSTICO] ===");
    }
}
