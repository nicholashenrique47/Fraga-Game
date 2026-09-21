using UnityEngine;
using System.Collections.Generic;

/// CraftingSystem: Sistema de crafting baseado em receitas.
/// Cada receita consome ingredientes do InventoryAdvanced e produz um novo item.
/// Receitas sao desbloqueadas progressivamente (algumas requerem fogueira proxima).

[System.Serializable]
public class CraftRecipe
{
    public string recipeName;
    public int outputItemId;
    public int outputAmount;
    public int[] ingredientIds;
    public int[] ingredientAmounts;
    public bool requiresBonfire;    // Precisa estar perto de fogueira para cozinhar
}

public class CraftingSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public InventoryAdvanced inventory;
    public PlayerStatus playerStatus;

    // Receitas registadas
    private List<CraftRecipe> recipes = new List<CraftRecipe>();

    // Eventos
    public event System.Action<CraftRecipe> OnCraftSuccess;
    public event System.Action<string> OnCraftFailed;

    private void Awake()
    {
        RegisterAllRecipes();
    }

    private void RegisterAllRecipes()
    {
        // === FERRAMENTAS ===
        AddRecipe("Machado Rudimentar", 300, 1,
            new int[] { 100, 101 },     // Madeira, Pedra
            new int[] { 5, 3 },
            false);

        AddRecipe("Picareta de Pedra", 301, 1,
            new int[] { 100, 101 },
            new int[] { 3, 5 },
            false);

        AddRecipe("Faca de Caca", 302, 1,
            new int[] { 101, 107 },     // Pedra, Ferro Velho
            new int[] { 2, 3 },
            false);

        AddRecipe("Tocha", 303, 2,
            new int[] { 100, 102 },     // Madeira, Oleo
            new int[] { 2, 1 },
            false);

        // === COMIDA (Requer Fogueira) ===
        AddRecipe("Carne Assada", 200, 1,
            new int[] { 104 },          // Carne Crua
            new int[] { 1 },
            true);

        AddRecipe("Sopa de Ervas", 201, 1,
            new int[] { 103, 104 },     // Ervas, Carne Crua (caldo)
            new int[] { 3, 1 },
            true);

        // === CONSUMIVEIS ===
        AddRecipe("Circulo de Sal", 402, 1,
            new int[] { 106 },          // Sal
            new int[] { 3 },
            false);

        AddRecipe("Kit de Fogueira", 400, 1,
            new int[] { 100, 102 },     // Madeira, Oleo
            new int[] { 5, 2 },
            false);

        AddRecipe("Armadilha de Caca", 401, 2,
            new int[] { 100, 107 },     // Madeira, Ferro Velho
            new int[] { 3, 2 },
            false);

        // === AVANCADOS ===
        AddRecipe("Agua Benta", 204, 1,
            new int[] { 106, 103 },     // Sal, Ervas
            new int[] { 5, 5 },
            true);
    }

    private void AddRecipe(string name, int outputId, int outputAmount, int[] ingIds, int[] ingAmounts, bool needsFire)
    {
        recipes.Add(new CraftRecipe
        {
            recipeName = name,
            outputItemId = outputId,
            outputAmount = outputAmount,
            ingredientIds = ingIds,
            ingredientAmounts = ingAmounts,
            requiresBonfire = needsFire
        });
    }

    /// Verifica se uma receita pode ser craftada (ingredientes suficientes + condicoes).
    public bool CanCraft(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= recipes.Count) return false;
        if (inventory == null) return false;

        CraftRecipe recipe = recipes[recipeIndex];

        // Verificar fogueira
        if (recipe.requiresBonfire && playerStatus != null && !playerStatus.isNearBonfire)
        {
            return false;
        }

        // Verificar ingredientes
        for (int i = 0; i < recipe.ingredientIds.Length; i++)
        {
            if (inventory.GetItemCount(recipe.ingredientIds[i]) < recipe.ingredientAmounts[i])
            {
                return false;
            }
        }

        return true;
    }

    /// Executa o craft: consome ingredientes e adiciona o output.
    public bool Craft(int recipeIndex)
    {
        if (!CanCraft(recipeIndex))
        {
            CraftRecipe failedRecipe = GetRecipe(recipeIndex);
            string reason = "Ingredientes insuficientes";
            if (failedRecipe != null && failedRecipe.requiresBonfire && playerStatus != null && !playerStatus.isNearBonfire)
            {
                reason = "Precisa estar perto de uma fogueira";
            }
            if (OnCraftFailed != null) OnCraftFailed.Invoke(reason);
            Debug.LogWarning($"[Crafting] Falhou: {reason}");
            return false;
        }

        CraftRecipe recipe = recipes[recipeIndex];

        // Consumir ingredientes
        for (int i = 0; i < recipe.ingredientIds.Length; i++)
        {
            inventory.RemoveItem(recipe.ingredientIds[i], recipe.ingredientAmounts[i]);
        }

        // Produzir output
        int overflow = inventory.AddItem(recipe.outputItemId, recipe.outputAmount);
        if (overflow > 0)
        {
            Debug.LogWarning($"[Crafting] Inventario cheio. {overflow}x {recipe.recipeName} perdidos.");
        }

        if (OnCraftSuccess != null) OnCraftSuccess.Invoke(recipe);

        ItemData outputData = ItemDatabase.GetItem(recipe.outputItemId);
        Debug.Log($"[Crafting] Craftado: {recipe.outputAmount}x {(outputData != null ? outputData.itemName : "???")}");

        // VFX
        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position);
        }

        return true;
    }

    /// Acesso publico para a UI listar receitas.
    public List<CraftRecipe> GetAllRecipes() { return recipes; }
    public CraftRecipe GetRecipe(int index)
    {
        if (index >= 0 && index < recipes.Count) return recipes[index];
        return null;
    }
    public int GetRecipeCount() { return recipes.Count; }
}
