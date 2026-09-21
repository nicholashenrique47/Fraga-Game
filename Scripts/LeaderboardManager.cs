using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    
    private int daysSurvivedInCurrentRun = 0;
    private float lastCheckedTime = 0f;

    private void Update()
    {
        if (dayNightCycle == null) return;

        // Detecta a transição de um dia inteiro (passagem da meia-noite)
        if (lastCheckedTime > 23f && dayNightCycle.currentTime < 1f)
        {
            daysSurvivedInCurrentRun++;
            Debug.Log($"[Leaderboard] Sobreviveu mais um dia! Total: {daysSurvivedInCurrentRun}");
            SubmitScore(daysSurvivedInCurrentRun);
        }

        lastCheckedTime = dayNightCycle.currentTime;
    }

    public void SubmitScore(int score)
    {
        // Num SDK real de Google Play Games / Apple Game Center:
        // Social.ReportScore(score, "CgkI_leaderboard_id", (bool success) => { });

        Debug.Log($"[Leaderboard] Reportando Score {score} para a nuvem...");

        // Salvar no perfil local se for o recorde
        MetaData meta = MetaSaveSystem.LoadMeta();
        if (score > meta.highestSurvivedNights)
        {
            meta.highestSurvivedNights = score;
            MetaSaveSystem.SaveMeta(meta);
            Debug.Log("[Leaderboard] Novo Recorde Pessoal Guardado!");
        }
    }

    public void ResetRun()
    {
        daysSurvivedInCurrentRun = 0;
    }
}
