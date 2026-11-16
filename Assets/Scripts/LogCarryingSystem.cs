using UnityEngine;

/// <summary>
/// LogCarryingSystem: Transporte fisico de ate 2 toras de madeira nos ombros.
/// Interacao unificada via InteractionDetector; ataque arremessa a tora via ChoppingAndCombatSystem.
/// </summary>
public class LogCarryingSystem : MonoBehaviour
{
    public static LogCarryingSystem Instance { get; private set; }

    [Header("Carrying Settings")]
    public int currentLogs = 0;
    public const int MAX_LOGS = 2;
    public float pickupRadius = 2.4f;
    public float throwForceForward = 450f;
    public float throwForceUp = 80f;

    [Header("Visual Mounts")]
    public Transform cameraTransform;
    private GameObject visualLog1;
    private GameObject visualLog2;

    [Header("Audio")]
    private AudioSource audioSource;
    private AudioClip pickupClip;
    private AudioClip dropClip;
    private AudioClip throwClip;

    private float walkSwayTimer = 0f;

    private void Awake()
    {
        Instance = this;
        SetupAudio();
    }

    private void Start()
    {
        if (cameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null) cameraTransform = cam.transform;
        }

        CreateVisualShoulderLogs();
        UpdateVisualLogMounts();
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.25f;
        audioSource.volume = 0.85f;

        pickupClip = GeneratePickupLogSound();
        dropClip = GenerateDropLogSound();
        throwClip = GenerateThrowLogSound();
    }

    private void CreateVisualShoulderLogs()
    {
        if (cameraTransform == null) return;

        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        if (barkMat == null) barkMat = Resources.Load<Material>("Materials/Wood_Bark");

        visualLog1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualLog1.name = "Shoulder_Log_01";
        Destroy(visualLog1.GetComponent<Collider>());
        visualLog1.transform.SetParent(cameraTransform, false);
        visualLog1.transform.localPosition = new Vector3(0.42f, -0.18f, 0.65f);
        visualLog1.transform.localRotation = Quaternion.Euler(78f, -12f, 18f);
        visualLog1.transform.localScale = new Vector3(0.18f, 0.70f, 0.18f);
        if (barkMat != null) visualLog1.GetComponent<MeshRenderer>().sharedMaterial = barkMat;

        visualLog2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualLog2.name = "Shoulder_Log_02";
        Destroy(visualLog2.GetComponent<Collider>());
        visualLog2.transform.SetParent(cameraTransform, false);
        visualLog2.transform.localPosition = new Vector3(0.40f, 0.08f, 0.62f);
        visualLog2.transform.localRotation = Quaternion.Euler(75f, -10f, 15f);
        visualLog2.transform.localScale = new Vector3(0.17f, 0.68f, 0.17f);
        if (barkMat != null) visualLog2.GetComponent<MeshRenderer>().sharedMaterial = barkMat;
    }

    private void Update()
    {
        if (currentLogs <= 0) return;

        PlayerController pc = PlayerController.Instance;
        if (pc != null && pc.IsMoving && pc.IsGrounded)
        {
            walkSwayTimer += Time.deltaTime * 6.0f;
            float swayY = Mathf.Sin(walkSwayTimer) * 0.018f;
            float swayX = Mathf.Cos(walkSwayTimer * 0.5f) * 0.012f;

            if (visualLog1 != null && visualLog1.activeSelf)
            {
                visualLog1.transform.localPosition = new Vector3(0.42f + swayX, -0.18f + swayY, 0.65f);
            }
            if (visualLog2 != null && visualLog2.activeSelf)
            {
                visualLog2.transform.localPosition = new Vector3(0.40f + swayX, 0.08f + swayY, 0.62f);
            }
        }
    }

    public bool CanPickupLog => currentLogs < MAX_LOGS;

    public bool TryPickupNearbyLog()
    {
        if (currentLogs >= MAX_LOGS) return false;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(origin, pickupRadius);

        foreach (var c in hits)
        {
            if (c == null) continue;
            string objName = c.gameObject.name.ToLower();

            if (objName.Contains("felled_wood_log") || objName.Contains("droppedwood"))
            {
                PickupLog();
                Destroy(c.gameObject);
                return true;
            }

            ResourcePickup pickup = c.GetComponent<ResourcePickup>();
            if (pickup != null && pickup.itemID == 100)
            {
                PickupLog();
                Destroy(c.gameObject);
                return true;
            }
        }

        return false;
    }

    public bool ConsumeOneLog()
    {
        if (currentLogs <= 0) return false;
        currentLogs--;
        UpdateVisualLogMounts();
        return true;
    }

    public void PickupLog()
    {
        if (currentLogs >= MAX_LOGS) return;
        currentLogs++;
        UpdateVisualLogMounts();

        if (audioSource != null && pickupClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(pickupClip, 0.85f);
        }

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Tora apoiada no ombro! ({currentLogs}/{MAX_LOGS})");
    }

    public void DropLog(bool throwForward)
    {
        if (currentLogs <= 0) return;
        currentLogs--;
        UpdateVisualLogMounts();

        Vector3 spawnPos = transform.position + transform.forward * 1.1f + Vector3.up * 0.6f;
        GameObject logObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        logObj.name = "Felled_Wood_Log";
        logObj.transform.position = spawnPos;
        logObj.transform.localScale = new Vector3(0.22f, 0.55f, 0.22f);
        logObj.transform.rotation = Quaternion.Euler(Random.Range(70f, 90f), transform.eulerAngles.y, 0);

        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        if (barkMat != null) logObj.GetComponent<MeshRenderer>().sharedMaterial = barkMat;

        Rigidbody rb = logObj.AddComponent<Rigidbody>();
        rb.mass = 18f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (throwForward)
        {
            Vector3 throwDir = Camera.main != null ? Camera.main.transform.forward : transform.forward;
            Vector3 force = throwDir * throwForceForward + Vector3.up * throwForceUp;
            rb.AddForce(force, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 12f, ForceMode.Impulse);

            if (audioSource != null && throwClip != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(throwClip, 0.9f);
            }
        }
        else
        {
            rb.linearVelocity = transform.forward * 1.5f + Vector3.down * 0.5f;
            if (audioSource != null && dropClip != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(dropClip, 0.8f);
            }
        }

        ResourcePickup pickup = logObj.AddComponent<ResourcePickup>();
        pickup.itemID = 100;
        pickup.amount = 2;

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip(throwForward ? "Tora arremessada!" : "Tora depositada no solo.");
    }

    private void UpdateVisualLogMounts()
    {
        if (visualLog1 != null) visualLog1.SetActive(currentLogs >= 1);
        if (visualLog2 != null) visualLog2.SetActive(currentLogs >= 2);
        if (FirstPersonViewmodelController.Instance != null)
        {
            FirstPersonViewmodelController.Instance.SetCarryingLogs(currentLogs > 0);
        }
    }

    private AudioClip GeneratePickupLogSound()
    {
        int sampleRate = 44100;
        float duration = 0.28f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float env = Mathf.Exp(-14f * t);
            float thud = Mathf.Sin(2f * Mathf.PI * 90f * ((float)i / sampleRate)) * env * 0.7f;
            float rustle = Random.Range(-0.35f, 0.35f) * Mathf.Exp(-6f * t);
            samples[i] = thud + rustle;
        }

        AudioClip clip = AudioClip.Create("PickupLog_Procedural", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateDropLogSound()
    {
        int sampleRate = 44100;
        float duration = 0.35f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float env = Mathf.Exp(-11f * t);
            float impact = Mathf.Sin(2f * Mathf.PI * 65f * ((float)i / sampleRate)) * env * 0.85f;
            float dirt = Random.Range(-0.4f, 0.4f) * env * 0.45f;
            samples[i] = impact + dirt;
        }

        AudioClip clip = AudioClip.Create("DropLog_Procedural", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateThrowLogSound()
    {
        int sampleRate = 44100;
        float duration = 0.30f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float whoosh = Mathf.Sin(t * Mathf.PI) * Random.Range(-0.6f, 0.6f) * 0.65f;
            float grunt = Mathf.Sin(2f * Mathf.PI * 130f * ((float)i / sampleRate)) * Mathf.Exp(-15f * t) * 0.45f;
            samples[i] = whoosh + grunt;
        }

        AudioClip clip = AudioClip.Create("ThrowLog_Procedural", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
