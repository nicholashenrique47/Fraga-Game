using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CinematicHorrorSystem : MonoBehaviour
{
    private DayNightCycle dayNightCycle;
    private bool midnightTriggered = false;
    
    [Header("Cinematic Effects")]
    public Light sunMoonLight;
    public Volume postProcessingVolume;
    private ColorAdjustments colorAdjustments;
    
    private void Start()
    {
        dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        if (postProcessingVolume != null && postProcessingVolume.profile != null)
        {
            postProcessingVolume.profile.TryGet(out colorAdjustments);
        }
    }
    
    private void Update()
    {
        if (dayNightCycle == null) return;
        
        // Se bateu meia noite (0h) apos o jogo estar rodando
        if (Time.timeSinceLevelLoad > 5.0f && dayNightCycle.currentTime >= 0f && dayNightCycle.currentTime < 0.1f && !midnightTriggered)
        {
            StartCoroutine(MidnightEventRoutine());
        }
        
        // Reset the trigger at a later time (e.g. 1am)
        if (dayNightCycle.currentTime >= 1f && midnightTriggered)
        {
            midnightTriggered = false;
        }
    }
    
    private IEnumerator MidnightEventRoutine()
    {
        midnightTriggered = true;
        Debug.LogWarning("[Cinematic] O TRASNO DESPERTOU A MEIA-NOITE!");
        
        // 1. Camera Shake (Tremor)
        HapticManager haptic = FindAnyObjectByType<HapticManager>();
        if (haptic != null) haptic.TriggerHeavyVibration();
        
        AudioManager am = FindAnyObjectByType<AudioManager>();
        if (am != null) am.PlayHorrorSting(); // Rugido global
        
        // 2. Apagar luzes bruscamente (Pitch Black)
        float originalIntensity = sunMoonLight != null ? sunMoonLight.intensity : 0f;
        Color originalColor = sunMoonLight != null ? sunMoonLight.color : Color.white;
        
        if (sunMoonLight != null)
        {
            sunMoonLight.intensity = 0f;
        }
        
        // 3. Dessaturacao Total (Tirar a cor do jogo)
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = -100f;
        }
        
        yield return new WaitForSeconds(2.5f);
        
        // 4. Lua de Sangue (Retorna a luz porem avermelhada intensa)
        if (sunMoonLight != null)
        {
            sunMoonLight.color = new Color(0.8f, 0f, 0f); // Vermelho puro
            sunMoonLight.intensity = Mathf.Max(originalIntensity * 1.5f, 1.2f);
        }
        
        // Retornar a cor gradualmente
        float elapsedTime = 0f;
        while(elapsedTime < 5f)
        {
            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.value = Mathf.Lerp(-100f, 0f, elapsedTime / 5f);
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
}
