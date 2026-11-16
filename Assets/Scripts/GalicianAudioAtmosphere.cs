using UnityEngine;

/// <summary>
/// Controlador de Acustica Atmosferica e Oclusao de Copas (Canopy Occlusion).
/// Filtra dinamicamente as frequencias sonoras com Low-Pass Filter quando o jogador
/// esta sob carvalhos densos, dentro de um horreo ou em cavernas de pedra, e adapta vento/chuva por regiao.
/// </summary>
public class GalicianAudioAtmosphere : MonoBehaviour
{
    public static GalicianAudioAtmosphere Instance { get; private set; }

    [Header("Fontes de Audio")]
    public AudioSource windSource;
    public AudioSource rainSource;
    public AudioLowPassFilter lowPassFilter;

    [Header("Deteccao de Copas e Abrigo")]
    public Transform playerHead;
    public LayerMask canopyLayerMask = ~0;
    public float checkDistance = 15.0f;
    public float openAirCutoff = 18000.0f;
    public float shelteredCutoff = 2800.0f;
    public float filterLerpSpeed = 3.5f;

    [Header("Status em Tempo Real")]
    public bool isSheltered = false;

    private float nextSenseTime = 0f;
    private FragaWorldBlueprint.WorldRegion cachedRegion = FragaWorldBlueprint.WorldRegion.OakForest;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if (playerHead == null)
        {
            Camera cam = Camera.main;
            if (cam != null) playerHead = cam.transform;
            else playerHead = transform;
        }

        if (lowPassFilter == null)
        {
            lowPassFilter = GetComponent<AudioLowPassFilter>();
            if (lowPassFilter == null && Camera.main != null)
            {
                lowPassFilter = Camera.main.GetComponent<AudioLowPassFilter>();
                if (lowPassFilter == null)
                {
                    lowPassFilter = Camera.main.gameObject.AddComponent<AudioLowPassFilter>();
                }
            }
        }
    }

    void Update()
    {
        if (playerHead == null) return;

        if (Time.time >= nextSenseTime)
        {
            nextSenseTime = Time.time + 0.14f;
            isSheltered = Physics.Raycast(playerHead.position, Vector3.up, out _, checkDistance, canopyLayerMask);
            cachedRegion = FragaWorldBlueprint.GetRegion(playerHead.position.x, playerHead.position.z);
            if (cachedRegion == FragaWorldBlueprint.WorldRegion.SacredCave)
            {
                isSheltered = true;
            }
        }

        if (lowPassFilter != null)
        {
            float targetFreq = isSheltered
                ? (cachedRegion == FragaWorldBlueprint.WorldRegion.SacredCave ? shelteredCutoff * 0.72f : shelteredCutoff)
                : openAirCutoff;
            lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetFreq, Time.deltaTime * filterLerpSpeed);
        }

        bool exposedCoastOrPeak = cachedRegion == FragaWorldBlueprint.WorldRegion.AtlanticCoast ||
                                  cachedRegion == FragaWorldBlueprint.WorldRegion.FarolBluff ||
                                  cachedRegion == FragaWorldBlueprint.WorldRegion.MountainSummit;

        if (windSource != null)
        {
            float baseWind = isSheltered ? 0.20f : (exposedCoastOrPeak ? 0.88f : 0.62f);
            float weatherBoost = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.currentState == GalicianWeatherState.Trevoada ? 1.25f : 1.0f;
            windSource.volume = Mathf.Lerp(windSource.volume, Mathf.Clamp01(baseWind * weatherBoost), Time.deltaTime * 2.0f);
            windSource.pitch = Mathf.Lerp(windSource.pitch, exposedCoastOrPeak ? 1.06f : (isSheltered ? 0.86f : 1.0f), Time.deltaTime * 2.0f);
        }

        if (rainSource != null)
        {
            float wet = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining
                ? GalicianWeatherEngine.Instance.globalWetness
                : 0f;
            float shelterScale = isSheltered ? 0.45f : 0.90f;
            rainSource.volume = Mathf.Lerp(rainSource.volume, wet * shelterScale * 0.55f, Time.deltaTime * 2.5f);
        }
    }
}
