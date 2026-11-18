using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SavedChestStackData
{
    public int itemId;
    public int amount;
}

[System.Serializable]
public class SavedBlueprintStructureData
{
    public string structureName;
    public string structureId;
    public string ownerPlayerId;
    public string ownerPlayerName;
    public bool isSharedWithGroup = true;
    public float posX;
    public float posY;
    public float posZ;
    public float rotY;
    public int neededLogs;
    public int neededStones;
    public int currentLogs;
    public int currentStones;
    public bool isComplete;
    public float currentHealth;
    public bool doorIsOpen;
    public int storedLogs;
    public int waterCharges;
    public int[] dryingRawItemIds;
    public float[] dryingTimers;
    public bool[] dryingIsDried;
    public List<SavedChestStackData> chestStacks = new List<SavedChestStackData>();
}

[System.Serializable]
public class SavedBonfireStateData
{
    public float posX;
    public float posY;
    public float posZ;
    public float fuelLevel = 100f;
    public bool isLit = true;
    public int[] slotRawIds;
    public float[] slotTimers;
    public float[] slotOvercookTimers;
    public int[] slotStages;
}

[System.Serializable]
public class SaveData
{
    public int saveVersion = 5;

    // Mundo & Tempo
    public int mapSeed = 1913;
    public float currentTime;
    public int currentDay;

    // Player Status
    public float playerX;
    public float playerY;
    public float health = 100f;
    public float sanity = 100f;
    public float oilAmount = 100f;
    public bool isLighterOn;
    public float bodyTemperature = 36.6f;
    public float clothesWetness = 0f;
    public float stamina = 100f;
    public float hunger = 100f;
    public float thirst = 100f;
    public bool isSick;
    public float sicknessTimer;
    public bool isFractured;
    public bool isBleeding;
    public bool isPoisoned;
    public float poisonTimer;

    // Equipamentos Ativos
    public int equippedMainHandId = -1;
    public int equippedOffHandId = -1;
    public int equippedArmorId = -1;
    public int[] savedHotbarItemIds;
    public int savedActiveHotbarIndex;

    // Inventario
    public List<int> inventoryItemIDs = new List<int>();
    public List<int> inventoryItemAmounts = new List<int>();
    public List<float> inventorySlotDurabilities = new List<float>();

    // Persistencia do Mundo (itens coletados, construcoes, baus e grid nativo)
    public List<int> harvestedKeysFlat = new List<int>();
    public List<int> builtTilesFlat = new List<int>();
    public List<SavedBlueprintStructureData> blueprintStructures = new List<SavedBlueprintStructureData>();
    public List<SavedBonfireStateData> bonfireStates = new List<SavedBonfireStateData>();
    public int tutorialStep = 0;
    public int[] savedMapGrid;
}

/// SaveSystem: Serializacao JSON para gravar o estado do mundo, estruturas construidas, baus, inventario, durabilidades e jogador.
public class SaveSystem : MonoBehaviour
{
    public Transform playerTransform;
    public DayNightCycle dayNightCycle;
    public SanitySystem sanitySystem;
    public PlayerStatus playerStatus;
    public InventoryAdvanced inventory;

    [Header("Auto-Save")]
    public float autosaveInterval = 180f;
    private float nextAutosaveTime = 180f;

    private string savePath;
    private string backupPath;

    private void Awake()
    {
        EnsurePath();
        nextAutosaveTime = Time.time + autosaveInterval;
    }

    private void Update()
    {
        if (autosaveInterval <= 0f || Time.time < nextAutosaveTime) return;
        nextAutosaveTime = Time.time + autosaveInterval;
        if (playerStatus != null && playerStatus.health > 0f && !UIManager.IsEndgameModalOpen)
        {
            SaveGame();
        }
    }

    public void SetSavePathForTesting(string customFilePath)
    {
        savePath = customFilePath;
        backupPath = string.IsNullOrEmpty(customFilePath) ? string.Empty : (customFilePath + ".bak");
    }

    private void EnsurePath()
    {
        if (string.IsNullOrEmpty(savePath))
        {
            savePath = Application.persistentDataPath + "/fraga_save_v2.dat";
            backupPath = savePath + ".bak";
        }
    }

    private void ResolveReferences()
    {
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (dayNightCycle == null) dayNightCycle = DayNightCycle.Instance != null ? DayNightCycle.Instance : FindAnyObjectByType<DayNightCycle>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (playerStatus == null) playerStatus = PlayerStatus.Instance != null ? PlayerStatus.Instance : FindAnyObjectByType<PlayerStatus>();
        if (inventory == null) inventory = InventoryAdvanced.Instance != null ? InventoryAdvanced.Instance : FindAnyObjectByType<InventoryAdvanced>();
    }

    public bool HasSaveFile()
    {
        EnsurePath();
        return File.Exists(savePath) || File.Exists(backupPath);
    }

    public int PeekSavedSeed(int fallbackSeed)
    {
        EnsurePath();
        SaveData data = TryReadSaveData(savePath) ?? TryReadSaveData(backupPath);
        if (data != null && data.mapSeed > 0) return data.mapSeed;
        return fallbackSeed;
    }

    public bool TryPeekSaveMetadata(out int day, out int seed, out float timeOfDay)
    {
        EnsurePath();
        SaveData data = TryReadSaveData(savePath) ?? TryReadSaveData(backupPath);
        if (data != null)
        {
            day = Mathf.Max(1, data.currentDay);
            seed = data.mapSeed > 0 ? data.mapSeed : 1913;
            timeOfDay = data.currentTime;
            return true;
        }
        day = 1;
        seed = 1913;
        timeOfDay = 10f;
        return false;
    }

    public static bool TryPeekDefaultSaveMetadata(out int day, out int seed, out float timeOfDay)
    {
        string defaultPath = Application.persistentDataPath + "/fraga_save_v2.dat";
        string[] candidates = { defaultPath, defaultPath + ".bak" };
        for (int i = 0; i < candidates.Length; i++)
        {
            string path = candidates[i];
            if (!File.Exists(path)) continue;
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) continue;
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data != null)
                {
                    day = Mathf.Max(1, data.currentDay);
                    seed = data.mapSeed > 0 ? data.mapSeed : 1913;
                    timeOfDay = data.currentTime;
                    return true;
                }
            }
            catch { }
        }
        day = 1;
        seed = 1913;
        timeOfDay = 10f;
        return false;
    }

    private SaveData TryReadSaveData(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            return null;
        }
    }

    public void SaveGame()
    {
        EnsurePath();
        ResolveReferences();
        if (dayNightCycle == null || sanitySystem == null || playerStatus == null || inventory == null || playerTransform == null) return;

        SaveData data = new SaveData();

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            data.mapSeed = gameManager.mapSeed;
            data.savedMapGrid = gameManager.GetMapData();
        }

        // Posicao
        data.playerX = playerTransform.position.x;
        data.playerY = playerTransform.position.z;

        // Tempo
        data.currentTime = dayNightCycle.currentTime;
        data.currentDay = dayNightCycle.currentDay;

        // Status
        data.health = playerStatus.health;
        data.sanity = sanitySystem.sanity;
        data.oilAmount = sanitySystem.oil;
        PlayerController pc = playerTransform.GetComponent<PlayerController>();
        data.isLighterOn = pc != null ? pc.IsLighterOn : sanitySystem.isLampOn;
        BodyTemperatureSystem bts = BodyTemperatureSystem.Instance != null
            ? BodyTemperatureSystem.Instance
            : FindAnyObjectByType<BodyTemperatureSystem>();
        data.bodyTemperature = bts != null ? bts.currentTemperature : playerStatus.bodyTemperature;
        data.clothesWetness = bts != null ? Mathf.Clamp01(bts.clothesWetness) : 0f;
        data.stamina = playerStatus.stamina;
        data.hunger = playerStatus.hunger;
        data.thirst = playerStatus.thirst;
        data.isSick = playerStatus.isSick;
        data.sicknessTimer = playerStatus.sicknessTimer;
        data.isFractured = playerStatus.isFractured;
        data.isBleeding = playerStatus.isBleeding;
        data.isPoisoned = playerStatus.isPoisoned;
        data.poisonTimer = playerStatus.isPoisoned ? Mathf.Max(0f, playerStatus.poisonTimer) : 0f;

        // Equipamentos e Inventario (Slots + Durabilidade)
        data.equippedMainHandId = inventory.equippedMainHandId;
        data.equippedOffHandId = inventory.equippedOffHandId;
        data.equippedArmorId = inventory.equippedArmorId;
        if (inventory.hotbarItemIds != null)
        {
            data.savedHotbarItemIds = (int[])inventory.hotbarItemIds.Clone();
        }
        data.savedActiveHotbarIndex = inventory.activeHotbarIndex;

        for (int i = 0; i < inventory.slots.Length; i++)
        {
            data.inventoryItemIDs.Add(inventory.slots[i].itemId);
            data.inventoryItemAmounts.Add(inventory.slots[i].quantity);
            data.inventorySlotDurabilities.Add(inventory.slots[i].durability);
        }

        // Estado do Mundo Aberto (coletados e construcoes do jogador)
        MapRenderer mapRenderer = FindAnyObjectByType<MapRenderer>();
        if (mapRenderer != null)
        {
            mapRenderer.ExportWorldState(data.harvestedKeysFlat, data.builtTilesFlat);
        }

        // Estruturas livres e estacoes do Livro de Sobrevivencia (Cabana, Bau, Secador, Coletor, Cesto, Modulares, Obras em andamento)
        BlueprintPlacementManager blueprintMgr = BlueprintPlacementManager.Instance != null
            ? BlueprintPlacementManager.Instance
            : FindAnyObjectByType<BlueprintPlacementManager>(FindObjectsInactive.Include);
        if (blueprintMgr != null)
        {
            blueprintMgr.ExportStructures(data.blueprintStructures);
        }

        // Estado fisico das Fogueiras (lenha, acesa/apagada e 3 slots da grelha)
        BonfireEntity[] sceneBonfires = BonfireEntity.AllBonfires.Count > 0
            ? BonfireEntity.AllBonfires.ToArray()
            : FindObjectsByType<BonfireEntity>(FindObjectsInactive.Exclude);
        for (int i = 0; i < sceneBonfires.Length; i++)
        {
            BonfireEntity bf = sceneBonfires[i];
            if (bf == null) continue;
            bf.ExportState(out int[] rawIds, out float[] timers, out float[] overcookTimers, out int[] stages);
            data.bonfireStates.Add(new SavedBonfireStateData
            {
                posX = bf.transform.position.x,
                posY = bf.transform.position.y,
                posZ = bf.transform.position.z,
                fuelLevel = bf.fuelLevel,
                isLit = bf.isLit,
                slotRawIds = rawIds,
                slotTimers = timers,
                slotOvercookTimers = overcookTimers,
                slotStages = stages
            });
        }

        TutorialManager tutorial = FindAnyObjectByType<TutorialManager>();
        if (tutorial != null)
        {
            data.tutorialStep = tutorial.CurrentStep;
        }

        string json = JsonUtility.ToJson(data);
        try
        {
            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (File.Exists(savePath)) File.Copy(savePath, backupPath, true);
        }
        catch { }
        File.WriteAllText(savePath, json);
        nextAutosaveTime = Time.time + autosaveInterval;

        Debug.Log($"[SaveSystem] Jogo salvo com sucesso em: {savePath}");
    }

    public void LoadGame()
    {
        EnsurePath();
        ResolveReferences();
        SaveData data = TryReadSaveData(savePath);
        if (data == null && File.Exists(backupPath))
        {
            Debug.LogWarning("[SaveSystem] Save principal ausente ou corrompido. Restaurando backup (.bak).");
            data = TryReadSaveData(backupPath);
        }
        if (data == null)
        {
            Debug.Log("[SaveSystem] Nenhum ficheiro de save valido encontrado. Comecar de novo.");
            if (inventory != null) inventory.ResetToStartingKit();
            return;
        }

        if (data != null && playerTransform != null)
        {
            StorageChestInteractable.CloseModal();
            if (LogCarryingSystem.Instance != null)
            {
                LogCarryingSystem.Instance.ClearCarriedLogs();
            }

            GameManager gameManager = FindAnyObjectByType<GameManager>();
            int expectedLen = gameManager != null ? (gameManager.mapWidth * gameManager.mapHeight) : (FragaWorldBlueprint.DefaultWidth * FragaWorldBlueprint.DefaultHeight);
            bool sameWorldScale = (data.savedMapGrid != null && data.savedMapGrid.Length == expectedLen);
            float maxX = gameManager != null ? Mathf.Max(10f, gameManager.mapWidth - 5f) : (FragaWorldBlueprint.DefaultWidth - 5f);
            float maxZ = gameManager != null ? Mathf.Max(10f, gameManager.mapHeight - 5f) : (FragaWorldBlueprint.DefaultHeight - 5f);

            // Restaura Posicao acima do terreno e executa SnapToGround seguro
            float safeX = sameWorldScale ? Mathf.Clamp(data.playerX, 24f, maxX) : FragaWorldBlueprint.CrashCampPos.x;
            float safeZ = sameWorldScale ? Mathf.Clamp(data.playerY, 24f, maxZ) : FragaWorldBlueprint.CrashCampPos.y;
            if (!FragaWorldBlueprint.IsInsideIsland(safeX, safeZ) || FragaWorldBlueprint.GetWaterDepth(safeX, safeZ) > 0.35f)
            {
                safeX = FragaWorldBlueprint.CrashCampPos.x;
                safeZ = FragaWorldBlueprint.CrashCampPos.y;
            }
            float terrainY = TerrainMeshGenerator.GetTerrainHeight(safeX, safeZ);

            playerTransform.position = new Vector3(safeX, terrainY + 0.25f, safeZ);
            var pc = playerTransform.GetComponent<PlayerController>();
            if (pc != null) pc.SnapToGround();

            // Reposiciona a camera para evitar olhar para o nada
            var isoCam = FindAnyObjectByType<IsometricCamera>();
            if (isoCam != null) isoCam.SnapToTarget();

            // Restaura Tempo de forma continua e segura
            if (dayNightCycle != null)
            {
                dayNightCycle.currentTime = Mathf.Repeat(data.currentTime, 24f);
                dayNightCycle.currentDay = Mathf.Max(1, data.currentDay);
            }

            // Restaura Status com garantias minimas seguras (previne loop de morte)
            if (sanitySystem != null)
            {
                sanitySystem.sanity = Mathf.Clamp(Mathf.Max(data.sanity, 35f), 0f, 100f);
                sanitySystem.oil = Mathf.Clamp(Mathf.Max(data.oilAmount, 35f), 0f, 100f);
            }
            if (pc != null)
            {
                pc.SetLighterState(data.isLighterOn, showEmptyFeedback: false);
            }
            float restoredTemp = Mathf.Clamp(data.bodyTemperature > 10f ? data.bodyTemperature : 36.6f, 28f, 38f);
            BodyTemperatureSystem btsLoad = BodyTemperatureSystem.Instance != null
                ? BodyTemperatureSystem.Instance
                : FindAnyObjectByType<BodyTemperatureSystem>();
            if (btsLoad != null)
            {
                btsLoad.RestoreSavedThermalState(restoredTemp, data.clothesWetness);
            }
            if (playerStatus != null)
            {
                playerStatus.health = Mathf.Clamp(data.health > 0f ? Mathf.Max(data.health, 35f) : 100f, 1f, 100f);
                playerStatus.bodyTemperature = restoredTemp;
                playerStatus.isFreezing = restoredTemp <= BodyTemperatureSystem.TIER1_SHIVER_TEMP;
                playerStatus.stamina = Mathf.Clamp(Mathf.Max(data.stamina, 50f), 0f, 100f);
                playerStatus.hunger = Mathf.Clamp(Mathf.Max(data.hunger, 35f), 0f, 100f);
                playerStatus.thirst = Mathf.Clamp(data.thirst > 0f ? Mathf.Max(data.thirst, 35f) : 100f, 0f, 100f);
                playerStatus.isSick = data.isSick;
                playerStatus.sicknessTimer = data.isSick ? Mathf.Max(0f, data.sicknessTimer) : 0f;
                playerStatus.isFractured = data.isFractured;
                playerStatus.isBleeding = data.isBleeding;
                playerStatus.isPoisoned = data.isPoisoned;
                playerStatus.poisonTimer = data.isPoisoned ? Mathf.Max(0f, data.poisonTimer) : 0f;
            }

            // Restaura Inventario + Durabilidades + Equipamentos
            if (inventory != null && inventory.slots != null)
            {
                for (int i = 0; i < inventory.slots.Length; i++)
                {
                    inventory.slots[i].Clear();
                }

                bool hasSavedDurabilities = data.inventorySlotDurabilities != null && data.inventorySlotDurabilities.Count > 0;
                for (int i = 0; i < data.inventoryItemIDs.Count && i < inventory.slots.Length; i++)
                {
                    int id = data.inventoryItemIDs[i];
                    int qty = i < data.inventoryItemAmounts.Count ? data.inventoryItemAmounts[i] : 0;
                    if (id < 0 || qty <= 0) continue;

                    inventory.slots[i].itemId = id;
                    inventory.slots[i].quantity = qty;

                    ItemData itemData = ItemDatabase.GetItem(id);
                    if (hasSavedDurabilities && i < data.inventorySlotDurabilities.Count)
                    {
                        inventory.slots[i].durability = data.inventorySlotDurabilities[i];
                    }
                    else if (itemData != null)
                    {
                        inventory.slots[i].durability = itemData.maxDurability > 0f ? itemData.maxDurability : -1f;
                    }
                }

                // Se um save antigo estiver completamente vazio (0 slots ocupados), restaura o kit inicial de sobrevivencia
                if (inventory.GetOccupiedSlotCount() == 0)
                {
                    inventory.ResetToStartingKit();
                }
                else
                {
                    inventory.equippedMainHandId = (data.equippedMainHandId > 0 && inventory.FindUsableEquipmentSlot(data.equippedMainHandId) >= 0) ? data.equippedMainHandId : -1;
                    inventory.equippedOffHandId = (data.equippedOffHandId > 0 && inventory.FindUsableEquipmentSlot(data.equippedOffHandId) >= 0) ? data.equippedOffHandId : -1;
                    inventory.equippedArmorId = (data.equippedArmorId > 0 && inventory.FindUsableEquipmentSlot(data.equippedArmorId) >= 0) ? data.equippedArmorId : -1;
                    bool hasSavedHotbar = data.savedHotbarItemIds != null && data.savedHotbarItemIds.Length > 0 && inventory.hotbarItemIds != null;
                    if (hasSavedHotbar)
                    {
                        for (int h = 0; h < inventory.hotbarItemIds.Length; h++)
                        {
                            inventory.hotbarItemIds[h] = h < data.savedHotbarItemIds.Length ? data.savedHotbarItemIds[h] : -1;
                        }
                    }
                    int maxHotbarIdx = inventory.hotbarItemIds != null && inventory.hotbarItemIds.Length > 0 ? inventory.hotbarItemIds.Length - 1 : 5;
                    inventory.activeHotbarIndex = Mathf.Clamp(data.savedActiveHotbarIndex, 0, maxHotbarIdx);
                    inventory.NotifyStateLoaded(preserveSavedHotbar: hasSavedHotbar);
                }
            }

            if (gameManager != null && data.savedMapGrid != null && data.savedMapGrid.Length > 0)
            {
                gameManager.LoadMapData(data.savedMapGrid, false);
            }

            MapRenderer mapRenderer = FindAnyObjectByType<MapRenderer>();
            if (mapRenderer != null)
            {
                mapRenderer.ImportWorldState(data.harvestedKeysFlat, data.builtTilesFlat);
            }

            BlueprintPlacementManager blueprintMgr = BlueprintPlacementManager.Instance != null
                ? BlueprintPlacementManager.Instance
                : FindAnyObjectByType<BlueprintPlacementManager>(FindObjectsInactive.Include);
            if (blueprintMgr != null)
            {
                blueprintMgr.ImportStructures(data.blueprintStructures);
            }

            if (data.bonfireStates != null && data.bonfireStates.Count > 0)
            {
                BonfireEntity[] sceneBonfires = FindObjectsByType<BonfireEntity>(FindObjectsInactive.Exclude);
                for (int s = 0; s < data.bonfireStates.Count; s++)
                {
                    SavedBonfireStateData savedBf = data.bonfireStates[s];
                    if (savedBf == null) continue;
                    Vector3 savedPos = new Vector3(savedBf.posX, savedBf.posY, savedBf.posZ);
                    BonfireEntity bestBf = null;
                    float bestDistSq = 1.2f * 1.2f;
                    for (int i = 0; i < sceneBonfires.Length; i++)
                    {
                        BonfireEntity cand = sceneBonfires[i];
                        if (cand == null) continue;
                        Vector3 diff = cand.transform.position - savedPos;
                        diff.y = 0f;
                        float dSq = diff.sqrMagnitude;
                        if (dSq <= bestDistSq)
                        {
                            bestDistSq = dSq;
                            bestBf = cand;
                        }
                    }
                    if (bestBf != null)
                    {
                        bestBf.ImportState(savedBf.fuelLevel, savedBf.isLit,
                            savedBf.slotRawIds, savedBf.slotTimers, savedBf.slotOvercookTimers, savedBf.slotStages);
                    }
                }
            }

            TutorialManager tutorial = FindAnyObjectByType<TutorialManager>();
            if (tutorial != null)
            {
                tutorial.SetStep(data.tutorialStep);
            }

            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.ForceRefreshNow();
                HUDSurvivalOverlay.Instance.RefreshResourceCounterBar();
            }

            Debug.Log("[SaveSystem] Jogo carregado com sucesso!");
        }
    }

    // Auto-Save ao perder foco na app (muito comum no Mobile)
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveGame();
        }
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }
}
