using UnityEngine;

public class HordeDirector : MonoBehaviour
{
    [Header("Director Settings")]
    public float baseTension = 0f;
    public float tensionBuildRate = 1f; // Tensão aumenta a cada segundo da noite
    public float hordeThreshold = 100f;

    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public VultoManager vultoManager;
    public SanitySystem playerSanity;

    private bool isHordeActive = false;

    private void Update()
    {
        if (dayNightCycle == null || vultoManager == null) return;

        bool isNight = dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f;

        if (isNight)
        {
            // Tensão aumenta mais rapidamente se a sanidade estiver baixa
            float sanityMod = (playerSanity != null && playerSanity.sanity < 50f) ? 2f : 1f;
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
        baseTension = 0f; // Reset tensão para a próxima onda
        Debug.Log("[HordeDirector] Tensão máxima atingida! Onda de Vultos a spawnar.");

        // Spawn agressivo na borda do Flow Field
        int spawnCount = Random.Range(10, 25);
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized * 25f;
            vultoManager.SpawnVulto(playerSanity.transform.position.x + randomDir.x, playerSanity.transform.position.z + randomDir.y);
        }

        // Eventualmente poderíamos tocar um som de "Horda" no AudioManager
    }
}
