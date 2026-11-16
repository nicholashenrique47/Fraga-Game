using UnityEngine;

/// <summary>
/// SkinningCarcass: Carcaca temporaria deixada por animais abatidos (Cervo, Lobo, Javali, Urso).
/// Permite esfola interativa com Faca de Caca (bonus) ou maos livres, rendendo Carne, Pele e Ossos.
/// </summary>
public class SkinningCarcass : MonoBehaviour
{
    public string animalDisplayName = "Cervo";
    public int meatItemId = 104;
    public int meatAmount = 3;
    public int hideItemId = 105;
    public int hideAmount = 2;
    public int boneItemId = 120;
    public int boneAmount = 2;
    public int bonusDropId = -1;
    public float lifeTimeSeconds = 35f;

    public bool IsDepleted { get; private set; }

    private void Start()
    {
        Destroy(gameObject, lifeTimeSeconds);
    }

    public void Harvest(InventoryAdvanced inv)
    {
        if (IsDepleted) return;
        IsDepleted = true;

        bool hasKnife = inv != null && inv.equippedMainHandId == 302;
        int finalMeat = hasKnife ? meatAmount + 1 : meatAmount;
        int finalHide = hasKnife ? hideAmount + 1 : hideAmount;
        int finalBones = hasKnife ? boneAmount + 1 : boneAmount;

        AddOrDrop(inv, meatItemId, finalMeat);
        AddOrDrop(inv, hideItemId, finalHide);
        AddOrDrop(inv, boneItemId, finalBones);
        if (bonusDropId > 0 && hasKnife) AddOrDrop(inv, bonusDropId, 1);

        if (FirstPersonViewmodelController.Instance != null)
        {
            FirstPersonViewmodelController.Instance.TriggerConsumeAnimation();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPickupSound();
        }

        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position + Vector3.up * 0.2f);
        }

        if (HUDSurvivalOverlay.Instance != null)
        {
            string bonusTag = hasKnife ? " [Bonus Faca]" : "";
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Esfolou {animalDisplayName}: +{finalMeat} Carne, +{finalHide} Pele, +{finalBones} Osso{bonusTag}");
        }

        Destroy(gameObject, 0.1f);
    }

    private void AddOrDrop(InventoryAdvanced inv, int itemId, int amount)
    {
        if (itemId <= 0 || amount <= 0) return;
        int leftover = inv != null ? inv.AddItem(itemId, amount) : amount;
        if (leftover > 0 && LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(itemId, leftover, transform.position + Vector3.up * 0.25f);
        }
    }
}
