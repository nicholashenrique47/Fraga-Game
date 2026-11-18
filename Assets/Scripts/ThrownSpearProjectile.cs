using UnityEngine;

/// <summary>
/// ThrownSpearProjectile: Projetil balistico da Lanca Celta (ID 308).
/// Causa alto dano de perfuracao em Vultos, Fauna e alvos fisicos e fica cravada para recuperacao via [E]/Interagir.
/// </summary>
public class ThrownSpearProjectile : MonoBehaviour
{
    public float damage = 60f;
    public float gravityMultiplier = 1.05f;
    public int spearItemId = 308;

    private Transform throwerRoot;
    private Rigidbody rb;
    private Collider mainCol;
    private bool isThrown = false;
    private bool hasImpacted = false;
    private float lifeTimer = 0f;

    public bool CanBePickedUp => hasImpacted;

    public void Launch(Vector3 initialVelocity, float spearDamage, Transform thrower)
    {
        damage = spearDamage;
        throwerRoot = thrower;
        isThrown = true;
        hasImpacted = false;

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.85f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = initialVelocity;

        mainCol = GetComponent<Collider>();
        if (mainCol != null && throwerRoot != null)
        {
            foreach (Collider c in throwerRoot.GetComponentsInChildren<Collider>(true))
            {
                Physics.IgnoreCollision(mainCol, c, true);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isThrown || hasImpacted) return;

        lifeTimer += Time.fixedDeltaTime;
        if (lifeTimer > 45f)
        {
            Destroy(gameObject);
            return;
        }

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.5f)
        {
            Vector3 vel = rb.linearVelocity;
            float stepDist = vel.magnitude * Time.fixedDeltaTime * 1.45f;
            Ray sweepRay = new Ray(transform.position, vel.normalized);

            if (VultoManager.Instance != null &&
                VultoManager.Instance.TryDamageVultoInRay(sweepRay, stepDist, 0.52f, damage, sweepRay.direction, out Vector3 vHit, out bool vKilled))
            {
                StickOnLivingTarget(vHit, -sweepRay.direction, vKilled, isShadow: true);
                return;
            }

            if (FaunaManager.Instance != null &&
                FaunaManager.Instance.TryDamageFaunaInRay(sweepRay, stepDist, 0.45f, damage, out Vector3 fHit, out bool fKilled))
            {
                StickOnLivingTarget(fHit, -sweepRay.direction, fKilled, isShadow: false);
                return;
            }

            if (BossSystem.Instance != null && BossSystem.Instance.IsActive &&
                BossSystem.Instance.TryDamageInRay(sweepRay, stepDist, 0.95f, damage, out Vector3 bHit, out bool bKilled))
            {
                StickOnLivingTarget(bHit, -sweepRay.direction, bKilled, isShadow: true);
                return;
            }

            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
        }
    }

    private void StickOnLivingTarget(Vector3 point, Vector3 normal, bool killed, bool isShadow)
    {
        hasImpacted = true;
        isThrown = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (mainCol != null) mainCol.isTrigger = true;

        float gy = TerrainMeshGenerator.GetTerrainHeight(point.x, point.z);
        transform.position = new Vector3(point.x, gy + 0.35f, point.z);
        transform.rotation = Quaternion.Euler(48f, transform.eulerAngles.y, 0f);

        SphereCollider sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.9f;

        if (VFXPoolManager.Instance != null)
        {
            if (isShadow) VFXPoolManager.Instance.PlayShadowHitVFX(point);
            else VFXPoolManager.Instance.PlayBloodSplashVFX(point, normal);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(0.08f);

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: true, isKill: killed);
            if (killed)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip(isShadow
                    ? "<color=#BB88FF>Vulto empalado pela Lanca Celta!</color>"
                    : "<color=#55FF88>Presa abatida com Lanca!</color>");
            }
        }

        Destroy(gameObject, 120f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;
        if (collision.collider == null) return;
        if (throwerRoot != null && (collision.collider.transform == throwerRoot || collision.collider.transform.IsChildOf(throwerRoot)))
            return;

        Vector3 point = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        Vector3 normal = collision.contacts.Length > 0 ? collision.contacts[0].normal : -transform.forward;
        HandleImpact(collision.collider, point, normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted || other == null || other.isTrigger) return;
        if (throwerRoot != null && (other.transform == throwerRoot || other.transform.IsChildOf(throwerRoot)))
            return;

        HandleImpact(other, transform.position, -transform.forward);
    }

    private void HandleImpact(Collider targetCol, Vector3 point, Vector3 normal)
    {
        if (hasImpacted) return;
        hasImpacted = true;
        isThrown = false;

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
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: true, isKill: faunaKilled || (hitBoss && !boss.IsActive));
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (mainCol != null)
        {
            mainCol.isTrigger = true;
        }

        transform.position = point - transform.forward * 0.18f;
        if (targetCol != null && !targetCol.gameObject.name.ToLower().Contains("terrain"))
        {
            transform.SetParent(targetCol.transform, true);
        }

        SphereCollider sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.85f;

        if (VFXPoolManager.Instance != null)
        {
            if (hitShadow) VFXPoolManager.Instance.PlayShadowHitVFX(point);
            else if (hitFauna) VFXPoolManager.Instance.PlayBloodSplashVFX(point, normal);
            else VFXPoolManager.Instance.PlayBuildPlaceVFX(point);
        }

        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.06f);
        }

        Destroy(gameObject, 120f);
    }

    public void Collect(InventoryAdvanced inv)
    {
        if (!CanBePickedUp) return;

        if (inv == null) inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null)
        {
            inv.AddItem(spearItemId, 1);
            if (inv.equippedMainHandId <= 0)
            {
                inv.EquipItem(spearItemId);
            }
        }

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip("+1 Lanca Celta recuperada");
        }

        Destroy(gameObject);
    }

    public static ThrownSpearProjectile Spawn(Vector3 position, Vector3 direction, float speed, float damageAmount, Transform thrower)
    {
        GameObject root = new GameObject("Thrown_Celtic_Spear");
        root.transform.position = position;
        root.transform.rotation = Quaternion.LookRotation(direction.normalized);

        Material woodMat = Resources.Load<Material>("Materials/Mat_Wood_Carved")
            ?? Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        Material steelMat = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        Material leatherMat = Resources.Load<Material>("Materials/Mat_Leather_Wrap");

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(shaft.GetComponent<Collider>());
        shaft.name = "Spear_Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localScale = new Vector3(0.036f, 0.72f, 0.036f);
        if (woodMat != null) shaft.GetComponent<MeshRenderer>().sharedMaterial = woodMat;

        GameObject wrap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(wrap.GetComponent<Collider>());
        wrap.name = "Spear_Binding";
        wrap.transform.SetParent(root.transform, false);
        wrap.transform.localPosition = new Vector3(0f, 0f, 0.56f);
        wrap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        wrap.transform.localScale = new Vector3(0.044f, 0.09f, 0.044f);
        if (leatherMat != null) wrap.GetComponent<MeshRenderer>().sharedMaterial = leatherMat;

        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(tip.GetComponent<Collider>());
        tip.name = "Spear_Head";
        tip.transform.SetParent(root.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.78f);
        tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        tip.transform.localScale = new Vector3(0.055f, 0.018f, 0.24f);
        if (steelMat != null) tip.GetComponent<MeshRenderer>().sharedMaterial = steelMat;

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.radius = 0.045f;
        col.height = 1.55f;

        ThrownSpearProjectile proj = root.AddComponent<ThrownSpearProjectile>();
        proj.Launch(direction.normalized * speed, damageAmount, thrower);
        return proj;
    }
}
