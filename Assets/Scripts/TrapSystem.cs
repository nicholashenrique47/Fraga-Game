using UnityEngine;
using System.Collections.Generic;

public class TrapSystem : MonoBehaviour
{
    private GameManager gameManager;
    private FaunaManager faunaManager;
    private VultoManager vultoManager;
    
    // Lista de entidades presas (ID, tempo restante)
    // Para simplificar no contexto C#, vamos usar dicionarios 
    // Mapeando a posicao da grid presa ou usar o array de Vultos/Lobos.
    
    private void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        faunaManager = FindAnyObjectByType<FaunaManager>();
        vultoManager = FindAnyObjectByType<VultoManager>();
    }
    
    private void Update()
    {
        if (faunaManager != null)
        {
            CheckTrapsForRabbits(faunaManager.GetRabbitPosX(), faunaManager.GetRabbitPosY(), faunaManager.maxRabbits);
        }
    }
    
    public void CheckTrapsForWolves(float[] wolfPosX, float[] wolfPosY, int maxWolves)
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;
        
        for (int i = 0; i < maxWolves; i++)
        {
            int gridX = Mathf.RoundToInt(wolfPosX[i]);
            int gridY = Mathf.RoundToInt(wolfPosY[i]);
            
            if (gridX >= 0 && gridX < gameManager.mapWidth && gridY >= 0 && gridY < gameManager.mapHeight)
            {
                int tileID = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, gridX, gridY);
                if (tileID == 5) // Armadilha!
                {
                    Debug.Log($"[TrapSystem] Lobo preso na armadilha em {gridX}, {gridY}!");
                    
                    // Danificar o lobo massivamente
                    faunaManager.TryDamageFaunaAt(new Vector3(wolfPosX[i], 0f, wolfPosY[i]), 100f);
                    
                    // Consumir Armadilha
                    gameManager.UpdateMapCellNative(gridX, gridY, 0);
                }
            }
        }
    }
    
    public void CheckTrapsForVultos(float[] posXArray, float[] posYArray, int activeVultos)
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;
        
        for (int i = 0; i < activeVultos; i++)
        {
            int gridX = Mathf.RoundToInt(posXArray[i]);
            int gridY = Mathf.RoundToInt(posYArray[i]);
            
            if (gridX >= 0 && gridX < gameManager.mapWidth && gridY >= 0 && gridY < gameManager.mapHeight)
            {
                int tileID = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, gridX, gridY);
                if (tileID == 5) // Armadilha!
                {
                    Debug.Log($"[TrapSystem] Vulto preso na armadilha em {gridX}, {gridY}!");
                    
                    // Danificar vulto
                    vultoManager.TryDamageVultoAt(new Vector3(posXArray[i], 0f, posYArray[i]), 100f);
                    
                    // Consumir Armadilha
                    gameManager.UpdateMapCellNative(gridX, gridY, 0);
                }
            }
        }
    }
    
    public void CheckTrapsForRabbits(float[] posXArray, float[] posYArray, int maxRabbits)
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;
        
        for (int i = 0; i < maxRabbits; i++)
        {
            int gridX = Mathf.RoundToInt(posXArray[i]);
            int gridY = Mathf.RoundToInt(posYArray[i]);
            
            if (gridX >= 0 && gridX < gameManager.mapWidth && gridY >= 0 && gridY < gameManager.mapHeight)
            {
                int tileID = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, gridX, gridY);
                if (tileID == 16) // Snare Trap!
                {
                    Debug.Log($"[TrapSystem] Coelho apanhado no laco em {gridX}, {gridY}!");
                    
                    // Capturar coelho (largar carne)
                    faunaManager.CatchRabbit(i);
                    
                    // Consumir Snare
                    gameManager.UpdateMapCellNative(gridX, gridY, 0);
                    
                    UIManager ui = FindAnyObjectByType<UIManager>();
                    if (ui != null) ui.SetLootTooltip("Um laco capturou um Coelho!");
                }
            }
        }
    }
}
