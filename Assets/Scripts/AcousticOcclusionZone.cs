using UnityEngine;

/// <summary>
/// AcousticOcclusionZone: Oclusão Acústica Dinâmica com Filtro Passa-Baixa (780 Hz) para Interiores e Cavernas.
/// </summary>
public class AcousticOcclusionZone : MonoBehaviour
{
    public static AcousticOcclusionZone Instance { get; private set; }

    [Header("Occlusion Cutoffs")]
    public float indoorCutoffFrequency = 780f;
    public float outdoorCutoffFrequency = 22000f;
    public float transitionSpeed = 3.5f;

    private AudioLowPassFilter lowPassFilter;
    private bool isIndoors = false;

    private void Awake()
    {
        Instance = this;

        // Procura ou adiciona o AudioLowPassFilter no AudioListener principal
        AudioListener listener = FindAnyObjectByType<AudioListener>();
        if (listener != null)
        {
            lowPassFilter = listener.GetComponent<AudioLowPassFilter>();
            if (lowPassFilter == null)
            {
                lowPassFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
            }
            lowPassFilter.cutoffFrequency = outdoorCutoffFrequency;
        }
    }

    private void Update()
    {
        // Checagem de teto sólido acima do jogador (Raycast Zenital para cima)
        Camera cam = Camera.main;
        if (cam != null)
        {
            // Dispara raio de 4.0 metros para o alto procurando telhados ou tetos de pedra
            if (Physics.Raycast(cam.transform.position, Vector3.up, out RaycastHit hit, 4.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                isIndoors = true;
            }
            else
            {
                isIndoors = false;
            }
        }

        if (lowPassFilter != null)
        {
            float targetFreq = isIndoors ? indoorCutoffFrequency : outdoorCutoffFrequency;
            lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetFreq, Time.deltaTime * transitionSpeed);
        }
    }
}