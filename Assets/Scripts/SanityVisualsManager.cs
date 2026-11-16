using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SanityVisualsManager : MonoBehaviour
{
    [Header("Core Dependencies")]
    public SanitySystem sanitySystem;
    public Volume postProcessVolume;

    [Header("Visual Thresholds")]
    public float hallucinationThreshold = 30f;
    public float severeThreshold = 15f;

    [Header("Hallucination Settings")]
    public Mesh fakeVultoMesh;
    public Material fakeVultoMaterial;
    public int maxHallucinations = 15;
    
    private Matrix4x4[] hallucinationMatrices;
    private int currentHallucinations = 0;
    private float hallucinationTimer = 0f;

    // Post-Processing overrides
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private FilmGrain filmGrain;
    private LensDistortion lensDistortion;

    private void Start()
    {
        hallucinationMatrices = new Matrix4x4[maxHallucinations];

        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out colorAdjustments);
            postProcessVolume.profile.TryGet(out filmGrain);
            postProcessVolume.profile.TryGet(out lensDistortion);
        }
    }

    private void Update()
    {
        if (sanitySystem == null) return;

        float sanity = sanitySystem.sanity;

        // Post-Processing desativado aqui para não entrar em conflito com PostProcessingManager

        if (sanity <= hallucinationThreshold)
        {
            UpdateHallucinations(sanity);
            DrawHallucinations();
        }
        else
        {
            currentHallucinations = 0; // Limpar alucinações se a sanidade recuperar
        }
    }

    private void UpdatePostProcessing(float sanity)
    {
        // Interpolação baseada na perda de sanidade
        float t = 1f - (sanity / 100f); 

        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(0.2f, 0.6f, t);
            vignette.color.value = Color.Lerp(Color.black, new Color(0.3f, 0f, 0f), t); // Vinheta avermelhada no limite
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = Mathf.Lerp(0f, -80f, t); // Drena as cores
            colorAdjustments.contrast.value = Mathf.Lerp(0f, 30f, t);
        }

        if (filmGrain != null)
        {
            filmGrain.intensity.value = Mathf.Lerp(0f, 1f, t); // Grão de filme pesado aumenta paranóia
        }

        if (lensDistortion != null)
        {
            lensDistortion.intensity.value = Mathf.Lerp(0f, -0.3f, t);
        }
    }

    private void UpdateHallucinations(float sanity)
    {
        hallucinationTimer += Time.deltaTime;
        
        // Spawnar e mover instâncias falsas rapidamente (comportamento errático)
        if (hallucinationTimer > 0.5f)
        {
            hallucinationTimer = 0f;
            currentHallucinations = sanity <= severeThreshold ? maxHallucinations : maxHallucinations / 2;

            Vector3 playerPos = sanitySystem.transform.position;

            for (int i = 0; i < currentHallucinations; i++)
            {
                // Aparecem randomicamente nas beiras da visão
                Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(5f, 12f);
                float wx = playerPos.x + randomCircle.x;
                float wz = playerPos.z + randomCircle.y;
                float wy = TerrainMeshGenerator.GetTerrainHeight(wx, wz);
                Vector3 spawnPos = new Vector3(wx, wy, wz);
                
                // Rotação encarando o jogador mas estática
                Vector3 lookDir = playerPos - spawnPos;
                lookDir.y = 0f;
                Quaternion rot = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;
                
                // Escala distorcida (Slimer/Taller) para parecer assustador
                Vector3 scale = new Vector3(Random.Range(0.5f, 1.5f), Random.Range(1.5f, 3f), Random.Range(0.5f, 1.5f));

                hallucinationMatrices[i].SetTRS(spawnPos, rot, scale);
            }
        }
    }

    private void DrawHallucinations()
    {
        if (currentHallucinations > 0 && fakeVultoMesh != null && fakeVultoMaterial != null)
        {
            // Renderiza inimigos falsos que desaparecem se o jogador tentar interagir com eles (apenas visual)
            Graphics.DrawMeshInstanced(fakeVultoMesh, 0, fakeVultoMaterial, hallucinationMatrices, currentHallucinations);
        }
    }
}
