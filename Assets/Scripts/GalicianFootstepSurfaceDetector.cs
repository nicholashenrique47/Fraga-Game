using UnityEngine;

/// <summary>
/// Detector Fisico de Superficie e Emissor de Passos Galegos (The Forest AAA).
/// Prioriza clipes externos em Resources/Audio/Footsteps/{Surface} e utiliza sintese multi-banda filtrada
/// como fallback (6 superficies: Humus, Mud, Granite, Moss, Wood, Water), alem de impacto de aterrissagem.
/// </summary>
public class GalicianFootstepSurfaceDetector : MonoBehaviour
{
    public static GalicianFootstepSurfaceDetector Instance { get; private set; }

    public enum SurfaceMaterialType
    {
        Humus = 0,
        Mud = 1,
        Granite = 2,
        Moss = 3,
        Wood = 4,
        Water = 5
    }

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

    public SurfaceMaterialType LastDetectedSurface { get; private set; } = SurfaceMaterialType.Humus;
    public float LastFootstepVolume { get; private set; } = 0f;
    public float LastLandingVolume { get; private set; } = 0f;

    private int lastClipIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
            return;
        }
        Instance = this;
        EnsureFootstepAudioSource();
        EnsureSurfaceBanks();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void EnsureFootstepAudioSource()
    {
        if (footstepAudioSource != null) return;
        footstepAudioSource = GetComponent<AudioSource>();
        if (footstepAudioSource == null)
        {
            footstepAudioSource = gameObject.AddComponent<AudioSource>();
            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.spatialBlend = 0.25f;
            footstepAudioSource.volume = 0.85f;
        }
    }

    public void EnsureSurfaceBanks()
    {
        if (humusClips == null || humusClips.Length == 0)
            humusClips = LoadOrBuildSurfaceBank("Humus", SurfaceMaterialType.Humus);
        if (mudClips == null || mudClips.Length == 0)
            mudClips = LoadOrBuildSurfaceBank("Mud", SurfaceMaterialType.Mud);
        if (graniteClips == null || graniteClips.Length == 0)
            graniteClips = LoadOrBuildSurfaceBank("Granite", SurfaceMaterialType.Granite);
        if (mossClips == null || mossClips.Length == 0)
            mossClips = LoadOrBuildSurfaceBank("Moss", SurfaceMaterialType.Moss);
        if (woodClips == null || woodClips.Length == 0)
            woodClips = LoadOrBuildSurfaceBank("Wood", SurfaceMaterialType.Wood);
        if (waterClips == null || waterClips.Length == 0)
            waterClips = LoadOrBuildSurfaceBank("Water", SurfaceMaterialType.Water);
    }

    private static AudioClip[] LoadOrBuildSurfaceBank(string label, SurfaceMaterialType surface)
    {
        AudioClip[] external = Resources.LoadAll<AudioClip>("Audio/Footsteps/" + label);
        if (external != null && external.Length > 0) return external;
        return BuildSurfaceBank(label, (int)surface);
    }

    private static AudioClip[] BuildSurfaceBank(string label, int surfaceKind)
    {
        const int sr = 22050;
        AudioClip[] bank = new AudioClip[3];
        for (int v = 0; v < 3; v++)
        {
            float dur = surfaceKind == 5 ? 0.22f : (surfaceKind == 1 ? 0.16f : 0.12f);
            int count = Mathf.Max(512, (int)(sr * dur));
            float[] samples = new float[count];
            float basePitch = 1f + (v - 1) * 0.085f;
            uint seed = (uint)(surfaceKind * 977 + v * 313 + 17);

            float lp1 = 0f;
            float lp2 = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float sec = (float)i / sr;
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 16) / 65535f) * 2f - 1f;
                lp1 = Mathf.Lerp(lp1, white, 0.18f);
                lp2 = Mathf.Lerp(lp2, lp1, 0.07f);
                float bandMid = lp1 - lp2;
                float high = white - lp1;

                float sample = 0f;
                switch (surfaceKind)
                {
                    case 0: // Humus: folha seca + graveto filtrado
                    {
                        float env = Mathf.Exp(-15f * t);
                        float crunch = (bandMid * 0.65f + high * 0.35f) * (0.45f + 0.35f * Mathf.Sin(t * 28f)) * env;
                        float thump = Mathf.Sin(2f * Mathf.PI * 82f * basePitch * sec) * Mathf.Exp(-22f * t) * 0.48f;
                        sample = crunch + thump;
                        break;
                    }
                    case 1: // Lama: squelch umido grave
                    {
                        float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-6.5f * t);
                        float squelch = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(170f, 68f, t) * basePitch * sec) * 0.58f
                                      + lp1 * 0.38f + bandMid * 0.24f;
                        sample = squelch * env;
                        break;
                    }
                    case 2: // Granito: batida seca e nitida de pedra
                    {
                        float env = Mathf.Exp(-27f * t);
                        float click = Mathf.Sin(2f * Mathf.PI * 560f * basePitch * sec) * 0.52f
                                    + Mathf.Sin(2f * Mathf.PI * 1180f * basePitch * sec) * 0.26f
                                    + bandMid * 0.38f + high * 0.14f;
                        sample = click * env;
                        break;
                    }
                    case 3: // Musgo: passo macio abafado
                    {
                        float env = Mathf.Exp(-16f * t);
                        sample = (Mathf.Sin(2f * Mathf.PI * 66f * basePitch * sec) * 0.52f + lp2 * 0.32f + bandMid * 0.14f) * env;
                        break;
                    }
                    case 4: // Madeira: ressonancia oca de tabua/tora
                    {
                        float env = Mathf.Exp(-18f * t);
                        float hollow = Mathf.Sin(2f * Mathf.PI * 195f * basePitch * sec) * 0.62f
                                     + Mathf.Sin(2f * Mathf.PI * 375f * basePitch * sec) * 0.28f;
                        sample = (hollow + bandMid * 0.24f) * env;
                        break;
                    }
                    default: // Agua: respingo fluido em banda media
                    {
                        float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-4.2f * t);
                        float ripple = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(340f, 130f, t) * sec) * 0.26f;
                        sample = (bandMid * 0.72f + high * 0.22f + ripple) * env;
                        break;
                    }
                }
                samples[i] = Mathf.Clamp(sample * 0.60f, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create($"Footstep_{label}_{v}", count, 1, sr, false);
            clip.SetData(samples, 0);
            bank[v] = clip;
        }
        return bank;
    }

    public SurfaceMaterialType DetectSurfaceAt(Vector3 footPosition)
    {
        if (FragaWorldBlueprint.GetWaterDepth(footPosition.x, footPosition.z) > 0.06f)
        {
            return SurfaceMaterialType.Water;
        }

        if (!Physics.Raycast(footPosition + Vector3.up * 0.30f, Vector3.down, out RaycastHit hit, 1.15f, ~0, QueryTriggerInteraction.Ignore))
        {
            var fallbackReg = FragaWorldBlueprint.GetRegion(footPosition.x, footPosition.z);
            if (fallbackReg == FragaWorldBlueprint.WorldRegion.SwampBasin) return SurfaceMaterialType.Mud;
            if (fallbackReg == FragaWorldBlueprint.WorldRegion.FarolBluff) return SurfaceMaterialType.Granite;
            if (fallbackReg == FragaWorldBlueprint.WorldRegion.PineRidge || fallbackReg == FragaWorldBlueprint.WorldRegion.HighlandLake) return SurfaceMaterialType.Moss;
            return SurfaceMaterialType.Humus;
        }

        string colName = hit.collider.name;

        if (colName.IndexOf("Wood", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Deck", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Step_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Roof_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Bridge", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Dock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Horreo", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Plank", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Cabin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Piso", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Escada", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SurfaceMaterialType.Wood;
        }

        if (colName.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Stone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Granite", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Ruin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Pedra", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SurfaceMaterialType.Granite;
        }

        if (colName.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Puddle", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SurfaceMaterialType.Water;
        }

        if (colName.IndexOf("Mud", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Swamp", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SurfaceMaterialType.Mud;
        }

        if (colName.IndexOf("Moss", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SurfaceMaterialType.Moss;
        }

        if (hit.collider is TerrainCollider terrainCol)
        {
            Terrain terrain = terrainCol.GetComponent<Terrain>();
            if (terrain != null)
            {
                int dominantSplat = GetDominantTerrainTextureIndex(terrain, hit.point);
                switch (dominantSplat)
                {
                    case 1: return SurfaceMaterialType.Mud;
                    case 2: return SurfaceMaterialType.Granite;
                    case 3: return SurfaceMaterialType.Moss;
                    default: return SurfaceMaterialType.Humus;
                }
            }
        }

        var reg = FragaWorldBlueprint.GetRegion(footPosition.x, footPosition.z);
        bool raining = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining;
        if (hit.normal.y < 0.84f || footPosition.y > 16f || reg == FragaWorldBlueprint.WorldRegion.FarolBluff)
            return SurfaceMaterialType.Granite;
        if (reg == FragaWorldBlueprint.WorldRegion.SwampBasin || (raining && footPosition.y < 6f))
            return SurfaceMaterialType.Mud;
        if (reg == FragaWorldBlueprint.WorldRegion.PineRidge || reg == FragaWorldBlueprint.WorldRegion.HighlandLake)
            return SurfaceMaterialType.Moss;
        return SurfaceMaterialType.Humus;
    }

    private AudioClip[] GetClipsForSurface(SurfaceMaterialType surface)
    {
        switch (surface)
        {
            case SurfaceMaterialType.Mud: return mudClips;
            case SurfaceMaterialType.Granite: return graniteClips;
            case SurfaceMaterialType.Moss: return mossClips;
            case SurfaceMaterialType.Wood: return woodClips;
            case SurfaceMaterialType.Water: return waterClips;
            default: return humusClips;
        }
    }

    public void PlayFootstep(Vector3 footPosition, bool isSprinting, bool isCrouching = false)
    {
        EnsureFootstepAudioSource();
        EnsureSurfaceBanks();
        if (footstepAudioSource == null) return;

        float baseVolume = isSprinting ? 0.92f : (isCrouching ? 0.32f : 0.62f);
        SurfaceMaterialType surface = DetectSurfaceAt(footPosition);
        LastDetectedSurface = surface;
        LastFootstepVolume = baseVolume;

        PlaySurfaceClip(GetClipsForSurface(surface), baseVolume, 0.92f, 1.08f);
    }

    public void PlayLandingImpact(Vector3 footPosition, float fallSpeed)
    {
        EnsureFootstepAudioSource();
        EnsureSurfaceBanks();
        if (footstepAudioSource == null) return;

        SurfaceMaterialType surface = DetectSurfaceAt(footPosition);
        LastDetectedSurface = surface;

        float impactVol = Mathf.Clamp(fallSpeed / 14f, 0.30f, 1.0f);
        LastLandingVolume = impactVol;

        PlaySurfaceClip(GetClipsForSurface(surface), impactVol, 0.72f, 0.88f);
        if (surface == SurfaceMaterialType.Water && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.WaterSplashIn, footPosition, impactVol, 0.88f, 1.04f);
        }
    }

    public void PlayWaterSplash(Vector3 worldPos, float intensity = 0.8f)
    {
        EnsureFootstepAudioSource();
        EnsureSurfaceBanks();
        float vol = Mathf.Clamp(intensity, 0.2f, 1.0f);
        LastDetectedSurface = SurfaceMaterialType.Water;
        PlaySurfaceClip(waterClips, vol, 0.90f, 1.10f);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.WaterSplashIn, worldPos, vol, 0.90f, 1.08f);
        }
    }

    private void PlaySurfaceClip(AudioClip[] targetClips, float baseVolume, float pitchMin, float pitchMax)
    {
        if (targetClips == null || targetClips.Length == 0 || footstepAudioSource == null) return;

        int idx = Random.Range(0, targetClips.Length);
        if (targetClips.Length > 1 && idx == lastClipIndex)
        {
            idx = (idx + 1) % targetClips.Length;
        }
        lastClipIndex = idx;

        AudioClip clip = targetClips[idx];
        if (clip != null)
        {
            footstepAudioSource.pitch = Random.Range(pitchMin, pitchMax);
            footstepAudioSource.PlayOneShot(clip, baseVolume);
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
