using UnityEngine;
using System.IO;

[System.Serializable]
public class MetaData
{
    public int totalSouls = 0; // Moeda de Metaprocessão
    public int upgradeLampLevel = 0; // Nível do lampião
    public int upgradeSpeedLevel = 0; // Nível de velocidade
    public int upgradeSanityLevel = 0; // Resistência à escuridão
    public int highestSurvivedNights = 0; // Leaderboard local
}

public class MetaSaveSystem
{
    private static string path = Application.persistentDataPath + "/fraga_meta.dat";

    public static void SaveMeta(MetaData data)
    {
        using (FileStream stream = new FileStream(path, FileMode.Create))
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(1); // Versão
                writer.Write(data.totalSouls);
                writer.Write(data.upgradeLampLevel);
                writer.Write(data.upgradeSpeedLevel);
                writer.Write(data.upgradeSanityLevel);
                writer.Write(data.highestSurvivedNights);
            }
        }
    }

    public static MetaData LoadMeta()
    {
        MetaData data = new MetaData();
        if (!File.Exists(path)) return data;

        using (FileStream stream = new FileStream(path, FileMode.Open))
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                data.totalSouls = reader.ReadInt32();
                data.upgradeLampLevel = reader.ReadInt32();
                data.upgradeSpeedLevel = reader.ReadInt32();
                data.upgradeSanityLevel = reader.ReadInt32();
                data.highestSurvivedNights = reader.ReadInt32();
            }
        }
        return data;
    }
}
