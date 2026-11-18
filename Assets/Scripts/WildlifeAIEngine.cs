using UnityEngine;
using System.Collections;

/// <summary>
/// WildlifeAIEngine: Inteligencia Artificial sensorial organica para animais selvagens (Lobos, Javalis, Cervos).
/// Suporta:
/// - Cone de visao frontal de 110 graus e audicao de passos (FragaStealthSystem).
/// - Pavor de fogo e tochas (Zero-GC com OverlapSphereNonAlloc + cache a 4 Hz).
/// - Windup telegrafado antes de investida e traumas (Sangramento/Fratura) em ataques nao esquivados.
/// - Sistema de carcaca e drops corrigidos (Carne Crua 104, Pele de Animal 105, Osso 120).
/// </summary>
public class WildlifeAIEngine : MonoBehaviour
{
    public enum CreatureType { IberianWolf, WildBoar, Deer }
    public enum AIState { Idle, Grazing, Stalking, Orbiting, Windup, Charging, FleeingFire, Dead }

    [Header("Creature Configuration")]
    public CreatureType creatureType = CreatureType.IberianWolf;
    public AIState currentState = AIState.Idle;
    public int maxHealth = 75;
    public int currentHealth;
    public float walkSpeed = 2.4f;
    public float runSpeed = 6.5f;
    public float chargeSpeed = 8.5f;

    [Header("Sensory Parameters")]
    public float visionAngle = 110f;
    public float visionDistance = 28f;
    public float fearOfFireDistance = 5.5f;
    public float attackReach = 1.75f;
    public int attackDamage = 22;

    [Header("Dependencies")]
    public Transform playerTransform;

    private float stateTimer = 0f;
    private float orbitAngle = 0f;
    private float nextAttackTime = 0f;
    private float nextFireScanTime = 0f;
    private bool cachedNearFire = false;
    private float hitFlashTimer = 0f;

    private AudioSource audioSource;
    private AudioClip growlClip;
    private AudioClip attackClip;
    private AudioClip deathClip;

    private PlayerStatus playerStatusCache;
    private PlayerController playerControllerCache;
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock flashMpb;
    private static readonly Collider[] s_fireOverlapBuffer = new Collider[16];
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    private void Awake()
    {
        currentHealth = maxHealth;
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        flashMpb = new MaterialPropertyBlock();
        SetupAudio();
    }

    private void Start()
    {
        ResolvePlayerRefs();
        orbitAngle = Random.Range(0f, 360f);
    }

    private void ResolvePlayerRefs()
    {
        if (playerTransform == null)
        {
            if (PlayerController.Instance != null)
            {
                playerTransform = PlayerController.Instance.transform;
            }
            else
            {
                GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player_CameraParent");
                if (player != null) playerTransform = player.transform;
            }
        }
        if (playerTransform != null)
        {
            if (playerStatusCache == null) playerStatusCache = playerTransform.GetComponent<PlayerStatus>() ?? PlayerStatus.Instance;
            if (playerControllerCache == null) playerControllerCache = playerTransform.GetComponent<PlayerController>() ?? PlayerController.Instance;
        }
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.85f;
        audioSource.minDistance = 3f;
        audioSource.maxDistance = 30f;

        growlClip = GenerateGrowlSound();
        attackClip = GenerateAttackSound();
        deathClip = GenerateDeathSound();
    }

    private void Update()
    {
        if (currentState == AIState.Dead) return;
        if (playerTransform == null)
        {
            ResolvePlayerRefs();
            if (playerTransform == null) return;
        }

        UpdateHitFlash();
        stateTimer += Time.deltaTime;

        // 1. Checa proximidade com fogo ou tocha (throttled a 4 Hz, Zero-GC)
        if (Time.time >= nextFireScanTime)
        {
            nextFireScanTime = Time.time + 0.25f;
            cachedNearFire = CheckForFireNear();
        }
        if (cachedNearFire && currentState != AIState.FleeingFire)
        {
            currentState = AIState.FleeingFire;
            stateTimer = 0f;
        }

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case AIState.Idle:
                float stealthMult = FragaStealthSystem.Instance != null ? FragaStealthSystem.Instance.GetDetectionMultiplier() : 1f;
                float effectiveVision = visionDistance * stealthMult;
                bool heardPlayer = FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.Noise01 > 0.68f && distToPlayer < 14f;
                if ((distToPlayer < effectiveVision && CanSeePlayer(distToPlayer)) || heardPlayer)
                {
                    currentState = (creatureType == CreatureType.Deer) ? AIState.FleeingFire : AIState.Orbiting;
                    stateTimer = 0f;
                    if (audioSource != null && growlClip != null) audioSource.PlayOneShot(growlClip, 0.7f);
                }
                break;

            case AIState.Orbiting:
                orbitAngle += Time.deltaTime * 35f;
                float rad = orbitAngle * Mathf.Deg2Rad;
                Vector3 targetOrbit = playerTransform.position + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 6.5f;
                transform.position = Vector3.MoveTowards(transform.position, targetOrbit, Time.deltaTime * walkSpeed);
                LookAtFlat(playerTransform.position);

                if (stateTimer > 3.6f && Time.time > nextAttackTime)
                {
                    currentState = AIState.Windup;
                    stateTimer = 0f;
                    if (audioSource != null && growlClip != null)
                    {
                        audioSource.pitch = 1.25f;
                        audioSource.PlayOneShot(growlClip, 0.9f);
                    }
                }
                break;

            case AIState.Windup:
                // Pequeno recuo telegrafado (0.45s) para permitir Esquiva ou Parry
                LookAtFlat(playerTransform.position);
                if (stateTimer >= 0.45f)
                {
                    currentState = AIState.Charging;
                    stateTimer = 0f;
                    if (audioSource != null && attackClip != null)
                    {
                        audioSource.pitch = 1.0f;
                        audioSource.PlayOneShot(attackClip, 0.85f);
                    }
                }
                break;

            case AIState.Charging:
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, Time.deltaTime * chargeSpeed);
                LookAtFlat(playerTransform.position);

                if (distToPlayer <= attackReach)
                {
                    ExecuteAttackImpact();
                    nextAttackTime = Time.time + 2.8f;
                    currentState = AIState.Orbiting;
                    stateTimer = 0f;
                }
                else if (stateTimer > 3.0f)
                {
                    currentState = AIState.Orbiting;
                    stateTimer = 0f;
                }
                break;

            case AIState.FleeingFire:
                Vector3 fleeDir = transform.position - playerTransform.position;
                fleeDir.y = 0f;
                if (fleeDir.sqrMagnitude < 0.01f) fleeDir = transform.forward;
                fleeDir.Normalize();
                transform.position += fleeDir * Time.deltaTime * runSpeed;
                transform.rotation = Quaternion.LookRotation(fleeDir);

                if (distToPlayer > 18f && !cachedNearFire)
                {
                    currentState = AIState.Idle;
                    stateTimer = 0f;
                }
                break;
        }

        float terrY = TerrainMeshGenerator.GetTerrainHeight(transform.position.x, transform.position.z);
        transform.position = new Vector3(transform.position.x, terrY, transform.position.z);
    }

    private void LookAtFlat(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }
    }

    private void ExecuteAttackImpact()
    {
        ResolvePlayerRefs();
        if (playerControllerCache != null && playerControllerCache.IsInvulnerable)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFDD>ESQUIVA PERFEITA!</color>");
            return;
        }

        float dmg = (float)attackDamage;
        if (playerStatusCache != null)
        {
            float hpBefore = playerStatusCache.health;
            playerStatusCache.TakeDamage(dmg);

            // Aplica traumas apenas se nao foi perfeitamente aparado (vida realmente caiu)
            if (playerStatusCache.health < hpBefore - 1f)
            {
                if (creatureType == CreatureType.IberianWolf && Random.value < 0.35f)
                {
                    playerStatusCache.isBleeding = true;
                }
                else if (creatureType == CreatureType.WildBoar && Random.value < 0.28f)
                {
                    playerStatusCache.isFractured = true;
                }
            }
        }

        if (playerControllerCache != null)
        {
            Vector3 kbDir = (playerTransform.position - transform.position);
            kbDir.y = 0f;
            if (kbDir.sqrMagnitude > 0.01f)
            {
                playerControllerCache.ApplyKnockback(kbDir.normalized * (creatureType == CreatureType.WildBoar ? 7.5f : 5.0f));
            }
        }

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowDamageFromDirection(transform.position);
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentState == AIState.Dead) return;

        int dmgInt = Mathf.Max(1, Mathf.RoundToInt(damage));
        currentHealth -= dmgInt;
        hitFlashTimer = 0.14f;

        if (audioSource != null && attackClip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.15f);
            audioSource.PlayOneShot(attackClip, 0.8f);
        }

        if (playerTransform != null)
        {
            Vector3 away = transform.position - playerTransform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
            {
                transform.position += away.normalized * 0.45f;
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (creatureType == CreatureType.WildBoar)
            {
                currentState = AIState.Charging;
                stateTimer = 0f;
            }
            else if (creatureType == CreatureType.Deer)
            {
                currentState = AIState.FleeingFire;
                stateTimer = 0f;
            }
            else if (currentHealth < maxHealth * 0.35f)
            {
                currentState = AIState.FleeingFire;
                stateTimer = 0f;
            }
            else
            {
                currentState = AIState.Charging;
                stateTimer = 0f;
            }
        }
    }

    private void UpdateHitFlash()
    {
        if (hitFlashTimer <= 0f || cachedRenderers == null) return;
        hitFlashTimer -= Time.deltaTime;
        bool active = hitFlashTimer > 0f;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r == null) continue;
            if (active)
            {
                flashMpb.SetColor(BaseColorProp, new Color(1.0f, 0.25f, 0.25f, 1f));
                flashMpb.SetColor(ColorProp, new Color(1.0f, 0.25f, 0.25f, 1f));
                r.SetPropertyBlock(flashMpb);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }

    private void Die()
    {
        currentState = AIState.Dead;

        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null) cachedRenderers[i].SetPropertyBlock(null);
            }
        }

        if (audioSource != null && deathClip != null)
        {
            audioSource.PlayOneShot(deathClip, 1.0f);
        }

        StartCoroutine(SmoothDeathTumble());
        SpawnCarcassDrops();

        if (HUDSurvivalOverlay.Instance != null)
        {
            string label = creatureType == CreatureType.WildBoar ? "Javali" : (creatureType == CreatureType.Deer ? "Cervo" : "Lobo Iberico");
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#55FF88>Presa abatida: {label}!</color>");
        }

        Destroy(gameObject, 45f);
    }

    private IEnumerator SmoothDeathTumble()
    {
        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.Euler(0f, transform.eulerAngles.y, 85f);
        float elapsed = 0f;
        const float duration = 0.35f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, endRot, elapsed / duration);
            yield return null;
        }
        transform.rotation = endRot;
    }

    private void SpawnCarcassDrops()
    {
        Vector3 pos = transform.position + Vector3.up * 0.2f;
        int meatQty = (creatureType == CreatureType.WildBoar) ? 3 : 2;
        int hideQty = (creatureType == CreatureType.WildBoar) ? 2 : 1;

        if (LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(104, meatQty, pos + Vector3.right * 0.25f); // Carne Crua
            LootManager.Instance.DropItem(105, hideQty, pos - Vector3.right * 0.25f); // Pele de Animal
            LootManager.Instance.DropItem(120, 2, pos + Vector3.forward * 0.25f);      // Ossos
            return;
        }

        CreateFallbackPickup("Harvest_RawMeat", 104, meatQty, pos + Vector3.right * 0.3f, new Vector3(0.2f, 0.15f, 0.2f));
        CreateFallbackPickup("Harvest_AnimalPelt", 105, hideQty, pos - Vector3.right * 0.3f, new Vector3(0.25f, 0.08f, 0.25f));
        CreateFallbackPickup("Harvest_Bones", 120, 2, pos + Vector3.forward * 0.3f, new Vector3(0.18f, 0.10f, 0.18f));
    }

    private static void CreateFallbackPickup(string objName, int itemId, int qty, Vector3 pos, Vector3 scale)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = objName;
        obj.transform.position = pos;
        obj.transform.localScale = scale;
        ResourcePickup p = obj.AddComponent<ResourcePickup>();
        p.itemID = itemId;
        p.amount = qty;
    }

    private bool CanSeePlayer(float distToPlayer)
    {
        if (distToPlayer < 4.5f) return true;
        Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, toPlayer);
        float fovHalf = visionAngle * 0.5f;
        if (FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.IsHidden) fovHalf *= 0.65f;
        return angle < fovHalf;
    }

    private bool CheckForFireNear()
    {
        if (playerTransform != null && InventoryAdvanced.Instance != null &&
            (InventoryAdvanced.Instance.equippedOffHandId == 303 || InventoryAdvanced.Instance.equippedOffHandId == 304))
        {
            if ((playerTransform.position - transform.position).sqrMagnitude <= fearOfFireDistance * fearOfFireDistance)
            {
                return true;
            }
        }

        int count = Physics.OverlapSphereNonAlloc(transform.position, fearOfFireDistance, s_fireOverlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider c = s_fireOverlapBuffer[i];
            if (c == null) continue;
            BonfireEntity bf = c.GetComponentInParent<BonfireEntity>();
            if (bf != null && bf.isLit) return true;

            string n = c.gameObject.name;
            if (n.IndexOf("torch", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("bonfire", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("campfire", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    // =================== SINTESE PROCEDURAL DE AUDIO ===================

    private AudioClip GenerateGrowlSound()
    {
        int sampleRate = 44100;
        float dur = 0.55f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = (creatureType == CreatureType.WildBoar) ? 75f : 120f;
            float growl = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * (Random.Range(-0.4f, 0.4f) + 0.6f) * Mathf.Sin(t * Mathf.PI) * 0.6f;
            samples[i] = growl;
        }

        AudioClip clip = AudioClip.Create("CreatureGrowl_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateAttackSound()
    {
        int sampleRate = 44100;
        float dur = 0.35f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float snap = Mathf.Sin(2f * Mathf.PI * 220f * ((float)i / sampleRate)) * Mathf.Exp(-12f * t) * 0.75f;
            samples[i] = snap;
        }

        AudioClip clip = AudioClip.Create("CreatureAttack_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateDeathSound()
    {
        int sampleRate = 44100;
        float dur = 0.60f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float groan = Mathf.Sin(2f * Mathf.PI * (160f - t * 80f) * ((float)i / sampleRate)) * Mathf.Exp(-4f * t) * 0.7f;
            samples[i] = groan;
        }

        AudioClip clip = AudioClip.Create("CreatureDeath_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
