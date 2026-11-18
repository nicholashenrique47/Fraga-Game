using UnityEngine;

/// ProceduralGrassSystem: Relva volumétrica 3D estilo The Forest via GPU Instancing.
/// Gera tufos estrela multi-face com curvatura natural, AO de raiz, textura de capim real
/// (Mat_Grass_Tuft), streaming contínuo em mapas grandes (220x220+) e calibração PC vs Mobile 60 FPS.
public class ProceduralGrassSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerTransform;
    public WeatherSystem weatherSystem;

    [Header("Grass Config")]
    public bool enableGrass = true;
    public int grassDensity = 4800;  // Escalado automaticamente para PC vs Mobile no Awake
    public float cullDistance = 45f;
    public float grassHeight = 0.58f;
    public float grassWidth = 0.52f;

    [Header("Visual Colors")]
    public Color grassColorDark = new Color(0.86f, 0.94f, 0.80f);
    public Color grassColorLight = new Color(0.95f, 1.0f, 0.88f);

    // Runtime
    private Mesh grassMesh;
    private Material grassMaterial;
    private Vector3[] grassPositions;
    private Matrix4x4[] grassMatrices;
    private int mapWidth = FragaWorldBlueprint.DefaultWidth;
    private int mapHeight = FragaWorldBlueprint.DefaultHeight;
    private Vector3 lastPlayerPos;
    private bool hasLastPlayerPos = false;

    // Até 10 lotes de 1023 instâncias (10.230 instâncias máx no PC; 2 lotes no Mobile)
    private Matrix4x4[][] batchMatrices;
    private int[] batchCounts;
    private int activeMaxBatches = 2;
    private const int MAX_BATCHES_PC = 10;
    private const int MAX_BATCHES_MOBILE = 2;
    private const int BATCH_SIZE = 1023;
    private static readonly int FragaPlayerPosID = Shader.PropertyToID("_FragaPlayerPos");

    private void Awake()
    {
        bool isMobile = Application.isMobilePlatform;
        activeMaxBatches = isMobile ? MAX_BATCHES_MOBILE : MAX_BATCHES_PC;

        if (!isMobile)
        {
            if (grassDensity <= 4800) grassDensity = 9600;
            if (cullDistance < 60f) cullDistance = 66f;
        }
        else
        {
            grassDensity = Mathf.Min(grassDensity, 2000);
            cullDistance = Mathf.Min(cullDistance, 28f);
        }

        RefreshMapBounds();
        CreateGrassMesh();
        CreateGrassMaterial();
        GenerateGrassPositions();

        batchMatrices = new Matrix4x4[activeMaxBatches][];
        for (int b = 0; b < activeMaxBatches; b++)
        {
            batchMatrices[b] = new Matrix4x4[BATCH_SIZE];
        }
        batchCounts = new int[activeMaxBatches];
    }

    private void Start()
    {
        RefreshMapBounds();
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            else
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
        }
        if (playerTransform != null)
        {
            lastPlayerPos = playerTransform.position;
            hasLastPlayerPos = true;
        }
    }

    private void RefreshMapBounds()
    {
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            mapWidth = Mathf.Max(32, gm.mapWidth);
            mapHeight = Mathf.Max(32, gm.mapHeight);
        }
    }

    /// Cria uma malha 3D volumétrica de Tufo Estrela (3 quads entrecruzados a 0°, 60°, 120°)
    /// com curvatura natural e Vertex Color: R = Wind, G = Phase, B = Root-to-Tip AO.
    /// O shader de folhagem já usa Cull Off + VFACE, portanto geramos apenas 1 face por quad (zero 4x overdraw!).
    private void CreateGrassMesh()
    {
        grassMesh = new Mesh();
        grassMesh.name = "TheForest_Grass_Tuft";

        float h = grassHeight;
        float w = grassWidth;

        int numQuads = 3;
        int vertsPerQuad = 6;
        int trisPerQuad = 4;

        Vector3[] vertices = new Vector3[numQuads * vertsPerQuad];
        Vector2[] uvs = new Vector2[numQuads * vertsPerQuad];
        Color[] colors = new Color[numQuads * vertsPerQuad];
        Vector3[] normals = new Vector3[numQuads * vertsPerQuad];
        int[] triangles = new int[numQuads * trisPerQuad * 3];

        int vIndex = 0;
        int tIndex = 0;

        for (int q = 0; q < numQuads; q++)
        {
            float angle = q * (180f / numQuads) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 norm = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            float phase = (q + 0.5f) / numQuads;

            int baseV = vIndex;

            // Base (raiz com AO forte e zero vento)
            vertices[vIndex + 0] = -dir * (w * 0.5f) + Vector3.down * 0.03f;
            uvs[vIndex + 0] = new Vector2(0f, 0f);
            colors[vIndex + 0] = new Color(0f, phase, 0.36f, 1f);
            normals[vIndex + 0] = Vector3.up;

            vertices[vIndex + 1] = dir * (w * 0.5f) + Vector3.down * 0.03f;
            uvs[vIndex + 1] = new Vector2(1f, 0f);
            colors[vIndex + 1] = new Color(0f, phase, 0.36f, 1f);
            normals[vIndex + 1] = Vector3.up;

            // Meio arqueado
            vertices[vIndex + 2] = -dir * (w * 0.58f) + norm * 0.05f + Vector3.up * (h * 0.52f);
            uvs[vIndex + 2] = new Vector2(0f, 0.5f);
            colors[vIndex + 2] = new Color(0.45f, phase, 0.78f, 1f);
            normals[vIndex + 2] = (Vector3.up * 0.85f + norm * 0.25f).normalized;

            vertices[vIndex + 3] = dir * (w * 0.58f) - norm * 0.05f + Vector3.up * (h * 0.52f);
            uvs[vIndex + 3] = new Vector2(1f, 0.5f);
            colors[vIndex + 3] = new Color(0.45f, phase, 0.78f, 1f);
            normals[vIndex + 3] = (Vector3.up * 0.85f - norm * 0.25f).normalized;

            // Ápice com vento máximo e iluminação cheia
            vertices[vIndex + 4] = -dir * (w * 0.42f) + norm * 0.11f + Vector3.up * h;
            uvs[vIndex + 4] = new Vector2(0.08f, 1f);
            colors[vIndex + 4] = new Color(1.0f, phase, 1.0f, 1f);
            normals[vIndex + 4] = Vector3.up;

            vertices[vIndex + 5] = dir * (w * 0.42f) - norm * 0.11f + Vector3.up * h;
            uvs[vIndex + 5] = new Vector2(0.92f, 1f);
            colors[vIndex + 5] = new Color(1.0f, phase, 1.0f, 1f);
            normals[vIndex + 5] = Vector3.up;

            // Face única (Cull Off no shader desenha frente e verso sem duplicar geometria)
            triangles[tIndex++] = baseV + 0; triangles[tIndex++] = baseV + 2; triangles[tIndex++] = baseV + 1;
            triangles[tIndex++] = baseV + 1; triangles[tIndex++] = baseV + 2; triangles[tIndex++] = baseV + 3;
            triangles[tIndex++] = baseV + 2; triangles[tIndex++] = baseV + 4; triangles[tIndex++] = baseV + 3;
            triangles[tIndex++] = baseV + 3; triangles[tIndex++] = baseV + 4; triangles[tIndex++] = baseV + 5;

            vIndex += vertsPerQuad;
        }

        grassMesh.vertices = vertices;
        grassMesh.uv = uvs;
        grassMesh.colors = colors;
        grassMesh.normals = normals;
        grassMesh.triangles = triangles;
        grassMesh.RecalculateBounds();
        grassMesh.RecalculateTangents();
    }

    private void CreateGrassMaterial()
    {
        Material grassRef = Resources.Load<Material>("Materials/Mat_Grass_Tuft");
        if (grassRef != null)
        {
            grassMaterial = new Material(grassRef);
        }
        else
        {
            Shader foliageShader = Shader.Find("Fraga/Foliage_TheForest_SSS");
            if (foliageShader == null) foliageShader = Shader.Find("Fraga/FoliageSway_Instanced");

            if (foliageShader != null)
            {
                grassMaterial = new Material(foliageShader);
                Material foliageRef = Resources.Load<Material>("Materials/Mat_Fern_Frond")
                    ?? Resources.Load<Material>("Materials/Mat_OakFoliage");
                if (foliageRef != null && foliageRef.HasProperty("_BaseMap") && foliageRef.GetTexture("_BaseMap") != null)
                {
                    grassMaterial.SetTexture("_BaseMap", foliageRef.GetTexture("_BaseMap"));
                }
                if (foliageRef != null && foliageRef.HasProperty("_BumpMap") && foliageRef.GetTexture("_BumpMap") != null)
                {
                    grassMaterial.SetTexture("_BumpMap", foliageRef.GetTexture("_BumpMap"));
                }
                grassMaterial.SetColor("_BaseColor", grassColorDark);
                grassMaterial.SetFloat("_Cutoff", 0.28f);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                grassMaterial = new Material(shader);
                grassMaterial.SetColor("_BaseColor", grassColorDark);
                grassMaterial.SetFloat("_AlphaClip", 1f);
                grassMaterial.SetFloat("_Cutoff", 0.30f);
                grassMaterial.EnableKeyword("_ALPHATEST_ON");
                grassMaterial.SetOverrideTag("RenderType", "TransparentCutout");
                grassMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
        }

        grassMaterial.enableInstancing = true;
        if (grassMaterial.HasProperty("_Cull")) grassMaterial.SetFloat("_Cull", 0f);
        if (grassMaterial.HasProperty("_Smoothness")) grassMaterial.SetFloat("_Smoothness", 0.22f);
    }

    private void PlaceTuftAround(int idx, float centerX, float centerZ, float minRadius, float maxRadius)
    {
        float ang = Random.Range(0f, Mathf.PI * 2f);
        float rad = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt(Random.value));
        float x = Mathf.Clamp(centerX + Mathf.Cos(ang) * rad, 3f, Mathf.Max(4f, mapWidth - 3f));
        float z = Mathf.Clamp(centerZ + Mathf.Sin(ang) * rad, 3f, Mathf.Max(4f, mapHeight - 3f));
        float y = TerrainMeshGenerator.GetTerrainHeight(x, z);

        // Evita relva submersa no oceano, lago, rio ou no pico nevado
        float waterDepth = FragaWorldBlueprint.GetWaterDepth(x, z);
        float coast = FragaWorldBlueprint.GetIslandCoastMetric(x, z);
        if (waterDepth > 0.05f || coast >= 0.94f || y > 24.5f)
        {
            grassPositions[idx] = new Vector3(x, -999f, z);
            grassMatrices[idx] = Matrix4x4.zero;
            return;
        }

        grassPositions[idx] = new Vector3(x, y, z);

        float scaleXZ = Random.Range(0.85f, 1.50f);
        float scaleY = Random.Range(0.78f, 1.38f);
        float rot = Random.Range(0f, 360f);

        grassMatrices[idx] = Matrix4x4.TRS(
            grassPositions[idx],
            Quaternion.Euler(Random.Range(-4f, 4f), rot, Random.Range(-4f, 4f)),
            new Vector3(scaleXZ, scaleY, scaleXZ)
        );
    }

    private void GenerateGrassPositions()
    {
        grassPositions = new Vector3[grassDensity];
        grassMatrices = new Matrix4x4[grassDensity];

        float centerX = mapWidth * 0.5f;
        float centerZ = mapHeight * 0.5f;

        for (int i = 0; i < grassDensity; i++)
        {
            PlaceTuftAround(i, centerX, centerZ, 0f, cullDistance);
        }
    }

    private void Update()
    {
        if (!enableGrass) return;

        if (playerTransform == null)
        {
            if (PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
            else return;
        }

        Shader.SetGlobalVector(FragaPlayerPosID, playerTransform.position);

        if (weatherSystem != null && grassMaterial != null)
        {
            if (grassMaterial.HasProperty("_WindSpeed"))
                grassMaterial.SetFloat("_WindSpeed", 1.2f + weatherSystem.currentWindSpeed * 0.4f);
            if (grassMaterial.HasProperty("_WindStrength"))
                grassMaterial.SetFloat("_WindStrength", 0.12f + weatherSystem.currentWindSpeed * 0.05f);
            if (grassMaterial.HasProperty("_GlobalWindSpeed"))
                grassMaterial.SetFloat("_GlobalWindSpeed", weatherSystem.currentWindSpeed);
        }

        RenderVisibleGrass();
    }

    private void RenderVisibleGrass()
    {
        if (!enableGrass) return;
        if (grassMesh == null || grassMaterial == null || playerTransform == null) return;
        if (grassMaterial.shader == null || grassMaterial.shader.name.Contains("Error") || grassMaterial.shader.name == "Hidden/InternalErrorShader") return;

        Vector3 playerPos = playerTransform.position;
        float cullDistSq = cullDistance * cullDistance;

        bool isTeleport = !hasLastPlayerPos || (playerPos - lastPlayerPos).sqrMagnitude > cullDistSq * 2.25f;
        int defaultQuota = Application.isMobilePlatform ? 160 : 360;
        int recycleQuota = isTeleport ? grassDensity : defaultQuota;
        float minRecycleRadius = isTeleport ? 0f : cullDistance * 0.55f;
        lastPlayerPos = playerPos;
        hasLastPlayerPos = true;

        for (int b = 0; b < activeMaxBatches; b++)
        {
            batchCounts[b] = 0;
        }

        int currentBatch = 0;

        for (int i = 0; i < grassDensity; i++)
        {
            float dx = grassPositions[i].x - playerPos.x;
            float dz = grassPositions[i].z - playerPos.z;
            float distSq = dx * dx + dz * dz;

            if (distSq >= cullDistSq && recycleQuota > 0)
            {
                PlaceTuftAround(i, playerPos.x, playerPos.z, minRecycleRadius, cullDistance * 0.98f);
                recycleQuota--;
                dx = grassPositions[i].x - playerPos.x;
                dz = grassPositions[i].z - playerPos.z;
                distSq = dx * dx + dz * dz;
            }

            if (distSq < cullDistSq && grassPositions[i].y > -50f)
            {
                if (batchCounts[currentBatch] >= BATCH_SIZE)
                {
                    currentBatch++;
                    if (currentBatch >= activeMaxBatches) break;
                }

                batchMatrices[currentBatch][batchCounts[currentBatch]++] = grassMatrices[i];
            }
        }

        for (int b = 0; b <= currentBatch && b < activeMaxBatches; b++)
        {
            if (batchCounts[b] > 0)
            {
                Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, batchMatrices[b], batchCounts[b],
                    null, UnityEngine.Rendering.ShadowCastingMode.Off, true);
            }
        }
    }

    public void RefreshGrassAt(float worldX, float worldZ, float radius)
    {
        float radiusSq = radius * radius;
        float cx = playerTransform != null ? playerTransform.position.x : mapWidth * 0.5f;
        float cz = playerTransform != null ? playerTransform.position.z : mapHeight * 0.5f;

        for (int i = 0; i < grassDensity; i++)
        {
            float dx = grassPositions[i].x - worldX;
            float dz = grassPositions[i].z - worldZ;
            if (dx * dx + dz * dz < radiusSq)
            {
                PlaceTuftAround(i, cx, cz, Mathf.Max(radius + 2f, cullDistance * 0.4f), cullDistance * 0.98f);
            }
        }
    }
}
