using UnityEngine;

/// <summary>
/// BowAndArrowSystem: Sistema balistico do Arco de Sobrevivencia (ID 305) com Flechas (110) e Flechas Penadas (117).
/// Integrado ao EquipmentSystem / FragaInputRouter e ao FirstPersonViewmodelController.
/// </summary>
public class BowAndArrowSystem : MonoBehaviour
{
    public static BowAndArrowSystem Instance { get; private set; }

    [Header("Bow Settings")]
    public float maxDrawTime = 1.15f;
    public float minArrowSpeed = 20f;
    public float maxArrowSpeed = 54f;
    public float baseDamage = 55f;
    public float staminaDrainPerSecond = 11f;
    public float bowAimFOV = 46f;

    [Header("Dependencies")]
    public Camera playerCamera;
    public InventoryAdvanced inventory;
    public PlayerStatus playerStatus;

    private bool isAiming = false;
    private float drawProgress = 0f;
    private int activeArrowId = 110;

    private AudioSource audioSource;
    private AudioClip drawClip;
    private AudioClip releaseClip;
    private AudioClip dryFireClip;
    private GameObject arrowPrefabCache;

    public bool IsAiming => isAiming;
    public float DrawProgress => drawProgress;

    private void Awake()
    {
        Instance = this;
        SetupAudio();
    }

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
        arrowPrefabCache = Resources.Load<GameObject>("Prefabs/Arrow_Prefab");
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.25f;
        audioSource.volume = 0.85f;

        drawClip = GenerateBowDrawSound();
        releaseClip = GenerateBowReleaseSound();
        dryFireClip = GenerateDryFireSound();
    }

    public bool HasAnyArrow()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        return inventory != null && (inventory.HasItem(117) || inventory.HasItem(110));
    }

    private int SelectBestArrowId()
    {
        if (inventory == null) return 110;
        if (inventory.HasItem(117)) return 117; // Flecha Penada primeiro (+45% velocidade/dano)
        if (inventory.HasItem(110)) return 110; // Flechas de Pedra
        return -1;
    }

    public void StartAiming()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (playerStatus == null) playerStatus = PlayerStatus.Instance;

        int arrowId = SelectBestArrowId();
        if (arrowId < 0)
        {
            if (audioSource != null && dryFireClip != null)
                audioSource.PlayOneShot(dryFireClip, 0.65f);

            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Sem flechas! Fabrique Flechas (110/117).");
            return;
        }

        activeArrowId = arrowId;
        isAiming = true;
        drawProgress = 0.1f;

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetBowDraw(true, drawProgress);

        if (audioSource != null && drawClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.05f);
            audioSource.PlayOneShot(drawClip, 0.7f);
        }
    }

    public void UpdateAimTick()
    {
        if (!isAiming) return;

        drawProgress = Mathf.Min(drawProgress + Time.deltaTime / maxDrawTime, 1.0f);

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetBowDraw(true, drawProgress);

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.SetAimFOVOverride(Mathf.Lerp(62f, bowAimFOV, drawProgress));

        if (playerStatus != null)
        {
            playerStatus.stamina = Mathf.Max(0f, playerStatus.stamina - staminaDrainPerSecond * Time.deltaTime);
            if (playerStatus.stamina <= 0f)
            {
                ReleaseArrow();
            }
        }
    }

    public void CancelAim()
    {
        if (!isAiming) return;
        isAiming = false;
        drawProgress = 0f;
        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetBowDraw(false, 0f);
        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.SetAimFOVOverride(-1f);
    }

    public void QuickShot()
    {
        int arrowId = SelectBestArrowId();
        if (arrowId < 0)
        {
            if (audioSource != null && dryFireClip != null) audioSource.PlayOneShot(dryFireClip, 0.65f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Sem flechas na mochila!");
            return;
        }

        activeArrowId = arrowId;
        isAiming = true;
        drawProgress = 0.45f;
        ReleaseArrow();
    }

    public void ReleaseArrow()
    {
        if (!isAiming) return;
        isAiming = false;

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetBowDraw(false, 0f);
        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.SetAimFOVOverride(-1f);

        if (drawProgress < 0.14f) return;

        int arrowId = SelectBestArrowId();
        if (arrowId < 0) return;
        activeArrowId = arrowId;

        if (inventory != null)
        {
            inventory.RemoveItem(activeArrowId, 1);
        }

        if (audioSource != null && releaseClip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.08f);
            audioSource.PlayOneShot(releaseClip, 0.9f);
        }

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        Vector3 forward = playerCamera.transform.forward;
        Vector3 spawnPos = playerCamera.transform.position + forward * 0.65f + playerCamera.transform.right * 0.12f - playerCamera.transform.up * 0.08f;

        GameObject arrowObj;
        if (arrowPrefabCache == null) arrowPrefabCache = Resources.Load<GameObject>("Prefabs/Arrow_Prefab");
        if (arrowPrefabCache != null)
        {
            arrowObj = Instantiate(arrowPrefabCache, spawnPos, Quaternion.LookRotation(forward));
            arrowObj.name = "Fired_Arrow";
            if (arrowObj.GetComponent<Collider>() == null)
            {
                CapsuleCollider col = arrowObj.AddComponent<CapsuleCollider>();
                col.direction = 2;
                col.radius = 0.025f;
                col.height = 0.72f;
            }
        }
        else
        {
            arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowObj.name = "Fired_Arrow";
            arrowObj.transform.position = spawnPos;
            arrowObj.transform.localScale = new Vector3(0.025f, 0.45f, 0.025f);
            arrowObj.transform.rotation = Quaternion.LookRotation(forward);

            Material woodMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
            if (woodMat != null) arrowObj.GetComponent<MeshRenderer>().sharedMaterial = woodMat;
        }

        Rigidbody rb = arrowObj.GetComponent<Rigidbody>() ?? arrowObj.AddComponent<Rigidbody>();
        rb.mass = 0.08f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ArrowProjectile arrowProj = arrowObj.GetComponent<ArrowProjectile>() ?? arrowObj.AddComponent<ArrowProjectile>();
        float bonusMult = (activeArrowId == 117) ? 1.45f : 1.0f;
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, drawProgress) * bonusMult;
        float damage = baseDamage * Mathf.Lerp(0.55f, 1.45f, drawProgress) * bonusMult;
        arrowProj.Launch(forward * speed, damage, activeArrowId, transform);

        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.05f * drawProgress);
        }
    }

    private AudioClip GenerateBowDrawSound()
    {
        int sampleRate = 44100;
        float dur = 0.45f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float creak = Mathf.Sin(2f * Mathf.PI * (160f + t * 40f) * ((float)i / sampleRate)) * Mathf.Exp(-3f * (1f - t)) * 0.35f;
            float tension = Random.Range(-0.2f, 0.2f) * t * 0.3f;
            samples[i] = creak + tension;
        }
        AudioClip clip = AudioClip.Create("BowDraw_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateBowReleaseSound()
    {
        int sampleRate = 44100;
        float dur = 0.22f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float snap = Mathf.Sin(2f * Mathf.PI * 340f * ((float)i / sampleRate)) * Mathf.Exp(-18f * t) * 0.8f;
            float whoosh = Random.Range(-0.4f, 0.4f) * Mathf.Exp(-12f * t) * 0.4f;
            samples[i] = snap + whoosh;
        }
        AudioClip clip = AudioClip.Create("BowRelease_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateDryFireSound()
    {
        int sampleRate = 44100;
        float dur = 0.12f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            samples[i] = Random.Range(-0.3f, 0.3f) * Mathf.Exp(-28f * t);
        }
        AudioClip clip = AudioClip.Create("BowDryFire_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
