using UnityEngine;

public class CampfireCookingSystem : MonoBehaviour
{
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public GameManager gameManager;

    [Header("Settings")]
    public float interactRange = 3.5f;

    private UIManager uiCache;

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        uiCache = FindAnyObjectByType<UIManager>();
    }

    // Chamado pelo InteractionDetector ou pela aba de Fabricacao
    public void TryCookFood()
    {
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (playerTransform == null || inventory == null) return;

        // Prioriza BonfireEntity fisica no mundo (grelha 3D diegetica)
        BonfireEntity nearestBonfire = FindNearestBonfire(playerTransform.position, interactRange + 1.2f);
        if (nearestBonfire != null)
        {
            if (!nearestBonfire.isLit)
            {
                ShowMsg("Fogueira apagada! Acenda com Fosforos, Tocha ou Lampiao [L].");
                return;
            }

            if (nearestBonfire.ReadySlotCount > 0)
            {
                nearestBonfire.TryCollectReadyItems(inventory, out string collectSummary);
                ShowMsg(collectSummary);
                return;
            }

            if (nearestBonfire.TryPlaceCookItemFromInventory(inventory, -1, out string placeMsg))
            {
                ShowMsg(placeMsg);
                return;
            }

            ShowMsg(placeMsg);
            return;
        }

        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;

        bool isNearCampfire = false;
        bool isNearCauldron = false;

        int pX = Mathf.RoundToInt(playerTransform.position.x);
        int pY = Mathf.RoundToInt(playerTransform.position.z);

        for (int x = pX - 3; x <= pX + 3; x++)
        {
            for (int y = pY - 3; y <= pY + 3; y++)
            {
                if (x >= 0 && x < gameManager.mapWidth && y >= 0 && y < gameManager.mapHeight)
                {
                    int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, y);
                    if (tile == 4) isNearCampfire = true;
                    if (tile == 15) isNearCauldron = true;
                }
            }
        }

        if (!isNearCampfire && !isNearCauldron && CraftingSystem.Instance != null)
        {
            if (CraftingSystem.Instance.IsNearStation(CraftingStationType.Cauldron)) isNearCauldron = true;
            if (CraftingSystem.Instance.IsNearStation(CraftingStationType.Bonfire)) isNearCampfire = true;
        }

        if (!isNearCampfire && !isNearCauldron && PlayerStatus.Instance != null && PlayerStatus.Instance.isNearBonfire)
        {
            isNearCampfire = true;
        }

        if (!isNearCampfire && !isNearCauldron)
        {
            ShowMsg("Aproxime-se de uma Fogueira ou Caldeirao!");
            return;
        }

        if (isNearCauldron && inventory.HasItem(111) && inventory.HasItem(203) && inventory.HasItem(109))
        {
            inventory.RemoveItem(111, 1);
            inventory.RemoveItem(203, 1);
            inventory.RemoveItem(109, 1);
            AddOrDrop(208, 1);
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(playerTransform.position);
            ShowMsg("Preparou: Ensopado Druida!");
            return;
        }

        if (SurvivalInventoryCraftUI.Instance != null)
        {
            SurvivalInventoryCraftUI.Instance.OpenCraftingAtStation(isNearCauldron ? CraftingStationType.Cauldron : CraftingStationType.Bonfire);
            return;
        }

        ShowMsg("Use a grelha da fogueira ou a aba de Fabricacao.");
    }

    private static BonfireEntity FindNearestBonfire(Vector3 pos, float maxRadius)
    {
        BonfireEntity best = null;
        float bestSq = maxRadius * maxRadius;
        for (int i = 0; i < BonfireEntity.AllBonfires.Count; i++)
        {
            BonfireEntity bf = BonfireEntity.AllBonfires[i];
            if (bf == null) continue;
            float dSq = (bf.transform.position - pos).sqrMagnitude;
            if (dSq <= bestSq)
            {
                bestSq = dSq;
                best = bf;
            }
        }
        return best;
    }

    private void AddOrDrop(int itemId, int amount)
    {
        if (inventory == null || amount <= 0) return;
        int leftover = inventory.AddItem(itemId, amount);
        if (leftover > 0 && LootManager.Instance != null && playerTransform != null)
        {
            LootManager.Instance.DropItem(itemId, leftover, playerTransform.position + playerTransform.forward + Vector3.up * 0.5f);
        }
    }

    private void ShowMsg(string msg)
    {
        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
        else if (uiCache != null) uiCache.SetLootTooltip(msg);
    }
}
