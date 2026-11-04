using UnityEngine;

/// <summary>
/// Motor de GPU Instancing Indirect para Vegetação de Fraga Game.
/// Consolida milhares de árvores e arbustos em 2 Draw Calls na GPU.
/// </summary>
public class GPUInstancedFoliageSystem : MonoBehaviour
{
    [Header("Malhas e Materiais")]
    public Mesh trunkMesh;
    public Material trunkMaterial;
    public Mesh foliageMesh;
    public Material foliageMaterial;

    private ComputeBuffer argsBufferTrunk;
    private ComputeBuffer argsBufferFoliage;
    private ComputeBuffer matrixBuffer;
    private int instanceCount = 0;
    private Bounds renderBounds;

    public void Initialize(Matrix4x4[] treeTransforms)
    {
        if (treeTransforms == null || treeTransforms.Length == 0) return;
        instanceCount = treeTransforms.Length;

        // 1. Alocar Buffer Estruturado com Matrizes de Transformação
        matrixBuffer = new ComputeBuffer(instanceCount, 16 * sizeof(float));
        matrixBuffer.SetData(treeTransforms);

        // 2. Configurar Buffers Indiretos de Argumentos da GPU (IndexCount, InstanceCount, StartIndex, BaseVertex, StartInstance)
        if (trunkMesh != null)
        {
            uint[] argsTrunk = new uint[5] { trunkMesh.GetIndexCount(0), (uint)instanceCount, 0, 0, 0 };
            argsBufferTrunk = new ComputeBuffer(1, argsTrunk.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBufferTrunk.SetData(argsTrunk);
            if (trunkMaterial != null) trunkMaterial.SetBuffer("_AllInstancesTransform", matrixBuffer);
        }

        if (foliageMesh != null)
        {
            uint[] argsFoliage = new uint[5] { foliageMesh.GetIndexCount(0), (uint)instanceCount, 0, 0, 0 };
            argsBufferFoliage = new ComputeBuffer(1, argsFoliage.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBufferFoliage.SetData(argsFoliage);
            if (foliageMaterial != null) foliageMaterial.SetBuffer("_AllInstancesTransform", matrixBuffer);
        }

        renderBounds = new Bounds(Vector3.zero, Vector3.one * 3000f);
    }

    private void Update()
    {
        if (instanceCount == 0) return;

        // Despacha toda a floresta em apenas 2 Draw Calls
        if (trunkMesh != null && trunkMaterial != null && argsBufferTrunk != null)
        {
            Graphics.DrawMeshInstancedIndirect(trunkMesh, 0, trunkMaterial, renderBounds, argsBufferTrunk);
        }
        if (foliageMesh != null && foliageMaterial != null && argsBufferFoliage != null)
        {
            Graphics.DrawMeshInstancedIndirect(foliageMesh, 0, foliageMaterial, renderBounds, argsBufferFoliage);
        }
    }

    private void OnDestroy()
    {
        if (matrixBuffer != null) { matrixBuffer.Release(); matrixBuffer = null; }
        if (argsBufferTrunk != null) { argsBufferTrunk.Release(); argsBufferTrunk = null; }
        if (argsBufferFoliage != null) { argsBufferFoliage.Release(); argsBufferFoliage = null; }
    }
}
