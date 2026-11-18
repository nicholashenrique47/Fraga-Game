using UnityEngine;

/// <summary>
/// CelticSlingSystem: Funda Celta artesanal de couro para arremesso de seixos e pedras de rio.
/// Arma de longo alcance economica e silenciosa para caca e combate na Galicia ancestral.
/// </summary>
public class CelticSlingSystem : MonoBehaviour
{
    public static CelticSlingSystem Instance { get; private set; }

    [Header("Sling Settings")]
    public float maxWindupTime = 0.90f;
    public float launchSpeed = 34f;
    public int bluntDamage = 30;
    public float staminaCost = 8f;

    [Header("Dependencies")]
    public Camera playerCamera;
    public InventoryAdvanced inventory;
    public PlayerStatus playerStatus;

    private bool isWindingUp = false;
    private float windupTimer = 0f;
    private AudioSource audioSource;
    private AudioClip windupClip;
    private AudioClip releaseClip;

    public bool IsWindingUp => isWindingUp;
    public float Windup01 => maxWindupTime > 0.01f ? Mathf.Clamp01(windupTimer / maxWindupTime) : 0f;

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
        if (isWindingUp) return;
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (playerStatus == null) playerStatus = PlayerStatus.Instance;

        if (inventory != null && !inventory.HasItem(101)) // 101 = Pedra
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Sem pedras para a funda! (Coletar Pedra)");
            return;
        }

        if (playerStatus != null && !playerStatus.CanSpendStamina(4f))
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip(playerStatus.isExhausted
                    ? "<color=#FF8844>Exausto! Aguarde o folego recuperar.</color>"
                    : "Sem folego para girar a funda!");
            return;
        }

        isWindingUp = true;
        windupTimer = 0f;

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetSlingWindup(true, 0.05f);

        if (audioSource != null && windupClip != null)
        {
            audioSource.clip = windupClip;
            audioSource.loop = true;
            audioSource.pitch = 0.85f;
            audioSource.Play();
        }
    }

    public void CancelWindup()
    {
        if (!isWindingUp) return;
        isWindingUp = false;
        windupTimer = 0f;

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.pitch = 1.0f;
        }

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetSlingWindup(false, 0f);
    }

    private void Update()
    {
        if (isWindingUp)
        {
            windupTimer += Time.deltaTime;
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.pitch = Mathf.Lerp(0.85f, 1.45f, windupTimer / maxWindupTime);
            }
            if (FirstPersonViewmodelController.Instance != null)
            {
                FirstPersonViewmodelController.Instance.SetSlingWindup(true, Windup01);
            }
        }
    }

    public bool ReleaseSling()
    {
        if (!isWindingUp) return false;
        isWindingUp = false;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.pitch = 1.0f;
        }

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetSlingWindup(false, 0f);

        if (windupTimer < 0.22f) return false; // Cancelado

        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (inventory != null && !inventory.RemoveItem(101, 1)) return false;

        if (playerStatus == null) playerStatus = PlayerStatus.Instance;
        if (playerStatus != null)
        {
            playerStatus.stamina = Mathf.Max(0f, playerStatus.stamina - staminaCost);
            if (playerStatus.stamina <= 0.5f) playerStatus.isExhausted = true;
        }

        if (audioSource != null && releaseClip != null)
        {
            audioSource.PlayOneShot(releaseClip, 0.9f);
        }

        if (FragaStealthSystem.Instance != null)
        {
            FragaStealthSystem.Instance.EmitNoisePulse(0.22f);
        }

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return false;

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

        bool isStealthCrit = FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.IsHidden;
        SlingStoneProjectile proj = stoneObj.AddComponent<SlingStoneProjectile>();
        proj.damage = Mathf.RoundToInt(bluntDamage * Mathf.Lerp(0.6f, 1.25f, chargeRatio) * (isStealthCrit ? 1.65f : 1f));
        proj.shooterRoot = transform;

        if (FirstPersonViewmodelController.Instance != null)
        {
            FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(0, chargeRatio >= 0.75f);
        }

        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.04f * chargeRatio);
        }

        return true;
    }

    // =================== SINTESE PROCEDURAL DE AUDIO ===================

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
    public Transform shooterRoot;
    private bool hasCollided = false;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (hasCollided || rb == null || rb.linearVelocity.sqrMagnitude < 1f) return;

        Vector3 vel = rb.linearVelocity;
        float stepDist = vel.magnitude * Time.fixedDeltaTime * 1.4f;
        Ray sweepRay = new Ray(transform.position, vel.normalized);

        if (VultoManager.Instance != null &&
            VultoManager.Instance.TryDamageVultoInRay(sweepRay, stepDist, 0.42f, damage, sweepRay.direction, out Vector3 vHit, out bool vKilled))
        {
            hasCollided = true;
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayShadowHitVFX(vHit);
            if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.TriggerHitMarker(false, vKilled);
            rb.linearVelocity *= 0.25f;
            Destroy(gameObject, 5f);
            return;
        }

        if (FaunaManager.Instance != null &&
            FaunaManager.Instance.TryDamageFaunaInRay(sweepRay, stepDist, 0.36f, damage, out Vector3 fHit, out bool fKilled))
        {
            hasCollided = true;
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBloodSplashVFX(fHit, -sweepRay.direction);
            if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.TriggerHitMarker(false, fKilled);
            rb.linearVelocity *= 0.25f;
            Destroy(gameObject, 5f);
            return;
        }

        if (BossSystem.Instance != null && BossSystem.Instance.IsActive &&
            BossSystem.Instance.TryDamageInRay(sweepRay, stepDist, 0.95f, (float)damage, out Vector3 bHit, out bool bKilled))
        {
            hasCollided = true;
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayShadowHitVFX(bHit);
            if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.TriggerHitMarker(false, bKilled);
            rb.linearVelocity *= 0.25f;
            Destroy(gameObject, 5f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasCollided) return;
        if (collision.collider != null && shooterRoot != null &&
            (collision.collider.transform == shooterRoot || collision.collider.transform.IsChildOf(shooterRoot)))
        {
            return;
        }

        hasCollided = true;
        Vector3 point = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        Vector3 normal = collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector3.up;

        bool hitShadow = VultoManager.Instance != null && VultoManager.Instance.TryDamageVultoAt(point, damage);
        bool faunaKilled = false;
        bool hitFauna = !hitShadow && FaunaManager.Instance != null && FaunaManager.Instance.TryDamageFaunaAt(point, damage, out faunaKilled);
        BossSystem boss = collision.collider != null ? collision.collider.GetComponentInParent<BossSystem>() : null;
        bool hitBoss = boss != null && boss.IsActive;

        if (hitBoss)
        {
            boss.TakeDamage((float)damage);
            hitShadow = true;
        }
        else if (collision.collider != null)
        {
            collision.collider.SendMessageUpwards("TakeDamage", (float)damage, SendMessageOptions.DontRequireReceiver);
        }

        if (hitShadow || hitBoss || hitFauna || (collision.collider != null && collision.collider.GetComponentInParent<WildlifeAIEngine>() != null))
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.TriggerHitMarker(false, faunaKilled || (hitBoss && !boss.IsActive));
        }

        if (VFXPoolManager.Instance != null)
        {
            if (hitShadow) VFXPoolManager.Instance.PlayShadowHitVFX(point);
            else if (hitFauna) VFXPoolManager.Instance.PlayBloodSplashVFX(point, normal);
            else VFXPoolManager.Instance.PlaySparkVFX(point, normal);
        }

        Destroy(gameObject, 12f);
    }
}
