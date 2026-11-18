using UnityEngine;

/// SceneryInteractionSystem: Gere interacoes do jogador com elementos de cenario.
/// Troncos caidos dao madeira e resina (exigem Machado), espinhos dao dano, sepulturas dao ossos e reliquias, cruzes restauram sanidade.
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
    private readonly System.Collections.Generic.Dictionary<Vector2Int, float> runicCooldowns = new System.Collections.Generic.Dictionary<Vector2Int, float>();
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
        TryInteractAtTileOrObject(targetX, targetZ, 0, null);
    }

    public void TryInteractAtTileOrObject(int targetX, int targetZ, int fallbackTileId, Collider targetCollider)
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (mapRenderer == null) mapRenderer = FindAnyObjectByType<MapRenderer>();

        int tile = fallbackTileId;
        if (gameManager != null && gameManager.mapDataPtr != System.IntPtr.Zero &&
            targetX >= 0 && targetX < gameManager.mapWidth && targetZ >= 0 && targetZ < gameManager.mapHeight)
        {
            int nativeTile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, targetX, targetZ);
            if (nativeTile > 1) tile = nativeTile;
        }

        float gy = TerrainMeshGenerator.GetTerrainHeight(targetX, targetZ);
        Vector3 worldPos = new Vector3(targetX, gy + 0.5f, targetZ);

        if (tile == 6 || tile == 18)
        {
            if (!ChoppingAndCombatSystem.EnsureAxeEquipped(inventory, true))
            {
                if (ChoppingAndCombatSystem.Instance != null)
                    ChoppingAndCombatSystem.Instance.ShowToolRequiredForWoodWarning();
                else
                    ShowMsg("Precisa equipar um Machado para cortar madeira!");
                return;
            }

            if (FirstPersonViewmodelController.Instance != null)
                FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(0, false);

            if (tile == 6)
            {
                if (gameManager != null) gameManager.UpdateMapCellNative(targetX, targetZ, 18);
                if (mapRenderer != null)
                {
                    mapRenderer.RemoveInstanceAt(targetX, targetZ, tile);
                    mapRenderer.AddInstanceAt(targetX, targetZ, 18);
                }

                AddOrDrop(100, 3, worldPos);
                AddOrDrop(119, 1, worldPos);

                if (VFXPoolManager.Instance != null)
                    VFXPoolManager.Instance.PlayWoodChipVFX(new Vector3(targetX, gy + 1.5f, targetZ), Vector3.up);
                inventory.DamageEquippedTool(EquipSlotType.MainHand, 1f);
                int totalW = inventory.GetItemCount(100);
                ShowMsg($"Arvore abatida! +3 Madeira (Total: {totalW}), +1 Resina");
                (audioCache ?? AudioManager.Instance)?.PlayFoley(AudioManager.FoleyEventType.TreeFall, worldPos, 0.88f);
                if (GameFeelManager.Instance != null) GameFeelManager.Instance.TriggerHitstop(0.06f);
                if (FirstPersonCamera.Instance != null) FirstPersonCamera.Instance.AddShake(0.24f);
            }
            else
            {
                if (gameManager != null) gameManager.UpdateMapCellNative(targetX, targetZ, 0);
                if (mapRenderer != null) mapRenderer.RemoveInstanceAt(targetX, targetZ, 18);
                if (targetCollider != null)
                {
                    GameObject propObj = ChoppingAndCombatSystem.Instance != null
                        ? ChoppingAndCombatSystem.Instance.FindSpecificPropObject(targetCollider)
                        : targetCollider.gameObject;
                    if (propObj != null) Destroy(propObj);
                }

                AddOrDrop(100, 2, worldPos);
                bool gotResin = Random.value < 0.45f;
                if (gotResin) AddOrDrop(119, 1, worldPos);

                if (VFXPoolManager.Instance != null)
                    VFXPoolManager.Instance.PlayWoodChipVFX(new Vector3(targetX, gy + 0.4f, targetZ), Vector3.up);
                inventory.DamageEquippedTool(EquipSlotType.MainHand, 1f);
                (audioCache ?? AudioManager.Instance)?.PlayFoley(AudioManager.FoleyEventType.ChopWood, worldPos, 0.82f);
                int totalW = inventory.GetItemCount(100);
                ShowMsg(gotResin ? $"+2 Madeira (Total: {totalW}), +1 Resina [Tronco Cortado]" : $"+2 Madeira (Total: {totalW}) [Tronco Cortado]");
            }
        }
        else if (tile == 22 && Time.time - lastDigTime > 5f)
        {
            lastDigTime = Time.time;

            int[] rareLoot = { 106, 107, 114, 120, 121, 116, 206 };
            int lootID = rareLoot[Random.Range(0, rareLoot.Length)];
            AddOrDrop(120, 1, worldPos);
            AddOrDrop(lootID, 1, worldPos);

            ItemData lootData = ItemDatabase.GetItem(lootID);
            ShowMsg($"Escavou: +1 Osso & {(lootData != null ? lootData.itemName : "Reliquia")}!");
            (audioCache ?? AudioManager.Instance)?.PlayPickupSoundForItem(lootID, worldPos);

            if (gameManager != null) gameManager.UpdateMapCellNative(targetX, targetZ, 0);
            if (mapRenderer != null) mapRenderer.RemoveInstanceAt(targetX, targetZ, 22);
            if (targetCollider != null)
            {
                GameObject propObj = ChoppingAndCombatSystem.Instance != null
                    ? ChoppingAndCombatSystem.Instance.FindSpecificPropObject(targetCollider)
                    : targetCollider.gameObject;
                if (propObj != null) Destroy(propObj);
            }

            if (vultoManager != null)
            {
                vultoManager.SpawnVulto(targetX + Random.Range(-3f, 3f), targetZ + Random.Range(-3f, 3f));
                vultoManager.SpawnVulto(targetX + Random.Range(-3f, 3f), targetZ + Random.Range(-3f, 3f));
                ShowMsg("Voce perturbou os mortos! Vultos surgiram!");
            }

            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(targetX, gy, targetZ));
            if (sanitySystem != null) sanitySystem.sanity -= 10f;
        }
        else if (tile == 17)
        {
            Vector2Int runeKey = new Vector2Int(targetX, targetZ);
            if (runicCooldowns.TryGetValue(runeKey, out float readyAt) && Time.time < readyAt)
            {
                ShowMsg($"Pedra Runica em recarga ({Mathf.CeilToInt(readyAt - Time.time)}s).");
            }
            else
            {
                runicCooldowns[runeKey] = Time.time + 60f;
                if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
                if (sanitySystem != null) sanitySystem.sanity = Mathf.Min(100f, sanitySystem.sanity + 8f);
                (audioCache ?? AudioManager.Instance)?.PlayFoley(AudioManager.FoleyEventType.WeaponInspect, worldPos, 0.70f);
                ShowMsg("As runas celtas restauraram sua mente (+8 Sanidade).");
            }
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
