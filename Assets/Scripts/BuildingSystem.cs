using UnityEngine;

public class BuildingSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerTransform;
    public GameManager gameManager;
    public InventoryAdvanced inventory;
    
    [Header("Visuals")]
    public GameObject hologramPrefab; // Cubo semi-transparente
    
    private GameObject currentHologram;
    private MeshRenderer hologramRenderer;
    private Material hologramMaterial;
    
    private bool isBuildModeActive = false;
    private int currentBuildingTileID = 0; // 3 = Barricada, 4 = Fogueira
    
    private System.IntPtr mapDataPtr;
    private int mapWidth;
    private int mapHeight;

    public void Initialize(System.IntPtr ptr, int width, int height)
    {
        mapDataPtr = ptr;
        mapWidth = width;
        mapHeight = height;
    }

    private void Update()
    {
        if (!isBuildModeActive || playerTransform == null || mapDataPtr == System.IntPtr.Zero) return;

        UpdateHologramPosition();

        // Confirmar construcao
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            TryBuild();
        }
        
        // Cancelar construcao
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            ExitBuildMode();
        }
    }

    public void EnterBuildMode(int tileID)
    {
        currentBuildingTileID = tileID;
        isBuildModeActive = true;

        if (currentHologram == null && hologramPrefab != null)
        {
            currentHologram = Instantiate(hologramPrefab);
            hologramRenderer = currentHologram.GetComponent<MeshRenderer>();
            
            if (hologramRenderer != null)
            {
                hologramMaterial = new Material(hologramRenderer.sharedMaterial);
                hologramRenderer.material = hologramMaterial;
            }
        }
        
        if (currentHologram != null) currentHologram.SetActive(true);
        Debug.Log($"[BuildingSystem] Modo Construcao para Tile {tileID}");
    }

    public void ExitBuildMode()
    {
        isBuildModeActive = false;
        if (currentHologram != null) currentHologram.SetActive(false);
    }

    private Vector2Int GetTargetGridPosition()
    {
        Vector3 forward = playerTransform.forward;
        Vector3 targetPos = playerTransform.position + forward * 1.5f; 
        
        int gridX = Mathf.RoundToInt(targetPos.x);
        int gridY = Mathf.RoundToInt(targetPos.z);
        
        return new Vector2Int(gridX, gridY);
    }

    private void UpdateHologramPosition()
    {
        if (currentHologram == null) return;

        Vector2Int gridPos = GetTargetGridPosition();
        currentHologram.transform.position = new Vector3(gridPos.x, 0.5f, gridPos.y);

        bool canBuild = CanBuildAt(gridPos.x, gridPos.y);

        if (hologramMaterial != null)
        {
            hologramMaterial.color = canBuild ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
        }
    }

    private bool CanBuildAt(int x, int y)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return false;
        
        int currentTile = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, x, y);
        return currentTile == 0 || currentTile == 2; // 0 = clareira, 2 = ruinas
    }

    private void TryBuild()
    {
        Vector2Int gridPos = GetTargetGridPosition();

        if (CanBuildAt(gridPos.x, gridPos.y))
        {
            // Deduzir os items
            if (currentBuildingTileID == 3) // Barricada
            {
                if (inventory != null && inventory.GetItemCount(100) >= 2) // Madeira
                {
                    inventory.RemoveItem(100, 2);
                }
                else
                {
                    UIManager ui = FindAnyObjectByType<UIManager>();
                    if (ui != null) ui.SetLootTooltip("Sem Madeira suficiente (2)");
                    Debug.LogWarning("[BuildingSystem] Sem madeira suficiente (2)!");
                    return;
                }
            }
            else if (currentBuildingTileID == 4) // Fogueira
            {
                if (inventory != null && inventory.GetItemCount(101) >= 2 && inventory.GetItemCount(100) >= 1) // Pedra e Madeira
                {
                    inventory.RemoveItem(101, 2);
                    inventory.RemoveItem(100, 1);
                }
                else
                {
                    UIManager ui = FindAnyObjectByType<UIManager>();
                    if (ui != null) ui.SetLootTooltip("Faltam recursos (2 Pedra, 1 Madeira)");
                    Debug.LogWarning("[BuildingSystem] Sem pedra/madeira suficiente!");
                    return;
                }
            }
            else if (currentBuildingTileID == 5) // Armadilha de Urso
            {
                if (inventory != null && inventory.GetItemCount(101) >= 1 && inventory.GetItemCount(100) >= 2) // 1 Pedra e 2 Madeira
                {
                    inventory.RemoveItem(101, 1);
                    inventory.RemoveItem(100, 2);
                }
                else
                {
                    UIManager ui = FindAnyObjectByType<UIManager>();
                    if (ui != null) ui.SetLootTooltip("Faltam recursos (1 Pedra, 2 Madeira)");
                    Debug.LogWarning("[BuildingSystem] Faltam recursos para Armadilha!");
                    return;
                }
            }

            gameManager.UpdateMapCellNative(gridPos.x, gridPos.y, currentBuildingTileID);
            
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(gridPos.x, 0f, gridPos.y));

            AudioManager am = FindAnyObjectByType<AudioManager>();
            if (am != null) am.PlayHorrorSting(); // Placeholder sound for building

            ExitBuildMode();
        }
        else
        {
            Debug.LogWarning("[BuildingSystem] Local invalido para construir.");
        }
    }

    public bool ForceBuild(int tileID)
    {
        // Wrapper simples para evitar erros de compilacao dos scripts antigos
        Vector2Int pos = new Vector2Int(Mathf.RoundToInt(playerTransform.position.x), Mathf.RoundToInt(playerTransform.position.z));
        if (gameManager != null)
        {
            gameManager.UpdateMapCellNative(pos.x, pos.y, tileID);
            return true;
        }
        return false;
    }
}
