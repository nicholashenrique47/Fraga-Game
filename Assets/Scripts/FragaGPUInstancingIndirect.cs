using UnityEngine;

/// <summary>
/// Sistema de Renderização Indireta por GPU para Vegetação em Massa (The Forest AAA).
/// Permite renderizar milhares de tufos de fentos e tojos com apenas 1 a 2 Draw Calls,
/// garantindo 60 FPS cravados no PC médio e excelente performance no Steam Deck.
/// </summary>
public class FragaGPUInstancingIndirect : MonoBehaviour
{
    [Header("Malha e Material")]
    public Mesh foliageMesh;
    public Material foliageMaterial;
    public int instanceCount = 2500;
    public Vector3 areaCenter = Vector3.zero;
    public Vector3 areaExtents = new Vector3(200f, 20f, 200f);

    private ComputeBuffer matrixBuffer;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    private bool isInitialized = false;

    void Start()
    {
        InitializeBuffers();
    }

    public void InitializeBuffers()
    {
        if (foliageMesh == null || foliageMaterial == null || instanceCount <= 0) return;

        // Libera buffers antigos se existirem
        ReleaseBuffers();

        // 1. Buffer de Matrizes de Transformacao
        matrixBuffer = new ComputeBuffer(instanceCount, 64);
        Matrix4x4[] matrices = new Matrix4x4[instanceCount];

        for (int i = 0; i < instanceCount; i++)
        {
            Vector3 pos = areaCenter + new Vector3(
                Random.Range(-areaExtents.x * 0.5f, areaExtents.x * 0.5f),
                Random.Range(-areaExtents.y * 0.5f, areaExtents.y * 0.5f),
                Random.Range(-areaExtents.z * 0.5f, areaExtents.z * 0.5f)
            );

            // Alinha com o chao via Raycast se houver colisor
            if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 50f))
            {
                pos.y = hit.point.y;
            }

            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Vector3 scale = Vector3.one * Random.Range(0.85f, 1.35f);
            matrices[i] = Matrix4x4.TRS(pos, rot, scale);
        }

        matrixBuffer.SetData(matrices);
        foliageMaterial.SetBuffer("_MatrixBuffer", matrixBuffer);

        // 2. Buffer de Argumentos de Desenho Indireto
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (uint)foliageMesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = (uint)foliageMesh.GetIndexStart(0);
        args[3] = (uint)foliageMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        isInitialized = true;
    }

    void Update()
    {
        if (isInitialized && foliageMesh != null && foliageMaterial != null && argsBuffer != null)
        {
            Graphics.DrawMeshInstancedIndirect(
                foliageMesh,
                0,
                foliageMaterial,
                new Bounds(areaCenter, areaExtents * 2f),
                argsBuffer
            );
        }
    }

    void OnDisable()
    {
        ReleaseBuffers();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void ReleaseBuffers()
    {
        if (matrixBuffer != null)
        {
            matrixBuffer.Release();
            matrixBuffer = null;
        }
        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }
        isInitialized = false;
    }
}
