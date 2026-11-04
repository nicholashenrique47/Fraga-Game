using UnityEngine;

/// ProceduralGrassSystem: Relva volumétrica 3D estilo The Forest via GPU Instancing.
/// Gera tufos estrela multi-face com curvatura natural e sway sincronizado ao vento.
/// Culling esférico otimizado para performance mobile estável a 60 FPS.
public class ProceduralGrassSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerTransform;
    public WeatherSystem weatherSystem;

    [Header("Grass Config")]
    public bool enableGrass = true;
    public int grassDensity = 4800;  // Cobertura completa do mapa de 100x100m
    public float cullDistance = 45f; // Distância de culling para 60 FPS mobile
    public float grassHeight = 0.54f;
    public float grassWidth = 0.46f;

    [Header("Visual Colors")]
    public Color grassColorDark = new Color(0.20f, 0.42f, 0.15f);
    public Color grassColorLight = new Color(0.32f, 0.62f, 0.22f);

    // Runtime
    private Mesh grassMesh;
    private Material grassMaterial;
    private Vector3[] grassPositions;
    private Matrix4x4[] grassMatrices;
    
    // Suporte a até 5 lotes de 1023 instâncias (5115 instâncias máx sem GC alloc)
    private Matrix4x4[][] batchMatrices;
    private int[] batchCounts;
    private const int MAX_BATCHES = 5;
    private const int BATCH_SIZE = 1023;

    private void Awake()
    {
        CreateGrassMesh();
        CreateGrassMaterial();
        GenerateGrassPositions();

        batchMatrices = new Matrix4x4[MAX_BATCHES][];
        for (int b = 0; b < MAX_BATCHES; b++)
        {
            batchMatrices[b] = new Matrix4x4[BATCH_SIZE];
        }
        batchCounts = new int[MAX_BATCHES];
    }

    private void Start()
    {
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
    }

    /// Cria uma malha 3D volumétrica de Tufo Estrela (Star-Clump)
    /// Composta por 3 quads entrecruzados a 0°, 60° e 120° com curvatura para fora
    private void CreateGrassMesh()
    {
        grassMesh = new Mesh();
        grassMesh.name = "TheForest_Grass_Tuft";

        float h = grassHeight;
        float w = grassWidth;

        int numQuads = 3;
        int vertsPerQuad = 6;
        int trisPerQuad = 4; // 2 segmentos de quad

        Vector3[] vertices = new Vector3[numQuads * vertsPerQuad];
        Vector2[] uvs = new Vector2[numQuads * vertsPerQuad];
        Color[] colors = new Color[numQuads * vertsPerQuad];
        Vector3[] normals = new Vector3[numQuads * vertsPerQuad];
        int[] triangles = new int[numQuads * trisPerQuad * 3 * 2]; // *2 para double-sided

        int vIndex = 0;
        int tIndex = 0;

        for (int q = 0; q < numQuads; q++)
        {
            float angle = q * (180f / numQuads) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 norm = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));

            int baseV = vIndex;

            // Segmento inferior (Base -> Meio)
            // 0: Base Esquerda
            vertices[vIndex + 0] = -dir * (w * 0.5f);
            uvs[vIndex + 0] = new Vector2(0f, 0f);
            colors[vIndex + 0] = new Color(0f, 0f, 0f, 1f); // Sem vento na raiz
            normals[vIndex + 0] = Vector3.up;

            // 1: Base Direita
            vertices[vIndex + 1] = dir * (w * 0.5f);
            uvs[vIndex + 1] = new Vector2(1f, 0f);
            colors[vIndex + 1] = new Color(0f, 0f, 0f, 1f);
            normals[vIndex + 1] = Vector3.up;

            // Segmento Médio (Leve expansão e curvatura convexa)
            // 2: Meio Esquerdo
            vertices[vIndex + 2] = -dir * (w * 0.58f) + norm * 0.04f + Vector3.up * (h * 0.52f);
            uvs[vIndex + 2] = new Vector2(0f, 0.5f);
            colors[vIndex + 2] = new Color(0.45f, 0.35f, 0f, 1f);
            normals[vIndex + 2] = (Vector3.up * 0.8f + norm * 0.2f).normalized;

            // 3: Meio Direito
            vertices[vIndex + 3] = dir * (w * 0.58f) - norm * 0.04f + Vector3.up * (h * 0.52f);
            uvs[vIndex + 3] = new Vector2(1f, 0.5f);
            colors[vIndex + 3] = new Color(0.45f, 0.35f, 0f, 1f);
            normals[vIndex + 3] = (Vector3.up * 0.8f - norm * 0.2f).normalized;

            // Segmento Superior (Ponta arqueada pelo vento)
            // 4: Ponta Esquerda
            vertices[vIndex + 4] = -dir * (w * 0.32f) + norm * 0.09f + Vector3.up * h;
            uvs[vIndex + 4] = new Vector2(0.2f, 1f);
            colors[vIndex + 4] = new Color(1.0f, 1.0f, 0f, 1f); // Vento máximo no ápice
            normals[vIndex + 4] = Vector3.up;

            // 5: Ponta Direita
            vertices[vIndex + 5] = dir * (w * 0.32f) - norm * 0.09f + Vector3.up * h;
            uvs[vIndex + 5] = new Vector2(0.8f, 1f);
            colors[vIndex + 5] = new Color(1.0f, 1.0f, 0f, 1f);
            normals[vIndex + 5] = Vector3.up;

            // Triângulos face frontal:
            triangles[tIndex++] = baseV + 0;
            triangles[tIndex++] = baseV + 2;
            triangles[tIndex++] = baseV + 1;

            triangles[tIndex++] = baseV + 1;
            triangles[tIndex++] = baseV + 2;
            triangles[tIndex++] = baseV + 3;

            // Quad superior: (2, 4, 3) e (3, 4, 5)
            triangles[tIndex++] = baseV + 2;
            triangles[tIndex++] = baseV + 4;
            triangles[tIndex++] = baseV + 3;

            triangles[tIndex++] = baseV + 3;
            triangles[tIndex++] = baseV + 4;
            triangles[tIndex++] = baseV + 5;

            // Triângulos face traseira (Double-sided para visibilidade total):
            triangles[tIndex++] = baseV + 1;
            triangles[tIndex++] = baseV + 2;
            triangles[tIndex++] = baseV + 0;

            triangles[tIndex++] = baseV + 3;
            triangles[tIndex++] = baseV + 2;
            triangles[tIndex++] = baseV + 1;

            triangles[tIndex++] = baseV + 3;
            triangles[tIndex++] = baseV + 4;
            triangles[tIndex++] = baseV + 2;

            triangles[tIndex++] = baseV + 5;
            triangles[tIndex++] = baseV + 4;
            triangles[tIndex++] = baseV + 3;

            vIndex += vertsPerQuad;
        }

        grassMesh.vertices = vertices;
        grassMesh.uv = uvs;
        grassMesh.colors = colors;
        grassMesh.normals = normals;
        grassMesh.triangles = triangles;
        grassMesh.RecalculateBounds();
        grassMesh.RecalculateNormals();
        grassMesh.RecalculateTangents();
    }

    private void CreateGrassMaterial()
    {
        // Prioriza shader fotorealista The Forest com Subsurface Scattering
        Shader foliageShader = Shader.Find("Fraga/Foliage_TheForest_SSS");
        if (foliageShader == null) foliageShader = Shader.Find("Fraga/FoliageSway_Instanced");
        
        if (foliageShader != null)
        {
            grassMaterial = new Material(foliageShader);
            Material foliageRef = Resources.Load<Material>("Materials/Mat_OakFoliage");
            if (foliageRef != null && foliageRef.HasProperty("_BaseMap") && foliageRef.GetTexture("_BaseMap") != null)
            {
                grassMaterial.SetTexture("_BaseMap", foliageRef.GetTexture("_BaseMap"));
            }
            grassMaterial.SetColor("_BaseColor", grassColorDark);
            grassMaterial.SetFloat("_Cutoff", 0.28f);
        }
        else
        {
            Material foliageRef = Resources.Load<Material>("Materials/Mat_OakFoliage");
            if (foliageRef != null)
            {
                grassMaterial = new Material(foliageRef);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                grassMaterial = new Material(shader);
            }
            grassMaterial.SetColor("_BaseColor", grassColorDark);
            grassMaterial.SetFloat("_AlphaClip", 1f);
            grassMaterial.SetFloat("_Cutoff", 0.30f);
            grassMaterial.EnableKeyword("_ALPHATEST_ON");
            grassMaterial.SetOverrideTag("RenderType", "TransparentCutout");
            grassMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        grassMaterial.enableInstancing = true;
        if (grassMaterial.HasProperty("_Cull")) grassMaterial.SetFloat("_Cull", 0f); // Double sided
        if (grassMaterial.HasProperty("_Smoothness")) grassMaterial.SetFloat("_Smoothness", 0.15f);
    }

    private void GenerateGrassPositions()
    {
        grassPositions = new Vector3[grassDensity];
        grassMatrices = new Matrix4x4[grassDensity];

        for (int i = 0; i < grassDensity; i++)
        {
            // Espalha a relva por todo o terreno de 100x100m com margem segura
            float x = Random.Range(3f, 97f);
            float z = Random.Range(3f, 97f);
            float y = TerrainMeshGenerator.GetTerrainHeight(x, z);

            grassPositions[i] = new Vector3(x, y, z);

            // Variação orgânica de escala e rotação azimutal
            float scaleXZ = Random.Range(0.85f, 1.45f);
            float scaleY = Random.Range(0.80f, 1.35f);
            float rot = Random.Range(0f, 360f);

            grassMatrices[i] = Matrix4x4.TRS(
                grassPositions[i],
                Quaternion.Euler(Random.Range(-3f, 3f), rot, Random.Range(-3f, 3f)),
                new Vector3(scaleXZ, scaleY, scaleXZ)
            );
        }
    }

    private void Update()
    {
        if (!enableGrass) return;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else return;
        }

        // Sincroniza parâmetros de vento se disponíveis no shader
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

        for (int b = 0; b < MAX_BATCHES; b++)
        {
            batchCounts[b] = 0;
        }

        int currentBatch = 0;

        for (int i = 0; i < grassDensity; i++)
        {
            float distSq = (grassPositions[i] - playerPos).sqrMagnitude;
            if (distSq < cullDistSq)
            {
                if (batchCounts[currentBatch] >= BATCH_SIZE)
                {
                    currentBatch++;
                    if (currentBatch >= MAX_BATCHES) break;
                }

                batchMatrices[currentBatch][batchCounts[currentBatch]++] = grassMatrices[i];
            }
        }

        for (int b = 0; b <= currentBatch && b < MAX_BATCHES; b++)
        {
            if (batchCounts[b] > 0)
            {
                Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, batchMatrices[b], batchCounts[b],
                    null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            }
        }
    }

    /// Atualiza posições de relva quando um objeto for colocado ou árvore derrubada
    public void RefreshGrassAt(float worldX, float worldZ, float radius)
    {
        float radiusSq = radius * radius;
        for (int i = 0; i < grassDensity; i++)
        {
            float dx = grassPositions[i].x - worldX;
            float dz = grassPositions[i].z - worldZ;
            if (dx * dx + dz * dz < radiusSq)
            {
                float newX = Random.Range(4f, 96f);
                float newZ = Random.Range(4f, 96f);
                float newY = TerrainMeshGenerator.GetTerrainHeight(newX, newZ);
                grassPositions[i] = new Vector3(newX, newY, newZ);

                grassMatrices[i] = Matrix4x4.TRS(
                    grassPositions[i],
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                    Vector3.one * Random.Range(0.85f, 1.4f)
                );
            }
        }
    }
}
