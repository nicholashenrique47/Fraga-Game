using UnityEngine;
using System.Collections.Generic;

public class MapRenderer : MonoBehaviour
{
    [Header("Map Visuals (Prefabs)")]
    public GameObject treePrefab;
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
    }

    public void RenderInitialMap()
    {
        if (mapDataPtr == System.IntPtr.Zero || treePrefab == null)
        {
            Debug.LogError("[MapRenderer] Prefabs de mapa nao configurados.");
            return;
        }

        // Cleanup previously rendered objects for reload support
        foreach(var kvp in activeInstances)
        {
            if(kvp.Value != null) Destroy(kvp.Value);
        }
        activeInstances.Clear();

        GameObject environmentParent = GameObject.Find("Environment_Instances");
        if (environmentParent == null) environmentParent = new GameObject("Environment_Instances");

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int tileID = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, x, y);
                Vector3 pos = new Vector3(x * tileSize, GetTerrainHeight(x * tileSize, y * tileSize), y * tileSize);
                Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                
                GameObject inst = null;

                if (tileID == 1 && treePrefab != null) 
                {
                    inst = Instantiate(treePrefab, pos, rot, environmentParent.transform);
                    inst.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
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

                if (inst != null)
                {
                    activeInstances[new Vector2Int(x, y)] = inst;
                }
            }
        }

        if (terrainGenerator != null)
        {
            terrainGenerator.GenerateTerrain(mapDataPtr, mapWidth, mapHeight);
        }
        
        // Spawn ÃƒÆ’Ã…Â¡nico: Farol Celta (Win Condition)
        if (farolCeltaPrefab != null)
        {
            Vector3 centerPos = new Vector3(50f, 0, 50f);
            GameObject farol = Instantiate(farolCeltaPrefab, centerPos, Quaternion.identity, transform);
            activeInstances[new Vector2Int(50, 50)] = farol;
        }

        Debug.Log($"[MapRenderer] Mapa instanciado com sucesso. {activeInstances.Count} objetos criados.");
    }

    private void Update()
    {
        // Agora usamos GameObjects instanciados estaticos. Nao precisamos de DrawMeshInstanced no Update.
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
        Vector3 pos = new Vector3(gridX * tileSize, GetTerrainHeight(gridX * tileSize, gridY * tileSize), gridY * tileSize);
        Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        GameObject inst = null;

        if (tileID == 1 && treePrefab != null) inst = Instantiate(treePrefab, pos, rot, transform);
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
            activeInstances[new Vector2Int(gridX, gridY)] = inst;
            Debug.Log($"[MapRenderer] Adicionada instancia ID {tileID} no grid ({gridX}, {gridY}).");
        }
    }
    
    private Dictionary<Vector2Int, int> treeHitCount = new Dictionary<Vector2Int, int>();

    public bool TryHarvestTreeAt(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        
        // Verifica se hÃƒÆ’Ã‚Â¡ algo instanciado nessa grid
        if (activeInstances.TryGetValue(key, out GameObject obj))
        {
            // Precisamos verificar o ID pela DLL, ou assumir que e ÃƒÆ’Ã‚Â¡rvore se tiver tag "Tree"
            int tileID = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, gridX, gridY);
            
            if (tileID == 1) // ÃƒÆ’Ã¢â‚¬Â° ÃƒÆ’Ã‚Â¡rvore
            {
                if (!treeHitCount.ContainsKey(key)) treeHitCount[key] = 0;
                treeHitCount[key]++;
                
                // AnimaÃƒÆ’Ã‚Â§ÃƒÆ’Ã‚Â£o/efeito bÃƒÆ’Ã‚Â¡sico de bater na ÃƒÆ’Ã‚Â¡rvore (scale punch)
                if (obj != null)
                {
                    obj.transform.localScale = Vector3.one * Random.Range(0.7f, 0.9f);
                }
                
                if (treeHitCount[key] >= 3)
                {
                    // Destruir ÃƒÆ’Ã‚Â¡rvore fisicamente (no C++ e no Unity)
                    GameManager gm = FindAnyObjectByType<GameManager>();
                    if (gm != null) gm.UpdateMapCellNative(gridX, gridY, 0); // Vira chao livre
                    
                    if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(gridX, 0.5f, gridY));
                    
                    treeHitCount.Remove(key);
                    return true; // Abatida!
                }
            }
        }
        return false;
    }
    
    private Dictionary<Vector2Int, int> rockHitCount = new Dictionary<Vector2Int, int>();

    public bool TryHarvestRockAt(int gridX, int gridY)
    {
        Vector2Int key = new Vector2Int(gridX, gridY);
        
        if (activeInstances.TryGetValue(key, out GameObject obj))
        {
            int tileID = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, gridX, gridY);
            
            if (tileID == 8) // ÃƒÆ’Ã¢â‚¬Â° rocha
            {
                if (!rockHitCount.ContainsKey(key)) rockHitCount[key] = 0;
                rockHitCount[key]++;
                
                if (obj != null)
                {
                    obj.transform.localScale = Vector3.one * Random.Range(1.0f, 1.2f);
                }
                
                if (rockHitCount[key] >= 5) // Pedras demoram 5 hits
                {
                    GameManager gm = FindAnyObjectByType<GameManager>();
                    if (gm != null) gm.UpdateMapCellNative(gridX, gridY, 0); 
                    
                    if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(gridX, 0.5f, gridY));
                    
                    rockHitCount.Remove(key);
                    return true;
                }
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
                
                return tileID; // Devolve qual planta foi colhida
            }
        }
        return 0;
    }
}
