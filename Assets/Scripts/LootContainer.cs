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
        AudioManager am = AudioManager.Instance ?? FindAnyObjectByType<AudioManager>();
        if (am != null) am.PlayFoley(AudioManager.FoleyEventType.ChestWoodLatch, transform.position, 0.76f);

        if (HUDSurvivalOverlay.Instance != null && !string.IsNullOrEmpty(foundSummary))
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#88FFAA>{containerName}: {foundSummary}</color>");
        
        // Abrir a caixa ou mudar textura
        Transform lid = transform.Find("Crate_Lid");
        if (lid != null)
        {
            lid.localPosition = new Vector3(0f, 0.68f, -0.18f);
            lid.localEulerAngles = new Vector3(-28f, 0f, -2.5f);
        }

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", new Color(0.45f, 0.45f, 0.45f, 1f));
            mpb.SetColor("_Color", new Color(0.45f, 0.45f, 0.45f, 1f));
            renderers[i].SetPropertyBlock(mpb);
        }
    }
}
