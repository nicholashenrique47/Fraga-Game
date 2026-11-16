using UnityEngine;

public class CampfireCookingSystem : MonoBehaviour
{
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public GameManager gameManager;

    [Header("Settings")]
    public float interactRange = 2.5f;

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
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero || inventory == null) return;

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

        bool cookedSomething = false;

        if (isNearCauldron && inventory.HasItem(111) && inventory.HasItem(203) && inventory.HasItem(109))
        {
            inventory.RemoveItem(111, 1);
            inventory.RemoveItem(203, 1);
            inventory.RemoveItem(109, 1);
            inventory.AddItem(208, 1);
            cookedSomething = true;
            ShowMsg("Preparou: Ensopado Druida!");
        }
        else if (inventory.HasItem(104))
        {
            inventory.RemoveItem(104, 1);
            inventory.AddItem(200, 1);
            cookedSomething = true;
            ShowMsg("Assou: Carne Assada!");
        }
        else if (inventory.HasItem(113))
        {
            inventory.RemoveItem(113, 1);
            inventory.AddItem(200, 1);
            cookedSomething = true;
            ShowMsg("Assou: Carne de Coelho!");
        }
        else if (inventory.HasItem(111))
        {
            inventory.RemoveItem(111, 1);
            inventory.AddItem(207, 1);
            cookedSomething = true;
            ShowMsg("Assou: Peixe Assado!");
        }
        else if (inventory.HasItem(123))
        {
            inventory.RemoveItem(123, 1);
            inventory.AddItem(109, 1);
            cookedSomething = true;
            ShowMsg("Ferveu: Agua Limpa purificada!");
        }
        else if (inventory.HasItem(109) && inventory.GetItemCount(103) >= 2)
        {
            inventory.RemoveItem(109, 1);
            inventory.RemoveItem(103, 2);
            inventory.AddItem(211, 1);
            cookedSomething = true;
            ShowMsg("Preparou: Cha de Ervas Quente!");
        }

        if (cookedSomething)
        {
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(playerTransform.position);
        }
        else
        {
            ShowMsg("Sem carne ou peixe cru na mochila!");
        }
    }

    private void ShowMsg(string msg)
    {
        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
        else if (uiCache != null) uiCache.SetLootTooltip(msg);
    }
}
