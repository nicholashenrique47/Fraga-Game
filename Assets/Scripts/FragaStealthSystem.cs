using UnityEngine;

/// <summary>
/// FragaStealthSystem: Calcula visibilidade e ruido do jogador (8 Hz, Zero-GC).
/// Influencia alcance sensorial de Vultos, animais selvagens e feedback tatico no HUD.
/// </summary>
public class FragaStealthSystem : MonoBehaviour
{
    public static FragaStealthSystem Instance { get; private set; }

    [Header("Parametros")]
    public float sampleInterval = 0.125f;
    public float noiseDecayPerSecond = 0.85f;

    public float Visibility01 { get; private set; }
    public float Noise01 { get; private set; }
    public float StealthScore01 => Mathf.Clamp01(1f - Mathf.Max(Visibility01, Noise01));
    public bool IsHidden => Visibility01 < 0.34f && Noise01 < 0.32f;

    private float nextSampleTime = 0f;
    private float pulseNoise01 = 0f;

    private PlayerController playerController;
    private SanitySystem sanitySystem;
    private DayNightCycle dayNightCycle;
    private InventoryAdvanced inventoryCache;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        playerController = GetComponent<PlayerController>();
        sanitySystem = GetComponent<SanitySystem>();
    }

    private void Start()
    {
        if (playerController == null) playerController = PlayerController.Instance ?? FindAnyObjectByType<PlayerController>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (dayNightCycle == null) dayNightCycle = DayNightCycle.Instance ?? FindAnyObjectByType<DayNightCycle>();
        if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
    }

    /// <summary>
    /// Emite pulso instantaneo de ruido (passos, golpes de machado, flecha, queda).
    /// </summary>
    public void EmitNoisePulse(float intensity01)
    {
        float weatherMask = 1f;
        if (GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining)
        {
            weatherMask = 0.72f;
        }
        pulseNoise01 = Mathf.Max(pulseNoise01, Mathf.Clamp01(intensity01 * weatherMask));
        Noise01 = Mathf.Clamp01(Mathf.Max(Noise01, pulseNoise01));
    }

    /// <summary>
    /// Multiplicador de alcance de deteccao para IA (0.34 oculto .. 1.35 exposto/barulhento).
    /// </summary>
    public float GetDetectionMultiplier()
    {
        float exposure = Mathf.Max(Visibility01 * 0.85f, Noise01);
        return Mathf.Lerp(0.34f, 1.35f, exposure);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (pulseNoise01 > 0f)
        {
            pulseNoise01 = Mathf.Max(0f, pulseNoise01 - noiseDecayPerSecond * dt);
        }

        if (Time.time < nextSampleTime) return;
        nextSampleTime = Time.time + sampleInterval;

        if (playerController == null) playerController = PlayerController.Instance;
        if (dayNightCycle == null) dayNightCycle = DayNightCycle.Instance;
        if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;

        bool crouching = playerController != null && playerController.isCrouching;
        bool sprinting = playerController != null && playerController.isSprinting;
        float speed = playerController != null ? playerController.CurrentSpeed : 0f;

        bool hasTorchOrLantern = (playerController != null && playerController.IsLighterOn) ||
                                 (sanitySystem != null && sanitySystem.isLampOn && sanitySystem.oil > 1f) ||
                                 (inventoryCache != null && (inventoryCache.equippedOffHandId == 303 || inventoryCache.equippedOffHandId == 304));

        bool isNight = dayNightCycle != null && dayNightCycle.isNight;
        bool isFoggy = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.currentState == GalicianWeatherState.NeboaMeiga;
        bool isRaining = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining;

        float vis = crouching ? 0.22f : (sprinting ? 0.72f : (speed > 0.4f ? 0.50f : 0.36f));
        if (isNight) vis -= 0.16f;
        if (isFoggy) vis -= 0.20f;
        else if (isRaining) vis -= 0.10f;
        if (hasTorchOrLantern) vis += 0.38f;

        Visibility01 = Mathf.Clamp01(vis);

        float moveNoise = 0.06f;
        if (speed > 0.4f)
        {
            moveNoise = crouching ? 0.18f : (sprinting ? 0.72f : 0.38f);
        }
        if (isRaining) moveNoise *= 0.76f;

        Noise01 = Mathf.Clamp01(Mathf.Max(moveNoise, pulseNoise01));
    }
}
