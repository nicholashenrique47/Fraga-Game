using UnityEngine;

/// <summary>
/// CelticSlingSystem: Funda Celta artesanal de couro para arremesso de seixos e pedras de rio.
/// Arma de longo alcance econômica e silenciosa para caça e combate na Galícia ancestral.
/// </summary>
public class CelticSlingSystem : MonoBehaviour
{
    public static CelticSlingSystem Instance { get; private set; }

    [Header("Sling Settings")]
    public float maxWindupTime = 0.90f;
    public float launchSpeed = 34f;
    public int bluntDamage = 30;

    [Header("Dependencies")]
    public Camera playerCamera;
    public InventoryAdvanced inventory;

    private bool isWindingUp = false;
    private float windupTimer = 0f;
    private AudioSource audioSource;
    private AudioClip windupClip;
    private AudioClip releaseClip;

    private void Awake()
    {
        Instance = this;
        SetupAudio();
    }

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.25f;
        audioSource.volume = 0.8f;

        windupClip = GenerateSlingWindupSound();
        releaseClip = GenerateSlingReleaseSound();
    }

    public void StartWindup()
    {
        if (inventory != null && !inventory.HasItem(101)) // 101 = Pedra
        {
            HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
            if (hud != null) hud.ShowLootTooltip("Sem pedras para a funda! (Coletar Pedra ID 101)");
            return;
        }

        isWindingUp = true;
        windupTimer = 0f;

        if (audioSource != null && windupClip != null)
        {
            audioSource.clip = windupClip;
            audioSource.loop = true;
            audioSource.pitch = 0.85f;
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (isWindingUp)
        {
            windupTimer += Time.deltaTime;
            if (audioSource != null && audioSource.isPlaying)
            {
                // Aumenta o tom e intensidade conforme gira mais rápido
                audioSource.pitch = Mathf.Lerp(0.85f, 1.45f, windupTimer / maxWindupTime);
            }
        }
    }

    public void ReleaseSling()
    {
        if (!isWindingUp) return;
        isWindingUp = false;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.pitch = 1.0f;
            if (releaseClip != null) audioSource.PlayOneShot(releaseClip, 0.9f);
        }

        if (windupTimer < 0.25f) return; // Cancelado

        if (inventory != null)
        {
            inventory.RemoveItem(101, 1); // Consome 1 pedra
        }

        if (playerCamera == null) playerCamera = Camera.main;
        Vector3 forward = playerCamera.transform.forward;
        Vector3 spawnPos = playerCamera.transform.position + forward * 0.6f;

        GameObject stoneObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        stoneObj.name = "Sling_Stone";
        stoneObj.transform.position = spawnPos;
        stoneObj.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);

        Material rockMat = Resources.Load<Material>("Materials/Ruin_Stones");
        if (rockMat != null) stoneObj.GetComponent<MeshRenderer>().sharedMaterial = rockMat;

        Rigidbody rb = stoneObj.AddComponent<Rigidbody>();
        rb.mass = 0.35f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        float chargeRatio = Mathf.Clamp01(windupTimer / maxWindupTime);
        float speed = Mathf.Lerp(16f, launchSpeed, chargeRatio);
        rb.linearVelocity = forward * speed;

        SlingStoneProjectile proj = stoneObj.AddComponent<SlingStoneProjectile>();
        proj.damage = Mathf.RoundToInt(bluntDamage * Mathf.Lerp(0.6f, 1.25f, chargeRatio));

        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.04f * chargeRatio);
        }
    }

    // =================== SÍNTESE PROCEDURAL DE ÁUDIO ===================

    private AudioClip GenerateSlingWindupSound()
    {
        int sampleRate = 44100;
        float dur = 0.50f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float whir = Mathf.Sin(2f * Mathf.PI * (180f + Mathf.Sin(t * Mathf.PI * 4f) * 40f) * ((float)i / sampleRate)) * 0.45f;
            samples[i] = whir;
        }

        AudioClip clip = AudioClip.Create("SlingWindup_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateSlingReleaseSound()
    {
        int sampleRate = 44100;
        float dur = 0.18f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float snap = Mathf.Sin(2f * Mathf.PI * 480f * ((float)i / sampleRate)) * Mathf.Exp(-20f * t) * 0.85f;
            samples[i] = snap;
        }

        AudioClip clip = AudioClip.Create("SlingRelease_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

public class SlingStoneProjectile : MonoBehaviour
{
    public int damage = 30;
    private bool hasCollided = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasCollided) return;
        hasCollided = true;

        if (collision.collider != null)
        {
            collision.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }

        Destroy(gameObject, 15f);
    }
}