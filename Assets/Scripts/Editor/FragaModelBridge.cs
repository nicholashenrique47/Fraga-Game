using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class FragaModelBridge : EditorWindow
{
    private const string PREFABS_DIR = "Assets/Resources/Prefabs";

    [MenuItem("Fraga Native/4. Sincronizar Modelos 3D Externos (FBX-OBJ -> Prefabs)")]
    public static void SyncExternalModels()
    {
        if (!AssetDatabase.IsValidFolder(PREFABS_DIR))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        }

        int syncedCount = 0;

        syncedCount += ProcessCategory("Assets/Art/Models/Trees", new string[] {
            "PineTree_Prefab", "DeadTree_Prefab", "Tree_Prefab"
        }, ModelType.Tree);

        syncedCount += ProcessCategory("Assets/Art/Models/Vegetation", new string[] {
            "ThornyBush_Prefab", "Fento_Prefab", "Berry_Prefab", "Mushroom_Prefab", "GlowMushroom_Prefab"
        }, ModelType.Vegetation);

        syncedCount += ProcessCategory("Assets/Art/Models/Rocks", new string[] {
            "Rock_Prefab", "RunicStone_Prefab", "CelticCross_Prefab", "AncientGrave_Prefab"
        }, ModelType.Rock);

        syncedCount += ProcessCategory("Assets/Art/Models/Weapons", new string[] {
            "Axe_Prefab", "Torch_Prefab", "Pickaxe_Prefab", "Lantern_Prefab", "Bow_Prefab"
        }, ModelType.Weapon);

        syncedCount += ProcessCategory("Assets/Art/Models/Props", new string[] {
            "FallenLog_Prefab", "Campfire_Prefab", "Workbench_Prefab", "Barricade_Prefab"
        }, ModelType.Prop);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FragaModelBridge] Sincronização concluída! {syncedCount} modelos botânicos The Forest convertidos com sucesso!");
    }

    private enum ModelType
    {
        Tree,
        Vegetation,
        Rock,
        Weapon,
        Prop
    }

    private static int ProcessCategory(string folderPath, string[] targetPrefabNames, ModelType type)
    {
        if (!Directory.Exists(folderPath)) return 0;

        string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        int processed = 0;

        foreach (string file in files)
        {
            string ext = Path.GetExtension(file).ToLower();
            if (ext != ".fbx" && ext != ".obj") continue;

            string unityPath = file.Replace("\\", "/");
            GameObject importedModel = AssetDatabase.LoadAssetAtPath<GameObject>(unityPath);
            if (importedModel == null) continue;

            string fileName = Path.GetFileNameWithoutExtension(file).ToLower();
            string normFileName = fileName.Replace("_", "").Replace("-", "").ToLower();
            string chosenPrefabName = null;

            foreach (string target in targetPrefabNames.OrderByDescending(t => t.Length))
            {
                string normTarget = target.Replace("_Prefab", "").Replace("_", "").Replace("-", "").ToLower();
                if (normFileName.Contains(normTarget) ||
                    (normTarget == "tree" && (normFileName.Contains("oak") || normFileName == "tree")) ||
                    (normTarget == "pinetree" && normFileName.Contains("pine")) ||
                    (normTarget == "deadtree" && normFileName.Contains("dead")) ||
                    (normTarget == "axe" && (normFileName.Contains("axe") || normFileName.Contains("machado"))) ||
                    (normTarget == "torch" && normFileName.Contains("torch")) ||
                    (normTarget == "rock" && (normFileName.Contains("rock") || normFileName.Contains("stone") || normFileName.Contains("granite"))) ||
                    (normTarget == "runicstone" && (normFileName.Contains("runic") || normFileName.Contains("menhir"))) ||
                    (normTarget == "celticcross" && (normFileName.Contains("cross") || normFileName.Contains("cruceiro"))) ||
                    (normTarget == "ancientgrave" && (normFileName.Contains("grave") || normFileName.Contains("dolmen") || normFileName.Contains("anta"))) ||
                    (normTarget == "fento" && (normFileName.Contains("fento") || normFileName.Contains("fern"))) ||
                    (normTarget == "thornybush" && (normFileName.Contains("bush") || normFileName.Contains("tojo") || normFileName.Contains("arbusto"))) ||
                    (normTarget == "mushroom" && normFileName.Contains("mushroom")) ||
                    (normTarget == "campfire" && (normFileName.Contains("campfire") || normFileName.Contains("fogueira"))) ||
                    (normTarget == "fallenlog" && (normFileName.Contains("log") || normFileName.Contains("tronco"))))
                {
                    chosenPrefabName = target;
                    break;
                }
            }

            if (string.IsNullOrEmpty(chosenPrefabName))
                chosenPrefabName = targetPrefabNames[0];

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(importedModel);
            instance.name = chosenPrefabName;

            // Desempacota completamente para criar prefab autônomo e definitivo (sem erros de override de submesh)
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Configurar Shaders e Materiais PBR
            ApplyShaders(instance, chosenPrefabName, type);

            // Configurar Colisores Físicos
            ApplyColliders(instance, chosenPrefabName, type);

            // Salvar como Prefab oficial
            string destPath = $"{PREFABS_DIR}/{chosenPrefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, destPath);
            DestroyImmediate(instance);

            // Se for mushroom, salvar também como GlowMushroom_Prefab com material emissivo
            if (chosenPrefabName == "Mushroom_Prefab")
            {
                GameObject glowInstance = (GameObject)PrefabUtility.InstantiatePrefab(importedModel);
                glowInstance.name = "GlowMushroom_Prefab";
                PrefabUtility.UnpackPrefabInstance(glowInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ApplyShaders(glowInstance, "GlowMushroom_Prefab", type);
                string glowDest = $"{PREFABS_DIR}/GlowMushroom_Prefab.prefab";
                PrefabUtility.SaveAsPrefabAsset(glowInstance, glowDest);
                DestroyImmediate(glowInstance);
                processed++;
            }

            Debug.Log($"[FragaModelBridge] Modelo '{unityPath}' sincronizado com sucesso como '{destPath}'.");
            processed++;
        }

        return processed;
    }

    private static void ApplyShaders(GameObject root, string prefabName, ModelType type)
    {
        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        Material pineNeedleMat = Resources.Load<Material>("Materials/Mat_PineNeedle");
        Material oakFoliageMat = Resources.Load<Material>("Materials/Mat_OakFoliage");
        Material foliageMat = Resources.Load<Material>("Materials/Mat_FoliageSway");
        Material rockMat = Resources.Load<Material>("Materials/Celtic_Stone");
        Material mossMat = Resources.Load<Material>("Materials/Mat_DarkForest_Moss");
        Material woodMat = Resources.Load<Material>("Materials/Wood_Bark");
        Material ironMat = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        Material glowMushroomMat = Resources.Load<Material>("Materials/Mat_Emissive_MushroomGlow");

        Material treeFoliage = prefabName.Contains("Pine") ? (pineNeedleMat ?? foliageMat) : (oakFoliageMat ?? foliageMat);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            int submeshCount = 1;
            if (mf != null && mf.sharedMesh != null)
            {
                submeshCount = Mathf.Max(1, mf.sharedMesh.subMeshCount);
            }
            else if (r.sharedMaterials != null && r.sharedMaterials.Length > 0)
            {
                submeshCount = r.sharedMaterials.Length;
            }

            Material[] mats = new Material[submeshCount];
            for (int i = 0; i < submeshCount; i++)
            {
                if (prefabName.Contains("Glow") && glowMushroomMat != null)
                {
                    mats[i] = glowMushroomMat;
                }
                else if (type == ModelType.Tree)
                {
                    if (i == 0) mats[i] = barkMat;
                    else mats[i] = treeFoliage;
                }
                else if (type == ModelType.Vegetation)
                {
                    mats[i] = oakFoliageMat ?? foliageMat;
                }
                else if (type == ModelType.Rock)
                {
                    mats[i] = rockMat;
                }
                else if (type == ModelType.Weapon)
                {
                    if (i == 0) mats[i] = woodMat;
                    else mats[i] = ironMat;
                }
                else if (type == ModelType.Prop)
                {
                    if (prefabName.Contains("Campfire"))
                    {
                        if (i == 0) mats[i] = rockMat;
                        else mats[i] = woodMat;
                    }
                    else if (prefabName.Contains("Log"))
                    {
                        if (i == 0) mats[i] = barkMat;
                        else mats[i] = mossMat;
                    }
                    else
                    {
                        if (i == 0) mats[i] = woodMat;
                        else mats[i] = mossMat;
                    }
                }
                else
                {
                    mats[i] = barkMat;
                }
            }
            r.sharedMaterials = mats;
        }
    }

    private static void ApplyColliders(GameObject root, string prefabName, ModelType type)
    {
        foreach (var c in root.GetComponentsInChildren<Collider>())
        {
            DestroyImmediate(c);
        }

        if (type == ModelType.Tree)
        {
            CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
            if (prefabName.Contains("Pine"))
            {
                cc.center = new Vector3(0, 7.0f, 0);
                cc.radius = 0.50f;
                cc.height = 14.0f;
            }
            else if (prefabName.Contains("Dead"))
            {
                cc.center = new Vector3(0, 3.2f, 0);
                cc.radius = 0.40f;
                cc.height = 6.4f;
            }
            else
            {
                cc.center = new Vector3(0, 4.5f, 0);
                cc.radius = 0.58f;
                cc.height = 9.0f;
            }
        }
        else if (type == ModelType.Rock)
        {
            MeshFilter mf = root.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = root.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
        }
        else if (type == ModelType.Prop)
        {
            if (prefabName.Contains("Log"))
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 0.32f, 0);
                bc.size = new Vector3(3.5f, 0.7f, 0.7f);
            }
            else if (prefabName.Contains("Campfire"))
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 0.15f, 0);
                bc.size = new Vector3(1.8f, 0.35f, 1.8f);
            }
            else
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 0.5f, 0);
                bc.size = Vector3.one;
            }
        }
    }
}
