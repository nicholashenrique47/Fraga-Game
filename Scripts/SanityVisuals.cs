using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// SanityVisuals: Liga o estado de Sanidade e Fome as definicoes do Post-Processing URP.
/// Reduz a saturacao das cores (B&W) e aumenta o panico em vinheta nas margens do ecra.
public class SanityVisuals : MonoBehaviour
{
    [Header("Dependencies")]
    public SanitySystem sanitySystem;
    public Volume globalVolume; // O Volume global do URP

    private ColorAdjustments colorAdjustments;
    private Vignette vignette;

    private void Start()
    {
        if (globalVolume == null)
        {
            Debug.LogWarning("[SanityVisuals] Nenhum Global Volume atribuido. Post-processing desativado.");
            return;
        }

        // Tentar ir buscar ou adicionar as overrides necessarias
        if (!globalVolume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments = globalVolume.profile.Add<ColorAdjustments>();
        }
        colorAdjustments.saturation.overrideState = true;

        if (!globalVolume.profile.TryGet(out vignette))
        {
            vignette = globalVolume.profile.Add<Vignette>();
        }
        vignette.intensity.overrideState = true;
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;
    }

    private void Update()
    {
        if (sanitySystem == null || colorAdjustments == null || vignette == null) return;

        float sanity = sanitySystem.sanity;

        // Saturation: Normal = 0, Perto da morte (0 de sanidade) = -100 (Black and white total)
        // Comeca a perder cor abaixo dos 60%
        float satTarget = 0f;
        if (sanity < 60f)
        {
            satTarget = Mathf.Lerp(-100f, 0f, sanity / 60f);
        }
        colorAdjustments.saturation.value = Mathf.Lerp(colorAdjustments.saturation.value, satTarget, Time.deltaTime * 2f);

        // Vignette: Normal = 0.2, Panico = 0.6
        // Comeca a escurecer os cantos abaixo dos 40%
        float vigTarget = 0.2f;
        if (sanity < 40f)
        {
            vigTarget = Mathf.Lerp(0.6f, 0.2f, sanity / 40f);
        }
        vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, vigTarget, Time.deltaTime * 3f);
    }
}
