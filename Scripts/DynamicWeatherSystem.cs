using UnityEngine;

public class DynamicWeatherSystem : MonoBehaviour
{
    public DayNightCycle dayNightCycle;
    public SanitySystem sanitySystem;
    
    [Header("Weather Effects")]
    public ParticleSystem rainSystem;
    public Light lightningLight;
    public AudioSource rainAudio;
    public AudioSource thunderAudio;
    
    [Header("Hallucinations")]
    public GameObject hallucinationPrefab;
    private GameObject currentHallucination;
    private float hallucinationTimer;

    private void Update()
    {
        UpdateWeather();
        UpdateHallucinations();
    }

    private void UpdateWeather()
    {
        if (sanitySystem == null || dayNightCycle == null) return;

        bool isNight = dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f;
        float sanity = sanitySystem.sanity;
        
        bool isRaining = isNight && sanity < 40f;
        
        if (isRaining)
        {
            if (rainSystem != null && !rainSystem.isPlaying) rainSystem.Play();
            if (rainAudio != null && !rainAudio.isPlaying) rainAudio.Play();
            
            // Trovoadas repentinas quando a sanidade despenca
            if (sanity < 20f && Random.value < 0.005f)
            {
                TriggerLightning();
            }
        }
        else
        {
            if (rainSystem != null && rainSystem.isPlaying) rainSystem.Stop();
            if (rainAudio != null && rainAudio.isPlaying) rainAudio.Stop();
        }
    }

    private void TriggerLightning()
    {
        if (lightningLight != null)
        {
            lightningLight.intensity = 5f;
            lightningLight.color = new Color(0.8f, 0.9f, 1f); // Azul frio
        }
        if (thunderAudio != null)
        {
            thunderAudio.PlayOneShot(thunderAudio.clip);
        }
        
        // Assustar com Haptics
        HapticManager haptic = FindAnyObjectByType<HapticManager>();
        if (haptic != null) haptic.TriggerHeavyVibration();
    }

    private void UpdateHallucinations()
    {
        if (sanitySystem == null || hallucinationPrefab == null) return;
        
        if (sanitySystem.sanity < 20f)
        {
            hallucinationTimer -= Time.deltaTime;
            if (hallucinationTimer <= 0f)
            {
                SpawnHallucination();
                hallucinationTimer = Random.Range(3f, 8f);
            }
        }
        else
        {
            if (currentHallucination != null)
            {
                Destroy(currentHallucination);
            }
        }
        
        // Decaimento suave do relâmpago
        if (lightningLight != null && lightningLight.intensity > 0)
        {
            lightningLight.intensity = Mathf.Lerp(lightningLight.intensity, 0f, Time.deltaTime * 5f);
        }
    }

    private void SpawnHallucination()
    {
        if (currentHallucination != null) Destroy(currentHallucination);
        
        Vector2 randomCircle = Random.insideUnitCircle.normalized * 6f;
        Vector3 spawnPos = sanitySystem.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        currentHallucination = Instantiate(hallucinationPrefab, spawnPos, Quaternion.LookRotation(sanitySystem.transform.position - spawnPos));
        
        // Desaparece misteriosamente
        Destroy(currentHallucination, 2f);
        
        AudioManager am = FindAnyObjectByType<AudioManager>();
        if (am != null) am.PlayHorrorSting();
    }
}
