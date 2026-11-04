using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class AutoBuildVisuals
{
    static AutoBuildVisuals()
    {
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        if (EditorPrefs.GetBool("Fraga_Fauna_Scenery_Overhaul_V15", false)) return;
        EditorPrefs.SetBool("Fraga_Fauna_Scenery_Overhaul_V15", true);

        Debug.Log("[AutoBuildVisuals] Configurando Fauna 3D (Lobos, Cervos, Ursos, Javalis, Coelhos), Ruinas Celtas e Densidade Cenica V15...");
        try
        {
            ProceduralMaterialGenerator.GenerateMaterials();
            FragaAssetGenerator.GenerateAssets();
            FragaModelBridge.SyncExternalModels();
            FragaSceneSetup.CreateGameScene();
            Debug.Log("[AutoBuildVisuals] Overhaul Fauna & Cenarios V15 Concluido com Exito!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[AutoBuildVisuals] Erro ao executar setup: " + ex);
        }
    }
}
