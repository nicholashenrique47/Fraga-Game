using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainMeshGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public float tileSize = 1f;
    public float heightVariation = 0.2f;

    [Header("Colors (Vertex Based)")]
    public Color darkDirtColor = new Color(0.22f, 0.16f, 0.10f); // Turfa escura
    public Color deadGrassColor = new Color(0.24f, 0.28f, 0.16f); // Relva outonal
    public Color waterColor = new Color(0.08f, 0.18f, 0.25f);
    public Color rockFloorColor = new Color(0.35f, 0.35f, 0.36f); // Granito galego
    public Color mossColor = new Color(0.18f, 0.32f, 0.14f);     // Musgo verdejante
    public Color sandColor = new Color(0.45f, 0.40f, 0.30f);     // Margens arenosas

    private Mesh terrainMesh;

    /// Calcula a altura do terreno numa posição arbitrária (The Forest Atlantic Biome).
    public static float GetTerrainHeight(float x, float z)
    {
        // 1. Macro hills (ondulação ampla da serra)
        float height = Mathf.PerlinNoise(x * 0.025f, z * 0.025f) * 4.2f;
        
        // 2. Ridged multifractal (cristas rochosas e ravinas orgânicas)
        float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.06f, z * 0.06f) * 2f - 1f);
        height += ridge * ridge * 1.6f;
        
        // 3. Micro-rugosidade tátil de solo de floresta
        height += Mathf.PerlinNoise(x * 0.16f + 85f, z * 0.16f + 85f) * 0.35f;
        
        return height;
    }

    public void GenerateTerrain(System.IntPtr mapDataPtr, int width, int height)
    {
        terrainMesh = new Mesh();
        terrainMesh.name = "Procedural_Fraga_Terrain";
        terrainMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[width * height * 6];

        // Geração de Vértices com multi-octave noise
        for (int i = 0, y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                float yOffset = GetTerrainHeight(x * tileSize, y * tileSize);
                
                int tileID = 0;
                if (x < width && y < height)
                {
                    tileID = ResourceManager.GetMapCell(mapDataPtr, width, height, x, y);
                }

                if (tileID == 7) yOffset -= 0.6f; // Água mais profunda

                vertices[i] = new Vector3(x * tileSize, yOffset, y * tileSize);
                // CRUCIAL: Tiling de 0.5f significa 1 textura a cada 2 metros, trazendo alta fidelidade (The Forest)
                uvs[i] = new Vector2(x * 0.5f, y * 0.5f);
                
                // Blending orgânico de cores por bioma
                float noiseVar = Mathf.PerlinNoise(x * 0.3f + 50f, y * 0.3f + 50f);
                
                Color baseColor;
                if (tileID == 1) // Floresta: musgo denso + relva outonal
                {
                    baseColor = Color.Lerp(deadGrassColor, mossColor, noiseVar);
                }
                else if (tileID == 2) // Ruína celta: granito + solo compacto
                {
                    baseColor = Color.Lerp(darkDirtColor, rockFloorColor, noiseVar * 0.7f);
                }
                else if (tileID == 7) // Água
                {
                    baseColor = waterColor;
                }
                else if (tileID == 8) // Caverna
                {
                    baseColor = Color.Lerp(rockFloorColor, darkDirtColor, noiseVar * 0.4f);
                }
                else // Clareiras
                {
                    baseColor = Color.Lerp(darkDirtColor, deadGrassColor, noiseVar * 0.6f);
                    
                    bool nearWater = false;
                    for (int dy = -2; dy <= 2 && !nearWater; dy++)
                    {
                        for (int dx = -2; dx <= 2 && !nearWater; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                if (ResourceManager.GetMapCell(mapDataPtr, width, height, nx, ny) == 7)
                                    nearWater = true;
                            }
                        }
                    }
                    if (nearWater) baseColor = Color.Lerp(baseColor, sandColor, 0.4f);
                }

                // Codifica depressões de umidade para o shader de poças (ForestGround_PBR_Wetness)
                float cavityMask = Mathf.Clamp01((3.0f - yOffset) * 0.25f);
                baseColor.a = cavityMask;
                
                colors[i] = baseColor;
                i++;
            }
        }

        // Geração de Triângulos
        int vert = 0;
        int tris = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                triangles[tris + 0] = vert + 0;
                triangles[tris + 1] = vert + width + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + width + 1;
                triangles[tris + 5] = vert + width + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }

        terrainMesh.vertices = vertices;
        terrainMesh.uv = uvs;
        terrainMesh.colors = colors;
        terrainMesh.triangles = triangles;
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateTangents();

        GetComponent<MeshFilter>().mesh = terrainMesh;

        // MeshCollider para passos e colisão
        MeshCollider col = GetComponent<MeshCollider>();
        if (col == null) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = terrainMesh;
        
        // Garante aplicação do material de alta resolução da floresta
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        Material groundMat = Resources.Load<Material>("Materials/Mat_DarkForest_Ground");
        if (groundMat != null)
        {
            renderer.sharedMaterial = groundMat;
        }
        else
        {
            Shader pbrShader = Shader.Find("Fraga/ForestGround_PBR_Wetness");
            if (pbrShader == null) pbrShader = Shader.Find("Fraga/ForestGround_Lit");
            if (pbrShader == null) pbrShader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = new Material(pbrShader);
            mat.SetFloat("_Smoothness", 0.12f);
            renderer.sharedMaterial = mat;
        }
    }
}
