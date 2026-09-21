using UnityEngine;

public class FishingSystem : MonoBehaviour
{
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public GameManager gameManager;
    
    private float lastFishTime = 0f;
    private float fishCooldown = 5f; // Demora 5s para pescar um peixe

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            TryFish();
        }
    }
    
    public void TryFish()
    {
        if (Time.time - lastFishTime < fishCooldown)
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Espera que o peixe morda o isco...");
            return;
        }

        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero || inventory == null) return;
        
        EquipmentSystem eq = playerTransform.GetComponent<EquipmentSystem>();
        if (eq != null && !inventory.HasItem(306)) // O player precisa da vara no inventario ou equipada (para simplificar, checa inventario)
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Precisas de uma Vara de Pesca!");
            return;
        }
        
        // Verifica se ha lago (Tile 11) perto
        bool isNearPond = false;
        int pX = Mathf.RoundToInt(playerTransform.position.x);
        int pY = Mathf.RoundToInt(playerTransform.position.z);
        
        for (int x = pX - 2; x <= pX + 2; x++)
        {
            for (int y = pY - 2; y <= pY + 2; y++)
            {
                if (x >= 0 && x < gameManager.mapWidth && y >= 0 && y < gameManager.mapHeight)
                {
                    int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, y);
                    if (tile == 11) // Pond
                    {
                        isNearPond = true;
                        break;
                    }
                }
            }
            if (isNearPond) break;
        }
        
        if (isNearPond)
        {
            inventory.AddItem(111, 1); // Apanha Peixe Cru
            lastFishTime = Time.time;
            
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Pescaste um Peixe Cru!");
            
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(playerTransform.position + playerTransform.forward * 2f);
            Debug.Log("[Fishing] Peixe apanhado com sucesso.");
        }
        else
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("Nao ha lagos por perto.");
        }
    }
}
