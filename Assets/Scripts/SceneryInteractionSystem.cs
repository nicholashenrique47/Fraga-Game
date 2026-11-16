using UnityEngine;

/// SceneryInteractionSystem: Gere interacoes do jogador com elementos de cenario.
/// Troncos caidos dao madeira e resina, espinhos dao dano, sepulturas dao ossos e reliquias, cruzes restauram sanidade.
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

    private float lastDigTime = -10f;
    private UIManager uiCache;
    private AudioManager audioCache;

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (mapRenderer == null) mapRenderer = FindAnyObjectByType<MapRenderer>();
        if (vultoManager == null) vultoManager = FindAnyObjectByType<VultoManager>();
        uiCache = FindAnyObjectByType<UIManager>();
        audioCache = AudioManager.Instance ?? FindAnyObjectByType<AudioManager>();
    }

    private void Update()
    {
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;

        int pX = Mathf.RoundToInt(playerTransform.position.x);
        int pZ = Mathf.RoundToInt(playerTransform.position.z);
        CheckPassiveEffects(pX, pZ);
    }

    private void CheckPassiveEffects(int px, int pz)
    {
        for (int x = px - 1; x <= px + 1; x++)
        {
            for (int z = pz - 1; z <= pz + 1; z++)
            {
                if (x < 0 || x >= gameManager.mapWidth || z < 0 || z >= gameManager.mapHeight) continue;

                int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, z);

                if (tile == 19 && x == px && z == pz && playerStatus != null)
                {
                    playerStatus.TakeDamagePassive(thornyDamagePerSecond * Time.deltaTime);
                }

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
        if (playerTransform == null) return;
        Vector3 forward = playerTransform.forward;
        int targetX = px + Mathf.RoundToInt(forward.x);
        int targetZ = pz + Mathf.RoundToInt(forward.z);
        TryInteractAtTile(targetX, targetZ);
    }

    public void TryInteractAtTile(int targetX, int targetZ)
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;
        if (targetX < 0 || targetX >= gameManager.mapWidth || targetZ < 0 || targetZ >= gameManager.mapHeight) return;

        int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, targetX, targetZ);
        float gy = TerrainMeshGenerator.GetTerrainHeight(targetX, targetZ);
        Vector3 worldPos = new Vector3(targetX, gy + 0.5f, targetZ);

        if (tile == 5 || tile == 6)
        {
            bool hasAxe = inventory != null && (inventory.HasItem(300) || inventory.HasItem(312) || inventory.HasItem(311));
            if (hasAxe)
            {
                gameManager.UpdateMapCellNative(targetX, targetZ, 18);
                if (mapRenderer != null)
                {
                    mapRenderer.RemoveInstanceAt(targetX, targetZ, tile);
                    mapRenderer.AddInstanceAt(targetX, targetZ, 18);
                }

                GameObject droppedLogPrefab = Resources.Load<GameObject>("Prefabs/DroppedWood_Prefab");
                if (droppedLogPrefab != null)
                {
                    for (int i = 0; i < 3; i++)
                        Instantiate(droppedLogPrefab, new Vector3(targetX, gy + 1.5f + i, targetZ), Quaternion.identity);
                }
                else
                {
                    AddOrDrop(100, 3, worldPos);
                }
                AddOrDrop(119, 1, worldPos);

                if (VFXPoolManager.Instance != null)
                    VFXPoolManager.Instance.PlayWoodChipVFX(new Vector3(targetX, gy + 1.5f, targetZ), Vector3.up);
                ShowMsg("Arvore abatida! +Resina de Pinheiro");
                (audioCache ?? AudioManager.Instance)?.PlayPickupSound();
                if (GameFeelManager.Instance != null) GameFeelManager.Instance.TriggerHitstop(0.06f);
                if (FirstPersonCamera.Instance != null) FirstPersonCamera.Instance.AddShake(0.24f);
            }
            else
            {
                ShowMsg("Precisas de um Machado para derrubar!");
            }
        }
        else if (tile == 18)
        {
            gameManager.UpdateMapCellNative(targetX, targetZ, 0);
            if (mapRenderer != null) mapRenderer.RemoveInstanceAt(targetX, targetZ, 18);

            AddOrDrop(100, 2, worldPos);
            bool gotResin = Random.value < 0.45f;
            if (gotResin) AddOrDrop(119, 1, worldPos);

            if (VFXPoolManager.Instance != null)
                VFXPoolManager.Instance.PlayWoodChipVFX(new Vector3(targetX, gy + 0.4f, targetZ), Vector3.up);
            (audioCache ?? AudioManager.Instance)?.PlayPickupSound();
            ShowMsg(gotResin ? "+2 Madeira, +1 Resina (Tronco Caido)" : "+2 Madeira (Tronco Caido)");
        }
        else if (tile == 22 && Time.time - lastDigTime > 5f)
        {
            lastDigTime = Time.time;

            int[] rareLoot = { 106, 107, 114, 120, 121, 116, 206 };
            int lootID = rareLoot[Random.Range(0, rareLoot.Length)];
            AddOrDrop(120, 1, worldPos); // Osso garantido
            AddOrDrop(lootID, 1, worldPos);

            ItemData lootData = ItemDatabase.GetItem(lootID);
            ShowMsg($"Escavou: +1 Osso & {(lootData != null ? lootData.itemName : "Reliquia")}!");
            (audioCache ?? AudioManager.Instance)?.PlayPickupSound();

            gameManager.UpdateMapCellNative(targetX, targetZ, 0);
            if (mapRenderer != null) mapRenderer.RemoveInstanceAt(targetX, targetZ, 22);

            if (vultoManager != null)
            {
                vultoManager.SpawnVulto(targetX + Random.Range(-3f, 3f), targetZ + Random.Range(-3f, 3f));
                vultoManager.SpawnVulto(targetX + Random.Range(-3f, 3f), targetZ + Random.Range(-3f, 3f));
                ShowMsg("Perturbaste os mortos! Vultos surgiram!");
            }

            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(targetX, gy, targetZ));
            if (sanitySystem != null) sanitySystem.sanity -= 10f;
        }
        else if (tile == 17)
        {
            if (sanitySystem != null) sanitySystem.sanity = Mathf.Min(100f, sanitySystem.sanity + 5f);
            ShowMsg("As runas celtas restauraram tua mente (+5 Sanidade).");
        }
        else if (tile == 30)
        {
            if (inventory != null && inventory.HasItem(115))
            {
                Vector3 altarPos = new Vector3(targetX, gy, targetZ);
                if (uiCache != null) uiCache.ShowAltarEndingChoice(altarPos, inventory);
                else if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayCelticBeaconVFX(altarPos);
            }
            else
            {
                ShowMsg("O Altar exige o Coracao Sombrio do Vulto Anciao...");
            }
        }
    }

    private void AddOrDrop(int itemId, int amount, Vector3 dropPos)
    {
        if (amount <= 0) return;
        int leftover = inventory != null ? inventory.AddItem(itemId, amount) : amount;
        if (leftover > 0 && LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(itemId, leftover, dropPos);
        }
    }

    private void ShowMsg(string msg)
    {
        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
        else if (uiCache != null) uiCache.SetLootTooltip(msg);
    }
}
