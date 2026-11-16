using UnityEngine;
using System.Runtime.InteropServices;

public class EntityEventSystem : MonoBehaviour
{
    // Acessos P/Invoke à DLL Nativa
    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern int TrasnoGetNextMove(System.IntPtr mapData, int width, int height, int startX, int startY, out int outTargetX, out int outTargetY);

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern float CalculateMeigaDebuff(System.IntPtr meigasX, System.IntPtr meigasY, int meigaCount, float playerX, float playerY);

    [Header("Trasno Settings (Saboteur)")]
    public Transform trasnoTransform;
    public float trasnoSpeed = 2f;
    private bool isTrasnoActive = false;
    private Vector3 trasnoTargetPos;

    [Header("Meigas Settings (Spiritual Threats)")]
    public Transform[] meigaTransforms; // Meigas são estáticas
    private float[] meigasXArray;
    private float[] meigasYArray;
    private GCHandle meigasXHandle;
    private GCHandle meigasYHandle;

    [Header("Core Dependencies")]
    public SanitySystem sanitySystem; // Para aplicar os debuffs
    private System.IntPtr mapDataPtr;
    private int mapWidth = FragaWorldBlueprint.DefaultWidth;
    private int mapHeight = FragaWorldBlueprint.DefaultHeight;

    public void Initialize(System.IntPtr ptr, int width, int height)
    {
        mapDataPtr = ptr;
        mapWidth = width;
        mapHeight = height;

        // Extrai posições das Meigas para arrays fixos (SoA) em C++
        if (meigaTransforms != null && meigaTransforms.Length > 0)
        {
            meigasXArray = new float[meigaTransforms.Length];
            meigasYArray = new float[meigaTransforms.Length];
            
            for(int i = 0; i < meigaTransforms.Length; i++)
            {
                meigasXArray[i] = meigaTransforms[i].position.x;
                meigasYArray[i] = meigaTransforms[i].position.z;
            }
            
            // Pinned Handles para evitar Garbage Collection em runtime
            meigasXHandle = GCHandle.Alloc(meigasXArray, GCHandleType.Pinned);
            meigasYHandle = GCHandle.Alloc(meigasYArray, GCHandleType.Pinned);
        }
    }

    public void SpawnTrasno(Vector3 spawnPos)
    {
        if (trasnoTransform != null)
        {
            spawnPos.y = TerrainMeshGenerator.GetTerrainHeight(spawnPos.x, spawnPos.z);
            trasnoTransform.position = spawnPos;
            trasnoTargetPos = spawnPos;
            trasnoTransform.gameObject.SetActive(true);
            isTrasnoActive = true;
        }
    }

    private void Update()
    {
        UpdateTrasno();
        UpdateMeigasDebuff();
    }

    private void UpdateTrasno()
    {
        if (!isTrasnoActive || mapDataPtr == System.IntPtr.Zero || trasnoTransform == null) return;

        // Verifica se o Trasno atingiu o tile atual para calcular o próximo passo no C++
        float dxT = trasnoTransform.position.x - trasnoTargetPos.x;
        float dzT = trasnoTransform.position.z - trasnoTargetPos.z;
        if (dxT * dxT + dzT * dzT < 0.04f)
        {
            int gridX = Mathf.RoundToInt(trasnoTransform.position.x);
            int gridY = Mathf.RoundToInt(trasnoTransform.position.z);

            int targetX, targetY;
            // Cálculo nativo A*/BFS restrito
            int foundPath = TrasnoGetNextMove(mapDataPtr, mapWidth, mapHeight, gridX, gridY, out targetX, out targetY);

            if (foundPath == 1)
            {
                trasnoTargetPos = new Vector3(targetX, TerrainMeshGenerator.GetTerrainHeight(targetX, targetY), targetY);
            }
        }

        // Movimento visual fluido interpolado para 60FPS mobile
        Vector3 nextPos = Vector3.MoveTowards(trasnoTransform.position, trasnoTargetPos, trasnoSpeed * Time.deltaTime);
        nextPos.y = TerrainMeshGenerator.GetTerrainHeight(nextPos.x, nextPos.z);
        trasnoTransform.position = nextPos;
    }

    private void UpdateMeigasDebuff()
    {
        if (sanitySystem == null || meigasXArray == null || !meigasXHandle.IsAllocated) return;

        float playerX = sanitySystem.transform.position.x;
        float playerY = sanitySystem.transform.position.z;

        // Cálculo de distância escalar e debuffs integralmente no C++ nativo O(N)
        float meigaDebuffMultiplier = CalculateMeigaDebuff(
            meigasXHandle.AddrOfPinnedObject(),
            meigasYHandle.AddrOfPinnedObject(),
            meigasXArray.Length,
            playerX, playerY
        );

        if (meigaDebuffMultiplier > 0f)
        {
            // Aplica degradação massiva de sanidade
            sanitySystem.sanity -= (meigaDebuffMultiplier * Time.deltaTime);
        }
    }

    private void OnDestroy()
    {
        if (meigasXHandle.IsAllocated) meigasXHandle.Free();
        if (meigasYHandle.IsAllocated) meigasYHandle.Free();
    }
}
