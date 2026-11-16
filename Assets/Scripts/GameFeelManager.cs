using UnityEngine;
using System.Collections;

public class GameFeelManager : MonoBehaviour
{
    public static GameFeelManager Instance;

    private int activeHitstopDepth = 0;
    private float savedTimeScale = 1f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void TriggerHitstop(float duration = 0.05f, float slowScale = 0.08f)
    {
        StartCoroutine(HitstopRoutine(Mathf.Clamp(duration, 0.01f, 0.15f), Mathf.Clamp(slowScale, 0.02f, 0.5f)));
    }

    private IEnumerator HitstopRoutine(float duration, float slowScale)
    {
        if (Time.timeScale <= 0.001f) yield break; // Nunca interfere em menu pausado

        if (activeHitstopDepth == 0)
        {
            savedTimeScale = Time.timeScale;
        }
        activeHitstopDepth++;
        Time.timeScale = Mathf.Min(Time.timeScale, slowScale);

        yield return new WaitForSecondsRealtime(duration);

        activeHitstopDepth = Mathf.Max(0, activeHitstopDepth - 1);
        if (activeHitstopDepth == 0 && Time.timeScale > 0f)
        {
            Time.timeScale = savedTimeScale > 0.01f ? savedTimeScale : 1f;
        }
    }
}
