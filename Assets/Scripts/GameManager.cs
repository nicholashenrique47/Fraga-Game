using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections;

public class GameManager : MonoBehaviour
{
    // C++ DLL Imports
    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern int InitFragaEngine();

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern void GenerateMapGrid(System.IntPtr mapData, int width, int height, int seed);

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern void CalculateFlowField(System.IntPtr mapData, System.IntPtr vectorX, System.IntPtr vectorY, System.IntPtr intField, System.IntPtr queue, int width, int height, int targetX, int targetY);

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UpdateMapCell(System.IntPtr mapData, int width, int height, int x, int y, int newTileID);

    [Header("Map Configuration")]
    public int mapWidth = 100;
    public int mapHeight = 100;
    public int mapSeed = 1913;
    public float pathfindingUpdateInterval = 0.2f; // Optimização Mobile: Pathfinding a cada 200ms em vez de todos os frames

    [Header("Player & Entities")]
    public Transform playerTransform;
    public GameObject companionPrefab;

    [Header("Sub-Systems")]
    public BuildingSystem buildingSystem;
    public ResourceManager resourceManager;
    public EntityEventSystem entityEventSystem;
    public VultoManager vultoManager;
    public MapRenderer mapRenderer; // Novo Sub-Sistema

    // Data-Oriented Memory Arrays
    private int[] mapDataArray;
    private float[] flowFieldXArray;
    private float[] flowFieldYArray;
    private ushort[] intFieldArray; // Novo: Previne alocacao nativa
    private int[] queueArray;       // Novo: Previne alocacao nativa

    // Pinned Pointers para zero-copy com C++
    private GCHandle mapDataHandle;
    private GCHandle flowFieldXHandle;
    private GCHandle flowFieldYHandle;
    private GCHandle intFieldHandle;
    private GCHandle queueHandle;

    public System.IntPtr mapDataPtr;
    private System.IntPtr flowFieldXPtr;
    private System.IntPtr flowFieldYPtr;
    private System.IntPtr intFieldPtr;
    private System.IntPtr queuePtr;

    private void Awake()
    {
        // 1. Inicializa a engine C++
        int engineVersion = InitFragaEngine();
        Debug.Log($"[FragaNative] Engine Inicializada. Versão/Check: {engineVersion}");

        // 2. Aloca Memória de forma contígua no C#
        int size = mapWidth * mapHeight;
        mapDataArray = new int[size];
        flowFieldXArray = new float[size];
        flowFieldYArray = new float[size];
        intFieldArray = new ushort[size];
        queueArray = new int[size];

        // 3. Fixa (Pin) a memória para garantir segurança nativa
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
        if (saveSys != null)
        {
            saveSys.LoadGame();
            // In the new SaveSystem, map is not saved, so we must regenerate it:
            GenerateMapGrid(mapDataPtr, mapWidth, mapHeight, mapSeed);
        }
        else
        {
            Debug.LogWarning("[GameManager] SaveSystem nao encontrado. Forcando New Game.");
            StartNewGame(mapSeed);
            return;
        }

        InitializeSubSystems();
    }

    public void StartNewGame(int seed)
    {
        mapSeed = seed;
        GenerateMapGrid(mapDataPtr, mapWidth, mapHeight, mapSeed);
        Debug.Log($"[GameManager] Novo Mundo (C++) gerado com a Seed: {mapSeed}");

        if (playerTransform != null)
        {
            float spawnY = TerrainMeshGenerator.GetTerrainHeight(mapWidth / 2f, mapHeight / 2f) + 1.0f;
            playerTransform.position = new Vector3(mapWidth / 2f, spawnY, mapHeight / 2f);
            var pc = playerTransform.GetComponent<PlayerController>();
            if (pc != null) pc.SnapToGround();
        }
        
        // Spawn de um companheiro proceduralmente perto das ruinas
        if (companionPrefab != null)
        {
            float compX = mapWidth / 2f + 5f;
            float compZ = mapHeight / 2f + 5f;
            float compY = TerrainMeshGenerator.GetTerrainHeight(compX, compZ) + 1.0f;
            Vector3 companionPos = new Vector3(compX, compY, compZ);
            Instantiate(companionPrefab, companionPos, Quaternion.identity);
            Debug.Log("[GameManager] Companheiro spawnado nas ruínas.");
        }

        InitializeSubSystems();
    }

    public int[] GetMapData()
    {
        int[] copy = new int[mapWidth * mapHeight];
        System.Array.Copy(mapDataArray, copy, mapDataArray.Length);
        return copy;
    }

    public void LoadMapData(int[] savedGrid)
    {
        if (savedGrid.Length == mapWidth * mapHeight)
        {
            System.Array.Copy(savedGrid, mapDataArray, savedGrid.Length);
            if (mapRenderer != null) mapRenderer.RenderInitialMap();
        }
    }

    private void InitializeSubSystems()
    {
        // 5. Inicializa os Sub-Sistemas injetando os ponteiros nativos
        if (buildingSystem != null) buildingSystem.Initialize(mapDataPtr, mapWidth, mapHeight);
        if (resourceManager != null) resourceManager.Initialize(mapDataPtr, mapWidth, mapHeight);
        if (entityEventSystem != null) entityEventSystem.Initialize(mapDataPtr, mapWidth, mapHeight);
        if (vultoManager != null) vultoManager.InitializeSystem(mapWidth, mapHeight, flowFieldXPtr, flowFieldYPtr);
        
        if (mapRenderer != null) 
        {
            mapRenderer.Initialize(mapDataPtr, mapWidth, mapHeight);
            mapRenderer.RenderInitialMap();
        }

        // FASE 2: Aplicação da Metaprocessão
        ApplyMetaUpgrades();

        // Inicia ciclo de Flow Field (Pathfinding)
        StartCoroutine(UpdateFlowFieldRoutine());
    }

    private void ApplyMetaUpgrades()
    {
        MetaData meta = MetaSaveSystem.LoadMeta();

        if (playerTransform != null)
        {
            // Bónus de Velocidade
            PlayerController pc = playerTransform.GetComponent<PlayerController>();
            if (pc != null) pc.speed += (meta.upgradeSpeedLevel * 0.3f);

            // Bónus de Lampião e Sanidade
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

    // Optimização Mobile: Coroutine para desfasar cálculo pesado da main thread / loop de renderização
    private IEnumerator UpdateFlowFieldRoutine()
    {
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
        
        Time.timeScale = 0f; // Pausa a simulação
        Debug.Log("[GameManager] GAME OVER Ativado! À espera de decisão do jogador.");
        
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

        // Resetar efeitos cinematográficos de morte
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
