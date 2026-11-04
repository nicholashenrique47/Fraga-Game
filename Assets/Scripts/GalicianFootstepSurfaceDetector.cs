using UnityEngine;

/// <summary>
/// Detector Fisico de Superficie e Emissor de Passos Galegos (The Forest AAA).
/// Reconhece camadas de textura do terreno (Húmus, Lama, Granito, Musgo) ou tags de objetos
/// para disparar áudios táteis e partículas de impacto corretas.
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
    }

    public void PlayFootstep(Vector3 footPosition, bool isSprinting, bool isCrouching = false)
    {
        if (footstepAudioSource == null) return;

        // Dispara raycast para baixo a partir do pé do personagem
        if (!Physics.Raycast(footPosition + Vector3.up * 0.25f, Vector3.down, out RaycastHit hit, 0.75f))
        {
            return;
        }

        AudioClip[] targetClips = humusClips;
        float baseVolume = isSprinting ? 0.95f : (isCrouching ? 0.35f : 0.65f);

        // 1. Verificacao por Tag
        if (hit.collider.CompareTag("Stone") || hit.collider.name.ToLower().Contains("rock") || hit.collider.name.ToLower().Contains("granite"))
        {
            targetClips = graniteClips;
        }
        else if (hit.collider.CompareTag("Wood_Structure") || hit.collider.name.ToLower().Contains("wood") || hit.collider.name.ToLower().Contains("horreo"))
        {
            targetClips = woodClips;
        }
        else if (hit.collider.CompareTag("Water") || hit.collider.name.ToLower().Contains("water") || hit.collider.name.ToLower().Contains("puddle"))
        {
            targetClips = waterClips;
        }
        // 2. Verificacao por Splat do Terreno
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

        // Toca o audio se houver clipes disponiveis
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
