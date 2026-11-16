using UnityEngine;

public class HubManager : MonoBehaviour
{
    [Header("Meta Data")]
    public MetaData metaData;

    [Header("UI Logic (Mock)")]
    public bool isHubActive = false; // Controla se estamos no menu principal/igreja

    // Preços base
    private int basePrice = 50;

    private void Awake()
    {
        metaData = MetaSaveSystem.LoadMeta();
    }

    public void OpenHub()
    {
        isHubActive = true;
        
        // FASE 3: Teleport físico para o "Santuário"
        Transform player = FindAnyObjectByType<PlayerController>()?.transform;
        if (player != null)
        {
            player.position = new Vector3(-500f, 1f, -500f);
            
            // Opcionalmente, desligar o ciclo de dia/noite temporariamente
            DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
            if (cycle != null) cycle.enabled = false;
        }

        Debug.Log($"[Santuário] Entrou no Hub Físico. Almas disponíveis: {metaData.totalSouls}");
    }

    public void CloseHub()
    {
        isHubActive = false;
        
        // O jogador pede para sair, vamos fazer "Reset" à floresta ou simplesmente mandá-lo para o meio
        Transform player = FindAnyObjectByType<PlayerController>()?.transform;
        if (player != null)
        {
            GameManager gm = FindAnyObjectByType<GameManager>();
            float cx = gm != null ? gm.mapWidth * 0.5f : FragaWorldBlueprint.CrashCampPos.x;
            float cz = gm != null ? gm.mapHeight * 0.5f : FragaWorldBlueprint.CrashCampPos.y;
            float cy = TerrainMeshGenerator.GetTerrainHeight(cx, cz) + 1.0f;
            player.position = new Vector3(cx, cy, cz);
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.SnapToGround();
            
            SanitySystem sanity = player.GetComponent<SanitySystem>();
            if (sanity != null)
            {
                sanity.sanity = 100f;
                sanity.oil = 100f;
            }

            DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
            if (cycle != null) cycle.enabled = true;
        }

        MetaSaveSystem.SaveMeta(metaData);
        Debug.Log("[Santuário] Saiu do Santuário, regressando à Fraga. Sanidade restaurada.");
    }

    public bool TryBuyLampUpgrade()
    {
        int cost = basePrice + (metaData.upgradeLampLevel * 25);
        if (metaData.totalSouls >= cost)
        {
            metaData.totalSouls -= cost;
            metaData.upgradeLampLevel++;
            MetaSaveSystem.SaveMeta(metaData);
            Debug.Log($"[Santuário] Lampião melhorado para nível {metaData.upgradeLampLevel}!");
            return true;
        }
        return false;
    }

    public bool TryBuySpeedUpgrade()
    {
        int cost = basePrice + (metaData.upgradeSpeedLevel * 25);
        if (metaData.totalSouls >= cost)
        {
            metaData.totalSouls -= cost;
            metaData.upgradeSpeedLevel++;
            MetaSaveSystem.SaveMeta(metaData);
            return true;
        }
        return false;
    }

    public void AddSouls(int amount)
    {
        metaData.totalSouls += amount;
        MetaSaveSystem.SaveMeta(metaData);
    }
}
