using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("Map Configurations (Ilha da Fraga 768x768m - 60 FPS Mobile/PC)")]
    public int mapWidth = 768;
    public int mapHeight = 768;
    public int mapSeed = 1913;
    public float pathfindingUpdateInterval = 0.25f; // 4 updates por segundo para 768x768m

    [Header("References")]
    public Transform playerTransform;
    public VultoManager vultoManager;
    public MapRenderer mapRenderer;
    public BuildingSystem buildingSystem;
    public ResourceManager resourceManager;
    public EntityEventSystem entityEventSystem;

    [Header("Procedural World Entities")]
    public GameObject companionPrefab;

    // Arrays Geridos no C#
    private int[] mapDataArray;
    private float[] flowFieldXArray;
    private float[] flowFieldYArray;
    private int[] intFieldArray;
    private int[] queueArray;

    // Handles para fixar (Pin) a memoria e obter Ponteiros Seguros para o C++
    private GCHandle mapDataHandle;
    private GCHandle flowFieldXHandle;
    private GCHandle flowFieldYHandle;
    private GCHandle intFieldHandle;
    private GCHandle queueHandle;

    [HideInInspector] public System.IntPtr mapDataPtr;
    [HideInInspector] public System.IntPtr flowFieldXPtr;
    [HideInInspector] public System.IntPtr flowFieldYPtr;
    private System.IntPtr intFieldPtr;
    private System.IntPtr queuePtr;

    private Coroutine flowFieldCoroutine;

    // Importacoes da Biblioteca Nativa (C++)
    [DllImport("FragaNative")]
    private static extern void GenerateForestMap(System.IntPtr outMapData, int width, int height, int seed);

    [DllImport("FragaNative")]
    private static extern void CalculateFlowField(
        System.IntPtr mapData, 
        System.IntPtr outFlowFieldX, 
        System.IntPtr outFlowFieldY, 
        System.IntPtr integrationField, 
        System.IntPtr bfsQueue, 
        int width, int height, int targetX, int targetY);

    [DllImport("FragaNative")]
    private static extern void UpdateMapCell(System.IntPtr mapData, int width, int height, int x, int y, int newTileID);

    private void Awake()
    {
        // Forca upgrade automatico de cenas antigas (< 768) para o novo mundo aberto 768x768m
        if (mapWidth < FragaWorldBlueprint.DefaultWidth) mapWidth = FragaWorldBlueprint.DefaultWidth;
        if (mapHeight < FragaWorldBlueprint.DefaultHeight) mapHeight = FragaWorldBlueprint.DefaultHeight;

        FragaWorldBlueprint.Initialize(mapSeed, mapWidth, mapHeight);

        int totalCells = mapWidth * mapHeight;

        // 1. Alocar a Memoria no C#
        mapDataArray = new int[totalCells];
        flowFieldXArray = new float[totalCells];
        flowFieldYArray = new float[totalCells];
        intFieldArray = new int[totalCells];
        queueArray = new int[totalCells];

        // 2. Fixar (Pin) a Memoria para que o Garbage Collector nao a mova
        mapDataHandle = GCHandle.Alloc(mapDataArray, GCHandleType.Pinned);
        flowFieldXHandle = GCHandle.Alloc(flowFieldXArray, GCHandleType.Pinned);
        flowFieldYHandle = GCHandle.Alloc(flowFieldYArray, GCHandleType.Pinned);
        intFieldHandle = GCHandle.Alloc(intFieldArray, GCHandleType.Pinned);
        queueHandle = GCHandle.Alloc(queueArray, GCHandleType.Pinned);

        mapDataPtr = mapDataHandle.AddrOfPinnedObject();
        flowFieldXPtr = flowFieldXHandle.AddrOfPinnedObject();
        flowFieldYPtr = flowFieldYHandle.AddrOfPinnedObject();
        intFieldPtr = intFieldHandle.AddrOfPinnedObject();
        queuePtr = queueHandle.AddrOfPinnedObject();
    }

    private void Start()
    {
        MainMenuManager menu = FindAnyObjectByType<MainMenuManager>();
        if (menu == null)
        {
            Debug.Log("[GameManager] Cena sem MainMenuManager detectada. Inicializando mundo automaticamente!");
            StartNewGame(mapSeed);
        }
    }

    public void ContinueGame()
    {
        SaveSystem saveSys = FindAnyObjectByType<SaveSystem>();
        if (saveSys != null && saveSys.HasSaveFile())
        {
            mapSeed = saveSys.PeekSavedSeed(mapSeed);
            FragaWorldBlueprint.Initialize(mapSeed, mapWidth, mapHeight);
            FragaWorldBlueprint.StampNativeMapGrid(mapDataPtr, mapWidth, mapHeight);
            saveSys.LoadGame();
        }
        else
        {
            Debug.LogWarning("[GameManager] SaveSystem ou ficheiro de save nao encontrado. Iniciando New Game.");
            StartNewGame(mapSeed);
            return;
        }

        InitializeSubSystems(preserveWorldState: true);

        if (playerTransform != null)
        {
            var pc = playerTransform.GetComponent<PlayerController>();
            if (pc != null) pc.SnapToGround();
        }
    }

    public void StartNewGame(int seed)
    {
        mapSeed = seed;
        FragaWorldBlueprint.Initialize(mapSeed, mapWidth, mapHeight);
        FragaWorldBlueprint.StampNativeMapGrid(mapDataPtr, mapWidth, mapHeight);
        Debug.Log($"[GameManager] Novo Mundo Aberto {mapWidth}x{mapHeight} gerado com a Seed: {mapSeed}");

        StorageChestInteractable.CloseModal();
        if (LogCarryingSystem.Instance != null)
        {
            LogCarryingSystem.Instance.ClearCarriedLogs();
        }
        BlueprintPlacementManager bpMgr = BlueprintPlacementManager.Instance
            ?? FindAnyObjectByType<BlueprintPlacementManager>(FindObjectsInactive.Include);
        if (bpMgr != null)
        {
            bpMgr.ClearAllPlacedStructures();
        }

        if (mapRenderer != null)
        {
            mapRenderer.ResetWorldState();
        }

        InventoryAdvanced inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null)
        {
            inv.ResetToStartingKit();
        }

        TutorialManager tut = FindAnyObjectByType<TutorialManager>();
        if (tut != null)
        {
            tut.SetStep(0);
        }

        Vector2 camp = FragaWorldBlueprint.CrashCampPos;
        if (playerTransform != null)
        {
            float spawnY = TerrainMeshGenerator.GetTerrainHeight(camp.x, camp.y) + 0.35f;
            playerTransform.position = new Vector3(camp.x, spawnY, camp.y);
            playerTransform.rotation = Quaternion.Euler(0f, 15f, 0f);
        }

        // Spawn de um companheiro proceduralmente perto das ruinas
        if (companionPrefab != null)
        {
            float compX = camp.x + 5f;
            float compZ = camp.y + 5f;
            float compY = TerrainMeshGenerator.GetTerrainHeight(compX, compZ) + 1.0f;
            Vector3 companionPos = new Vector3(compX, compY, compZ);
            Instantiate(companionPrefab, companionPos, Quaternion.identity);
            Debug.Log("[GameManager] Companheiro spawnado nas ruinas.");
        }

        InitializeSubSystems(preserveWorldState: false);

        if (playerTransform != null)
        {
            var pc = playerTransform.GetComponent<PlayerController>();
            if (pc != null) pc.SnapToGround();
        }
    }

    public int[] GetMapData()
    {
        int[] copy = new int[mapWidth * mapHeight];
        System.Array.Copy(mapDataArray, copy, mapDataArray.Length);
        return copy;
    }

    public void LoadMapData(int[] savedGrid, bool renderImmediately = false)
    {
        if (savedGrid.Length == mapWidth * mapHeight)
        {
            System.Array.Copy(savedGrid, mapDataArray, savedGrid.Length);
            if (renderImmediately && mapRenderer != null) mapRenderer.RenderInitialMap();
        }
    }

    private void InitializeSubSystems(bool preserveWorldState = false)
    {
        // 5. Inicializa os Sub-Sistemas injetando os ponteiros nativos
        if (buildingSystem != null) buildingSystem.Initialize(mapDataPtr, mapWidth, mapHeight);
        if (resourceManager != null) resourceManager.Initialize(mapDataPtr, mapWidth, mapHeight);
        if (entityEventSystem != null) entityEventSystem.Initialize(mapDataPtr, mapWidth, mapHeight);
        if (vultoManager != null) vultoManager.InitializeSystem(mapWidth, mapHeight, flowFieldXPtr, flowFieldYPtr);
        
        if (mapRenderer != null) 
        {
            mapRenderer.Initialize(mapDataPtr, mapWidth, mapHeight);
            mapRenderer.RenderInitialMap(preserveWorldState);
        }

        if (FindAnyObjectByType<SurvivalMapSystem>() == null)
        {
            var mapSys = gameObject.AddComponent<SurvivalMapSystem>();
            mapSys.playerTransform = playerTransform;
        }

        if (FindAnyObjectByType<SceneryLightingSystem>() == null)
        {
            var scLights = gameObject.AddComponent<SceneryLightingSystem>();
            scLights.mapRenderer = mapRenderer;
            scLights.dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        }
        else
        {
            var scLights = FindAnyObjectByType<SceneryLightingSystem>();
            if (scLights.mapRenderer == null) scLights.mapRenderer = mapRenderer;
        }

        if (FindAnyObjectByType<TutorialManager>() == null)
        {
            var tut = gameObject.AddComponent<TutorialManager>();
            tut.playerTransform = playerTransform;
            tut.inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
            tut.uiManager = FindAnyObjectByType<UIManager>();
        }

        FragaCoopSession.GetOrCreate();

        // FASE 2: Aplicacao da Metaprogressao
        ApplyMetaUpgrades();

        // Inicia ciclo de Flow Field (Pathfinding)
        if (flowFieldCoroutine != null) StopCoroutine(flowFieldCoroutine);
        flowFieldCoroutine = StartCoroutine(UpdateFlowFieldRoutine());
    }

    private void ApplyMetaUpgrades()
    {
        MetaData meta = MetaSaveSystem.LoadMeta();

        if (playerTransform != null)
        {
            // Bonus de Velocidade
            PlayerController pc = playerTransform.GetComponent<PlayerController>();
            if (pc != null) pc.speed += (meta.upgradeSpeedLevel * 0.3f);

            // Bonus de Lampiao e Sanidade
            SanitySystem sanity = playerTransform.GetComponent<SanitySystem>();
            if (sanity != null)
            {
                sanity.sanityDepletionRateDark -= (meta.upgradeSanityLevel * 0.1f);
                if (sanity.lampLight != null)
                {
                    sanity.lampLight.range += (meta.upgradeLampLevel * 1.5f);
                    sanity.lampLight.intensity += (meta.upgradeLampLevel * 0.5f);
                }
            }
            Debug.Log($"[GameManager] Upgrades Aplicados: Speed+{meta.upgradeSpeedLevel}, Lamp+{meta.upgradeLampLevel}, Sanity+{meta.upgradeSanityLevel}");
        }
    }

    // Optimizacao Mobile: Coroutine para desfasar calculo pesado da main thread / loop de renderizacao
    private IEnumerator UpdateFlowFieldRoutine()
    {
        yield return null;
        while (true)
        {
            if (playerTransform != null && mapDataPtr != System.IntPtr.Zero && !isGameOver)
            {
                int pX = Mathf.RoundToInt(playerTransform.position.x);
                int pY = Mathf.RoundToInt(playerTransform.position.z);

                // Chama C++ para atualizar a matriz do Flow Field de Vultos apontando para o jogador
                CalculateFlowField(mapDataPtr, flowFieldXPtr, flowFieldYPtr, intFieldPtr, queuePtr, mapWidth, mapHeight, pX, pY);
            }
            
            yield return new WaitForSeconds(pathfindingUpdateInterval);
        }
    }

    public void UpdateMapCellNative(int x, int y, int newTileID)
    {
        if (mapDataPtr != System.IntPtr.Zero)
        {
            UpdateMapCell(mapDataPtr, mapWidth, mapHeight, x, y, newTileID);
            if (mapRenderer != null)
            {
                mapRenderer.RemoveInstanceAt(x, y, 0); // Limpa objeto existente
                mapRenderer.AddInstanceAt(x, y, newTileID); // Adiciona novo objeto
            }
        }
    }

    private bool isGameOver = false;

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        
        Time.timeScale = 0f; // Pausa a simulacao
        Debug.Log("[GameManager] GAME OVER Ativado! A espera de decisao do jogador.");
        
        UIManager uiManager = FindAnyObjectByType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowDeathScreen();
        }
    }

    public void RevivePlayer()
    {
        isGameOver = false;
        Time.timeScale = 1f;
        
        if (playerTransform != null)
        {
            SanitySystem s = playerTransform.GetComponent<SanitySystem>();
            if (s != null)
            {
                s.sanity = 100f;
                s.oil = 100f;
            }
            PlayerStatus ps = playerTransform.GetComponent<PlayerStatus>();
            if (ps != null)
            {
                ps.Revive();
            }
        }

        // Renasce no amanhecer seguro (08:00 AM) para nao reaparecer no breu da noite
        DayNightCycle dnc = FindAnyObjectByType<DayNightCycle>();
        if (dnc != null)
        {
            dnc.currentTime = 8.0f;
        }

        // Limpa ameacas e Vultos imediatos num raio de 40m
        if (vultoManager != null && playerTransform != null)
        {
            vultoManager.ClearNearbyVultos(playerTransform.position, 40f);
        }

        // Resetar efeitos cinematograficos de morte
        PostProcessingManager ppManager = FindAnyObjectByType<PostProcessingManager>();
        if (ppManager != null) ppManager.ResetAllEffects();

        UIManager uiManager = FindAnyObjectByType<UIManager>();
        if (uiManager != null) uiManager.HideDeathScreen();

        Debug.Log("[GameManager] O Jogador ressuscitou e a simulacao continuou!");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveCurrentState(); // Essencial para mobile (quando a app vai para background)
    }

    private void OnApplicationQuit()
    {
        SaveCurrentState();
    }

    private void SaveCurrentState()
    {
        if (mapDataPtr == System.IntPtr.Zero || playerTransform == null) return;
        
        float pX = playerTransform.position.x;
        float pZ = playerTransform.position.z;
        
        float sanity = 100f;
        float oil = 100f;
        var s = playerTransform.GetComponent<SanitySystem>();
        if (s != null) { sanity = s.sanity; oil = s.oil; }

        // Guardar Inventario
        InventoryAdvanced inv = FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null)
        {
            for (int i = 0; i < inv.maxSlots; i++)
            {
                PlayerPrefs.SetInt($"Slot{i}_ID", inv.slots[i].itemId);
                PlayerPrefs.SetInt($"Slot{i}_Qty", inv.slots[i].quantity);
            }
        }

        SaveSystem saveSys = FindAnyObjectByType<SaveSystem>();
        if (saveSys != null)
        {
            saveSys.SaveGame();
        }
    }

    private void OnDestroy()
    {
        // Limpeza estrita para evitar Memory Leaks (Muito Importante)
        if (mapDataHandle.IsAllocated) mapDataHandle.Free();
        if (flowFieldXHandle.IsAllocated) flowFieldXHandle.Free();
        if (flowFieldYHandle.IsAllocated) flowFieldYHandle.Free();
        if (intFieldHandle.IsAllocated) intFieldHandle.Free();
        if (queueHandle.IsAllocated) queueHandle.Free();
    }
}
