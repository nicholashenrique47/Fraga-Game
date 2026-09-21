using UnityEngine;
using System.Runtime.InteropServices;

public class MapGenerator : MonoBehaviour
{
    [DllImport("FragaNative")]
    public static extern void GenerateMapGrid(int[] mapData, int width, int height, int seed);

    [Header("Configurações do Mapa")]
    public int mapWidth = 50;
    public int mapHeight = 50;
    public int mapSeed = 1913;
    public float spacing = 2.5f;

    [Header("Prefabs Visuais")]
    public GameObject treePrefab;
    public GameObject ruinPrefab;

    void Start()
    {
        int[] spatialGrid = new int[mapWidth * mapHeight];
        GenerateMapGrid(spatialGrid, mapWidth, mapHeight, mapSeed);
        RenderizarMapa(spatialGrid);
    }

    void RenderizarMapa(int[] grid)
    {
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int index = y * mapWidth + x;
                int tileId = grid[index];

                Vector3 position = new Vector3(x * spacing, 0, y * spacing);

                if (tileId == 1 && treePrefab != null)
                {
                    Instantiate(treePrefab, position, Quaternion.identity, transform);
                }
                else if (tileId == 2 && ruinPrefab != null)
                {
                    Instantiate(ruinPrefab, position, Quaternion.identity, transform);
                }
            }
        }
    }
}