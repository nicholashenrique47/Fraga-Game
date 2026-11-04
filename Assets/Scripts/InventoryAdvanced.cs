using UnityEngine;
using System;
using System.Collections.Generic;

/// InventoryAdvanced: Sistema de inventario com slots, stacking e consumo.
/// Substitui o InventorySystem basico anterior com logica de slots fisicos.
/// Integra com ItemDatabase, PlayerStatus e CraftingSystem.

[System.Serializable]
public class InventorySlot
{
    public int itemId = -1;     // -1 = slot vazio
    public int quantity = 0;
    public float durability = -1f; // -1 = nao apodrece

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
    [Header("Config")]
    public int maxSlots = 20;

    [Header("Dependencies")]
    public PlayerStatus playerStatus;
    public SanitySystem sanitySystem;
    public BuildingSystem buildingSystem;

    // Slots do inventario
    public InventorySlot[] slots;

    // Eventos para a UI
    public event Action OnInventoryChanged;

    private void Awake()
    {
        ItemDatabase.Initialize();
        slots = new InventorySlot[maxSlots];
        for (int i = 0; i < maxSlots; i++)
        {
            slots[i] = new InventorySlot();
        }

        // Itens iniciais de sobrevivencia
        AddItem(205, 3);  // 3 Fosforos
        AddItem(106, 2);  // 2 Sal
        AddItem(202, 5);  // 5 Bagas Silvestres
    }

    private void Update()
    {
        bool changed = false;
        // Podridao de Itens (Spoilage)
        for (int i = 0; i < maxSlots; i++)
        {
            if (!slots[i].IsEmpty() && slots[i].durability > 0f)
            {
                slots[i].durability -= Time.deltaTime;
                if (slots[i].durability <= 0f)
                {
                    // Apodreceu
                    slots[i].itemId = 108; // 108 = Comida Podre
                    slots[i].durability = -1f;
                    changed = true;
                    Debug.LogWarning($"[Inventory] O item no slot {i} apodreceu!");
                }
            }
        }

        if (changed && OnInventoryChanged != null) OnInventoryChanged.Invoke();
    }

    /// Adiciona um item ao inventario. Retorna a quantidade que nao coube.
    public int AddItem(int itemId, int amount)
    {
        ItemData data = ItemDatabase.GetItem(itemId);
        if (data == null) return amount;

        int remaining = amount;

        // Fase 1: Tentar empilhar em slots existentes do mesmo item
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
                slots[i].durability = data.isPerishable ? data.maxDurability : -1f;
                remaining -= canAdd;
            }
        }

        if (remaining < amount)
        {
            if (OnInventoryChanged != null) OnInventoryChanged.Invoke();
        }

        if (remaining > 0)
        {
            Debug.LogWarning($"[Inventory] Inventario cheio. {remaining}x {data.itemName} perdidos.");
        }

        return remaining;
    }

    /// Remove uma quantidade de um item. Retorna true se conseguiu.
    public bool RemoveItem(int itemId, int amount)
    {
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

        if (OnInventoryChanged != null) OnInventoryChanged.Invoke();
        return true;
    }

    /// Conta quantas unidades de um item existem no inventario.
    public int GetItemCount(int itemId)
    {
        int count = 0;
        for (int i = 0; i < maxSlots; i++)
        {
            if (slots[i].itemId == itemId) count += slots[i].quantity;
        }
        return count;
    }

    /// Alias compatível para checagem rápida de quantidade
    public int CountItem(int itemId) => GetItemCount(itemId);

    /// Verifica se um item existe no inventario (pelo menos 1).
    public bool HasItem(int itemId)
    {
        return GetItemCount(itemId) > 0;
    }

    /// Usa um item consumivel do slot indicado.
    public bool UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots) return false;
        if (slots[slotIndex].IsEmpty()) return false;

        ItemData data = ItemDatabase.GetItem(slots[slotIndex].itemId);
        if (data == null) return false;

        if (data.type == ItemType.Consumable)
        {
            // Aplicar efeitos de comida e bebida
            if (playerStatus != null)
            {
                if (data.hungerRestore > 0f || data.sanityRestore != 0f)
                {
                    playerStatus.Eat(data.hungerRestore, data.sanityRestore);
                }
                
                if (data.thirstRestore > 0f)
                {
                    playerStatus.Drink(data.thirstRestore);
                }
                
                if (data.healthRestore > 0f)
                {
                    playerStatus.Heal(data.healthRestore);
                }
            }

            // Aplicar veneno (Carne Crua = 50% chance, Cogumelo = 100%)
            if (data.poisonDuration > 0f && playerStatus != null)
            {
                if (data.id == 104) 
                {
                    if (UnityEngine.Random.value > 0.5f) 
                    {
                        playerStatus.ApplyPoison(data.poisonDuration);
                        Debug.LogWarning("[Inventory] A Carne Crua estava estragada. Foste envenenado!");
                    }
                }
                else
                {
                    playerStatus.ApplyPoison(data.poisonDuration);
                }
            }

            // Curar veneno (Ervas Medicinais / Sopa de Ervas)
            if (data.poisonDuration < 0f && playerStatus != null)
            {
                playerStatus.isPoisoned = false;
                playerStatus.poisonTimer = 0f;
                Debug.Log("[Inventory] Veneno curado.");
            }

            // Fosforos: acender fogueira
            if (data.id == 205 && buildingSystem != null)
            {
                buildingSystem.ForceBuild(4);
            }
            
            // Lore: Página do Diário
            if (data.id == 116)
            {
                LoreSystem lore = FindAnyObjectByType<LoreSystem>();
                if (lore != null) lore.ShowNextPage();
            }

            slots[slotIndex].quantity--;
            if (slots[slotIndex].quantity <= 0) slots[slotIndex].Clear();

            if (OnInventoryChanged != null) OnInventoryChanged.Invoke();
            Debug.Log($"[Inventory] Usado: {data.itemName}");
            return true;
        }

        return false;
    }

    /// Descarta um item de um slot (para gerir espaco).
    public void DropItem(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots) return;
        if (slots[slotIndex].IsEmpty()) return;

        int toDrop = Mathf.Min(amount, slots[slotIndex].quantity);
        slots[slotIndex].quantity -= toDrop;
        if (slots[slotIndex].quantity <= 0) slots[slotIndex].Clear();

        if (OnInventoryChanged != null) OnInventoryChanged.Invoke();
    }

    /// Troca dois slots de posicao (para a UI de drag-and-drop).
    public void SwapSlots(int slotA, int slotB)
    {
        if (slotA < 0 || slotA >= maxSlots || slotB < 0 || slotB >= maxSlots) return;

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

        if (OnInventoryChanged != null) OnInventoryChanged.Invoke();
    }
}