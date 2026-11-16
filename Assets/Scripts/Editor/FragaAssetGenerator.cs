using UnityEngine;
using UnityEditor;

public class FragaAssetGenerator : MonoBehaviour
{
    [MenuItem("Fraga Native/3. Gerar Modelos 3D e Prefabs")]
    public static void GenerateAssets()
    {
        string prefabsDir = "Assets/Resources/Prefabs";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        }

        // Garante geracao previa de materiais PBR e texturas procedurais
        ProceduralMaterialGenerator.GenerateMaterials();

        GenerateTreePrefab(prefabsDir);
        GenerateRockPrefab(prefabsDir);
        GeneratePlayerPrefab(prefabsDir);
        GenerateVultoPrefab(prefabsDir);
        GenerateHerbPrefab(prefabsDir);
        GenerateMushroomPrefab(prefabsDir);
        GenerateBerryPrefab(prefabsDir);
        
        GenerateWolfPrefab(prefabsDir);
        GenerateDeerPrefab(prefabsDir);
        GenerateBearPrefab(prefabsDir);
        GenerateBoarPrefab(prefabsDir);
        GenerateRuinPrefab(prefabsDir);
        
        GenerateBossPrefab(prefabsDir);
        GenerateAltarPrefab(prefabsDir);
        
        GenerateAxePrefab(prefabsDir);
        GenerateTorchPrefab(prefabsDir);
        GeneratePickaxePrefab(prefabsDir);
        GenerateLanternPrefab(prefabsDir);
        GenerateBandagePrefab(prefabsDir);
        
        GenerateBowPrefab(prefabsDir);
        GenerateArrowPrefab(prefabsDir);
        
        GenerateFishingRodPrefab(prefabsDir);
        GenerateWaterCatcherPrefab(prefabsDir);
        GenerateWorkbenchPrefab(prefabsDir);
        GeneratePondPrefab(prefabsDir);
        
        GenerateHoePrefab(prefabsDir);
        GenerateCauldronPrefab(prefabsDir);
        GenerateRabbitPrefab(prefabsDir);
        GenerateFarmlandPrefab(prefabsDir);
        GenerateSnareTrapPrefab(prefabsDir);
        
        // Loop 8: Environmental Models
        GenerateRunicStonePrefab(prefabsDir);
        GenerateFallenLogPrefab(prefabsDir);
        GenerateDroppedWoodPrefab(prefabsDir);
        GenerateThornyBushPrefab(prefabsDir);
        GenerateGlowMushroomPrefab(prefabsDir);
        GenerateCelticCrossPrefab(prefabsDir);
        GenerateAncientGravePrefab(prefabsDir);

        // Sub-bosque e vegetacao Galega
        GenerateFentoPrefab(prefabsDir);

        // Loop 13: Novos Modelos
        GenerateDeadTreePrefab(prefabsDir);
        GeneratePineTreePrefab(prefabsDir);
        GenerateStalkerPrefab(prefabsDir);
        
        GenerateFarolCeltaPrefab(prefabsDir);

        FragaModelBridge.SyncExternalModels();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("[FragaAssetGenerator] Modelos 3D Gerados com Sucesso!");
    }

    private static void GenerateTreePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Trees/Oak_Tree.obj")) return;
        GameObject treeObj = new GameObject("Tree_Prefab");

        // 1. Raízes superficiais que se cravam no solo de turfa da Galícia
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f + Random.Range(-12f, 12f);
            Quaternion rot = Quaternion.Euler(32f, angle, 0f);
            Vector3 offset = Quaternion.Euler(0, angle, 0) * new Vector3(0, 0, 0.42f);

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Root_" + i;
            root.transform.SetParent(treeObj.transform);
            root.transform.localPosition = new Vector3(offset.x, 0.12f, offset.z);
            root.transform.localScale = new Vector3(0.24f, 0.42f, 0.22f);
            root.transform.localRotation = rot;
            SetColor(root, new Color(0.22f, 0.15f, 0.08f), "Mat_DarkForest_Bark");
        }

        // 2. Base do Tronco robusta (Carballo ancestral)
        GameObject trunkBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunkBase.name = "Trunk_Base";
        trunkBase.transform.SetParent(treeObj.transform);
        trunkBase.transform.localPosition = new Vector3(0, 0.8f, 0);
        trunkBase.transform.localScale = new Vector3(0.68f, 0.8f, 0.65f);
        SetColor(trunkBase, new Color(0.24f, 0.16f, 0.08f), "Mat_DarkForest_Bark");

        // 3. Tronco Médio com curvatura orgânica
        GameObject trunkMid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunkMid.name = "Trunk_Mid";
        trunkMid.transform.SetParent(treeObj.transform);
        trunkMid.transform.localPosition = new Vector3(0.04f, 2.1f, 0.03f);
        trunkMid.transform.localScale = new Vector3(0.52f, 0.75f, 0.5f);
        trunkMid.transform.localRotation = Quaternion.Euler(4f, 22f, -3f);
        SetColor(trunkMid, new Color(0.24f, 0.16f, 0.08f), "Mat_DarkForest_Bark");

        // 4. Tronco Superior
        GameObject trunkTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunkTop.name = "Trunk_Top";
        trunkTop.transform.SetParent(treeObj.transform);
        trunkTop.transform.localPosition = new Vector3(0.08f, 3.2f, 0.05f);
        trunkTop.transform.localScale = new Vector3(0.4f, 0.65f, 0.38f);
        trunkTop.transform.localRotation = Quaternion.Euler(-5f, -18f, 4f);
        SetColor(trunkTop, new Color(0.24f, 0.16f, 0.08f), "Mat_DarkForest_Bark");

        // 5. Galhos Mestres (4 Boughs robustos espalhados)
        float[] boughYaws = { 25f, 115f, 205f, 295f };
        float[] boughPitches = { 38f, 44f, 36f, 42f };
        float[] boughLengths = { 1.45f, 1.35f, 1.55f, 1.4f };

        for (int b = 0; b < 4; b++)
        {
            GameObject bough = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bough.name = "Bough_" + b;
            bough.transform.SetParent(treeObj.transform);
            Quaternion boughRot = Quaternion.Euler(boughPitches[b], boughYaws[b], 0);
            Vector3 dirBough = boughRot * Vector3.up;
            bough.transform.localPosition = new Vector3(0.08f, 2.8f, 0.05f) + dirBough * (boughLengths[b] * 0.5f);
            bough.transform.localScale = new Vector3(0.18f, boughLengths[b] * 0.5f, 0.18f);
            bough.transform.localRotation = boughRot;
            SetColor(bough, new Color(0.22f, 0.15f, 0.08f), "Mat_DarkForest_Bark");

            // Copa de folhas densa na extremidade
            Vector3 tipPos = new Vector3(0.08f, 2.8f, 0.05f) + dirBough * boughLengths[b];
            for (int f = 0; f < 2; f++)
            {
                GameObject leafCluster = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leafCluster.name = "LeafCluster_" + b + "_" + f;
                leafCluster.transform.SetParent(treeObj.transform);
                Vector3 jitter = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.15f, 0.25f), Random.Range(-0.25f, 0.25f));
                leafCluster.transform.localPosition = tipPos + jitter;
                float foliageScale = Random.Range(1.4f, 1.8f);
                leafCluster.transform.localScale = new Vector3(foliageScale * 1.2f, foliageScale * 0.8f, foliageScale * 1.15f);
                leafCluster.transform.localRotation = Quaternion.Euler(Random.Range(-20, 20), Random.Range(0, 360), Random.Range(-20, 20));
                SetColor(leafCluster, new Color(0.14f, 0.36f, 0.13f), "Mat_FoliageSway");
            }
        }

        // 6. Copa Central Superior Densa
        for (int c = 0; c < 3; c++)
        {
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "CrownCluster_" + c;
            crown.transform.SetParent(treeObj.transform);
            crown.transform.localPosition = new Vector3(Random.Range(-0.25f, 0.25f), 3.9f + (c * 0.45f), Random.Range(-0.25f, 0.25f));
            float crownScale = 2.1f - (c * 0.3f);
            crown.transform.localScale = new Vector3(crownScale * 1.2f, crownScale * 0.8f, crownScale * 1.2f);
            crown.transform.localRotation = Quaternion.Euler(Random.Range(-15, 15), Random.Range(0, 360), Random.Range(-15, 15));
            SetColor(crown, new Color(0.12f, 0.34f, 0.12f), "Mat_FoliageSway");
        }

        ApplyOrganicNoiseToMesh(treeObj, 0.18f, 2.0f);

        CapsuleCollider coll = treeObj.AddComponent<CapsuleCollider>();
        coll.radius = 0.45f;
        coll.height = 3.6f;
        coll.center = new Vector3(0, 1.8f, 0);

        PrefabUtility.SaveAsPrefabAsset(treeObj, dir + "/Tree_Prefab.prefab");
        DestroyImmediate(treeObj);
    }

    private static void GenerateDeadTreePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Trees/Dead_Tree.obj")) return;
        GameObject treeObj = new GameObject("DeadTree_Prefab");

        // Tronco retorcido e partido
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(treeObj.transform);
        trunk.transform.localPosition = new Vector3(0, 1.6f, 0);
        trunk.transform.localScale = new Vector3(0.32f, 1.6f, 0.32f);
        trunk.transform.localRotation = Quaternion.Euler(0, 15, 6);
        SetColor(trunk, new Color(0.18f, 0.14f, 0.1f), "Mat_DarkForest_Bark");

        // 5 Galhos secos e retorcidos
        float[] branchAnglesY = { 20f, 95f, 175f, 250f, 320f };
        float[] branchAnglesZ = { 42f, -38f, 48f, -44f, 35f };
        for (int i = 0; i < 5; i++)
        {
            GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            branch.name = "DeadBranch_" + i;
            branch.transform.SetParent(treeObj.transform);
            branch.transform.localScale = new Vector3(0.09f, 0.9f, 0.09f);
            branch.transform.localPosition = new Vector3(0, 1.5f + (i * 0.45f), 0);
            branch.transform.localRotation = Quaternion.Euler(Random.Range(-15, 15), branchAnglesY[i], branchAnglesZ[i]);
            SetColor(branch, new Color(0.16f, 0.12f, 0.08f), "Mat_DarkForest_Bark");
        }

        ApplyOrganicNoiseToMesh(treeObj, 0.22f, 2.5f);

        CapsuleCollider coll = treeObj.AddComponent<CapsuleCollider>();
        coll.radius = 0.35f;
        coll.height = 3.6f;
        coll.center = new Vector3(0, 1.8f, 0);

        PrefabUtility.SaveAsPrefabAsset(treeObj, dir + "/DeadTree_Prefab.prefab");
        DestroyImmediate(treeObj);
    }

    private static void GeneratePineTreePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Trees/Pine_Tree.obj")) return;
        GameObject treeObj = new GameObject("PineTree_Prefab");

        // Tronco esguio e alto
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(treeObj.transform);
        trunk.transform.localPosition = new Vector3(0, 2.6f, 0);
        trunk.transform.localScale = new Vector3(0.35f, 2.6f, 0.35f);
        SetColor(trunk, new Color(0.25f, 0.16f, 0.08f), "Mat_DarkForest_Bark");

        // 5 Camadas (Whorls) de galhos e agulhas
        int tiers = 5;
        for (int t = 0; t < tiers; t++)
        {
            float height = 1.8f + (t * 0.75f);
            float radius = 1.6f - (t * 0.26f);
            float needleScale = 1.8f - (t * 0.28f);

            // Galhos radiais
            for (int b = 0; b < 4; b++)
            {
                float angle = b * 90f + (t * 35f);
                GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                branch.name = "Branch_" + t + "_" + b;
                branch.transform.SetParent(treeObj.transform);
                Quaternion rot = Quaternion.Euler(75f, angle, 0);
                branch.transform.localRotation = rot;
                Vector3 dirVec = rot * Vector3.up;
                branch.transform.localPosition = new Vector3(0, height, 0) + dirVec * (radius * 0.4f);
                branch.transform.localScale = new Vector3(0.08f, radius * 0.45f, 0.08f);
                SetColor(branch, new Color(0.22f, 0.14f, 0.07f), "Mat_DarkForest_Bark");
            }

            // Agulhas da camada
            GameObject needles = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            needles.name = "Needles_" + t;
            needles.transform.SetParent(treeObj.transform);
            needles.transform.localPosition = new Vector3(0, height + 0.2f, 0);
            needles.transform.localScale = new Vector3(needleScale, 0.35f, needleScale);
            SetColor(needles, new Color(0.08f, 0.26f, 0.11f), "Mat_FoliageSway");
        }

        // Topo cônico
        GameObject topCrown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        topCrown.name = "Pine_TopCrown";
        topCrown.transform.SetParent(treeObj.transform);
        topCrown.transform.localPosition = new Vector3(0, 5.5f, 0);
        topCrown.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
        SetColor(topCrown, new Color(0.09f, 0.28f, 0.12f), "Mat_FoliageSway");

        ApplyOrganicNoiseToMesh(treeObj, 0.12f, 2.5f);

        CapsuleCollider coll = treeObj.AddComponent<CapsuleCollider>();
        coll.radius = 0.35f;
        coll.height = 5.6f;
        coll.center = new Vector3(0, 2.8f, 0);

        PrefabUtility.SaveAsPrefabAsset(treeObj, dir + "/PineTree_Prefab.prefab");
        DestroyImmediate(treeObj);
    }

        private static void GenerateRockPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Rocks/Granite_Rock.obj")) return;
        GameObject rockObj = new GameObject("Rock_Prefab");

        // Bloco de granito central principal
        GameObject mainStone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainStone.name = "Main_Granite";
        mainStone.transform.SetParent(rockObj.transform);
        mainStone.transform.localPosition = new Vector3(0, 0.6f, 0);
        mainStone.transform.localScale = new Vector3(1.35f, 1.15f, 1.25f);
        mainStone.transform.localRotation = Quaternion.Euler(7f, 25f, -5f);
        SetColor(mainStone, new Color(0.42f, 0.42f, 0.45f), "Celtic_Stone", "celtic_stone");

        // Bloco flanqueador esquerdo
        GameObject stoneL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stoneL.name = "Flank_Left";
        stoneL.transform.SetParent(rockObj.transform);
        stoneL.transform.localPosition = new Vector3(-0.65f, 0.4f, 0.2f);
        stoneL.transform.localScale = new Vector3(0.95f, 0.8f, 0.85f);
        stoneL.transform.localRotation = Quaternion.Euler(-12f, 65f, 15f);
        SetColor(stoneL, new Color(0.40f, 0.40f, 0.43f), "Celtic_Stone", "celtic_stone");

        // Bloco flanqueador direito
        GameObject stoneR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stoneR.name = "Flank_Right";
        stoneR.transform.SetParent(rockObj.transform);
        stoneR.transform.localPosition = new Vector3(0.7f, 0.45f, -0.15f);
        stoneR.transform.localScale = new Vector3(1.05f, 0.85f, 0.95f);
        stoneR.transform.localRotation = Quaternion.Euler(15f, -40f, -10f);
        SetColor(stoneR, new Color(0.43f, 0.43f, 0.46f), "Celtic_Stone", "celtic_stone");

        // Laje rasa de base com musgo denso
        GameObject stoneBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stoneBase.name = "Base_Slab";
        stoneBase.transform.SetParent(rockObj.transform);
        stoneBase.transform.localPosition = new Vector3(0.1f, 0.18f, -0.4f);
        stoneBase.transform.localScale = new Vector3(1.1f, 0.35f, 0.95f);
        stoneBase.transform.localRotation = Quaternion.Euler(-5f, 10f, 3f);
        SetColor(stoneBase, new Color(0.38f, 0.38f, 0.4f), "Celtic_Stone", "celtic_stone");

        ApplyOrganicNoiseToMesh(rockObj, 0.28f, 1.8f);

        BoxCollider coll = rockObj.AddComponent<BoxCollider>();
        coll.size = new Vector3(2.3f, 1.25f, 2.1f);
        coll.center = new Vector3(0.05f, 0.6f, 0);

        PrefabUtility.SaveAsPrefabAsset(rockObj, dir + "/Rock_Prefab.prefab");
        DestroyImmediate(rockObj);
    }

    private static void GeneratePlayerPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Entities/Player_Character.obj")) return;
        GameObject playerObj = new GameObject("Player_Prefab");
        
        // Torso
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(playerObj.transform);
        body.transform.localPosition = new Vector3(0, 1.1f, 0);
        body.transform.localScale = new Vector3(0.7f, 0.8f, 0.4f);
        SetColor(body, new Color(0.4f, 0.3f, 0.2f)); // Casaco de cabedal
        
        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.transform.SetParent(playerObj.transform);
        head.transform.localPosition = new Vector3(0, 1.7f, 0);
        head.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        SetColor(head, new Color(0.9f, 0.7f, 0.6f)); // Pele
        
        // Hat
        GameObject hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hat.transform.SetParent(playerObj.transform);
        hat.transform.localPosition = new Vector3(0, 1.95f, 0);
        hat.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
        SetColor(hat, new Color(0.1f, 0.1f, 0.1f));
        
        GameObject hatTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hatTop.transform.SetParent(playerObj.transform);
        hatTop.transform.localPosition = new Vector3(0, 2.15f, 0);
        hatTop.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
        SetColor(hatTop, new Color(0.1f, 0.1f, 0.1f));

        // Backpack
        GameObject backpack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backpack.transform.SetParent(playerObj.transform);
        backpack.transform.localPosition = new Vector3(0, 1.1f, -0.3f);
        backpack.transform.localScale = new Vector3(0.5f, 0.6f, 0.25f);
        SetColor(backpack, new Color(0.25f, 0.15f, 0.08f));
        
        // Arms
        GameObject armL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armL.transform.SetParent(playerObj.transform);
        armL.transform.localPosition = new Vector3(-0.45f, 1.1f, 0);
        armL.transform.localScale = new Vector3(0.2f, 0.7f, 0.2f);
        SetColor(armL, new Color(0.4f, 0.3f, 0.2f));

        GameObject armR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armR.transform.SetParent(playerObj.transform);
        armR.transform.localPosition = new Vector3(0.45f, 1.1f, 0);
        armR.transform.localScale = new Vector3(0.2f, 0.7f, 0.2f);
        SetColor(armR, new Color(0.4f, 0.3f, 0.2f));
        
        // Legs
        GameObject legL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        legL.transform.SetParent(playerObj.transform);
        legL.transform.localPosition = new Vector3(-0.2f, 0.35f, 0);
        legL.transform.localScale = new Vector3(0.25f, 0.7f, 0.25f);
        SetColor(legL, new Color(0.15f, 0.15f, 0.15f)); // Calcas escuras

        GameObject legR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        legR.transform.SetParent(playerObj.transform);
        legR.transform.localPosition = new Vector3(0.2f, 0.35f, 0);
        legR.transform.localScale = new Vector3(0.25f, 0.7f, 0.25f);
        SetColor(legR, new Color(0.15f, 0.15f, 0.15f));

        PrefabUtility.SaveAsPrefabAsset(playerObj, dir + "/Player_Prefab.prefab");
        DestroyImmediate(playerObj);
    }

    private static void GenerateVultoPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Entities/Vulto_Entity.obj")) return;
        GameObject vultoObj = new GameObject("Vulto_Prefab");
        
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.transform.SetParent(vultoObj.transform);
        body.transform.localPosition = new Vector3(0, 1.5f, 0);
        body.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
        SetColor(body, new Color(0.05f, 0.05f, 0.05f, 0.85f));

        GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeL.transform.SetParent(vultoObj.transform);
        eyeL.transform.localPosition = new Vector3(-0.15f, 2.2f, 0.3f);
        eyeL.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        SetColor(eyeL, Color.red);

        GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeR.transform.SetParent(vultoObj.transform);
        eyeR.transform.localPosition = new Vector3(0.15f, 2.2f, 0.3f);
        eyeR.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        SetColor(eyeR, Color.red);

        PrefabUtility.SaveAsPrefabAsset(vultoObj, dir + "/Vulto_Prefab.prefab");
        DestroyImmediate(vultoObj);
    }

    private static void GenerateStalkerPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Entities/Stalker_Entity.obj")) return;
        GameObject vultoObj = new GameObject("Stalker_Prefab");
        
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(vultoObj.transform);
        body.transform.localPosition = new Vector3(0, 1.5f, 0);
        body.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);
        SetColor(body, new Color(0.05f, 0.05f, 0.05f, 0.8f));

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(vultoObj.transform);
        head.transform.localPosition = new Vector3(0, 3.2f, 0);
        head.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
        SetColor(head, new Color(0.01f, 0.01f, 0.01f, 0.9f));

        // Olhos Brancos Brilhantes
        GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeL.transform.SetParent(head.transform);
        eyeL.transform.localPosition = new Vector3(-0.2f, 0, 0.4f);
        eyeL.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        SetColor(eyeL, Color.white);

        GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeR.transform.SetParent(head.transform);
        eyeR.transform.localPosition = new Vector3(0.2f, 0, 0.4f);
        eyeR.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        SetColor(eyeR, Color.white);

        PrefabUtility.SaveAsPrefabAsset(vultoObj, dir + "/Stalker_Prefab.prefab");
        DestroyImmediate(vultoObj);
    }

    private static void GenerateHerbPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Herb_Cluster.obj")) return;
        GameObject herbObj = new GameObject("Herb_Prefab");

        // Talo central flexível
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(herbObj.transform);
        stem.transform.localPosition = new Vector3(0, 0.22f, 0);
        stem.transform.localScale = new Vector3(0.025f, 0.22f, 0.025f);
        stem.transform.localRotation = Quaternion.Euler(5f, 0, 3f);
        SetColor(stem, new Color(0.2f, 0.5f, 0.15f), "Mat_FoliageSway");

        // 3 Pares de folhas opostas (Hierba de San Juan)
        for (int i = 0; i < 3; i++)
        {
            float yPos = 0.12f + (i * 0.12f);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaf.name = "Leaf_" + i + "_" + (side > 0 ? "R" : "L");
                leaf.transform.SetParent(herbObj.transform);
                leaf.transform.localPosition = new Vector3(side * 0.08f, yPos, 0);
                leaf.transform.localScale = new Vector3(0.12f, 0.02f, 0.06f);
                leaf.transform.localRotation = Quaternion.Euler(15f, side * 30f, side * -20f);
                SetColor(leaf, new Color(0.25f, 0.65f, 0.2f), "Mat_FoliageSway");
            }
        }

        // Flor amarela medicinal no topo
        GameObject flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flower.name = "Flower";
        flower.transform.SetParent(herbObj.transform);
        flower.transform.localPosition = new Vector3(0, 0.46f, 0);
        flower.transform.localScale = new Vector3(0.08f, 0.06f, 0.08f);
        SetColor(flower, new Color(1f, 0.85f, 0.1f));

        ApplyOrganicNoiseToMesh(herbObj, 0.06f, 4.0f);

        PrefabUtility.SaveAsPrefabAsset(herbObj, dir + "/Herb_Prefab.prefab");
        DestroyImmediate(herbObj);
    }

    private static void GenerateMushroomPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Vegetation/Mushroom_Cluster.obj")) return;
        GameObject mushObj = new GameObject("Mushroom_Prefab");

        // Haste carnuda com textura esbranquiçada
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(mushObj.transform);
        stem.transform.localPosition = new Vector3(0, 0.18f, 0);
        stem.transform.localScale = new Vector3(0.08f, 0.18f, 0.08f);
        stem.transform.localRotation = Quaternion.Euler(3f, 0, -2f);
        SetColor(stem, new Color(0.92f, 0.90f, 0.82f));

        // Chapéu convexo silvestre
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = "Cap";
        cap.transform.SetParent(mushObj.transform);
        cap.transform.localPosition = new Vector3(0, 0.34f, 0);
        cap.transform.localScale = new Vector3(0.38f, 0.18f, 0.38f);
        SetColor(cap, new Color(0.55f, 0.22f, 0.18f));

        // Lamelas inferiores
        GameObject gills = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        gills.name = "Gills";
        gills.transform.SetParent(mushObj.transform);
        gills.transform.localPosition = new Vector3(0, 0.30f, 0);
        gills.transform.localScale = new Vector3(0.32f, 0.02f, 0.32f);
        SetColor(gills, new Color(0.85f, 0.80f, 0.72f));

        ApplyOrganicNoiseToMesh(mushObj, 0.08f, 3.5f);

        PrefabUtility.SaveAsPrefabAsset(mushObj, dir + "/Mushroom_Prefab.prefab");
        DestroyImmediate(mushObj);
    }

    private static void GenerateBerryPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Vegetation/Berry_Bush.obj")) return;
        GameObject berryBushObj = new GameObject("Berry_Prefab");

        // Folhagem densa do arbusto
        GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bush.name = "Bush_Foliage";
        bush.transform.SetParent(berryBushObj.transform);
        bush.transform.localPosition = new Vector3(0, 0.3f, 0);
        bush.transform.localScale = new Vector3(0.75f, 0.6f, 0.75f);
        SetColor(bush, new Color(0.12f, 0.38f, 0.12f), "Mat_FoliageSway");

        // Bagas silvestres maduras (arándanos / amoras da Galícia)
        Vector3[] berryPositions = {
            new Vector3(0.25f, 0.35f, 0.2f),
            new Vector3(-0.2f, 0.4f, 0.22f),
            new Vector3(0.1f, 0.45f, -0.28f),
            new Vector3(-0.25f, 0.3f, -0.15f),
            new Vector3(0.3f, 0.25f, -0.1f)
        };

        for (int i = 0; i < berryPositions.Length; i++)
        {
            GameObject berry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            berry.name = "Berry_" + i;
            berry.transform.SetParent(berryBushObj.transform);
            berry.transform.localPosition = berryPositions[i];
            berry.transform.localScale = new Vector3(0.11f, 0.11f, 0.11f);
            SetColor(berry, new Color(0.18f, 0.05f, 0.28f), "Mat_Meadow_Wildflower");
        }

        ApplyOrganicNoiseToMesh(berryBushObj, 0.12f, 3.0f);

        PrefabUtility.SaveAsPrefabAsset(berryBushObj, dir + "/Berry_Prefab.prefab");
        DestroyImmediate(berryBushObj);
    }

    private static void GenerateAxePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Weapons/Survival_Axe.obj")) return;
        GameObject axeObj = new GameObject("Axe_Prefab");

        // Cabo ergonômico de madeira entalhada (Ash/Freixo)
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Axe_Handle";
        handle.transform.SetParent(axeObj.transform);
        handle.transform.localPosition = new Vector3(0, 0, 0);
        handle.transform.localScale = new Vector3(0.042f, 0.45f, 0.048f);
        handle.transform.localRotation = Quaternion.Euler(0, 0, 4f);
        SetColor(handle, new Color(0.32f, 0.22f, 0.12f), "Wood_Bark");

        // Empunhadura de tiras de couro curtido
        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        grip.name = "Leather_Grip";
        grip.transform.SetParent(axeObj.transform);
        grip.transform.localPosition = new Vector3(-0.015f, -0.2f, 0);
        grip.transform.localScale = new Vector3(0.052f, 0.2f, 0.056f);
        grip.transform.localRotation = Quaternion.Euler(0, 0, 4f);
        SetColor(grip, new Color(0.22f, 0.14f, 0.08f), "Mat_DarkForest_Bark");

        // Anel de reforço de ferro
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Iron_Collar";
        ring.transform.SetParent(axeObj.transform);
        ring.transform.localPosition = new Vector3(0.018f, 0.32f, 0);
        ring.transform.localScale = new Vector3(0.055f, 0.035f, 0.06f);
        SetColor(ring, new Color(0.35f, 0.35f, 0.37f), "Mat_Ruins_Iron");

        // Lâmina de ferro forjado chanfrada (Cutting Bit)
        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "Axe_Blade";
        blade.transform.SetParent(axeObj.transform);
        blade.transform.localPosition = new Vector3(0.12f, 0.38f, 0);
        blade.transform.localScale = new Vector3(0.18f, 0.14f, 0.024f);
        blade.transform.localRotation = Quaternion.Euler(0, 0, -8f);
        SetColor(blade, new Color(0.68f, 0.70f, 0.72f), "Mat_Ruins_Iron");

        // Martelo de trás da machadinha (Poll)
        GameObject poll = GameObject.CreatePrimitive(PrimitiveType.Cube);
        poll.name = "Axe_Poll";
        poll.transform.SetParent(axeObj.transform);
        poll.transform.localPosition = new Vector3(-0.045f, 0.38f, 0);
        poll.transform.localScale = new Vector3(0.07f, 0.08f, 0.038f);
        SetColor(poll, new Color(0.45f, 0.45f, 0.47f), "Mat_Ruins_Iron");

        // Amarração de corda em cruz (Leather Cross-Lashings)
        GameObject lash1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lash1.name = "Lash_Cross1";
        lash1.transform.SetParent(axeObj.transform);
        lash1.transform.localPosition = new Vector3(0.02f, 0.38f, 0);
        lash1.transform.localScale = new Vector3(0.058f, 0.05f, 0.058f);
        lash1.transform.localRotation = Quaternion.Euler(35f, 0, 0);
        SetColor(lash1, new Color(0.28f, 0.20f, 0.12f), "Wood_Bark");

        PrefabUtility.SaveAsPrefabAsset(axeObj, dir + "/Axe_Prefab.prefab");
        DestroyImmediate(axeObj);
    }

    private static void GenerateTorchPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Weapons/Torch.obj")) return;
        GameObject torchObj = new GameObject("Torch_Prefab");

        // Cabo de ramo entalhado orgânico
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Branch_Handle";
        handle.transform.SetParent(torchObj.transform);
        handle.transform.localPosition = new Vector3(0, 0, 0);
        handle.transform.localScale = new Vector3(0.042f, 0.38f, 0.042f);
        handle.transform.localRotation = Quaternion.Euler(2f, 0, -2f);
        SetColor(handle, new Color(0.24f, 0.16f, 0.08f), "Mat_DarkForest_Bark");

        // Tecido de sarapilheira / juta enrolado na cabeça
        GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cloth.name = "Burlap_Wrap";
        cloth.transform.SetParent(torchObj.transform);
        cloth.transform.localPosition = new Vector3(0, 0.32f, 0);
        cloth.transform.localScale = new Vector3(0.09f, 0.12f, 0.09f);
        SetColor(cloth, new Color(0.18f, 0.14f, 0.1f));

        // Fios de arame de amarração
        for (int w = -1; w <= 1; w++)
        {
            GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wire.name = "Wire_Tie_" + w;
            wire.transform.SetParent(torchObj.transform);
            wire.transform.localPosition = new Vector3(0, 0.32f + (w * 0.04f), 0);
            wire.transform.localScale = new Vector3(0.096f, 0.012f, 0.096f);
            SetColor(wire, new Color(0.4f, 0.38f, 0.35f));
        }

        // Núcleo em brasa / chama
        GameObject firePoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        firePoint.name = "Flame_EmberPoint";
        firePoint.transform.SetParent(torchObj.transform);
        firePoint.transform.localPosition = new Vector3(0, 0.44f, 0);
        firePoint.transform.localScale = new Vector3(0.11f, 0.15f, 0.11f);
        SetColor(firePoint, new Color(1f, 0.55f, 0.05f), "Mat_Emissive_Campfire");

        Light l = firePoint.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.65f, 0.25f);
        l.range = 9.5f;
        l.intensity = 2.4f;
        l.shadows = LightShadows.Soft;

        PrefabUtility.SaveAsPrefabAsset(torchObj, dir + "/Torch_Prefab.prefab");
        DestroyImmediate(torchObj);
    }
    
    private static void GeneratePickaxePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Weapons/Pickaxe.obj")) return;
        GameObject pickaxeObj = new GameObject("Pickaxe_Prefab");

        // Cabo longo de freixo com curva ergonômica
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Handle";
        handle.transform.SetParent(pickaxeObj.transform);
        handle.transform.localPosition = new Vector3(0, 0, 0);
        handle.transform.localScale = new Vector3(0.045f, 0.5f, 0.045f);
        handle.transform.localRotation = Quaternion.Euler(0, 0, 3f);
        SetColor(handle, new Color(0.34f, 0.24f, 0.14f), "Wood_Bark");

        // Colar de fixação de ferro
        GameObject collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        collar.name = "Collar";
        collar.transform.SetParent(pickaxeObj.transform);
        collar.transform.localPosition = new Vector3(0.018f, 0.36f, 0);
        collar.transform.localScale = new Vector3(0.06f, 0.05f, 0.06f);
        SetColor(collar, new Color(0.35f, 0.35f, 0.38f), "Mat_Ruins_Iron");

        // Cabeça dupla forjada (Ponteiro pontiagudo e choupa plana)
        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        blade.name = "Pick_Head";
        blade.transform.SetParent(pickaxeObj.transform);
        blade.transform.localPosition = new Vector3(0.018f, 0.42f, 0);
        blade.transform.localScale = new Vector3(0.065f, 0.38f, 0.065f);
        blade.transform.localRotation = Quaternion.Euler(0, 0, 84f);
        SetColor(blade, new Color(0.62f, 0.64f, 0.66f), "Mat_Ruins_Iron");

        PrefabUtility.SaveAsPrefabAsset(pickaxeObj, dir + "/Pickaxe_Prefab.prefab");
        DestroyImmediate(pickaxeObj);
    }
    
    private static void GenerateLanternPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Weapons/Lantern.obj")) return;
        GameObject lanternObj = new GameObject("Lantern_Prefab");

        // Base de ferro fundido com reservatório de óleo
        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.name = "Oil_Reservoir";
        baseObj.transform.SetParent(lanternObj.transform);
        baseObj.transform.localPosition = new Vector3(0, 0, 0);
        baseObj.transform.localScale = new Vector3(0.18f, 0.06f, 0.18f);
        SetColor(baseObj, new Color(0.22f, 0.22f, 0.24f));

        // Chaminé de vidro cilíndrico
        GameObject glassObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        glassObj.name = "Glass_Chimney";
        glassObj.transform.SetParent(lanternObj.transform);
        glassObj.transform.localPosition = new Vector3(0, 0.16f, 0);
        glassObj.transform.localScale = new Vector3(0.14f, 0.12f, 0.14f);
        SetColor(glassObj, new Color(0.95f, 0.95f, 0.9f, 0.45f));

        // 4 Barras de gaiola de proteção vertical
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "CageBar_" + i;
            bar.transform.SetParent(lanternObj.transform);
            bar.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.08f, 0.16f, Mathf.Sin(angle) * 0.08f);
            bar.transform.localScale = new Vector3(0.012f, 0.12f, 0.012f);
            SetColor(bar, new Color(0.25f, 0.25f, 0.27f));
        }

        // Cúpula superior de exaustão
        GameObject hood = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hood.name = "Vented_Hood";
        hood.transform.SetParent(lanternObj.transform);
        hood.transform.localPosition = new Vector3(0, 0.31f, 0);
        hood.transform.localScale = new Vector3(0.17f, 0.04f, 0.17f);
        SetColor(hood, new Color(0.22f, 0.22f, 0.24f));

        // Anel de transporte superior
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Handle_Ring";
        ring.transform.SetParent(lanternObj.transform);
        ring.transform.localPosition = new Vector3(0, 0.38f, 0);
        ring.transform.localScale = new Vector3(0.08f, 0.01f, 0.08f);
        ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
        SetColor(ring, new Color(0.55f, 0.45f, 0.2f));

        // Pavio e Luz Quente de Querosene
        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.name = "Flame_Wick";
        flame.transform.SetParent(lanternObj.transform);
        flame.transform.localPosition = new Vector3(0, 0.14f, 0);
        flame.transform.localScale = new Vector3(0.035f, 0.05f, 0.035f);
        SetColor(flame, new Color(1f, 0.85f, 0.4f), "Mat_Emissive_Campfire");

        Light l = lanternObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.82f, 0.45f);
        l.range = 16f;
        l.intensity = 3.2f;
        l.shadows = LightShadows.Soft;

        PrefabUtility.SaveAsPrefabAsset(lanternObj, dir + "/Lantern_Prefab.prefab");
        DestroyImmediate(lanternObj);
    }
    
    private static void GenerateBandagePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Bandage_Roll.obj")) return;
        GameObject bandageObj = new GameObject("Bandage_Prefab");
        
        GameObject roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        roll.transform.SetParent(bandageObj.transform);
        roll.transform.localPosition = new Vector3(0, 0, 0);
        roll.transform.localScale = new Vector3(0.1f, 0.05f, 0.1f);
        SetColor(roll, new Color(0.9f, 0.9f, 0.8f));

        PrefabUtility.SaveAsPrefabAsset(bandageObj, dir + "/Bandage_Prefab.prefab");
        DestroyImmediate(bandageObj);
    }
    
    private static void GenerateBowPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Weapons/Bow.obj")) return;
        GameObject bowObj = new GameObject("Bow_Prefab");
        
        // Curved arc
        GameObject arc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        arc.transform.SetParent(bowObj.transform);
        arc.transform.localPosition = new Vector3(0, 0, 0);
        arc.transform.localScale = new Vector3(0.05f, 0.6f, 0.05f);
        SetColor(arc, new Color(0.4f, 0.2f, 0.05f)); // Madeira escura
        
        // String
        GameObject stringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stringObj.transform.SetParent(bowObj.transform);
        stringObj.transform.localPosition = new Vector3(0.1f, 0, 0);
        stringObj.transform.localScale = new Vector3(0.01f, 0.6f, 0.01f);
        SetColor(stringObj, new Color(0.9f, 0.9f, 0.9f));
        
        PrefabUtility.SaveAsPrefabAsset(bowObj, dir + "/Bow_Prefab.prefab");
        DestroyImmediate(bowObj);
    }
    
    private static void GenerateArrowPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Weapons/Arrow.obj")) return;
        GameObject arrowObj = new GameObject("Arrow_Prefab");
        
        // Shaft
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.transform.SetParent(arrowObj.transform);
        shaft.transform.localPosition = new Vector3(0, 0, 0);
        shaft.transform.localScale = new Vector3(0.02f, 0.3f, 0.02f);
        shaft.transform.localRotation = Quaternion.Euler(0, 0, 90);
        SetColor(shaft, new Color(0.5f, 0.3f, 0.1f));
        
        // Tip
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.transform.SetParent(arrowObj.transform);
        tip.transform.localPosition = new Vector3(0.3f, 0, 0);
        tip.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        SetColor(tip, new Color(0.2f, 0.2f, 0.2f)); // Pedra flint
        
        PrefabUtility.SaveAsPrefabAsset(arrowObj, dir + "/Arrow_Prefab.prefab");
        DestroyImmediate(arrowObj);
    }
    
    private static void GenerateFishingRodPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Weapons/Fishing_Rod.obj")) return;
        GameObject rodObj = new GameObject("FishingRod_Prefab");
        
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.transform.SetParent(rodObj.transform);
        pole.transform.localPosition = new Vector3(0, 0.5f, 0.3f);
        pole.transform.localScale = new Vector3(0.03f, 0.8f, 0.03f);
        pole.transform.localRotation = Quaternion.Euler(45, 0, 0);
        SetColor(pole, new Color(0.4f, 0.2f, 0.05f)); 
        
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        line.transform.SetParent(rodObj.transform);
        line.transform.localPosition = new Vector3(0, 0, 0.8f);
        line.transform.localScale = new Vector3(0.005f, 0.5f, 0.005f);
        SetColor(line, new Color(0.9f, 0.9f, 0.9f));
        
        PrefabUtility.SaveAsPrefabAsset(rodObj, dir + "/FishingRod_Prefab.prefab");
        DestroyImmediate(rodObj);
    }
    
    private static void GenerateWaterCatcherPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Water_Catcher.obj")) return;
        GameObject catcherObj = new GameObject("WaterCatcher_Prefab");
        
        // Base supports
        for(int i=0; i<4; i++)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.transform.SetParent(catcherObj.transform);
            float x = (i % 2 == 0) ? 0.4f : -0.4f;
            float z = (i < 2) ? 0.4f : -0.4f;
            leg.transform.localPosition = new Vector3(x, 0.5f, z);
            leg.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
            SetColor(leg, new Color(0.3f, 0.15f, 0.05f));
        }
        
        // Tarp (Cone upside down)
        GameObject tarp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tarp.transform.SetParent(catcherObj.transform);
        tarp.transform.localPosition = new Vector3(0, 0.8f, 0);
        tarp.transform.localScale = new Vector3(1f, 0.4f, 1f);
        tarp.transform.localRotation = Quaternion.Euler(180, 0, 0);
        SetColor(tarp, new Color(0.2f, 0.2f, 0.8f)); // Blue lona
        
        PrefabUtility.SaveAsPrefabAsset(catcherObj, dir + "/WaterCatcher_Prefab.prefab");
        DestroyImmediate(catcherObj);
    }
    
    private static void GenerateWorkbenchPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Workbench.obj")) return;
        GameObject benchObj = new GameObject("Workbench_Prefab");
        
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.transform.SetParent(benchObj.transform);
        table.transform.localPosition = new Vector3(0, 0.5f, 0);
        table.transform.localScale = new Vector3(1.2f, 0.1f, 0.6f);
        SetColor(table, new Color(0.4f, 0.25f, 0.1f));
        
        for(int i=0; i<4; i++)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.transform.SetParent(benchObj.transform);
            float x = (i % 2 == 0) ? 0.5f : -0.5f;
            float z = (i < 2) ? 0.2f : -0.2f;
            leg.transform.localPosition = new Vector3(x, 0.25f, z);
            leg.transform.localScale = new Vector3(0.05f, 0.25f, 0.05f);
            SetColor(leg, new Color(0.3f, 0.2f, 0.1f));
        }
        
        PrefabUtility.SaveAsPrefabAsset(benchObj, dir + "/Workbench_Prefab.prefab");
        DestroyImmediate(benchObj);
    }
    
    private static void GeneratePondPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Pond_Basin.obj")) return;
        GameObject pondObj = new GameObject("Pond_Prefab");

        // Margem de pedras e rochas graníticas com musgo
        for (int s = 0; s < 8; s++)
        {
            float angle = s * 45f * Mathf.Deg2Rad;
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Shore_Stone_" + s;
            stone.transform.SetParent(pondObj.transform);
            stone.transform.localPosition = new Vector3(Mathf.Cos(angle) * 1.85f, 0.08f, Mathf.Sin(angle) * 1.85f);
            stone.transform.localScale = new Vector3(Random.Range(0.4f, 0.7f), Random.Range(0.2f, 0.35f), Random.Range(0.4f, 0.7f));
            stone.transform.localRotation = Quaternion.Euler(Random.Range(-20, 20), Random.Range(0, 360), Random.Range(-20, 20));
            SetColor(stone, new Color(0.38f, 0.38f, 0.4f), "Celtic_Stone", "celtic_stone");
        }

        // Superfície da água translúcida e móvel (GalicianWater_Lit)
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        water.name = "Water_Surface";
        water.transform.SetParent(pondObj.transform);
        water.transform.localPosition = new Vector3(0, 0.02f, 0);
        water.transform.localScale = new Vector3(3.6f, 0.01f, 3.6f);
        SetColor(water, new Color(0.08f, 0.22f, 0.28f, 0.8f), "Water_Surface");

        water.AddComponent<AnimatedWater>();

        ApplyOrganicNoiseToMesh(pondObj, 0.15f, 2.0f);

        PrefabUtility.SaveAsPrefabAsset(pondObj, dir + "/Pond_Prefab.prefab");
        DestroyImmediate(pondObj);
    }
    
    private static void GenerateHoePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Weapons/Hoe.obj")) return;
        GameObject hoeObj = new GameObject("Hoe_Prefab");
        
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.transform.SetParent(hoeObj.transform);
        handle.transform.localPosition = new Vector3(0, 0.4f, 0);
        handle.transform.localScale = new Vector3(0.04f, 0.6f, 0.04f);
        SetColor(handle, new Color(0.5f, 0.3f, 0.1f));
        
        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.transform.SetParent(hoeObj.transform);
        blade.transform.localPosition = new Vector3(0, 0.9f, 0.1f);
        blade.transform.localScale = new Vector3(0.2f, 0.05f, 0.3f);
        SetColor(blade, new Color(0.4f, 0.4f, 0.4f));
        
        PrefabUtility.SaveAsPrefabAsset(hoeObj, dir + "/Hoe_Prefab.prefab");
        DestroyImmediate(hoeObj);
    }
    
    private static void GenerateCauldronPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Cauldron.obj")) return;
        GameObject cauldronObj = new GameObject("Cauldron_Prefab");
        
        GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bowl.transform.SetParent(cauldronObj.transform);
        bowl.transform.localPosition = new Vector3(0, 0.4f, 0);
        bowl.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
        SetColor(bowl, new Color(0.1f, 0.1f, 0.1f));
        
        // Tripod legs
        for(int i=0; i<3; i++)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.transform.SetParent(cauldronObj.transform);
            float angle = i * 120f * Mathf.Deg2Rad;
            leg.transform.localPosition = new Vector3(Mathf.Cos(angle)*0.3f, 0.3f, Mathf.Sin(angle)*0.3f);
            leg.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);
            leg.transform.localRotation = Quaternion.Euler(30f, i * 120f, 0);
            SetColor(leg, new Color(0.2f, 0.2f, 0.2f));
        }
        
        PrefabUtility.SaveAsPrefabAsset(cauldronObj, dir + "/Cauldron_Prefab.prefab");
        DestroyImmediate(cauldronObj);
    }
    
    private static void GenerateRabbitPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Fauna/Rabbit_Model.obj")) return;
        GameObject rabbitObj = new GameObject("Rabbit_Prefab");
        
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(rabbitObj.transform);
        body.transform.localPosition = new Vector3(0, 0.2f, 0);
        body.transform.localScale = new Vector3(0.2f, 0.2f, 0.3f);
        body.transform.localRotation = Quaternion.Euler(90, 0, 0);
        SetColor(body, new Color(0.8f, 0.8f, 0.8f));
        
        GameObject ear1 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        ear1.transform.SetParent(rabbitObj.transform);
        ear1.transform.localPosition = new Vector3(0.05f, 0.4f, 0.1f);
        ear1.transform.localScale = new Vector3(0.05f, 0.15f, 0.05f);
        SetColor(ear1, new Color(0.8f, 0.8f, 0.8f));
        
        GameObject ear2 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        ear2.transform.SetParent(rabbitObj.transform);
        ear2.transform.localPosition = new Vector3(-0.05f, 0.4f, 0.1f);
        ear2.transform.localScale = new Vector3(0.05f, 0.15f, 0.05f);
        SetColor(ear2, new Color(0.8f, 0.8f, 0.8f));
        
        CapsuleCollider col = rabbitObj.AddComponent<CapsuleCollider>();
        col.radius = 0.18f;
        col.height = 0.45f;
        col.center = new Vector3(0, 0.2f, 0);

        var anim = rabbitObj.AddComponent<AnimalProceduralAnimator>();
        anim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.Hopper;
        anim.strideSpeedMultiplier = 5.0f;
        anim.bodyBobAmount = 0.15f;

        PrefabUtility.SaveAsPrefabAsset(rabbitObj, dir + "/Rabbit_Prefab.prefab");
        DestroyImmediate(rabbitObj);
    }
    
    private static void GenerateFarmlandPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Farmland.obj")) return;
        GameObject farmObj = new GameObject("Farmland_Prefab");
        
        GameObject soil = GameObject.CreatePrimitive(PrimitiveType.Cube);
        soil.transform.SetParent(farmObj.transform);
        soil.transform.localPosition = new Vector3(0, 0.05f, 0);
        soil.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);
        SetColor(soil, new Color(0.25f, 0.15f, 0.05f)); // Terra molhada escura
        
        PrefabUtility.SaveAsPrefabAsset(farmObj, dir + "/Farmland_Prefab.prefab");
        DestroyImmediate(farmObj);
    }
    
    private static void GenerateSnareTrapPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Snare_Trap.obj")) return;
        GameObject snareObj = new GameObject("SnareTrap_Prefab");
        
        GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wire.transform.SetParent(snareObj.transform);
        wire.transform.localPosition = new Vector3(0, 0.1f, 0);
        wire.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        SetColor(wire, new Color(0.8f, 0.8f, 0.8f)); // Wire color
        
        GameObject peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        peg.transform.SetParent(snareObj.transform);
        peg.transform.localPosition = new Vector3(0.3f, 0.1f, 0);
        peg.transform.localScale = new Vector3(0.02f, 0.2f, 0.02f);
        SetColor(peg, new Color(0.3f, 0.1f, 0.05f));
        
        PrefabUtility.SaveAsPrefabAsset(snareObj, dir + "/SnareTrap_Prefab.prefab");
        DestroyImmediate(snareObj);
    }

    // ============ LOOP 8: ENVIRONMENTAL MODELS ============

        private static void GenerateRunicStonePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Rocks/Runic_Stone.obj")) return;
        GameObject obj = new GameObject("RunicStone_Prefab");

        // Monólito de granito antigo entalhado com 3 blocos irregulares
        for (int i = 0; i < 3; i++)
        {
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stone.name = "Runic_Slab_" + i;
            stone.transform.SetParent(obj.transform);
            stone.transform.localPosition = new Vector3(Random.Range(-0.08f, 0.08f), 0.9f + (i * 0.1f), Random.Range(-0.08f, 0.08f));
            stone.transform.localScale = new Vector3(0.55f + Random.Range(-0.08f, 0.12f), 1.8f - (i * 0.2f), 0.38f + Random.Range(-0.06f, 0.08f));
            stone.transform.localRotation = Quaternion.Euler(Random.Range(-6, 6), Random.Range(0, 360), Random.Range(-6, 6));
            SetColor(stone, new Color(0.38f, 0.38f, 0.41f), "Celtic_Stone", "celtic_stone");
        }

        // Glifo rúnico luminoso entalhado em baixo-relevo
        GameObject rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rune.name = "Rune_Glyph";
        rune.transform.SetParent(obj.transform);
        rune.transform.localPosition = new Vector3(0, 0.95f, 0.21f);
        rune.transform.localScale = new Vector3(0.06f, 0.95f, 0.04f);
        SetColor(rune, new Color(0.1f, 0.95f, 0.75f), "Mat_Emissive_RuneGlow");

        ApplyOrganicNoiseToMesh(obj, 0.2f, 2.0f);

        BoxCollider coll = obj.AddComponent<BoxCollider>();
        coll.size = new Vector3(0.85f, 2.0f, 0.65f);
        coll.center = new Vector3(0, 1.0f, 0);

        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/RunicStone_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
    private static void GenerateFallenLogPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Props/Fallen_Log.obj")) return;
        GameObject obj = new GameObject("FallenLog_Prefab");

        // Tronco caído oco e envelhecido
        GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log.name = "Fallen_Trunk";
        log.transform.SetParent(obj.transform);
        log.transform.localPosition = new Vector3(0, 0.22f, 0);
        log.transform.localScale = new Vector3(0.38f, 1.4f, 0.38f);
        log.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        SetColor(log, new Color(0.26f, 0.18f, 0.1f), "Wood_Bark");

        // Tocos de galhos partidos
        for (int i = 0; i < 2; i++)
        {
            GameObject stub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stub.name = "Branch_Stub_" + i;
            stub.transform.SetParent(obj.transform);
            stub.transform.localPosition = new Vector3(-0.4f + (i * 0.9f), 0.38f, 0.15f * (i == 0 ? 1 : -1));
            stub.transform.localScale = new Vector3(0.08f, 0.2f, 0.08f);
            stub.transform.localRotation = Quaternion.Euler(45f * (i == 0 ? 1 : -1), 0, 0);
            SetColor(stub, new Color(0.24f, 0.16f, 0.08f), "Wood_Bark");
        }

        // Cogumelos em prateleira (Polypores / Fungos de árvore da Galícia)
        for (int m = 0; m < 3; m++)
        {
            GameObject shelfFungus = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shelfFungus.name = "Shelf_Fungus_" + m;
            shelfFungus.transform.SetParent(obj.transform);
            shelfFungus.transform.localPosition = new Vector3(-0.5f + (m * 0.45f), 0.28f + (m * 0.04f), 0.34f);
            shelfFungus.transform.localScale = new Vector3(0.18f, 0.02f, 0.12f);
            shelfFungus.transform.localRotation = Quaternion.Euler(15f, 0, 0);
            SetColor(shelfFungus, new Color(0.6f, 0.52f, 0.4f));
        }

        // Manto de musgo espesso na crista do tronco
        GameObject mossRidge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mossRidge.name = "Moss_Ridge";
        mossRidge.transform.SetParent(obj.transform);
        mossRidge.transform.localPosition = new Vector3(0.1f, 0.38f, 0);
        mossRidge.transform.localScale = new Vector3(1.2f, 0.14f, 0.32f);
        SetColor(mossRidge, new Color(0.12f, 0.34f, 0.08f), "Mat_DarkForest_Moss");

        ApplyOrganicNoiseToMesh(obj, 0.16f, 2.0f);

        BoxCollider coll = obj.AddComponent<BoxCollider>();
        coll.size = new Vector3(2.8f, 0.6f, 0.8f);
        coll.center = new Vector3(0, 0.25f, 0);

        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/FallenLog_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
        private static void GenerateDroppedWoodPrefab(string dir)
    {
        GameObject obj = new GameObject("DroppedWood_Prefab");
        GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log.transform.SetParent(obj.transform);
        log.transform.localScale = new Vector3(0.2f, 0.6f, 0.2f);
        log.transform.localRotation = Quaternion.Euler(90, 0, 0);
        SetColor(log, new Color(0.4f, 0.3f, 0.2f), "Wood_Bark", "pine_bark");
        
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.mass = 2f;
        BoxCollider coll = obj.AddComponent<BoxCollider>();
        coll.size = new Vector3(0.4f, 0.4f, 1.2f);
        
        // Atacha o script dinamicamente no Unity Editor
        var script = obj.AddComponent<ResourcePickup>();
        script.itemID = 100; // Madeira
        script.amount = 1;

        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/DroppedWood_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
    private static void GenerateThornyBushPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Vegetation/Thorny_Bush.obj")) return;
        GameObject obj = new GameObject("ThornyBush_Prefab");

        // Galhos espinhosos cruzados na base
        for (int s = 0; s < 4; s++)
        {
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Spine_Stem_" + s;
            stem.transform.SetParent(obj.transform);
            float angle = s * 90f + 20f;
            stem.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.15f, 0.35f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.15f);
            stem.transform.localScale = new Vector3(0.04f, 0.45f, 0.04f);
            stem.transform.localRotation = Quaternion.Euler(Random.Range(-25, 25), angle, Random.Range(-25, 25));
            SetColor(stem, new Color(0.2f, 0.16f, 0.1f), "Mat_DarkForest_Bark");
        }

        // Folhagem densa volumétrica com shader de folha The Forest
        for (int i = 0; i < 4; i++)
        {
            GameObject cluster = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cluster.name = "Bush_Cluster_" + i;
            cluster.transform.SetParent(obj.transform);
            cluster.transform.localPosition = new Vector3(Random.Range(-0.25f, 0.25f), 0.45f + (i * 0.1f), Random.Range(-0.25f, 0.25f));
            float scale = Random.Range(0.85f, 1.25f);
            cluster.transform.localScale = new Vector3(scale, scale * 0.7f, scale);
            SetColor(cluster, new Color(0.13f, 0.32f, 0.1f), "Bush_Leaves", "foliage_dark");
        }

        // Pequenas flores amarelas características do tojo galego
        for (int fl = 0; fl < 5; fl++)
        {
            GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bloom.name = "Toxo_Bloom_" + fl;
            bloom.transform.SetParent(obj.transform);
            float bAngle = fl * 72f * Mathf.Deg2Rad;
            bloom.transform.localPosition = new Vector3(Mathf.Cos(bAngle) * 0.35f, 0.55f + Random.Range(-0.1f, 0.15f), Mathf.Sin(bAngle) * 0.35f);
            bloom.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
            SetColor(bloom, new Color(1f, 0.88f, 0.15f));
        }

        ApplyOrganicNoiseToMesh(obj, 0.14f, 2.8f);

        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/ThornyBush_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
    private static void GenerateGlowMushroomPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Vegetation/Mushroom_Cluster.obj")) return;
        GameObject obj = new GameObject("GlowMushroom_Prefab");
        
        // Stem
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.transform.SetParent(obj.transform);
        stem.transform.localPosition = new Vector3(0, 0.3f, 0);
        stem.transform.localScale = new Vector3(0.1f, 0.4f, 0.1f);
        SetColor(stem, new Color(0.4f, 0.5f, 0.4f));
        
        // Cap (emissive)
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.transform.SetParent(obj.transform);
        cap.transform.localPosition = new Vector3(0, 0.6f, 0);
        cap.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
        SetColor(cap, new Color(0.1f, 0.9f, 0.7f)); // Bioluminescent teal
        
        // Secondary smaller cap
        GameObject cap2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap2.transform.SetParent(obj.transform);
        cap2.transform.localPosition = new Vector3(0.15f, 0.45f, 0.1f);
        cap2.transform.localScale = new Vector3(0.2f, 0.12f, 0.2f);
        SetColor(cap2, new Color(0.05f, 0.7f, 0.9f)); // Blue glow
        
        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/GlowMushroom_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
    private static void GenerateCelticCrossPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Rocks/Celtic_Cross.obj")) return;
        GameObject obj = new GameObject("CelticCross_Prefab");

        // Base escalonada de 2 degraus
        GameObject base1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        base1.name = "Plinth_Step1";
        base1.transform.SetParent(obj.transform);
        base1.transform.localPosition = new Vector3(0, 0.15f, 0);
        base1.transform.localScale = new Vector3(0.9f, 0.3f, 0.9f);
        SetColor(base1, new Color(0.42f, 0.42f, 0.45f), "Celtic_Stone", "celtic_stone");

        GameObject base2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        base2.name = "Plinth_Step2";
        base2.transform.SetParent(obj.transform);
        base2.transform.localPosition = new Vector3(0, 0.38f, 0);
        base2.transform.localScale = new Vector3(0.65f, 0.2f, 0.65f);
        SetColor(base2, new Color(0.42f, 0.42f, 0.45f), "Celtic_Stone", "celtic_stone");

        // Haste vertical de granito
        GameObject vert = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vert.name = "Cross_Vertical";
        vert.transform.SetParent(obj.transform);
        vert.transform.localPosition = new Vector3(0, 1.45f, 0);
        vert.transform.localScale = new Vector3(0.18f, 2.0f, 0.14f);
        SetColor(vert, new Color(0.44f, 0.44f, 0.47f), "Celtic_Stone", "celtic_stone");

        // Braço horizontal
        GameObject horiz = GameObject.CreatePrimitive(PrimitiveType.Cube);
        horiz.name = "Cross_Horizontal";
        horiz.transform.SetParent(obj.transform);
        horiz.transform.localPosition = new Vector3(0, 1.8f, 0);
        horiz.transform.localScale = new Vector3(0.95f, 0.18f, 0.14f);
        SetColor(horiz, new Color(0.44f, 0.44f, 0.47f), "Celtic_Stone", "celtic_stone");

        // Nimbus / Aro Céltico Solar
        GameObject nimbus = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        nimbus.name = "Nimbus_Ring";
        nimbus.transform.SetParent(obj.transform);
        nimbus.transform.localPosition = new Vector3(0, 1.8f, 0);
        nimbus.transform.localScale = new Vector3(0.55f, 0.06f, 0.55f);
        nimbus.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        SetColor(nimbus, new Color(0.46f, 0.46f, 0.49f), "Celtic_Stone", "celtic_stone");

        ApplyOrganicNoiseToMesh(obj, 0.12f, 2.5f);

        BoxCollider coll = obj.AddComponent<BoxCollider>();
        coll.size = new Vector3(0.95f, 2.5f, 0.95f);
        coll.center = new Vector3(0, 1.25f, 0);

        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/CelticCross_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
    private static void GenerateAncientGravePrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Rocks/Ancient_Grave.obj")) return;
        GameObject obj = new GameObject("AncientGrave_Prefab");

        // Lápide de pedra de xisto/granito fincada inclinada
        GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stone.name = "Headstone";
        stone.transform.SetParent(obj.transform);
        stone.transform.localPosition = new Vector3(0, 0.48f, -0.35f);
        stone.transform.localScale = new Vector3(0.46f, 0.9f, 0.14f);
        stone.transform.localRotation = Quaternion.Euler(7f, 0, -4f);
        SetColor(stone, new Color(0.38f, 0.38f, 0.41f), "Celtic_Stone", "celtic_stone");

        // Montículo de terra de turfa escuro e pedras de contorno
        GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mound.name = "Burial_Mound";
        mound.transform.SetParent(obj.transform);
        mound.transform.localPosition = new Vector3(0, 0.1f, 0.25f);
        mound.transform.localScale = new Vector3(0.7f, 0.22f, 1.15f);
        SetColor(mound, new Color(0.18f, 0.13f, 0.08f), "Mat_DarkForest_Ground");

        // Pedras menores de orla
        for (int p = 0; p < 4; p++)
        {
            GameObject borderStone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            borderStone.name = "BorderStone_" + p;
            borderStone.transform.SetParent(obj.transform);
            float z = -0.1f + (p * 0.25f);
            float x = (p % 2 == 0) ? 0.35f : -0.35f;
            borderStone.transform.localPosition = new Vector3(x, 0.08f, z);
            borderStone.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f);
            SetColor(borderStone, new Color(0.36f, 0.36f, 0.38f), "Celtic_Stone", "celtic_stone");
        }

        ApplyOrganicNoiseToMesh(obj, 0.12f, 3.0f);

        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/AncientGrave_Prefab.prefab");
        DestroyImmediate(obj);
    }

        private static void GenerateWolfPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Fauna/Wolf_Model.obj")) return;
        GameObject wolfObj = new GameObject("Wolf_Prefab");
        
        // Body (Lean and aggressive)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(wolfObj.transform);
        body.transform.localPosition = new Vector3(0, 0.7f, 0);
        body.transform.localScale = new Vector3(0.45f, 0.5f, 0.9f);
        body.transform.localRotation = Quaternion.Euler(85, 0, 0);
        SetColor(body, new Color(0.2f, 0.2f, 0.22f), "Wolf_Fur", "wolf_fur"); 
        
        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(wolfObj.transform);
        head.transform.localPosition = new Vector3(0, 0.9f, 0.6f);
        head.transform.localScale = new Vector3(0.35f, 0.35f, 0.4f);
        SetColor(head, new Color(0.18f, 0.18f, 0.2f), "Wolf_Fur", "wolf_fur");
        
        // Snout
        GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        snout.transform.SetParent(wolfObj.transform);
        snout.transform.localPosition = new Vector3(0, 0.85f, 0.8f);
        snout.transform.localScale = new Vector3(0.2f, 0.25f, 0.3f);
        snout.transform.localRotation = Quaternion.Euler(90, 0, 0);
        SetColor(snout, new Color(0.1f, 0.1f, 0.12f));

        // Ears
        for (int i = -1; i <= 1; i+=2) {
            GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ear.transform.SetParent(wolfObj.transform);
            ear.transform.localPosition = new Vector3(i * 0.15f, 1.05f, 0.55f);
            ear.transform.localScale = new Vector3(0.1f, 0.15f, 0.1f);
            ear.transform.localRotation = Quaternion.Euler(0, 0, i * -20f);
            SetColor(ear, new Color(0.15f, 0.15f, 0.18f));
        }

        // Legs
        for (int i = -1; i <= 1; i+=2) {
            for (int j = -1; j <= 1; j+=2) {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.transform.SetParent(wolfObj.transform);
                float z = j == 1 ? 0.35f : -0.35f;
                leg.transform.localPosition = new Vector3(i * 0.15f, 0.3f, z);
                leg.transform.localScale = new Vector3(0.12f, 0.3f, 0.12f);
                SetColor(leg, new Color(0.18f, 0.18f, 0.2f), "Wolf_Fur", "wolf_fur");
            }
        }
        
        // Tail
        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        tail.transform.SetParent(wolfObj.transform);
        tail.transform.localPosition = new Vector3(0, 0.6f, -0.6f);
        tail.transform.localScale = new Vector3(0.15f, 0.35f, 0.15f);
        tail.transform.localRotation = Quaternion.Euler(45, 0, 0);
        SetColor(tail, new Color(0.15f, 0.15f, 0.18f), "Wolf_Fur", "wolf_fur");
        
        // Olhos Noturnos Luminescentes
        for (int i = -1; i <= 1; i += 2) {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye_" + (i == 1 ? "R" : "L");
            eye.transform.SetParent(wolfObj.transform);
            eye.transform.localPosition = new Vector3(i * 0.11f, 0.98f, 0.74f);
            eye.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
            SetColor(eye, new Color(1.0f, 0.85f, 0.2f), "Animal_GlowingEyes");
        }

        CapsuleCollider wCol = wolfObj.AddComponent<CapsuleCollider>();
        wCol.radius = 0.35f;
        wCol.height = 1.3f;
        wCol.center = new Vector3(0, 0.65f, 0);

        var wAnim = wolfObj.AddComponent<AnimalProceduralAnimator>();
        wAnim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.QuadrupedStalker;
        wAnim.strideSpeedMultiplier = 3.8f;
        wAnim.legSwingAngle = 28f;
        wAnim.bodyBobAmount = 0.05f;

        PrefabUtility.SaveAsPrefabAsset(wolfObj, dir + "/Wolf_Prefab.prefab");
        DestroyImmediate(wolfObj);
    }

        private static void GenerateDeerPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Fauna/Deer_Model.obj")) return;
        GameObject deerObj = new GameObject("Deer_Prefab");
        
        // Body (Tall and elegant)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(deerObj.transform);
        body.transform.localPosition = new Vector3(0, 1.0f, 0);
        body.transform.localScale = new Vector3(0.5f, 0.6f, 1.1f);
        body.transform.localRotation = Quaternion.Euler(90, 0, 0);
        SetColor(body, new Color(0.4f, 0.25f, 0.15f)); 
        
        // Neck
        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        neck.transform.SetParent(deerObj.transform);
        neck.transform.localPosition = new Vector3(0, 1.4f, 0.5f);
        neck.transform.localScale = new Vector3(0.25f, 0.4f, 0.25f);
        neck.transform.localRotation = Quaternion.Euler(45, 0, 0);
        SetColor(neck, new Color(0.35f, 0.2f, 0.1f));
        
        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        head.transform.SetParent(deerObj.transform);
        head.transform.localPosition = new Vector3(0, 1.7f, 0.7f);
        head.transform.localScale = new Vector3(0.2f, 0.2f, 0.4f);
        head.transform.localRotation = Quaternion.Euler(80, 0, 0);
        SetColor(head, new Color(0.35f, 0.2f, 0.1f));

        // Antlers (Chifres farpados)
        for (int i = -1; i <= 1; i+=2) {
            GameObject antlerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antlerBase.transform.SetParent(deerObj.transform);
            antlerBase.transform.localPosition = new Vector3(i * 0.15f, 1.9f, 0.6f);
            antlerBase.transform.localScale = new Vector3(0.04f, 0.3f, 0.04f);
            antlerBase.transform.localRotation = Quaternion.Euler(-30, 0, i * -20f);
            SetColor(antlerBase, new Color(0.8f, 0.7f, 0.6f));
            
            // Antler Branch
            GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            branch.transform.SetParent(antlerBase.transform);
            branch.transform.localPosition = new Vector3(i * 0.5f, 0.5f, 0.5f);
            branch.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            branch.transform.localRotation = Quaternion.Euler(-45, 0, i * -45f);
            SetColor(branch, new Color(0.8f, 0.7f, 0.6f));
        }

        // Legs (Long and thin)
        for (int i = -1; i <= 1; i+=2) {
            for (int j = -1; j <= 1; j+=2) {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.transform.SetParent(deerObj.transform);
                float z = j == 1 ? 0.4f : -0.4f;
                leg.transform.localPosition = new Vector3(i * 0.2f, 0.5f, z);
                leg.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
                SetColor(leg, new Color(0.25f, 0.15f, 0.1f));
            }
        }
        
        CapsuleCollider dCol = deerObj.AddComponent<CapsuleCollider>();
        dCol.radius = 0.40f;
        dCol.height = 1.7f;
        dCol.center = new Vector3(0, 1.0f, 0);

        var dAnim = deerObj.AddComponent<AnimalProceduralAnimator>();
        dAnim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.QuadrupedUngulate;
        dAnim.strideSpeedMultiplier = 3.2f;
        dAnim.legSwingAngle = 24f;
        dAnim.bodyBobAmount = 0.06f;

        PrefabUtility.SaveAsPrefabAsset(deerObj, dir + "/Deer_Prefab.prefab");
        DestroyImmediate(deerObj);
    }


    private static void GenerateBearPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Fauna/Bear_Model.obj")) return;
        GameObject bearObj = new GameObject("Bear_Prefab");

        // Corpo Macico e Pesado
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body_Torso";
        body.transform.SetParent(bearObj.transform);
        body.transform.localPosition = new Vector3(0, 0.95f, 0);
        body.transform.localScale = new Vector3(0.85f, 0.9f, 1.45f);
        body.transform.localRotation = Quaternion.Euler(85, 0, 0);
        SetColor(body, new Color(0.20f, 0.14f, 0.10f), "Bear_Fur", "wolf_fur");

        // Cupim / Ombros Poderosos
        GameObject hump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hump.name = "Shoulder_Hump";
        hump.transform.SetParent(bearObj.transform);
        hump.transform.localPosition = new Vector3(0, 1.25f, 0.35f);
        hump.transform.localScale = new Vector3(0.8f, 0.65f, 0.75f);
        SetColor(hump, new Color(0.18f, 0.12f, 0.08f), "Bear_Fur", "wolf_fur");

        // Cabeca Larga
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(bearObj.transform);
        head.transform.localPosition = new Vector3(0, 1.15f, 0.85f);
        head.transform.localScale = new Vector3(0.55f, 0.5f, 0.55f);
        SetColor(head, new Color(0.22f, 0.15f, 0.11f), "Bear_Fur", "wolf_fur");

        // Focinho Grosso
        GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        snout.name = "Snout";
        snout.transform.SetParent(bearObj.transform);
        snout.transform.localPosition = new Vector3(0, 1.0f, 1.15f);
        snout.transform.localScale = new Vector3(0.32f, 0.32f, 0.42f);
        snout.transform.localRotation = Quaternion.Euler(90, 0, 0);
        SetColor(snout, new Color(0.14f, 0.10f, 0.08f));

        // Orelhas Arredondadas
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ear.name = "Ear_" + (i == 1 ? "R" : "L");
            ear.transform.SetParent(bearObj.transform);
            ear.transform.localPosition = new Vector3(i * 0.28f, 1.45f, 0.75f);
            ear.transform.localScale = new Vector3(0.16f, 0.16f, 0.10f);
            SetColor(ear, new Color(0.15f, 0.10f, 0.08f));
        }

        // Patas Robustas
        for (int i = -1; i <= 1; i += 2)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                string side = (j == 1 ? "F" : "B") + (i == 1 ? "R" : "L");
                leg.name = "Leg_" + side;
                leg.transform.SetParent(bearObj.transform);
                float z = j == 1 ? 0.45f : -0.45f;
                leg.transform.localPosition = new Vector3(i * 0.32f, 0.42f, z);
                leg.transform.localScale = new Vector3(0.22f, 0.42f, 0.22f);
                SetColor(leg, new Color(0.16f, 0.11f, 0.08f), "Bear_Fur", "wolf_fur");
            }
        }

        // Cauda Curta
        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tail.name = "Tail";
        tail.transform.SetParent(bearObj.transform);
        tail.transform.localPosition = new Vector3(0, 0.95f, -0.8f);
        tail.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
        SetColor(tail, new Color(0.15f, 0.10f, 0.08f));

        // Colisor e Animador Procedural
        CapsuleCollider col = bearObj.AddComponent<CapsuleCollider>();
        col.radius = 0.65f;
        col.height = 1.9f;
        col.center = new Vector3(0, 0.85f, 0);

        var anim = bearObj.AddComponent<AnimalProceduralAnimator>();
        anim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.QuadrupedHeavy;
        anim.strideSpeedMultiplier = 2.4f;
        anim.legSwingAngle = 20f;
        anim.bodyBobAmount = 0.06f;

        PrefabUtility.SaveAsPrefabAsset(bearObj, dir + "/Bear_Prefab.prefab");
        DestroyImmediate(bearObj);
    }

    private static void GenerateBoarPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Fauna/Boar_Model.obj")) return;
        GameObject boarObj = new GameObject("Boar_Prefab");

        // Corpo Musculoso e Compacto
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body_Torso";
        body.transform.SetParent(boarObj.transform);
        body.transform.localPosition = new Vector3(0, 0.55f, 0);
        body.transform.localScale = new Vector3(0.55f, 0.62f, 1.15f);
        body.transform.localRotation = Quaternion.Euler(85, 0, 0);
        SetColor(body, new Color(0.25f, 0.18f, 0.14f), "Boar_Bristles", "wolf_fur");

        // Cerdas / Crista Dorsal
        for (int s = 0; s < 4; s++)
        {
            GameObject bristle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bristle.name = "Bristle_" + s;
            bristle.transform.SetParent(boarObj.transform);
            bristle.transform.localPosition = new Vector3(0, 0.92f - s * 0.06f, 0.35f - s * 0.22f);
            bristle.transform.localScale = new Vector3(0.08f, 0.18f, 0.18f);
            bristle.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            SetColor(bristle, new Color(0.16f, 0.12f, 0.10f));
        }

        // Cabeca Conica com Focinho Achatado
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(boarObj.transform);
        head.transform.localPosition = new Vector3(0, 0.65f, 0.65f);
        head.transform.localScale = new Vector3(0.42f, 0.45f, 0.52f);
        SetColor(head, new Color(0.22f, 0.16f, 0.12f), "Boar_Bristles", "wolf_fur");

        GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        snout.name = "Snout";
        snout.transform.SetParent(boarObj.transform);
        snout.transform.localPosition = new Vector3(0, 0.52f, 0.92f);
        snout.transform.localScale = new Vector3(0.24f, 0.14f, 0.24f);
        snout.transform.localRotation = Quaternion.Euler(90, 0, 0);
        SetColor(snout, new Color(0.14f, 0.10f, 0.08f));

        // Presas Brancas Afiadas (Tusks)
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject tusk = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tusk.name = "Tusk_" + (i == 1 ? "R" : "L");
            tusk.transform.SetParent(boarObj.transform);
            tusk.transform.localPosition = new Vector3(i * 0.15f, 0.56f, 0.90f);
            tusk.transform.localScale = new Vector3(0.06f, 0.16f, 0.06f);
            tusk.transform.localRotation = Quaternion.Euler(-40f, 0f, i * -25f);
            SetColor(tusk, new Color(0.92f, 0.90f, 0.82f));
        }

        // Orelhas Pontiagudas
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ear.name = "Ear_" + (i == 1 ? "R" : "L");
            ear.transform.SetParent(boarObj.transform);
            ear.transform.localPosition = new Vector3(i * 0.20f, 0.88f, 0.58f);
            ear.transform.localScale = new Vector3(0.10f, 0.20f, 0.08f);
            ear.transform.localRotation = Quaternion.Euler(20f, 0f, i * -30f);
            SetColor(ear, new Color(0.18f, 0.13f, 0.10f));
        }

        // Patas
        for (int i = -1; i <= 1; i += 2)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                string side = (j == 1 ? "F" : "B") + (i == 1 ? "R" : "L");
                leg.name = "Leg_" + side;
                leg.transform.SetParent(boarObj.transform);
                float z = j == 1 ? 0.35f : -0.35f;
                leg.transform.localPosition = new Vector3(i * 0.18f, 0.24f, z);
                leg.transform.localScale = new Vector3(0.14f, 0.24f, 0.14f);
                SetColor(leg, new Color(0.15f, 0.11f, 0.08f));
            }
        }

        // Colisor e Animador Procedural
        CapsuleCollider col = boarObj.AddComponent<CapsuleCollider>();
        col.radius = 0.45f;
        col.height = 1.4f;
        col.center = new Vector3(0, 0.5f, 0);

        var anim = boarObj.AddComponent<AnimalProceduralAnimator>();
        anim.locomotionStyle = AnimalProceduralAnimator.LocomotionStyle.QuadrupedUngulate;
        anim.strideSpeedMultiplier = 3.6f;
        anim.legSwingAngle = 24f;
        anim.bodyBobAmount = 0.04f;

        PrefabUtility.SaveAsPrefabAsset(boarObj, dir + "/Boar_Prefab.prefab");
        DestroyImmediate(boarObj);
    }

    private static void GenerateRuinPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Rocks/Celtic_Ruin.obj")) return;
        GameObject ruinObj = new GameObject("Ruin_Prefab");

        // 1. Pilares Megaliticos Verticais (Anta / Dolmen Celta da Galicia)
        Vector3[] pillarOffsets = new Vector3[] {
            new Vector3(-1.6f, 1.8f, -1.4f),
            new Vector3(1.6f, 1.75f, -1.3f),
            new Vector3(-1.5f, 1.85f, 1.4f),
            new Vector3(1.7f, 1.8f, 1.35f)
        };

        for (int i = 0; i < pillarOffsets.Length; i++)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Megalith_Pillar_" + i;
            pillar.transform.SetParent(ruinObj.transform);
            pillar.transform.localPosition = pillarOffsets[i];
            pillar.transform.localScale = new Vector3(0.95f, 3.6f, 0.95f);
            pillar.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f));
            SetColor(pillar, new Color(0.55f, 0.55f, 0.58f), "Celtic_Stone", "celtic_stone");
        }

        // 2. Colossal Laje Superior Horizontal (Capstone / Tampa do Dolmen)
        GameObject capstone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        capstone.name = "Megalith_Capstone";
        capstone.transform.SetParent(ruinObj.transform);
        capstone.transform.localPosition = new Vector3(0.05f, 3.75f, 0.02f);
        capstone.transform.localScale = new Vector3(4.2f, 0.75f, 4.4f);
        capstone.transform.localRotation = Quaternion.Euler(-2.5f, 18f, 1.5f);
        SetColor(capstone, new Color(0.52f, 0.52f, 0.55f), "Celtic_Stone", "celtic_stone");

        // 3. Pedra de Altar / Lareira Ritual Central
        GameObject hearth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hearth.name = "Ritual_Hearth";
        hearth.transform.SetParent(ruinObj.transform);
        hearth.transform.localPosition = new Vector3(0, 0.2f, 0);
        hearth.transform.localScale = new Vector3(1.8f, 0.35f, 1.8f);
        SetColor(hearth, new Color(0.38f, 0.36f, 0.34f), "Mat_Ruins_Stone", "celtic_stone");

        // 4. Monolitos e Penedos Caidos nos Arredores
        for (int r = 0; r < 3; r++)
        {
            GameObject fallen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallen.name = "Fallen_Slab_" + r;
            fallen.transform.SetParent(ruinObj.transform);
            float ang = r * 2.1f;
            fallen.transform.localPosition = new Vector3(Mathf.Cos(ang) * 3.2f, 0.4f, Mathf.Sin(ang) * 3.2f);
            fallen.transform.localScale = new Vector3(1.1f, 0.6f, 1.8f);
            fallen.transform.localRotation = Quaternion.Euler(Random.Range(10f, 35f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
            SetColor(fallen, new Color(0.50f, 0.50f, 0.52f), "Celtic_Stone", "celtic_stone");
        }

        PrefabUtility.SaveAsPrefabAsset(ruinObj, dir + "/Ruin_Prefab.prefab");
        DestroyImmediate(ruinObj);
    }

    private static void GenerateBossPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Entities/Boss_Anciao.obj")) return;
        GameObject bossObj = new GameObject("Boss_Prefab");
        
        // Body (Giant)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.transform.SetParent(bossObj.transform);
        body.transform.localPosition = new Vector3(0, 2.5f, 0);
        body.transform.localScale = new Vector3(1.5f, 2.5f, 1.5f);
        SetColor(body, new Color(0.1f, 0.05f, 0.15f, 0.9f)); // Roxo escuro

        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(bossObj.transform);
        head.transform.localPosition = new Vector3(0, 5.5f, 0.5f);
        head.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        SetColor(head, new Color(0.05f, 0.05f, 0.05f));

        // Horns / Crown
        for(int i = -1; i <= 1; i += 2)
        {
            GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            horn.transform.SetParent(bossObj.transform);
            horn.transform.localPosition = new Vector3(0.5f * i, 6.2f, 0.2f);
            horn.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);
            horn.transform.localRotation = Quaternion.Euler(30, 0, 30 * -i);
            SetColor(horn, new Color(1f, 0.2f, 0.1f));
        }

        // Glowing Eyes
        for(int i = -1; i <= 1; i += 2)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.transform.SetParent(bossObj.transform);
            eye.transform.localPosition = new Vector3(0.3f * i, 5.5f, 1.0f);
            eye.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            SetColor(eye, new Color(1f, 0.1f, 0.1f));
            
            Light eyeLight = eye.AddComponent<Light>();
            eyeLight.type = LightType.Point;
            eyeLight.color = Color.red;
            eyeLight.range = 5f;
            eyeLight.intensity = 2f;
        }

        // Floating runestones around him
        for(int i = 0; i < 3; i++)
        {
            GameObject rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rune.transform.SetParent(bossObj.transform);
            float angle = i * 120f * Mathf.Deg2Rad;
            rune.transform.localPosition = new Vector3(Mathf.Cos(angle) * 2.5f, 3f, Mathf.Sin(angle) * 2.5f);
            rune.transform.localScale = new Vector3(0.3f, 0.8f, 0.1f);
            SetColor(rune, new Color(0.2f, 0.2f, 0.2f));
        }

        PrefabUtility.SaveAsPrefabAsset(bossObj, dir + "/Boss_Prefab.prefab");
        DestroyImmediate(bossObj);
    }

    private static void GenerateAltarPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Rocks/Stone_Altar.obj")) return;
        GameObject altarObj = new GameObject("Altar_Prefab");

        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.transform.SetParent(altarObj.transform);
        baseObj.transform.localPosition = new Vector3(0, 0.5f, 0);
        baseObj.transform.localScale = new Vector3(3f, 0.5f, 3f);
        SetColor(baseObj, new Color(0.2f, 0.2f, 0.2f));

        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.transform.SetParent(altarObj.transform);
        pillar.transform.localPosition = new Vector3(0, 1.5f, 0);
        pillar.transform.localScale = new Vector3(1f, 1f, 1f);
        SetColor(pillar, new Color(0.15f, 0.15f, 0.15f));

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.transform.SetParent(altarObj.transform);
        top.transform.localPosition = new Vector3(0, 2.6f, 0);
        top.transform.localScale = new Vector3(1.5f, 0.2f, 1.5f);
        SetColor(top, new Color(0.25f, 0.25f, 0.25f));

        Light aura = altarObj.AddComponent<Light>();
        aura.type = LightType.Point;
        aura.color = new Color(0.2f, 0.5f, 1f);
        aura.intensity = 2f;
        aura.range = 8f;

        PrefabUtility.SaveAsPrefabAsset(altarObj, dir + "/Altar_Prefab.prefab");
        DestroyImmediate(altarObj);
    }


    private static void GenerateFentoPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Vegetation/Fento_Fern.obj")) return;
        GameObject obj = new GameObject("Fento_Prefab");

        // Rizoma / Coroa central
        GameObject rhizome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rhizome.name = "Rhizome_Crown";
        rhizome.transform.SetParent(obj.transform);
        rhizome.transform.localPosition = new Vector3(0, 0.08f, 0);
        rhizome.transform.localScale = new Vector3(0.22f, 0.12f, 0.22f);
        SetColor(rhizome, new Color(0.2f, 0.14f, 0.08f), "Mat_DarkForest_Bark");

        // 8 Frondes arqueadas em leque radial (Fentos das Fragas do Eume)
        int frondCount = 8;
        for (int f = 0; f < frondCount; f++)
        {
            float angle = f * (360f / frondCount) + Random.Range(-8f, 8f);
            Quaternion frondYaw = Quaternion.Euler(0, angle, 0);

            // Raque (eixo central da fronde)
            GameObject stipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stipe.name = "Stipe_" + f;
            stipe.transform.SetParent(obj.transform);
            Quaternion stipePitch = frondYaw * Quaternion.Euler(38f, 0, 0);
            stipe.transform.localRotation = stipePitch;
            Vector3 stipeDir = stipePitch * Vector3.up;
            stipe.transform.localPosition = new Vector3(0, 0.08f, 0) + stipeDir * 0.35f;
            stipe.transform.localScale = new Vector3(0.02f, 0.4f, 0.02f);
            SetColor(stipe, new Color(0.18f, 0.35f, 0.12f), "Mat_DarkForest_Bark");

            // Lâmina foliar da fronde (leque translúcido com shader de folhagem)
            GameObject leafBlade = GameObject.CreatePrimitive(PrimitiveType.Quad);
            leafBlade.name = "Frond_Blade_" + f;
            leafBlade.transform.SetParent(obj.transform);
            Vector3 bladePos = new Vector3(0, 0.08f, 0) + stipeDir * 0.55f;
            leafBlade.transform.localPosition = bladePos;
            leafBlade.transform.localRotation = frondYaw * Quaternion.Euler(48f, 0, 0);
            leafBlade.transform.localScale = new Vector3(0.45f, 0.75f, 0.45f);
            SetColor(leafBlade, new Color(0.16f, 0.42f, 0.15f), "Mat_FoliageSway");
        }

        ApplyOrganicNoiseToMesh(obj, 0.08f, 3.2f);

        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/Fento_Prefab.prefab");
        DestroyImmediate(obj);
    }

    private static void SetColor(GameObject obj, Color color, string matName = "", string textureName = "")
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null)
        {
            string resolvedMatName = matName;
            if (string.IsNullOrEmpty(resolvedMatName))
            {
                resolvedMatName = "Mat_Auto_" + ColorUtility.ToHtmlStringRGB(color);
            }

            string matPath = "Assets/Resources/Materials/" + resolvedMatName + ".mat";
            Material loadedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (loadedMat != null)
            {
                r.sharedMaterial = loadedMat;
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.2f);
            mat.enableInstancing = true;
            
            if (!string.IsNullOrEmpty(textureName))
            {
                Texture2D tex = Resources.Load<Texture2D>("Textures/" + textureName);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
                    mat.color = Color.white;
                    mat.SetColor("_BaseColor", Color.white);
                }
            }

            if (!System.IO.Directory.Exists("Assets/Resources/Materials"))
                System.IO.Directory.CreateDirectory("Assets/Resources/Materials");

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            r.sharedMaterial = mat;
        }
    }

        private static void ApplyOrganicNoiseToMesh(GameObject obj, float intensity, float scale)
    {
        MeshFilter[] mfs = obj.GetComponentsInChildren<MeshFilter>();
        int meshIndex = 0;
        string meshDir = "Assets/Art/Meshes";
        if (!System.IO.Directory.Exists(meshDir))
            System.IO.Directory.CreateDirectory(meshDir);

        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            Mesh m = Instantiate(mf.sharedMesh);
            Vector3[] verts = m.vertices;
            for (int v = 0; v < verts.Length; v++)
            {
                Vector3 worldPos = mf.transform.TransformPoint(verts[v]);
                float noiseX = Mathf.PerlinNoise(worldPos.y * scale, worldPos.z * scale) * 2f - 1f;
                float noiseY = Mathf.PerlinNoise(worldPos.x * scale, worldPos.z * scale) * 2f - 1f;
                float noiseZ = Mathf.PerlinNoise(worldPos.x * scale, worldPos.y * scale) * 2f - 1f;
                verts[v] += new Vector3(noiseX, noiseY, noiseZ) * intensity;
            }
            m.vertices = verts;
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.name = $"{obj.name}_{mf.gameObject.name}_{meshIndex}";

            string meshPath = $"{meshDir}/{obj.name}_{mf.gameObject.name}_{meshIndex}.asset";
            AssetDatabase.CreateAsset(m, meshPath);
            mf.sharedMesh = m;
            
            MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = m;
            meshIndex++;
        }
    }
    private static void GenerateFarolCeltaPrefab(string dir)
    {
        if (System.IO.File.Exists("Assets/Art/Models/Rocks/Farol_Celta.obj")) return;
        GameObject farolObj = new GameObject("FarolCelta_Prefab");
        
        // Base de Pedra
        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.transform.SetParent(farolObj.transform);
        baseObj.transform.localPosition = new Vector3(0, 2f, 0);
        baseObj.transform.localScale = new Vector3(4f, 2f, 4f);
        SetColor(baseObj, new Color(0.3f, 0.3f, 0.3f), "Mat_Ruins_Stone");
        
        // Torre Principal
        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tower.transform.SetParent(farolObj.transform);
        tower.transform.localPosition = new Vector3(0, 10f, 0);
        tower.transform.localScale = new Vector3(3f, 6f, 3f);
        SetColor(tower, new Color(0.2f, 0.2f, 0.2f), "Mat_Ruins_Stone");
        
        // Varanda / Ring Superior
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(farolObj.transform);
        ring.transform.localPosition = new Vector3(0, 16.5f, 0);
        ring.transform.localScale = new Vector3(3.5f, 0.5f, 3.5f);
        SetColor(ring, new Color(0.15f, 0.15f, 0.15f), "Mat_Ruins_Iron");
        
        // Cupula e Luz do Farol
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.transform.SetParent(farolObj.transform);
        core.transform.localPosition = new Vector3(0, 18f, 0);
        core.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
        SetColor(core, new Color(0.1f, 0.5f, 1f), "Mat_Emissive_HolyWater"); // Vidro azulado antes de acender
        
        // Elemento estrutural
        FarolCeltaSystem system = farolObj.AddComponent<FarolCeltaSystem>();
        
        // Criar Luz Real
        GameObject lightObj = new GameObject("SweepingLight");
        lightObj.transform.SetParent(core.transform);
        lightObj.transform.localPosition = Vector3.zero;
        Light l = lightObj.AddComponent<Light>();
        system.farolLight = l;
        
        // Elemento estrutural
        ApplyOrganicNoiseToMesh(baseObj, 0.3f, 1.5f);
        ApplyOrganicNoiseToMesh(tower, 0.2f, 2f);
        
        PrefabUtility.SaveAsPrefabAsset(farolObj, dir + "/FarolCelta_Prefab.prefab");
        DestroyImmediate(farolObj);
    }

    [MenuItem("Fraga Native/5. Reparar Materiais e Prefabs (Remover Rosa/Erros)")]
    public static void SanitizeAllPrefabs()
    {
        string prefabsDir = "Assets/Resources/Prefabs";
        string matDir = "Assets/Resources/Materials";
        if (!System.IO.Directory.Exists(prefabsDir)) return;

        Material barkMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_DarkForest_Bark.mat");
        Material foliageMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_OakFoliage.mat");
        Material rockMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Celtic_Stone.mat");
        Material mossMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_DarkForest_Moss.mat");
        Material ironMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_Ruins_Iron.mat");
        Material woodMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_Wood_Carved.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Wood_Bark.mat");
        Material leatherMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_Leather_Wrap.mat");
        Material fabricMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_Linen_Fabric.mat");
        Material clothMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_Player_DarkCloth.mat") ?? fabricMat;
        Material soilMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_Farmland_Soil.mat");
        Material boneMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_Bone_Horn.mat");
        Material waterMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Water_Surface.mat");
        Material vultoMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Mat_Vulto_Cloak.mat");
        Material wolfMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Wolf_Fur.mat");
        Material deerMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Deer_Pelt.mat") ?? wolfMat;
        Material bearMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Bear_Fur.mat") ?? wolfMat;
        Material boarMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Boar_Bristles.mat") ?? wolfMat;
        Material rabbitMat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/Rabbit_Fur.mat") ?? wolfMat;

        string[] prefabFiles = System.IO.Directory.GetFiles(prefabsDir, "*.prefab");
        int repairedCount = 0;

        foreach (var pFile in prefabFiles)
        {
            string unityPath = pFile.Replace("\\", "/");
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(unityPath);
            if (prefabAsset == null) continue;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            bool modified = false;

            Renderer[] rends = inst.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                int subCount = (mf != null && mf.sharedMesh != null) ? Mathf.Max(1, mf.sharedMesh.subMeshCount) : 1;
                Material[] mats = r.sharedMaterials;
                bool needsFix = (mats == null || mats.Length < subCount);
                if (!needsFix)
                {
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null || mats[m].shader == null || mats[m].shader.name.Contains("Error") || mats[m].shader.name == "Hidden/InternalErrorShader")
                        {
                            needsFix = true;
                            break;
                        }
                    }
                }

                if (needsFix)
                {
                    Material[] fixedMats = new Material[subCount];
                    for (int s = 0; s < subCount; s++)
                    {
                        if (mats != null && s < mats.Length && mats[s] != null && mats[s].shader != null && !mats[s].shader.name.Contains("Error") && mats[s].shader.name != "Hidden/InternalErrorShader")
                        {
                            fixedMats[s] = mats[s];
                        }
                        else
                        {
                            string oName = r.gameObject.name.ToLower();
                            string pName = prefabAsset.name.ToLower();
                            if (oName.Contains("berry") || pName.Contains("berry")) fixedMats[s] = foliageMat ?? mossMat;
                            else if (oName.Contains("foliage") || oName.Contains("leaf") || oName.Contains("bush") || oName.Contains("fern") || oName.Contains("fento")) fixedMats[s] = foliageMat;
                            else if (oName.Contains("stone") || oName.Contains("rock") || oName.Contains("altar") || oName.Contains("cross") || oName.Contains("grave")) fixedMats[s] = rockMat;
                            else if (oName.Contains("water") || pName.Contains("pond")) fixedMats[s] = waterMat ?? rockMat;
                            else if (oName.Contains("iron") || oName.Contains("metal") || oName.Contains("wire")) fixedMats[s] = ironMat;
                            else if (oName.Contains("leather") || oName.Contains("grip")) fixedMats[s] = leatherMat ?? barkMat;
                            else if (oName.Contains("fabric") || oName.Contains("bandage")) fixedMats[s] = fabricMat ?? barkMat;
                            else if (oName.Contains("soil") || pName.Contains("farmland")) fixedMats[s] = soilMat ?? barkMat;
                            else if (oName.Contains("bone") || oName.Contains("horn")) fixedMats[s] = boneMat ?? rockMat;
                            else if (pName.Contains("wolf")) fixedMats[s] = wolfMat ?? barkMat;
                            else if (pName.Contains("deer")) fixedMats[s] = deerMat ?? barkMat;
                            else if (pName.Contains("bear")) fixedMats[s] = bearMat ?? barkMat;
                            else if (pName.Contains("boar")) fixedMats[s] = boarMat ?? barkMat;
                            else if (pName.Contains("rabbit")) fixedMats[s] = rabbitMat ?? barkMat;
                            else if (pName.Contains("vulto") || pName.Contains("stalker") || pName.Contains("boss")) fixedMats[s] = vultoMat ?? barkMat;
                            else if (oName.Contains("wood") || oName.Contains("log") || oName.Contains("plank") || oName.Contains("workbench")) fixedMats[s] = woodMat ?? barkMat;
                            else if (oName.Contains("bark") || oName.Contains("trunk")) fixedMats[s] = barkMat;
                            else if (pName.Contains("player")) fixedMats[s] = clothMat ?? barkMat;
                            else fixedMats[s] = barkMat;
                        }
                    }
                    r.sharedMaterials = fixedMats;
                    modified = true;
                }
            }

            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(inst, unityPath);
                repairedCount++;
            }
            DestroyImmediate(inst);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FragaAssetGenerator] SanitizeAllPrefabs concluído! {repairedCount} prefabs corrigidos sem materiais rosa.");
    }
}
