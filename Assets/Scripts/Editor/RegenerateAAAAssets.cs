using UnityEngine;
using UnityEditor;

/// <summary>
/// Utilitário de Editor para Regenerar Imediatamente os Modelos e Prefabs AAA da Fraga.
/// Substitui os cones empilhados e machado cilíndrico primitivo pelas novas malhas orgânicas.
/// </summary>
public static class RegenerateAAAAssets
{
    [MenuItem("Fraga Native/Regenerar Modelos AAA (Arvores e Machado The Forest)")]
    [InitializeOnLoadMethod]
    public static void ExecuteRegeneration()
    {
        string dir = "Assets/Resources/Prefabs";
        if (!System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        Debug.Log("[Fraga AAA Overhaul] Iniciando regeneração botânica e de armas...");

        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        Material oakFolMat = Resources.Load<Material>("Materials/Mat_OakFoliage");
        Material pineNeedleMat = Resources.Load<Material>("Materials/Mat_PineNeedle");
        Material ironMat = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        Material woodBarkMat = Resources.Load<Material>("Materials/Wood_Bark");
        Material stoneMat = Resources.Load<Material>("Materials/Celtic_Stone");

        // 1. Regenerar Tree_Prefab (Carvalho Galego)
        GameObject oakObj = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
            GalicianProceduralTreeGenerator.TreeType.GalicianOak, barkMat, oakFolMat);
        PrefabUtility.SaveAsPrefabAsset(oakObj, dir + "/Tree_Prefab.prefab");
        Object.DestroyImmediate(oakObj);
        Debug.Log("[Fraga AAA Overhaul] Tree_Prefab.prefab (Carvalho) regenerado com sucesso.");

        // 2. Regenerar PineTree_Prefab (Pinheiro Bravo)
        GameObject pineObj = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
            GalicianProceduralTreeGenerator.TreeType.MaritimePine, barkMat, pineNeedleMat);
        PrefabUtility.SaveAsPrefabAsset(pineObj, dir + "/PineTree_Prefab.prefab");
        Object.DestroyImmediate(pineObj);
        Debug.Log("[Fraga AAA Overhaul] PineTree_Prefab.prefab (Pinheiro) regenerado com sucesso.");

        // 3. Regenerar DeadTree_Prefab (Árvore Seca)
        GameObject deadObj = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
            GalicianProceduralTreeGenerator.TreeType.DeadTree, barkMat, null);
        PrefabUtility.SaveAsPrefabAsset(deadObj, dir + "/DeadTree_Prefab.prefab");
        Object.DestroyImmediate(deadObj);
        Debug.Log("[Fraga AAA Overhaul] DeadTree_Prefab.prefab (Árvore Seca) regenerado com sucesso.");

        // 4. Regenerar Axe_Prefab (Machado Tradicional de Sobrevivência)
        GameObject axeObj = ProceduralSurvivalAxeGenerator.BuildAxeGameObject(woodBarkMat, ironMat, stoneMat);
        PrefabUtility.SaveAsPrefabAsset(axeObj, dir + "/Axe_Prefab.prefab");
        Object.DestroyImmediate(axeObj);
        Debug.Log("[Fraga AAA Overhaul] Axe_Prefab.prefab (Machado AAA) regenerado com sucesso.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[Fraga AAA Overhaul] Concluído com êxito! Todos os ativos foram atualizados para o padrão The Forest.</color>");
    }
}
