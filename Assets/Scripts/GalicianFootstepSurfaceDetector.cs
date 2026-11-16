using UnityEngine;

/// <summary>
/// Detector Fisico de Superficie e Emissor de Passos Galegos (The Forest AAA).
/// Sintetiza 6 bancos procedurais por superficie (Humus, Lama, Granito, Musgo, Madeira, Agua)
/// e detecta terreno/estruturas sem alocacoes de memoria por passo.
/// </summary>
public class GalicianFootstepSurfaceDetector : MonoBehaviour
{
    public static GalicianFootstepSurfaceDetector Instance { get; private set; }

    [Header("Audio Source")]
    public AudioSource footstepAudioSource;

    [Header("Colecoes de Audio por Piso")]
    public AudioClip[] humusClips;
    public AudioClip[] mudClips;
    public AudioClip[] graniteClips;
    public AudioClip[] mossClips;
    public AudioClip[] woodClips;
    public AudioClip[] waterClips;

    [Header("Particulas de Respingos Opcionais")]
    public ParticleSystem mudSplashPrefab;
    public ParticleSystem waterRipplePrefab;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (footstepAudioSource == null) footstepAudioSource = GetComponent<AudioSource>();
        EnsureProceduralSurfaceBanks();
    }

    private void EnsureProceduralSurfaceBanks()
    {
        if (humusClips == null || humusClips.Length == 0) humusClips = BuildSurfaceBank("Humus", 0);
        if (mudClips == null || mudClips.Length == 0) mudClips = BuildSurfaceBank("Mud", 1);
        if (graniteClips == null || graniteClips.Length == 0) graniteClips = BuildSurfaceBank("Granite", 2);
        if (mossClips == null || mossClips.Length == 0) mossClips = BuildSurfaceBank("Moss", 3);
        if (woodClips == null || woodClips.Length == 0) woodClips = BuildSurfaceBank("Wood", 4);
        if (waterClips == null || waterClips.Length == 0) waterClips = BuildSurfaceBank("Water", 5);
    }

    private static AudioClip[] BuildSurfaceBank(string label, int surfaceKind)
    {
        const int sr = 22050;
        AudioClip[] bank = new AudioClip[3];
        for (int v = 0; v < 3; v++)
        {
            float dur = surfaceKind == 5 ? 0.19f : (surfaceKind == 1 ? 0.14f : 0.10f);
            int count = Mathf.Max(512, (int)(sr * dur));
            float[] samples = new float[count];
            float basePitch = 1f + (v - 1) * 0.09f;
            uint seed = (uint)(surfaceKind * 977 + v * 313 + 17);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float sec = (float)i / sr;
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 16) / 65535f) * 2f - 1f;

                float sample = 0f;
                switch (surfaceKind)
                {
                    case 0: // Humus: folha seca + graveto
                    {
                        float env = Mathf.Exp(-14f * t);
                        float crunch = white * (0.42f + 0.35f * Mathf.Sin(t * 28f)) * env;
                        float thump = Mathf.Sin(2f * Mathf.PI * 82f * basePitch * sec) * Mathf.Exp(-20f * t) * 0.45f;
                        sample = crunch + thump;
                        break;
                    }
                    case 1: // Lama: squelch umido grave
                    {
                        float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-7f * t);
                        float squelch = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 70f, t) * basePitch * sec) * 0.55f + white * 0.25f;
                        sample = squelch * env;
                        break;
                    }
                    case 2: // Granito: batida seca e aguda de pedra
                    {
                        float env = Mathf.Exp(-28f * t);
                        float click = Mathf.Sin(2f * Mathf.PI * 540f * basePitch * sec) * 0.55f
                                    + Mathf.Sin(2f * Mathf.PI * 1120f * basePitch * sec) * 0.28f
                                    + white * 0.30f;
                        sample = click * env;
                        break;
                    }
                    case 3: // Musgo: passo macio abafado
                    {
                        float env = Mathf.Exp(-16f * t);
                        sample = (Mathf.Sin(2f * Mathf.PI * 64f * basePitch * sec) * 0.50f + white * 0.16f) * env;
                        break;
                    }
                    case 4: // Madeira: ressonancia oca de tabua/tora
                    {
                        float env = Mathf.Exp(-18f * t);
                        float hollow = Mathf.Sin(2f * Mathf.PI * 195f * basePitch * sec) * 0.62f
                                     + Mathf.Sin(2f * Mathf.PI * 365f * basePitch * sec) * 0.28f;
                        sample = (hollow + white * 0.12f) * env;
                        break;
                    }
                    default: // Agua: respingo fluido
                    {
                        float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-4.5f * t);
                        float ripple = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 140f, t) * sec) * 0.22f;
                        sample = (white * 0.65f + ripple) * env;
                        break;
                    }
                }
                samples[i] = Mathf.Clamp(sample * 0.55f, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create($"Footstep_{label}_{v}", count, 1, sr, false);
            clip.SetData(samples, 0);
            bank[v] = clip;
        }
        return bank;
    }

    public void PlayFootstep(Vector3 footPosition, bool isSprinting, bool isCrouching = false)
    {
        if (footstepAudioSource == null) return;

        float baseVolume = isSprinting ? 0.92f : (isCrouching ? 0.32f : 0.62f);

        if (FragaWorldBlueprint.GetWaterDepth(footPosition.x, footPosition.z) > 0.06f)
        {
            PlaySurfaceClip(waterClips, baseVolume);
            return;
        }

        if (!Physics.Raycast(footPosition + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 0.90f, ~0, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        AudioClip[] targetClips = humusClips;
        string colName = hit.collider.name;

        if (colName.IndexOf("Wood", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Deck", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Step_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Roof_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Bridge", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Dock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Horreo", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            targetClips = woodClips;
        }
        else if (colName.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 colName.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 colName.IndexOf("Granite", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 colName.IndexOf("Ruin", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            targetClips = graniteClips;
        }
        else if (colName.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 colName.IndexOf("Puddle", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            targetClips = waterClips;
        }
        else if (hit.collider is TerrainCollider terrainCol)
        {
            Terrain terrain = terrainCol.GetComponent<Terrain>();
            if (terrain != null)
            {
                int dominantSplat = GetDominantTerrainTextureIndex(terrain, hit.point);
                switch (dominantSplat)
                {
                    case 0: targetClips = humusClips; break;
                    case 1: targetClips = mudClips; break;
                    case 2: targetClips = graniteClips; break;
                    case 3: targetClips = mossClips; break;
                    default: targetClips = humusClips; break;
                }
            }
        }
        else
        {
            var reg = FragaWorldBlueprint.GetRegion(footPosition.x, footPosition.z);
            bool raining = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining;
            if (hit.normal.y < 0.84f || footPosition.y > 16f || reg == FragaWorldBlueprint.WorldRegion.FarolBluff)
                targetClips = graniteClips;
            else if (reg == FragaWorldBlueprint.WorldRegion.SwampBasin || (raining && footPosition.y < 6f))
                targetClips = mudClips;
            else if (reg == FragaWorldBlueprint.WorldRegion.PineRidge || reg == FragaWorldBlueprint.WorldRegion.HighlandLake)
                targetClips = mossClips;
            else
                targetClips = humusClips;
        }

        PlaySurfaceClip(targetClips, baseVolume);
    }

    private void PlaySurfaceClip(AudioClip[] targetClips, float baseVolume)
    {
        if (targetClips != null && targetClips.Length > 0)
        {
            AudioClip clip = targetClips[Random.Range(0, targetClips.Length)];
            if (clip != null)
            {
                footstepAudioSource.pitch = Random.Range(0.92f, 1.08f);
                footstepAudioSource.PlayOneShot(clip, baseVolume);
            }
        }
    }

    private int GetDominantTerrainTextureIndex(Terrain terrain, Vector3 worldPos)
    {
        if (terrain == null || terrain.terrainData == null) return 0;

        TerrainData tData = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;

        int mapX = Mathf.FloorToInt(((worldPos.x - tPos.x) / tData.size.x) * tData.alphamapWidth);
        int mapZ = Mathf.FloorToInt(((worldPos.z - tPos.z) / tData.size.z) * tData.alphamapHeight);

        mapX = Mathf.Clamp(mapX, 0, tData.alphamapWidth - 1);
        mapZ = Mathf.Clamp(mapZ, 0, tData.alphamapHeight - 1);

        float[,,] splatmapData = tData.GetAlphamaps(mapX, mapZ, 1, 1);
        int maxIndex = 0;
        float maxWeight = 0f;

        for (int i = 0; i < tData.alphamapLayers; i++)
        {
            if (splatmapData[0, 0, i] > maxWeight)
            {
                maxWeight = splatmapData[0, 0, i];
                maxIndex = i;
            }
        }

        return maxIndex;
    }
}
