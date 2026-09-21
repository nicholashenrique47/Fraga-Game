using UnityEngine;
using System.Runtime.InteropServices;

public class ResourceManager : MonoBehaviour
{
    // Acesso P/Invoke ao C++ (Data-Oriented Access)
    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetMapCell(System.IntPtr mapData, int width, int height, int x, int y);

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UpdateMapCell(System.IntPtr mapData, int width, int height, int x, int y, int newTileID);

    [Header("Dependencies")]
    public float interactionRange = 1.5f;
    public Transform playerTransform;
    public InventoryAdvanced inventory;

    // Referencias do Mapa
    public System.IntPtr MapDataPtr { get; private set; }
    public int MapWidth { get; private set; } = 100;
    public int MapHeight { get; private set; } = 100;

    public void Initialize(System.IntPtr ptr, int width, int height)
    {
        MapDataPtr = ptr;
        MapWidth = width;
        MapHeight = height;
    }

    private void Update()
    {
        // Num jogo mobile, isto seria ativado por um botao UI "Interact" ou tap na grelha adjacente
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryHarvest();
        }
    }

    private System.Collections.Generic.Dictionary<Vector2Int, int> resourceHealth = new System.Collections.Generic.Dictionary<Vector2Int, int>();

    public void TryHarvest()
    {
        if (playerTransform == null || MapDataPtr == System.IntPtr.Zero || inventory == null) return;

        // Posicao da celula imediatamente a frente do Iago
        Vector3 interactPos = playerTransform.position + playerTransform.forward * interactionRange;
        int gridX = Mathf.RoundToInt(interactPos.x);
        int gridY = Mathf.RoundToInt(interactPos.z);

        // Leitura otimizada O(1) diretamente da DLL Nativa
        int tileID = GetMapCell(MapDataPtr, MapWidth, MapHeight, gridX, gridY);
        
        UIManager ui = FindAnyObjectByType<UIManager>();
        Vector2Int posKey = new Vector2Int(gridX, gridY);

        if (tileID == 1) // 1 = Arvore (Madeira) - Max Health 3
        {
            if (!resourceHealth.ContainsKey(posKey)) resourceHealth[posKey] = 3;

            int amount = 1;
            string bonusText = "";

            // Ferramenta Passiva: Machado Rudimentar (ID 300)
            if (inventory.HasItem(300)) 
            {
                amount *= 2;
                bonusText = " (Bonus Machado)";
            }

            inventory.AddItem(100, amount); // 100 = Madeira
            
            resourceHealth[posKey]--;
            if (resourceHealth[posKey] <= 0)
            {
                UpdateMapCell(MapDataPtr, MapWidth, MapHeight, gridX, gridY, 0); // Torna em clareira
                resourceHealth.Remove(posKey);
                
                // --- NOVO: Sincronizar GPU Instancing (Evita Arvore Fantasma) ---
                MapRenderer mapRenderer = FindAnyObjectByType<MapRenderer>();
                if (mapRenderer != null) mapRenderer.RemoveInstanceAt(gridX, gridY, 1);

                if (ui != null) ui.SetLootTooltip($"+{amount} Madeira{bonusText} [Arvore Destruida]");
            }
            else
            {
                if (ui != null) ui.SetLootTooltip($"+{amount} Madeira{bonusText} [Hits Restantes: {resourceHealth[posKey]}]");
            }
            
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(interactPos); // Particulas de madeira
        }
        else if (tileID == 2) // 2 = Ruina Celta Antiga (Pedra e Ferro) - Max Health 4
        {
            if (!resourceHealth.ContainsKey(posKey)) resourceHealth[posKey] = 4;

            int amount = 1;
            int ironAmount = Random.Range(0, 100) < 30 ? 1 : 0; // 30% de chance por hit
            string bonusText = "";

            // Ferramenta Passiva: Picareta de Pedra (ID 301)
            if (inventory.HasItem(301)) 
            {
                amount *= 2;
                if (ironAmount > 0) ironAmount *= 2;
                bonusText = " (Bonus Picareta)";
            }

            inventory.AddItem(101, amount); // 101 = Pedra
            if (ironAmount > 0) inventory.AddItem(107, ironAmount); // 107 = Ferro Velho

            resourceHealth[posKey]--;
            if (resourceHealth[posKey] <= 0)
            {
                UpdateMapCell(MapDataPtr, MapWidth, MapHeight, gridX, gridY, 0); 
                resourceHealth.Remove(posKey);

                // --- NOVO: Sincronizar GPU Instancing (Evita Ruina Fantasma) ---
                MapRenderer mapRenderer = FindAnyObjectByType<MapRenderer>();
                if (mapRenderer != null) mapRenderer.RemoveInstanceAt(gridX, gridY, 2);

                if (ui != null) ui.SetLootTooltip($"+{amount} Pedra, +{ironAmount} Ferro{bonusText} [Ruina Destruida]");
            }
            else
            {
                if (ui != null) 
                {
                    if (ironAmount > 0) ui.SetLootTooltip($"+{amount} Pedra, +{ironAmount} Ferro{bonusText} [Hits Restantes: {resourceHealth[posKey]}]");
                    else ui.SetLootTooltip($"+{amount} Pedra{bonusText} [Hits Restantes: {resourceHealth[posKey]}]");
                }
            }
            
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(interactPos);
        }
        else
        {
            if (ui != null) ui.SetLootTooltip(""); // Limpar se bater no ar
        }
    }
}

