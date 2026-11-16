using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class FragaModelBridge : EditorWindow
{
    private const string PREFABS_DIR = "Assets/Resources/Prefabs";
    private const string FAUNA_MESHES_DIR = "Assets/Art/Meshes/Fauna";

    [MenuItem("Fraga Native/4. Sincronizar Modelos 3D Externos (FBX-OBJ -> Prefabs)")]
    public static void SyncExternalModels()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PREFABS_DIR))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Meshes"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets", "Art");
            AssetDatabase.CreateFolder("Assets/Art", "Meshes");
        }
        if (!AssetDatabase.IsValidFolder(FAUNA_MESHES_DIR))
            AssetDatabase.CreateFolder("Assets/Art/Meshes", "Fauna");

        AssetDatabase.Refresh();

        int syncedCount = 0;

        syncedCount += ProcessCategory("Assets/Art/Models/Trees", new string[] {
            "PineTree_Prefab", "DeadTree_Prefab", "Tree_Prefab"
        }, ModelType.Tree);

        syncedCount += ProcessCategory("Assets/Art/Models/Vegetation", new string[] {
            "ThornyBush_Prefab", "Fento_Prefab", "Berry_Prefab", "Mushroom_Prefab", "GlowMushroom_Prefab"
        }, ModelType.Vegetation);

        syncedCount += ProcessCategory("Assets/Art/Models/Rocks", new string[] {
            "Rock_Prefab", "CliffBoulder_Prefab", "RunicStone_Prefab", "CelticCross_Prefab", "AncientGrave_Prefab",
            "Ruin_Prefab", "CastroRoundhouse_Prefab", "StoneCircleHenge_Prefab", "CaveArchGrotto_Prefab",
            "FarolCelta_Prefab", "Altar_Prefab"
        }, ModelType.Rock);

        syncedCount += ProcessCategory("Assets/Art/Models/Entities", new string[] {
            "Vulto_Prefab", "Stalker_Prefab", "Boss_Prefab", "Player_Prefab"
        }, ModelType.Entity);

        syncedCount += ProcessCategory("Assets/Art/Models/Fauna", new string[] {
            "Wolf_Prefab", "Deer_Prefab", "Bear_Prefab", "Boar_Prefab", "Rabbit_Prefab", "Crow_Prefab", "Eagle_Prefab"
        }, ModelType.Fauna);

        syncedCount += ProcessCategory("Assets/Art/Models/Weapons", new string[] {
            "Axe_Prefab", "Torch_Prefab", "Pickaxe_Prefab", "Lantern_Prefab",
            "Bow_Prefab", "Arrow_Prefab", "FishingRod_Prefab", "Hoe_Prefab"
        }, ModelType.Weapon);

        syncedCount += ProcessCategory("Assets/Art/Models/Props", new string[] {
            "FallenLog_Prefab", "Campfire_Prefab", "Workbench_Prefab", "Barricade_Prefab",
            "Pond_Prefab", "WaterCatcher_Prefab", "Cauldron_Prefab", "Farmland_Prefab",
            "SnareTrap_Prefab", "Herb_Prefab", "Bandage_Prefab",
            "CrashCampShelter_Prefab", "WoodenDockBridge_Prefab", "SwampTotem_Prefab"
        }, ModelType.Prop);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FragaModelBridge] Sincronização concluída! {syncedCount} modelos HD convertidos com sucesso em Prefabs!");
    }

    private enum ModelType
    {
        Tree,
        Vegetation,
        Rock,
        Entity,
        Fauna,
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
            if (type == ModelType.Fauna)
            {
                ModelImporter mi = AssetImporter.GetAtPath(unityPath) as ModelImporter;
                if (mi != null && !mi.isReadable)
                {
                    mi.isReadable = true;
                    mi.SaveAndReimport();
                }
            }
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
                    (normTarget == "axe" && (normFileName.Contains("axe") || normFileName.Contains("machado")) && !normFileName.Contains("pick")) ||
                    (normTarget == "pickaxe" && normFileName.Contains("pickaxe")) ||
                    (normTarget == "torch" && normFileName.Contains("torch")) ||
                    (normTarget == "lantern" && normFileName.Contains("lantern")) ||
                    (normTarget == "bow" && normFileName.Contains("bow")) ||
                    (normTarget == "arrow" && normFileName.Contains("arrow")) ||
                    (normTarget == "fishingrod" && normFileName.Contains("fishing")) ||
                    (normTarget == "hoe" && normFileName.Contains("hoe")) ||
                    (normTarget == "cliffboulder" && normFileName.Contains("cliff")) ||
                    (normTarget == "rock" && (normFileName.Contains("rock") || normFileName.Contains("granite")) && !normFileName.Contains("cliff")) ||
                    (normTarget == "runicstone" && (normFileName.Contains("runic") || normFileName.Contains("menhir"))) ||
                    (normTarget == "celticcross" && (normFileName.Contains("cross") || normFileName.Contains("cruceiro"))) ||
                    (normTarget == "ancientgrave" && (normFileName.Contains("grave") || normFileName.Contains("sepultura"))) ||
                    (normTarget == "castroroundhouse" && normFileName.Contains("castro")) ||
                    (normTarget == "stonecirclehenge" && (normFileName.Contains("henge") || normFileName.Contains("stonecircle"))) ||
                    (normTarget == "cavearchgrotto" && (normFileName.Contains("cave") || normFileName.Contains("grotto"))) ||
                    (normTarget == "ruin" && (normFileName.Contains("ruin") || normFileName.Contains("dolmen") || normFileName.Contains("anta"))) ||
                    (normTarget == "farolcelta" && (normFileName.Contains("farol") || normFileName.Contains("lighthouse") || normFileName.Contains("beacon"))) ||
                    (normTarget == "altar" && normFileName.Contains("altar")) ||
                    (normTarget == "fento" && (normFileName.Contains("fento") || normFileName.Contains("fern"))) ||
                    (normTarget == "thornybush" && (normFileName.Contains("thorny") || normFileName.Contains("tojo") || normFileName.Contains("arbusto"))) ||
                    (normTarget == "berry" && (normFileName.Contains("berry") || normFileName.Contains("amora"))) ||
                    (normTarget == "mushroom" && normFileName.Contains("mushroom")) ||
                    (normTarget == "campfire" && (normFileName.Contains("campfire") || normFileName.Contains("fogueira"))) ||
                    (normTarget == "fallenlog" && (normFileName.Contains("log") || normFileName.Contains("tronco"))) ||
                    (normTarget == "pond" && normFileName.Contains("pond")) ||
                    (normTarget == "watercatcher" && normFileName.Contains("catcher")) ||
                    (normTarget == "workbench" && normFileName.Contains("workbench")) ||
                    (normTarget == "cauldron" && normFileName.Contains("cauldron")) ||
                    (normTarget == "farmland" && normFileName.Contains("farmland")) ||
                    (normTarget == "snaretrap" && normFileName.Contains("snare")) ||
                    (normTarget == "barricade" && normFileName.Contains("barricade")) ||
                    (normTarget == "herb" && normFileName.Contains("herb")) ||
                    (normTarget == "bandage" && normFileName.Contains("bandage")) ||
                    (normTarget == "crashcampshelter" && (normFileName.Contains("crashcamp") || normFileName.Contains("shelter"))) ||
                    (normTarget == "woodendockbridge" && (normFileName.Contains("dock") || normFileName.Contains("bridge"))) ||
                    (normTarget == "swamptotem" && normFileName.Contains("totem")) ||
                    (normTarget == "vulto" && normFileName.Contains("vulto")) ||
                    (normTarget == "stalker" && normFileName.Contains("stalker")) ||
                    (normTarget == "boss" && (normFileName.Contains("boss") || normFileName.Contains("anciao"))) ||
                    (normTarget == "player" && (normFileName.Contains("player") || normFileName.Contains("iago"))) ||
                    (normTarget == "wolf" && normFileName.Contains("wolf")) ||
                    (normTarget == "deer" && normFileName.Contains("deer")) ||
                    (normTarget == "bear" && normFileName.Contains("bear")) ||
                    (normTarget == "boar" && normFileName.Contains("boar")) ||
                    (normTarget == "rabbit" && normFileName.Contains("rabbit")) ||
                    (normTarget == "crow" && normFileName.Contains("crow")) ||
                    (normTarget == "eagle" && normFileName.Contains("eagle")))
                {
                    chosenPrefabName = target;
                    break;
                }
            }

            if (string.IsNullOrEmpty(chosenPrefabName))
                chosenPrefabName = targetPrefabNames[0];

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(importedModel);
            instance.name = chosenPrefabName;

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            ApplyShaders(instance, chosenPrefabName, type);
            if (type == ModelType.Fauna)
            {
                PrepareArticulatedFauna(instance, chosenPrefabName);
            }
            ApplyCollidersAndComponents(instance, chosenPrefabName, type);

            string destPath = $"{PREFABS_DIR}/{chosenPrefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, destPath);
            DestroyImmediate(instance);

            if (chosenPrefabName == "Mushroom_Prefab")
            {
                GameObject glowInstance = (GameObject)PrefabUtility.InstantiatePrefab(importedModel);
                glowInstance.name = "GlowMushroom_Prefab";
                PrefabUtility.UnpackPrefabInstance(glowInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ApplyShaders(glowInstance, "GlowMushroom_Prefab", type);
                ApplyCollidersAndComponents(glowInstance, "GlowMushroom_Prefab", type);
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

    private static Material ResolveMaterialBySlotName(string matSlotName, string prefabName, ModelType type, int index,
        Material barkMat, Material treeFoliage, Material fernMat, Material bushMat, Material toxoBloomMat, Material berryMat,
        Material rockMat, Material ruinsRockMat, Material mossMat, Material woodMat, Material carvedWoodMat, Material ironMat,
        Material leatherMat, Material fabricMat, Material darkClothMat, Material skinMat, Material soilMat, Material boneMat,
        Material glassMat, Material waterMat, Material herbLeafMat,
        Material runeGlowMat, Material beaconGlowMat, Material mushroomMat, Material glowMushroomMat, Material campfireGlowMat,
        Material vultoCloakMat, Material stalkerEyesMat, Material vultoEyesMat,
        Material wolfFur, Material deerPelt, Material bearFur, Material boarFur, Material rabbitFur, Material crowFeathers, Material eaglePlumage, Material animalEyes)
    {
        string slot = (matSlotName ?? "").ToLower();

        if (slot.Contains("vultocloak") || slot.Contains("cloak"))
            return vultoCloakMat ?? barkMat;
        if (slot.Contains("eyeglow"))
            return vultoEyesMat ?? campfireGlowMat;
        if (slot.Contains("eyes"))
        {
            if (prefabName.Contains("Stalker")) return stalkerEyesMat ?? animalEyes;
            if (prefabName.Contains("Vulto") || prefabName.Contains("Boss")) return vultoEyesMat ?? campfireGlowMat;
            return animalEyes ?? stalkerEyesMat;
        }
        if (slot.Contains("runeglow"))
            return runeGlowMat ?? beaconGlowMat;
        if (slot.Contains("leather"))
            return leatherMat ?? woodMat;
        if (slot.Contains("iron"))
            return ironMat;
        if (slot.Contains("glass"))
            return glassMat ?? waterMat;
        if (slot.Contains("emissive"))
            return campfireGlowMat;
        if (slot.Contains("darkcloth"))
            return darkClothMat ?? fabricMat;
        if (slot.Contains("fabric") || slot.Contains("string"))
            return fabricMat ?? woodMat;
        if (slot.Contains("skin"))
            return skinMat ?? leatherMat;
        if (slot.Contains("soil"))
            return soilMat ?? barkMat;
        if (slot.Contains("bone"))
            return boneMat ?? rockMat;
        if (slot.Contains("water"))
            return waterMat;
        if (slot.Contains("moss"))
            return mossMat;
        if (slot.Contains("stone"))
            return (prefabName.Contains("Castro") || prefabName.Contains("Henge") || prefabName.Contains("Ruin")) ? ruinsRockMat : rockMat;
        if (slot.Contains("herbleaf"))
            return herbLeafMat ?? bushMat ?? treeFoliage;
        if (slot.Contains("bloom"))
            return toxoBloomMat ?? campfireGlowMat;
        if (slot.Contains("bark"))
            return barkMat;
        if (slot.Contains("wood"))
            return carvedWoodMat ?? woodMat;
        if (slot.Contains("feathercrow"))
            return crowFeathers ?? leatherMat ?? barkMat;
        if (slot.Contains("feathereagle"))
            return eaglePlumage ?? deerPelt ?? barkMat;
        if (slot.Contains("dark") && type == ModelType.Fauna)
            return leatherMat ?? barkMat;
        if (slot.Contains("fur"))
        {
            if (prefabName.Contains("Deer")) return deerPelt ?? wolfFur;
            if (prefabName.Contains("Bear")) return bearFur ?? wolfFur;
            if (prefabName.Contains("Boar")) return boarFur ?? wolfFur;
            if (prefabName.Contains("Rabbit")) return rabbitFur ?? wolfFur;
            return wolfFur;
        }

        if (prefabName == "GlowMushroom_Prefab") return glowMushroomMat ?? mossMat;
        if (prefabName == "Mushroom_Prefab") return mushroomMat ?? mossMat;
        if (prefabName == "Fento_Prefab") return fernMat ?? treeFoliage;
        if (prefabName == "ThornyBush_Prefab")
        {
            if (index == 0) return barkMat;
            if (index == 1) return bushMat ?? treeFoliage;
            return toxoBloomMat ?? bushMat;
        }
        if (prefabName == "Berry_Prefab")
        {
            if (index == 0) return barkMat;
            if (index == 1) return bushMat ?? treeFoliage;
            return berryMat ?? mushroomMat;
        }
        if (type == ModelType.Tree) return (index == 0) ? barkMat : treeFoliage;
        if (prefabName == "FarolCelta_Prefab")
        {
            if (index == 0) return ruinsRockMat;
            if (index == 1) return ironMat;
            return beaconGlowMat;
        }
        if (prefabName == "RunicStone_Prefab" || prefabName == "Altar_Prefab")
            return (index == 0) ? rockMat : (runeGlowMat ?? rockMat);
        if (type == ModelType.Rock)
            return (prefabName.Contains("Ruin") || prefabName.Contains("Castro") || prefabName.Contains("Henge")) ? ruinsRockMat : rockMat;
        if (type == ModelType.Weapon)
            return (index == 0) ? (carvedWoodMat ?? woodMat) : ironMat;
        if (type == ModelType.Prop)
        {
            if (prefabName.Contains("Campfire")) return (index == 0) ? rockMat : woodMat;
            if (prefabName.Contains("Log")) return (index == 0) ? barkMat : mossMat;
            return (index == 0) ? (carvedWoodMat ?? woodMat) : mossMat;
        }

        return barkMat ?? campfireGlowMat;
    }

    private static void ApplyShaders(GameObject root, string prefabName, ModelType type)
    {
        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        Material pineNeedleMat = Resources.Load<Material>("Materials/Mat_PineNeedle");
        Material oakFoliageMat = Resources.Load<Material>("Materials/Mat_OakFoliage");
        Material foliageMat = Resources.Load<Material>("Materials/Mat_FoliageSway");
        Material fernMat = Resources.Load<Material>("Materials/Mat_Fern_Frond");
        Material bushMat = Resources.Load<Material>("Materials/Bush_Leaves");
        Material toxoBloomMat = Resources.Load<Material>("Materials/Mat_Toxo_Bloom");
        Material berryMat = Resources.Load<Material>("Materials/Mat_Berry_Fruit");
        Material rockMat = Resources.Load<Material>("Materials/Celtic_Stone");
        Material ruinsRockMat = Resources.Load<Material>("Materials/Mat_Ruins_Stone") ?? rockMat;
        Material mossMat = Resources.Load<Material>("Materials/Mat_DarkForest_Moss");
        Material woodMat = Resources.Load<Material>("Materials/Wood_Bark");
        Material carvedWoodMat = Resources.Load<Material>("Materials/Mat_Wood_Carved") ?? woodMat;
        Material ironMat = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        Material leatherMat = Resources.Load<Material>("Materials/Mat_Leather_Wrap") ?? woodMat;
        Material fabricMat = Resources.Load<Material>("Materials/Mat_Linen_Fabric") ?? woodMat;
        Material darkClothMat = Resources.Load<Material>("Materials/Mat_Player_DarkCloth") ?? fabricMat;
        Material skinMat = Resources.Load<Material>("Materials/Mat_Player_Skin") ?? leatherMat;
        Material soilMat = Resources.Load<Material>("Materials/Mat_Farmland_Soil") ?? barkMat;
        Material boneMat = Resources.Load<Material>("Materials/Mat_Bone_Horn") ?? rockMat;
        Material glassMat = Resources.Load<Material>("Materials/Mat_Lantern_Glass");
        Material waterMat = Resources.Load<Material>("Materials/Water_Surface") ?? Resources.Load<Material>("Materials/Mat_Swamp_Water");
        Material herbLeafMat = Resources.Load<Material>("Materials/Mat_Herb_Leaf") ?? foliageMat;

        Material runeGlowMat = Resources.Load<Material>("Materials/Mat_Emissive_RuneGlow");
        Material beaconGlowMat = Resources.Load<Material>("Materials/Mat_Emissive_BeaconGlow")
            ?? Resources.Load<Material>("Materials/Mat_Emissive_HolyWater");
        Material mushroomMat = Resources.Load<Material>("Materials/Mat_Meadow_Wildflower");
        Material glowMushroomMat = Resources.Load<Material>("Materials/Mat_Emissive_MushroomGlow");
        Material campfireGlowMat = Resources.Load<Material>("Materials/Mat_Emissive_Campfire");

        Material vultoCloakMat = Resources.Load<Material>("Materials/Mat_Vulto_Cloak");
        Material stalkerEyesMat = Resources.Load<Material>("Materials/Mat_Stalker_Eyes");
        Material vultoEyesMat = Resources.Load<Material>("Materials/Mat_Emissive_VultoEyes");

        Material wolfFur = Resources.Load<Material>("Materials/Wolf_Fur");
        Material deerPelt = Resources.Load<Material>("Materials/Deer_Pelt");
        Material bearFur = Resources.Load<Material>("Materials/Bear_Fur");
        Material boarFur = Resources.Load<Material>("Materials/Boar_Bristles");
        Material rabbitFur = Resources.Load<Material>("Materials/Rabbit_Fur");
        Material crowFeathers = Resources.Load<Material>("Materials/Crow_Feathers");
        Material eaglePlumage = Resources.Load<Material>("Materials/Eagle_Plumage");
        Material animalEyes = Resources.Load<Material>("Materials/Animal_GlowingEyes");

        Material treeFoliage = prefabName.Contains("Pine")
            ? (pineNeedleMat ?? foliageMat)
            : (oakFoliageMat ?? foliageMat);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            int submeshCount = 1;
            if (mf != null && mf.sharedMesh != null)
                submeshCount = Mathf.Max(1, mf.sharedMesh.subMeshCount);
            else if (r.sharedMaterials != null && r.sharedMaterials.Length > 0)
                submeshCount = r.sharedMaterials.Length;

            Material[] existingMats = r.sharedMaterials;
            Material[] mats = new Material[submeshCount];
            for (int i = 0; i < submeshCount; i++)
            {
                string slotName = (existingMats != null && i < existingMats.Length && existingMats[i] != null)
                    ? existingMats[i].name
                    : "";

                mats[i] = ResolveMaterialBySlotName(
                    slotName, prefabName, type, i,
                    barkMat, treeFoliage, fernMat, bushMat, toxoBloomMat, berryMat,
                    rockMat, ruinsRockMat, mossMat, woodMat, carvedWoodMat, ironMat,
                    leatherMat, fabricMat, darkClothMat, skinMat, soilMat, boneMat,
                    glassMat, waterMat, herbLeafMat,
                    runeGlowMat, beaconGlowMat, mushroomMat, glowMushroomMat, campfireGlowMat,
                    vultoCloakMat, stalkerEyesMat, vultoEyesMat,
                    wolfFur, deerPelt, bearFur, boarFur, rabbitFur, crowFeathers, eaglePlumage, animalEyes
                );
            }
            r.sharedMaterials = mats;
        }
    }

    private static Mesh CreateRepivotedMeshAsset(Mesh sourceMesh, Vector3 pivotOffset, string assetName)
    {
        Mesh clone = Instantiate(sourceMesh);
        clone.name = assetName;
        Vector3[] verts = clone.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] -= pivotOffset;
        }
        clone.vertices = verts;
        clone.RecalculateBounds();
        clone.RecalculateTangents();

        string assetPath = $"{FAUNA_MESHES_DIR}/{assetName}.asset";
        if (File.Exists(assetPath))
            AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(clone, assetPath);
        return clone;
    }

    private static void PrepareArticulatedFauna(GameObject root, string prefabName)
    {
        Transform headT = null;
        Vector3 headPivot = Vector3.zero;
        Transform earL = null, earR = null;
        Vector3 earLPivot = Vector3.zero, earRPivot = Vector3.zero;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t == root.transform) continue;
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            string n = t.name.ToLower();
            if (n.Contains("body") || n.Contains("torso") || n.Contains("corpo"))
            {
                t.localPosition = Vector3.zero;
                continue;
            }

            Bounds b = mf.sharedMesh.bounds;
            Vector3 pivot = Vector3.zero;

            if (n.Contains("leg"))
            {
                pivot = new Vector3(b.center.x, Mathf.Max(b.min.y, b.max.y - 0.04f), b.center.z);
            }
            else if (n.Contains("tail"))
            {
                pivot = new Vector3(
                    b.center.x,
                    b.max.y - Mathf.Min(0.04f, b.size.y * 0.25f),
                    b.max.z - Mathf.Min(0.04f, b.size.z * 0.20f));
            }
            else if (n.Contains("wing"))
            {
                float innerOffset = Mathf.Min(0.03f, b.size.x * 0.08f);
                pivot = new Vector3(
                    n.Contains("_l") ? (b.max.x - innerOffset) : (b.min.x + innerOffset),
                    b.center.y,
                    b.center.z);
            }
            else if (n.Contains("head"))
            {
                pivot = new Vector3(b.center.x, b.min.y + Mathf.Min(0.08f, b.size.y * 0.2f), b.min.z + Mathf.Min(0.10f, b.size.z * 0.25f));
                headT = t;
                headPivot = pivot;
            }
            else if (n.Contains("ear"))
            {
                pivot = new Vector3(b.center.x, b.min.y + 0.02f, b.center.z);
                if (n.Contains("_l")) { earL = t; earLPivot = pivot; }
                else { earR = t; earRPivot = pivot; }
            }
            else
            {
                continue;
            }

            string cleanPref = prefabName.Replace("_Prefab", "");
            mf.sharedMesh = CreateRepivotedMeshAsset(mf.sharedMesh, pivot, $"{cleanPref}_{t.name}");
            t.localPosition = pivot;
        }

        if (headT != null)
        {
            if (earL != null)
            {
                earL.SetParent(headT, false);
                earL.localPosition = earLPivot - headPivot;
            }
            if (earR != null)
            {
                earR.SetParent(headT, false);
                earR.localPosition = earRPivot - headPivot;
            }
        }
    }

    private static void ApplyCollidersAndComponents(GameObject root, string prefabName, ModelType type)
    {
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
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
        else if (prefabName == "FarolCelta_Prefab")
        {
            CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0, 9.5f, 0);
            cc.radius = 2.2f;
            cc.height = 19.0f;

            FarolCeltaSystem system = root.GetComponent<FarolCeltaSystem>();
            if (system == null) system = root.AddComponent<FarolCeltaSystem>();

            Transform existingLight = root.transform.Find("SweepingLight");
            GameObject lightObj = existingLight != null ? existingLight.gameObject : new GameObject("SweepingLight");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 17.6f, 0f);
            Light l = lightObj.GetComponent<Light>();
            if (l == null) l = lightObj.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(0.85f, 0.95f, 1.0f);
            l.range = 65f;
            l.spotAngle = 45f;
            l.intensity = 6.0f;
            l.enabled = false;
            system.farolLight = l;
        }
        else if (prefabName == "Altar_Prefab")
        {
            BoxCollider bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 1.15f, 0);
            bc.size = new Vector3(2.8f, 2.3f, 2.8f);

            Light aura = root.GetComponent<Light>();
            if (aura == null) aura = root.AddComponent<Light>();
            aura.type = LightType.Point;
            aura.color = new Color(0.18f, 0.72f, 0.95f);
            aura.intensity = 2.4f;
            aura.range = 9.5f;
        }
        else if (prefabName == "Ruin_Prefab" || prefabName == "CastroRoundhouse_Prefab" ||
                 prefabName == "StoneCircleHenge_Prefab" || prefabName == "CaveArchGrotto_Prefab")
        {
            MeshFilter mf = root.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }
            if (prefabName == "StoneCircleHenge_Prefab" || prefabName == "CaveArchGrotto_Prefab")
            {
                Light runeLight = root.GetComponent<Light>();
                if (runeLight == null) runeLight = root.AddComponent<Light>();
                runeLight.type = LightType.Point;
                runeLight.color = new Color(0.20f, 0.78f, 0.96f);
                runeLight.intensity = 1.8f;
                runeLight.range = 10f;
            }
        }
        else if (type == ModelType.Rock)
        {
            MeshFilter mf = root.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
        }
        else if (prefabName == "GlowMushroom_Prefab")
        {
            Light glow = root.GetComponent<Light>();
            if (glow == null) glow = root.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.15f, 0.95f, 0.70f);
            glow.intensity = 1.4f;
            glow.range = 4.5f;
        }
        else if (type == ModelType.Fauna)
        {
            if (prefabName == "Crow_Prefab" || prefabName == "Eagle_Prefab")
            {
                return;
            }

            CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
            AnimalProceduralAnimator anim = root.GetComponent<AnimalProceduralAnimator>();
            if (anim == null) anim = root.AddComponent<AnimalProceduralAnimator>();

            if (prefabName == "Wolf_Prefab")
            {
                cc.radius = 0.35f; cc.height = 1.3f; cc.center = new Vector3(0, 0.65f, 0);
                anim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.QuadrupedStalker;
                anim.strideSpeedMultiplier = 3.8f; anim.legSwingAngle = 28f; anim.bodyBobAmount = 0.05f;
            }
            else if (prefabName == "Deer_Prefab")
            {
                cc.radius = 0.40f; cc.height = 1.7f; cc.center = new Vector3(0, 1.0f, 0);
                anim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.QuadrupedUngulate;
                anim.strideSpeedMultiplier = 3.2f; anim.legSwingAngle = 24f; anim.bodyBobAmount = 0.06f;
            }
            else if (prefabName == "Bear_Prefab")
            {
                cc.radius = 0.62f; cc.height = 1.9f; cc.center = new Vector3(0, 0.9f, 0);
                anim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.QuadrupedHeavy;
                anim.strideSpeedMultiplier = 2.6f; anim.legSwingAngle = 20f; anim.bodyBobAmount = 0.06f;
            }
            else if (prefabName == "Boar_Prefab")
            {
                cc.radius = 0.45f; cc.height = 1.4f; cc.center = new Vector3(0, 0.5f, 0);
                anim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.QuadrupedUngulate;
                anim.strideSpeedMultiplier = 3.6f; anim.legSwingAngle = 24f; anim.bodyBobAmount = 0.04f;
            }
            else if (prefabName == "Rabbit_Prefab")
            {
                cc.radius = 0.18f; cc.height = 0.45f; cc.center = new Vector3(0, 0.2f, 0);
                anim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.Hopper;
                anim.strideSpeedMultiplier = 5.0f; anim.bodyBobAmount = 0.15f;
            }
        }
        else if (type == ModelType.Entity)
        {
            if (prefabName == "Boss_Prefab")
            {
                CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
                cc.center = new Vector3(0, 3.0f, 0); cc.radius = 1.1f; cc.height = 6.0f;
                Light l = root.GetComponent<Light>();
                if (l == null) l = root.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(0.95f, 0.15f, 0.10f);
                l.range = 10f; l.intensity = 2.5f;
            }
            else if (prefabName == "Stalker_Prefab")
            {
                CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
                cc.center = new Vector3(0, 1.6f, 0); cc.radius = 0.45f; cc.height = 3.2f;
            }
        }
        else if (prefabName == "Torch_Prefab")
        {
            GameObject firePoint = new GameObject("Flame_EmberPoint");
            firePoint.transform.SetParent(root.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0.44f, 0f);
            Light l = firePoint.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.65f, 0.25f);
            l.range = 9.5f;
            l.intensity = 2.4f;
            l.shadows = LightShadows.Soft;
        }
        else if (prefabName == "Lantern_Prefab")
        {
            Light l = root.GetComponent<Light>();
            if (l == null) l = root.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.82f, 0.45f);
            l.range = 16f;
            l.intensity = 3.2f;
            l.shadows = LightShadows.Soft;
        }
        else if (prefabName == "Axe_Prefab")
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.28f, 0.05f);
            box.size = new Vector3(0.08f, 0.65f, 0.25f);
            box.isTrigger = true;
        }
        else if (prefabName == "Pond_Prefab")
        {
            Transform waterSurf = root.transform.Find("Water_Surface");
            if (waterSurf != null)
            {
                AnimatedWater aw = waterSurf.GetComponent<AnimatedWater>();
                if (aw == null) waterSurf.gameObject.AddComponent<AnimatedWater>();
            }
        }
        else if (type == ModelType.Prop)
        {
            if (prefabName == "WoodenDockBridge_Prefab")
            {
                // Walkable plank deck + smooth low side curbs
                BoxCollider deck = root.AddComponent<BoxCollider>();
                deck.center = new Vector3(0f, 0.36f, 0f);
                deck.size = new Vector3(3.2f, 0.22f, 8.4f);

                GameObject railL = new GameObject("Rail_L");
                railL.transform.SetParent(root.transform, false);
                BoxCollider rlc = railL.AddComponent<BoxCollider>();
                rlc.center = new Vector3(-1.42f, 0.95f, 0f);
                rlc.size = new Vector3(0.18f, 0.95f, 7.6f);

                GameObject railR = new GameObject("Rail_R");
                railR.transform.SetParent(root.transform, false);
                BoxCollider rrc = railR.AddComponent<BoxCollider>();
                rrc.center = new Vector3(1.42f, 0.95f, 0f);
                rrc.size = new Vector3(0.18f, 0.95f, 7.6f);
            }
            else if (prefabName == "CrashCampShelter_Prefab")
            {
                BoxCollider backWall = root.AddComponent<BoxCollider>();
                backWall.center = new Vector3(0f, 1.0f, -0.75f);
                backWall.size = new Vector3(4.1f, 2.0f, 0.8f);
            }
            else if (prefabName == "SwampTotem_Prefab")
            {
                CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
                cc.center = new Vector3(0f, 1.6f, 0f);
                cc.radius = 0.35f;
                cc.height = 3.2f;
                Light l = root.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.25f, 0.92f, 0.55f);
                l.intensity = 1.4f;
                l.range = 7.5f;
            }
            else if (prefabName.Contains("Log"))
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
            else if (prefabName.Contains("Barricade"))
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 0.6f, 0);
                bc.size = new Vector3(1.9f, 1.2f, 0.9f);
            }
            else if (prefabName.Contains("Workbench"))
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 0.45f, 0);
                bc.size = new Vector3(1.4f, 0.9f, 0.65f);
            }
            else if (prefabName.Contains("WaterCatcher"))
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 0.55f, 0);
                bc.size = new Vector3(1.1f, 1.1f, 1.1f);
            }
            else if (prefabName.Contains("Cauldron"))
            {
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, 0.35f, 0);
                bc.size = new Vector3(0.85f, 0.7f, 0.85f);
            }
        }
    }
}
