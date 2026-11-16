using UnityEngine;
using System.Collections.Generic;

/// CraftingSystem: Sistema unificado de fabricacao com Tiers (Mao Livre, Fogueira, Bancada, Caldeirao),
/// suporte a fabricacao em lote e queda segura de excedentes no chao caso a mochila encha.

public enum CraftingStationType
{
    Hand = 0,       // Sempre disponivel na mochila
    Bonfire = 1,    // Perto de fogueira acesa
    Workbench = 2,  // Perto da Mesa de Trabalho (tile 13)
    Cauldron = 3    // Perto do Caldeirao (tile 15)
}

[System.Serializable]
public class CraftRecipe
{
    public string recipeName;
    public int outputItemId;
    public int outputAmount;
    public int[] ingredientIds;
    public int[] ingredientAmounts;
    public CraftingStationType requiredStation = CraftingStationType.Hand;
    public float craftDuration = 0.6f;

    public bool requiresBonfire => requiredStation == CraftingStationType.Bonfire;
    public bool requiresWorkbench => requiredStation == CraftingStationType.Workbench;
    public bool requiresCauldron => requiredStation == CraftingStationType.Cauldron;
}

public class CraftingSystem : MonoBehaviour
{
    public static CraftingSystem Instance { get; private set; }

    [Header("Dependencies")]
    public InventoryAdvanced inventory;
    public PlayerStatus playerStatus;
    public WorkbenchSystem workbenchSystem;
    public GameManager gameManager;

    private readonly List<CraftRecipe> recipes = new List<CraftRecipe>();

    public event System.Action<CraftRecipe> OnCraftSuccess;
    public event System.Action<string> OnCraftFailed;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        RegisterAllRecipes();
    }

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();
        if (workbenchSystem == null) workbenchSystem = FindAnyObjectByType<WorkbenchSystem>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
    }

    private void RegisterAllRecipes()
    {
        recipes.Clear();

        // === TIER 0: MAOS LIVRES (MOCHILA) ===
        AddRecipe("Machado de Pedra", 300, 1, new[] { 100, 101, 118 }, new[] { 3, 2, 1 }, CraftingStationType.Hand, 0.7f);
        AddRecipe("Corda de Fibra (x2)", 118, 2, new[] { 103 }, new[] { 2 }, CraftingStationType.Hand, 0.4f);
        AddRecipe("Tecido Rustico (x2)", 121, 2, new[] { 103 }, new[] { 3 }, CraftingStationType.Hand, 0.5f);
        AddRecipe("Clava de Osso", 311, 1, new[] { 120, 100, 118 }, new[] { 4, 2, 1 }, CraftingStationType.Hand, 0.9f);
        AddRecipe("Tocha Selvagem", 303, 1, new[] { 100, 119 }, new[] { 2, 1 }, CraftingStationType.Hand, 0.5f);
        AddRecipe("Kit de Fogueira", 400, 1, new[] { 100, 101 }, new[] { 3, 2 }, CraftingStationType.Hand, 0.6f);
        AddRecipe("Ligaduras Medicas (x2)", 206, 2, new[] { 103, 121 }, new[] { 2, 1 }, CraftingStationType.Hand, 0.5f);
        AddRecipe("Pomada de Resina", 213, 1, new[] { 103, 119, 121 }, new[] { 2, 1, 1 }, CraftingStationType.Hand, 0.7f);
        AddRecipe("Tala de Madeira", 209, 1, new[] { 100, 118 }, new[] { 3, 1 }, CraftingStationType.Hand, 0.6f);
        AddRecipe("Circulo de Sal", 402, 1, new[] { 106 }, new[] { 3 }, CraftingStationType.Hand, 0.5f);
        AddRecipe("Composto de Sementes (x2)", 112, 2, new[] { 108, 100 }, new[] { 2, 1 }, CraftingStationType.Hand, 0.6f);
        AddRecipe("Encher Cantil (Agua Limpa)", 210, 1, new[] { 210, 109 }, new[] { 1, 1 }, CraftingStationType.Hand, 0.4f);
        AddRecipe("Mesa de Trabalho", 404, 1, new[] { 100, 101 }, new[] { 6, 4 }, CraftingStationType.Hand, 1.2f);
        AddRecipe("Laco para Coelhos", 406, 1, new[] { 100, 118 }, new[] { 2, 2 }, CraftingStationType.Hand, 0.7f);

        // === TIER 1: FOGUEIRA ===
        AddRecipe("Carne Assada", 200, 1, new[] { 104 }, new[] { 1 }, CraftingStationType.Bonfire, 0.9f);
        AddRecipe("Coelho Assado", 200, 1, new[] { 113 }, new[] { 1 }, CraftingStationType.Bonfire, 0.8f);
        AddRecipe("Peixe Assado", 207, 1, new[] { 111 }, new[] { 1 }, CraftingStationType.Bonfire, 0.8f);
        AddRecipe("Ferver Agua Limpa", 109, 1, new[] { 123 }, new[] { 1 }, CraftingStationType.Bonfire, 0.7f);
        AddRecipe("Carvao Vegetal (x3)", 122, 3, new[] { 100 }, new[] { 2 }, CraftingStationType.Bonfire, 0.9f);
        AddRecipe("Cha de Ervas Quente", 211, 1, new[] { 109, 103 }, new[] { 1, 2 }, CraftingStationType.Bonfire, 0.8f);
        AddRecipe("Charque Salgado (x2)", 212, 2, new[] { 104, 106 }, new[] { 1, 1 }, CraftingStationType.Bonfire, 1.0f);
        AddRecipe("Charque de Coelho (x2)", 212, 2, new[] { 113, 106 }, new[] { 2, 1 }, CraftingStationType.Bonfire, 0.9f);
        AddRecipe("Sopa de Ervas", 201, 1, new[] { 103, 104 }, new[] { 3, 1 }, CraftingStationType.Bonfire, 1.0f);

        // === TIER 2: MESA DE TRABALHO (BANCADA) ===
        AddRecipe("Picareta de Pedra", 301, 1, new[] { 100, 101, 118 }, new[] { 3, 4, 1 }, CraftingStationType.Workbench, 1.0f);
        AddRecipe("Faca de Caca", 302, 1, new[] { 101, 107, 118 }, new[] { 1, 2, 1 }, CraftingStationType.Workbench, 1.0f);
        AddRecipe("Machado de Ferro", 312, 1, new[] { 100, 107, 118, 122 }, new[] { 2, 4, 2, 2 }, CraftingStationType.Workbench, 1.4f);
        AddRecipe("Lanca Celta", 308, 1, new[] { 100, 107, 118 }, new[] { 3, 2, 2 }, CraftingStationType.Workbench, 1.1f);
        AddRecipe("Escudo de Madeira", 309, 1, new[] { 100, 107, 105 }, new[] { 5, 2, 1 }, CraftingStationType.Workbench, 1.2f);
        AddRecipe("Arco de Sobrevivencia", 305, 1, new[] { 100, 118 }, new[] { 4, 3 }, CraftingStationType.Workbench, 1.3f);
        AddRecipe("Flechas de Pedra (x5)", 110, 5, new[] { 100, 101 }, new[] { 2, 2 }, CraftingStationType.Workbench, 0.7f);
        AddRecipe("Flechas Penadas (x5)", 117, 5, new[] { 100, 107, 114 }, new[] { 2, 1, 3 }, CraftingStationType.Workbench, 0.8f);
        AddRecipe("Vara de Pesca", 306, 1, new[] { 100, 118 }, new[] { 3, 2 }, CraftingStationType.Workbench, 0.9f);
        AddRecipe("Enxada Rustica", 307, 1, new[] { 100, 101, 107 }, new[] { 3, 2, 1 }, CraftingStationType.Workbench, 0.9f);
        AddRecipe("Cantil de Couro", 210, 1, new[] { 105, 118, 121 }, new[] { 2, 1, 1 }, CraftingStationType.Workbench, 1.0f);
        AddRecipe("Filtrar Agua c/ Carvao", 109, 1, new[] { 123, 122 }, new[] { 1, 1 }, CraftingStationType.Workbench, 0.7f);
        AddRecipe("Lampiao Reforcado", 304, 1, new[] { 107, 102 }, new[] { 4, 2 }, CraftingStationType.Workbench, 1.4f);
        AddRecipe("Casaco de Pele", 310, 1, new[] { 105, 118 }, new[] { 4, 3 }, CraftingStationType.Workbench, 1.5f);
        AddRecipe("Armadura de Ossos", 313, 1, new[] { 120, 105, 118 }, new[] { 6, 2, 2 }, CraftingStationType.Workbench, 1.6f);
        AddRecipe("Fosforos de Resina (x4)", 205, 4, new[] { 100, 119, 122 }, new[] { 1, 1, 1 }, CraftingStationType.Workbench, 0.7f);
        AddRecipe("Sal Mineral (x3)", 106, 3, new[] { 101, 122 }, new[] { 2, 1 }, CraftingStationType.Workbench, 0.8f);
        AddRecipe("Armadilha de Caca Pesada", 401, 1, new[] { 100, 107 }, new[] { 3, 3 }, CraftingStationType.Workbench, 1.0f);
        AddRecipe("Coletor de Agua", 403, 1, new[] { 100, 105 }, new[] { 4, 2 }, CraftingStationType.Workbench, 1.1f);
        AddRecipe("Caldeirao Druida", 405, 1, new[] { 107, 101 }, new[] { 5, 3 }, CraftingStationType.Workbench, 1.4f);

        // === TIER 3: CALDEIRAO ===
        AddRecipe("Agua Benta", 204, 1, new[] { 106, 103, 109 }, new[] { 3, 3, 1 }, CraftingStationType.Cauldron, 1.2f);
        AddRecipe("Tonico da Fraga", 214, 1, new[] { 202, 103, 109 }, new[] { 3, 2, 1 }, CraftingStationType.Cauldron, 1.1f);
        AddRecipe("Ensopado Druida", 208, 1, new[] { 111, 203, 109, 103 }, new[] { 1, 1, 1, 2 }, CraftingStationType.Cauldron, 1.6f);
        AddRecipe("Oleo Refinado (Gordura)", 102, 2, new[] { 104, 103 }, new[] { 2, 2 }, CraftingStationType.Cauldron, 1.1f);
        AddRecipe("Oleo de Resina (x2)", 102, 2, new[] { 119, 122 }, new[] { 2, 1 }, CraftingStationType.Cauldron, 1.0f);
    }

    private void AddRecipe(string name, int outputId, int outputAmount, int[] ingIds, int[] ingAmounts, CraftingStationType station, float duration)
    {
        recipes.Add(new CraftRecipe
        {
            recipeName = name,
            outputItemId = outputId,
            outputAmount = outputAmount,
            ingredientIds = ingIds,
            ingredientAmounts = ingAmounts,
            requiredStation = station,
            craftDuration = duration
        });
    }

    public bool IsNearStation(CraftingStationType station)
    {
        switch (station)
        {
            case CraftingStationType.Hand:
                return true;
            case CraftingStationType.Bonfire:
                return IsNearBonfire();
            case CraftingStationType.Workbench:
                return (workbenchSystem != null && workbenchSystem.IsNearWorkbench()) || IsNearNativeTile(13, 3);
            case CraftingStationType.Cauldron:
                return IsNearNativeTile(15, 3);
            default:
                return true;
        }
    }

    private bool IsNearBonfire()
    {
        if (playerStatus != null && playerStatus.isNearBonfire) return true;
        return IsNearNativeTile(4, 3);
    }

    private bool IsNearNativeTile(int tileId, int radius)
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        Transform pt = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        if (pt == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return false;

        int pX = Mathf.RoundToInt(pt.position.x);
        int pZ = Mathf.RoundToInt(pt.position.z);
        for (int x = pX - radius; x <= pX + radius; x++)
        {
            for (int z = pZ - radius; z <= pZ + radius; z++)
            {
                if (x >= 0 && x < gameManager.mapWidth && z >= 0 && z < gameManager.mapHeight)
                {
                    if (ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, z) == tileId)
                        return true;
                }
            }
        }
        return false;
    }

    public bool CanCraft(int recipeIndex) => CanCraft(recipeIndex, out _);

    public bool CanCraft(int recipeIndex, out string reason)
    {
        reason = string.Empty;
        if (recipeIndex < 0 || recipeIndex >= recipes.Count) { reason = "Receita invalida"; return false; }
        if (inventory == null) { reason = "Sem inventario"; return false; }

        CraftRecipe recipe = recipes[recipeIndex];

        if (!IsNearStation(recipe.requiredStation))
        {
            switch (recipe.requiredStation)
            {
                case CraftingStationType.Bonfire: reason = "Requer Fogueira proxima"; break;
                case CraftingStationType.Workbench: reason = "Requer Mesa de Trabalho"; break;
                case CraftingStationType.Cauldron: reason = "Requer Caldeirao"; break;
            }
            return false;
        }

        for (int i = 0; i < recipe.ingredientIds.Length; i++)
        {
            if (inventory.GetItemCount(recipe.ingredientIds[i]) < recipe.ingredientAmounts[i])
            {
                reason = "Materiais insuficientes";
                return false;
            }
        }

        return true;
    }

    public bool Craft(int recipeIndex, int batchCount = 1)
    {
        batchCount = Mathf.Max(1, batchCount);
        bool craftedAny = false;
        int droppedOverflow = 0;

        for (int b = 0; b < batchCount; b++)
        {
            if (!CanCraft(recipeIndex, out string reason))
            {
                if (!craftedAny) OnCraftFailed?.Invoke(reason);
                break;
            }

            CraftRecipe recipe = recipes[recipeIndex];
            for (int i = 0; i < recipe.ingredientIds.Length; i++)
            {
                inventory.RemoveItem(recipe.ingredientIds[i], recipe.ingredientAmounts[i]);
            }

            int leftover = inventory.AddItem(recipe.outputItemId, recipe.outputAmount);
            if (leftover > 0 && LootManager.Instance != null && PlayerController.Instance != null)
            {
                Vector3 dropPos = PlayerController.Instance.transform.position + PlayerController.Instance.transform.forward * 1.1f + Vector3.up * 0.25f;
                LootManager.Instance.DropItem(recipe.outputItemId, leftover, dropPos);
                droppedOverflow += leftover;
            }

            craftedAny = true;
            OnCraftSuccess?.Invoke(recipe);
        }

        if (craftedAny)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
            if (VFXPoolManager.Instance != null && PlayerController.Instance != null)
                VFXPoolManager.Instance.PlayBuildPlaceVFX(PlayerController.Instance.transform.position);
            if (droppedOverflow > 0 && HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Mochila cheia! +{droppedOverflow} item(ns) soltos no chao.");
        }

        return craftedAny;
    }

    public List<CraftRecipe> GetAllRecipes() => recipes;

    public CraftRecipe GetRecipe(int index)
    {
        if (index >= 0 && index < recipes.Count) return recipes[index];
        return null;
    }

    public int GetRecipeCount() => recipes.Count;
}
