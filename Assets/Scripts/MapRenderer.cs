using UnityEngine;
using System.Collections.Generic;

public class MapRenderer : MonoBehaviour
{
    [Header("Map Visuals (Prefabs)")]
    public GameObject treePrefab;
    public GameObject pineTreePrefab;
    public GameObject deadTreePrefab;
    public GameObject fentoPrefab;
    public GameObject ruinPrefab;
    public GameObject rockPrefab;
    public GameObject barricadePrefab;
    public GameObject campfirePrefab;
    public GameObject berryPrefab;
    public GameObject mushroomPrefab;
    public GameObject pondPrefab;
    public GameObject waterCatcherPrefab;
    public GameObject workbenchPrefab;
    public GameObject farmlandPrefab;
    public GameObject cauldronPrefab;
    public GameObject snareTrapPrefab;
    
    // Loop 8: Environmental
    public GameObject runicStonePrefab;
    public GameObject fallenLogPrefab;
    public GameObject thornyBushPrefab;
    public GameObject glowMushroomPrefab;
    public GameObject celticCrossPrefab;
    public GameObject ancientGravePrefab;
    public GameObject farolCeltaPrefab;

    [Header("Rendering Settings")]
    public float tileSize = 1f;

    [Header("Terrain Generator")]
    public TerrainMeshGenerator terrainGenerator;

    private System.IntPtr mapDataPtr;
    private int mapWidth;
    private int mapHeight;

    // Dicionario para referenciar as instancias e poder destrui-las rapidamente
    public Dictionary<Vector2Int, GameObject> activeInstances = new Dictionary<Vector2Int, GameObject>();

    public void Initialize(System.IntPtr ptr, int width, int height)
    {
        mapDataPtr = ptr;
        mapWidth = width;
        mapHeight = height;
        UpgradeToAAATrees();
    }

    private void UpgradeToAAATrees()
    {
        InitFallbackMaterials();
        try
        {
            GameObject oakTemplate = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
                GalicianProceduralTreeGenerator.TreeType.GalicianOak, s_barkFallback, s_foliageFallback);
            oakTemplate.transform.position = new Vector3(0, -9999f, 0);
            oakTemplate.hideFlags = HideFlags.HideAndDontSave;
            treePrefab = oakTemplate;

            GameObject pineTemplate = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
                GalicianProceduralTreeGenerator.TreeType.MaritimePine, s_barkFallback, s_pineFallback);
            pineTemplate.transform.position = new Vector3(0, -9999f, 0);
            pineTemplate.hideFlags = HideFlags.HideAndDontSave;
            pineTreePrefab = pineTemplate;

            GameObject deadTemplate = GalicianProceduralTreeGenerator.CreateTreePrefabObject(
                GalicianProceduralTreeGenerator.TreeType.DeadTree, s_barkFallback, null);
            deadTemplate.transform.position = new Vector3(0, -9999f, 0);
            deadTemplate.hideFlags = HideFlags.HideAndDontSave;
            deadTreePrefab = deadTemplate;
            Debug.Log("[MapRenderer] Modelos de Arvores AAA (The Forest Standard) ativados com sucesso.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[MapRenderer] Falha ao instanciar gerador procedural de arvores: " + ex.Message);
        }
    }

    private static Material s_barkFallback;
    private static Material s_foliageFallback;
    private static Material s_pineFallback;
    private static Material s_rockFallback;
    private static Material s_mossFallback;

    private static void InitFallbackMaterials()
    {
        if (s_barkFallback == null) s_barkFallback = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        if (s_foliageFallback == null) s_foliageFallback = Resources.Load<Material>("Materials/Mat_OakFoliage");
        if (s_pineFallback == null) s_pineFallback = Resources.Load<Material>("Materials/Mat_PineNeedle");
        if (s_rockFallback == null) s_rockFallback = Resources.Load<Material>("Materials/Celtic_Stone");
        if (s_mossFallback == null) s_mossFallback = Resources.Load<Material>("Materials/Mat_DarkForest_Moss");
    }

    public static void EnsureValidMaterials(GameObject go, Material primary = null, Material secondary = null)
    {
        if (go == null) return;
        InitFallbackMaterials();
        
        if (primary == null)
        {
            string n = go.name.ToLower();
            if (n.Contains("fern") || n.Contains("fento") || n.Contains("bush") || n.Contains("foliage") || n.Contains("leaf") || n.Contains("berry"))
                primary = s_foliageFallback;
            else if (n.Contains("rock") || n.Contains("stone") || n.Contains("ruin") || n.Contains("grave") || n.Contains("cross"))
                primary = s_rockFallback;
            else if (n.Contains("moss") || n.Contains("mush"))
                primary = s_mossFallback;
            else
                primary = s_barkFallback;
        }

        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
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
                    bool validSlot = (mats != null && s < mats.Length && mats[s] != null && mats[s].shader != null && !mats[s].shader.name.Contains("Error") && mats[s].shader.name != "Hidden/InternalErrorShader");
                    if (validSlot)
                    {
                        fixedMats[s] = mats[s];
                    }
                    else
                    {
                        fixedMats[s] = (s == 0 || secondary == null) ? primary : secondary;
                        if (fixedMats[s] == null) fixedMats[s] = s_barkFallback;
                    }
                }
                r.sharedMaterials = fixedMats;
            }
        }
    }

    public void RenderInitialMap()
    {
        if (mapDataPtr == System.IntPtr.Zero || treePrefab == null)
        {
            Debug.LogError("[MapRenderer] Prefabs de mapa nao configurados.");
            return;
        }

        // Cleanup previously rendered objects for reload support
        foreach (var kvp in activeInstances)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        activeInstances.Clear();

        GameObject environmentParent = GameObject.Find("Environment_Instances");
        if (environmentParent == null) environmentParent = new GameObject("Environment_Instances");

        float centerX = mapWidth * 0.5f;
        float centerY = mapHeight * 0.5f;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int tileID = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, x, y);
                Vector3 pos = new Vector3(x * tileSize, TerrainMeshGenerator.GetTerrainHeight(x * tileSize, y * tileSize), y * tileSize);
                Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                
                GameObject inst = null;

                // Clareira do spawn / acampamento (raio de 6.5 metros livre de arvores)
                float distToSpawn = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                if (distToSpawn < 6.5f)
                {
                    // Na clareira central, coloca apenas alguma vegetacao rasteira ocasional
                    if (fentoPrefab != null && Random.value < 0.08f)
                    {
                        Vector3 fPos = pos + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
                        fPos.y = TerrainMeshGenerator.GetTerrainHeight(fPos.x, fPos.z);
                        GameObject fernObj = Instantiate(fentoPrefab, fPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0), environmentParent.transform);
                        fernObj.transform.localScale = Vector3.one * Random.Range(0.65f, 0.95f);
                        EnsureValidMaterials(fernObj);
                    }
                    continue;
                }

                if (tileID == 1) 
                {
                    // The Forest Style: Distribuicao organica com espacamento natural (3 a 4.5 metros)
                    // Grade hexagonal-staggered: espacamento regular para navegacao fluida e clareiras
                    bool isTreeSlot = ((x % 3 == 0) && ((y + (x / 3) % 2) % 3 == 0));
                    
                    // Ruido Perlin para densidade de bosques (groves vs glades)
                    float canopyNoise = Mathf.PerlinNoise(x * 0.09f + 13.5f, y * 0.09f + 71.2f);
                    
                    bool spawnTree = false;
                    if (isTreeSlot && canopyNoise > 0.32f)
                    {
                        spawnTree = true;
                    }
                    else if (canopyNoise <= 0.32f && (x % 6 == 0 && y % 6 == 0))
                    {
                        // Arvore solitaria em clareiras
                        spawnTree = true;
                    }

                    if (spawnTree)
                    {
                        GameObject chosenTree = treePrefab != null ? treePrefab : pineTreePrefab;
                        float variantRoll = Random.value;
                        if (variantRoll < 0.40f && pineTreePrefab != null) chosenTree = pineTreePrefab;
                        else if (variantRoll > 0.90f && deadTreePrefab != null) chosenTree = deadTreePrefab;
                        else if (treePrefab != null) chosenTree = treePrefab;

                        if (chosenTree != null)
                        {
                            Vector3 treePos = pos + new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.35f, 0.35f));
                            treePos.y = TerrainMeshGenerator.GetTerrainHeight(treePos.x, treePos.z);
                            Quaternion treeRot = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f));
                            
                            inst = Instantiate(chosenTree, treePos, treeRot, environmentParent.transform);
                            inst.SetActive(true);
                            EnsureValidMaterials(inst, s_barkFallback, (chosenTree == pineTreePrefab) ? s_pineFallback : s_foliageFallback);
                            float overallScale = Random.Range(0.85f, 1.30f);
                            float widthVariation = Random.Range(0.95f, 1.15f);
                            inst.transform.localScale = new Vector3(overallScale * widthVariation, overallScale, overallScale * widthVariation);

                            // Sub-bosque denso estilo The Forest: Fentos das Fragas e Tojo ao redor das raizes
                            if (fentoPrefab != null && Random.value < 0.45f)
                            {
                                int fernClusters = Random.Range(1, 3);
                                for (int fc = 0; fc < fernClusters; fc++)
                                {
                                    float rad = Random.Range(0.7f, 1.4f);
                                    float ang = Random.Range(0f, Mathf.PI * 2f);
                                    Vector3 fernPos = treePos + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                                    fernPos.y = TerrainMeshGenerator.GetTerrainHeight(fernPos.x, fernPos.z);
                                    GameObject fernObj = Instantiate(fentoPrefab, fernPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0), inst.transform);
                                    fernObj.transform.localScale = Vector3.one * Random.Range(0.75f, 1.25f);
                                    EnsureValidMaterials(fernObj);
                                }
                            }
                            if (thornyBushPrefab != null && Random.value < 0.18f)
                            {
                                float rad = Random.Range(0.8f, 1.6f);
                                float ang = Random.Range(0f, Mathf.PI * 2f);
                                Vector3 bushPos = treePos + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                                bushPos.y = TerrainMeshGenerator.GetTerrainHeight(bushPos.x, bushPos.z);
                                GameObject bObj = Instantiate(thornyBushPrefab, bushPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0), inst.transform);
                                bObj.transform.localScale = Vector3.one * Random.Range(0.65f, 1.05f);
                                EnsureValidMaterials(bObj);
                            }
                        }
                    }
                    else
                    {
                        // Em tiles de floresta sem arvore alta: rica vegetacao rasteira, flora e monumentos celtas
                        float roll = Random.value;
                        if (fentoPrefab != null && roll < 0.20f)
                        {
                            inst = Instantiate(fentoPrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.85f, 1.35f);
                        }
                        else if (thornyBushPrefab != null && roll < 0.32f)
                        {
                            inst = Instantiate(thornyBushPrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.85f, 1.25f);
                        }
                        else if (fallenLogPrefab != null && roll < 0.42f)
                        {
                            inst = Instantiate(fallenLogPrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.9f, 1.3f);
                        }
                        else if (rockPrefab != null && roll < 0.52f)
                        {
                            inst = Instantiate(rockPrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = new Vector3(Random.Range(0.8f, 1.4f), Random.Range(0.6f, 1.3f), Random.Range(0.8f, 1.4f));
                        }
                        else if (mushroomPrefab != null && roll < 0.62f)
                        {
                            inst = Instantiate(mushroomPrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.8f, 1.3f);
                        }
                        else if (berryPrefab != null && roll < 0.70f)
                        {
                            inst = Instantiate(berryPrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.85f, 1.25f);
                        }
                        else if (glowMushroomPrefab != null && roll < 0.76f)
                        {
                            inst = Instantiate(glowMushroomPrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.9f, 1.3f);
                        }
                        else if (runicStonePrefab != null && roll < 0.81f)
                        {
                            inst = Instantiate(runicStonePrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.9f, 1.25f);
                        }
                        else if (celticCrossPrefab != null && roll < 0.85f)
                        {
                            inst = Instantiate(celticCrossPrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.9f, 1.25f);
                        }
                        else if (ancientGravePrefab != null && roll < 0.89f)
                        {
                            inst = Instantiate(ancientGravePrefab, pos, rot, environmentParent.transform);
                            inst.transform.localScale = Vector3.one * Random.Range(0.9f, 1.25f);
                        }
                    }
                }
                else if (tileID == 2 && ruinPrefab != null) 
                {
                    inst = Instantiate(ruinPrefab, pos, rot, environmentParent.transform);
                    inst.transform.localScale = Vector3.one * Random.Range(0.9f, 1.1f);
                }
                else if (tileID == 8 && rockPrefab != null)
                {
                    inst = Instantiate(rockPrefab, pos, rot, environmentParent.transform);
                    inst.transform.localScale = new Vector3(Random.Range(1.0f, 1.5f), Random.Range(0.8f, 1.5f), Random.Range(1.0f, 1.5f));
                }
                else if (tileID == 9 && berryPrefab != null)
                {
                    inst = Instantiate(berryPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 10 && mushroomPrefab != null)
                {
                    inst = Instantiate(mushroomPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 11 && pondPrefab != null)
                {
                    inst = Instantiate(pondPrefab, pos, rot, environmentParent.transform);
                    inst.transform.localScale = Vector3.one * Random.Range(1.0f, 2.0f);
                }
                else if (tileID == 12 && waterCatcherPrefab != null)
                {
                    inst = Instantiate(waterCatcherPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 13 && workbenchPrefab != null)
                {
                    inst = Instantiate(workbenchPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 14 && farmlandPrefab != null)
                {
                    inst = Instantiate(farmlandPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 15 && cauldronPrefab != null)
                {
                    inst = Instantiate(cauldronPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 16 && snareTrapPrefab != null)
                {
                    inst = Instantiate(snareTrapPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 17 && runicStonePrefab != null)
                {
                    inst = Instantiate(runicStonePrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 18 && fallenLogPrefab != null)
                {
                    inst = Instantiate(fallenLogPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 19 && thornyBushPrefab != null)
                {
                    inst = Instantiate(thornyBushPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 20 && glowMushroomPrefab != null)
                {
                    inst = Instantiate(glowMushroomPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 21 && celticCrossPrefab != null)
                {
                    inst = Instantiate(celticCrossPrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 22 && ancientGravePrefab != null)
                {
                    inst = Instantiate(ancientGravePrefab, pos, rot, environmentParent.transform);
                }
                else if (tileID == 0) // Clareiras e solo aberto florido / organico
                {
                    float openRoll = Random.value;
                    if (fentoPrefab != null && openRoll < 0.12f)
                    {
                        inst = Instantiate(fentoPrefab, pos, rot, environmentParent.transform);
                        inst.transform.localScale = Vector3.one * Random.Range(0.75f, 1.2f);
                    }
                    else if (rockPrefab != null && openRoll < 0.20f)
                    {
                        inst = Instantiate(rockPrefab, pos, rot, environmentParent.transform);
                        inst.transform.localScale = new Vector3(Random.Range(0.7f, 1.3f), Random.Range(0.5f, 1.1f), Random.Range(0.7f, 1.3f));
                    }
                    else if (thornyBushPrefab != null && openRoll < 0.28f)
                    {
                        inst = Instantiate(thornyBushPrefab, pos, rot, environmentParent.transform);
                        inst.transform.localScale = Vector3.one * Random.Range(0.75f, 1.15f);
                    }
                    else if (berryPrefab != null && openRoll < 0.35f)
                    {
                        inst = Instantiate(berryPrefab, pos, rot, environmentParent.transform);
                        inst.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
                    }
                    else if (fallenLogPrefab != null && openRoll < 0.40f)
                    {
                        inst = Instantiate(fallenLogPrefab, pos, rot, environmentParent.transform);
                        inst.transform.localScale = Vector3.one * Random.Range(0.85f, 1.2f);
                    }
                }

                if (inst != null)
                {
                    EnsureValidMaterials(inst);
                    activeInstances[new Vector2Int(x, y)] = inst;
                }
            }
        }

        if (terrainGenerator != null)
        {
            terrainGenerator.GenerateTerrain(mapDataPtr, mapWidth, mapHeight);
        }
        
        // Spawn Único: Farol Celta (Win Condition)
        if (farolCeltaPrefab != null)
        {
            Vector3 centerPos = new Vector3(50f, 0, 50f);
            centerPos.y = TerrainMeshGenerator.GetTerrainHeight(centerPos.x, centerPos.z);
            GameObject farol = Instantiate(farolCeltaPrefab, centerPos, Quaternion.identity, transform);
            EnsureValidMaterials(farol);
            activeInstances[new Vector2Int(50, 50)] = farol;
        }

        Debug.Log($"[MapRenderer] Mapa instanciado com sucesso estilo The Forest. {activeInstances.Count} objetos criados.");
    }

    private void Update()
    {
        // Instancias fisicas estaticas.
    }

    public void RemoveInstanceAt(int gridX, int gridY, int tileID)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        if (activeInstances.TryGetValue(key, out GameObject obj))
        {
            Destroy(obj);
            activeInstances.Remove(key);
            Debug.Log($"[MapRenderer] Objeto Destruido no grid ({gridX}, {gridY}).");
        }
    }

    public void AddInstanceAt(int gridX, int gridY, int tileID)
    {
        InstantiateTile(gridX, gridY, tileID);
    }

    public void InstantiateTile(int gridX, int gridY, int tileID)
    {
        Vector3 pos = new Vector3(gridX * tileSize, TerrainMeshGenerator.GetTerrainHeight(gridX * tileSize, gridY * tileSize), gridY * tileSize);
        Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        GameObject inst = null;

        if (tileID == 1 && treePrefab != null) 
        {
            GameObject chosenTree = treePrefab;
            float roll = Random.value;
            if (roll > 0.90f && deadTreePrefab != null) chosenTree = deadTreePrefab;
            else if (roll > 0.70f && pineTreePrefab != null) chosenTree = pineTreePrefab;

            if (chosenTree != null)
            {
                inst = Instantiate(chosenTree, pos, rot, transform);
                inst.SetActive(true);
                EnsureValidMaterials(inst, s_barkFallback, (chosenTree == pineTreePrefab) ? s_pineFallback : s_foliageFallback);
                if (fentoPrefab != null && Random.value < 0.45f)
                {
                    Vector3 fPos = pos + new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));
                    fPos.y = TerrainMeshGenerator.GetTerrainHeight(fPos.x, fPos.z);
                    GameObject fernObj = Instantiate(fentoPrefab, fPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0), inst.transform);
                    fernObj.transform.localScale = Vector3.one * Random.Range(0.75f, 1.2f);
                    EnsureValidMaterials(fernObj);
                }
            }
        }
        else if (tileID == 2 && ruinPrefab != null) inst = Instantiate(ruinPrefab, pos, rot, transform);
        else if (tileID == 8 && rockPrefab != null) inst = Instantiate(rockPrefab, pos, rot, transform);
        else if (tileID == 3 && barricadePrefab != null) inst = Instantiate(barricadePrefab, pos, rot, transform);
        else if (tileID == 4 && campfirePrefab != null) inst = Instantiate(campfirePrefab, pos, rot, transform);
        else if (tileID == 9 && berryPrefab != null) inst = Instantiate(berryPrefab, pos, rot, transform);
        else if (tileID == 10 && mushroomPrefab != null) inst = Instantiate(mushroomPrefab, pos, rot, transform);
        else if (tileID == 11 && pondPrefab != null) inst = Instantiate(pondPrefab, pos, rot, transform);
        else if (tileID == 12 && waterCatcherPrefab != null) inst = Instantiate(waterCatcherPrefab, pos, rot, transform);
        else if (tileID == 13 && workbenchPrefab != null) inst = Instantiate(workbenchPrefab, pos, rot, transform);
        else if (tileID == 14 && farmlandPrefab != null) inst = Instantiate(farmlandPrefab, pos, rot, transform);
        else if (tileID == 15 && cauldronPrefab != null) inst = Instantiate(cauldronPrefab, pos, rot, transform);
        else if (tileID == 16 && snareTrapPrefab != null) inst = Instantiate(snareTrapPrefab, pos, rot, transform);
        else if (tileID == 17 && runicStonePrefab != null) inst = Instantiate(runicStonePrefab, pos, rot, transform);
        else if (tileID == 18 && fallenLogPrefab != null) inst = Instantiate(fallenLogPrefab, pos, rot, transform);
        else if (tileID == 19 && thornyBushPrefab != null) inst = Instantiate(thornyBushPrefab, pos, rot, transform);
        else if (tileID == 20 && glowMushroomPrefab != null) inst = Instantiate(glowMushroomPrefab, pos, rot, transform);
        else if (tileID == 21 && celticCrossPrefab != null) inst = Instantiate(celticCrossPrefab, pos, rot, transform);
        else if (tileID == 22 && ancientGravePrefab != null) inst = Instantiate(ancientGravePrefab, pos, rot, transform);

        if (inst != null)
        {
            EnsureValidMaterials(inst);
            activeInstances[new Vector2Int(gridX, gridY)] = inst;
            Debug.Log($"[MapRenderer] Adicionada instancia ID {tileID} no grid ({gridX}, {gridY}).");
        }
    }
    
    private Dictionary<Vector2Int, int> treeHitCount = new Dictionary<Vector2Int, int>();

    public bool TryHarvestTreeAt(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        GameObject obj = null;

        // Se a chave exata nao existir, busca no raio de 2.2 metros
        if (!activeInstances.TryGetValue(key, out obj) || obj == null)
        {
            float closestDist = float.MaxValue;
            Vector2Int bestKey = key;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    Vector2Int testKey = new Vector2Int(gridX + dx, gridY + dy);
                    if (activeInstances.TryGetValue(testKey, out GameObject candidate) && candidate != null)
                    {
                        float d = Vector2.Distance(new Vector2(gridX, gridY), new Vector2(testKey.x, testKey.y));
                        if (d <= 2.5f && d < closestDist)
                        {
                            string lower = candidate.name.ToLower();
                            if (lower.Contains("tree") || lower.Contains("pine"))
                            {
                                closestDist = d;
                                bestKey = testKey;
                                obj = candidate;
                            }
                        }
                    }
                }
            }
            if (obj != null) key = bestKey;
        }

        if (obj != null)
        {
            if (!treeHitCount.ContainsKey(key)) treeHitCount[key] = 0;
            treeHitCount[key]++;
            
            // 1. Resposta tátil estilo The Forest: ScreenShake em cada golpe
            if (ScreenShakeSystem.Instance != null)
            {
                ScreenShakeSystem.Instance.AddTrauma(0.12f);
            }

            // 2. Animação de impacto na árvore (scale punch)
            obj.transform.localScale = Vector3.one * Random.Range(0.85f, 0.95f);

            // 3. Partículas de lascas de madeira no ponto de impacto
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayBuildPlaceVFX(obj.transform.position + Vector3.up * 1.2f);
            }
            
            if (treeHitCount[key] >= 3)
            {
                // Destruir árvore fisicamente (no C++ e no Unity)
                GameManager gm = FindAnyObjectByType<GameManager>();
                if (gm != null) gm.UpdateMapCellNative(key.x, key.y, 0); // Vira chão livre
                
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(key.x, 0.5f, key.y));
                if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.AddTrauma(0.38f);

                // Spawn de tora caída / madeira coletável
                GameObject woodDrop = Resources.Load<GameObject>("Prefabs/DroppedWood_Prefab");
                if (woodDrop == null) woodDrop = Resources.Load<GameObject>("Prefabs/FallenLog_Prefab");
                if (woodDrop != null)
                {
                    Vector3 dropPos = new Vector3(key.x, TerrainMeshGenerator.GetTerrainHeight(key.x, key.y) + 0.15f, key.y);
                    Instantiate(woodDrop, dropPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                }

                // Atualiza a relva localmente para refletir a clareira aberta
                ProceduralGrassSystem grass = FindAnyObjectByType<ProceduralGrassSystem>();
                if (grass != null) grass.RefreshGrassAt(key.x, key.y, 2.5f);

                Destroy(obj);
                activeInstances.Remove(key);
                treeHitCount.Remove(key);
                return true; // Abatida!
            }
        }
        return false;
    }
    
    private Dictionary<Vector2Int, int> rockHitCount = new Dictionary<Vector2Int, int>();

    public bool TryHarvestRockAt(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        GameObject obj = null;

        if (!activeInstances.TryGetValue(key, out obj) || obj == null)
        {
            float closestDist = float.MaxValue;
            Vector2Int bestKey = key;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    Vector2Int testKey = new Vector2Int(gridX + dx, gridY + dy);
                    if (activeInstances.TryGetValue(testKey, out GameObject candidate) && candidate != null)
                    {
                        float d = Vector2.Distance(new Vector2(gridX, gridY), new Vector2(testKey.x, testKey.y));
                        if (d <= 2.5f && d < closestDist)
                        {
                            string lower = candidate.name.ToLower();
                            if (lower.Contains("rock"))
                            {
                                closestDist = d;
                                bestKey = testKey;
                                obj = candidate;
                            }
                        }
                    }
                }
            }
            if (obj != null) key = bestKey;
        }

        if (obj != null)
        {
            if (!rockHitCount.ContainsKey(key)) rockHitCount[key] = 0;
            rockHitCount[key]++;
            
            if (ScreenShakeSystem.Instance != null)
            {
                ScreenShakeSystem.Instance.AddTrauma(0.15f);
            }
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayBuildPlaceVFX(obj.transform.position + Vector3.up * 0.8f);
            }

            obj.transform.localScale = Vector3.one * Random.Range(1.05f, 1.18f);
            
            if (rockHitCount[key] >= 5) // Pedras demoram 5 hits
            {
                GameManager gm = FindAnyObjectByType<GameManager>();
                if (gm != null) gm.UpdateMapCellNative(key.x, key.y, 0); 
                
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(key.x, 0.5f, key.y));
                if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.AddTrauma(0.45f);

                Destroy(obj);
                activeInstances.Remove(key);
                rockHitCount.Remove(key);
                return true;
            }
        }
        return false;
    }
    
    public int TryHarvestPlantAt(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        
        if (activeInstances.TryGetValue(key, out GameObject obj))
        {
            int tileID = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, gridX, gridY);
            
            if (tileID == 9 || tileID == 10) // 9 = Berry, 10 = Mushroom
            {
                GameManager gm = FindAnyObjectByType<GameManager>();
                if (gm != null) gm.UpdateMapCellNative(gridX, gridY, 0);
                
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(gridX, 0.5f, gridY));
                
                Destroy(obj);
                activeInstances.Remove(key);
                return tileID; // Devolve qual planta foi colhida
            }
        }
        return 0;
    }
}
