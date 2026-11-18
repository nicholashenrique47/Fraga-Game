using UnityEngine;

/// BossSystem: IA avançada para o Vulto Anciao (Boss Final).
/// Comporta-se de forma agressiva com varios padroes de ataque (Summon, AoE, Melee).
public class BossSystem : MonoBehaviour
{
    public static BossSystem Instance { get; private set; }

    public enum BossState { Dormant, Chasing, Summoning, AreaAttack, Dead }
    
    [Header("Dependencies")]
    public Transform playerTransform;
    public VultoManager vultoManager;
    public DayNightCycle dayNightCycle;
    public SanitySystem sanitySystem;
    
    [Header("Boss Stats")]
    public float maxHealth = 500f;
    public float currentHealth;
    public float moveSpeed = 4f;
    
    [Header("Attacks")]
    public float meleeRange = 3f;
    public float meleeDamage = 25f;
    public float summonCooldown = 20f;
    public float aoeCooldown = 15f;
    public float aoeRadius = 8f;
    public float aoeDamage = 40f;
    
    private BossState state = BossState.Dormant;
    private GameObject bossInstance;
    
    private float summonTimer = 0f;
    private float aoeTimer = 0f;
    private float attackTimer = 0f;
    private float stateTimer = 0f;
    
    private Vector3 aoeTarget;
    private GameObject aoeIndicator;

    private Renderer[] bossRenderers;
    private Transform bossHaloTransform;
    private Renderer aoeRenderer;
    private MaterialPropertyBlock mpb;
    private Material cachedAoeMat;
    private bool hasEnteredPhase2 = false;
    private float hitFlashTimer = 0f;
    private bool isWindingMelee = false;
    private float meleeWindupTimer = 0f;
    private float baseMoveSpeed;
    private float baseAoeCooldown;
    private float baseSummonCooldown;

    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    public bool IsActive => state != BossState.Dormant && state != BossState.Dead;
    public bool IsEnraged => IsActive && currentHealth <= maxHealth * 0.5f;
    public string BossTitle => "VULTO ANCIAO DA FRAGA";
    public float HealthPercentage => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public Vector3 BossPosition => bossInstance != null ? bossInstance.transform.position : transform.position;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        mpb = new MaterialPropertyBlock();
        baseMoveSpeed = moveSpeed;
        baseAoeCooldown = aoeCooldown;
        baseSummonCooldown = summonCooldown;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        if (playerTransform == null && PlayerController.Instance != null)
            playerTransform = PlayerController.Instance.transform;
        if (vultoManager == null) vultoManager = VultoManager.Instance ?? FindAnyObjectByType<VultoManager>();
        if (dayNightCycle == null) dayNightCycle = DayNightCycle.Instance ?? FindAnyObjectByType<DayNightCycle>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (audioManager == null) audioManager = AudioManager.Instance ?? FindAnyObjectByType<AudioManager>();
    }

    public void SpawnBoss(Vector3 position)
    {
        if (IsActive) return;
        
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            position.x = Mathf.Clamp(position.x, 8f, Mathf.Max(16f, gm.mapWidth - 8f));
            position.z = Mathf.Clamp(position.z, 8f, Mathf.Max(16f, gm.mapHeight - 8f));
        }
        position.y = TerrainMeshGenerator.GetTerrainHeight(position.x, position.z);

        GameObject prefab = Resources.Load<GameObject>("Prefabs/Boss_Prefab");
        if (prefab != null)
        {
            bossInstance = Instantiate(prefab, position, Quaternion.identity, transform);
        }
        else
        {
            bossInstance = CreateProceduralBoss(position);
        }

        state = BossState.Chasing;
        currentHealth = maxHealth;
        hasEnteredPhase2 = false;
        isWindingMelee = false;
        meleeWindupTimer = 0f;
        hitFlashTimer = 0f;
        moveSpeed = baseMoveSpeed > 0.1f ? baseMoveSpeed : 4f;
        aoeCooldown = baseAoeCooldown > 1f ? baseAoeCooldown : 15f;
        summonCooldown = baseSummonCooldown > 1f ? baseSummonCooldown : 20f;
        bossRenderers = bossInstance != null ? bossInstance.GetComponentsInChildren<Renderer>(true) : null;
        bossHaloTransform = bossInstance != null ? bossInstance.transform.Find("RuneHalo") : null;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        summonTimer = 5f;
        aoeTimer = 10f;

        Debug.LogWarning("[BossSystem] O VULTO ANCIAO DESPERTOU!");

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF4466>O VULTO ANCIAO DESPERTOU DAS SOMBRAS!</color>");
        else
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("O Anciao Despertou...");
        }

        HapticManager haptic = FindAnyObjectByType<HapticManager>();
        if (haptic != null) haptic.TriggerHeavyVibration();
        if (audioManager != null && bossMusicClip != null) audioManager.PlayMusic(bossMusicClip);
        else if (audioManager != null) audioManager.PlayNightHorror();
    }

    private GameObject CreateProceduralBoss(Vector3 pos)
    {
        GameObject root = new GameObject("ElderVulto_Boss");
        root.transform.SetParent(transform, false);
        root.transform.position = pos;

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1.8f, 0f);
        col.radius = 0.85f;
        col.height = 3.6f;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material darkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark") ?? new Material(lit);
        darkMat.color = new Color(0.08f, 0.06f, 0.10f);
        Material stoneMat = Resources.Load<Material>("Materials/Ruin_Stones") ?? darkMat;
        Material eyeMat = Resources.Load<Material>("Materials/Mat_Emissive_VultoEyes");
        if (eyeMat == null)
        {
            eyeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? lit);
            eyeMat.color = new Color(1.0f, 0.14f, 0.22f);
        }
        Material runeMat = Resources.Load<Material>("Materials/Mat_Emissive_RuneGlow") ?? eyeMat;

        // Manto sombrio inferior + tronco esculpido + ombreiras runicas
        CreateBossPart(PrimitiveType.Cylinder, "CloakSkirt", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(1.25f, 0.95f, 1.05f), Quaternion.identity, darkMat);
        CreateBossPart(PrimitiveType.Capsule, "Torso", root.transform, new Vector3(0f, 2.05f, 0f), new Vector3(1.18f, 1.10f, 0.88f), Quaternion.identity, darkMat);
        CreateBossPart(PrimitiveType.Cube, "Pauldron_L", root.transform, new Vector3(-0.78f, 2.70f, 0f), new Vector3(0.55f, 0.44f, 0.62f), Quaternion.Euler(0f, 0f, 24f), stoneMat);
        CreateBossPart(PrimitiveType.Cube, "Pauldron_R", root.transform, new Vector3(0.78f, 2.70f, 0f), new Vector3(0.55f, 0.44f, 0.62f), Quaternion.Euler(0f, 0f, -24f), stoneMat);

        // Bracos longos e garras
        for (int side = -1; side <= 1; side += 2)
        {
            string sTag = side < 0 ? "L" : "R";
            CreateBossPart(PrimitiveType.Capsule, $"Arm_{sTag}", root.transform, new Vector3(side * 0.92f, 1.95f, 0.14f), new Vector3(0.28f, 0.92f, 0.28f), Quaternion.Euler(18f, 0f, side * -12f), darkMat);
            for (int c = -1; c <= 1; c++)
            {
                CreateBossPart(PrimitiveType.Cube, $"Claw_{sTag}_{c + 1}", root.transform, new Vector3(side * 1.02f + c * 0.08f, 1.08f, 0.36f), new Vector3(0.06f, 0.42f, 0.08f), Quaternion.Euler(35f, 0f, 0f), stoneMat);
            }
        }

        // Cranio encapuzado + galhada retorcida + olhos duplos emissivos
        CreateBossPart(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 3.12f, 0.08f), new Vector3(0.68f, 0.78f, 0.72f), Quaternion.identity, darkMat);
        CreateBossPart(PrimitiveType.Cube, "Horn_L", root.transform, new Vector3(-0.36f, 3.65f, -0.02f), new Vector3(0.11f, 0.78f, 0.11f), Quaternion.Euler(-12f, 0f, 28f), stoneMat);
        CreateBossPart(PrimitiveType.Cube, "Horn_R", root.transform, new Vector3(0.36f, 3.65f, -0.02f), new Vector3(0.11f, 0.78f, 0.11f), Quaternion.Euler(-12f, 0f, -28f), stoneMat);
        CreateBossPart(PrimitiveType.Sphere, "Eye_L", root.transform, new Vector3(-0.15f, 3.16f, 0.38f), new Vector3(0.12f, 0.09f, 0.10f), Quaternion.identity, eyeMat);
        CreateBossPart(PrimitiveType.Sphere, "Eye_R", root.transform, new Vector3(0.15f, 3.16f, 0.38f), new Vector3(0.12f, 0.09f, 0.10f), Quaternion.identity, eyeMat);

        // Nucleo ritual peitoral
        CreateBossPart(PrimitiveType.Sphere, "Core", root.transform, new Vector3(0f, 2.22f, 0.42f), new Vector3(0.36f, 0.44f, 0.24f), Quaternion.identity, runeMat);

        // Halo de runas flutuantes nas costas
        GameObject halo = new GameObject("RuneHalo");
        halo.transform.SetParent(root.transform, false);
        halo.transform.localPosition = new Vector3(0f, 2.55f, -0.35f);
        for (int i = 0; i < 6; i++)
        {
            float ang = i * 60f * Mathf.Deg2Rad;
            Vector3 lp = new Vector3(Mathf.Cos(ang) * 1.15f, Mathf.Sin(ang) * 1.15f, 0f);
            CreateBossPart(PrimitiveType.Cube, $"RuneShard_{i}", halo.transform, lp, new Vector3(0.14f, 0.34f, 0.08f), Quaternion.Euler(0f, 0f, i * 60f), runeMat);
        }
        bossHaloTransform = halo.transform;

        GameObject auraLightObj = new GameObject("BossAuraLight");
        auraLightObj.transform.SetParent(root.transform, false);
        auraLightObj.transform.localPosition = new Vector3(0f, 2.4f, 0.4f);
        Light aura = auraLightObj.AddComponent<Light>();
        aura.type = LightType.Point;
        aura.color = new Color(0.95f, 0.18f, 0.32f);
        aura.range = 10f;
        aura.intensity = 2.2f;
        aura.shadows = LightShadows.None;

        return root;
    }

    private static GameObject CreateBossPart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;
        part.transform.localRotation = localRot;
        Collider c = part.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
        Renderer r = part.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
        return part;
    }
    
    public AudioManager audioManager;
    public AudioClip bossMusicClip;
    
    private void Update()
    {
        if (playerTransform == null && PlayerController.Instance != null)
            playerTransform = PlayerController.Instance.transform;
        if (playerTransform == null) return;

        // Spawns automatically on Day 5 night
        if (state == BossState.Dormant && dayNightCycle != null)
        {
            bool caveAwaken = dayNightCycle.IsNight() &&
                Vector2.Distance(new Vector2(playerTransform.position.x, playerTransform.position.z), FragaWorldBlueprint.SacredCavePos) < 15f;
            if ((dayNightCycle.currentDay >= 5 && dayNightCycle.currentTime >= 20f) || caveAwaken)
            {
                // Spawn near player
                Vector3 spawnPos = playerTransform.position + new Vector3(Random.Range(-15f, 15f), 0, Random.Range(-15f, 15f));
                SpawnBoss(spawnPos);
            }
        }
        
        if (!IsActive || bossInstance == null || playerTransform == null) return;
        UpdateBossVisualFeedback();

        // Face player
        Vector3 dirToPlayer = (playerTransform.position - bossInstance.transform.position);
        dirToPlayer.y = 0;
        float distToPlayer = dirToPlayer.magnitude;
        if (distToPlayer > 0.01f)
        {
            bossInstance.transform.rotation = Quaternion.Slerp(bossInstance.transform.rotation, Quaternion.LookRotation(dirToPlayer), Time.deltaTime * 3f);
        }
        
        switch (state)
        {
            case BossState.Chasing:
                UpdateChasing(dirToPlayer, distToPlayer);
                break;
            case BossState.Summoning:
                UpdateSummoning();
                break;
            case BossState.AreaAttack:
                UpdateAreaAttack(distToPlayer);
                break;
        }
        
        // Aura passiva: drena sanidade se estiver perto
        if (distToPlayer < 15f && sanitySystem != null)
        {
            sanitySystem.sanity -= 2f * Time.deltaTime;
        }
    }
    
    private void UpdateChasing(Vector3 dirToPlayer, float distToPlayer)
    {
        if (isWindingMelee)
        {
            meleeWindupTimer -= Time.deltaTime;
            float pulse = 1.0f + Mathf.Sin((0.55f - Mathf.Max(0f, meleeWindupTimer)) * 14f) * 0.08f;
            bossInstance.transform.localScale = new Vector3(pulse, pulse, pulse);
            if (meleeWindupTimer <= 0f)
            {
                isWindingMelee = false;
                bossInstance.transform.localScale = Vector3.one;
                ExecuteBossMeleeStrike(dirToPlayer, distToPlayer);
            }
            return;
        }

        // Move towards player
        if (distToPlayer > meleeRange)
        {
            Vector3 nextPos = bossInstance.transform.position + dirToPlayer.normalized * moveSpeed * Time.deltaTime;
            nextPos.y = TerrainMeshGenerator.GetTerrainHeight(nextPos.x, nextPos.z);
            bossInstance.transform.position = nextPos;
        }
        else
        {
            Vector3 curPos = bossInstance.transform.position;
            curPos.y = TerrainMeshGenerator.GetTerrainHeight(curPos.x, curPos.z);
            bossInstance.transform.position = curPos;

            // Melee Attack com windup telegrafado
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isWindingMelee = true;
                meleeWindupTimer = hasEnteredPhase2 ? 0.42f : 0.55f;
            }
        }

        // Check state transitions
        summonTimer -= Time.deltaTime;
        aoeTimer -= Time.deltaTime;

        if (aoeTimer <= 0)
        {
            state = BossState.AreaAttack;
            stateTimer = hasEnteredPhase2 ? 2.35f : 3f;
            aoeTimer = aoeCooldown;
            aoeTarget = playerTransform.position;

            // Create red indicator
            CreateAoEIndicator(aoeTarget);
        }
        else if (summonTimer <= 0 && vultoManager != null)
        {
            state = BossState.Summoning;
            stateTimer = 2f; // 2 seconds channel
            summonTimer = summonCooldown;
        }
    }

    private void ExecuteBossMeleeStrike(Vector3 dirToPlayer, float distToPlayer)
    {
        attackTimer = hasEnteredPhase2 ? 1.45f : 2.0f;
        if (distToPlayer > meleeRange + 1.35f) return;

        PlayerController pController = playerTransform.GetComponent<PlayerController>() ?? PlayerController.Instance;
        if (pController != null && pController.IsInvulnerable)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFDD>ESQUIVA PERFEITA!</color>");
            return;
        }

        PlayerStatus pStatus = playerTransform.GetComponent<PlayerStatus>() ?? PlayerStatus.Instance;
        if (pStatus != null)
        {
            float hpBefore = pStatus.health;
            float dmg = hasEnteredPhase2 ? meleeDamage * 1.2f : meleeDamage;
            pStatus.TakeDamage(dmg);
            if (pStatus.health < hpBefore - 1f && Random.value < 0.35f)
            {
                pStatus.ApplyBleeding();
            }
        }

        if (pController != null && dirToPlayer.sqrMagnitude > 0.01f)
        {
            pController.ApplyKnockback(dirToPlayer.normalized * 8.5f);
        }

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowDamageFromDirection(BossPosition);

        HapticManager haptic = FindAnyObjectByType<HapticManager>();
        if (haptic != null) haptic.TriggerHeavyVibration();
        if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.ShakeMedium();
    }
    
    private void UpdateSummoning()
    {
        stateTimer -= Time.deltaTime;
        
        // VFX pulsing
        float scale = 1f + Mathf.Sin(Time.time * 20f) * 0.1f;
        bossInstance.transform.localScale = new Vector3(scale, scale, scale);
        
        // Screen Shake contínuo durante channeling
        if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.ShakeContinuous(0.15f);
        
        if (stateTimer <= 0)
        {
            // Spawn 3 vultos
            Vector3 pos = bossInstance.transform.position;
            vultoManager.SpawnVulto(pos.x + 2f, pos.z);
            vultoManager.SpawnVulto(pos.x - 2f, pos.z);
            vultoManager.SpawnVulto(pos.x, pos.z + 2f);
            
            bossInstance.transform.localScale = Vector3.one;
            state = BossState.Chasing;
        }
    }
    
    private void UpdateAreaAttack(float distToPlayer)
    {
        stateTimer -= Time.deltaTime;

        if (aoeRenderer != null && mpb != null)
        {
            // Flash indicator
            float alpha = Mathf.PingPong(Time.time * 5f, 0.5f) + 0.2f;
            Color c = new Color(1f, 0.08f, 0.08f, alpha);
            mpb.SetColor(BaseColorProp, c);
            mpb.SetColor(ColorProp, c);
            aoeRenderer.SetPropertyBlock(mpb);
        }

        if (stateTimer <= 0)
        {
            // BOOM!
            float distToTarget = Vector3.Distance(playerTransform.position, aoeTarget);
            if (distToTarget <= aoeRadius)
            {
                PlayerController pController = playerTransform.GetComponent<PlayerController>() ?? PlayerController.Instance;
                if (pController != null && pController.IsInvulnerable)
                {
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFDD>ESQUIVA DO IMPACTO SOMBRIO!</color>");
                }
                else
                {
                    PlayerStatus pStatus = playerTransform.GetComponent<PlayerStatus>() ?? PlayerStatus.Instance;
                    if (pStatus != null)
                    {
                        float hpBefore = pStatus.health;
                        pStatus.TakeDamage(aoeDamage);
                        if (pStatus.health < hpBefore - 1f) pStatus.ApplyBleeding();
                    }

                    if (pController != null)
                        pController.ApplyKnockback((playerTransform.position - aoeTarget).normalized * 12f);

                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowDamageFromDirection(aoeTarget);
                }
            }
            
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(aoeTarget); // Mega smoke
            HapticManager haptic = FindAnyObjectByType<HapticManager>();
            if (haptic != null) haptic.TriggerHeavyVibration();

            // Screen Shake massivo na explosão AoE
            if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.ShakeHeavy();
            
            if (aoeIndicator != null) Destroy(aoeIndicator);
            aoeRenderer = null;
            state = BossState.Chasing;
        }
    }

    private void CreateAoEIndicator(Vector3 pos)
    {
        aoeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(aoeIndicator.GetComponent<Collider>());
        pos.y = TerrainMeshGenerator.GetTerrainHeight(pos.x, pos.z);
        aoeIndicator.transform.position = pos + new Vector3(0, 0.1f, 0);
        aoeIndicator.transform.localScale = new Vector3(aoeRadius * 2f, 0.05f, aoeRadius * 2f);

        if (cachedAoeMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            cachedAoeMat = new Material(sh);
            cachedAoeMat.color = new Color(1f, 0.08f, 0.08f, 0.4f);
            cachedAoeMat.SetFloat("_Surface", 1);
            cachedAoeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            cachedAoeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            cachedAoeMat.SetInt("_ZWrite", 0);
            cachedAoeMat.renderQueue = 3000;
        }
        aoeRenderer = aoeIndicator.GetComponent<Renderer>();
        if (aoeRenderer != null) aoeRenderer.sharedMaterial = cachedAoeMat;

        // Perimetro segmentado externo para leitura nitida do raio letal
        for (int i = 0; i < 16; i++)
        {
            float ang = (i / 16f) * Mathf.PI * 2f;
            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"AoE_RingSeg_{i}";
            seg.transform.SetParent(aoeIndicator.transform, false);
            seg.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.49f, 1.4f, Mathf.Sin(ang) * 0.49f);
            seg.transform.localRotation = Quaternion.Euler(0f, -i * (360f / 16f), 0f);
            seg.transform.localScale = new Vector3(0.04f, 1.8f, 0.14f);
            Collider sc = seg.GetComponent<Collider>();
            if (sc != null) Destroy(sc);
            Renderer sr = seg.GetComponent<Renderer>();
            if (sr != null) sr.sharedMaterial = cachedAoeMat;
        }
    }

    public bool TryDamageInRay(Ray ray, float maxRange, float radius, float amount, out Vector3 hitPos, out bool killed)
    {
        hitPos = Vector3.zero;
        killed = false;
        if (!IsActive || bossInstance == null) return false;

        Vector3 center = bossInstance.transform.position + Vector3.up * 1.8f;
        Vector3 toCenter = center - ray.origin;
        float proj = Vector3.Dot(toCenter, ray.direction);
        if (proj < -0.25f || proj > maxRange + 0.6f) return false;

        Vector3 closest = ray.origin + ray.direction * Mathf.Max(0f, proj);
        float dx = center.x - closest.x;
        float dz = center.z - closest.z;
        float dy = Mathf.Abs(center.y - closest.y);
        float thr = radius + 0.95f;

        if (dx * dx + dz * dz <= thr * thr && dy <= 2.1f)
        {
            hitPos = closest;
            TakeDamage(amount);
            killed = !IsActive;
            return true;
        }
        return false;
    }

    private void UpdateBossVisualFeedback()
    {
        if (bossRenderers == null && bossInstance != null)
            bossRenderers = bossInstance.GetComponentsInChildren<Renderer>(true);
        if (bossRenderers == null || mpb == null) return;

        if (bossHaloTransform != null)
        {
            float rotSpeed = hasEnteredPhase2 ? 120f : 42f;
            bossHaloTransform.Rotate(0f, 0f, rotSpeed * Time.deltaTime, Space.Self);
        }

        if (hitFlashTimer > 0f)
            hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.deltaTime);

        bool showFlash = hitFlashTimer > 0f;
        bool showWarn = isWindingMelee;
        if (!showFlash && !showWarn && !hasEnteredPhase2)
        {
            for (int i = 0; i < bossRenderers.Length; i++)
            {
                if (bossRenderers[i] != null) bossRenderers[i].SetPropertyBlock(null);
            }
            return;
        }

        Color c = showFlash
            ? new Color(1.0f, 0.22f, 0.22f, 1f)
            : (showWarn ? new Color(1.0f, 0.45f, 0.12f, 1f) : new Color(0.55f, 0.08f, 0.14f, 1f));

        mpb.SetColor(BaseColorProp, c);
        mpb.SetColor(ColorProp, c);
        for (int i = 0; i < bossRenderers.Length; i++)
        {
            if (bossRenderers[i] != null) bossRenderers[i].SetPropertyBlock(mpb);
        }
    }

    public void TakeDamage(float amount)
    {
        if (!IsActive) return;

        currentHealth -= amount;
        hitFlashTimer = 0.15f;

        if (!hasEnteredPhase2 && currentHealth > 0f && currentHealth <= maxHealth * 0.5f)
        {
            hasEnteredPhase2 = true;
            moveSpeed = (baseMoveSpeed > 0.1f ? baseMoveSpeed : 4f) * 1.32f;
            aoeCooldown = (baseAoeCooldown > 1f ? baseAoeCooldown : 15f) * 0.72f;
            summonCooldown = (baseSummonCooldown > 1f ? baseSummonCooldown : 20f) * 0.75f;

            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FF3355>FASE 2: O VULTO ANCIAO ENFURECEU!</color>");
            if (ScreenShakeSystem.Instance != null)
                ScreenShakeSystem.Instance.ShakeHeavy();
            if (VFXPoolManager.Instance != null)
                VFXPoolManager.Instance.PlayShadowHitVFX(BossPosition + Vector3.up * 1.5f);
        }

        if (HUDSurvivalOverlay.Instance != null && currentHealth > 0f)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Vulto Anciao: {Mathf.CeilToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)} HP");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        state = BossState.Dead;
        
        if (aoeIndicator != null) Destroy(aoeIndicator);
        
        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayDeathVFX(bossInstance.transform.position);
            VFXPoolManager.Instance.PlayDeathVFX(bossInstance.transform.position + Vector3.up);
        }
        
        // Drop Coração Sombrio (Item 115)
        if (LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(115, 1, bossInstance.transform.position);
        }
        
        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFAA>O VULTO ANCIAO FOI DERROTADO! Pegue o Coracao Sombrio.</color>");
        else
        {
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("O VULTO ANCIAO FOI DERROTADO!");
        }
        
        Destroy(bossInstance);
        
        // Restore music
        if (audioManager != null) audioManager.StopMusic(); // Vai voltar ao normal
    }
}
