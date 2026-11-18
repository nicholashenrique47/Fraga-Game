using UnityEngine;
using System.Collections.Generic;

/// CraftingSystem: Sistema unificado de fabricacao com Tiers (Mao Livre, Fogueira, Bancada, Caldeirao),
/// fila cronometrada cancelavel com reembolso integral de materiais, suporte a fabricacao em lote
/// e queda segura de excedentes no chao caso a mochila encha.

public enum CraftingStationType
{
    Hand = 0,       // Sempre disponivel na mochila
    Bonfire = 1,    // Perto de fogueira acesa
    Workbench = 2,  // Perto da Mesa de Trabalho (tile 13)
    Cauldron = 3    // Perto do Caldeirao (tile 15)
}

/// <summary>
/// Marcador leve para estacoes instanciadas fora ou sobre o grid (acampamento, blueprints ou deployables).
/// Mantem registro estatico Zero-GC para CraftingSystem e InteractionDetector.
/// </summary>
public class CraftingStationMarker : MonoBehaviour
{
    public static readonly List<CraftingStationMarker> AllMarkers = new List<CraftingStationMarker>();

    public CraftingStationType stationType = CraftingStationType.Workbench;

    private void OnEnable()
    {
        if (!AllMarkers.Contains(this)) AllMarkers.Add(this);
    }

    private void OnDisable()
    {
        AllMarkers.Remove(this);
    }
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
    public float craftDuration = 0.9f;

    public bool requiresBonfire => requiredStation == CraftingStationType.Bonfire;
    public bool requiresWorkbench => requiredStation == CraftingStationType.Workbench;
    public bool requiresCauldron => requiredStation == CraftingStationType.Cauldron;
}

[System.Serializable]
public class CraftQueueJob
{
    public int recipeIndex;
    public string recipeName;
    public int outputItemId;
    public int outputAmount;
    public int[] ingredientIds;
    public int[] ingredientAmounts;
    public CraftingStationType requiredStation;
    public bool isCanteenRefill;
    public float duration;
    public float remaining;

    public float Progress01 => duration > 0.001f ? Mathf.Clamp01(1f - (remaining / duration)) : 1f;
}

public class CraftingSystem : MonoBehaviour
{
    public static CraftingSystem Instance { get; private set; }

    public const int MaxCraftQueueSize = 8;

    [Header("Dependencies")]
    public InventoryAdvanced inventory;
    public PlayerStatus playerStatus;
    public WorkbenchSystem workbenchSystem;
    public GameManager gameManager;

    private readonly List<CraftRecipe> recipes = new List<CraftRecipe>();
    private readonly List<CraftQueueJob> craftQueue = new List<CraftQueueJob>();

    public IReadOnlyList<CraftQueueJob> CraftQueue => craftQueue;
    public bool HasQueuedJobs => craftQueue.Count > 0;
    public CraftQueueJob CurrentJob => craftQueue.Count > 0 ? craftQueue[0] : null;
    public float CurrentJobProgress01 => craftQueue.Count > 0 ? craftQueue[0].Progress01 : 0f;
    public bool IsCurrentJobPausedByStation { get; private set; }

    public event System.Action<CraftRecipe> OnCraftSuccess;
    public event System.Action<string> OnCraftFailed;
    public event System.Action OnCraftQueueChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        RegisterAllRecipes();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();
        if (workbenchSystem == null) workbenchSystem = FindAnyObjectByType<WorkbenchSystem>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
    }

    private void Update()
    {
        if (craftQueue.Count == 0 || Time.timeScale <= 0f) return;
        AdvanceCraftQueue(Time.deltaTime);
    }

    private void RegisterAllRecipes()
    {
        recipes.Clear();

        // === TIER 0: MAOS LIVRES (MOCHILA) ===
        AddRecipe("Machado de Pedra", 300, 1, new[] { 100, 101, 118 }, new[] { 3, 2, 1 }, CraftingStationType.Hand, 1.5f);
        AddRecipe("Funda Celta", 314, 1, new[] { 105, 118 }, new[] { 1, 2 }, CraftingStationType.Hand, 1.4f);
        AddRecipe("Corda de Fibra (x2)", 118, 2, new[] { 103 }, new[] { 2 }, CraftingStationType.Hand, 0.9f);
        AddRecipe("Tecido Rustico (x2)", 121, 2, new[] { 103 }, new[] { 3 }, CraftingStationType.Hand, 1.1f);
        AddRecipe("Clava de Osso", 311, 1, new[] { 120, 100, 118 }, new[] { 4, 2, 1 }, CraftingStationType.Hand, 1.8f);
        AddRecipe("Tocha Selvagem", 303, 1, new[] { 100, 119 }, new[] { 2, 1 }, CraftingStationType.Hand, 1.0f);
        AddRecipe("Kit de Fogueira", 400, 1, new[] { 100, 101 }, new[] { 3, 2 }, CraftingStationType.Hand, 1.4f);
        AddRecipe("Ataduras Medicas (x2)", 206, 2, new[] { 103, 121 }, new[] { 2, 1 }, CraftingStationType.Hand, 1.0f);
        AddRecipe("Pomada de Resina", 213, 1, new[] { 103, 119, 121 }, new[] { 2, 1, 1 }, CraftingStationType.Hand, 1.4f);
        AddRecipe("Tala de Madeira", 209, 1, new[] { 100, 118 }, new[] { 3, 1 }, CraftingStationType.Hand, 1.2f);
        AddRecipe("Circulo de Sal", 402, 1, new[] { 106 }, new[] { 3 }, CraftingStationType.Hand, 1.1f);
        AddRecipe("Composto de Sementes (x2)", 112, 2, new[] { 108, 100 }, new[] { 2, 1 }, CraftingStationType.Hand, 1.2f);
        AddRecipe("Encher Cantil (Agua Limpa)", 210, 1, new[] { 210, 109 }, new[] { 1, 1 }, CraftingStationType.Hand, 0.8f);
        AddRecipe("Mesa de Trabalho", 404, 1, new[] { 100, 101 }, new[] { 6, 4 }, CraftingStationType.Hand, 2.2f);
        AddRecipe("Laco para Coelhos", 406, 1, new[] { 100, 118 }, new[] { 2, 2 }, CraftingStationType.Hand, 1.3f);

        // === TIER 1: FOGUEIRA ===
        AddRecipe("Carne Assada", 200, 1, new[] { 104 }, new[] { 1 }, CraftingStationType.Bonfire, 2.0f);
        AddRecipe("Coelho Assado", 200, 1, new[] { 113 }, new[] { 1 }, CraftingStationType.Bonfire, 1.8f);
        AddRecipe("Peixe Assado", 207, 1, new[] { 111 }, new[] { 1 }, CraftingStationType.Bonfire, 1.8f);
        AddRecipe("Ferver Agua Limpa", 109, 1, new[] { 123 }, new[] { 1 }, CraftingStationType.Bonfire, 1.5f);
        AddRecipe("Carvao Vegetal (x3)", 122, 3, new[] { 100 }, new[] { 2 }, CraftingStationType.Bonfire, 2.1f);
        AddRecipe("Cha de Ervas Quente", 211, 1, new[] { 109, 103 }, new[] { 1, 2 }, CraftingStationType.Bonfire, 1.9f);
        AddRecipe("Charque Salgado (x2)", 212, 2, new[] { 104, 106 }, new[] { 1, 1 }, CraftingStationType.Bonfire, 2.2f);
        AddRecipe("Charque de Coelho (x2)", 212, 2, new[] { 113, 106 }, new[] { 2, 1 }, CraftingStationType.Bonfire, 2.0f);
        AddRecipe("Sopa de Ervas", 201, 1, new[] { 103, 104 }, new[] { 3, 1 }, CraftingStationType.Bonfire, 2.4f);

        // === TIER 2: MESA DE TRABALHO (BANCADA) ===
        AddRecipe("Picareta de Pedra", 301, 1, new[] { 100, 101, 118 }, new[] { 3, 4, 1 }, CraftingStationType.Workbench, 1.8f);
        AddRecipe("Faca de Caca", 302, 1, new[] { 101, 107, 118 }, new[] { 1, 2, 1 }, CraftingStationType.Workbench, 1.8f);
        AddRecipe("Machado de Ferro", 312, 1, new[] { 100, 107, 118, 122 }, new[] { 2, 4, 2, 2 }, CraftingStationType.Workbench, 2.6f);
        AddRecipe("Lanca Celta", 308, 1, new[] { 100, 107, 118 }, new[] { 3, 2, 2 }, CraftingStationType.Workbench, 2.1f);
        AddRecipe("Escudo de Madeira", 309, 1, new[] { 100, 107, 105 }, new[] { 5, 2, 1 }, CraftingStationType.Workbench, 2.2f);
        AddRecipe("Arco de Sobrevivencia", 305, 1, new[] { 100, 118 }, new[] { 4, 3 }, CraftingStationType.Workbench, 2.4f);
        AddRecipe("Flechas de Pedra (x5)", 110, 5, new[] { 100, 101 }, new[] { 2, 2 }, CraftingStationType.Workbench, 1.2f);
        AddRecipe("Flechas Penadas (x5)", 117, 5, new[] { 100, 107, 114 }, new[] { 2, 1, 3 }, CraftingStationType.Workbench, 1.4f);
        AddRecipe("Vara de Pesca", 306, 1, new[] { 100, 118 }, new[] { 3, 2 }, CraftingStationType.Workbench, 1.6f);
        AddRecipe("Enxada Rustica", 307, 1, new[] { 100, 101, 107 }, new[] { 3, 2, 1 }, CraftingStationType.Workbench, 1.7f);
        AddRecipe("Cantil de Couro", 210, 1, new[] { 105, 118, 121 }, new[] { 2, 1, 1 }, CraftingStationType.Workbench, 1.8f);
        AddRecipe("Filtrar Agua c/ Carvao", 109, 1, new[] { 123, 122 }, new[] { 1, 1 }, CraftingStationType.Workbench, 1.2f);
        AddRecipe("Lampiao Reforcado", 304, 1, new[] { 107, 102 }, new[] { 4, 2 }, CraftingStationType.Workbench, 2.5f);
        AddRecipe("Casaco de Pele", 310, 1, new[] { 105, 118 }, new[] { 4, 3 }, CraftingStationType.Workbench, 2.7f);
        AddRecipe("Armadura de Ossos", 313, 1, new[] { 120, 105, 118 }, new[] { 6, 2, 2 }, CraftingStationType.Workbench, 3.0f);
        AddRecipe("Fosforos de Resina (x4)", 205, 4, new[] { 100, 119, 122 }, new[] { 1, 1, 1 }, CraftingStationType.Workbench, 1.2f);
        AddRecipe("Sal Mineral (x3)", 106, 3, new[] { 101, 122 }, new[] { 2, 1 }, CraftingStationType.Workbench, 1.4f);
        AddRecipe("Armadilha de Caca Pesada", 401, 1, new[] { 100, 107 }, new[] { 3, 3 }, CraftingStationType.Workbench, 1.9f);
        AddRecipe("Coletor de Agua", 403, 1, new[] { 100, 105 }, new[] { 4, 2 }, CraftingStationType.Workbench, 2.1f);
        AddRecipe("Caldeirao Druida", 405, 1, new[] { 107, 101 }, new[] { 5, 3 }, CraftingStationType.Workbench, 2.6f);

        // === TIER 3: CALDEIRAO ===
        AddRecipe("Agua Benta", 204, 1, new[] { 106, 103, 109 }, new[] { 3, 3, 1 }, CraftingStationType.Cauldron, 2.4f);
        AddRecipe("Tonico da Fraga", 214, 1, new[] { 202, 103, 109 }, new[] { 3, 2, 1 }, CraftingStationType.Cauldron, 2.2f);
        AddRecipe("Ensopado Druida", 208, 1, new[] { 111, 203, 109, 103 }, new[] { 1, 1, 1, 2 }, CraftingStationType.Cauldron, 3.0f);
        AddRecipe("Oleo Refinado (Gordura)", 102, 2, new[] { 104, 103 }, new[] { 2, 2 }, CraftingStationType.Cauldron, 2.1f);
        AddRecipe("Oleo de Resina (x2)", 102, 2, new[] { 119, 122 }, new[] { 2, 1 }, CraftingStationType.Cauldron, 1.9f);
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
                return (workbenchSystem != null && workbenchSystem.IsNearWorkbench())
                    || IsNearNativeTile(13, 3)
                    || IsNearStationMarker(CraftingStationType.Workbench, 4.4f);
            case CraftingStationType.Cauldron:
                return IsNearNativeTile(15, 3)
                    || IsNearStationMarker(CraftingStationType.Cauldron, 4.4f);
            default:
                return true;
        }
    }

    private bool IsNearStationMarker(CraftingStationType stationType, float radius)
    {
        Transform pt = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        if (pt == null) return false;
        float rSq = radius * radius;
        Vector3 p = pt.position;
        for (int i = 0; i < CraftingStationMarker.AllMarkers.Count; i++)
        {
            CraftingStationMarker m = CraftingStationMarker.AllMarkers[i];
            if (m == null || m.stationType != stationType) continue;
            Vector3 d = m.transform.position - p;
            d.y = 0f;
            if (d.sqrMagnitude <= rSq) return true;
        }
        return false;
    }

    private bool IsNearBonfire()
    {
        if (playerStatus != null && playerStatus.isNearBonfire) return true;

        Transform pt = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        if (pt != null)
        {
            Vector3 p = pt.position;
            for (int i = 0; i < BonfireEntity.AllBonfires.Count; i++)
            {
                BonfireEntity bf = BonfireEntity.AllBonfires[i];
                if (bf != null && bf.isLit && (bf.transform.position - p).sqrMagnitude <= 25f)
                    return true;
            }
        }

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
                case CraftingStationType.Bonfire: reason = "Requer Fogueira acesa proxima"; break;
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

        if (recipe.outputItemId == 210 && recipe.ingredientIds.Length > 0 && recipe.ingredientIds[0] == 210)
        {
            if (!inventory.HasRefillableCanteen())
            {
                reason = "Cantil ja esta cheio (3/3)";
                return false;
            }
        }

        return true;
    }

    public int GetMaxCraftableCount(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= recipes.Count || inventory == null) return 0;
        CraftRecipe recipe = recipes[recipeIndex];
        if (!IsNearStation(recipe.requiredStation)) return 0;

        if (recipe.outputItemId == 210 && recipe.ingredientIds.Length > 0 && recipe.ingredientIds[0] == 210)
        {
            return (inventory.HasRefillableCanteen() && inventory.GetItemCount(109) >= 1) ? 1 : 0;
        }

        int maxBatches = int.MaxValue;
        for (int i = 0; i < recipe.ingredientIds.Length; i++)
        {
            int need = Mathf.Max(1, recipe.ingredientAmounts[i]);
            int have = inventory.GetItemCount(recipe.ingredientIds[i]);
            maxBatches = Mathf.Min(maxBatches, have / need);
            if (maxBatches == 0) return 0;
        }
        return maxBatches == int.MaxValue ? 0 : maxBatches;
    }

    private static bool IsCanteenRefillRecipe(CraftRecipe recipe)
    {
        return recipe != null && recipe.outputItemId == 210 && recipe.ingredientIds.Length > 0 && recipe.ingredientIds[0] == 210;
    }

    private bool HasQueuedCanteenRefill()
    {
        for (int i = 0; i < craftQueue.Count; i++)
        {
            if (craftQueue[i].isCanteenRefill) return true;
        }
        return false;
    }

    /// <summary>
    /// Enfileira fabricacao cronometrada em tempo real (com reserva imediata e reembolso 100% ao cancelar).
    /// </summary>
    public bool EnqueueCraft(int recipeIndex, int batchCount, out string failReason)
    {
        failReason = string.Empty;
        batchCount = Mathf.Max(1, batchCount);
        int enqueuedCount = 0;

        for (int b = 0; b < batchCount; b++)
        {
            if (craftQueue.Count >= MaxCraftQueueSize)
            {
                if (enqueuedCount == 0)
                {
                    failReason = $"Fila de fabricacao cheia (max {MaxCraftQueueSize})";
                    OnCraftFailed?.Invoke(failReason);
                }
                break;
            }

            if (!CanCraft(recipeIndex, out string stepReason))
            {
                if (enqueuedCount == 0)
                {
                    failReason = stepReason;
                    OnCraftFailed?.Invoke(stepReason);
                }
                break;
            }

            CraftRecipe recipe = recipes[recipeIndex];
            bool canteenRefill = IsCanteenRefillRecipe(recipe);
            if (canteenRefill && HasQueuedCanteenRefill())
            {
                if (enqueuedCount == 0)
                {
                    failReason = "Recarga do cantil ja esta na fila";
                    OnCraftFailed?.Invoke(failReason);
                }
                break;
            }

            int[] consumedIds;
            int[] consumedAmounts;

            if (canteenRefill)
            {
                inventory.RemoveItem(109, 1);
                consumedIds = new[] { 109 };
                consumedAmounts = new[] { 1 };
            }
            else
            {
                consumedIds = new int[recipe.ingredientIds.Length];
                consumedAmounts = new int[recipe.ingredientAmounts.Length];
                for (int i = 0; i < recipe.ingredientIds.Length; i++)
                {
                    consumedIds[i] = recipe.ingredientIds[i];
                    consumedAmounts[i] = recipe.ingredientAmounts[i];
                    inventory.RemoveItem(consumedIds[i], consumedAmounts[i]);
                }
            }

            float dur = Mathf.Max(0.25f, recipe.craftDuration);
            craftQueue.Add(new CraftQueueJob
            {
                recipeIndex = recipeIndex,
                recipeName = recipe.recipeName,
                outputItemId = recipe.outputItemId,
                outputAmount = recipe.outputAmount,
                ingredientIds = consumedIds,
                ingredientAmounts = consumedAmounts,
                requiredStation = recipe.requiredStation,
                isCanteenRefill = canteenRefill,
                duration = dur,
                remaining = dur
            });
            enqueuedCount++;
        }

        if (enqueuedCount > 0)
        {
            OnCraftQueueChanged?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Avanca a fila de fabricacao em segundos (chamado por Update e disponivel para simulacao deterministica).
    /// </summary>
    public void AdvanceCraftQueue(float deltaTime)
    {
        if (craftQueue.Count == 0 || deltaTime <= 0f)
        {
            if (craftQueue.Count == 0) IsCurrentJobPausedByStation = false;
            return;
        }

        float dtRemaining = deltaTime;
        bool queueChanged = false;

        while (craftQueue.Count > 0 && dtRemaining > 0.0001f)
        {
            CraftQueueJob active = craftQueue[0];
            if (active.requiredStation != CraftingStationType.Hand && !IsNearStation(active.requiredStation))
            {
                if (!IsCurrentJobPausedByStation)
                {
                    IsCurrentJobPausedByStation = true;
                    queueChanged = true;
                }
                break;
            }

            if (IsCurrentJobPausedByStation)
            {
                IsCurrentJobPausedByStation = false;
                queueChanged = true;
            }

            if (dtRemaining < active.remaining)
            {
                active.remaining -= dtRemaining;
                dtRemaining = 0f;
            }
            else
            {
                dtRemaining -= active.remaining;
                active.remaining = 0f;
                craftQueue.RemoveAt(0);
                DeliverJobOutput(active);
                queueChanged = true;
            }
        }

        if (craftQueue.Count == 0) IsCurrentJobPausedByStation = false;
        if (queueChanged) OnCraftQueueChanged?.Invoke();
    }

    public bool CancelQueuedJob(int queueIndex, bool silent = false)
    {
        if (queueIndex < 0 || queueIndex >= craftQueue.Count) return false;
        CraftQueueJob job = craftQueue[queueIndex];
        craftQueue.RemoveAt(queueIndex);
        RefundJobIngredients(job);
        if (queueIndex == 0) IsCurrentJobPausedByStation = false;

        if (!silent && HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FFCC66>Cancelado: {job.recipeName} (materiais devolvidos).</color>");
        }
        OnCraftQueueChanged?.Invoke();
        return true;
    }

    public int CancelAllQueuedJobs(bool silent = false)
    {
        if (craftQueue.Count == 0) return 0;
        int cancelled = craftQueue.Count;
        for (int i = craftQueue.Count - 1; i >= 0; i--)
        {
            RefundJobIngredients(craftQueue[i]);
        }
        craftQueue.Clear();
        IsCurrentJobPausedByStation = false;

        if (!silent && HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FFCC66>Fila limpa ({cancelled}): materiais devolvidos.</color>");
        }
        OnCraftQueueChanged?.Invoke();
        return cancelled;
    }

    private void RefundJobIngredients(CraftQueueJob job)
    {
        if (job == null || inventory == null || job.ingredientIds == null || job.ingredientAmounts == null) return;
        for (int i = 0; i < job.ingredientIds.Length && i < job.ingredientAmounts.Length; i++)
        {
            int amt = job.ingredientAmounts[i];
            if (amt <= 0) continue;
            int leftover = inventory.AddItem(job.ingredientIds[i], amt);
            if (leftover > 0 && LootManager.Instance != null && PlayerController.Instance != null)
            {
                Vector3 dropPos = PlayerController.Instance.transform.position + PlayerController.Instance.transform.forward * 1.1f + Vector3.up * 0.25f;
                LootManager.Instance.DropItem(job.ingredientIds[i], leftover, dropPos);
            }
        }
    }

    private void DeliverJobOutput(CraftQueueJob job)
    {
        if (job == null || inventory == null) return;

        int droppedOverflow = 0;
        if (job.isCanteenRefill)
        {
            if (!inventory.TryRefillCanteen(out _))
            {
                RefundJobIngredients(job);
                return;
            }
        }
        else
        {
            int leftover = inventory.AddItem(job.outputItemId, job.outputAmount);
            if (leftover > 0 && LootManager.Instance != null && PlayerController.Instance != null)
            {
                Vector3 dropPos = PlayerController.Instance.transform.position + PlayerController.Instance.transform.forward * 1.1f + Vector3.up * 0.25f;
                LootManager.Instance.DropItem(job.outputItemId, leftover, dropPos);
                droppedOverflow = leftover;
            }
        }

        if (AudioManager.Instance != null)
        {
            Vector3 fxPos = PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero;
            AudioManager.Instance.PlayPickupSoundForItem(job.isCanteenRefill ? 210 : job.outputItemId, fxPos);
        }
        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
        if (VFXPoolManager.Instance != null && PlayerController.Instance != null)
            VFXPoolManager.Instance.PlayBuildPlaceVFX(PlayerController.Instance.transform.position);

        CraftRecipe recipe = GetRecipe(job.recipeIndex);
        if (recipe != null) OnCraftSuccess?.Invoke(recipe);

        if (HUDSurvivalOverlay.Instance != null)
        {
            if (droppedOverflow > 0)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Mochila cheia! +{droppedOverflow} item(ns) soltos no chao.");
            }
            else if (SurvivalInventoryCraftUI.Instance == null || !SurvivalInventoryCraftUI.Instance.IsOpen)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#66FFAA>Fabricado: {job.recipeName}</color>");
            }
        }
    }

    public bool Craft(int recipeIndex, int batchCount = 1)
    {
        batchCount = Mathf.Max(1, batchCount);
        bool craftedAny = false;
        int droppedOverflow = 0;
        int lastOutputId = -1;

        for (int b = 0; b < batchCount; b++)
        {
            if (!CanCraft(recipeIndex, out string reason))
            {
                if (!craftedAny) OnCraftFailed?.Invoke(reason);
                break;
            }

            CraftRecipe recipe = recipes[recipeIndex];
            bool isCanteenRefill = IsCanteenRefillRecipe(recipe);
            if (isCanteenRefill)
            {
                if (!inventory.TryRefillCanteen(out _)) break;
                lastOutputId = 210;
                inventory.RemoveItem(109, 1);
            }
            else
            {
                for (int i = 0; i < recipe.ingredientIds.Length; i++)
                {
                    inventory.RemoveItem(recipe.ingredientIds[i], recipe.ingredientAmounts[i]);
                }
                lastOutputId = recipe.outputItemId;

                int leftover = inventory.AddItem(recipe.outputItemId, recipe.outputAmount);
                if (leftover > 0 && LootManager.Instance != null && PlayerController.Instance != null)
                {
                    Vector3 dropPos = PlayerController.Instance.transform.position + PlayerController.Instance.transform.forward * 1.1f + Vector3.up * 0.25f;
                    LootManager.Instance.DropItem(recipe.outputItemId, leftover, dropPos);
                    droppedOverflow += leftover;
                }
            }

            craftedAny = true;
            OnCraftSuccess?.Invoke(recipe);
        }

        if (craftedAny)
        {
            if (AudioManager.Instance != null)
            {
                Vector3 fxPos = PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero;
                AudioManager.Instance.PlayPickupSoundForItem(lastOutputId, fxPos);
            }
            if (FirstPersonViewmodelController.Instance != null)
                FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
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
