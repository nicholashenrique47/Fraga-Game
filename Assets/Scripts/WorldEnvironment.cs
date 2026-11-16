using UnityEngine;

/// WorldEnvironment: Gere biomas, pontos de interesse e itens colecionaveis no mundo.
/// Spawna ervas, cogumelos, bagas e pedras no mapa com base nos IDs de tile.
/// Integra com o mapa nativo do C++ para manter coerencia espacial.
public class WorldEnvironment : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public DayNightCycle dayNightCycle;
    public PlayerStatus playerStatus;

    [Header("World Items (GPU Instanced)")]
    public Mesh herbMesh;
    public Material herbMaterial;
    public Material[] herbMaterials;
    public Mesh mushroomMesh;
    public Material mushroomMaterial;
    public Material[] mushroomMaterials;
    public Mesh berryMesh;
    public Material berryMaterial;
    public Material[] berryMaterials;

    [Header("Config")]
    public int maxHerbs = 90;
    public int maxMushrooms = 45;
    public int maxBerries = 70;
    public float collectRange = 2.2f;
    public float renderDistance = 75f;
    public float respawnTime = 120f; // Segundos para respawn de itens

    // Posicoes e estados dos colecionaveis
    private Vector3[] herbPositions;
    private Vector3[] mushroomPositions;
    private Vector3[] berryPositions;
    private float[] herbRespawnTimers;
    private float[] mushroomRespawnTimers;
    private float[] berryRespawnTimers;
    private bool[] herbActive;
    private bool[] mushroomActive;
    private bool[] berryActive;

    // Rendering (pre-allocated to avoid per-frame GC)
    private Matrix4x4[] herbMatrices;
    private Matrix4x4[] mushroomMatrices;
    private Matrix4x4[] berryMatrices;
    private Matrix4x4[] activeHerbsBuffer;
    private Matrix4x4[] activeMushBuffer;
    private Matrix4x4[] activeBerriesBuffer;
    private GameManager gameManagerCache;

    private void Awake()
    {
        gameManagerCache = FindAnyObjectByType<GameManager>();
        herbPositions = new Vector3[maxHerbs];
        mushroomPositions = new Vector3[maxMushrooms];
        berryPositions = new Vector3[maxBerries];

        herbRespawnTimers = new float[maxHerbs];
        mushroomRespawnTimers = new float[maxMushrooms];
        berryRespawnTimers = new float[maxBerries];

        herbActive = new bool[maxHerbs];
        mushroomActive = new bool[maxMushrooms];
        berryActive = new bool[maxBerries];

        herbMatrices = new Matrix4x4[maxHerbs];
        mushroomMatrices = new Matrix4x4[maxMushrooms];
        berryMatrices = new Matrix4x4[maxBerries];

        activeHerbsBuffer = new Matrix4x4[maxHerbs];
        activeMushBuffer = new Matrix4x4[maxMushrooms];
        activeBerriesBuffer = new Matrix4x4[maxBerries];

        EnsureItemVisualsLoaded();
        SpawnWorldItems();
    }

    private void Start()
    {
        if (gameManagerCache == null) gameManagerCache = FindAnyObjectByType<GameManager>();
        if (playerTransform == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) playerTransform = pc.transform;
        }
        if (playerStatus == null && playerTransform != null)
        {
            playerStatus = playerTransform.GetComponent<PlayerStatus>();
        }
        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
    }

    private void GetMapBounds(out float maxX, out float maxZ)
    {
        int mw = gameManagerCache != null ? gameManagerCache.mapWidth : FragaWorldBlueprint.DefaultWidth;
        int mh = gameManagerCache != null ? gameManagerCache.mapHeight : FragaWorldBlueprint.DefaultHeight;
        maxX = Mathf.Max(45f, mw - 35f);
        maxZ = Mathf.Max(45f, mh - 35f);
    }

    private Vector3 SampleGroundPos(float minX, float maxX, float minZ, float maxZ, float yOffset)
    {
        float x = FragaWorldBlueprint.CrashCampPos.x;
        float z = FragaWorldBlueprint.CrashCampPos.y;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float cx = Random.Range(Mathf.Max(35f, minX), maxX);
            float cz = Random.Range(Mathf.Max(35f, minZ), maxZ);
            if (FragaWorldBlueprint.IsInsideIsland(cx, cz) && FragaWorldBlueprint.GetWaterDepth(cx, cz) < 0.08f)
            {
                x = cx; z = cz; break;
            }
        }
        float y = TerrainMeshGenerator.GetTerrainHeight(x, z) + yOffset;
        return new Vector3(x, y, z);
    }

    private void EnsureItemVisualsLoaded()
    {
        TryLoadItemPrefab("Prefabs/Herb_Prefab", ref herbMesh, ref herbMaterial, ref herbMaterials);
        TryLoadItemPrefab("Prefabs/Mushroom_Prefab", ref mushroomMesh, ref mushroomMaterial, ref mushroomMaterials);
        TryLoadItemPrefab("Prefabs/Berry_Prefab", ref berryMesh, ref berryMaterial, ref berryMaterials);
    }

    private static void TryLoadItemPrefab(string prefabPath, ref Mesh mesh, ref Material singleMat, ref Material[] multiMats)
    {
        if (mesh != null && (multiMats != null && multiMats.Length > 0 || singleMat != null)) return;

        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab == null) return;

        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        Renderer rend = prefab.GetComponentInChildren<Renderer>();
        if (mf != null && mf.sharedMesh != null) mesh = mf.sharedMesh;
        if (rend != null)
        {
            if (rend.sharedMaterials != null && rend.sharedMaterials.Length > 0)
            {
                multiMats = rend.sharedMaterials;
                foreach (var m in multiMats)
                {
                    if (m != null) m.enableInstancing = true;
                }
            }
            if (rend.sharedMaterial != null)
            {
                singleMat = rend.sharedMaterial;
                singleMat.enableInstancing = true;
            }
        }
    }

    private void SpawnWorldItems()
    {
        GetMapBounds(out float maxX, out float maxZ);

        // Ervas: perto de arvores (zonas de sombra)
        for (int i = 0; i < maxHerbs; i++)
        {
            herbPositions[i] = SampleGroundPos(5f, maxX, 5f, maxZ, 0.08f);
            herbActive[i] = true;
            herbMatrices[i] = Matrix4x4.TRS(herbPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.65f);
        }

        // Cogumelos: em zonas mais sombrias e humidas
        for (int i = 0; i < maxMushrooms; i++)
        {
            mushroomPositions[i] = SampleGroundPos(5f, maxX, 5f, maxZ, 0.04f);
            mushroomActive[i] = true;
            mushroomMatrices[i] = Matrix4x4.TRS(mushroomPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.55f);
        }

        // Bagas: perto de clareiras
        for (int i = 0; i < maxBerries; i++)
        {
            berryPositions[i] = SampleGroundPos(5f, maxX, 5f, maxZ, 0.12f);
            berryActive[i] = true;
            berryMatrices[i] = Matrix4x4.TRS(berryPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.5f);
        }
    }

    private void Update()
    {
        UpdateRespawnTimers();
        RenderWorldItems();

        // Detecao de proximidade a fogueiras para o PlayerStatus
        if (playerStatus != null && playerTransform != null)
        {
            playerStatus.isNearBonfire = CheckNearBonfire();
        }
    }

    /// Verifica se o jogador esta perto de uma fogueira (tile ID 4).
    private bool CheckNearBonfire()
    {
        if (playerTransform == null) return false;

        if (gameManagerCache == null || gameManagerCache.mapDataPtr == System.IntPtr.Zero) return false;

        int px = Mathf.RoundToInt(playerTransform.position.x);
        int pz = Mathf.RoundToInt(playerTransform.position.z);

        // Verificacao rapida nos tiles adjacentes
        for (int y = pz - 2; y <= pz + 2; y++)
        {
            for (int x = px - 2; x <= px + 2; x++)
            {
                int tile = ResourceManager.GetMapCell(gameManagerCache.mapDataPtr, gameManagerCache.mapWidth, gameManagerCache.mapHeight, x, y);
                if (tile == 4) return true;
            }
        }
        return false;
    }

    /// Tenta coletar itens proximos. Chamado pelo botao Interact da UI.
    public void TryCollectNearby()
    {
        if (playerTransform == null || inventory == null) return;

        Vector3 pPos = playerTransform.position;
        float rangeSq = collectRange * collectRange;

        // Ervas
        for (int i = 0; i < maxHerbs; i++)
        {
            if (!herbActive[i]) continue;
            float dx = herbPositions[i].x - pPos.x;
            float dz = herbPositions[i].z - pPos.z;
            if (dx * dx + dz * dz < rangeSq)
            {
                inventory.AddItem(103, 1); // Ervas
                herbActive[i] = false;
                herbRespawnTimers[i] = respawnTime;
                Debug.Log("[World] Ervas coletadas.");
                return;
            }
        }

        // Cogumelos
        for (int i = 0; i < maxMushrooms; i++)
        {
            if (!mushroomActive[i]) continue;
            float dx = mushroomPositions[i].x - pPos.x;
            float dz = mushroomPositions[i].z - pPos.z;
            if (dx * dx + dz * dz < rangeSq)
            {
                inventory.AddItem(203, 1); // Cogumelo Estranho
                mushroomActive[i] = false;
                mushroomRespawnTimers[i] = respawnTime * 2f; // Cogumelos sao mais raros
                Debug.Log("[World] Cogumelo estranho coletado.");
                return;
            }
        }

        // Bagas
        for (int i = 0; i < maxBerries; i++)
        {
            if (!berryActive[i]) continue;
            float dx = berryPositions[i].x - pPos.x;
            float dz = berryPositions[i].z - pPos.z;
            if (dx * dx + dz * dz < rangeSq)
            {
                inventory.AddItem(202, 2); // 2x Bagas Silvestres
                berryActive[i] = false;
                berryRespawnTimers[i] = respawnTime;
                Debug.Log("[World] Bagas coletadas.");
                return;
            }
        }
    }

    private void UpdateRespawnTimers()
    {
        float dt = Time.deltaTime;
        GetMapBounds(out float maxX, out float maxZ);

        for (int i = 0; i < maxHerbs; i++)
        {
            if (herbActive[i]) continue;
            herbRespawnTimers[i] -= dt;
            if (herbRespawnTimers[i] <= 0f)
            {
                herbActive[i] = true;
                herbPositions[i] = SampleGroundPos(5f, maxX, 5f, maxZ, 0.08f);
                herbMatrices[i] = Matrix4x4.TRS(herbPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.65f);
            }
        }

        for (int i = 0; i < maxMushrooms; i++)
        {
            if (mushroomActive[i]) continue;
            mushroomRespawnTimers[i] -= dt;
            if (mushroomRespawnTimers[i] <= 0f)
            {
                mushroomActive[i] = true;
                mushroomPositions[i] = SampleGroundPos(5f, maxX, 5f, maxZ, 0.04f);
                mushroomMatrices[i] = Matrix4x4.TRS(mushroomPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.55f);
            }
        }

        for (int i = 0; i < maxBerries; i++)
        {
            if (berryActive[i]) continue;
            berryRespawnTimers[i] -= dt;
            if (berryRespawnTimers[i] <= 0f)
            {
                berryActive[i] = true;
                berryPositions[i] = SampleGroundPos(5f, maxX, 5f, maxZ, 0.12f);
                berryMatrices[i] = Matrix4x4.TRS(berryPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.5f);
            }
        }
    }

    private static void DrawInstancedMultiSubmesh(Mesh mesh, Material fallbackMat, Material[] subMats, Matrix4x4[] matrices, int count)
    {
        if (count <= 0 || mesh == null) return;
        int drawCount = Mathf.Min(count, 1023);
        int subCount = Mathf.Max(1, mesh.subMeshCount);
        for (int s = 0; s < subCount; s++)
        {
            Material m = (subMats != null && s < subMats.Length && subMats[s] != null) ? subMats[s] : fallbackMat;
            if (m != null)
            {
                if (!m.enableInstancing) m.enableInstancing = true;
                Graphics.DrawMeshInstanced(mesh, s, m, matrices, drawCount);
            }
        }
    }

    private void RenderWorldItems()
    {
        bool hasPlayer = playerTransform != null;
        Vector3 pPos = hasPlayer ? playerTransform.position : Vector3.zero;
        float cullDistSq = renderDistance * renderDistance;

        int herbCount = 0;
        for (int i = 0; i < maxHerbs; i++)
        {
            if (!herbActive[i]) continue;
            if (hasPlayer)
            {
                float dx = herbPositions[i].x - pPos.x;
                float dz = herbPositions[i].z - pPos.z;
                if (dx * dx + dz * dz > cullDistSq) continue;
            }
            activeHerbsBuffer[herbCount++] = herbMatrices[i];
        }
        DrawInstancedMultiSubmesh(herbMesh, herbMaterial, herbMaterials, activeHerbsBuffer, herbCount);

        int mushCount = 0;
        for (int i = 0; i < maxMushrooms; i++)
        {
            if (!mushroomActive[i]) continue;
            if (hasPlayer)
            {
                float dx = mushroomPositions[i].x - pPos.x;
                float dz = mushroomPositions[i].z - pPos.z;
                if (dx * dx + dz * dz > cullDistSq) continue;
            }
            activeMushBuffer[mushCount++] = mushroomMatrices[i];
        }
        DrawInstancedMultiSubmesh(mushroomMesh, mushroomMaterial, mushroomMaterials, activeMushBuffer, mushCount);

        int berryCount = 0;
        for (int i = 0; i < maxBerries; i++)
        {
            if (!berryActive[i]) continue;
            if (hasPlayer)
            {
                float dx = berryPositions[i].x - pPos.x;
                float dz = berryPositions[i].z - pPos.z;
                if (dx * dx + dz * dz > cullDistSq) continue;
            }
            activeBerriesBuffer[berryCount++] = berryMatrices[i];
        }
        DrawInstancedMultiSubmesh(berryMesh, berryMaterial, berryMaterials, activeBerriesBuffer, berryCount);
    }
}
