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

    private readonly List<DroppedItem> droppedItems = new List<DroppedItem>();
    private readonly Matrix4x4[] renderMatrices = new Matrix4x4[1023]; // Limite do GPU Instancing por batch

    public static LootManager Instance;
    private UIManager uiCache;
    private AudioManager audioCache;
    private bool hadNearbyTooltip = false;

    private void Awake()
    {
        Instance = this;
        uiCache = FindAnyObjectByType<UIManager>();
        audioCache = AudioManager.Instance ?? FindAnyObjectByType<AudioManager>();

        if (sackMesh == null)
        {
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sackMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);
        }

        if (sackMaterial == null)
        {
            sackMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            sackMaterial.color = new Color(0.6f, 0.4f, 0.2f);
            sackMaterial.enableInstancing = true;
        }
    }

    private void Start()
    {
        if (playerTransform == null && PlayerController.Instance != null)
            playerTransform = PlayerController.Instance.transform;
        if (inventory == null)
            inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
    }

    /// Larga um item no chao do mapa (Ex: Cervo morto, ou dropar do inventario)
    public void DropItem(int itemID, int amount, Vector3 pos)
    {
        if (amount <= 0) return;
        if (droppedItems.Count >= 1023)
        {
            Debug.LogWarning("[LootManager] Maximo de itens no chao atingido.");
            return;
        }

        DroppedItem drop = new DroppedItem();
        drop.itemID = itemID;

        Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0.2f, Random.Range(-0.5f, 0.5f));
        drop.position = pos + randomOffset;

        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        drop.matrix = Matrix4x4.TRS(drop.position, rot, Vector3.one * 0.4f);

        for (int i = 0; i < amount; i++)
        {
            if (droppedItems.Count >= 1023) break;
            droppedItems.Add(drop);
            drop.position += new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f));
            drop.matrix = Matrix4x4.TRS(drop.position, rot, Vector3.one * 0.4f);
        }

        UpdateMatrices();
    }

    private void Update()
    {
        RenderLoot();
        TryPickupNearby();
    }

    private void TryPickupNearby()
    {
        if (playerTransform == null && PlayerController.Instance != null)
            playerTransform = PlayerController.Instance.transform;
        if (inventory == null)
            inventory = InventoryAdvanced.Instance;

        if (playerTransform == null || inventory == null || droppedItems.Count == 0)
        {
            if (hadNearbyTooltip && uiCache != null)
            {
                uiCache.SetLootTooltip("");
                hadNearbyTooltip = false;
            }
            return;
        }

        if (Time.frameCount % 5 != 0) return;

        Vector3 pPos = playerTransform.position;
        float radiusSq = pickupRadius * pickupRadius;
        float tooltipRadiusSq = (pickupRadius * 2f) * (pickupRadius * 2f);

        float closestDistSq = float.MaxValue;
        int closestItemID = -1;
        bool pickedUpAny = false;

        for (int i = droppedItems.Count - 1; i >= 0; i--)
        {
            Vector3 diff = droppedItems[i].position - pPos;
            float distSq = diff.sqrMagnitude;

            if (distSq < tooltipRadiusSq && distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closestItemID = droppedItems[i].itemID;
            }

            if (distSq <= radiusSq)
            {
                int itemId = droppedItems[i].itemID;
                int leftover = inventory.AddItem(itemId, 1);

                if (leftover == 0)
                {
                    ItemData pickedData = ItemDatabase.GetItem(itemId);
                    string msg = $"+1 {(pickedData != null ? pickedData.itemName : "Item")}";

                    if (audioCache == null) audioCache = AudioManager.Instance;
                    if (audioCache != null) audioCache.PlayPickupSound();

                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
                    else if (uiCache != null)
                        uiCache.SetLootTooltip(msg);

                    droppedItems.RemoveAt(i);
                    UpdateMatrices();
                    pickedUpAny = true;
                    hadNearbyTooltip = false;
                    break;
                }
            }
        }

        if (!pickedUpAny && uiCache != null)
        {
            if (closestItemID != -1)
            {
                ItemData data = ItemDatabase.GetItem(closestItemID);
                uiCache.SetLootTooltip(data != null ? $"[ {data.itemName} ]" : "");
                hadNearbyTooltip = true;
            }
            else if (hadNearbyTooltip)
            {
                uiCache.SetLootTooltip("");
                hadNearbyTooltip = false;
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
