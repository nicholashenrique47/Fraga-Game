using UnityEngine;
using System.Collections.Generic;

public class WaterCatcherSystem : MonoBehaviour
{
    public GameManager gameManager;
    public InventoryAdvanced inventory;
    
    private float waterGenerationTimer = 0f;
    private float waterGenerationRate = 60f; // 1 Agua Limpa a cada 60s por coletor
    
    private void Update()
    {
        waterGenerationTimer += Time.deltaTime;
        if (waterGenerationTimer >= waterGenerationRate)
        {
            GenerateWater();
            waterGenerationTimer = 0f;
        }
    }
    
    private void GenerateWater()
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero || inventory == null) return;
        
        int catchersCount = 0;
        
        // Em um jogo gigante fariamos cache das coordenadas. Por simplificacao, vamos percorrer um raio ao redor do jogador ou cachear quando o MapRenderer instancia.
        // Como FragaNative gere os tiles globalmente, podemos procurar Tile 12 numa area 50x50 em torno do player.
        Transform p = FindAnyObjectByType<PlayerController>()?.transform;
        if (p == null) return;
        
        int pX = Mathf.RoundToInt(p.position.x);
        int pY = Mathf.RoundToInt(p.position.z);
        
        for (int x = pX - 25; x <= pX + 25; x++)
        {
            for (int y = pY - 25; y <= pY + 25; y++)
            {
                if (x >= 0 && x < gameManager.mapWidth && y >= 0 && y < gameManager.mapHeight)
                {
                    int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, y);
                    if (tile == 12) // Water Catcher
                    {
                        catchersCount++;
                    }
                }
            }
        }
        
        if (catchersCount > 0)
        {
            int waterGained = catchersCount;
            inventory.AddItem(109, waterGained); // ID 109 = Agua Limpa
            Debug.Log($"[WaterCatcher] Coletores geraram {waterGained} de Agua Limpa.");
            
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip($"+{waterGained} Agua Filtrada!");
        }
    }
}
