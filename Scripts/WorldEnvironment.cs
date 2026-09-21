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
    public Mesh mushroomMesh;
    public Material mushroomMaterial;
    public Mesh berryMesh;
    public Material berryMaterial;

    [Header("Config")]
    public int maxHerbs = 40;
    public int maxMushrooms = 20;
    public int maxBerries = 30;
    public float collectRange = 2f;
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

    // Rendering
    private Matrix4x4[] herbMatrices;
    private Matrix4x4[] mushroomMatrices;
    private Matrix4x4[] berryMatrices;
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

        SpawnWorldItems();
    }

    private void SpawnWorldItems()
    {
        // Ervas: perto de arvores (zonas de sombra)
        for (int i = 0; i < maxHerbs; i++)
        {
            herbPositions[i] = new Vector3(Random.Range(5f, 95f), 0.1f, Random.Range(5f, 95f));
            herbActive[i] = true;
            herbMatrices[i] = Matrix4x4.TRS(herbPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.3f);
        }

        // Cogumelos: em zonas mais sombrias e humidas
        for (int i = 0; i < maxMushrooms; i++)
        {
            mushroomPositions[i] = new Vector3(Random.Range(5f, 95f), 0.05f, Random.Range(5f, 95f));
            mushroomActive[i] = true;
            mushroomMatrices[i] = Matrix4x4.TRS(mushroomPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.2f);
        }

        // Bagas: perto de clareiras
        for (int i = 0; i < maxBerries; i++)
        {
            berryPositions[i] = new Vector3(Random.Range(5f, 95f), 0.15f, Random.Range(5f, 95f));
            berryActive[i] = true;
            berryMatrices[i] = Matrix4x4.TRS(berryPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.25f);
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

        // Verificacao rapida nos 9 tiles adjacentes
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
            float distSq = (herbPositions[i] - pPos).sqrMagnitude;
            if (distSq < rangeSq)
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
            float distSq = (mushroomPositions[i] - pPos).sqrMagnitude;
            if (distSq < rangeSq)
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
            float distSq = (berryPositions[i] - pPos).sqrMagnitude;
            if (distSq < rangeSq)
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

        for (int i = 0; i < maxHerbs; i++)
        {
            if (herbActive[i]) continue;
            herbRespawnTimers[i] -= dt;
            if (herbRespawnTimers[i] <= 0f)
            {
                herbActive[i] = true;
                herbPositions[i] = new Vector3(Random.Range(5f, 95f), 0.1f, Random.Range(5f, 95f));
                herbMatrices[i] = Matrix4x4.TRS(herbPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.3f);
            }
        }

        for (int i = 0; i < maxMushrooms; i++)
        {
            if (mushroomActive[i]) continue;
            mushroomRespawnTimers[i] -= dt;
            if (mushroomRespawnTimers[i] <= 0f)
            {
                mushroomActive[i] = true;
                mushroomPositions[i] = new Vector3(Random.Range(5f, 95f), 0.05f, Random.Range(5f, 95f));
                mushroomMatrices[i] = Matrix4x4.TRS(mushroomPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.2f);
            }
        }

        for (int i = 0; i < maxBerries; i++)
        {
            if (berryActive[i]) continue;
            berryRespawnTimers[i] -= dt;
            if (berryRespawnTimers[i] <= 0f)
            {
                berryActive[i] = true;
                berryPositions[i] = new Vector3(Random.Range(5f, 95f), 0.15f, Random.Range(5f, 95f));
                berryMatrices[i] = Matrix4x4.TRS(berryPositions[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), Vector3.one * 0.25f);
            }
        }
    }

    private void RenderWorldItems()
    {
        // Renderizar apenas itens ativos
        int herbCount = 0;
        Matrix4x4[] activeHerbs = new Matrix4x4[maxHerbs];
        for (int i = 0; i < maxHerbs; i++)
        {
            if (herbActive[i]) activeHerbs[herbCount++] = herbMatrices[i];
        }
        if (herbCount > 0 && herbMesh != null && herbMaterial != null)
        {
            Graphics.DrawMeshInstanced(herbMesh, 0, herbMaterial, activeHerbs, herbCount);
        }

        int mushCount = 0;
        Matrix4x4[] activeMush = new Matrix4x4[maxMushrooms];
        for (int i = 0; i < maxMushrooms; i++)
        {
            if (mushroomActive[i]) activeMush[mushCount++] = mushroomMatrices[i];
        }
        if (mushCount > 0 && mushroomMesh != null && mushroomMaterial != null)
        {
            Graphics.DrawMeshInstanced(mushroomMesh, 0, mushroomMaterial, activeMush, mushCount);
        }

        int berryCount = 0;
        Matrix4x4[] activeBerries = new Matrix4x4[maxBerries];
        for (int i = 0; i < maxBerries; i++)
        {
            if (berryActive[i]) activeBerries[berryCount++] = berryMatrices[i];
        }
        if (berryCount > 0 && berryMesh != null && berryMaterial != null)
        {
            Graphics.DrawMeshInstanced(berryMesh, 0, berryMaterial, activeBerries, berryCount);
        }
    }
}
