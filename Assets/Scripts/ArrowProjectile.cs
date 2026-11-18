using UnityEngine;

/// <summary>
/// ArrowProjectile: Balistica realista de flecha com gravidade, varredura de acerto em Vultos/Fauna,
/// VFX por material e recuperacao correta (ID 110 ou 117).
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    [Header("Ballistics")]
    public float damage = 45f;
    public float gravityMultiplier = 1.15f;
    public int pickupItemId = 110;
    public bool isFired = false;
    public Vector3 velocity;

    private Transform shooterRoot;
    private bool hasImpacted = false;
    private Rigidbody rb;
    private Collider col;
    private float lifeTime = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void Launch(Vector3 initialVelocity, float arrowDamage, int recoveredItemId = 110, Transform shooter = null)
    {
        velocity = initialVelocity;
        damage = arrowDamage;
        pickupItemId = recoveredItemId;
        shooterRoot = shooter;
        isFired = true;

        if (col != null && shooterRoot != null)
        {
            foreach (Collider c in shooterRoot.GetComponentsInChildren<Collider>(true))
            {
                Physics.IgnoreCollision(col, c, true);
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = initialVelocity;
            rb.useGravity = true;
        }
    }

    private void FixedUpdate()
    {
        if (!isFired || hasImpacted) return;

        lifeTime += Time.fixedDeltaTime;
        if (lifeTime > 25f)
        {
            Destroy(gameObject);
            return;
        }

        if (rb != null && rb.linearVelocity.sqrMagnitude > 1f)
        {
            Vector3 vel = rb.linearVelocity;
            float stepDist = vel.magnitude * Time.fixedDeltaTime * 1.4f;
            Ray sweepRay = new Ray(transform.position, vel.normalized);

            if (VultoManager.Instance != null &&
                VultoManager.Instance.TryDamageVultoInRay(sweepRay, stepDist, 0.42f, damage, sweepRay.direction, out Vector3 vultoHit, out bool vultoKilled))
            {
                CompleteLivingHit(vultoHit, -sweepRay.direction, vultoKilled, isShadow: true);
                return;
            }

            if (FaunaManager.Instance != null &&
                FaunaManager.Instance.TryDamageFaunaInRay(sweepRay, stepDist, 0.36f, damage, out Vector3 faunaHit, out bool faunaKilled))
            {
                CompleteLivingHit(faunaHit, -sweepRay.direction, faunaKilled, isShadow: false);
                return;
            }

            if (BossSystem.Instance != null && BossSystem.Instance.IsActive &&
                BossSystem.Instance.TryDamageInRay(sweepRay, stepDist, 0.95f, damage, out Vector3 bossHit, out bool bossKilled))
            {
                CompleteLivingHit(bossHit, -sweepRay.direction, bossKilled, isShadow: true);
                return;
            }

            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }

    private void CompleteLivingHit(Vector3 hitPos, Vector3 normal, bool killed, bool isShadow)
    {
        hasImpacted = true;
        isFired = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        if (col != null) col.enabled = false;

        transform.position = hitPos;
        PlayImpactAudio();

        if (VFXPoolManager.Instance != null)
        {
            if (isShadow) VFXPoolManager.Instance.PlayShadowHitVFX(hitPos);
            else VFXPoolManager.Instance.PlayBloodSplashVFX(hitPos, normal);
        }

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: damage >= 50f, isKill: killed);
            if (killed)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip(isShadow
                    ? "<color=#BB88FF>Vulto abatido com flecha!</color>"
                    : "<color=#55FF88>Presa abatida com flecha!</color>");
            }
        }

        CreateArrowPickupArea(hitPos);
        Destroy(gameObject, 45f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;
        if (collision.collider == null) return;
        if (shooterRoot != null && (collision.collider.transform == shooterRoot || collision.collider.transform.IsChildOf(shooterRoot)))
            return;

        Vector3 point = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        Vector3 normal = collision.contacts.Length > 0 ? collision.contacts[0].normal : -transform.forward;
        HandleImpact(collision.collider, point, normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted || other == null || other.isTrigger) return;
        if (shooterRoot != null && (other.transform == shooterRoot || other.transform.IsChildOf(shooterRoot)))
            return;

        HandleImpact(other, transform.position, -transform.forward);
    }

    private void HandleImpact(Collider targetCol, Vector3 point, Vector3 normal)
    {
        if (hasImpacted) return;
        hasImpacted = true;
        isFired = false;

        bool hitShadow = VultoManager.Instance != null && VultoManager.Instance.TryDamageVultoAt(point, damage);
        bool faunaKilled = false;
        bool hitFauna = !hitShadow && FaunaManager.Instance != null && FaunaManager.Instance.TryDamageFaunaAt(point, damage, out faunaKilled);
        BossSystem boss = targetCol != null ? targetCol.GetComponentInParent<BossSystem>() : null;
        bool hitBoss = boss != null && boss.IsActive;

        if (hitBoss)
        {
            boss.TakeDamage(damage);
            hitShadow = true;
        }
        else if (targetCol != null)
        {
            targetCol.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }

        if (hitShadow || hitBoss || hitFauna || (targetCol != null && targetCol.GetComponentInParent<WildlifeAIEngine>() != null))
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: damage >= 50f, isKill: faunaKilled || (hitBoss && !boss.IsActive));
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        if (col != null) col.enabled = false;

        transform.position = point - transform.forward * 0.12f;
        if (targetCol != null && !targetCol.gameObject.name.ToLower().Contains("terrain"))
        {
            transform.SetParent(targetCol.transform, true);
        }

        PlayImpactAudio();
        SpawnImpactVFX(targetCol, point, normal, hitShadow, hitFauna);
        CreateArrowPickupArea(point);

        Destroy(gameObject, 60f);
    }

    private void CreateArrowPickupArea(Vector3 point)
    {
        GameObject pickupArea = new GameObject("Arrow_Pickup");
        pickupArea.transform.position = point;
        SphereCollider sc = pickupArea.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.0f;
        ResourcePickup pickup = pickupArea.AddComponent<ResourcePickup>();
        pickup.itemID = pickupItemId;
        pickup.amount = 1;
        pickupArea.transform.SetParent(transform, true);
    }

    private static AudioClip s_fallbackImpactClip;

    private void PlayImpactAudio()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.ArrowImpact, transform.position, 0.78f, 0.90f, 1.12f, 32f);
            return;
        }

        AudioSource audio = gameObject.AddComponent<AudioSource>();
        audio.spatialBlend = 0.85f;
        audio.minDistance = 2f;
        audio.maxDistance = 35f;

        if (s_fallbackImpactClip == null)
        {
            int sampleRate = 22050;
            int count = (int)(sampleRate * 0.18f);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                samples[i] = Mathf.Sin(2f * Mathf.PI * 200f * ((float)i / sampleRate)) * Mathf.Exp(-22f * t) * 0.85f;
            }
            s_fallbackImpactClip = AudioClip.Create("ArrowHit_Procedural", count, 1, sampleRate, false);
            s_fallbackImpactClip.SetData(samples, 0);
        }
        audio.pitch = Random.Range(0.9f, 1.1f);
        audio.PlayOneShot(s_fallbackImpactClip, 0.75f);
    }

    private void SpawnImpactVFX(Collider targetCol, Vector3 point, Vector3 normal, bool isShadow, bool isFauna)
    {
        if (VFXPoolManager.Instance != null)
        {
            if (isShadow)
            {
                VFXPoolManager.Instance.PlayShadowHitVFX(point);
                return;
            }
            if (isFauna || (targetCol != null && targetCol.GetComponentInParent<WildlifeAIEngine>() != null))
            {
                VFXPoolManager.Instance.PlayBloodSplashVFX(point, normal);
                return;
            }

            if (targetCol != null)
            {
                string n = targetCol.gameObject.name.ToLower();
                if (n.Contains("rock") || n.Contains("stone") || n.Contains("ruin"))
                {
                    VFXPoolManager.Instance.PlaySparkVFX(point, normal);
                    return;
                }
                if (n.Contains("tree") || n.Contains("trunk") || n.Contains("wood"))
                {
                    VFXPoolManager.Instance.PlayWoodChipVFX(point, normal);
                    return;
                }
            }

            VFXPoolManager.Instance.PlayBuildPlaceVFX(point);
            return;
        }

        GameObject vfxObj = new GameObject("ArrowImpactVFX");
        vfxObj.transform.position = point;
        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        var m = ps.main;
        m.duration = 0.25f;
        m.startLifetime = 0.35f;
        m.startSpeed = 3.0f;
        m.startSize = 0.05f;
        m.startColor = new Color(0.7f, 0.6f, 0.45f);
        m.loop = false;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 25f;
        sh.rotation = Quaternion.LookRotation(normal).eulerAngles;

        Destroy(vfxObj, 0.6f);
    }
}
