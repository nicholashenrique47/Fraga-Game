using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class HeartbeatSynth : MonoBehaviour
{
    public SanitySystem sanitySystem;
    
    private float phase;
    private float sampleRate;
    private AudioSource source;

    private void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        source = GetComponent<AudioSource>();
        source.playOnAwake = true;
        source.loop = true;
        source.clip = AudioClip.Create("HeartbeatDummy", 1, 1, (int)sampleRate, false);
        source.Play(); // Needs to be playing for OnAudioFilterRead to work
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (sanitySystem == null) return;
        
        float sanity = sanitySystem.sanity;
        
        // Only beat if sanity is below 30
        if (sanity >= 30f)
        {
            for (int i = 0; i < data.Length; i++) data[i] = 0f;
            return;
        }

        float panicT = 1f - (sanity / 30f); // 0 to 1
        float bpm = Mathf.Lerp(80f, 160f, panicT);
        float bps = bpm / 60f;
        
        float amplitude = Mathf.Lerp(0f, 0.8f, panicT);

        for (int i = 0; i < data.Length; i += channels)
        {
            phase += bps / sampleRate;
            if (phase > 1f) phase -= 1f;

            // Synthetic Heartbeat (double thump shape)
            // thump 1 at phase 0.0, thump 2 at phase 0.3
            float thump1 = Mathf.Exp(-50f * phase) * Mathf.Sin(2f * Mathf.PI * 45f * phase);
            
            float p2 = phase > 0.3f ? phase - 0.3f : 0f;
            float thump2 = Mathf.Exp(-60f * p2) * Mathf.Sin(2f * Mathf.PI * 55f * p2);

            float sample = (thump1 + thump2 * 0.7f) * amplitude;

            for (int c = 0; c < channels; c++)
            {
                data[i + c] = sample;
            }
        }
    }
}
