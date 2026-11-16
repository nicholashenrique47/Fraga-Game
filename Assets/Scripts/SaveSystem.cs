using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public int saveVersion = 3;

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
    public float bodyTemperature = 36.6f;
    public float stamina = 100f;
    public float hunger = 100f;
    public float thirst = 100f;
    public bool isSick;
    public float sicknessTimer;
    public bool isFractured;
    public bool isBleeding;

    // Equipamentos Ativos
    public int equippedMainHandId = -1;
    public int equippedOffHandId = -1;
    public int equippedArmorId = -1;

    // Inventario
    public List<int> inventoryItemIDs = new List<int>();
    public List<int> inventoryItemAmounts = new List<int>();
    public List<float> inventorySlotDurabilities = new List<float>();

    // Persistencia do Mundo (itens coletados, construcoes e grid nativo)
    public List<int> harvestedKeysFlat = new List<int>();
    public List<int> builtTilesFlat = new List<int>();
    public int tutorialStep = 0;
    public int[] savedMapGrid;
}

/// SaveSystem: Serializacao JSON para gravar o estado do mundo, inventario, durabilidades e jogador.
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
        if (dayNightCycle == null) dayNightCycle = DayNightCycle.Instance ?? FindAnyObjectByType<DayNightCycle>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
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
        data.bodyTemperature = playerStatus.bodyTemperature;
        data.stamina = playerStatus.stamina;
        data.hunger = playerStatus.hunger;
        data.thirst = playerStatus.thirst;
        data.isSick = playerStatus.isSick;
        data.sicknessTimer = playerStatus.sicknessTimer;
        data.isFractured = playerStatus.isFractured;
        data.isBleeding = playerStatus.isBleeding;

        // Equipamentos e Inventario (Slots + Durabilidade)
        data.equippedMainHandId = inventory.equippedMainHandId;
        data.equippedOffHandId = inventory.equippedOffHandId;
        data.equippedArmorId = inventory.equippedArmorId;

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

        TutorialManager tutorial = FindAnyObjectByType<TutorialManager>();
        if (tutorial != null)
        {
            data.tutorialStep = tutorial.CurrentStep;
        }

        string json = JsonUtility.ToJson(data);
        try
        {
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
            return;
        }

        if (data != null && playerTransform != null)
        {
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
            if (playerStatus != null)
            {
                playerStatus.health = Mathf.Clamp(data.health > 0f ? Mathf.Max(data.health, 35f) : 100f, 1f, 100f);
                playerStatus.bodyTemperature = Mathf.Clamp(data.bodyTemperature > 10f ? data.bodyTemperature : 36.6f, 28f, 38f);
                playerStatus.stamina = Mathf.Clamp(Mathf.Max(data.stamina, 50f), 0f, 100f);
                playerStatus.hunger = Mathf.Clamp(Mathf.Max(data.hunger, 35f), 0f, 100f);
                playerStatus.thirst = Mathf.Clamp(data.thirst > 0f ? Mathf.Max(data.thirst, 35f) : 100f, 0f, 100f);
                playerStatus.isSick = data.isSick;
                playerStatus.sicknessTimer = data.isSick ? Mathf.Max(0f, data.sicknessTimer) : 0f;
                playerStatus.isFractured = data.isFractured;
                playerStatus.isBleeding = data.isBleeding;
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

                inventory.equippedMainHandId = (data.equippedMainHandId > 0 && inventory.FindUsableEquipmentSlot(data.equippedMainHandId) >= 0) ? data.equippedMainHandId : -1;
                inventory.equippedOffHandId = (data.equippedOffHandId > 0 && inventory.FindUsableEquipmentSlot(data.equippedOffHandId) >= 0) ? data.equippedOffHandId : -1;
                inventory.equippedArmorId = (data.equippedArmorId > 0 && inventory.FindUsableEquipmentSlot(data.equippedArmorId) >= 0) ? data.equippedArmorId : -1;
                inventory.NotifyStateLoaded();
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

            TutorialManager tutorial = FindAnyObjectByType<TutorialManager>();
            if (tutorial != null)
            {
                tutorial.SetStep(data.tutorialStep);
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
