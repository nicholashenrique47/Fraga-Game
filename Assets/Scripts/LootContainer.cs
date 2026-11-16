using UnityEngine;
using System.Collections.Generic;

public class LootContainer : MonoBehaviour
{
    [Header("Loot Settings")]
    public string containerName = "Caixa de Suprimentos";
    public List<int> possibleItems; // IDs dos itens possiveis (ex: Fosforos = 201)
    public int minItems = 1;
    public int maxItems = 3;
    
    private bool isOpened = false;
    public bool IsOpened => isOpened;

    public void Interact(InventoryAdvanced playerInventory)
    {
        if (isOpened)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"{containerName} ja foi saqueada.");
            return;
        }
        if (playerInventory == null) return;

        int lootCount = Random.Range(minItems, maxItems + 1);
        string foundSummary = "";
        
        for (int i = 0; i < lootCount; i++)
        {
            if (possibleItems.Count > 0)
            {
                int itemID = possibleItems[Random.Range(0, possibleItems.Count)];
                int leftover = playerInventory.AddItem(itemID, 1);
                if (leftover > 0 && LootManager.Instance != null)
                    LootManager.Instance.DropItem(itemID, leftover, transform.position + Vector3.up * 0.6f);
                ItemData d = ItemDatabase.GetItem(itemID);
                string nm = d != null ? d.itemName : $"Item {itemID}";
                foundSummary += (foundSummary.Length > 0 ? ", " : "") + "+1 " + nm;
            }
        }
        
        isOpened = true;
        
        // Efeitos Visuais / Sonoros
        AudioManager am = FindAnyObjectByType<AudioManager>();
        if (am != null) am.PlayPickupSound();

        if (HUDSurvivalOverlay.Instance != null && !string.IsNullOrEmpty(foundSummary))
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#88FFAA>{containerName}: {foundSummary}</color>");
        
        // Abrir a caixa ou mudar textura
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.material.color = Color.gray; // Feedback visual de que está vazio
        }
    }
}
