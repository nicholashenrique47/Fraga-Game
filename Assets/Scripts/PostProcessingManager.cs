using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using UnityEngine.UI;

/// PostProcessingManager: Gere todos os efeitos visuais AAA de dano, morte e tensao.
/// Opera sobre o Volume URP global e sincroniza com HapticManager para feedback fisico.
/// Substitui e estende a logica basica do SanityVisualsManager com transicoes cinematicas.
public class PostProcessingManager : MonoBehaviour
{
    [Header("Dependencies")]
    public SanitySystem sanitySystem;
    public HapticManager hapticManager;
    public Volume postProcessVolume;
    public PlayerStatus playerStatus;

    [Header("Cold Breathing Effect")]
    public float breathingFogThreshold = 8f; // Temperatura abaixo da qual o bafo aparece (apenas no frio real)
    private CanvasGroup breathFogOverlay; // Overlay branco semitransparente que pulsa

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
    private Tonemapping tonemapping;

    // Estado interno
    private float lastSanity = 100f;
    private float lastFlashTime = -10f;
    private bool isDeathSequenceActive = false;
    private float deathSequenceTimer = 0f;

    // Valores base capturados no Start para interpolar contra
    private float baseVignetteIntensity = 0.08f;
    private float baseSaturation = 0f;
    private float baseContrast = 0f;
    private float baseGrainIntensity = 0f;
    private float baseLensDistortion = 0f;
    private float baseChromaticAberration = 0f;

    private void Start()
    {
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();

        // 0. Garante que o deathOverlay comece 100% invisível e não-bloqueante
        if (deathOverlay == null)
        {
            var overlayObj = GameObject.Find("Death_Overlay");
            if (overlayObj != null) deathOverlay = overlayObj.GetComponent<CanvasGroup>();
        }

        if (deathOverlay != null)
        {
            deathOverlay.alpha = 0f;
            deathOverlay.blocksRaycasts = false;
            deathOverlay.interactable = false;
        }

        isDeathSequenceActive = false;
        deathSequenceTimer = 0f;

        if (postProcessVolume == null)
        {
            postProcessVolume = FindAnyObjectByType<Volume>();
            if (postProcessVolume == null)
            {
                Debug.LogWarning("[PostProcessingManager] Volume URP nao encontrado na cena.");
                return;
            }
        }

        if (postProcessVolume.profile == null)
        {
            Debug.Log("[PostProcessingManager] Profile nao encontrado no Volume. Criando VolumeProfile em runtime.");
            postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
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
        EnsureOverride(out tonemapping);

        // Configurar estados iniciais
        if (vignette != null)
        {
            vignette.intensity.overrideState = true;
            vignette.color.overrideState = true;
            vignette.smoothness.overrideState = true;
            vignette.intensity.value = baseVignetteIntensity;
        }
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = 0.50f; // Clareamento suave e limpo The Forest
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
        if (tonemapping != null)
        {
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;
        }
        if (bloom != null)
        {
            bloom.intensity.overrideState = true;
            bloom.threshold.overrideState = true;
            bloom.intensity.value = 0.35f;
            bloom.threshold.value = 1.15f;
        }
        if (depthOfField != null)
        {
            depthOfField.mode.overrideState = true;
            depthOfField.mode.value = DepthOfFieldMode.Gaussian;
            depthOfField.gaussianStart.overrideState = true;
            depthOfField.gaussianEnd.overrideState = true;
            depthOfField.gaussianMaxRadius.overrideState = true;
            depthOfField.gaussianStart.value = 8f;
            depthOfField.gaussianEnd.value = 40f;
            depthOfField.gaussianMaxRadius.value = 0.8f;
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

            // Screen Shake proporcional ao dano
            if (ScreenShakeSystem.Instance != null)
            {
                float shakeIntensity = Mathf.Clamp01(sanityDelta / 30f);
                ScreenShakeSystem.Instance.AddTrauma(shakeIntensity * 0.5f);
            }
        }

        UpdateAmbientPostProcessing(sanity);
        UpdateBreathingFog();
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
            vignette.intensity.value = Mathf.Lerp(baseVignetteIntensity, 0.45f, t);
            vignette.smoothness.value = Mathf.Lerp(0.2f, 0.65f, t);

            // Transicao de cor: preto -> vermelho escuro -> vermelho saturado
            Color vignetteColor;
            if (sanity > 50f)
            {
                vignetteColor = Color.Lerp(Color.black, new Color(0.2f, 0f, 0f), t * 2f);
            }
            else
            {
                float panicT = (50f - sanity) / 50f;
                vignetteColor = Color.Lerp(new Color(0.2f, 0f, 0f), new Color(0.45f, 0f, 0f), panicT);

                // Pulsacao suave de batimento cardiaco na vignette em panico (sem efeito estroboscopico)
                float pulse = Mathf.Sin(Time.time * 2.2f) * 0.5f + 0.5f;
                vignette.intensity.value += pulse * 0.05f * panicT;
            }
            vignette.color.value = vignetteColor;
        }

        // Camada 2: Dessaturacao progressiva e exposicao clara
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = Mathf.Lerp(baseSaturation, -50f, t);
            colorAdjustments.contrast.value = Mathf.Lerp(baseContrast, 20f, t);

            // Exposicao: sempre nítido (+0.50 EV) durante jogo normal, escurece apenas em pânico real extremo
            if (sanity < 20f)
            {
                float darkT = (20f - sanity) / 20f;
                colorAdjustments.postExposure.value = Mathf.Lerp(0.50f, -0.25f, darkT);
            }
            else
            {
                colorAdjustments.postExposure.value = 0.50f;
            }
        }

        // Camada 3: Film Grain (paranoia visual)
        if (filmGrain != null)
        {
            filmGrain.intensity.value = Mathf.Lerp(baseGrainIntensity, 0.65f, t);
        }

        // Camada 4: Distorcao de lente (instabilidade mental)
        if (lensDistortion != null)
        {
            if (sanity < 35f)
            {
                float distT = (35f - sanity) / 35f;
                float wave = Mathf.Sin(Time.time * 2.0f) * distT;
                lensDistortion.intensity.value = Mathf.Lerp(baseLensDistortion, -0.2f, distT) + wave * 0.05f;
            }
            else
            {
                lensDistortion.intensity.value = baseLensDistortion;
            }
        }

        // Camada 5: Aberracao cromatica (visao danificada)
        if (chromaticAberration != null)
        {
            if (sanity < 25f)
            {
                float chromaT = (25f - sanity) / 25f;
                chromaticAberration.intensity.value = Mathf.Lerp(baseChromaticAberration, 0.45f, chromaT);
            }
            else
            {
                chromaticAberration.intensity.value = baseChromaticAberration;
            }
        }

        // Camada 6: Motion Blur em panico extremo
        if (motionBlur != null)
        {
            motionBlur.intensity.value = sanity < 12f ? Mathf.Lerp(0f, 0.3f, (12f - sanity) / 12f) : 0f;
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
                vignette.intensity.value += flashCurve * 0.15f * normalizedIntensity;
                vignette.color.value = Color.Lerp(vignette.color.value, new Color(0.6f, 0f, 0f), flashCurve);
            }

            if (chromaticAberration != null)
            {
                chromaticAberration.intensity.value += flashCurve * 0.3f * normalizedIntensity;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// Atualiza o efeito de bafo de frio (breathing fog)
    private void UpdateBreathingFog()
    {
        if (playerStatus == null) return;

        float temp = playerStatus.bodyTemperature;
        if (temp < breathingFogThreshold)
        {
            if (breathFogOverlay == null)
            {
                GameObject fogObj = new GameObject("BreathFogOverlay");
                Canvas canvas = fogObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90;

                CanvasScaler scaler = fogObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                GameObject imgObj = new GameObject("FogImage");
                imgObj.transform.SetParent(fogObj.transform);
                RectTransform imgRect = imgObj.AddComponent<RectTransform>();
                imgRect.anchorMin = Vector2.zero;
                imgRect.anchorMax = Vector2.one;
                imgRect.offsetMin = Vector2.zero;
                imgRect.offsetMax = Vector2.zero;

                Image fogImg = imgObj.AddComponent<Image>();
                fogImg.color = new Color(0.85f, 0.9f, 1f, 0.04f);
                fogImg.raycastTarget = false;

                breathFogOverlay = fogObj.AddComponent<CanvasGroup>();
                breathFogOverlay.alpha = 0f;
                breathFogOverlay.blocksRaycasts = false;
                breathFogOverlay.interactable = false;
            }

            float coldT = Mathf.Clamp01((breathingFogThreshold - temp) / breathingFogThreshold);
            float breathCycle = Mathf.Sin(Time.time * 1.5f) * 0.5f + 0.5f;
            breathFogOverlay.alpha = coldT * breathCycle * 0.06f;
        }
        else
        {
            if (breathFogOverlay != null && breathFogOverlay.alpha > 0f)
            {
                breathFogOverlay.alpha = Mathf.Lerp(breathFogOverlay.alpha, 0f, Time.deltaTime * 3f);
            }
        }
    }

    /// Sequencia cinematica de morte. Chamado pelo GameManager antes de TriggerGameOver.
    public void StartDeathSequence()
    {
        if (isDeathSequenceActive) return;
        if (Time.timeSinceLevelLoad < 3.0f)
        {
            Debug.LogWarning("[PostProcessingManager] StartDeathSequence suprimida durante o boot inicial da cena.");
            return;
        }
        isDeathSequenceActive = true;
        deathSequenceTimer = 0f;

        if (hapticManager != null)
        {
            hapticManager.TriggerHeavyVibration();
        }

        if (ScreenShakeSystem.Instance != null)
        {
            ScreenShakeSystem.Instance.ShakeHeavy();
        }

        Debug.Log("[PostProcessingManager] Sequencia de morte iniciada.");
    }

    private void UpdateDeathSequence()
    {
        deathSequenceTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(deathSequenceTimer / deathFadeDuration);

        float easedT = t * t * t;

        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(0.4f, 1f, easedT);
            vignette.smoothness.value = Mathf.Lerp(0.4f, 1f, easedT);
            vignette.color.value = Color.Lerp(new Color(0.4f, 0f, 0f), Color.black, easedT);
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = Mathf.Lerp(0f, -6f, easedT);
            colorAdjustments.saturation.value = -100f;
        }

        if (deathOverlay != null)
        {
            deathOverlay.alpha = easedT;
        }

        if (t >= 1f)
        {
            isDeathSequenceActive = false;
            GameManager gm = FindAnyObjectByType<GameManager>();
            if (gm != null) gm.TriggerGameOver();
        }
    }

    public void ResetAllEffects()
    {
        ResetEffects();
    }

    public void ResetEffects()
    {
        isDeathSequenceActive = false;
        deathSequenceTimer = 0f;
        if (deathOverlay != null) deathOverlay.alpha = 0f;
        if (breathFogOverlay != null) breathFogOverlay.alpha = 0f;

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
            colorAdjustments.postExposure.value = 0.50f;
        }
    }
}
