using UnityEngine;

/// PositionalAudioSystem: Gere sons ambientes posicionais dinamicos.
/// Cria fontes de audio 3D que acompanham o jogador em todo o mapa.
public class PositionalAudioSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerTransform;
    public DayNightCycle dayNightCycle;
    public AudioManager audioManager;

    [Header("Audio Clips")]
    public AudioClip windClip;
    public AudioClip waterDripClip;
    public AudioClip crowCawClip;
    public AudioClip owlHootClip;
    public AudioClip cricketClip;
    public AudioClip wolfHowlClip;

    [Header("Config")]
    public float maxAudioDistance = 20f;
    public int maxAudioSources = 12;

    private AudioSource[] ambientSources;
    private Vector3[] sourcePositions;
    private float[] sourceTimers;
    private int mapWidth = FragaWorldBlueprint.DefaultWidth;
    private int mapHeight = FragaWorldBlueprint.DefaultHeight;

    private void Awake()
    {
        ambientSources = new AudioSource[maxAudioSources];
        sourcePositions = new Vector3[maxAudioSources];
        sourceTimers = new float[maxAudioSources];

        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            mapWidth = Mathf.Max(32, gm.mapWidth);
            mapHeight = Mathf.Max(32, gm.mapHeight);
        }

        if (windClip == null) windClip = GenerateCritterClip("wind");
        if (crowCawClip == null) crowCawClip = GenerateCritterClip("crow");
        if (owlHootClip == null) owlHootClip = GenerateCritterClip("owl");
        if (cricketClip == null) cricketClip = GenerateCritterClip("cricket");
        if (wolfHowlClip == null) wolfHowlClip = GenerateCritterClip("wolf");

        CreateAmbientSources();
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) playerTransform = pc.transform;
        }
    }

    private AudioClip GenerateCritterClip(string type)
    {
        int sampleRate = 44100;
        float duration = type == "wolf" ? 3f : type == "crow" ? 0.8f : type == "cricket" ? 0.2f : 1f;
        float[] samples = new float[(int)(sampleRate * duration)];

        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / samples.Length;
            float timeSec = (float)i / sampleRate;

            float envelope = Mathf.Sin(t * Mathf.PI);
            float sample = 0f;

            if (type == "wolf")
            {
                float freq = 300f + Mathf.Sin(t * Mathf.PI) * 150f;
                sample = Mathf.Sin(2f * Mathf.PI * freq * timeSec) * envelope;
            }
            else if (type == "crow")
            {
                float freq = 800f - t * 300f;
                sample = (Mathf.Sin(2f * Mathf.PI * freq * timeSec) + Random.Range(-0.5f, 0.5f)) * envelope * 0.5f;
            }
            else if (type == "owl")
            {
                float freq = 200f;
                float tremolo = Mathf.Sin(2f * Mathf.PI * 10f * timeSec) * 0.5f + 0.5f;
                sample = Mathf.Sin(2f * Mathf.PI * freq * timeSec) * tremolo * envelope;
            }
            else if (type == "cricket")
            {
                float freq = 4500f;
                sample = Mathf.Sin(2f * Mathf.PI * freq * timeSec) * envelope * (Random.value > 0.5f ? 1f : 0f);
            }
            else
            {
                sample = Random.Range(-1f, 1f) * envelope * 0.2f;
            }

            samples[i] = sample * 0.5f;
        }

        AudioClip clip = AudioClip.Create($"Synth_{type}", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private Vector3 PickRingPosition(float centerX, float centerZ)
    {
        float ang = Random.Range(0f, Mathf.PI * 2f);
        float rad = Random.Range(4f, Mathf.Max(6f, maxAudioDistance * 0.95f));
        float x = Mathf.Clamp(centerX + Mathf.Cos(ang) * rad, 4f, Mathf.Max(8f, mapWidth - 4f));
        float z = Mathf.Clamp(centerZ + Mathf.Sin(ang) * rad, 4f, Mathf.Max(8f, mapHeight - 4f));
        float y = TerrainMeshGenerator.GetTerrainHeight(x, z) + 0.5f;
        return new Vector3(x, y, z);
    }

    private void CreateAmbientSources()
    {
        float cx = mapWidth * 0.5f;
        float cz = mapHeight * 0.5f;

        for (int i = 0; i < maxAudioSources; i++)
        {
            GameObject obj = new GameObject($"AmbientAudio_{i}");
            obj.transform.SetParent(transform);

            AudioSource src = obj.AddComponent<AudioSource>();
            src.spatialBlend = 1f; // Full 3D
            src.minDistance = 3f;
            src.maxDistance = maxAudioDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.playOnAwake = false;
            src.loop = false;
            src.volume = 0.3f;

            ambientSources[i] = src;
            sourcePositions[i] = PickRingPosition(cx, cz);
            obj.transform.position = sourcePositions[i];
            sourceTimers[i] = Random.Range(5f, 20f);
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        float time = dayNightCycle != null ? dayNightCycle.currentTime : 12f;
        bool isNight = time >= 19f || time < 6f;

        for (int i = 0; i < maxAudioSources; i++)
        {
            if (ambientSources[i] == null) continue;

            // Recicla fontes distantes ao redor do jogador no mapa amplo
            float dist = Vector3.Distance(playerTransform.position, sourcePositions[i]);
            if (dist > maxAudioDistance * 1.4f && !ambientSources[i].isPlaying)
            {
                sourcePositions[i] = PickRingPosition(playerTransform.position.x, playerTransform.position.z);
                ambientSources[i].transform.position = sourcePositions[i];
                dist = Vector3.Distance(playerTransform.position, sourcePositions[i]);
            }

            sourceTimers[i] -= Time.deltaTime;

            if (sourceTimers[i] <= 0 && !ambientSources[i].isPlaying)
            {
                if (dist < maxAudioDistance)
                {
                    AudioClip clip = SelectAmbientClip(isNight, i);
                    if (clip != null)
                    {
                        ambientSources[i].clip = clip;
                        ambientSources[i].volume = Mathf.Lerp(0.4f, 0.1f, dist / maxAudioDistance);
                        ambientSources[i].pitch = Random.Range(0.8f, 1.2f);
                        ambientSources[i].Play();
                    }
                }

                sourceTimers[i] = Random.Range(8f, 25f);
            }
        }
    }

    private AudioClip SelectAmbientClip(bool isNight, int index)
    {
        if (isNight)
        {
            int r = index % 4;
            if (r == 0 && owlHootClip != null) return owlHootClip;
            if (r == 1 && cricketClip != null) return cricketClip;
            if (r == 2 && wolfHowlClip != null) return wolfHowlClip;
            if (r == 3 && windClip != null) return windClip;
        }
        else
        {
            int r = index % 3;
            if (r == 0 && windClip != null) return windClip;
            if (r == 1 && crowCawClip != null) return crowCawClip;
            if (r == 2 && waterDripClip != null) return waterDripClip;
        }

        return windClip;
    }
}
