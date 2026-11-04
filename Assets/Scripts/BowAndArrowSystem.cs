using UnityEngine;

/// <summary>
/// BowAndArrowSystem: Sistema balístico de arco curto artesanal de teixo.
/// Suporta mira orgânica diegética, retesamento com consumo de fôlego e tiro balístico parabólico.
/// </summary>
public class BowAndArrowSystem : MonoBehaviour
{
    public static BowAndArrowSystem Instance { get; private set; }

    [Header("Bow Settings")]
    public float maxDrawTime = 1.35f;
    public float minArrowSpeed = 15f;
    public float maxArrowSpeed = 52f;
    public float baseDamage = 45f;
    public float staminaDrainPerSecond = 14f;

    [Header("Dependencies")]
    public Camera playerCamera;
    public InventoryAdvanced inventory;
    public PlayerStatus playerStatus;

    private bool isAiming = false;
    private float drawProgress = 0f;
    private AudioSource audioSource;
    private AudioClip drawClip;
    private AudioClip releaseClip;
    private AudioClip dryFireClip;

    private GameObject bowVisual;
    private GameObject arrowVisual;

    private void Awake()
    {
        Instance = this;
        SetupAudio();
    }

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();

        CreateProceduralBowVisual();
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

    private void CreateProceduralBowVisual()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        // Cria objeto do arco rústico de teixo
        bowVisual = new GameObject("Procedural_Bow");
        bowVisual.transform.SetParent(playerCamera.transform, false);
        bowVisual.transform.localPosition = new Vector3(0.28f, -0.22f, 0.48f);
        bowVisual.transform.localRotation = Quaternion.Euler(15f, -8f, -12f);

        // Haste de madeira do arco (cilindro curvo / esticado)
        GameObject stave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(stave.GetComponent<Collider>());
        stave.name = "Bow_Stave";
        stave.transform.SetParent(bowVisual.transform, false);
        stave.transform.localPosition = Vector3.zero;
        stave.transform.localScale = new Vector3(0.045f, 0.52f, 0.045f);
        stave.transform.localRotation = Quaternion.Euler(0, 0, 75f);

        Material woodMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        if (woodMat == null) woodMat = Resources.Load<Material>("Materials/Wood_Bark");
        if (woodMat != null) stave.GetComponent<MeshRenderer>().sharedMaterial = woodMat;

        // Flecha nockada (visível apenas ao retesar)
        arrowVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(arrowVisual.GetComponent<Collider>());
        arrowVisual.name = "Bow_NockedArrow";
        arrowVisual.transform.SetParent(bowVisual.transform, false);
        arrowVisual.transform.localPosition = new Vector3(0, 0, 0.25f);
        arrowVisual.transform.localScale = new Vector3(0.02f, 0.45f, 0.02f);
        arrowVisual.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        if (woodMat != null) arrowVisual.GetComponent<MeshRenderer>().sharedMaterial = woodMat;

        arrowVisual.SetActive(false);
        bowVisual.SetActive(false); // Ativa quando o arco estiver selecionado
    }

    private void Update()
    {
        // Verifica se o jogador possui o arco no inventário (ID 310)
        bool hasBow = inventory != null && (inventory.HasItem(310) || inventory.HasItem(300));
        if (bowVisual != null) bowVisual.SetActive(hasBow);

        if (!hasBow) return;

        // Entrada PC: Botão Direito do Mouse mira/retesa, soltar atira
        if (Input.GetMouseButtonDown(1))
        {
            StartAiming();
        }

        if (isAiming)
        {
            if (Input.GetMouseButton(1))
            {
                UpdateAiming();
            }
            else if (Input.GetMouseButtonUp(1))
            {
                ReleaseArrow();
            }
        }
    }

    public void StartAiming()
    {
        if (inventory != null && !inventory.HasItem(320))
        {
            if (audioSource != null && dryFireClip != null)
                audioSource.PlayOneShot(dryFireClip, 0.6f);

            HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
            if (hud != null) hud.ShowLootTooltip("Sem flechas na mochila! (Fabricar Flecha ID 320)");
            return;
        }

        isAiming = true;
        drawProgress = 0f;
        if (arrowVisual != null) arrowVisual.SetActive(true);

        if (audioSource != null && drawClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.05f);
            audioSource.PlayOneShot(drawClip, 0.7f);
        }
    }

    private void UpdateAiming()
    {
        drawProgress = Mathf.Min(drawProgress + Time.deltaTime / maxDrawTime, 1.0f);

        // Recua a flecha visual conforme a corda estica
        if (arrowVisual != null)
        {
            arrowVisual.transform.localPosition = new Vector3(0, 0, 0.25f - drawProgress * 0.16f);
        }

        // Drena estamina do jogador
        if (playerStatus != null)
        {
            playerStatus.stamina = Mathf.Max(0, playerStatus.stamina - staminaDrainPerSecond * Time.deltaTime);
            if (playerStatus.stamina <= 0f)
            {
                // Fadiga extrema força o disparo fraco
                ReleaseArrow();
            }
        }
    }

    public void ReleaseArrow()
    {
        if (!isAiming) return;
        isAiming = false;

        if (arrowVisual != null) arrowVisual.SetActive(false);

        if (drawProgress < 0.15f) return; // Cancelamento suave

        // Consome 1 flecha
        if (inventory != null)
        {
            inventory.RemoveItem(320, 1);
        }

        if (audioSource != null && releaseClip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.08f);
            audioSource.PlayOneShot(releaseClip, 0.9f);
        }

        // Balística da flecha
        if (playerCamera == null) playerCamera = Camera.main;
        Vector3 forward = playerCamera.transform.forward;
        Vector3 spawnPos = playerCamera.transform.position + forward * 0.65f + playerCamera.transform.right * 0.15f - playerCamera.transform.up * 0.12f;

        GameObject arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowObj.name = "Fired_Arrow";
        arrowObj.transform.position = spawnPos;
        arrowObj.transform.localScale = new Vector3(0.025f, 0.45f, 0.025f);
        arrowObj.transform.rotation = Quaternion.LookRotation(forward);

        Material woodMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        if (woodMat != null) arrowObj.GetComponent<MeshRenderer>().sharedMaterial = woodMat;

        Rigidbody rb = arrowObj.AddComponent<Rigidbody>();
        rb.mass = 0.08f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ArrowProjectile arrowProj = arrowObj.AddComponent<ArrowProjectile>();
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, drawProgress);
        float damage = baseDamage * Mathf.Lerp(0.5f, 1.4f, drawProgress);
        arrowProj.Launch(forward * speed, damage);

        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.05f * drawProgress);
        }
    }

    // =================== SÍNTESE PROCEDURAL DE ÁUDIO ===================

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