using UnityEngine;

/// <summary>
/// Controlador de Acústica Atmosférica e Oclusão de Copas (Canopy Occlusion).
/// Filtra dinamicamente as frequências sonoras com Low-Pass Filter quando o jogador
/// está sob carvalhos densos, dentro de um hórreo ou em cavernas de pedra.
/// </summary>
public class GalicianAudioAtmosphere : MonoBehaviour
{
    [Header("Fontes de Audio")]
    public AudioSource windSource;
    public AudioSource rainSource;
    public AudioLowPassFilter lowPassFilter;

    [Header("Deteccao de Copas e Abrigo")]
    public Transform playerHead;
    public LayerMask canopyLayerMask = ~0; // Default para qualquer obstaculo superior se nao especificado
    public float checkDistance = 15.0f;
    public float openAirCutoff = 18000.0f;
    public float shelteredCutoff = 2800.0f;
    public float filterLerpSpeed = 3.5f;

    [Header("Status em Tempo Real")]
    public bool isSheltered = false;

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

        // Dispara raycast vertical para cima para checar presenca de folhagem ou teto
        isSheltered = Physics.Raycast(playerHead.position, Vector3.up, out RaycastHit hit, checkDistance, canopyLayerMask);

        if (lowPassFilter != null)
        {
            float targetFreq = isSheltered ? shelteredCutoff : openAirCutoff;
            lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetFreq, Time.deltaTime * filterLerpSpeed);
        }

        // Se estiver sob abrigo, o som do vento diminui e o som de gotejamento de chuva fica mais intimo
        if (windSource != null)
        {
            float targetWindVol = isSheltered ? 0.25f : 0.75f;
            windSource.volume = Mathf.Lerp(windSource.volume, targetWindVol, Time.deltaTime * 2.0f);
        }
    }
}
