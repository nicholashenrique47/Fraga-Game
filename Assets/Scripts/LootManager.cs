using UnityEngine;
using System.Collections.Generic;

/// LootManager: Gere items perdidos ou largados no chao do mundo.
/// Renderiza via GPU Instancing (Sem prefabs/GameObjects para performance).
public struct DroppedItem
{
    public int itemID;
    public Vector3 position;
    public Matrix4x4 matrix;
}

public class LootManager : MonoBehaviour
{
    [Header("Rendering (Mesh Instancing)")]
    public Mesh sackMesh;
    public Material sackMaterial;
    
    [Header("Interaction")]
    public float pickupRadius = 2.5f;
    public Transform playerTransform;
    public InventoryAdvanced inventory;

    private List<DroppedItem> droppedItems = new List<DroppedItem>();
    private Matrix4x4[] renderMatrices = new Matrix4x4[1023]; // Limite do GPU Instancing por batch

    // Pseudo-Singleton para chamadas rapidas do C#
    public static LootManager Instance;
    private UIManager uiCache;

    private void Awake()
    {
        Instance = this;
        uiCache = FindAnyObjectByType<UIManager>();

        // Se nao for fornecido um mesh, cria um saco de serapilheira simples
        if (sackMesh == null)
        {
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sackMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);
        }

        if (sackMaterial == null)
        {
            sackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            sackMaterial.color = new Color(0.6f, 0.4f, 0.2f);
            sackMaterial.enableInstancing = true;
        }
    }

    /// Larga um item no chao do mapa (Ex: Cervo morto, ou dropar do inventario)
    public void DropItem(int itemID, int amount, Vector3 pos)
    {
        if (droppedItems.Count >= 1023) 
        {
            Debug.LogWarning("[LootManager] Maximo de itens no chao atingido.");
            return;
        }

        DroppedItem drop = new DroppedItem();
        drop.itemID = itemID;

        // Adiciona alguma variancia na posicao para nao colidirem todos no mesmo ponto exato
        Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0.2f, Random.Range(-0.5f, 0.5f));
        drop.position = pos + randomOffset;
        
        // Rotacao aleatoria e escala pequena para simular um "Drop"
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        drop.matrix = Matrix4x4.TRS(drop.position, rot, Vector3.one * 0.4f);

        // Se for uma stack inteira (ex: 3 peles), adiciona multiplos (para simplificar, criamos multiplas instnacias visuais)
        for(int i=0; i<amount; i++)
        {
            droppedItems.Add(drop);
            // Re-offset para o proximo do mesmo batch
            drop.position += new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));
            drop.matrix = Matrix4x4.TRS(drop.position, rot, Vector3.one * 0.4f);
        }

        UpdateMatrices();
    }

    private void Update()
    {
        RenderLoot();
        
        // Scan para apanhar se o jogador estiver perto (Apenas 1 por frame para nao travar)
        TryPickupNearby();
    }

    private void TryPickupNearby()
    {
        if (playerTransform == null || inventory == null || droppedItems.Count == 0) return;

        // Otimizacao: Só verifica se houver movimento ou a cada N frames
        if (Time.frameCount % 5 != 0) return;

        Vector3 pPos = playerTransform.position;
        float radiusSq = pickupRadius * pickupRadius;
        float tooltipRadiusSq = (pickupRadius * 2f) * (pickupRadius * 2f); // Raio de aviso visual

        float closestDistSq = float.MaxValue;
        int closestItemID = -1;

        for (int i = droppedItems.Count - 1; i >= 0; i--)
        {
            Vector3 diff = droppedItems[i].position - pPos;
            float distSq = diff.sqrMagnitude;

            // Logica do Tooltip: Guardar o mais proximo
            if (distSq < tooltipRadiusSq && distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closestItemID = droppedItems[i].itemID;
            }

            // Logica de Pickup
            if (distSq <= radiusSq)
            {
                // Tenta adicionar ao inventario
                int leftover = inventory.AddItem(droppedItems[i].itemID, 1);
                
                if (leftover == 0)
                {
                    // Sucesso a apanhar
                    Debug.Log($"[LootManager] Apanhou {ItemDatabase.GetItem(droppedItems[i].itemID).itemName}");
                    droppedItems.RemoveAt(i);
                    UpdateMatrices();
                    break; // Apanha 1 por tick para gerar satisfacao e som fluido
                }
            }
        }

        // --- PREMIUM AAA: Tooltip Dinamico no UI ---
        if (uiCache != null)
        {
            if (closestItemID != -1)
            {
                ItemData data = ItemDatabase.GetItem(closestItemID);
                uiCache.SetLootTooltip(data != null ? $"[ {data.itemName} ]" : "");
            }
            else
            {
                uiCache.SetLootTooltip(""); // Limpar
            }
        }
    }

    private void UpdateMatrices()
    {
        for (int i = 0; i < droppedItems.Count; i++)
        {
            renderMatrices[i] = droppedItems[i].matrix;
        }
    }

    private void RenderLoot()
    {
        if (droppedItems.Count > 0 && sackMesh != null && sackMaterial != null)
        {
            Graphics.DrawMeshInstanced(sackMesh, 0, sackMaterial, renderMatrices, droppedItems.Count,
                null, UnityEngine.Rendering.ShadowCastingMode.On, false, 0, null, 
                UnityEngine.Rendering.LightProbeUsage.Off);
        }
    }
}
