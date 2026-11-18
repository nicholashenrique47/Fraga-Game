using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainMeshGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public float tileSize = 1f;
    public float heightVariation = 0.2f;

    [Header("Chunk Streaming (768x768m Mobile 60 FPS)")]
    public int chunkSizeMeters = 64;
    public int vertexStepMeters = 2;

    [Header("Colors (Vertex Based)")]
    public Color darkDirtColor = new Color(0.22f, 0.16f, 0.10f); // Turfa escura / Trilha
    public Color deadGrassColor = new Color(0.24f, 0.28f, 0.16f); // Relva outonal
    public Color waterColor = new Color(0.08f, 0.18f, 0.25f);
    public Color rockFloorColor = new Color(0.35f, 0.35f, 0.36f); // Granito galego
    public Color mossColor = new Color(0.18f, 0.32f, 0.14f);     // Musgo verdejante
    public Color sandColor = new Color(0.56f, 0.50f, 0.38f);     // Praia atlântica
    public Color snowColor = new Color(0.84f, 0.87f, 0.90f);     // Neve nos picos > 25m

    private readonly List<GameObject> spawnedChunks = new List<GameObject>();

    /// <summary>
    /// Calcula a altura exata do terreno em qualquer posição do mundo (x, z).
    /// Sincronizada com o FragaWorldBlueprint (Montanha, Lago, Rio, Enseada, Pântano, Acampamento).
    /// </summary>
    public static float GetTerrainHeight(float x, float z)
    {
        return FragaWorldBlueprint.SampleHeight(x, z);
    }

    public static Vector3 GetTerrainNormal(float x, float z)
    {
        float hL = FragaWorldBlueprint.SampleHeight(x - 1f, z);
        float hR = FragaWorldBlueprint.SampleHeight(x + 1f, z);
        float hD = FragaWorldBlueprint.SampleHeight(x, z - 1f);
        float hU = FragaWorldBlueprint.SampleHeight(x, z + 1f);
        return new Vector3(hL - hR, 2f, hD - hU).normalized;
    }

    private Material ResolveGroundMaterial()
    {
        Material groundMat = Resources.Load<Material>("Materials/Mat_DarkForest_Ground");
        if (groundMat != null) return groundMat;

        Shader pbrShader = Shader.Find("Fraga/ForestGround_PBR_Wetness");
        if (pbrShader == null) pbrShader = Shader.Find("Fraga/ForestGround_Lit");
        if (pbrShader == null) pbrShader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = new Material(pbrShader);
        mat.SetFloat("_Smoothness", 0.14f);
        return mat;
    }

    public void GenerateTerrain(System.IntPtr mapDataPtr, int width, int height)
    {
        FragaWorldBlueprint.EnsureInitialized();

        // Limpa chunks antigos em caso de recarregamento / Novo Jogo
        for (int i = 0; i < spawnedChunks.Count; i++)
        {
            if (spawnedChunks[i] != null) Destroy(spawnedChunks[i]);
        }
        spawnedChunks.Clear();

        for (int c = transform.childCount - 1; c >= 0; c--)
        {
            Transform child = transform.GetChild(c);
            if (child != null && child.name.StartsWith("Procedural_Terrain_Chunk"))
            {
                Destroy(child.gameObject);
            }
        }

        // Remove colisor da raiz para delegar exclusivamente aos chunks (melhor frustum culling e physics broadphase)
        MeshCollider rootCol = GetComponent<MeshCollider>();
        if (rootCol != null) rootCol.sharedMesh = null;
        MeshFilter rootMF = GetComponent<MeshFilter>();
        if (rootMF != null) rootMF.sharedMesh = null;
        MeshRenderer rootMR = GetComponent<MeshRenderer>();
        if (rootMR != null) rootMR.enabled = false;

        Material groundMat = ResolveGroundMaterial();
        int step = Mathf.Max(1, vertexStepMeters);
        int cSize = Mathf.Max(16, chunkSizeMeters);
        // Garante divisibilidade exata pelo step para zero costuras entre chunks
        cSize = Mathf.Max(step, (cSize / step) * step);

        int chunksX = Mathf.CeilToInt(width / (float)cSize);
        int chunksZ = Mathf.CeilToInt(height / (float)cSize);

        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                int startX = cx * cSize;
                int startZ = cz * cSize;
                int spanX = Mathf.Min(cSize, width - startX);
                int spanZ = Mathf.Min(cSize, height - startZ);
                if (spanX < step || spanZ < step) continue;

                BuildTerrainChunk(cx, cz, startX, startZ, spanX, spanZ, step, mapDataPtr, width, height, groundMat);
            }
        }

        // Gera automaticamente Oceano Atlântico, Lago da Fraga e Rio da Fraga
        FragaWaterManager waterMgr = FindAnyObjectByType<FragaWaterManager>();
        if (waterMgr == null)
        {
            GameObject waterObj = new GameObject("Fraga_Water_System");
            waterMgr = waterObj.AddComponent<FragaWaterManager>();
        }
        waterMgr.BuildWater(width, height);

        Debug.Log($"[TerrainMeshGenerator] Terreno chunked 768x768 ({chunksX}x{chunksZ} = {spawnedChunks.Count} chunks, passo {step}m) + Águas Atlânticas gerados.");
    }

    private void BuildTerrainChunk(
        int chunkX, int chunkZ,
        int startX, int startZ,
        int spanX, int spanZ,
        int step,
        System.IntPtr mapDataPtr, int mapW, int mapH,
        Material groundMat)
    {
        int vertsX = (spanX / step) + 1;
        int vertsZ = (spanZ / step) + 1;
        int vertTotal = vertsX * vertsZ;

        Vector3[] vertices = new Vector3[vertTotal];
        Vector3[] normals = new Vector3[vertTotal];
        Vector2[] uvs = new Vector2[vertTotal];
        Color[] colors = new Color[vertTotal];
        int[] triangles = new int[(vertsX - 1) * (vertsZ - 1) * 6];

        Color swampPeat = new Color(0.16f, 0.18f, 0.11f);
        Color autumnLeaf = new Color(0.32f, 0.23f, 0.13f);
        Color pineNeedle = new Color(0.15f, 0.24f, 0.13f);

        int idx = 0;
        for (int vz = 0; vz < vertsZ; vz++)
        {
            float wz = (startZ + vz * step) * tileSize;
            for (int vx = 0; vx < vertsX; vx++)
            {
                float wx = (startX + vx * step) * tileSize;
                float h = FragaWorldBlueprint.SampleHeight(wx, wz);
                if (float.IsNaN(h) || float.IsInfinity(h)) h = FragaWorldBlueprint.SeaLevel;

                // Normais analíticas globais eliminam qualquer costura visível entre chunks 64x64m
                float hL = FragaWorldBlueprint.SampleHeight(wx - 1f, wz);
                float hR = FragaWorldBlueprint.SampleHeight(wx + 1f, wz);
                float hD = FragaWorldBlueprint.SampleHeight(wx, wz - 1f);
                float hU = FragaWorldBlueprint.SampleHeight(wx, wz + 1f);
                Vector3 nrm = new Vector3(hL - hR, 2f, hD - hU).normalized;

                vertices[idx] = new Vector3(wx, h, wz);
                normals[idx] = nrm;
                uvs[idx] = new Vector2(wx * 0.5f, wz * 0.5f);

                float coast = FragaWorldBlueprint.GetIslandCoastMetric(wx, wz);
                float waterDepth = FragaWorldBlueprint.GetWaterDepth(wx, wz);
                float trailW = FragaWorldBlueprint.GetTrailWeight(wx, wz);
                float noiseVar = Mathf.PerlinNoise(wx * 0.18f + 50f, wz * 0.18f + 50f);
                float patchVar = Mathf.PerlinNoise(wx * 0.045f + 13f, wz * 0.045f + 91f);
                FragaWorldBlueprint.WorldRegion reg = FragaWorldBlueprint.GetRegion(wx, wz);

                Color baseCol;
                float wetness = 0f;

                if (coast >= 0.99f || waterDepth > 0.25f)
                {
                    // Leito marinho / fundo de lago e rio
                    baseCol = Color.Lerp(sandColor * 0.65f, waterColor, Mathf.Clamp01(waterDepth * 0.45f));
                    wetness = 0.92f;
                }
                else if (coast >= 0.87f && h < FragaWorldBlueprint.SeaLevel + 1.55f && reg != FragaWorldBlueprint.WorldRegion.FarolBluff)
                {
                    // Praia arenosa dourada galega
                    float sandT = FragaWorldBlueprint.SmoothRange(0.87f, 0.96f, coast);
                    baseCol = Color.Lerp(deadGrassColor, sandColor, sandT);
                    wetness = Mathf.Clamp01((FragaWorldBlueprint.SeaLevel + 0.6f - h) * 0.8f);
                }
                else if (reg == FragaWorldBlueprint.WorldRegion.SwampBasin)
                {
                    // Turfa úmida do Pântano das Meigas
                    baseCol = Color.Lerp(swampPeat, mossColor * 0.85f, noiseVar);
                    wetness = Mathf.Clamp01(0.45f + (2.8f - h) * 0.25f);
                }
                else if (h > 25.0f)
                {
                    // Cume da serra com granito e neve
                    float snowT = FragaWorldBlueprint.SmoothRange(25.0f, 30.2f, h);
                    baseCol = Color.Lerp(rockFloorColor, snowColor, snowT);
                    wetness = 0.15f;
                }
                else if (h > 16.0f || nrm.y < 0.72f)
                {
                    // Encostas rochosas e penhasco do farol
                    float rockT = Mathf.Clamp01(Mathf.Max((h - 14f) / 6f, (0.78f - nrm.y) * 2.5f));
                    baseCol = Color.Lerp(pineNeedle, rockFloorColor, rockT);
                    wetness = 0.12f;
                }
                else if (reg == FragaWorldBlueprint.WorldRegion.PineRidge)
                {
                    baseCol = Color.Lerp(pineNeedle, mossColor, noiseVar * 0.65f);
                    wetness = Mathf.Clamp01((4.0f - h) * 0.15f);
                }
                else
                {
                    // Floresta dos Carvalhos com variação de folhas outonais e musgo vivo
                    Color leafOrMoss = Color.Lerp(mossColor, autumnLeaf, patchVar * 0.7f);
                    baseCol = Color.Lerp(deadGrassColor, leafOrMoss, noiseVar);
                    wetness = Mathf.Clamp01((3.5f - h) * 0.18f);
                }

                // Trilhas batidas bem legíveis em marrom terroso
                if (trailW > 0.05f && waterDepth < 0.1f)
                {
                    Color trailCol = Color.Lerp(darkDirtColor * 1.15f, sandColor * 0.72f, noiseVar * 0.35f);
                    baseCol = Color.Lerp(baseCol, trailCol, trailW * 0.85f);
                    wetness *= (1f - trailW * 0.65f);
                }

                // Ruínas do Castro Celta (lajes de pedra antiga)
                if (mapDataPtr != System.IntPtr.Zero && wx < mapW && wz < mapH)
                {
                    int gx = Mathf.Clamp(Mathf.FloorToInt(wx), 0, mapW - 1);
                    int gz = Mathf.Clamp(Mathf.FloorToInt(wz), 0, mapH - 1);
                    int tileID = ResourceManager.GetMapCell(mapDataPtr, mapW, mapH, gx, gz);
                    if (tileID == 2)
                    {
                        baseCol = Color.Lerp(baseCol, rockFloorColor, 0.65f);
                    }
                }

                baseCol.a = Mathf.Clamp01(wetness);
                colors[idx] = baseCol;
                idx++;
            }
        }

        int t = 0;
        for (int z = 0; z < vertsZ - 1; z++)
        {
            for (int x = 0; x < vertsX - 1; x++)
            {
                int v0 = z * vertsX + x;
                int v1 = v0 + 1;
                int v2 = v0 + vertsX;
                int v3 = v2 + 1;

                triangles[t++] = v0;
                triangles[t++] = v2;
                triangles[t++] = v1;

                triangles[t++] = v1;
                triangles[t++] = v2;
                triangles[t++] = v3;
            }
        }

        Mesh chunkMesh = new Mesh();
        chunkMesh.name = $"Fraga_Terrain_Chunk_{chunkX}_{chunkZ}";
        chunkMesh.vertices = vertices;
        chunkMesh.normals = normals;
        chunkMesh.uv = uvs;
        chunkMesh.colors = colors;
        chunkMesh.triangles = triangles;
        chunkMesh.RecalculateBounds();
        chunkMesh.RecalculateTangents();

        // Nome contém "Terrain" para manter 100% compatibilidade com PlayerController.SolveObstacleCollisions
        GameObject chunkObj = new GameObject($"Procedural_Terrain_Chunk_{chunkX}_{chunkZ}");
        chunkObj.transform.SetParent(transform, false);

        MeshFilter mf = chunkObj.AddComponent<MeshFilter>();
        mf.sharedMesh = chunkMesh;

        MeshRenderer mr = chunkObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = groundMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        MeshCollider mc = chunkObj.AddComponent<MeshCollider>();
        mc.sharedMesh = chunkMesh;

        spawnedChunks.Add(chunkObj);
    }
}
