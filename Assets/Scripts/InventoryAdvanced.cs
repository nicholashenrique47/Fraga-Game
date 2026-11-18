using UnityEngine;
using System;

/// InventoryAdvanced: Fonte unica de verdade para mochila, hotbar, equipamentos e consumiveis.
[System.Serializable]
public class InventorySlot
{
    public int itemId = -1;     // -1 = slot vazio
    public int quantity = 0;
    public float durability = -1f; // -1 = sem durabilidade; >0 = tempo de validade, cargas (Cantil) ou durabilidade de ferramenta

    public bool IsEmpty() { return itemId < 0 || quantity <= 0; }

    public void Clear()
    {
        itemId = -1;
        quantity = 0;
        durability = -1f;
    }
}

public class InventoryAdvanced : MonoBehaviour
{
    public static InventoryAdvanced Instance { get; private set; }

    [Header("Config")]
    public int maxSlots = 24;

    [Header("Dependencies")]
    public PlayerStatus playerStatus;
    public SanitySystem sanitySystem;
    public BuildingSystem buildingSystem;

    // Slots da mochila
    public InventorySlot[] slots;

    // Hotbar (6 atalhos rapidos ligados por itemId para nao quebrar ao reordenar)
    public int[] hotbarItemIds = new int[6] { -1, -1, -1, -1, -1, -1 };
    public int activeHotbarIndex = 0;

    // Equipamentos Ativos
    public int equippedMainHandId = -1; // -1 = Punhos
    public int equippedOffHandId = -1;  // -1 = Vazio
    public int equippedArmorId = -1;    // -1 = Sem armadura/vestuario

    // Eventos
    public event Action OnInventoryChanged;
    public event Action OnEquipmentChanged;
    /// <summary>Disparado sempre que itens sao adicionados (itemId, addedAmount, newTotal).</summary>
    public event Action<int, int, int> OnItemAdded;

    private bool suppressAddNotifications = false;
    private float nextSpoilCheckTime = 0f;
    private float nextSpoilWarnTime = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        ItemDatabase.Initialize();
        ResetToStartingKit();
    }

    public void ResetToStartingKit()
    {
        ItemDatabase.Initialize();

        if (slots == null || slots.Length != maxSlots)
        {
            slots = new InventorySlot[maxSlots];
            for (int i = 0; i < maxSlots; i++)
            {
                slots[i] = new InventorySlot();
            }
        }
        else
        {
            for (int i = 0; i < maxSlots; i++)
            {
                if (slots[i] == null) slots[i] = new InventorySlot();
                else slots[i].Clear();
            }
        }

        if (hotbarItemIds == null || hotbarItemIds.Length != 6)
        {
            hotbarItemIds = new int[6];
        }
        for (int h = 0; h < hotbarItemIds.Length; h++)
        {
            hotbarItemIds[h] = -1;
        }

        activeHotbarIndex = 0;
        equippedMainHandId = -1;
        equippedOffHandId = -1;
        equippedArmorId = -1;

        // Kit inicial equilibrado de sobrevivencia (suprime notificacoes na inicializacao)
        suppressAddNotifications = true;
        try
        {
            AddItem(205, 3);  // 3 Fosforos
            AddItem(106, 2);  // 2 Sal
            AddItem(202, 5);  // 5 Bagas Silvestres
            AddItem(100, 2);  // 2 Madeira (galhos secos na mochila de viagem)
            AddItem(101, 2);  // 2 Pedra (pederneiras de emergencia)
        }
        finally
        {
            suppressAddNotifications = false;
        }

        OnInventoryChanged?.Invoke();
        OnEquipmentChanged?.Invoke();
    }

    public void SanitizeHotbarBindings()
    {
        SanitizeHotbarBindings(true);
    }

    public void SanitizeHotbarBindings(bool autoFillMissing)
    {
        if (hotbarItemIds == null || hotbarItemIds.Length != 6)
        {
            hotbarItemIds = new int[6] { -1, -1, -1, -1, -1, -1 };
        }

        for (int h = 0; h < hotbarItemIds.Length; h++)
        {
            if (hotbarItemIds[h] > 0 && !HasItem(hotbarItemIds[h]))
            {
                hotbarItemIds[h] = -1;
            }
        }

        if (!autoFillMissing) return;

        for (int i = 0; i < maxSlots; i++)
        {
            if (!slots[i].IsEmpty())
            {
                ItemData d = ItemDatabase.GetItem(slots[i].itemId);
                if (d != null) AutoBindHotbarIfUseful(d);
            }
        }
    }

    private void Start()
    {
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (buildingSystem == null) buildingSystem = FindAnyObjectByType<BuildingSystem>();
    }

    private void Update()
    {
        // Otimizacao Mobile: checar apodrecimento a cada 1s em vez de todo frame
        if (Time.time < nextSpoilCheckTime) return;
        nextSpoilCheckTime = Time.time + 1.0f;

        bool changed = false;
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i].IsEmpty() || slots[i].durability <= 0f) continue;

            ItemData data = ItemDatabase.GetItem(slots[i].itemId);
            if (data != null && data.isPerishable)
            {
                float prevDur = slots[i].durability;
                slots[i].durability = prevDur - 1.0f;
                if (slots[i].durability <= 0f)
                {
                    slots[i].itemId = 108; // 108 = Comida Podre
                    slots[i].durability = -1f;
                    changed = true;
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF8844>{data.itemName} apodreceu na mochila!</color>");
                }
                else if (prevDur > 45f && slots[i].durability <= 45f && Time.time >= nextSpoilWarnTime)
                {
                    nextSpoilWarnTime = Time.time + 15f;
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FFCC44>{data.itemName} prestes a estragar! Cozinhe ou cure com Sal.</color>");
                }
            }
        }

        if (changed) OnInventoryChanged?.Invoke();
    }

    public int AddItem(int itemId, int amount)
    {
        ItemData data = ItemDatabase.GetItem(itemId);
        if (data == null || amount <= 0) return amount;

        int remaining = amount;

        // Fase 1: Empilhar em slots existentes
        for (int i = 0; i < maxSlots && remaining > 0; i++)
        {
            if (slots[i].itemId == itemId && slots[i].quantity < data.maxStack)
            {
                int canAdd = Mathf.Min(remaining, data.maxStack - slots[i].quantity);
                float freshDur = data.maxDurability > 0f ? data.maxDurability : -1f;
                slots[i].durability = MergeSlotDurability(data, slots[i].durability, slots[i].quantity, freshDur, canAdd);
                slots[i].quantity += canAdd;
                remaining -= canAdd;
            }
        }

        // Fase 2: Ocupar slots vazios
        for (int i = 0; i < maxSlots && remaining > 0; i++)
        {
            if (slots[i].IsEmpty())
            {
                int canAdd = Mathf.Min(remaining, data.maxStack);
                slots[i].itemId = itemId;
                slots[i].quantity = canAdd;
                slots[i].durability = data.maxDurability > 0f ? data.maxDurability : -1f;
                remaining -= canAdd;
            }
        }

        int added = amount - remaining;
        if (added > 0)
        {
            AutoBindHotbarIfUseful(data);
            AutoEquipIfFirstTool(data);
            OnInventoryChanged?.Invoke();

            if (!suppressAddNotifications)
            {
                int totalNow = GetItemCount(itemId);
                OnItemAdded?.Invoke(itemId, added, totalNow);
            }
        }

        return remaining;
    }

    public int GetOccupiedSlotCount()
    {
        int count = 0;
        for (int i = 0; i < maxSlots; i++)
        {
            if (!slots[i].IsEmpty()) count++;
        }
        return count;
    }

    public bool HasRefillableCanteen()
    {
        ItemData canteenData = ItemDatabase.GetItem(210);
        float maxCharges = canteenData != null && canteenData.maxDurability > 0f ? canteenData.maxDurability : 3f;
        for (int i = 0; i < maxSlots; i++)
        {
            if (!slots[i].IsEmpty() && slots[i].itemId == 210 && slots[i].durability < maxCharges - 0.01f)
                return true;
        }
        return false;
    }

    public bool TryRefillCanteen(out string message)
    {
        message = string.Empty;
        ItemData canteenData = ItemDatabase.GetItem(210);
        float maxCharges = canteenData != null && canteenData.maxDurability > 0f ? canteenData.maxDurability : 3f;

        for (int i = 0; i < maxSlots; i++)
        {
            if (!slots[i].IsEmpty() && slots[i].itemId == 210 && slots[i].durability < maxCharges)
            {
                slots[i].durability = maxCharges;
                OnInventoryChanged?.Invoke();
                message = "Cantil de Couro reabastecido (3/3 goles)!";
                return true;
            }
        }
        return false;
    }

    public int FindUsableEquipmentSlot(int itemId)
    {
        ItemData data = ItemDatabase.GetItem(itemId);
        bool checksDurability = data != null && !data.isPerishable && data.maxDurability > 0f && data.equipSlot != EquipSlotType.None;
        for (int i = 0; i < maxSlots; i++)
        {
            if (!slots[i].IsEmpty() && slots[i].itemId == itemId)
            {
                if (!checksDurability || slots[i].durability < 0f || slots[i].durability > 0f)
                    return i;
            }
        }
        return -1;
    }

    public bool DamageEquippedTool(EquipSlotType slotType, float amount = 1f)
    {
        int targetId = -1;
        if (slotType == EquipSlotType.MainHand) targetId = equippedMainHandId;
        else if (slotType == EquipSlotType.OffHand) targetId = equippedOffHandId;
        else if (slotType == EquipSlotType.BodyArmor) targetId = equippedArmorId;

        if (targetId <= 0 || amount <= 0f) return false;
        ItemData data = ItemDatabase.GetItem(targetId);
        if (data == null || data.isPerishable || data.maxDurability <= 0f) return false;

        int slotIdx = FindUsableEquipmentSlot(targetId);
        if (slotIdx < 0) return false;

        if (slots[slotIdx].durability < 0f) slots[slotIdx].durability = data.maxDurability;

        float prev = slots[slotIdx].durability;
        slots[slotIdx].durability = Mathf.Max(0f, prev - amount);

        if (slots[slotIdx].durability <= 0f)
        {
            if (slotType == EquipSlotType.MainHand) equippedMainHandId = -1;
            else if (slotType == EquipSlotType.OffHand) equippedOffHandId = -1;
            else if (slotType == EquipSlotType.BodyArmor) equippedArmorId = -1;

            OnEquipmentChanged?.Invoke();
            OnInventoryChanged?.Invoke();

            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF6655>{data.itemName} quebrou! Conserte na Bancada.</color>");
            return true;
        }

        float lowThreshold = data.maxDurability * 0.2f;
        if (prev > lowThreshold && slots[slotIdx].durability <= lowThreshold && HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FFBB44>{data.itemName} desgastado ({Mathf.CeilToInt(slots[slotIdx].durability)}/{Mathf.RoundToInt(data.maxDurability)})</color>");
        }

        OnInventoryChanged?.Invoke();
        return false;
    }

    public int GetRepairIngredientId(int itemId, out string ingredientName)
    {
        if (itemId == 312 || itemId == 304) { ingredientName = "Ferro Velho"; return 107; }
        if (itemId == 310 || itemId == 314) { ingredientName = "Pele de Animal"; return 105; }
        if (itemId == 311 || itemId == 313) { ingredientName = "Osso"; return 120; }
        if (HasItem(101)) { ingredientName = "Pedra"; return 101; }
        ingredientName = "Madeira";
        return 100;
    }

    public bool RepairItemSlot(int slotIndex, out string message)
    {
        message = string.Empty;
        if (slotIndex < 0 || slotIndex >= maxSlots || slots[slotIndex].IsEmpty())
        {
            message = "Slot invalido.";
            return false;
        }

        ItemData data = ItemDatabase.GetItem(slots[slotIndex].itemId);
        if (data == null || data.isPerishable || data.equipSlot == EquipSlotType.None || data.maxDurability <= 0f)
        {
            message = "Este item nao precisa de conserto.";
            return false;
        }

        if (slots[slotIndex].durability < 0f || slots[slotIndex].durability >= data.maxDurability - 0.1f)
        {
            message = "Ferramenta ja esta com durabilidade maxima.";
            return false;
        }

        if (CraftingSystem.Instance != null && !CraftingSystem.Instance.IsNearStation(CraftingStationType.Workbench))
        {
            message = "Aproxime-se da Mesa de Trabalho (Bancada) para consertar!";
            return false;
        }

        int matId = GetRepairIngredientId(data.id, out string matName);
        if (!HasItem(matId))
        {
            message = $"Precisa de 1x {matName} para consertar {data.itemName}.";
            return false;
        }

        RemoveItem(matId, 1);
        slots[slotIndex].durability = data.maxDurability;
        OnInventoryChanged?.Invoke();

        if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
        message = $"{data.itemName} consertado (100%) com 1x {matName}!";
        return true;
    }

    private void AutoBindHotbarIfUseful(ItemData data)
    {
        if (data.type != ItemType.Tool && data.type != ItemType.Consumable && data.type != ItemType.Deployable) return;
        for (int h = 0; h < hotbarItemIds.Length; h++)
        {
            if (hotbarItemIds[h] == data.id) return;
        }

        // Prioriza substituir slots vazios antes de substituir itens consumidos
        for (int h = 0; h < hotbarItemIds.Length; h++)
        {
            if (hotbarItemIds[h] <= 0)
            {
                hotbarItemIds[h] = data.id;
                return;
            }
        }

        for (int h = 0; h < hotbarItemIds.Length; h++)
        {
            if (!HasItem(hotbarItemIds[h]))
            {
                hotbarItemIds[h] = data.id;
                return;
            }
        }
    }

    private void AutoEquipIfFirstTool(ItemData data)
    {
        if (data.equipSlot == EquipSlotType.MainHand && equippedMainHandId <= 0)
        {
            EquipItem(data.id);
        }
        else if (data.equipSlot == EquipSlotType.BodyArmor && equippedArmorId <= 0)
        {
            EquipItem(data.id);
        }
    }

    public bool RemoveItem(int itemId, int amount)
    {
        if (amount <= 0) return true;
        int total = GetItemCount(itemId);
        if (total < amount) return false;

        int remaining = amount;
        for (int i = 0; i < maxSlots && remaining > 0; i++)
        {
            if (slots[i].itemId == itemId)
            {
                int canRemove = Mathf.Min(remaining, slots[i].quantity);
                slots[i].quantity -= canRemove;
                remaining -= canRemove;
                if (slots[i].quantity <= 0) slots[i].Clear();
            }
        }

        ValidateEquippedAfterRemoval(itemId);
        OnInventoryChanged?.Invoke();
        return true;
    }

    private void ValidateEquippedAfterRemoval(int itemId)
    {
        if (!HasItem(itemId))
        {
            bool changed = false;
            if (equippedMainHandId == itemId) { equippedMainHandId = -1; changed = true; }
            if (equippedOffHandId == itemId) { equippedOffHandId = -1; changed = true; }
            if (equippedArmorId == itemId) { equippedArmorId = -1; changed = true; }
            if (changed) OnEquipmentChanged?.Invoke();
        }
    }

    public int GetItemCount(int itemId)
    {
        int count = 0;
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i].itemId == itemId) count += slots[i].quantity;
        }
        return count;
    }

    public int CountItem(int itemId) => GetItemCount(itemId);
    public bool HasItem(int itemId) => GetItemCount(itemId) > 0;

    public int FindFirstSlotIndex(int itemId)
    {
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i].itemId == itemId && slots[i].quantity > 0) return i;
        }
        return -1;
    }

    public bool EquipItem(int itemId)
    {
        if (itemId <= 0)
        {
            equippedMainHandId = -1;
            OnEquipmentChanged?.Invoke();
            return true;
        }

        if (!HasItem(itemId)) return false;
        ItemData data = ItemDatabase.GetItem(itemId);
        if (data == null || data.equipSlot == EquipSlotType.None) return false;

        if (!data.isPerishable && data.maxDurability > 0f && FindUsableEquipmentSlot(itemId) < 0)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"{data.itemName} quebrado! Conserte na Mesa de Trabalho.");
            return false;
        }

        switch (data.equipSlot)
        {
            case EquipSlotType.MainHand:
                equippedMainHandId = itemId;
                break;
            case EquipSlotType.OffHand:
                equippedOffHandId = (equippedOffHandId == itemId) ? -1 : itemId;
                break;
            case EquipSlotType.BodyArmor:
                equippedArmorId = (equippedArmorId == itemId) ? -1 : itemId;
                break;
        }

        OnEquipmentChanged?.Invoke();
        return true;
    }

    public bool EquipFromSlot(int slotIndex, EquipSlotType? requiredSlot = null)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots || slots[slotIndex].IsEmpty()) return false;
        int itemId = slots[slotIndex].itemId;
        ItemData data = ItemDatabase.GetItem(itemId);
        if (data == null || data.equipSlot == EquipSlotType.None) return false;
        if (requiredSlot.HasValue && data.equipSlot != requiredSlot.Value) return false;

        if (!data.isPerishable && data.maxDurability > 0f && slots[slotIndex].durability == 0f)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"{data.itemName} quebrado! Conserte na Mesa de Trabalho.");
            return false;
        }

        // Ao arrastar diretamente para um card de equipamento, nunca alterna para vazio se ja estiver equipado.
        if (data.equipSlot == EquipSlotType.OffHand && equippedOffHandId == itemId)
        {
            OnEquipmentChanged?.Invoke();
            return true;
        }
        if (data.equipSlot == EquipSlotType.BodyArmor && equippedArmorId == itemId)
        {
            OnEquipmentChanged?.Invoke();
            return true;
        }

        return EquipItem(itemId);
    }

    public void AssignHotbarSlot(int hotbarIndex, int itemId)
    {
        if (hotbarIndex < 0 || hotbarIndex >= hotbarItemIds.Length) return;
        if (itemId > 0)
        {
            for (int h = 0; h < hotbarItemIds.Length; h++)
            {
                if (h != hotbarIndex && hotbarItemIds[h] == itemId)
                    hotbarItemIds[h] = -1;
            }
        }
        hotbarItemIds[hotbarIndex] = itemId;
        OnEquipmentChanged?.Invoke();
    }

    public bool AssignSlotToHotbar(int invSlotIndex, int hotbarIndex)
    {
        if (invSlotIndex < 0 || invSlotIndex >= maxSlots || slots[invSlotIndex].IsEmpty()) return false;
        if (hotbarIndex < 0 || hotbarIndex >= hotbarItemIds.Length) return false;
        AssignHotbarSlot(hotbarIndex, slots[invSlotIndex].itemId);
        return true;
    }

    public bool SwapHotbarSlots(int hotbarA, int hotbarB)
    {
        if (hotbarItemIds == null || hotbarA < 0 || hotbarA >= hotbarItemIds.Length || hotbarB < 0 || hotbarB >= hotbarItemIds.Length)
            return false;
        if (hotbarA == hotbarB) return true;

        int tmp = hotbarItemIds[hotbarA];
        hotbarItemIds[hotbarA] = hotbarItemIds[hotbarB];
        hotbarItemIds[hotbarB] = tmp;

        if (activeHotbarIndex == hotbarA) activeHotbarIndex = hotbarB;
        else if (activeHotbarIndex == hotbarB) activeHotbarIndex = hotbarA;

        OnEquipmentChanged?.Invoke();
        return true;
    }

    public void ClearHotbarSlot(int hotbarIndex)
    {
        if (hotbarItemIds == null || hotbarIndex < 0 || hotbarIndex >= hotbarItemIds.Length) return;
        hotbarItemIds[hotbarIndex] = -1;
        OnEquipmentChanged?.Invoke();
    }

    public bool UseHotbarSlot(int hotbarIndex)
    {
        if (hotbarIndex < 0 || hotbarIndex >= hotbarItemIds.Length) return false;
        activeHotbarIndex = hotbarIndex;
        int itemId = hotbarItemIds[hotbarIndex];
        OnEquipmentChanged?.Invoke();

        if (itemId <= 0 || !HasItem(itemId))
        {
            EquipItem(-1);
            return false;
        }

        ItemData data = ItemDatabase.GetItem(itemId);
        if (data == null) return false;

        if (data.equipSlot != EquipSlotType.None)
        {
            return EquipItem(itemId);
        }

        int slotIdx = FindFirstSlotIndex(itemId);
        return slotIdx >= 0 && UseItem(slotIdx);
    }

    public bool UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots) return false;
        if (playerStatus != null && playerStatus.IsDead) return false;
        if (slots[slotIndex].IsEmpty()) return false;

        ItemData data = ItemDatabase.GetItem(slots[slotIndex].itemId);
        if (data == null) return false;

        // 1. Ferramentas, Armas, Escudo e Armadura -> Equipar
        if (data.type == ItemType.Tool || data.equipSlot != EquipSlotType.None)
        {
            if (!data.isPerishable && data.maxDurability > 0f && slots[slotIndex].durability == 0f)
            {
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"{data.itemName} quebrado! Use CONSERTAR perto da Bancada.");
                return false;
            }
            bool ok = EquipItem(data.id);
            if (ok && HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Equipado: {data.itemName}");
            }
            return ok;
        }

        // 2. Estruturas / Deployables -> Modo Construcao ou uso direto
        if (data.type == ItemType.Deployable)
        {
            if (TryUseDeployable(data.id))
            {
                slots[slotIndex].quantity--;
                if (slots[slotIndex].quantity <= 0) slots[slotIndex].Clear();
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        // 3. Consumiveis
        if (data.type == ItemType.Consumable)
        {
            if (data.id == 205) // Fosforos
            {
                if (!TryUseMatch()) return false;
                slots[slotIndex].quantity--;
                if (slots[slotIndex].quantity <= 0) slots[slotIndex].Clear();
                OnInventoryChanged?.Invoke();
                return true;
            }

            if (data.id == 210) // Cantil de Couro reutilizavel (cargas)
            {
                if (slots[slotIndex].durability <= 0f)
                {
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("Cantil vazio! Encha no Coletor de Chuva ou fabrique recarga c/ Agua Limpa.");
                    return false;
                }

                if (!HasAnyPhysiologicalBenefit(data))
                {
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FFCC66>Sede e folego ja estao no maximo — Cantil economizado!</color>");
                    return false;
                }

                slots[slotIndex].durability = Mathf.Max(0f, slots[slotIndex].durability - 1f);
                ApplyConsumablePhysiology(data);

                if (FirstPersonViewmodelController.Instance != null)
                    FirstPersonViewmodelController.Instance.TriggerConsumeAnimation(FirstPersonViewmodelController.ConsumeAnimKind.Drink, data.id);
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.DrinkGulp, null, 0.72f, 0.94f, 1.06f);
                }

                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"Bebeu do Cantil ({Mathf.RoundToInt(slots[slotIndex].durability)}/{Mathf.RoundToInt(data.maxDurability)} goles)");

                OnInventoryChanged?.Invoke();
                return true;
            }

            if (!HasAnyPhysiologicalBenefit(data))
            {
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FFCC66>Atributos ja estao no maximo — item economizado!</color>");
                return false;
            }

            ApplyConsumablePhysiology(data);

            if (data.id == 116)
            {
                LoreSystem lore = FindAnyObjectByType<LoreSystem>();
                if (lore != null) lore.ShowNextPage();
            }

            ResolveConsumeAnimAndFoley(data, out FirstPersonViewmodelController.ConsumeAnimKind animKind, out AudioManager.FoleyEventType foleyKind);
            if (FirstPersonViewmodelController.Instance != null)
            {
                FirstPersonViewmodelController.Instance.TriggerConsumeAnimation(animKind, data.id);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFoley(foleyKind, null, 0.72f, 0.93f, 1.07f);
            }

            slots[slotIndex].quantity--;
            if (slots[slotIndex].quantity <= 0) slots[slotIndex].Clear();

            OnInventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    private static void ResolveConsumeAnimAndFoley(ItemData data, out FirstPersonViewmodelController.ConsumeAnimKind animKind, out AudioManager.FoleyEventType foleyKind)
    {
        if (data == null)
        {
            animKind = FirstPersonViewmodelController.ConsumeAnimKind.Generic;
            foleyKind = AudioManager.FoleyEventType.EatCrunch;
            return;
        }
        if (data.curesBleeding || data.id == 208)
        {
            animKind = FirstPersonViewmodelController.ConsumeAnimKind.Bandage;
            foleyKind = AudioManager.FoleyEventType.BandageWrap;
            return;
        }
        if (data.curesFracture || data.id == 209)
        {
            animKind = FirstPersonViewmodelController.ConsumeAnimKind.Splint;
            foleyKind = AudioManager.FoleyEventType.SplintSnap;
            return;
        }
        if (data.id == 116)
        {
            animKind = FirstPersonViewmodelController.ConsumeAnimKind.ReadNote;
            foleyKind = AudioManager.FoleyEventType.PickupHerb;
            return;
        }
        if (data.thirstRestore > 0f && data.hungerRestore <= 0f)
        {
            animKind = FirstPersonViewmodelController.ConsumeAnimKind.Drink;
            foleyKind = AudioManager.FoleyEventType.DrinkGulp;
            return;
        }
        animKind = FirstPersonViewmodelController.ConsumeAnimKind.Eat;
        foleyKind = AudioManager.FoleyEventType.EatCrunch;
    }

    private bool HasAnyPhysiologicalBenefit(ItemData data)
    {
        if (data == null || data.poisonDuration > 0f || data.id == 116 || data.id == 205)
            return true;

        if (playerStatus == null) playerStatus = PlayerStatus.Instance;
        if (playerStatus == null) return true;

        if (data.hungerRestore > 0f && playerStatus.hunger < 99.5f) return true;
        if (data.thirstRestore > 0f && playerStatus.thirst < 99.5f) return true;
        if (data.healthRestore > 0f && playerStatus.health < 99.5f) return true;
        if (data.staminaRestore > 0f && playerStatus.stamina < playerStatus.EffectiveMaxStamina - 1f) return true;
        if (data.sanityRestore > 0f && sanitySystem != null && sanitySystem.sanity < 99.5f) return true;
        if (data.curesBleeding && playerStatus.isBleeding) return true;
        if (data.curesFracture && playerStatus.isFractured) return true;
        if (data.poisonDuration < 0f && playerStatus.isPoisoned) return true;
        if ((data.id == 211 || data.id == 201 || data.id == 208) && playerStatus.isSick) return true;
        if (data.warmthBonus > 0f && BodyTemperatureSystem.Instance != null && BodyTemperatureSystem.Instance.currentTemperature < 36.8f) return true;

        return false;
    }

    private void ApplyConsumablePhysiology(ItemData data)
    {
        if (playerStatus != null)
        {
            if (data.hungerRestore > 0f || data.sanityRestore != 0f)
                playerStatus.Eat(data.hungerRestore, data.sanityRestore);

            if (data.thirstRestore > 0f)
                playerStatus.Drink(data.thirstRestore);

            if (data.healthRestore > 0f)
                playerStatus.Heal(data.healthRestore);

            if (data.staminaRestore > 0f)
                playerStatus.RestoreStamina(data.staminaRestore);

            if (data.curesBleeding) playerStatus.isBleeding = false;
            if (data.curesFracture) playerStatus.isFractured = false;
            if (data.id == 211 || data.id == 201 || data.id == 208)
            {
                playerStatus.isSick = false;
                playerStatus.sicknessTimer = 0f;
            }

            if (data.poisonDuration > 0f)
            {
                if (data.id == 104 || data.id == 123)
                {
                    if (UnityEngine.Random.value > 0.45f) playerStatus.ApplyPoison(data.poisonDuration);
                    if (data.id == 123 && UnityEngine.Random.value < 0.55f) playerStatus.ApplySickness(45f);
                }
                else
                {
                    playerStatus.ApplyPoison(data.poisonDuration);
                    if (data.id == 108 || (data.id == 203 && UnityEngine.Random.value < 0.6f)) playerStatus.ApplySickness(55f);
                }
            }
            else if (data.poisonDuration < 0f)
            {
                playerStatus.isPoisoned = false;
                playerStatus.poisonTimer = 0f;
            }
        }

        if (data.warmthBonus > 0f && BodyTemperatureSystem.Instance != null)
        {
            BodyTemperatureSystem.Instance.WarmUp(data.warmthBonus);
        }
    }

    private bool TryUseMatch()
    {
        Transform player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        if (player == null) return false;

        BonfireEntity[] bonfires = FindObjectsByType<BonfireEntity>();
        BonfireEntity closest = null;
        float bestDistSq = 16f;
        foreach (var b in bonfires)
        {
            if (b == null) continue;
            float dSq = (b.transform.position - player.position).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                closest = b;
            }
        }

        if (closest != null)
        {
            closest.AddFuel(100f);
            closest.Ignite();
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.FireIgnite, closest.transform.position, 0.78f);
            }
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Fogueira acesa com Fosforo!");
            return true;
        }

        if (sanitySystem != null && sanitySystem.oil < 95f)
        {
            sanitySystem.RefillOil(25f);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.LighterFlick, null, 0.65f);
            }
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Lampiao aceso/recarregado (+25% Oleo)!");
            return true;
        }

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("Aproxime-se de uma fogueira para acender!");
        return false;
    }

    public static int GetDeployableTileId(int itemId)
    {
        switch (itemId)
        {
            case 400: return 4;  // Fogueira
            case 401: return 5;  // Armadilha de Caca Pesada
            case 402: return 25; // Circulo de Sal (dedicado)
            case 403: return 12; // Coletor de Agua
            case 404: return 13; // Mesa de Trabalho
            case 405: return 15; // Caldeirao
            case 406: return 16; // Laco para Coelhos
            default: return -1;
        }
    }

    private bool TryUseDeployable(int itemId)
    {
        if (buildingSystem == null) buildingSystem = BuildingSystem.Instance ?? FindAnyObjectByType<BuildingSystem>();

        int tileId = GetDeployableTileId(itemId);
        if (tileId > 0 && buildingSystem != null && buildingSystem.ForceBuild(tileId))
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Estrutura posicionada!");
            return true;
        }

        return false;
    }

    public float GetTotalArmorDefense()
    {
        float def = 0f;
        if (equippedArmorId > 0)
        {
            var d = ItemDatabase.GetItem(equippedArmorId);
            if (d != null) def += d.armorDefense;
        }
        if (equippedOffHandId > 0)
        {
            var d = ItemDatabase.GetItem(equippedOffHandId);
            if (d != null) def += d.armorDefense;
        }
        return Mathf.Clamp(def, 0f, 0.75f);
    }

    public float GetColdInsulation()
    {
        float ins = 0f;
        if (equippedArmorId > 0)
        {
            var d = ItemDatabase.GetItem(equippedArmorId);
            if (d != null) ins += d.coldInsulation;
        }
        if (equippedOffHandId > 0)
        {
            var d = ItemDatabase.GetItem(equippedOffHandId);
            if (d != null) ins += d.coldInsulation;
        }
        if (ins <= 0f && HasItem(105)) ins = 0.20f;
        return Mathf.Clamp01(ins);
    }

    public void DropItem(int slotIndex, int amount)
    {
        DropItemToWorld(slotIndex, amount);
    }

    public void DropItemToWorld(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots) return;
        if (slots[slotIndex].IsEmpty()) return;

        int itemId = slots[slotIndex].itemId;
        int toDrop = Mathf.Min(amount, slots[slotIndex].quantity);

        slots[slotIndex].quantity -= toDrop;
        if (slots[slotIndex].quantity <= 0) slots[slotIndex].Clear();

        ValidateEquippedAfterRemoval(itemId);

        if (LootManager.Instance != null && PlayerController.Instance != null)
        {
            Vector3 dropPos = PlayerController.Instance.transform.position + PlayerController.Instance.transform.forward * 1.4f + Vector3.up * 0.4f;
            LootManager.Instance.DropItem(itemId, toDrop, dropPos);
        }

        OnInventoryChanged?.Invoke();
    }

    private static float MergeSlotDurability(ItemData data, float targetDur, int targetQty, float sourceDur, int movedQty)
    {
        if (data == null || data.maxDurability <= 0f) return targetDur;
        float tDur = targetDur < 0f ? data.maxDurability : targetDur;
        float sDur = sourceDur < 0f ? data.maxDurability : sourceDur;
        if (data.isPerishable)
        {
            int total = Mathf.Max(1, targetQty + movedQty);
            float weighted = (tDur * Mathf.Max(0, targetQty) + sDur * Mathf.Max(0, movedQty)) / total;
            return Mathf.Clamp(weighted, 0.5f, data.maxDurability);
        }
        return Mathf.Clamp(Mathf.Min(tDur, sDur), 0f, data.maxDurability);
    }

    public bool SplitSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots) return false;
        if (slots[slotIndex].IsEmpty() || slots[slotIndex].quantity < 2) return false;
        return SplitSlot(slotIndex, slots[slotIndex].quantity / 2, -1);
    }

    public bool SplitSlot(int slotIndex, int splitAmount, int targetEmptySlot = -1)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots) return false;
        if (slots[slotIndex].IsEmpty() || slots[slotIndex].quantity < 2) return false;
        if (splitAmount <= 0 || splitAmount >= slots[slotIndex].quantity) return false;

        int emptyIdx = -1;
        if (targetEmptySlot >= 0)
        {
            if (targetEmptySlot >= maxSlots || targetEmptySlot == slotIndex || !slots[targetEmptySlot].IsEmpty())
                return false;
            emptyIdx = targetEmptySlot;
        }
        else
        {
            for (int i = 0; i < maxSlots; i++)
            {
                if (slots[i].IsEmpty()) { emptyIdx = i; break; }
            }
        }
        if (emptyIdx < 0) return false;

        slots[slotIndex].quantity -= splitAmount;
        slots[emptyIdx].itemId = slots[slotIndex].itemId;
        slots[emptyIdx].quantity = splitAmount;
        slots[emptyIdx].durability = slots[slotIndex].durability;

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool MoveOrMergeSlot(int fromSlot, int toSlot, int moveAmount = -1)
    {
        if (fromSlot < 0 || fromSlot >= maxSlots || toSlot < 0 || toSlot >= maxSlots || fromSlot == toSlot)
            return false;
        if (slots[fromSlot].IsEmpty()) return false;

        if (moveAmount <= 0 || moveAmount >= slots[fromSlot].quantity)
        {
            SwapSlots(fromSlot, toSlot);
            return true;
        }

        if (slots[toSlot].IsEmpty())
        {
            return SplitSlot(fromSlot, moveAmount, toSlot);
        }

        if (slots[toSlot].itemId != slots[fromSlot].itemId) return false;

        ItemData data = ItemDatabase.GetItem(slots[fromSlot].itemId);
        int maxStack = data != null ? data.maxStack : 1;
        int actualMove = Mathf.Min(moveAmount, maxStack - slots[toSlot].quantity);
        if (actualMove <= 0) return false;

        slots[toSlot].durability = MergeSlotDurability(data, slots[toSlot].durability, slots[toSlot].quantity, slots[fromSlot].durability, actualMove);
        slots[toSlot].quantity += actualMove;
        slots[fromSlot].quantity -= actualMove;
        if (slots[fromSlot].quantity <= 0) slots[fromSlot].Clear();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void SwapSlots(int slotA, int slotB)
    {
        if (slotA < 0 || slotA >= maxSlots || slotB < 0 || slotB >= maxSlots || slotA == slotB) return;

        if (!slots[slotA].IsEmpty() && slots[slotA].itemId == slots[slotB].itemId)
        {
            ItemData data = ItemDatabase.GetItem(slots[slotA].itemId);
            if (data != null && slots[slotB].quantity < data.maxStack)
            {
                int move = Mathf.Min(slots[slotA].quantity, data.maxStack - slots[slotB].quantity);
                slots[slotB].durability = MergeSlotDurability(data, slots[slotB].durability, slots[slotB].quantity, slots[slotA].durability, move);
                slots[slotB].quantity += move;
                slots[slotA].quantity -= move;
                if (slots[slotA].quantity <= 0) slots[slotA].Clear();
                OnInventoryChanged?.Invoke();
                return;
            }
        }

        InventorySlot temp = new InventorySlot
        {
            itemId = slots[slotA].itemId,
            quantity = slots[slotA].quantity,
            durability = slots[slotA].durability
        };

        slots[slotA].itemId = slots[slotB].itemId;
        slots[slotA].quantity = slots[slotB].quantity;
        slots[slotA].durability = slots[slotB].durability;

        slots[slotB].itemId = temp.itemId;
        slots[slotB].quantity = temp.quantity;
        slots[slotB].durability = temp.durability;

        OnInventoryChanged?.Invoke();
    }

    public void NotifyStateLoaded(bool preserveSavedHotbar = false)
    {
        SanitizeHotbarBindings(!preserveSavedHotbar);
        OnInventoryChanged?.Invoke();
        OnEquipmentChanged?.Invoke();
    }

    public float GetEstimatedWeightKg()
    {
        float total = 0f;
        if (slots == null) return 0f;
        for (int i = 0; i < maxSlots && i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].IsEmpty()) continue;
            ItemData data = ItemDatabase.GetItem(slots[i].itemId);
            if (data == null) continue;
            float unitW = 0.35f;
            switch (data.category)
            {
                case ItemCategory.ToolAndWeapon: unitW = 1.45f; break;
                case ItemCategory.ArmorAndLight: unitW = 1.85f; break;
                case ItemCategory.Deployable: unitW = 2.20f; break;
                case ItemCategory.Consumable: unitW = 0.30f; break;
                case ItemCategory.Resource: unitW = (data.id == 100 || data.id == 101) ? 0.65f : 0.35f; break;
            }
            total += unitW * slots[i].quantity;
        }
        return total;
    }

    public const float HeavyWeightThresholdKg = 32f;
    public const float MaxCarryWeightKg = 45f;

    public float GetEncumbrance01() => Mathf.Clamp01(GetEstimatedWeightKg() / MaxCarryWeightKg);
    public bool IsHeavyLoad => GetEstimatedWeightKg() >= HeavyWeightThresholdKg && !IsOverEncumbered;
    public bool IsOverEncumbered => GetEstimatedWeightKg() >= MaxCarryWeightKg;

    public float GetMoveSpeedMultiplier()
    {
        float w = GetEstimatedWeightKg();
        if (w >= MaxCarryWeightKg) return 0.62f;
        if (w >= HeavyWeightThresholdKg) return 0.85f;
        return 1.0f;
    }

    public string GetEncumbranceLabel()
    {
        float w = GetEstimatedWeightKg();
        if (w >= MaxCarryWeightKg) return "SOBRECARGA";
        if (w >= HeavyWeightThresholdKg) return "PESADO";
        return "NORMAL";
    }

    public void SortAndCompact()
    {
        if (slots == null || slots.Length == 0) return;

        // 1. Fundir pilhas incompletas do mesmo item
        for (int i = 0; i < maxSlots && i < slots.Length; i++)
        {
            if (slots[i].IsEmpty()) continue;
            ItemData data = ItemDatabase.GetItem(slots[i].itemId);
            if (data == null || data.maxStack <= 1 || slots[i].quantity >= data.maxStack) continue;

            for (int j = i + 1; j < maxSlots && j < slots.Length; j++)
            {
                if (slots[j].IsEmpty() || slots[j].itemId != slots[i].itemId) continue;
                int move = Mathf.Min(slots[j].quantity, data.maxStack - slots[i].quantity);
                if (move <= 0) break;
                slots[i].durability = MergeSlotDurability(data, slots[i].durability, slots[i].quantity, slots[j].durability, move);
                slots[i].quantity += move;
                slots[j].quantity -= move;
                if (slots[j].quantity <= 0) slots[j].Clear();
                if (slots[i].quantity >= data.maxStack) break;
            }
        }

        // 2. Ordenar por categoria, nome e ID (slots vazios no fim)
        System.Array.Sort(slots, (a, b) =>
        {
            bool aEmpty = a == null || a.IsEmpty();
            bool bEmpty = b == null || b.IsEmpty();
            if (aEmpty && bEmpty) return 0;
            if (aEmpty) return 1;
            if (bEmpty) return -1;

            ItemData da = ItemDatabase.GetItem(a.itemId);
            ItemData db = ItemDatabase.GetItem(b.itemId);
            int catA = da != null ? (int)da.category : 99;
            int catB = db != null ? (int)db.category : 99;
            if (catA != catB) return catA.CompareTo(catB);

            string na = da != null ? da.itemName : string.Empty;
            string nb = db != null ? db.itemName : string.Empty;
            int cmp = string.Compare(na, nb, System.StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;
            return a.itemId.CompareTo(b.itemId);
        });

        SanitizeHotbarBindings(false);
        OnInventoryChanged?.Invoke();
    }
}
