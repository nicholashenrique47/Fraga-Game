using UnityEngine;

public class BonfireEntity : MonoBehaviour
{
    [Header("Fuel System")]
    public float fuelLevel = 100f; // 0 a 100
    public float burnRate = 1.5f;  // Drena X por segundo
    public Light fireLight;
    public ParticleSystem fireParticles;
    
    [Header("Cooking System")]
    public bool isCooking = false;
    public float cookTimer = 0f;
    public float cookDuration = 10f; // Segundos para assar carne
    public int itemBeingCooked = -1; // -1 = Vazio, 104 = Carne Crua
    
    public bool isLit { get; private set; } = true;

    private void Update()
    {
        if (!isLit) return;

        // Drena combustivel
        fuelLevel -= burnRate * Time.deltaTime;
        if (fuelLevel <= 0f)
        {
            fuelLevel = 0f;
            Extinguish();
        }

        // Visuais de Fogo ligados ao combustivel
        if (fireLight != null)
        {
            fireLight.intensity = Mathf.Lerp(0.5f, 4f, fuelLevel / 100f);
            fireLight.range = Mathf.Lerp(2f, 10f, fuelLevel / 100f);
        }

        // Sistema de Assar Comida
        if (isCooking)
        {
            cookTimer += Time.deltaTime;
            if (cookTimer >= cookDuration)
            {
                FinishCooking();
            }
        }
    }

    public void AddFuel(float amount)
    {
        fuelLevel = Mathf.Clamp(fuelLevel + amount, 0f, 100f);
        if (!isLit && fuelLevel > 0f) Ignite();
    }

    public bool TryStartCooking(int rawItemID)
    {
        if (!isLit || isCooking) return false;
        
        // 104 = Carne Crua, 103 = Ervas
        if (rawItemID == 104 || rawItemID == 103)
        {
            isCooking = true;
            cookTimer = 0f;
            itemBeingCooked = rawItemID;
            return true;
        }
        return false;
    }

    private void FinishCooking()
    {
        isCooking = false;
        int cookedItemID = -1;
        
        if (itemBeingCooked == 104) cookedItemID = 200; // Carne Assada
        else if (itemBeingCooked == 103) cookedItemID = 201; // Sopa de Ervas

        itemBeingCooked = -1;
        
        if (cookedItemID != -1)
        {
            // Fica pronta. Tenta ejetar para o chao ou dar ao jogador se estiver perto
            LootManager loot = FindAnyObjectByType<LootManager>();
            if (loot != null)
            {
                loot.DropItem(cookedItemID, 1, transform.position + Vector3.up);
            }
        }
    }

    public void Ignite()
    {
        isLit = true;
        if (fireLight != null) fireLight.enabled = true;
        if (fireParticles != null) fireParticles.Play();
    }

    public void Extinguish()
    {
        isLit = false;
        if (fireLight != null) fireLight.enabled = false;
        if (fireParticles != null) fireParticles.Stop();
        if (isCooking)
        {
            // Perde o progresso
            isCooking = false;
            itemBeingCooked = -1;
        }
    }
}
