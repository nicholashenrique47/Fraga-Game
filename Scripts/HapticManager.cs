using UnityEngine;

public class HapticManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SanitySystem sanitySystem;

    private float nextHeartbeatTime = 0f;

    private void Update()
    {
        if (sanitySystem == null) return;

        // Se a sanidade estiver abaixo de 30%, ativa o batimento cardíaco tátil
        if (sanitySystem.sanity < 30f)
        {
            if (Time.time >= nextHeartbeatTime)
            {
                TriggerHeartbeatVibration();
                
                // Quanto menor a sanidade, mais rápidos são os batimentos
                float delay = Mathf.Lerp(0.3f, 1.2f, sanitySystem.sanity / 30f);
                nextHeartbeatTime = Time.time + delay;
            }
        }
    }

    public void TriggerLightVibration()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate(); // Forma básica. Idealmente usaríamos a biblioteca "Vibration" ou "HapticFeedback" do iOS/Android para controlar a força.
#endif
    }

    public void TriggerHeavyVibration()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate(); 
        // Num SDK avançado: HapticPatterns.PlayPreset(HapticPatterns.PresetType.HeavyImpact);
#endif
    }

    private void TriggerHeartbeatVibration()
    {
        // Duplo pulso típico de batimento cardíaco
        TriggerLightVibration();
        Invoke("TriggerLightVibration", 0.15f);
    }
}
