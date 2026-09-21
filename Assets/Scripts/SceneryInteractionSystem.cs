using UnityEngine;

/// SceneryInteractionSystem: Gere interacoes do jogador com elementos de cenario.
/// Troncos caidos dao madeira, espinhos dao dano, sepulturas dao loot, cruzes restauram sanidade.
public class SceneryInteractionSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerTransform;
    public GameManager gameManager;
    public InventoryAdvanced inventory;
    public SanitySystem sanitySystem;
    public PlayerStatus playerStatus;
    public MapRenderer mapRenderer;
    public VultoManager vultoManager;
    
    [Header("Config")]
    public float interactRange = 2.5f;
    public float thornyDamagePerSecond = 5f;
    public float crossSanityPerSecond = 2f;
    
    private float lastDigTime = 0f;
    
    private void Update()
    {
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;
        
        int pX = Mathf.RoundToInt(playerTransform.position.x);
        int pZ = Mathf.RoundToInt(playerTransform.position.z);
        
        // Verificar tiles adjacentes para efeitos passivos
        CheckPassiveEffects(pX, pZ);
        
        // Interacao ativa (pressionar E)
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteractWithScenery(pX, pZ);
        }
    }
    
    private void CheckPassiveEffects(int px, int pz)
    {
        for (int x = px - 1; x <= px + 1; x++)
        {
            for (int z = pz - 1; z <= pz + 1; z++)
            {
                if (x < 0 || x >= gameManager.mapWidth || z < 0 || z >= gameManager.mapHeight) continue;
                
                int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, z);
                
                // Arbusto Espinhoso (19): causa dano ao pisar
                if (tile == 19 && x == px && z == pz)
                {
                    if (playerStatus != null)
                    {
                        playerStatus.TakeDamagePassive(thornyDamagePerSecond * Time.deltaTime);
                    }
                }
                
                // Cruz Celta (21): restaura sanidade quando perto
                if (tile == 21)
                {
                    float dist = Vector2.Distance(new Vector2(px, pz), new Vector2(x, z));
                    if (dist < interactRange && sanitySystem != null)
                    {
                        sanitySystem.sanity = Mathf.Min(100f, sanitySystem.sanity + crossSanityPerSecond * Time.deltaTime);
                    }
                }
            }
        }
    }
    
    public void TryInteractWithScenery(int px, int pz)
    {
        // Verificar tiles num raio de 2 a frente do jogador
        Vector3 forward = playerTransform.forward;
        int targetX = px + Mathf.RoundToInt(forward.x);
        int targetZ = pz + Mathf.RoundToInt(forward.z);
        
        if (targetX < 0 || targetX >= gameManager.mapWidth || targetZ < 0 || targetZ >= gameManager.mapHeight) return;
        
        int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, targetX, targetZ);
        UIManager ui = FindAnyObjectByType<UIManager>();
        
                        // Arvore Viva (5 ou 6): Cortar para transformar em tronco
        if (tile == 5 || tile == 6)
        {
            if (inventory != null && inventory.HasItem(300)) // Requer machado
            {
                gameManager.UpdateMapCellNative(targetX, targetZ, 18);
                if (mapRenderer != null) 
                {
                    mapRenderer.RemoveInstanceAt(targetX, targetZ, tile);
                    mapRenderer.AddInstanceAt(targetX, targetZ, 18);
                }
                
                // --- PHYSICS DROP ---
                GameObject droppedLogPrefab = Resources.Load<GameObject>("Prefabs/DroppedWood_Prefab");
                if (droppedLogPrefab != null)
                {
                    for(int i = 0; i < 3; i++) {
                        Instantiate(droppedLogPrefab, new Vector3(targetX, 2f + i, targetZ), Quaternion.identity);
                    }
                }
                
                                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(targetX, 3f, targetZ));
                if (ui != null) ui.SetLootTooltip("TIMBER!");
                
                AudioManager am = FindAnyObjectByType<AudioManager>();
                if (am != null) am.PlayHorrorSting(); // Som forte de quebrar arvore
                
                // --- AAA POLISH: Hitstop & Shake ---
                if (GameFeelManager.Instance != null) GameFeelManager.Instance.TriggerHitstop(0.08f);
                if (FirstPersonCamera.Instance != null) FirstPersonCamera.Instance.AddShake(0.3f);
            }
        }

        // Tronco Caido (18): colher para madeira extra
        if (tile == 18)
        {
            if (inventory != null && inventory.HasItem(300)) // Requer machado
            {
                gameManager.UpdateMapCellNative(targetX, targetZ, 0);
                if (mapRenderer != null) mapRenderer.RemoveInstanceAt(targetX, targetZ, 18);
                
                GameObject droppedLogPrefab = Resources.Load<GameObject>("Prefabs/DroppedWood_Prefab");
                if (droppedLogPrefab != null)
                {
                    for(int i = 0; i < 2; i++) {
                        Instantiate(droppedLogPrefab, new Vector3(targetX, 1f, targetZ), Quaternion.identity);
                    }
                }
                
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(targetX, 0, targetZ));
                if (ui != null) ui.SetLootTooltip("Tronco destruido.");
            }
        }
        
        // Sepultura Antiga (22): escavar para loot raro + spawn vulto
        if (tile == 22 && Time.time - lastDigTime > 5f)
        {
            lastDigTime = Time.time;
            
            // Loot raro aleatorio
            int[] rareLoot = { 106, 107, 114, 206 }; // Sal, Ferro, Pena, Ligaduras
            int lootID = rareLoot[Random.Range(0, rareLoot.Length)];
            
            if (inventory != null) inventory.AddItem(lootID, 1);
            
            ItemData lootData = ItemDatabase.GetItem(lootID);
            if (ui != null) ui.SetLootTooltip($"Escavou: {(lootData != null ? lootData.itemName : "???")}!");
            
            // Consumir sepultura
            gameManager.UpdateMapCellNative(targetX, targetZ, 0);
            if (mapRenderer != null) mapRenderer.RemoveInstanceAt(targetX, targetZ, 22);
            
            // Spawn vulto como consequencia!
            if (vultoManager != null)
            {
                vultoManager.SpawnVulto(targetX + Random.Range(-3f, 3f), targetZ + Random.Range(-3f, 3f));
                vultoManager.SpawnVulto(targetX + Random.Range(-3f, 3f), targetZ + Random.Range(-3f, 3f));
                if (ui != null) ui.SetLootTooltip("Perturbaste os mortos...");
                Debug.LogWarning("[Scenery] SEPULTURA PROFANADA! Vultos emergiram!");
            }
            
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(targetX, 0, targetZ));
            
            // Perda de sanidade
            if (sanitySystem != null) sanitySystem.sanity -= 10f;
        }
        
        // Pedra Runica (17): ler para ganhar sanidade
        if (tile == 17)
        {
            if (sanitySystem != null) sanitySystem.sanity = Mathf.Min(100f, sanitySystem.sanity + 5f);
            if (ui != null) ui.SetLootTooltip("As runas sussurram sabedoria...");
        }
        
        // Altar de Purificacao (30): Vitoria do Jogo
        if (tile == 30)
        {
            if (inventory != null && inventory.HasItem(115)) // CoraÃƒÆ’Ã‚Â§ÃƒÆ’Ã‚Â£o Sombrio
            {
                // VENCEU O JOGO!
                if (ui != null) ui.ShowVictoryPanel();
                
                // Efeito visual
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(targetX, 0, targetZ));
            }
            else
            {
                if (ui != null) ui.SetLootTooltip("O Altar exige o Coracao Sombrio...");
            }
        }
    }
}
