using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// DailyQuestManager: Sistema de missoes diarias para retencao freemium.
/// Persiste estado em disco binario. Usa UTC para impedir manipulacao de relogio.
/// Integra com MetaSaveSystem para recompensas em Almas.
[System.Serializable]
public class DailyQuest
{
    public string id;           // Identificador unico da quest
    public string description;  // Texto para UI
    public int targetValue;     // Quantidade necessaria para completar
    public int currentValue;    // Progresso atual
    public int rewardSouls;     // Recompensa em Almas
    public bool completed;      // Ja foi completada neste ciclo
    public bool claimed;        // Recompensa ja foi resgatada
}

[System.Serializable]
public class DailyQuestSaveData
{
    public long lastResetTimestamp;  // Ultimo reset em Unix UTC ticks
    public int loginStreak;         // Dias consecutivos de login
    public List<DailyQuest> activeQuests = new List<DailyQuest>();
}

public class DailyQuestManager : MonoBehaviour
{
    [Header("Dependencies")]
    public HubManager hubManager;

    [Header("Quest Config")]
    public int questsPerDay = 3;
    public int streakBonusPerDay = 25; // Almas bonus por dia de streak

    // Estado em runtime
    private DailyQuestSaveData saveData;
    private static string savePath;

    // Eventos observaveis para a UI
    public event Action<DailyQuest> OnQuestCompleted;
    public event Action<int> OnStreakUpdated;

    // Templates de quest para rotacao (pool de variedade)
    private static readonly (string id, string desc, int target, int reward)[] questTemplates = new[]
    {
        ("survive_nights_2",    "Sobreviver 2 noites",                2,   100),
        ("survive_nights_5",    "Sobreviver 5 noites",                5,   300),
        ("build_barricades_3",  "Construir 3 barricadas",             3,    50),
        ("build_bonfires_2",    "Acender 2 fogueiras",                2,    75),
        ("collect_wood_20",     "Coletar 20 madeiras",               20,    40),
        ("collect_stone_15",    "Coletar 15 pedras",                 15,    40),
        ("kill_vultos_10",      "Eliminar 10 vultos com luz",        10,   120),
        ("build_celtic_1",      "Erigir um Farol Celta",              1,   200),
        ("use_salt_2",          "Usar 2 circulos de Sal",             2,    80),
        ("survive_low_sanity",  "Sobreviver 60s abaixo de 30% san.", 60,   150),
        ("oil_trap_3",          "Colocar 3 armadilhas de oleo",       3,    90),
        ("explore_ruins_5",     "Descobrir 5 ruinas",                 5,   110),
    };

    private void Awake()
    {
        savePath = Application.persistentDataPath + "/fraga_daily.dat";
        LoadState();
        CheckAndResetDaily();
    }

    /// Verifica se passou um dia desde o ultimo reset.
    /// Usa DateTime.UtcNow para ser imune a manipulacao de fuso/relogio.
    private void CheckAndResetDaily()
    {
        DateTime now = DateTime.UtcNow;
        DateTime lastReset = new DateTime(saveData.lastResetTimestamp, DateTimeKind.Utc);

        // Se o dia UTC mudou, resetar quests
        if (now.Date > lastReset.Date)
        {
            // Calcular streak
            TimeSpan gap = now.Date - lastReset.Date;
            if (gap.TotalDays <= 1.5) // Tolerancia de 36h para fusos
            {
                saveData.loginStreak++;
            }
            else
            {
                saveData.loginStreak = 1; // Streak quebrada
            }

            GenerateNewQuests();
            saveData.lastResetTimestamp = now.Ticks;
            SaveState();

            if (OnStreakUpdated != null) OnStreakUpdated.Invoke(saveData.loginStreak);
            Debug.Log($"[DailyQuest] Novo dia detectado. Streak: {saveData.loginStreak}. Quests geradas.");
        }
        else
        {
            Debug.Log($"[DailyQuest] Mesmo dia. Quests existentes carregadas. Streak: {saveData.loginStreak}.");
        }
    }

    /// Gera quests aleatorias a partir do pool de templates sem repeticao.
    private void GenerateNewQuests()
    {
        saveData.activeQuests.Clear();

        // Fisher-Yates parcial para selecionar N quests sem repeticao
        int[] indices = new int[questTemplates.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;

        // Seed baseada no dia para ser deterministica (mesmas quests se reabrir a app no mesmo dia)
        System.Random rng = new System.Random(DateTime.UtcNow.DayOfYear * 1000 + DateTime.UtcNow.Year);

        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int temp = indices[i];
            indices[i] = indices[j];
            indices[j] = temp;
        }

        int count = Mathf.Min(questsPerDay, questTemplates.Length);
        for (int i = 0; i < count; i++)
        {
            var template = questTemplates[indices[i]];
            saveData.activeQuests.Add(new DailyQuest
            {
                id = template.id,
                description = template.desc,
                targetValue = template.target,
                currentValue = 0,
                rewardSouls = template.reward,
                completed = false,
                claimed = false
            });
        }
    }

    /// Reporta progresso de um tipo de quest. Chamado pelos sistemas do jogo.
    /// Exemplo: DailyQuestManager.Instance.ReportProgress("survive_nights_2", 1);
    public void ReportProgress(string questId, int amount)
    {
        for (int i = 0; i < saveData.activeQuests.Count; i++)
        {
            DailyQuest quest = saveData.activeQuests[i];
            if (quest.id == questId && !quest.completed)
            {
                quest.currentValue += amount;
                if (quest.currentValue >= quest.targetValue)
                {
                    quest.currentValue = quest.targetValue;
                    quest.completed = true;
                    if (OnQuestCompleted != null) OnQuestCompleted.Invoke(quest);
                    Debug.Log($"[DailyQuest] Quest completada: {quest.description}");
                }
                SaveState();
                return;
            }
        }
    }

    /// Resgata a recompensa de uma quest completada.
    public bool ClaimReward(int questIndex)
    {
        if (questIndex < 0 || questIndex >= saveData.activeQuests.Count) return false;

        DailyQuest quest = saveData.activeQuests[questIndex];
        if (!quest.completed || quest.claimed) return false;

        quest.claimed = true;
        int totalReward = quest.rewardSouls;

        // Bonus de streak
        int streakBonus = saveData.loginStreak * streakBonusPerDay;
        totalReward += streakBonus;

        if (hubManager != null)
        {
            hubManager.AddSouls(totalReward);
        }

        SaveState();
        Debug.Log($"[DailyQuest] Recompensa resgatada: {totalReward} Almas (base {quest.rewardSouls} + streak {streakBonus}).");
        return true;
    }

    /// Acesso publico ao estado para a UI.
    public List<DailyQuest> GetActiveQuests() { return saveData.activeQuests; }
    public int GetLoginStreak() { return saveData.loginStreak; }

    // Persistencia binaria otimizada
    private void SaveState()
    {
        try
        {
            using (FileStream stream = new FileStream(savePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(2); // Versao do formato
                writer.Write(saveData.lastResetTimestamp);
                writer.Write(saveData.loginStreak);
                writer.Write(saveData.activeQuests.Count);

                for (int i = 0; i < saveData.activeQuests.Count; i++)
                {
                    DailyQuest q = saveData.activeQuests[i];
                    writer.Write(q.id);
                    writer.Write(q.description);
                    writer.Write(q.targetValue);
                    writer.Write(q.currentValue);
                    writer.Write(q.rewardSouls);
                    writer.Write(q.completed);
                    writer.Write(q.claimed);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DailyQuest] Falha ao gravar: {e.Message}");
        }
    }

    private void LoadState()
    {
        saveData = new DailyQuestSaveData();
        saveData.lastResetTimestamp = DateTime.UtcNow.AddDays(-1).Ticks; // Forca reset na primeira execucao

        if (!File.Exists(savePath)) return;

        try
        {
            using (FileStream stream = new FileStream(savePath, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                saveData.lastResetTimestamp = reader.ReadInt64();
                saveData.loginStreak = reader.ReadInt32();
                int questCount = reader.ReadInt32();

                saveData.activeQuests = new List<DailyQuest>(questCount);
                for (int i = 0; i < questCount; i++)
                {
                    saveData.activeQuests.Add(new DailyQuest
                    {
                        id = reader.ReadString(),
                        description = reader.ReadString(),
                        targetValue = reader.ReadInt32(),
                        currentValue = reader.ReadInt32(),
                        rewardSouls = reader.ReadInt32(),
                        completed = reader.ReadBoolean(),
                        claimed = reader.ReadBoolean()
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DailyQuest] Falha ao ler save (corrupto?): {e.Message}. Resetando.");
            saveData = new DailyQuestSaveData();
            saveData.lastResetTimestamp = DateTime.UtcNow.AddDays(-1).Ticks;
        }
    }
}
