using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    // Tempo
    public float currentTime;
    public int currentDay;

    // Player Status
    public float playerX;
    public float playerY;
    public float sanity;
    public float oilAmount;
    public float bodyTemperature;
    public float stamina;
    public float hunger;

    // Inventario
    public List<int> inventoryItemIDs = new List<int>();
    public List<int> inventoryItemAmounts = new List<int>();

    // Opcional: Grelha do mapa serializada por run-length encoding ou array simples, 
    // mas por agora vamos focar no progresso do jogador e meta-data.
    public int[] savedMapGrid;
}

/// SaveSystem: Serializacao Binaria para gravar o estado do mundo e do jogador
/// Utilizado para manter o jogo persistente no mobile.
public class SaveSystem : MonoBehaviour
{
    public Transform playerTransform;
    public DayNightCycle dayNightCycle;
    public SanitySystem sanitySystem;
    public PlayerStatus playerStatus;
    public InventoryAdvanced inventory;

    private string savePath;

    private void Awake()
    {
        savePath = Application.persistentDataPath + "/fraga_save_v2.dat";
    }

    public void SaveGame()
    {
        if (dayNightCycle == null || sanitySystem == null || playerStatus == null || inventory == null) return;

        SaveData data = new SaveData();
        
        // Posicao
        data.playerX = playerTransform.position.x;
        data.playerY = playerTransform.position.z;

        // Tempo
        data.currentTime = dayNightCycle.currentTime;
        data.currentDay = dayNightCycle.currentDay;

        // Status
        data.sanity = sanitySystem.sanity;
        data.oilAmount = sanitySystem.oil;
        data.bodyTemperature = playerStatus.bodyTemperature;
        data.stamina = playerStatus.stamina;
        data.hunger = playerStatus.hunger;

        // Inventario (Slots limitados pelo maxSlots = 20)
        for (int i = 0; i < inventory.slots.Length; i++)
        {
            data.inventoryItemIDs.Add(inventory.slots[i].itemId);
            data.inventoryItemAmounts.Add(inventory.slots[i].quantity);
        }

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null) data.savedMapGrid = gameManager.GetMapData();

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(savePath, json);

        Debug.Log($"[SaveSystem] Jogo salvo com sucesso em: {savePath}");
    }

    public void LoadGame()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("[SaveSystem] Nenhum ficheiro de save encontrado. Comecar de novo.");
            return;
        }

        string json = File.ReadAllText(savePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        if (data != null)
        {
            // Restaura Posicao
            Vector3 pos = playerTransform.position;
            pos.x = data.playerX;
            pos.z = data.playerY;
            playerTransform.position = pos;

            // Restaura Tempo
            dayNightCycle.currentTime = data.currentTime;
            dayNightCycle.currentDay = data.currentDay;

            // Restaura Status
            sanitySystem.sanity = data.sanity;
            sanitySystem.oil = data.oilAmount;
            playerStatus.bodyTemperature = data.bodyTemperature;
            playerStatus.stamina = data.stamina;
            playerStatus.hunger = data.hunger;

            // Restaura Inventario
            for (int i = 0; i < inventory.slots.Length; i++)
            {
                inventory.slots[i].Clear();
            }
            
            for (int i = 0; i < data.inventoryItemIDs.Count && i < inventory.slots.Length; i++)
            {
                inventory.slots[i].itemId = data.inventoryItemIDs[i];
                inventory.slots[i].quantity = data.inventoryItemAmounts[i];
                
                ItemData itemData = ItemDatabase.GetItem(data.inventoryItemIDs[i]);
                if (itemData != null)
                {
                    inventory.slots[i].durability = itemData.isPerishable ? itemData.maxDurability : -1f;
                }
            }

            GameManager gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager != null && data.savedMapGrid != null && data.savedMapGrid.Length > 0)
            {
                gameManager.LoadMapData(data.savedMapGrid);
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
