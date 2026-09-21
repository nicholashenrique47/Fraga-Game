using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    [Header("Consumíveis")]
    public int matches = 2; // Fósforos: Acendem uma fogueira ignorando recursos
    public int saltBags = 1; // Sal: Cria círculo que repele inimigos instantaneamente
    
    [Header("Dependências")]
    public BuildingSystem buildingSystem;
    public SanitySystem sanitySystem;

    private void Update()
    {
        // 1 para Fósforos, 2 para Sal
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            UseMatch();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            UseSalt();
        }
    }

    public void UseMatch()
    {
        if (matches <= 0 || buildingSystem == null) return;

        // Tenta forçar a construção de uma Fogueira (ID 4)
        if (buildingSystem.ForceBuild(4))
        {
            matches--;
            Debug.Log($"[Inventory] Fósforo usado! Restantes: {matches}");
        }
    }

    public void UseSalt()
    {
        if (saltBags <= 0) return;

        // Cria círculo de sal virtual que afasta vultos por 10 segundos
        // Pode ser implementado no C++ como um pulso massivo de luz
        saltBags--;
        Debug.Log($"[Inventory] Círculo de Sal ativo! Restantes: {saltBags}");
        
        // Bonus instantâneo de sanidade pelo alívio
        if (sanitySystem != null)
        {
            sanitySystem.sanity += 30f; 
        }
    }
}
