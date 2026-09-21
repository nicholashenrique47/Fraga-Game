using UnityEngine;
using System.Collections;

public class GameFeelManager : MonoBehaviour
{
    public static GameFeelManager Instance;
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }
    
    public void TriggerHitstop(float duration = 0.05f)
    {
        StartCoroutine(HitstopRoutine(duration));
    }
    
    private IEnumerator HitstopRoutine(float duration)
    {
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}
