using UnityEngine;
using System.Collections.Generic;

public class FarmingSystem : MonoBehaviour
{
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public GameManager gameManager;
    public MapRenderer mapRenderer;
    public DayNightCycle dayNightCycle;
    
    [System.Serializable]
    public class FarmPlot
    {
        public int x, y;
        public float plantTime;
        public bool isGrown;
    }
    
    private List<FarmPlot> activePlots = new List<FarmPlot>();

    private void Update()
    {
        // PC Shortcut for Farming
        if (Input.GetKeyDown(KeyCode.T))
        {
            TryTillOrPlant();
        }
        
        CheckGrowth();
    }
    
    public void TryTillOrPlant()
    {
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero || inventory == null) return;
        
        int pX = Mathf.RoundToInt(playerTransform.position.x);
        int pY = Mathf.RoundToInt(playerTransform.position.z + 1f); // O tile a frente do player
        
        int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, pX, pY);
        
        EquipmentSystem eq = playerTransform.GetComponent<EquipmentSystem>();
        
        if (tile == 0) // Terra normal
        {
            // Tentar lavrar a terra
            if (inventory.HasItem(307)) // Enxada Rustica
            {
                gameManager.UpdateMapCellNative(pX, pY, 14); // 14 = Terra Fertil
                if (mapRenderer != null) mapRenderer.AddInstanceAt(pX, pY, 14);
                
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(pX, 0.1f, pY));
                
                UIManager ui = FindAnyObjectByType<UIManager>();
                if (ui != null) ui.SetLootTooltip("Terra Fértil criada.");
            }
        }
        else if (tile == 14) // Terra Fertil
        {
            // Tentar plantar
            if (inventory.HasItem(112)) // Sementes
            {
                inventory.RemoveItem(112, 1);
                
                FarmPlot plot = new FarmPlot { x = pX, y = pY, plantTime = dayNightCycle.currentTime, isGrown = false };
                activePlots.Add(plot);
                
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(pX, 0.1f, pY));
                
                UIManager ui = FindAnyObjectByType<UIManager>();
                if (ui != null) ui.SetLootTooltip("Sementes plantadas.");
            }
        }
    }
    
    private void CheckGrowth()
    {
        if (dayNightCycle == null) return;
        
        for (int i = activePlots.Count - 1; i >= 0; i--)
        {
            FarmPlot p = activePlots[i];
            if (!p.isGrown)
            {
                // Cresce passado 1 dia (24h in game time)
                float timePassed = dayNightCycle.currentTime - p.plantTime;
                if (timePassed < 0) timePassed += 24f; // Virou o dia
                
                if (timePassed > 12f) // Demora metade de um ciclo do dia para crescer
                {
                    p.isGrown = true;
                    gameManager.UpdateMapCellNative(p.x, p.y, 9); // Substitui a terra por Arbusto de Bagas (9)
                    if (mapRenderer != null)
                    {
                        mapRenderer.RemoveInstanceAt(p.x, p.y, 14);
                        mapRenderer.AddInstanceAt(p.x, p.y, 9);
                    }
                    activePlots.RemoveAt(i);
                }
            }
        }
    }
}
