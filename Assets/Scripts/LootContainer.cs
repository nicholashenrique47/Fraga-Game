using UnityEngine;
using System.Collections.Generic;

public class LootContainer : MonoBehaviour
{
    [Header("Loot Settings")]
    public List<int> possibleItems; // IDs dos itens possiveis (ex: Fosforos = 201)
    public int minItems = 1;
    public int maxItems = 3;
    
    private bool isOpened = false;

    public void Interact(InventoryAdvanced playerInventory)
    {
        if (isOpened)
        {
            Debug.Log("[LootContainer] Já foi saqueado!");
            return;
        }

        int lootCount = Random.Range(minItems, maxItems + 1);
        
        for (int i = 0; i < lootCount; i++)
        {
            if (possibleItems.Count > 0)
            {
                int itemID = possibleItems[Random.Range(0, possibleItems.Count)];
                playerInventory.AddItem(itemID, 1);
                Debug.Log($"[LootContainer] Encontrou item ID {itemID}");
            }
        }
        
        isOpened = true;
        
        // Efeitos Visuais / Sonoros
        AudioManager am = FindAnyObjectByType<AudioManager>();
        if (am != null) am.PlayPickupSound();
        
        // Abrir a caixa ou mudar textura
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.material.color = Color.gray; // Feedback visual de que está vazio
        }
    }
}
