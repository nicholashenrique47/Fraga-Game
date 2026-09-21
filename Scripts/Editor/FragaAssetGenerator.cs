using UnityEngine;
using UnityEditor;

public class FragaAssetGenerator : MonoBehaviour
{
    [MenuItem("Fraga Native/0. Gerar Modelos 3D (Estilo Low Poly)")]
    public static void GenerateAssets()
    {
        GenerateBarricadePrefab("Assets/Resources/Prefabs");
        string prefabsDir = "Assets/Resources/Prefabs";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        }

        GenerateTreePrefab(prefabsDir);
        GenerateRockPrefab(prefabsDir);
        GeneratePlayerPrefab(prefabsDir);
        GenerateVultoPrefab(prefabsDir);
        GenerateHerbPrefab(prefabsDir);
        GenerateMushroomPrefab(prefabsDir);
        GenerateBerryPrefab(prefabsDir);
        
        GenerateWolfPrefab(prefabsDir);
        GenerateDeerPrefab(prefabsDir);
        
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

        // Loop 13: Novos Modelos
        GenerateDeadTreePrefab(prefabsDir);
        GeneratePineTreePrefab(prefabsDir);
        GenerateStalkerPrefab(prefabsDir);
        
        GenerateFarolCeltaPrefab(prefabsDir);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("[FragaAssetGenerator] Modelos 3D Gerados com Sucesso!");
    }

    private static void GenerateTreePrefab(string dir)
    {
        GameObject treeObj = new GameObject("Tree_Prefab");
        
        // Tronco
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(treeObj.transform);
        trunk.transform.localPosition = new Vector3(0, 0.5f, 0);
        trunk.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
        SetColor(trunk, new Color(0.25f, 0.15f, 0.05f), "Mat_DarkForest_Bark");

        // Folhas (3 Cones invertidos estilizados)
        for(int i = 0; i < 3; i++)
        {
            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaves.transform.SetParent(treeObj.transform);
            float scale = 1.6f - (i * 0.4f);
            leaves.transform.localScale = new Vector3(scale, scale, scale);
            leaves.transform.localPosition = new Vector3(0, 1.0f + (i * 0.7f), 0);
            leaves.transform.localRotation = Quaternion.Euler(45, 45, 45); 
            SetColor(leaves, new Color(0.1f, 0.35f, 0.15f), "Mat_FoliageSway"); 
        }

        ApplyOrganicNoiseToMesh(treeObj, 0.15f, 2.5f);

        PrefabUtility.SaveAsPrefabAsset(treeObj, dir + "/Tree_Prefab.prefab");
        DestroyImmediate(treeObj);
    }

    private static void GenerateDeadTreePrefab(string dir)
    {
        GameObject treeObj = new GameObject("DeadTree_Prefab");
        
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(treeObj.transform);
        trunk.transform.localPosition = new Vector3(0, 1.5f, 0);
        trunk.transform.localScale = new Vector3(0.2f, 1.5f, 0.2f);
        trunk.transform.localRotation = Quaternion.Euler(0, 0, 5);
        SetColor(trunk, new Color(0.15f, 0.1f, 0.05f), "Mat_DarkForest_Bark");

        for(int i = 0; i < 4; i++)
        {
            GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            branch.transform.SetParent(treeObj.transform);
            branch.transform.localScale = new Vector3(0.05f, 0.8f, 0.05f);
            branch.transform.localPosition = new Vector3(0, 1.0f + (i * 0.5f), 0);
            float angleZ = (i % 2 == 0) ? 45 : -45;
            float angleY = i * 90;
            branch.transform.localRotation = Quaternion.Euler(0, angleY, angleZ);
            SetColor(branch, new Color(0.15f, 0.1f, 0.05f), "Mat_DarkForest_Bark");
        }

        ApplyOrganicNoiseToMesh(treeObj, 0.2f, 3.0f);

        PrefabUtility.SaveAsPrefabAsset(treeObj, dir + "/DeadTree_Prefab.prefab");
        DestroyImmediate(treeObj);
    }

    private static void GeneratePineTreePrefab(string dir)
    {
        GameObject treeObj = new GameObject("PineTree_Prefab");
        
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(treeObj.transform);
        trunk.transform.localPosition = new Vector3(0, 1f, 0);
        trunk.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
        SetColor(trunk, new Color(0.25f, 0.15f, 0.05f));

        for(int i = 0; i < 4; i++)
        {
            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            MeshFilter mf = leaves.GetComponent<MeshFilter>();
            
            // Hack para fazer um cone
            Mesh m = Instantiate(mf.sharedMesh);
            Vector3[] verts = m.vertices;
            for(int v=0; v<verts.Length; v++) {
                if(verts[v].y > 0) verts[v] = new Vector3(0, verts[v].y, 0);
            }
            m.vertices = verts;
            m.RecalculateNormals();
            mf.sharedMesh = m;

            leaves.transform.SetParent(treeObj.transform);
            float scale = 1.5f - (i * 0.3f);
            leaves.transform.localScale = new Vector3(scale, 1.2f, scale);
            leaves.transform.localPosition = new Vector3(0, 1.5f + (i * 0.8f), 0);
            SetColor(leaves, new Color(0.05f, 0.25f, 0.1f));
        }

        PrefabUtility.SaveAsPrefabAsset(treeObj, dir + "/PineTree_Prefab.prefab");
        DestroyImmediate(treeObj);
    }

        private static void GenerateRockPrefab(string dir)
    {
        GameObject rockObj = new GameObject("Rock_Prefab");
        
        for(int i=0; i<4; i++) {
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.transform.SetParent(rockObj.transform);
            stone.transform.localPosition = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 0.5f), Random.Range(-0.3f, 0.3f));
            stone.transform.localScale = new Vector3(Random.Range(0.5f, 1.2f), Random.Range(0.4f, 0.8f), Random.Range(0.5f, 1.2f));
            stone.transform.localRotation = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
            SetColor(stone, new Color(0.4f, 0.4f, 0.4f), "Ruin_Stones", "ruin_stones");
        }
        
        PrefabUtility.SaveAsPrefabAsset(rockObj, dir + "/Rock_Prefab.prefab");
        DestroyImmediate(rockObj);
    }

    private static void GeneratePlayerPrefab(string dir)
    {
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
        GameObject herbObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        herbObj.name = "Herb_Prefab";
        herbObj.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
        SetColor(herbObj, new Color(0.2f, 0.8f, 0.2f));
        PrefabUtility.SaveAsPrefabAsset(herbObj, dir + "/Herb_Prefab.prefab");
        DestroyImmediate(herbObj);
    }

    private static void GenerateMushroomPrefab(string dir)
    {
        GameObject mushObj = new GameObject("Mushroom_Prefab");
        
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.transform.SetParent(mushObj.transform);
        stem.transform.localPosition = new Vector3(0, 0.15f, 0);
        stem.transform.localScale = new Vector3(0.1f, 0.15f, 0.1f);
        SetColor(stem, Color.white);

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.transform.SetParent(mushObj.transform);
        cap.transform.localPosition = new Vector3(0, 0.3f, 0);
        cap.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
        SetColor(cap, new Color(0.6f, 0.1f, 0.8f));

        PrefabUtility.SaveAsPrefabAsset(mushObj, dir + "/Mushroom_Prefab.prefab");
        DestroyImmediate(mushObj);
    }

    private static void GenerateBerryPrefab(string dir)
    {
        GameObject berryBushObj = new GameObject("Berry_Prefab");
        
        GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bush.transform.SetParent(berryBushObj.transform);
        bush.transform.localPosition = new Vector3(0, 0.25f, 0);
        bush.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        SetColor(bush, new Color(0.1f, 0.4f, 0.1f));

        GameObject berry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        berry.transform.SetParent(berryBushObj.transform);
        berry.transform.localPosition = new Vector3(0.2f, 0.3f, 0.2f);
        berry.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        SetColor(berry, Color.red);

        PrefabUtility.SaveAsPrefabAsset(berryBushObj, dir + "/Berry_Prefab.prefab");
        DestroyImmediate(berryBushObj);
    }

    private static void GenerateAxePrefab(string dir)
    {
        GameObject axeObj = new GameObject("Axe_Prefab");
        
        // Handle
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.transform.SetParent(axeObj.transform);
        handle.transform.localPosition = new Vector3(0, 0, 0);
        handle.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);
        SetColor(handle, new Color(0.3f, 0.2f, 0.1f));

        // Blade
        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.transform.SetParent(axeObj.transform);
        blade.transform.localPosition = new Vector3(0.1f, 0.3f, 0);
        blade.transform.localScale = new Vector3(0.2f, 0.15f, 0.02f);
        SetColor(blade, new Color(0.7f, 0.7f, 0.7f)); // Metal/Ferro

        PrefabUtility.SaveAsPrefabAsset(axeObj, dir + "/Axe_Prefab.prefab");
        DestroyImmediate(axeObj);
    }

    private static void GenerateTorchPrefab(string dir)
    {
        GameObject torchObj = new GameObject("Torch_Prefab");
        
        // Handle
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.transform.SetParent(torchObj.transform);
        handle.transform.localPosition = new Vector3(0, 0, 0);
        handle.transform.localScale = new Vector3(0.04f, 0.3f, 0.04f);
        SetColor(handle, new Color(0.25f, 0.15f, 0.05f));

        // Fire point (To indicate where particle system goes)
        GameObject firePoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        firePoint.transform.SetParent(torchObj.transform);
        firePoint.transform.localPosition = new Vector3(0, 0.3f, 0);
        firePoint.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        SetColor(firePoint, new Color(1f, 0.5f, 0f)); // Laranja

        // Attach Light
        Light l = firePoint.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.6f, 0.2f);
        l.range = 8f;
        l.intensity = 2f;
        l.shadows = LightShadows.Soft;

        PrefabUtility.SaveAsPrefabAsset(torchObj, dir + "/Torch_Prefab.prefab");
        DestroyImmediate(torchObj);
    }
    
    private static void GeneratePickaxePrefab(string dir)
    {
        GameObject pickaxeObj = new GameObject("Pickaxe_Prefab");
        
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.transform.SetParent(pickaxeObj.transform);
        handle.transform.localPosition = new Vector3(0, 0, 0);
        handle.transform.localScale = new Vector3(0.05f, 0.45f, 0.05f);
        SetColor(handle, new Color(0.35f, 0.25f, 0.15f));

        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        blade.transform.SetParent(pickaxeObj.transform);
        blade.transform.localPosition = new Vector3(0, 0.35f, 0);
        blade.transform.localScale = new Vector3(0.08f, 0.3f, 0.08f);
        blade.transform.localRotation = Quaternion.Euler(0, 0, 90);
        SetColor(blade, new Color(0.6f, 0.6f, 0.6f));

        PrefabUtility.SaveAsPrefabAsset(pickaxeObj, dir + "/Pickaxe_Prefab.prefab");
        DestroyImmediate(pickaxeObj);
    }
    
    private static void GenerateLanternPrefab(string dir)
    {
        GameObject lanternObj = new GameObject("Lantern_Prefab");
        
        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.transform.SetParent(lanternObj.transform);
        baseObj.transform.localPosition = new Vector3(0, 0, 0);
        baseObj.transform.localScale = new Vector3(0.15f, 0.05f, 0.15f);
        SetColor(baseObj, new Color(0.1f, 0.1f, 0.1f));
        
        GameObject glassObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        glassObj.transform.SetParent(lanternObj.transform);
        glassObj.transform.localPosition = new Vector3(0, 0.15f, 0);
        glassObj.transform.localScale = new Vector3(0.12f, 0.1f, 0.12f);
        SetColor(glassObj, new Color(1f, 1f, 1f, 0.5f)); // Transparente idealmente
        
        GameObject topObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        topObj.transform.SetParent(lanternObj.transform);
        topObj.transform.localPosition = new Vector3(0, 0.3f, 0);
        topObj.transform.localScale = new Vector3(0.15f, 0.05f, 0.15f);
        SetColor(topObj, new Color(0.1f, 0.1f, 0.1f));
        
        Light l = lanternObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.8f, 0.4f);
        l.range = 15f;
        l.intensity = 3f;

        PrefabUtility.SaveAsPrefabAsset(lanternObj, dir + "/Lantern_Prefab.prefab");
        DestroyImmediate(lanternObj);
    }
    
    private static void GenerateBandagePrefab(string dir)
    {
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
        GameObject pondObj = new GameObject("Pond_Prefab");
        
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        water.transform.SetParent(pondObj.transform);
        water.transform.localPosition = new Vector3(0, 0.02f, 0);
        water.transform.localScale = new Vector3(2f, 0.01f, 2f);
        SetColor(water, new Color(0.1f, 0.3f, 0.8f, 0.8f)); // Agua transparente
        
        // Animate water
        water.AddComponent<AnimatedWater>();
        
        PrefabUtility.SaveAsPrefabAsset(pondObj, dir + "/Pond_Prefab.prefab");
        DestroyImmediate(pondObj);
    }
    
    private static void GenerateHoePrefab(string dir)
    {
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
        
        PrefabUtility.SaveAsPrefabAsset(rabbitObj, dir + "/Rabbit_Prefab.prefab");
        DestroyImmediate(rabbitObj);
    }
    
    private static void GenerateFarmlandPrefab(string dir)
    {
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
        GameObject obj = new GameObject("RunicStone_Prefab");
        
        // Base monolith (Jagged using multiple intersected cubes)
        for(int i = 0; i < 3; i++) {
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stone.transform.SetParent(obj.transform);
            stone.transform.localPosition = new Vector3(Random.Range(-0.1f, 0.1f), 0.8f + Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.1f));
            stone.transform.localScale = new Vector3(0.5f + Random.Range(-0.1f, 0.2f), 1.6f, 0.3f + Random.Range(-0.1f, 0.1f));
            stone.transform.localRotation = Quaternion.Euler(Random.Range(-5, 5), Random.Range(0, 360), Random.Range(-5, 5));
            SetColor(stone, new Color(0.35f, 0.35f, 0.38f), "Celtic_Stone", "celtic_stone");
        }
        
        // Rune glow strip
        GameObject rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rune.transform.SetParent(obj.transform);
        rune.transform.localPosition = new Vector3(0, 0.8f, 0.2f);
        rune.transform.localScale = new Vector3(0.05f, 0.8f, 0.05f);
        SetColor(rune, new Color(0.1f, 0.8f, 0.6f)); // Emissive teal
        
        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/RunicStone_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
    private static void GenerateFallenLogPrefab(string dir)
    {
        GameObject obj = new GameObject("FallenLog_Prefab");
        
        GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log.transform.SetParent(obj.transform);
        log.transform.localPosition = new Vector3(0, 0.15f, 0);
        log.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);
        log.transform.localRotation = Quaternion.Euler(0, 0, 90);
        SetColor(log, new Color(0.25f, 0.15f, 0.08f));
        
        // Moss patches
        GameObject moss = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moss.transform.SetParent(obj.transform);
        moss.transform.localPosition = new Vector3(0.3f, 0.25f, 0);
        moss.transform.localScale = new Vector3(0.3f, 0.1f, 0.2f);
        SetColor(moss, new Color(0.15f, 0.4f, 0.1f));
        
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
        GameObject obj = new GameObject("ThornyBush_Prefab");
        
        // Realistic Foliage Quads
        for (int i = 0; i < 3; i++)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(obj.transform);
            quad.transform.localPosition = new Vector3(0, 0.5f, 0);
            quad.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            quad.transform.localRotation = Quaternion.Euler(0, i * 60f, 0);
            SetColor(quad, new Color(0.15f, 0.25f, 0.1f), "Bush_Leaves", "foliage_dark");
        }
        
        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/ThornyBush_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
    private static void GenerateGlowMushroomPrefab(string dir)
    {
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
        GameObject obj = new GameObject("CelticCross_Prefab");
        
        // Vertical beam
        GameObject vert = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vert.transform.SetParent(obj.transform);
        vert.transform.localPosition = new Vector3(0, 1f, 0);
        vert.transform.localScale = new Vector3(0.15f, 2f, 0.1f);
        SetColor(vert, new Color(0.45f, 0.45f, 0.48f));
        
        // Horizontal beam
        GameObject horiz = GameObject.CreatePrimitive(PrimitiveType.Cube);
        horiz.transform.SetParent(obj.transform);
        horiz.transform.localPosition = new Vector3(0, 1.3f, 0);
        horiz.transform.localScale = new Vector3(0.8f, 0.15f, 0.1f);
        SetColor(horiz, new Color(0.45f, 0.45f, 0.48f));
        
        // Circle (nimbus)
        GameObject nimbus = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        nimbus.transform.SetParent(obj.transform);
        nimbus.transform.localPosition = new Vector3(0, 1.3f, 0);
        nimbus.transform.localScale = new Vector3(0.4f, 0.4f, 0.05f);
        SetColor(nimbus, new Color(0.5f, 0.5f, 0.52f));
        
        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/CelticCross_Prefab.prefab");
        DestroyImmediate(obj);
    }
    
    private static void GenerateAncientGravePrefab(string dir)
    {
        GameObject obj = new GameObject("AncientGrave_Prefab");
        
        // Tombstone
        GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stone.transform.SetParent(obj.transform);
        stone.transform.localPosition = new Vector3(0, 0.4f, 0);
        stone.transform.localScale = new Vector3(0.4f, 0.8f, 0.1f);
        stone.transform.localRotation = Quaternion.Euler(5, 0, -3);
        SetColor(stone, new Color(0.3f, 0.3f, 0.32f));
        
        // Dirt mound
        GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mound.transform.SetParent(obj.transform);
        mound.transform.localPosition = new Vector3(0, 0.08f, 0.3f);
        mound.transform.localScale = new Vector3(0.6f, 0.15f, 0.8f);
        SetColor(mound, new Color(0.2f, 0.15f, 0.08f));
        
        PrefabUtility.SaveAsPrefabAsset(obj, dir + "/AncientGrave_Prefab.prefab");
        DestroyImmediate(obj);
    }

        private static void GenerateWolfPrefab(string dir)
    {
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
        
        PrefabUtility.SaveAsPrefabAsset(wolfObj, dir + "/Wolf_Prefab.prefab");
        DestroyImmediate(wolfObj);
    }

        private static void GenerateDeerPrefab(string dir)
    {
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
        
        PrefabUtility.SaveAsPrefabAsset(deerObj, dir + "/Deer_Prefab.prefab");
        DestroyImmediate(deerObj);
    }

    private static void GenerateBossPrefab(string dir)
    {
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

    private static void SetColor(GameObject obj, Color color, string matName = "", string textureName = "")
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null)
        {
            if (!string.IsNullOrEmpty(matName))
            {
                Material loadedMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/" + matName + ".mat");
                if (loadedMat != null)
                {
                    r.sharedMaterial = loadedMat;
                    return;
                }
            }
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null) mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.enableInstancing = true;
            
            if (!string.IsNullOrEmpty(textureName))
            {
                Texture2D tex = Resources.Load<Texture2D>("Textures/" + textureName);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
                    mat.color = Color.white;
                }
            }

            if (!string.IsNullOrEmpty(matName))
            {
                if (!System.IO.Directory.Exists("Assets/Resources/Materials"))
                    System.IO.Directory.CreateDirectory("Assets/Resources/Materials");
                AssetDatabase.CreateAsset(mat, "Assets/Resources/Materials/" + matName + ".mat");
                AssetDatabase.SaveAssets();
            }
            r.sharedMaterial = mat;
        }
    }

    private static void ApplyOrganicNoiseToMesh(GameObject obj, float intensity, float scale)
    {
        MeshFilter[] mfs = obj.GetComponentsInChildren<MeshFilter>();
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
            mf.sharedMesh = m;
            
            MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = m;
        }
    }
    private static void GenerateFarolCeltaPrefab(string dir)
    {
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
        
        // CÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âºpula (Farol Core)
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.transform.SetParent(farolObj.transform);
        core.transform.localPosition = new Vector3(0, 18f, 0);
        core.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
        SetColor(core, new Color(0.1f, 0.5f, 1f)); // Vidro azulado antes de acender
        
        // Adicionar Componentes LÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³gicos
        FarolCeltaSystem system = farolObj.AddComponent<FarolCeltaSystem>();
        
        // Criar Luz Real
        GameObject lightObj = new GameObject("SweepingLight");
        lightObj.transform.SetParent(core.transform);
        lightObj.transform.localPosition = Vector3.zero;
        Light l = lightObj.AddComponent<Light>();
        system.farolLight = l;
        
        // Aplicar ruÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­do sÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â¦ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ nas pedras
        ApplyOrganicNoiseToMesh(baseObj, 0.3f, 1.5f);
        ApplyOrganicNoiseToMesh(tower, 0.2f, 2f);
        
        PrefabUtility.SaveAsPrefabAsset(farolObj, dir + "/FarolCelta_Prefab.prefab");
        DestroyImmediate(farolObj);
    }
}
