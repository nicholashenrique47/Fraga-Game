using UnityEngine;

/// <summary>
/// SurfaceAudio: Emissor auxiliar de passos que delega ao GalicianFootstepSurfaceDetector
/// sempre que ativo (evitando passos duplicados com PlayerController) e utiliza sintese filtrada
/// em fallback quando usado isoladamente (PC + Mobile via FragaInputRouter).
/// </summary>
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
    public float stepIntervalWalk = 0.48f;
    public float stepIntervalSprint = 0.30f;

    private float stepTimer = 0f;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.6f;
                audioSource.volume = 0.6f;
            }
        }

        if (dirtSteps == null || dirtSteps.Length == 0) dirtSteps = new AudioClip[] { GenerateFilteredStepClip("Dirt", 90f, 0.11f) };
        if (grassSteps == null || grassSteps.Length == 0) grassSteps = new AudioClip[] { GenerateFilteredStepClip("Grass", 140f, 0.13f) };
        if (waterSteps == null || waterSteps.Length == 0) waterSteps = new AudioClip[] { GenerateFilteredStepClip("Water", 260f, 0.20f) };
        if (rockSteps == null || rockSteps.Length == 0) rockSteps = new AudioClip[] { GenerateFilteredStepClip("Rock", 540f, 0.09f) };
    }

    private static AudioClip GenerateFilteredStepClip(string label, float centerFreq, float duration)
    {
        const int sr = 22050;
        int len = Mathf.Max(512, Mathf.RoundToInt(sr * duration));
        float[] samples = new float[len];
        uint seed = (uint)(label.GetHashCode() ^ 0x4F1BBCDC);
        float lp1 = 0f;
        float lp2 = 0f;

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float sec = (float)i / sr;
            seed = seed * 1664525u + 1013904223u;
            float w = ((seed >> 16) / 65535f) * 2f - 1f;
            lp1 = Mathf.Lerp(lp1, w, 0.18f);
            lp2 = Mathf.Lerp(lp2, lp1, 0.07f);
            float band = lp1 - lp2;

            float env = Mathf.Exp(-14f * t);
            float tone = Mathf.Sin(2f * Mathf.PI * centerFreq * sec) * 0.45f;
            samples[i] = Mathf.Clamp((tone + band * 0.55f) * env * 0.55f, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create($"SurfaceStep_{label}", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void Update()
    {
        // Quando o PlayerController + GalicianFootstepSurfaceDetector ja conduzem os passos, evita passos duplos.
        if (GalicianFootstepSurfaceDetector.Instance != null && playerController != null)
            return;

        if (playerTransform == null || playerController == null) return;
        if (!playerController.IsGrounded) return;

        bool isMoving = playerController.IsMoving;
        if (!isMoving && FragaInputRouter.Instance != null)
        {
            isMoving = FragaInputRouter.Instance.MoveInput.sqrMagnitude > 0.01f;
        }

        if (isMoving)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                PlaySurfaceStep();
                stepTimer = playerController.isSprinting ? stepIntervalSprint : stepIntervalWalk;
            }
        }
        else
        {
            stepTimer = 0.08f;
        }
    }

    public void PlaySurfaceStep()
    {
        if (GalicianFootstepSurfaceDetector.Instance != null && playerTransform != null)
        {
            bool sprinting = playerController != null && playerController.isSprinting;
            bool crouching = playerController != null && playerController.isCrouching;
            GalicianFootstepSurfaceDetector.Instance.PlayFootstep(playerTransform.position, sprinting, crouching);
            return;
        }

        if (audioSource == null || playerTransform == null) return;

        AudioClip[] clipsToPlay = dirtSteps;
        if (resourceManager != null && resourceManager.MapDataPtr != System.IntPtr.Zero)
        {
            int gridX = Mathf.RoundToInt(playerTransform.position.x);
            int gridY = Mathf.RoundToInt(playerTransform.position.z);
            int tileID = ResourceManager.GetMapCell(resourceManager.MapDataPtr, resourceManager.MapWidth, resourceManager.MapHeight, gridX, gridY);

            if (tileID == 1) clipsToPlay = grassSteps;
            else if (tileID == 7) clipsToPlay = waterSteps;
            else if (tileID == 8 || tileID == 2) clipsToPlay = rockSteps;
        }

        if (clipsToPlay != null && clipsToPlay.Length > 0)
        {
            AudioClip clip = clipsToPlay[Random.Range(0, clipsToPlay.Length)];
            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.91f, 1.09f);
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
