using UnityEngine;

/// <summary>
/// HeartbeatSynth: Sintetizador procedural de batimento cardiaco para panico extremo (Sanidade < 30).
/// Isolado automaticamente em um GameObject filho com AudioSource unico caso este GO possua outros AudioSources,
/// evitando erros de OnAudioFilterRead multi-AudioSource na Unity.
/// </summary>
public class HeartbeatSynth : MonoBehaviour
{
    public SanitySystem sanitySystem;

    private float sampleRate = 48000f;
    private float currentSanity = 100f;
    private AudioSource dedicatedSource;
    private HeartbeatFilterNode filterNode;

    private void Start()
    {
        sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000f;
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();

        AudioSource[] existing = GetComponents<AudioSource>();
        if (existing.Length > 0)
        {
            // Ja existem outros AudioSources neste GameObject -> cria filho dedicado com exatamente 1 AudioSource
            GameObject child = new GameObject("HeartbeatSynth_Audio");
            child.transform.SetParent(transform, false);
            dedicatedSource = child.AddComponent<AudioSource>();
            filterNode = child.AddComponent<HeartbeatFilterNode>();
        }
        else
        {
            dedicatedSource = gameObject.AddComponent<AudioSource>();
            filterNode = gameObject.AddComponent<HeartbeatFilterNode>();
        }

        dedicatedSource.spatialBlend = 0f;
        dedicatedSource.playOnAwake = true;
        dedicatedSource.loop = true;
        dedicatedSource.volume = 0.85f;
        dedicatedSource.clip = AudioClip.Create("HeartbeatDummy", 1, 1, (int)sampleRate, false);
        dedicatedSource.Play();

        filterNode.Configure(this, sampleRate);
    }

    private void Update()
    {
        if (sanitySystem != null)
        {
            currentSanity = sanitySystem.sanity;
        }
    }

    internal float CurrentSanity => currentSanity;

    private sealed class HeartbeatFilterNode : MonoBehaviour
    {
        private HeartbeatSynth owner;
        private float phase;
        private float sampleRate = 48000f;

        public void Configure(HeartbeatSynth ownerRef, float rate)
        {
            owner = ownerRef;
            sampleRate = rate > 0f ? rate : 48000f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (owner == null)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            float sanity = owner.CurrentSanity;
            if (sanity >= 30f)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            float panicT = 1f - (sanity / 30f);
            float bpm = Mathf.Lerp(80f, 160f, panicT);
            float bps = bpm / 60f;
            float amplitude = Mathf.Lerp(0f, 0.8f, panicT);

            for (int i = 0; i < data.Length; i += channels)
            {
                phase += bps / sampleRate;
                if (phase > 1f) phase -= 1f;

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
}
