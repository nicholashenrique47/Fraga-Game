using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainMeshGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public float tileSize = 1f;
    public float heightVariation = 0.2f;

    [Header("Colors (Vertex Based)")]
    public Color darkDirtColor = new Color(0.15f, 0.1f, 0.05f);
    public Color deadGrassColor = new Color(0.1f, 0.2f, 0.1f);
    public Color waterColor = new Color(0.05f, 0.15f, 0.25f);
    public Color rockFloorColor = new Color(0.2f, 0.2f, 0.2f);

    private Mesh terrainMesh;

    public void GenerateTerrain(System.IntPtr mapDataPtr, int width, int height)
    {
        terrainMesh = new Mesh();
        terrainMesh.name = "Procedural_Fraga_Terrain";
        // Para suportar mapas 100x100 (10k vertices), usamos formato de indice de 16-bit. 
        // Acima de 255x255, mudar para 32-bit.
        terrainMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[width * height * 6];

        // Geracao de Vertices
        for (int i = 0, y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                // Ruido leve para nao ser totalmente liso, mas sem quebrar a isometria severamente
                float yOffset = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * heightVariation;
                
                // Se for agua, afundar um pouco o terreno
                int tileID = 0;
                if (x < width && y < height)
                {
                    tileID = ResourceManager.GetMapCell(mapDataPtr, width, height, x, y);
                }

                if (tileID == 7) yOffset -= 0.4f;

                vertices[i] = new Vector3(x * tileSize, yOffset, y * tileSize);
                uvs[i] = new Vector2((float)x / width, (float)y / height);
                
                // Se for arvore (1), a terra por baixo e mais musgosa. Ruina (2) terra batida.
                if (tileID == 1) colors[i] = deadGrassColor;
                else if (tileID == 2) colors[i] = Color.Lerp(darkDirtColor, Color.gray, 0.5f);
                else if (tileID == 7) colors[i] = waterColor; // Agua
                else if (tileID == 8) colors[i] = rockFloorColor; // ChÃƒÂ£o de caverna
                else colors[i] = darkDirtColor;

                i++;
            }
        }

        // GeraÃƒÂ§ÃƒÂ£o de TriÃƒÂ¢ngulos
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

        GetComponent<MeshFilter>().mesh = terrainMesh;
        
        // Criar um material bÃƒÂ¡sico para Vertex Colors se nÃƒÂ£o houver um assinalado
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer.sharedMaterial == null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Smoothness", 0f); // SuperfÃƒÂ­cie fosca
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); // Desabilitado
            renderer.sharedMaterial = mat;
        }
    }
}
