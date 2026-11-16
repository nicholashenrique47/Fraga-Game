using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ChoppingAndCombatSystem: Sistema unificado de Combate Corpo a Corpo, Corte de Arvores e Mineracao.
/// - Usa stats reais da arma equipada no ItemDatabase (dano, alcance, cadencia, estamina, durabilidade e multiplicadores de coleta).
/// - Suporta combo alternado, Golpe Pesado (1.65x), contra-ataque de Parry (2.5x) e fisica NonAlloc (Zero-GC).
/// - Integra dano direto em Vultos (GPU-Instanced), Fauna selvagem e entidades com Collider, com VFX por material e hitmarker.
/// - Deixa toco realista (Stump_Cleared) e toras fisicas ao derrubar arvores estilo The Forest, alem de Resina, Ferro e Sal.
/// </summary>
public class ChoppingAndCombatSystem : MonoBehaviour
{
    public static ChoppingAndCombatSystem Instance { get; private set; }

    [Header("Fallback Fists Settings")]
    public float baseAttackRange = 2.25f;
    public float attackRadius = 0.28f;
    public float baseAttackCooldown = 0.40f;
    public float baseDamage = 12f;

    [Header("Dependencies")]
    public Camera playerCamera;
    public InventoryAdvanced inventory;
    public PlayerController playerController;
    public PlayerStatus playerStatus;

    private float lastAttackTime = -10f;
    private int comboStep = 0;

    private AudioSource audioSource;
    private AudioClip chopWoodClip;
    private AudioClip treeFallClip;
    private AudioClip hitRockClip;
    private AudioClip swingWhooshClip;
    private AudioClip hitFleshClip;

    private MapRenderer mapRendererCache;
    private HapticManager hapticCache;
    private Material cachedStumpTopMat;

    private readonly Dictionary<int, int> treeHitsRemaining = new Dictionary<int, int>();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[12];

    private void Awake()
    {
        Instance = this;
        SetupAudio();
    }

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerController == null) playerController = PlayerController.Instance ?? GetComponent<PlayerController>();
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? GetComponent<PlayerStatus>();
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        mapRendererCache = FindAnyObjectByType<MapRenderer>();
        hapticCache = FindAnyObjectByType<HapticManager>();
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.35f;
        audioSource.volume = 0.78f;

        chopWoodClip = GenerateChopWoodSound();
        treeFallClip = GenerateTreeFallSound();
        hitRockClip = GenerateRockHitSound();
        swingWhooshClip = GenerateSwingWhooshSound();
        hitFleshClip = GenerateFleshHitSound();
    }

    public void TriggerAttack()
    {
        TriggerAttack(false);
    }

    public void TriggerAttack(bool isHeavy)
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (playerStatus == null) playerStatus = PlayerStatus.Instance;
        if (playerCamera == null) playerCamera = Camera.main;

        // Se estiver carregando tora no ombro, arremessa a tora para frente
        if (LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs > 0)
        {
            LogCarryingSystem.Instance.DropLog(throwForward: true);
            lastAttackTime = Time.time;
            return;
        }

        int weaponId = inventory != null ? inventory.equippedMainHandId : -1;
        ItemData weaponData = weaponId > 0 ? ItemDatabase.GetItem(weaponId) : null;

        float cooldown = weaponData != null ? weaponData.attackCooldown : baseAttackCooldown;
        if (isHeavy) cooldown *= 1.18f;
        if (Time.time < lastAttackTime + cooldown) return;

        float staminaCost = (weaponData != null ? weaponData.staminaCost : 7f) * (isHeavy ? 1.55f : 1f);
        if (playerStatus != null && playerStatus.stamina < 4f)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Sem folego para golpear!");
            return;
        }
        if (playerStatus != null)
        {
            playerStatus.stamina = Mathf.Max(0f, playerStatus.stamina - staminaCost);
        }

        lastAttackTime = Time.time;
        comboStep = (comboStep + 1) % 2;

        if (FirstPersonViewmodelController.Instance != null)
        {
            FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(comboStep, isHeavy);
        }

        if (FragaStealthSystem.Instance != null)
        {
            FragaStealthSystem.Instance.EmitNoisePulse(isHeavy ? 0.85f : 0.55f);
        }

        if (audioSource != null && swingWhooshClip != null)
        {
            audioSource.pitch = isHeavy ? Random.Range(0.76f, 0.88f) : Random.Range(0.95f, 1.12f);
            audioSource.PlayOneShot(swingWhooshClip, isHeavy ? 0.55f : 0.38f);
        }

        if (playerCamera == null) return;

        float range = weaponData != null ? weaponData.attackRange : baseAttackRange;
        float damage = (weaponData != null ? weaponData.attackDamage : baseDamage) * (isHeavy ? 1.65f : 1f);
        float treeMult = weaponData != null ? weaponData.harvestTreeMultiplier : 1f;
        float rockMult = weaponData != null ? weaponData.harvestRockMultiplier : 1f;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool hasColliderHit = TryFindBestHit(ray, range, out RaycastHit bestHit);

        float obstacleLimit = range + 0.35f;
        if (hasColliderHit && bestHit.collider != null && IsStaticObstacle(bestHit.collider))
        {
            obstacleLimit = Mathf.Min(obstacleLimit, bestHit.distance + 0.45f);
        }

        // 1. Prioriza entidades sombrias (Vultos GPU-Instanced) no cone frontal
        if (VultoManager.Instance != null)
        {
            float previewDamage = PreviewParryDamage(damage, out _);
            if (VultoManager.Instance.TryDamageVultoInRay(ray, obstacleLimit, attackRadius + 0.28f, previewDamage, ray.direction, out Vector3 vultoHitPos, out bool vultoKilled))
            {
                bool parryCrit = ConsumeParryCrit();
                HandleLivingTargetFeedback(vultoHitPos, Vector3.up, previewDamage, parryCrit, isHeavy, vultoKilled, isShadow: true);
                DegradeEquippedTool();
                return;
            }
        }

        // 2. Verifica animais controlados pelo FaunaManager (SoA nativo / procedural)
        if (FaunaManager.Instance != null)
        {
            float previewDamage = PreviewParryDamage(damage, out _);
            if (FaunaManager.Instance.TryDamageFaunaInRay(ray, obstacleLimit, attackRadius + 0.22f, previewDamage, out Vector3 faunaHitPos, out bool faunaKilled))
            {
                bool parryCrit = ConsumeParryCrit();
                HandleLivingTargetFeedback(faunaHitPos, Vector3.up, previewDamage, parryCrit, isHeavy, faunaKilled, isShadow: false);
                DegradeEquippedTool();
                return;
            }
        }

        // 3. Processa colisoes fisicas (Arvores, Rochas, Construcoes, WildlifeAIEngine)
        if (hasColliderHit)
        {
            ProcessHit(bestHit, weaponId, damage, isHeavy, treeMult, rockMult);
        }
    }

    private void DegradeEquippedTool()
    {
        if (inventory != null && inventory.equippedMainHandId > 0)
        {
            inventory.DamageEquippedTool(EquipSlotType.MainHand, 1f);
        }
    }

    private float PreviewParryDamage(float baseDmg, out bool hasParry)
    {
        if (PlayerMeleeCombatDefense.Instance != null && PlayerMeleeCombatDefense.Instance.hasParryBonus)
        {
            hasParry = true;
            return baseDmg * PlayerMeleeCombatDefense.Instance.counterMultiplier;
        }
        hasParry = false;
        return baseDmg;
    }

    private bool ConsumeParryCrit()
    {
        if (PlayerMeleeCombatDefense.Instance != null && PlayerMeleeCombatDefense.Instance.hasParryBonus)
        {
            PlayerMeleeCombatDefense.Instance.ConsumeParryBonus();
            return true;
        }
        return false;
    }

    private bool IsStaticObstacle(Collider col)
    {
        if (col == null) return false;
        if (IsRockCollider(col)) return true;
        string n = col.gameObject.name;
        return n.IndexOf("terrain", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsRockCollider(Collider col)
    {
        if (col == null) return false;
        if (col.GetComponentInParent<MineableRockNode>() != null) return true;

        Transform cur = col.transform;
        for (int depth = 0; depth < 4 && cur != null; depth++)
        {
            string n = cur.name;
            if (n.StartsWith("Chunk_", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Environment_Instances", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Landmarks_Global", System.StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (n.IndexOf("terrain", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("pickup", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("runic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("workbench", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("cauldron", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("campfire", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("shelter", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (n.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("stone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("ruin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("boulder", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("cliff", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("pedra", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("rocha", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("granite", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("henge", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            cur = cur.parent;
        }
        return false;
    }

    private void HandleLivingTargetFeedback(Vector3 hitPos, Vector3 normal, float actualDamage, bool parryCrit, bool isHeavy, bool killed, bool isShadow)
    {
        if (audioSource != null && hitFleshClip != null)
        {
            audioSource.pitch = isShadow ? Random.Range(0.72f, 0.88f) : Random.Range(0.92f, 1.12f);
            audioSource.PlayOneShot(hitFleshClip, isShadow ? 0.72f : 0.90f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(parryCrit ? 0.22f : (isHeavy ? 0.15f : 0.09f));

        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.TriggerHitstop(parryCrit || killed ? 0.055f : (isHeavy ? 0.042f : 0.028f), 0.09f);

        if (hapticCache != null)
        {
            if (killed || parryCrit) hapticCache.TriggerHeavyVibration();
            else hapticCache.TriggerLightVibration();
        }

        if (VFXPoolManager.Instance != null)
        {
            if (isShadow) VFXPoolManager.Instance.PlayShadowHitVFX(hitPos);
            else VFXPoolManager.Instance.PlayBloodSplashVFX(hitPos, normal);
            VFXPoolManager.Instance.PlayBloodDecalVFX(hitPos, isShadow);
        }

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: parryCrit || isHeavy, isKill: killed);

            if (killed)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip(isShadow
                    ? "<color=#BB88FF>Vulto dissipado na escuridao!</color>"
                    : "<color=#55FF88>Presa abatida!</color>");
            }
            else if (parryCrit)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#55FFDD>CRITICO DE PARRY! ({actualDamage:F0} Dano)</color>");
            }
            else if (isHeavy)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Golpe Pesado! ({actualDamage:F0} Dano)");
            }
        }
    }

    private bool TryFindBestHit(Ray ray, float range, out RaycastHit bestHit)
    {
        bestHit = default;
        float minDist = float.MaxValue;
        bool found = false;

        int count = Physics.SphereCastNonAlloc(ray, attackRadius, hitBuffer, range, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = hitBuffer[i].collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (hitBuffer[i].distance < minDist)
            {
                minDist = hitBuffer[i].distance;
                bestHit = hitBuffer[i];
                found = true;
            }
        }

        if (found) return true;

        count = Physics.RaycastNonAlloc(ray, hitBuffer, range + 0.25f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = hitBuffer[i].collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (hitBuffer[i].distance < minDist)
            {
                minDist = hitBuffer[i].distance;
                bestHit = hitBuffer[i];
                found = true;
            }
        }
        return found;
    }

    private void ProcessHit(RaycastHit hit, int weaponId, float damage, bool isHeavy, float treeMult, float rockMult)
    {
        Collider col = hit.collider;
        if (col == null) return;

        string colName = col.gameObject.name.ToLower();
        string rootName = col.transform.root.gameObject.name.ToLower();

        bool isTree = !colName.Contains("stump") && (
                      colName.Contains("tree") || colName.Contains("trunk") || colName.Contains("foliage") ||
                      rootName.Contains("tree") || rootName.Contains("pine") || rootName.Contains("arvore") ||
                      col.GetComponentInParent<GalicianProceduralTreeGenerator>() != null);

        bool isRock = !isTree && IsRockCollider(col);

        if (isTree)
        {
            GameObject singleTree = FindSpecificTreeObject(col);
            if (singleTree != null)
            {
                HandleTreeChop(hit, singleTree, treeMult, isHeavy);
                DegradeEquippedTool();
            }
        }
        else if (isRock)
        {
            HandleRockStrike(hit, rockMult, isHeavy);
            DegradeEquippedTool();
        }
        else
        {
            HandleEntityStrike(hit, weaponId, damage, isHeavy);
        }
    }

    private GameObject FindSpecificTreeObject(Collider col)
    {
        if (col == null) return null;

        GalicianProceduralTreeGenerator gen = col.GetComponentInParent<GalicianProceduralTreeGenerator>();
        if (gen != null) return gen.gameObject;

        if (mapRendererCache == null) mapRendererCache = FindAnyObjectByType<MapRenderer>();
        Transform mapRoot = (mapRendererCache != null) ? mapRendererCache.transform : null;

        Transform cur = col.transform;
        while (cur != null && cur.parent != null)
        {
            if (cur.parent == mapRoot) return cur.gameObject;

            string pName = cur.parent.name.ToLower();
            if (pName.Contains("map") || pName.Contains("environment") || pName.Contains("scene") || cur.parent == cur.root)
                return cur.gameObject;

            cur = cur.parent;
        }
        return col.gameObject;
    }

    private void HandleTreeChop(RaycastHit hit, GameObject treeRoot, float treeMult, bool isHeavy)
    {
        if (audioSource != null && chopWoodClip != null)
        {
            audioSource.pitch = Random.Range(0.88f, 1.12f);
            audioSource.PlayOneShot(chopWoodClip, 0.85f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(isHeavy ? 0.14f : 0.08f);

        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.TriggerHitstop(isHeavy ? 0.045f : 0.025f, 0.12f);

        if (hapticCache != null) hapticCache.TriggerLightVibration();
        SpawnWoodChipsVFX(hit.point, hit.normal);

        int treeId = treeRoot.GetHashCode();
        if (!treeHitsRemaining.ContainsKey(treeId))
            treeHitsRemaining[treeId] = 4;

        int chopPower = (treeMult >= 2.8f ? 3 : (treeMult >= 1.8f ? 2 : 1)) + (isHeavy ? 1 : 0);
        treeHitsRemaining[treeId] -= chopPower;
        int remaining = Mathf.Max(0, treeHitsRemaining[treeId]);

        int woodGained = treeMult >= 2.8f ? 2 : 1;
        AddOrDrop(100, woodGained, hit.point);

        bool gotResin = Random.value < (treeMult >= 1.8f ? 0.35f : 0.20f);
        if (gotResin) AddOrDrop(119, 1, hit.point);

        if (remaining <= 0)
        {
            treeHitsRemaining.Remove(treeId);

            if (audioSource != null && treeFallClip != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(treeFallClip, 1.0f);
            }

            if (FirstPersonCamera.Instance != null)
                FirstPersonCamera.Instance.AddShake(0.24f);

            Vector3 treePos = treeRoot.transform.position;
            AddOrDrop(119, 1, treePos);
            bool gotHerbs = Random.value < 0.65f;
            if (gotHerbs) AddOrDrop(103, 1, treePos);

            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: false);
                HUDSurvivalOverlay.Instance.ShowLootTooltip(gotHerbs
                    ? "Arvore derrubada! +1 Resina, +1 Ervas - Colete as toras"
                    : "Arvore derrubada! +1 Resina - Colete as toras no chao");
            }

            SpawnTreeStump(treePos);
            Vector3 fallDir = (treePos - transform.position);
            fallDir.y = 0f;
            if (fallDir.sqrMagnitude < 0.01f) fallDir = transform.forward;
            fallDir.Normalize();

            int gridX = Mathf.RoundToInt(treePos.x);
            int gridZ = Mathf.RoundToInt(treePos.z);
            if (mapRendererCache != null) mapRendererCache.RemoveInstanceAt(gridX, gridZ, 1);

            StartCoroutine(AnimateTreeFallAndDestroy(treeRoot, fallDir));
        }
        else
        {
            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: isHeavy, isKill: false);
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{woodGained} Madeira{(gotResin ? " +1 Resina" : "")} (Corte {4 - remaining}/4)");
            }

            StartCoroutine(ShakeTreeTrunk(treeRoot.transform));
        }
    }

    private void SpawnTreeStump(Vector3 treePos)
    {
        float gy = TerrainMeshGenerator.GetTerrainHeight(treePos.x, treePos.z);

        GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stump.name = "Stump_Cleared";
        stump.transform.position = new Vector3(treePos.x, gy + 0.24f, treePos.z);
        stump.transform.localScale = new Vector3(0.46f, 0.28f, 0.46f);

        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        if (barkMat != null) stump.GetComponent<MeshRenderer>().sharedMaterial = barkMat;

        GameObject cutTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cutTop.name = "Stump_CutFace";
        Destroy(cutTop.GetComponent<Collider>());
        cutTop.transform.SetParent(stump.transform, false);
        cutTop.transform.localPosition = new Vector3(0f, 0.96f, 0f);
        cutTop.transform.localScale = new Vector3(0.92f, 0.08f, 0.92f);

        if (cachedStumpTopMat == null)
        {
            cachedStumpTopMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            cachedStumpTopMat.color = new Color(0.68f, 0.50f, 0.30f);
            cachedStumpTopMat.SetFloat("_Smoothness", 0.22f);
        }
        cutTop.GetComponent<MeshRenderer>().sharedMaterial = cachedStumpTopMat;
    }

    private void HandleRockStrike(RaycastHit hit, float rockMult, bool isHeavy)
    {
        if (audioSource != null && hitRockClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.15f);
            audioSource.PlayOneShot(hitRockClip, 0.78f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(isHeavy ? 0.11f : 0.06f);

        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.TriggerHitstop(0.028f, 0.14f);

        if (hapticCache != null) hapticCache.TriggerLightVibration();
        if (VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.PlaySparkVFX(hit.point, hit.normal);

        MineableRockNode node = hit.collider != null ? hit.collider.GetComponentInParent<MineableRockNode>() : null;
        if (node != null && node.isPermanent && !node.TryConsumeStrike(out _, out int waitSec))
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Monumento esgotado temporariamente - aguarde {waitSec}s");
            return;
        }

        int stoneYield = rockMult >= 2.3f ? (isHeavy ? 3 : 2) : (rockMult >= 1.4f ? 2 : 1);
        float ironChance = rockMult >= 2.0f ? 0.65f : 0.22f;
        float saltChance = rockMult >= 2.0f ? 0.42f : 0.16f;

        bool gotIron = Random.value < ironChance;
        bool gotSalt = Random.value < saltChance;

        AddOrDrop(101, stoneYield, hit.point);
        if (gotIron) AddOrDrop(107, 1, hit.point);
        if (gotSalt) AddOrDrop(106, 1, hit.point);

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: rockMult >= 2.0f, isKill: false);
            string bonus = "";
            if (gotIron) bonus += ", +1 Ferro";
            if (gotSalt) bonus += ", +1 Sal";
            string actionWord = rockMult <= 1.05f ? "lascada (Maos)" : "minerada";
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{stoneYield} Pedra{bonus} {actionWord}");
        }
    }

    private void AddOrDrop(int itemId, int amount, Vector3 dropPos)
    {
        if (amount <= 0) return;
        int leftover = inventory != null ? inventory.AddItem(itemId, amount) : amount;
        if (leftover > 0 && LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(itemId, leftover, dropPos);
        }
    }

    private void HandleEntityStrike(RaycastHit hit, int weaponId, float actualDamage, bool isHeavy)
    {
        bool parryCrit = false;
        WildlifeAIEngine wildlife = hit.collider.GetComponentInParent<WildlifeAIEngine>();
        bool isTerrainOrGround = hit.collider.gameObject.name.ToLower().Contains("terrain");

        if (!isTerrainOrGround && PlayerMeleeCombatDefense.Instance != null && PlayerMeleeCombatDefense.Instance.hasParryBonus)
        {
            actualDamage *= PlayerMeleeCombatDefense.Instance.counterMultiplier;
            PlayerMeleeCombatDefense.Instance.ConsumeParryBonus();
            parryCrit = true;
        }

        if (isTerrainOrGround)
        {
            if (audioSource != null && hitRockClip != null)
            {
                audioSource.pitch = Random.Range(0.65f, 0.82f);
                audioSource.PlayOneShot(hitRockClip, 0.36f);
            }
            if (VFXPoolManager.Instance != null)
                VFXPoolManager.Instance.PlayBuildPlaceVFX(hit.point);
        }
        else if (audioSource != null && hitFleshClip != null)
        {
            audioSource.pitch = Random.Range(0.90f, 1.10f);
            audioSource.PlayOneShot(hitFleshClip, 0.85f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(parryCrit ? 0.22f : (isHeavy ? 0.15f : 0.09f));

        if (!isTerrainOrGround && GameFeelManager.Instance != null)
            GameFeelManager.Instance.TriggerHitstop(parryCrit || isHeavy ? 0.055f : 0.032f, 0.08f);

        if (VFXPoolManager.Instance != null)
        {
            if (wildlife != null)
            {
                VFXPoolManager.Instance.PlayBloodSplashVFX(hit.point, hit.normal);
                VFXPoolManager.Instance.PlayBloodDecalVFX(hit.point, false);
            }
            else
                VFXPoolManager.Instance.PlayBuildPlaceVFX(hit.point);
        }

        if (!isTerrainOrGround)
        {
            hit.collider.SendMessageUpwards("TakeDamage", actualDamage, SendMessageOptions.DontRequireReceiver);
            DegradeEquippedTool();

            if (HUDSurvivalOverlay.Instance != null)
            {
                bool killed = wildlife != null && wildlife.currentState == WildlifeAIEngine.AIState.Dead;
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: parryCrit || isHeavy, isKill: killed);

                if (parryCrit)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#55FFDD>CRITICO DE PARRY! ({actualDamage:F0} Dano)</color>");
                else if (isHeavy)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"Golpe Pesado! ({actualDamage:F0} Dano)");
                else if (weaponId == 302)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"Corte com Faca ({actualDamage:F0} Dano)");
            }
        }
    }

    private System.Collections.IEnumerator ShakeTreeTrunk(Transform treeTrunk)
    {
        if (treeTrunk == null) yield break;
        Vector3 initialPos = treeTrunk.position;
        float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (treeTrunk == null) yield break;
            elapsed += Time.deltaTime;
            float strength = (1f - (elapsed / duration)) * 0.04f;
            treeTrunk.position = initialPos + new Vector3(Random.Range(-strength, strength), 0, Random.Range(-strength, strength));
            yield return null;
        }
        if (treeTrunk != null) treeTrunk.position = initialPos;
    }

    private System.Collections.IEnumerator AnimateTreeFallAndDestroy(GameObject treeObj)
    {
        yield return AnimateTreeFallAndDestroy(treeObj, transform.forward);
    }

    private System.Collections.IEnumerator AnimateTreeFallAndDestroy(GameObject treeObj, Vector3 fallDir)
    {
        if (treeObj == null) yield break;

        Collider[] cols = treeObj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].enabled = false;
        }

        Vector3 toTree = fallDir;
        toTree.y = 0f;
        if (toTree.sqrMagnitude < 0.01f) toTree = transform.forward;
        toTree.Normalize();
        Vector3 fallAxis = Vector3.Cross(Vector3.up, toTree);
        if (fallAxis.sqrMagnitude < 0.01f) fallAxis = Vector3.right;

        float elapsed = 0f;
        float duration = 1.15f;
        Quaternion startRot = treeObj.transform.rotation;
        Vector3 basePos = treeObj.transform.position;

        while (elapsed < duration)
        {
            if (treeObj == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float ease = t * t * t;
            float angle = Mathf.Lerp(0f, 88f, ease);
            treeObj.transform.rotation = startRot * Quaternion.AngleAxis(angle, fallAxis);
            yield return null;
        }

        // Impacto no solo: rebote sutil + estilhacos + toras fisicas rolantes
        float bounceElapsed = 0f;
        while (bounceElapsed < 0.22f && treeObj != null)
        {
            bounceElapsed += Time.deltaTime;
            float b = Mathf.Sin((bounceElapsed / 0.22f) * Mathf.PI) * 4.5f;
            treeObj.transform.rotation = startRot * Quaternion.AngleAxis(88f - b, fallAxis);
            yield return null;
        }

        Vector3 impactCenter = basePos + toTree * 2.6f;
        impactCenter.y = TerrainMeshGenerator.GetTerrainHeight(impactCenter.x, impactCenter.z) + 0.35f;
        SpawnWoodChipsVFX(impactCenter, Vector3.up);
        if (FirstPersonCamera.Instance != null) FirstPersonCamera.Instance.AddShake(0.18f);
        SpawnFelledTreeLogs(basePos, toTree);

        yield return new WaitForSeconds(0.55f);
        if (treeObj != null) Destroy(treeObj);
    }

    private void SpawnFelledTreeLogs(Vector3 treePos, Vector3 fallDir)
    {
        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");

        for (int i = 0; i < 2; i++)
        {
            float along = 1.6f + i * 1.45f;
            Vector3 side = Vector3.Cross(Vector3.up, fallDir) * Random.Range(-0.45f, 0.45f);
            Vector3 spawnPos = treePos + fallDir * along + side;
            spawnPos.y = TerrainMeshGenerator.GetTerrainHeight(spawnPos.x, spawnPos.z) + 0.42f;

            GameObject logObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            logObj.name = "Felled_Wood_Log";
            logObj.transform.position = spawnPos;
            logObj.transform.localScale = new Vector3(0.24f, 0.62f, 0.24f);
            logObj.transform.rotation = Quaternion.LookRotation(fallDir) * Quaternion.Euler(90f, Random.Range(-18f, 18f), 0f);

            if (barkMat != null) logObj.GetComponent<MeshRenderer>().sharedMaterial = barkMat;

            CapsuleCollider physCol = logObj.GetComponent<CapsuleCollider>();
            if (physCol != null) physCol.isTrigger = false;

            SphereCollider pickupTrig = logObj.AddComponent<SphereCollider>();
            pickupTrig.radius = 0.65f;
            pickupTrig.isTrigger = true;

            Rigidbody rb = logObj.AddComponent<Rigidbody>();
            rb.mass = 24f;
            rb.linearDamping = 1.2f;
            rb.angularDamping = 1.4f;
            rb.AddForce(Vector3.up * 2.1f + side * 2.2f, ForceMode.Impulse);

            ResourcePickup pickup = logObj.AddComponent<ResourcePickup>();
            pickup.itemID = 100;
            pickup.amount = 2;
        }
    }

    private void SpawnWoodChipsVFX(Vector3 pos, Vector3 normal)
    {
        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayWoodChipVFX(pos, normal);
            return;
        }

        GameObject vfxObj = new GameObject("WoodChipsBurst");
        vfxObj.transform.position = pos;
        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.4f;
        main.startLifetime = 0.5f;
        main.startSpeed = 4.5f;
        main.startSize = 0.08f;
        main.startColor = new Color(0.65f, 0.48f, 0.25f);
        main.loop = false;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.rotation = Quaternion.LookRotation(normal).eulerAngles;

        Destroy(vfxObj, 0.8f);
    }

    private AudioClip GenerateChopWoodSound()
    {
        int sr = 44100; int len = (int)(sr * 0.24f); float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            samples[i] = (Mathf.Sin(2f * Mathf.PI * 110f * ((float)i / sr)) * Mathf.Exp(-18f * t) * 0.65f + Random.Range(-1f, 1f) * Mathf.Exp(-8f * t) * 0.45f) * 0.95f;
        }
        AudioClip clip = AudioClip.Create("AxeChopWood_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateTreeFallSound()
    {
        int sr = 44100; int len = (int)(sr * 1.25f); float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float crack = (t < 0.35f) ? Random.Range(-0.8f, 0.8f) * Mathf.Sin(t * 120f) : 0f;
            float crash = (t >= 0.35f) ? Mathf.Sin(2f * Mathf.PI * 55f * ((float)i / sr)) * Mathf.Exp(-4f * (t - 0.35f)) : 0f;
            samples[i] = Mathf.Clamp(crack * 0.7f + crash * 0.85f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("TreeFall_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateRockHitSound()
    {
        int sr = 44100; int len = (int)(sr * 0.18f); float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len; float env = Mathf.Exp(-24f * t);
            samples[i] = Mathf.Sin(2f * Mathf.PI * 720f * ((float)i / sr)) * env * 0.7f + Random.Range(-0.3f, 0.3f) * env;
        }
        AudioClip clip = AudioClip.Create("RockHit_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateSwingWhooshSound()
    {
        int sr = 44100; int len = (int)(sr * 0.16f); float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            samples[i] = Random.Range(-1f, 1f) * Mathf.Sin(t * Mathf.PI) * 0.35f;
        }
        AudioClip clip = AudioClip.Create("AxeWhoosh_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateFleshHitSound()
    {
        int sr = 44100; int len = (int)(sr * 0.14f); float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len; float env = Mathf.Exp(-16f * t);
            samples[i] = (Mathf.Sin(2f * Mathf.PI * 140f * ((float)i / sr)) * 0.65f + Random.Range(-0.4f, 0.4f) * 0.35f) * env;
        }
        AudioClip clip = AudioClip.Create("FleshHit_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
