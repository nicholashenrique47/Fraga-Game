using UnityEngine;

/// <summary>
/// ArrowProjectile: Balística realista de flecha com gravidade e penetração física em alvos.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    [Header("Ballistics")]
    public float damage = 45f;
    public float gravityMultiplier = 1.15f;
    public bool isFired = false;
    public Vector3 velocity;

    private bool hasImpacted = false;
    private Rigidbody rb;
    private Collider col;
    private float lifeTime = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void Launch(Vector3 initialVelocity, float arrowDamage)
    {
        velocity = initialVelocity;
        damage = arrowDamage;
        isFired = true;

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

        // Aplica gravidade adicional e alinha a ponta com a trajetória
        if (rb != null && rb.linearVelocity.sqrMagnitude > 1f)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;
        HandleImpact(collision.collider, collision.contacts[0].point, collision.contacts[0].normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted || other.isTrigger) return;
        HandleImpact(other, transform.position, -transform.forward);
    }

    private void HandleImpact(Collider targetCol, Vector3 point, Vector3 normal)
    {
        if (hasImpacted) return;
        hasImpacted = true;
        isFired = false;

        // 1. Aplica dano à entidade atingida
        if (targetCol != null)
        {
            targetCol.SendMessageUpwards("TakeDamage", (int)damage, SendMessageOptions.DontRequireReceiver);
        }

        // 2. Trava a flecha no alvo (fixa na madeira, carne ou terreno)
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

        // 3. Toca som de impacto e cria pequenas lascas
        PlayImpactAudio();
        SpawnImpactVFX(point, normal);

        // 4. Torna a flecha recuperável adicionando ResourcePickup (Flecha de Sílex ID 320)
        GameObject pickupArea = new GameObject("Arrow_Pickup");
        pickupArea.transform.position = point;
        SphereCollider sc = pickupArea.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.0f;
        ResourcePickup pickup = pickupArea.AddComponent<ResourcePickup>();
        pickup.itemID = 320;
        pickup.amount = 1;
        pickupArea.transform.SetParent(transform, true);

        // Se passar de 60 segundos, destrói
        Destroy(gameObject, 60f);
    }

    private void PlayImpactAudio()
    {
        AudioSource audio = gameObject.AddComponent<AudioSource>();
        audio.spatialBlend = 0.85f;
        audio.minDistance = 2f;
        audio.maxDistance = 35f;

        // Som sintetizado de flecha cravando na madeira
        int sampleRate = 44100;
        float dur = 0.18f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float thud = Mathf.Sin(2f * Mathf.PI * 180f * ((float)i / sampleRate)) * Mathf.Exp(-22f * t);
            float snap = Random.Range(-0.3f, 0.3f) * Mathf.Exp(-12f * t);
            samples[i] = (thud + snap) * 0.9f;
        }

        AudioClip clip = AudioClip.Create("ArrowHit_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        audio.pitch = Random.Range(0.9f, 1.1f);
        audio.PlayOneShot(clip, 0.75f);
    }

    private void SpawnImpactVFX(Vector3 point, Vector3 normal)
    {
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