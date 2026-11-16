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
    public int[] hotbarItemIds = new int[6] { 300, 305, 302, 303, 210, 206 };
    public int activeHotbarIndex = 0;

    // Equipamentos Ativos
    public int equippedMainHandId = -1; // -1 = Punhos
    public int equippedOffHandId = -1;  // -1 = Vazio
    public int equippedArmorId = -1;    // -1 = Sem armadura/vestuario

    // Eventos
    public event Action OnInventoryChanged;
    public event Action OnEquipmentChanged;

    private float nextSpoilCheckTime = 0f;
    private float nextSpoilWarnTime = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        ItemDatabase.Initialize();

        slots = new InventorySlot[maxSlots];
        for (int i = 0; i < maxSlots; i++)
        {
            slots[i] = new InventorySlot();
        }

        // Itens iniciais de sobrevivencia equilibrados
        AddItem(205, 3);  // 3 Fosforos
        AddItem(106, 2);  // 2 Sal
        AddItem(202, 5);  // 5 Bagas Silvestres
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

        if (remaining < amount)
        {
            AutoBindHotbarIfUseful(data);
            AutoEquipIfFirstTool(data);
            OnInventoryChanged?.Invoke();
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
                message = "Cantil de Couro reabastecido (3/3 golos)!";
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
        if (itemId == 310) { ingredientName = "Pele de Animal"; return 105; }
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
        for (int h = 0; h < hotbarItemIds.Length; h++)
        {
            if (hotbarItemIds[h] <= 0 || !HasItem(hotbarItemIds[h]))
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

    public void AssignHotbarSlot(int hotbarIndex, int itemId)
    {
        if (hotbarIndex < 0 || hotbarIndex >= hotbarItemIds.Length) return;
        hotbarItemIds[hotbarIndex] = itemId;
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

                slots[slotIndex].durability = Mathf.Max(0f, slots[slotIndex].durability - 1f);
                ApplyConsumablePhysiology(data);

                if (FirstPersonViewmodelController.Instance != null)
                    FirstPersonViewmodelController.Instance.TriggerConsumeAnimation();

                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"Bebeu do Cantil ({Mathf.RoundToInt(slots[slotIndex].durability)}/{Mathf.RoundToInt(data.maxDurability)} golos)");

                OnInventoryChanged?.Invoke();
                return true;
            }

            ApplyConsumablePhysiology(data);

            if (data.id == 116)
            {
                LoreSystem lore = FindAnyObjectByType<LoreSystem>();
                if (lore != null) lore.ShowNextPage();
            }

            if (FirstPersonViewmodelController.Instance != null)
            {
                FirstPersonViewmodelController.Instance.TriggerConsumeAnimation();
            }

            slots[slotIndex].quantity--;
            if (slots[slotIndex].quantity <= 0) slots[slotIndex].Clear();

            OnInventoryChanged?.Invoke();
            return true;
        }

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
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Fogueira acesa com Fosforo!");
            return true;
        }

        if (sanitySystem != null && sanitySystem.oil < 95f)
        {
            sanitySystem.RefillOil(25f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Lampiao aceso/recarregado (+25% Oleo)!");
            return true;
        }

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("Aproxime-se de uma fogueira para acender!");
        return false;
    }

    private bool TryUseDeployable(int itemId)
    {
        if (buildingSystem == null) buildingSystem = BuildingSystem.Instance ?? FindAnyObjectByType<BuildingSystem>();

        int tileId = -1;
        switch (itemId)
        {
            case 400: tileId = 4; break;  // Fogueira
            case 401: tileId = 5; break;  // Armadilha
            case 404: tileId = 13; break; // Bancada
            case 405: tileId = 15; break; // Caldeirao
            case 402: tileId = 16; break; // Circulo de Sal
            case 403: tileId = 17; break; // Coletor de Agua
            case 406: tileId = 5; break;  // Laco de Coelhos
        }

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

    public bool SplitSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots) return false;
        if (slots[slotIndex].IsEmpty() || slots[slotIndex].quantity < 2) return false;

        int emptyIdx = -1;
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i].IsEmpty()) { emptyIdx = i; break; }
        }
        if (emptyIdx < 0) return false;

        int half = slots[slotIndex].quantity / 2;
        slots[slotIndex].quantity -= half;
        slots[emptyIdx].itemId = slots[slotIndex].itemId;
        slots[emptyIdx].quantity = half;
        slots[emptyIdx].durability = slots[slotIndex].durability;

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

    public void NotifyStateLoaded()
    {
        OnInventoryChanged?.Invoke();
        OnEquipmentChanged?.Invoke();
    }
}
