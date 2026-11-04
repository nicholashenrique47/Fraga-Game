using UnityEngine;

/// <summary>
/// Diretor de Tensão e Hordas da Noite.
/// Constrói tensão atmosférica ao longo da noite e dispara incursões controladas de Vultos,
/// permitindo que o jogador se defenda com tochas, fogueiras e machado.
/// </summary>
public class HordeDirector : MonoBehaviour
{
    [Header("Director Settings")]
    public float baseTension = 0f;
    public float tensionBuildRate = 0.20f; // Tensão gradual e equilibrada (levará vários minutos de noite)
    public float hordeThreshold = 100f;

    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public VultoManager vultoManager;
    public SanitySystem playerSanity;

    private bool isHordeActive = false;

    private void Update()
    {
        if (dayNightCycle == null || vultoManager == null) return;

        bool isNight = dayNightCycle.currentTime >= 20f || dayNightCycle.currentTime < 5.5f;

        if (isNight)
        {
            float sanityMod = (playerSanity != null && playerSanity.sanity < 40f) ? 1.5f : 1f;
            baseTension += tensionBuildRate * sanityMod * Time.deltaTime;

            if (baseTension >= hordeThreshold && !isHordeActive)
            {
                TriggerHorde();
            }
        }
        else
        {
            baseTension = 0f;
            isHordeActive = false;
        }
    }

    private void TriggerHorde()
    {
        isHordeActive = true;
        baseTension = 0f;
        Debug.Log("[HordeDirector] Tensão máxima da noite! Pequeno grupo de Vultos avistado nas sombras.");

        if (playerSanity != null)
        {
            int spawnCount = Random.Range(2, 5); // Incursão equilibrada para permitir combate justo
            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized * Random.Range(25f, 35f);
                vultoManager.SpawnVulto(playerSanity.transform.position.x + randomDir.x, playerSanity.transform.position.z + randomDir.y);
            }
        }
    }
}
