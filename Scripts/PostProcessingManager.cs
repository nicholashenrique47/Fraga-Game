using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// PostProcessingManager: Gere todos os efeitos visuais AAA de dano, morte e tensao.
/// Opera sobre o Volume URP global e sincroniza com HapticManager para feedback fisico.
/// Substitui e estende a logica basica do SanityVisualsManager com transicoes cinematicas.
public class PostProcessingManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SanitySystem sanitySystem;
    public HapticManager hapticManager;
    public Volume postProcessVolume;

    [Header("Death Sequence")]
    public float deathFadeDuration = 2.5f;
    public CanvasGroup deathOverlay; // Painel preto no Canvas para fade-to-black

    [Header("Damage Flash")]
    public float flashDuration = 0.15f;
    public float flashCooldown = 0.4f;

    // URP Post-Processing overrides
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private FilmGrain filmGrain;
    private LensDistortion lensDistortion;
    private ChromaticAberration chromaticAberration;
    private MotionBlur motionBlur;
    private Bloom bloom;
    private DepthOfField depthOfField;

    // Estado interno
    private float lastSanity = 100f;
    private float lastFlashTime = -10f;
    private bool isDeathSequenceActive = false;
    private float deathSequenceTimer = 0f;

    // Valores base capturados no Start para interpolar contra
    private float baseVignetteIntensity = 0.15f;
    private float baseSaturation = 0f;
    private float baseContrast = 0f;
    private float baseGrainIntensity = 0f;
    private float baseLensDistortion = 0f;
    private float baseChromaticAberration = 0f;

    private void Start()
    {
        if (postProcessVolume == null || postProcessVolume.profile == null)
        {
            Debug.LogError("[PostProcessingManager] Volume URP nao assinalado. Criando profile em runtime.");
            if (postProcessVolume != null)
            {
                postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }
            return;
        }

        // Extrair ou criar overrides
        EnsureOverride(out vignette);
        EnsureOverride(out colorAdjustments);
        EnsureOverride(out filmGrain);
        EnsureOverride(out lensDistortion);
        EnsureOverride(out chromaticAberration);
        EnsureOverride(out motionBlur);
        EnsureOverride(out bloom);
        EnsureOverride(out depthOfField);

        // Configurar estados iniciais
        if (vignette != null)
        {
            vignette.intensity.overrideState = true;
            vignette.color.overrideState = true;
            vignette.smoothness.overrideState = true;
        }
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.postExposure.overrideState = true;
        }
        if (filmGrain != null)
        {
            filmGrain.intensity.overrideState = true;
            filmGrain.type.overrideState = true;
            filmGrain.type.value = FilmGrainLookup.Thin1;
        }
        if (lensDistortion != null)
        {
            lensDistortion.intensity.overrideState = true;
        }
        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.overrideState = true;
        }
        if (motionBlur != null)
        {
            motionBlur.intensity.overrideState = true;
            motionBlur.quality.overrideState = true;
            motionBlur.quality.value = MotionBlurQuality.Low; // Mobile-safe
        }
        if (bloom != null)
        {
            bloom.intensity.overrideState = true;
            bloom.threshold.overrideState = true;
            bloom.intensity.value = 0.5f;
            bloom.threshold.value = 0.9f;
        }
        if (depthOfField != null)
        {
            depthOfField.mode.overrideState = true;
            depthOfField.mode.value = DepthOfFieldMode.Gaussian;
            depthOfField.gaussianStart.overrideState = true;
            depthOfField.gaussianEnd.overrideState = true;
            depthOfField.gaussianMaxRadius.overrideState = true;
            depthOfField.gaussianStart.value = 5f;
            depthOfField.gaussianEnd.value = 20f;
            depthOfField.gaussianMaxRadius.value = 1f;
        }
    }

    private void EnsureOverride<T>(out T component) where T : VolumeComponent
    {
        if (!postProcessVolume.profile.TryGet(out component))
        {
            component = postProcessVolume.profile.Add<T>(true);
        }
    }

    private void Update()
    {
        if (sanitySystem == null) return;

        float sanity = sanitySystem.sanity;

        if (isDeathSequenceActive)
        {
            UpdateDeathSequence();
            return;
        }

        // Deteccao de dano (queda subita de sanidade)
        float sanityDelta = lastSanity - sanity;
        if (sanityDelta > 3f && Time.time - lastFlashTime > flashCooldown)
        {
            StartCoroutine(DamageFlashCoroutine(sanityDelta));
            lastFlashTime = Time.time;

            if (hapticManager != null)
            {
                hapticManager.TriggerHeavyVibration();
            }
        }

        UpdateAmbientPostProcessing(sanity);
        lastSanity = sanity;
    }

    public void TriggerDamageFlash()
    {
        if (Time.time - lastFlashTime > flashCooldown)
        {
            StartCoroutine(DamageFlashCoroutine(25f));
            lastFlashTime = Time.time;
            
            // Hit-Stop leve
            if (!isDeathSequenceActive)
            {
                StartCoroutine(HitStopCoroutine());
            }
        }
    }

    private IEnumerator HitStopCoroutine()
    {
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(0.05f);
        Time.timeScale = 1f;
    }

    /// Interpolacao continua dos efeitos baseada na sanidade atual.
    /// Camadas de intensidade: Normal (100-60), Tensao (60-30), Panico (30-0).
    private void UpdateAmbientPostProcessing(float sanity)
    {
        // t normalizado: 0 = sanidade cheia, 1 = sanidade zero
        float t = 1f - (sanity / 100f);

        // Camada 1: Vignette (suave -> agressivo vermelho escuro)
        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(baseVignetteIntensity, 0.65f, t);
            vignette.smoothness.value = Mathf.Lerp(0.2f, 0.8f, t);

            // Transicao de cor: preto -> vermelho escuro -> vermelho saturado
            Color vignetteColor;
            if (sanity > 50f)
            {
                vignetteColor = Color.Lerp(Color.black, new Color(0.2f, 0f, 0f), t * 2f);
            }
            else
            {
                float panicT = (50f - sanity) / 50f;
                vignetteColor = Color.Lerp(new Color(0.2f, 0f, 0f), new Color(0.5f, 0f, 0f), panicT);

                // Pulsacao ritmica da vignette em panico
                float pulse = Mathf.Sin(Time.time * (3f + panicT * 8f)) * 0.5f + 0.5f;
                vignette.intensity.value += pulse * 0.15f * panicT;
            }
            vignette.color.value = vignetteColor;
        }

        // Camada 2: Dessaturacao progressiva e contraste
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = Mathf.Lerp(baseSaturation, -85f, t);
            colorAdjustments.contrast.value = Mathf.Lerp(baseContrast, 40f, t);

            // Exposicao: escurece ligeiramente em panico
            if (sanity < 20f)
            {
                float darkT = (20f - sanity) / 20f;
                colorAdjustments.postExposure.value = Mathf.Lerp(0f, -1.5f, darkT);
            }
            else
            {
                colorAdjustments.postExposure.value = 0f;
            }
        }

        // Camada 3: Film Grain (paranoia visual)
        if (filmGrain != null)
        {
            filmGrain.intensity.value = Mathf.Lerp(baseGrainIntensity, 0.95f, t);
        }

        // Camada 4: Distorcao de lente (instabilidade mental)
        if (lensDistortion != null)
        {
            if (sanity < 40f)
            {
                float distT = (40f - sanity) / 40f;
                // Oscilacao ondulante da distorcao
                float wave = Mathf.Sin(Time.time * 2.5f) * distT;
                lensDistortion.intensity.value = Mathf.Lerp(baseLensDistortion, -0.4f, distT) + wave * 0.1f;
            }
            else
            {
                lensDistortion.intensity.value = baseLensDistortion;
            }
        }

        // Camada 5: Aberracao cromatica (visao danificada)
        if (chromaticAberration != null)
        {
            if (sanity < 30f)
            {
                float chromaT = (30f - sanity) / 30f;
                chromaticAberration.intensity.value = Mathf.Lerp(baseChromaticAberration, 0.8f, chromaT);
            }
            else
            {
                chromaticAberration.intensity.value = baseChromaticAberration;
            }
        }

        // Camada 6: Motion Blur em panico extremo
        if (motionBlur != null)
        {
            motionBlur.intensity.value = sanity < 15f ? Mathf.Lerp(0f, 0.5f, (15f - sanity) / 15f) : 0f;
        }
    }

    /// Flash de dano: pulso rapido vermelho na vignette + chromatic aberration spike.
    private IEnumerator DamageFlashCoroutine(float intensity)
    {
        float normalizedIntensity = Mathf.Clamp01(intensity / 30f);
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            float t = elapsed / flashDuration;
            float flashCurve = 1f - (t * t); // Ease-out quadratico

            if (vignette != null)
            {
                vignette.intensity.value += flashCurve * 0.3f * normalizedIntensity;
                vignette.color.value = Color.Lerp(vignette.color.value, new Color(0.8f, 0f, 0f), flashCurve);
            }

            if (chromaticAberration != null)
            {
                chromaticAberration.intensity.value += flashCurve * 0.5f * normalizedIntensity;
            }

            elapsed += Time.unscaledDeltaTime; // unscaledDeltaTime para funcionar durante a pausa
            yield return null;
        }
    }

    /// Sequencia cinematica de morte. Chamado pelo GameManager antes de TriggerGameOver.
    public void StartDeathSequence()
    {
        if (isDeathSequenceActive) return;
        isDeathSequenceActive = true;
        deathSequenceTimer = 0f;

        if (hapticManager != null)
        {
            hapticManager.TriggerHeavyVibration();
        }

        Debug.Log("[PostProcessingManager] Sequencia de morte iniciada.");
    }

    private void UpdateDeathSequence()
    {
        deathSequenceTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(deathSequenceTimer / deathFadeDuration);

        // Curva de ease-in para dramatismo
        float easedT = t * t * t;

        // Fade visual progressivo
        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(0.5f, 1f, easedT);
            vignette.smoothness.value = Mathf.Lerp(0.5f, 1f, easedT);
            vignette.color.value = Color.Lerp(new Color(0.5f, 0f, 0f), Color.black, easedT);
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = Mathf.Lerp(-1f, -8f, easedT);
            colorAdjustments.saturation.value = -100f;
        }

        if (filmGrain != null)
        {
            filmGrain.intensity.value = 1f;
        }

        if (lensDistortion != null)
        {
            lensDistortion.intensity.value = Mathf.Lerp(-0.3f, -0.6f, easedT);
        }

        // Fade-to-black via Canvas overlay
        if (deathOverlay != null)
        {
            deathOverlay.alpha = easedT;
        }

        // Sequencia completa
        if (t >= 1f)
        {
            isDeathSequenceActive = false;
            GameManager gm = FindAnyObjectByType<GameManager>();
            if (gm != null) gm.TriggerGameOver();
        }
    }

    /// Chamado pelo GameManager apos Revive para resetar todos os efeitos visuais.
    public void ResetAllEffects()
    {
        isDeathSequenceActive = false;
        deathSequenceTimer = 0f;

        if (deathOverlay != null) deathOverlay.alpha = 0f;

        if (vignette != null)
        {
            vignette.intensity.value = baseVignetteIntensity;
            vignette.color.value = Color.black;
            vignette.smoothness.value = 0.2f;
        }
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = baseSaturation;
            colorAdjustments.contrast.value = baseContrast;
            colorAdjustments.postExposure.value = 0f;
        }
        if (filmGrain != null) filmGrain.intensity.value = baseGrainIntensity;
        if (lensDistortion != null) lensDistortion.intensity.value = baseLensDistortion;
        if (chromaticAberration != null) chromaticAberration.intensity.value = baseChromaticAberration;
        if (motionBlur != null) motionBlur.intensity.value = 0f;

        Debug.Log("[PostProcessingManager] Efeitos visuais resetados.");
    }
}
