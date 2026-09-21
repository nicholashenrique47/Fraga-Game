using UnityEngine;

public class WorkbenchSystem : MonoBehaviour
{
    public Transform playerTransform;
    public GameManager gameManager;
    
    // Lista de itens que so podem ser craftados na Workbench (Tier 2)
    // 301 - Picareta, 305 - Arco, 306 - Vara de Pesca, 401 - Armadilha
    
    public bool IsNearWorkbench()
    {
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return false;
        
        int pX = Mathf.RoundToInt(playerTransform.position.x);
        int pY = Mathf.RoundToInt(playerTransform.position.z);
        
        for (int x = pX - 3; x <= pX + 3; x++)
        {
            for (int y = pY - 3; y <= pY + 3; y++)
            {
                if (x >= 0 && x < gameManager.mapWidth && y >= 0 && y < gameManager.mapHeight)
                {
                    int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, y);
                    if (tile == 13) // Workbench
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
