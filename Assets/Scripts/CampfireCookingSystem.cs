using UnityEngine;

public class CampfireCookingSystem : MonoBehaviour
{
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public GameManager gameManager;
    
    [Header("Settings")]
    public float interactRange = 2.5f;

    private void Update()
    {
        // PC Shortcut for cooking
        if (Input.GetKeyDown(KeyCode.C))
        {
            TryCookFood();
        }
    }
    
    // Chamado pela UI de Crafting ou Botao de Interacao
    public void TryCookFood()
    {
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero || inventory == null) return;
        
        // Verifica se ha fogueira ou caldeirao perto
        bool isNearCampfire = false;
        bool isNearCauldron = false;
        
        int pX = Mathf.RoundToInt(playerTransform.position.x);
        int pY = Mathf.RoundToInt(playerTransform.position.z);
        
        for (int x = pX - 2; x <= pX + 2; x++)
        {
            for (int y = pY - 2; y <= pY + 2; y++)
            {
                if (x >= 0 && x < gameManager.mapWidth && y >= 0 && y < gameManager.mapHeight)
                {
                    int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, y);
                    if (tile == 4) isNearCampfire = true;
                    if (tile == 15) isNearCauldron = true;
                }
            }
        }
        
        if (!isNearCampfire && !isNearCauldron)
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Precisas de Fogo!");
            Debug.Log("[Cooking] Sem fogueira ou caldeirao por perto.");
            return;
        }
        
        bool cookedSomething = false;

        // Receita Druida (Requer Caldeirao)
        if (isNearCauldron && inventory.HasItem(111) && inventory.HasItem(203) && inventory.HasItem(109))
        {
            inventory.RemoveItem(111, 1);
            inventory.RemoveItem(203, 1);
            inventory.RemoveItem(109, 1);
            inventory.AddItem(208, 1); // Ensopado Druida
            cookedSomething = true;
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Fez: Ensopado Druida!");
            Debug.Log("[Cooking] Ensopado feito com sucesso.");
        }
        else if (inventory.HasItem(104))
        {
            inventory.RemoveItem(104, 1);
            inventory.AddItem(200, 1);
            cookedSomething = true;
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Cozinhou: Carne Assada!");
            Debug.Log("[Cooking] Carne Assada cozinhada com sucesso.");
        }
        
        // Converte Carne Leve (113) em Carne Assada (200) - Pode ser no mesmo output ou um novo. Vamos usar 200.
        else if (inventory.HasItem(113))
        {
            inventory.RemoveItem(113, 1);
            inventory.AddItem(200, 1);
            cookedSomething = true;
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Cozinhou: Carne Assada (Coelho)!");
            Debug.Log("[Cooking] Coelho Assado com sucesso.");
        }
        
        // Converte Peixe Cru (111) em Peixe Assado (207)
        else if (inventory.HasItem(111))
        {
            inventory.RemoveItem(111, 1);
            inventory.AddItem(207, 1);
            cookedSomething = true;
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Cozinhou: Peixe Assado!");
            Debug.Log("[Cooking] Peixe Assado cozinhado com sucesso.");
        }
        
        if (cookedSomething)
        {
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(playerTransform.position);
        }
        else
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Sem comida crua!");
            Debug.Log("[Cooking] Nao tens comida crua para cozinhar.");
        }
    }
}
