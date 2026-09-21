using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource ambientSource;
    public AudioSource sanityWhisperSource;
    public AudioSource vultoSwarmSource;
    public AudioSource srcEnvironment;

    [Header("Clips")]
    public AudioClip dayAmbient;
    public AudioClip nightAmbient;
    public AudioClip lowSanityWhispers;
    public AudioClip vultoSwarmScreech;
    public AudioClip horrorClip;

    [Header("Dependencies")]
    public DayNightCycle dayNightCycle;
    public SanitySystem sanitySystem;
    public VultoManager vultoManager;

    private AudioLowPassFilter ambientLPF;

    private void Start()
    {
        if (ambientSource != null)
        {
            ambientSource.loop = true;
            ambientSource.clip = dayAmbient;
            
            // Adiciona Low Pass Filter
            ambientLPF = ambientSource.gameObject.AddComponent<AudioLowPassFilter>();
            ambientLPF.cutoffFrequency = 22000f; // Aberto por padrao

            ambientSource.Play();
        }

        if (sanityWhisperSource != null)
        {
            sanityWhisperSource.loop = true;
            sanityWhisperSource.clip = lowSanityWhispers;
            sanityWhisperSource.volume = 0f;
            sanityWhisperSource.Play();
        }
    }

    private void Update()
    {
        UpdateAmbientSound();
        UpdateSanityWhispers();
    }

    private void UpdateAmbientSound()
    {
        if (dayNightCycle == null || ambientSource == null) return;

        bool isNight = dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f;
        AudioClip targetClip = isNight ? nightAmbient : dayAmbient;

        if (ambientSource.clip != targetClip)
        {
            ambientSource.clip = targetClip;
            ambientSource.Play();
        }

        // Modulacao de Pitch e Low Pass Filter por Sanidade
        if (sanitySystem != null)
        {
            float sanity = sanitySystem.sanity;
            float t = 1f - Mathf.Clamp01(sanity / 100f);

            // Se a sanidade baixar, o som fica lento e abafado
            ambientSource.pitch = Mathf.Lerp(1.0f, 0.75f, t);
            
            if (ambientLPF != null)
            {
                // De 22000Hz (normal) para 1500Hz (muito abafado)
                ambientLPF.cutoffFrequency = Mathf.Lerp(22000f, 1500f, t * t);
            }
        }
    }

    private void UpdateSanityWhispers()
    {
        if (sanitySystem == null || sanityWhisperSource == null) return;

        // Os sussurros aumentam de volume Ã  medida que a sanidade desce abaixo dos 40%
        float sanity = sanitySystem.sanity;
        if (sanity < 40f)
        {
            float targetVolume = 1f - (sanity / 40f);
            sanityWhisperSource.volume = Mathf.Lerp(sanityWhisperSource.volume, targetVolume, Time.deltaTime);
        }
        else
        {
            sanityWhisperSource.volume = Mathf.Lerp(sanityWhisperSource.volume, 0f, Time.deltaTime);
        }
    }

    public void PlayVultoAttackSound()
    {
        if (vultoSwarmSource != null && vultoSwarmScreech != null && !vultoSwarmSource.isPlaying)
        {
            vultoSwarmSource.PlayOneShot(vultoSwarmScreech);
        }
    }

    public void PlayNightHorror()
    {
        if (horrorClip != null && srcEnvironment != null) srcEnvironment.PlayOneShot(horrorClip, 0.5f);
    }

    public void PlayPickupSound()
    {
        if (srcEnvironment != null && horrorClip != null) 
            srcEnvironment.PlayOneShot(horrorClip, 0.2f); // Placeholder para som de pickup
    }

    public void PlayHorrorSting()
    {
        // Placeholder for future horror sting
        if (ambientSource != null)
        {
            ambientSource.pitch = 0.5f;
            Invoke("ResetPitch", 2f);
        }
    }

    private void ResetPitch()
    {
        if (ambientSource != null) ambientSource.pitch = 1f;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && srcEnvironment != null)
        {
            srcEnvironment.PlayOneShot(clip);
        }
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float volume, float maxDistance)
    {
        if (clip == null) return;
        GameObject go = new GameObject("TempAudio");
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.volume = volume;
        source.maxDistance = maxDistance;
        source.Play();
        Destroy(go, clip.length);
    }

    public void PlayMusic(AudioClip music)
    {
        if (ambientSource != null && music != null)
        {
            ambientSource.clip = music;
            if (!ambientSource.isPlaying) ambientSource.Play();
        }
    }

    public void StopMusic()
    {
        if (ambientSource != null)
        {
            ambientSource.Stop();
        }
    }
}
