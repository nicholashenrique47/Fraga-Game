using UnityEngine;

/// SurfaceAudio: Sistema reativo que emite passos sonoros dependendo do 
/// tipo de chao que o jogador pisa (Lendo o mapa C++ em O(1)).
public class SurfaceAudio : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerTransform;
    public ResourceManager resourceManager;
    public AudioSource audioSource;
    public PlayerController playerController;

    [Header("Audio Clips")]
    public AudioClip[] dirtSteps;
    public AudioClip[] grassSteps;
    public AudioClip[] waterSteps;
    public AudioClip[] rockSteps;

    [Header("Settings")]
    public float stepIntervalWalk = 0.5f;
    public float stepIntervalSprint = 0.3f;
    
    private float stepTimer = 0f;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.volume = 0.6f;
        }

        if (dirtSteps == null || dirtSteps.Length == 0) dirtSteps = new AudioClip[] { GenerateStepClip(0.08f, 1500f) };
        if (grassSteps == null || grassSteps.Length == 0) grassSteps = new AudioClip[] { GenerateStepClip(0.12f, 2000f) };
        if (waterSteps == null || waterSteps.Length == 0) waterSteps = new AudioClip[] { GenerateStepClip(0.2f, 800f) };
        if (rockSteps == null || rockSteps.Length == 0) rockSteps = new AudioClip[] { GenerateStepClip(0.06f, 3000f) };
    }

    private AudioClip GenerateStepClip(float duration, float frequencyCutoff)
    {
        int sampleRate = 44100;
        float[] samples = new float[(int)(sampleRate * duration)];
        
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / samples.Length;
            // Burst de ruido branco mitigado por um envelope exponencial para simular impacto físico
            float noise = Random.Range(-1f, 1f);
            float envelope = Mathf.Exp(-10f * t);
            
            samples[i] = noise * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("SynthStep", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void Update()
    {
        if (playerTransform == null || resourceManager == null || playerController == null) return;

        // Verificar se esta em movimento (simplificado usando o Rigidbody speed ou input)
        float speed = playerController.speed; 
        bool isMoving = Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0;

        if (isMoving)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                PlaySurfaceStep();
                
                // Redefinir o timer baseado em se estamos a fazer sprint (velocidade > 5)
                stepTimer = (speed > 6f) ? stepIntervalSprint : stepIntervalWalk;
            }
        }
        else
        {
            stepTimer = 0.1f; // Reseta rapidamente para o proximo passo inicial
        }
    }

    private void PlaySurfaceStep()
    {
        if (resourceManager.MapDataPtr == System.IntPtr.Zero) return;

        int gridX = Mathf.RoundToInt(playerTransform.position.x);
        int gridY = Mathf.RoundToInt(playerTransform.position.z);

        int tileID = ResourceManager.GetMapCell(resourceManager.MapDataPtr, resourceManager.MapWidth, resourceManager.MapHeight, gridX, gridY);

        AudioClip[] clipsToPlay = dirtSteps; // Base

        if (tileID == 1) clipsToPlay = grassSteps; // Floresta
        else if (tileID == 7) clipsToPlay = waterSteps; // Rio/Agua
        else if (tileID == 8 || tileID == 2) clipsToPlay = rockSteps; // Rocha ou Ruina

        if (clipsToPlay != null && clipsToPlay.Length > 0)
        {
            AudioClip clip = clipsToPlay[Random.Range(0, clipsToPlay.Length)];
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clip);
        }
    }
}
