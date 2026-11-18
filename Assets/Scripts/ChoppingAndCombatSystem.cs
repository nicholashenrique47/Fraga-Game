using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ChoppingAndCombatSystem: Sistema unificado de Combate Corpo a Corpo, Corte de Arvores, Mineracao e Coleta de Recursos.
/// - Regras estritas estilo The Forest:
///   * Madeira de Arvores e Troncos Caidos SO pode ser cortada com Machado (300/312) — funciona com Clique Esquerdo, Clique Direito ou [E]!
///   * Rochas e Pedregulhos grandes SO podem ser quebrados com Machado (300/312) ou Picareta (301) — funciona com Clique Esquerdo, Clique Direito ou [E]!
///   * Se o jogador ja fabricou o Machado/Picareta e ele esta na mochila, clicar na arvore/rocha equipa a ferramenta automaticamente!
///   * Sem ferramentas, apenas Pedras Soltas no chao, Galhos Soltos no chao e Plantas/Ervas/Bagas podem ser coletados com [E]!
/// </summary>
public class ChoppingAndCombatSystem : MonoBehaviour
{
    public static ChoppingAndCombatSystem Instance { get; private set; }

    [Header("Fallback Fists Settings")]
    public float baseAttackRange = 3.20f;
    public float attackRadius = 0.42f;
    public float baseAttackCooldown = 0.34f;
    public float baseDamage = 12f;

    [Header("Dependencies")]
    public Camera playerCamera;
    public InventoryAdvanced inventory;
    public PlayerController playerController;
    public PlayerStatus playerStatus;

    private float lastAttackTime = -10f;
    private int comboStep = 0;
    private float comboResetTimer = 0f;

    private AudioSource audioSource;
    private AudioClip chopWoodClip;
    private AudioClip treeFallClip;
    private AudioClip hitRockClip;
    private AudioClip swingWhooshClip;
    private AudioClip hitFleshClip;

    private MapRenderer mapRendererCache;
    private ResourceManager resourceManagerCache;
    private HapticManager hapticCache;
    private Material cachedStumpTopMat;

    private readonly Dictionary<int, int> treeHitsRemaining = new Dictionary<int, int>();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[24];
    private readonly Collider[] nearBuffer = new Collider[24];

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
        resourceManagerCache = FindAnyObjectByType<ResourceManager>();
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

    public static bool IsAxeEquipped(InventoryAdvanced inv)
    {
        return inv != null && (inv.equippedMainHandId == 300 || inv.equippedMainHandId == 312);
    }

    public static bool IsRockToolEquipped(InventoryAdvanced inv)
    {
        return inv != null && (inv.equippedMainHandId == 300 || inv.equippedMainHandId == 312 || inv.equippedMainHandId == 301);
    }

    /// <summary>
    /// Se o jogador ja estiver com o Machado equipado retorna true; se tiver Machado utilizavel na mochila e estiver desarmado/com outra ferramenta basica,
    /// equipa o Machado automaticamente para que o clique funcione na hora!
    /// </summary>
    public static bool EnsureAxeEquipped(InventoryAdvanced inv, bool autoEquipFromBag = true)
    {
        if (inv == null) return false;
        if (IsAxeEquipped(inv)) return true;
        if (!autoEquipFromBag) return false;

        if (inv.FindUsableEquipmentSlot(312) >= 0 && inv.EquipItem(312))
            return true;
        if (inv.FindUsableEquipmentSlot(300) >= 0 && inv.EquipItem(300))
            return true;
        return false;
    }

    /// <summary>
    /// Garante Machado ou Picareta equipada para quebrar rochas (equipa da mochila se disponivel).
    /// </summary>
    public static bool EnsureRockToolEquipped(InventoryAdvanced inv, bool autoEquipFromBag = true)
    {
        if (inv == null) return false;
        if (IsRockToolEquipped(inv)) return true;
        if (!autoEquipFromBag) return false;

        if (inv.FindUsableEquipmentSlot(301) >= 0 && inv.EquipItem(301))
            return true;
        if (inv.FindUsableEquipmentSlot(312) >= 0 && inv.EquipItem(312))
            return true;
        if (inv.FindUsableEquipmentSlot(300) >= 0 && inv.EquipItem(300))
            return true;
        return false;
    }

    public bool TryCheckRightClickHarvestWarning()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return false;
        if (inventory == null) inventory = InventoryAdvanced.Instance;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!TryFindBestHit(ray, baseAttackRange + 0.65f, out RaycastHit hit) || hit.collider == null)
            return false;

        if (IsTreeCollider(hit.collider) || IsFallenLogCollider(hit.collider))
        {
            if (EnsureAxeEquipped(inventory, true))
            {
                TriggerAttack(false, true);
                return true;
            }
            ShowToolRequiredForWoodWarning();
            return true;
        }
        if (IsRockCollider(hit.collider))
        {
            if (EnsureRockToolEquipped(inventory, true))
            {
                TriggerAttack(false, true);
                return true;
            }
            ShowToolRequiredForRockWarning();
            return true;
        }
        return false;
    }

    public void ShowToolRequiredForWoodWarning()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        bool hasAxeInBag = inventory != null && (inventory.HasItem(300) || inventory.HasItem(312));
        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(hasAxeInBag
                ? "<color=#FFCC44>Equipe o Machado na mao (Tecla [1] ou Mochila [I]) e clique para cortar!</color>"
                : "<color=#FFBB44>Precisa de MACHADO para cortar arvores! Pegue Galhos, Pedras e Ervas no chao com [E] e crie no menu [I].</color>");
        }
    }

    public void ShowToolRequiredForRockWarning()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        bool hasToolInBag = inventory != null && (inventory.HasItem(300) || inventory.HasItem(312) || inventory.HasItem(301));
        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(hasToolInBag
                ? "<color=#FFCC44>Equipe o Machado ou Picareta na mao e clique para quebrar rochas!</color>"
                : "<color=#FFBB44>Precisa de MACHADO ou PICARETA para quebrar rochas! Sem ferramentas, pegue apenas Pedras Soltas no chao com [E].</color>");
        }
    }

    public void TriggerAttack()
    {
        TriggerAttack(false, false);
    }

    public void TriggerAttack(bool isHeavy)
    {
        TriggerAttack(isHeavy, false);
    }

    public void TriggerAttack(bool isHeavy, bool fromRightMouse)
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

        float staminaCost = (weaponData != null ? weaponData.staminaCost : 6f) * (isHeavy ? 1.55f : 1f);
        if (playerStatus != null && !playerStatus.CanSpendStamina(4f))
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip(playerStatus.isExhausted
                    ? "<color=#FF8844>Exausto! Aguarde o folego recuperar.</color>"
                    : "Sem folego para golpear!");
            return;
        }
        if (playerStatus != null)
        {
            playerStatus.stamina = Mathf.Max(0f, playerStatus.stamina - staminaCost);
            if (playerStatus.stamina <= 0.5f) playerStatus.isExhausted = true;
        }

        if (Time.time - lastAttackTime > 1.4f)
        {
            comboStep = 0;
        }
        else
        {
            comboStep = (comboStep + 1) % 3;
        }
        lastAttackTime = Time.time;
        comboResetTimer = 1.4f;
        bool isFinisher = comboStep == 2;

        if (FirstPersonViewmodelController.Instance != null)
        {
            FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(comboStep, isHeavy);
        }

        if (FragaStealthSystem.Instance != null)
        {
            FragaStealthSystem.Instance.EmitNoisePulse(isHeavy ? 0.85f : 0.55f);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(isHeavy ? AudioManager.FoleyEventType.SwingHeavy : AudioManager.FoleyEventType.SwingLight, transform.position, isHeavy ? 0.64f : 0.46f, 0.90f, 1.10f);
        }
        else if (audioSource != null && swingWhooshClip != null)
        {
            audioSource.pitch = isHeavy ? Random.Range(0.76f, 0.88f) : Random.Range(0.95f, 1.12f);
            audioSource.PlayOneShot(swingWhooshClip, isHeavy ? 0.55f : 0.38f);
        }

        if (playerCamera == null) return;

        float range = weaponData != null ? Mathf.Max(baseAttackRange, weaponData.attackRange + 0.65f) : baseAttackRange;
        if (weaponId == 308) range += 0.55f; // Lanca Celta tem alcance estendido

        float damage = (weaponData != null ? weaponData.attackDamage : baseDamage) * (isHeavy ? 1.65f : 1f);
        if (isFinisher) damage *= 1.28f;

        bool isStealthAmbush = FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.IsHidden;
        float stealthMult = isStealthAmbush ? (weaponId == 302 ? 2.55f : 2.25f) : 1f;
        bool hasTorchOffhand = inventory != null && inventory.equippedOffHandId == 303;
        float treeMult = weaponData != null ? weaponData.harvestTreeMultiplier : 1f;
        float rockMult = weaponData != null ? weaponData.harvestRockMultiplier : 1f;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool hasColliderHit = TryFindBestHit(ray, range, out RaycastHit bestHit);

        float obstacleLimit = range + 0.45f;
        if (hasColliderHit && bestHit.collider != null && IsStaticObstacle(bestHit.collider))
        {
            obstacleLimit = Mathf.Min(obstacleLimit, bestHit.distance + 0.45f);
        }

        // 1. Prioriza entidades sombrias (Vultos GPU-Instanced) no cone frontal
        if (VultoManager.Instance != null)
        {
            float vultoDmg = damage * stealthMult * (hasTorchOffhand ? 1.25f : 1f);
            float previewDamage = PreviewParryDamage(vultoDmg, out _);
            if (VultoManager.Instance.TryDamageVultoInRay(ray, obstacleLimit, attackRadius + 0.28f, previewDamage, ray.direction, out Vector3 vultoHitPos, out bool vultoKilled))
            {
                bool parryCrit = ConsumeParryCrit();
                HandleLivingTargetFeedback(vultoHitPos, Vector3.up, previewDamage, parryCrit, isHeavy || isFinisher, vultoKilled, isShadow: true, isStealthCrit: isStealthAmbush);
                DegradeEquippedTool();
                return;
            }
        }

        // 2. Verifica animais controlados pelo FaunaManager (SoA nativo / procedural)
        if (FaunaManager.Instance != null)
        {
            float faunaDmg = damage * stealthMult;
            float previewDamage = PreviewParryDamage(faunaDmg, out _);
            if (FaunaManager.Instance.TryDamageFaunaInRay(ray, obstacleLimit, attackRadius + 0.22f, previewDamage, out Vector3 faunaHitPos, out bool faunaKilled))
            {
                bool parryCrit = ConsumeParryCrit();
                HandleLivingTargetFeedback(faunaHitPos, Vector3.up, previewDamage, parryCrit, isHeavy || isFinisher, faunaKilled, isShadow: false, isStealthCrit: isStealthAmbush);
                DegradeEquippedTool();
                return;
            }
        }

        // 3. Processa colisoes fisicas e recursos (Arvores, Rochas, Troncos, Plantas, Pickups, Blueprints, WildlifeAIEngine)
        if (hasColliderHit)
        {
            ProcessHit(bestHit, weaponId, damage, isHeavy || isFinisher, treeMult, rockMult, fromRightMouse);
            return;
        }

        // 4. Fallback em Grid Nativo: caso a arvore/rocha a frente esteja no grid nativo
        if (resourceManagerCache == null) resourceManagerCache = FindAnyObjectByType<ResourceManager>();
        if (resourceManagerCache != null && resourceManagerCache.MapDataPtr != System.IntPtr.Zero)
        {
            Vector3 fwd = playerCamera.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = transform.forward;
            fwd.Normalize();
            Vector3 checkPos = transform.position + fwd * 1.75f;
            int gx = Mathf.RoundToInt(checkPos.x);
            int gz = Mathf.RoundToInt(checkPos.z);
            if (gx >= 0 && gx < resourceManagerCache.MapWidth && gz >= 0 && gz < resourceManagerCache.MapHeight)
            {
                int tile = ResourceManager.GetMapCell(resourceManagerCache.MapDataPtr, resourceManagerCache.MapWidth, resourceManagerCache.MapHeight, gx, gz);
                if (tile == 1 || tile == 2 || tile == 8)
                {
                    resourceManagerCache.TryHarvestAtGrid(gx, gz);
                }
            }
        }
    }

    /// <summary>
    /// Chamado quando o jogador aperta [E] ou botao Interagir em uma Arvore.
    /// Executa o corte diretamente se tiver Machado equipado (ou na mochila), tanto no PC quanto no Mobile!
    /// </summary>
    public void HarvestTreeFromInteraction(Collider col)
    {
        if (col == null) return;
        if (inventory == null) inventory = InventoryAdvanced.Instance;

        if (!EnsureAxeEquipped(inventory, true))
        {
            ShowToolRequiredForWoodWarning();
            return;
        }

        if (Time.time < lastAttackTime + 0.36f) return;

        if (playerStatus == null) playerStatus = PlayerStatus.Instance;
        if (playerStatus != null)
        {
            if (!playerStatus.CanSpendStamina(4f))
            {
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip("Sem folego para cortar!");
                return;
            }
            playerStatus.stamina = Mathf.Max(0f, playerStatus.stamina - 6f);
            if (playerStatus.stamina <= 0.5f) playerStatus.isExhausted = true;
        }
        lastAttackTime = Time.time;

        if (FirstPersonViewmodelController.Instance != null)
        {
            comboStep = (comboStep + 1) % 2;
            FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(comboStep, false);
        }

        ItemData weaponData = ItemDatabase.GetItem(inventory.equippedMainHandId);
        float treeMult = weaponData != null && weaponData.harvestTreeMultiplier > 1.01f ? weaponData.harvestTreeMultiplier : 2.2f;

        GameObject singleTree = FindSpecificTreeObject(col);
        if (singleTree != null)
        {
            Vector3 hitPoint = col.ClosestPoint(transform.position + Vector3.up * 1.1f);
            HandleTreeChopAtPoint(hitPoint, Vector3.up, singleTree, treeMult, false);
            DegradeEquippedTool();
        }
    }

    public static bool IsTreeCollider(Collider col)
    {
        if (col == null) return false;
        string colName = col.gameObject.name.ToLower();
        if (colName.Contains("stump") || colName.Contains("fallen") || colName.Contains("pickup") || colName.Contains("felled"))
            return false;

        if (col.GetComponentInParent<GalicianProceduralTreeGenerator>() != null)
            return true;

        Transform cur = col.transform;
        for (int depth = 0; depth < 4 && cur != null; depth++)
        {
            string n = cur.name.ToLower();
            if (n.StartsWith("chunk_") || n == "environment_instances" || n == "landmarks_global")
                break;
            if (n.Contains("tree") || n.Contains("trunk") || n.Contains("foliage") || n.Contains("pine") || n.Contains("arvore"))
                return true;
            cur = cur.parent;
        }
        return false;
    }

    public static bool IsFallenLogCollider(Collider col)
    {
        if (col == null) return false;
        Transform cur = col.transform;
        for (int depth = 0; depth < 4 && cur != null; depth++)
        {
            string n = cur.name.ToLower();
            if (n.StartsWith("chunk_") || n == "environment_instances" || n == "landmarks_global")
                break;
            if (n.Contains("fallenlog") || n.Contains("fallen_log") || n.Contains("felled_wood_log") || n.Contains("droppedwood"))
                return true;
            cur = cur.parent;
        }
        return false;
    }

    public static bool IsHarvestablePlantCollider(Collider col, out int plantKind)
    {
        plantKind = 0; // 9=Berry, 10=Mushroom/Fern, 19=ThornyBush, 20=GlowMushroom
        if (col == null) return false;

        Transform cur = col.transform;
        for (int depth = 0; depth < 4 && cur != null; depth++)
        {
            string n = cur.name.ToLower();
            if (n.StartsWith("chunk_") || n == "environment_instances" || n == "landmarks_global")
                break;
            if (n.Contains("berry")) { plantKind = 9; return true; }
            if (n.Contains("glowmushroom") || n.Contains("glow_mushroom")) { plantKind = 20; return true; }
            if (n.Contains("mushroom")) { plantKind = 10; return true; }
            if (n.Contains("thorny") || n.Contains("bush")) { plantKind = 19; return true; }
            if (n.Contains("fento") || n.Contains("fern")) { plantKind = 10; return true; }
            cur = cur.parent;
        }
        return false;
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
                n.IndexOf("shelter", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("roundhouse", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("blueprint", System.StringComparison.OrdinalIgnoreCase) >= 0)
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
        HandleLivingTargetFeedback(hitPos, normal, actualDamage, parryCrit, isHeavy, killed, isShadow, false);
    }

    private void HandleLivingTargetFeedback(Vector3 hitPos, Vector3 normal, float actualDamage, bool parryCrit, bool isHeavy, bool killed, bool isShadow, bool isStealthCrit)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(isShadow ? AudioManager.FoleyEventType.HitShadow : AudioManager.FoleyEventType.HitFlesh, hitPos, isShadow ? 0.78f : 0.88f, isShadow ? 0.84f : 0.92f, 1.12f);
        }
        else if (audioSource != null && hitFleshClip != null)
        {
            audioSource.pitch = isShadow ? Random.Range(0.72f, 0.88f) : Random.Range(0.92f, 1.12f);
            audioSource.PlayOneShot(hitFleshClip, isShadow ? 0.72f : 0.90f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake((parryCrit || isStealthCrit) ? 0.22f : (isHeavy ? 0.15f : 0.09f));

        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.TriggerHitstop((parryCrit || isStealthCrit || killed) ? 0.055f : (isHeavy ? 0.042f : 0.028f), 0.09f);

        if (hapticCache != null)
        {
            if (killed || parryCrit || isStealthCrit) hapticCache.TriggerHeavyVibration();
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
            HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: parryCrit || isStealthCrit || isHeavy, isKill: killed);

            if (killed)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip(isShadow
                    ? "<color=#BB88FF>Vulto dissipado na escuridao!</color>"
                    : "<color=#55FF88>Presa abatida!</color>");
            }
            else if (isStealthCrit)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#44FFCC>EMBOSCADA FURTIVA! ({actualDamage:F0} Dano)</color>");
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
        float minHarvestOrEnemyDist = float.MaxValue;
        RaycastHit bestHarvestOrEnemy = default;
        bool foundHarvestOrEnemy = false;

        float minNonTerrainDist = float.MaxValue;
        RaycastHit bestNonTerrain = default;
        bool foundNonTerrain = false;

        float minTerrainDist = float.MaxValue;
        RaycastHit bestTerrain = default;
        bool foundTerrain = false;

        int count = Physics.SphereCastNonAlloc(ray, attackRadius, hitBuffer, range + 0.65f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider c = hitBuffer[i].collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;

            bool isTreeOrLog = IsTreeCollider(c) || IsFallenLogCollider(c);
            bool isRock = IsRockCollider(c);
            bool isPlant = IsHarvestablePlantCollider(c, out _);
            bool isBlueprint = c.GetComponentInParent<WorldBlueprintSite>() != null;
            bool isEnemy = c.GetComponentInParent<WildlifeAIEngine>() != null || c.GetComponentInParent<BossSystem>() != null;
            bool isPickup = c.GetComponent<ResourcePickup>() != null;

            if (c.isTrigger && !isPickup && !isPlant && !isTreeOrLog && !isBlueprint)
                continue;

            bool isTerrain = c.gameObject.name.IndexOf("terrain", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isTerrain)
            {
                if (hitBuffer[i].distance < minTerrainDist)
                {
                    minTerrainDist = hitBuffer[i].distance;
                    bestTerrain = hitBuffer[i];
                    foundTerrain = true;
                }
            }
            else if (isTreeOrLog || isRock || isEnemy || isBlueprint)
            {
                if (hitBuffer[i].distance < minHarvestOrEnemyDist)
                {
                    minHarvestOrEnemyDist = hitBuffer[i].distance;
                    bestHarvestOrEnemy = hitBuffer[i];
                    foundHarvestOrEnemy = true;
                }
            }
            else
            {
                if (hitBuffer[i].distance < minNonTerrainDist)
                {
                    minNonTerrainDist = hitBuffer[i].distance;
                    bestNonTerrain = hitBuffer[i];
                    foundNonTerrain = true;
                }
            }
        }

        if (foundHarvestOrEnemy)
        {
            bestHit = bestHarvestOrEnemy;
            return true;
        }

        // Fallback de Proximidade Frontal (OverlapSphere): caso o jogador esteja muito colado ao tronco da arvore, rocha ou blueprint
        // (onde o SphereCast da Unity ignora colliders que ja contem a origem do raio!)
        Vector3 probeCenter = ray.origin + ray.direction * 1.65f;
        int nearCount = Physics.OverlapSphereNonAlloc(probeCenter, 1.80f, nearBuffer, ~0, QueryTriggerInteraction.Collide);
        Collider bestOverlapCol = null;
        float bestOverlapScore = float.MaxValue;
        Vector3 bestOverlapPoint = probeCenter;

        for (int i = 0; i < nearCount; i++)
        {
            Collider c = nearBuffer[i];
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.gameObject.name.IndexOf("terrain", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            bool isTreeOrLog = IsTreeCollider(c) || IsFallenLogCollider(c);
            bool isRock = IsRockCollider(c);
            bool isBlueprint = c.GetComponentInParent<WorldBlueprintSite>() != null;
            bool isEnemy = c.GetComponentInParent<WildlifeAIEngine>() != null || c.GetComponentInParent<BossSystem>() != null;
            bool isPlant = IsHarvestablePlantCollider(c, out _);

            if (!isTreeOrLog && !isRock && !isBlueprint && !isEnemy && !isPlant) continue;

            Vector3 closestPt = c.ClosestPoint(ray.origin);
            Vector3 toPt = closestPt - ray.origin;
            float dist = toPt.magnitude;
            float align = dist > 0.15f ? Vector3.Dot(ray.direction, toPt / dist) : 1f;
            if (align < -0.25f) continue;

            float priorityBonus = (isTreeOrLog || isRock || isEnemy) ? -0.8f : 0f;
            float score = dist - (align * 1.5f) + priorityBonus;
            if (score < bestOverlapScore)
            {
                bestOverlapScore = score;
                bestOverlapCol = c;
                bestOverlapPoint = closestPt;
            }
        }

        if (bestOverlapCol != null)
        {
            // Constroi um RaycastHit valido ou faz um Raycast curto em direcao ao collider encontrado
            Vector3 dirToCol = (bestOverlapCol.bounds.center - ray.origin);
            if (dirToCol.sqrMagnitude > 0.001f && bestOverlapCol.Raycast(new Ray(ray.origin - dirToCol.normalized * 0.5f, dirToCol.normalized), out RaycastHit directHit, range + 2.5f))
            {
                bestHit = directHit;
                return true;
            }
            if (foundNonTerrain)
            {
                bestHit = bestNonTerrain;
                return true;
            }
            // Executa colheita direta para o collider sobreposto
            ProcessOverlapColliderDirectly(bestOverlapCol, bestOverlapPoint);
            return false;
        }

        if (foundNonTerrain)
        {
            bestHit = bestNonTerrain;
            return true;
        }

        if (foundTerrain)
        {
            bestHit = bestTerrain;
            return true;
        }

        count = Physics.RaycastNonAlloc(ray, hitBuffer, range + 0.65f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider c = hitBuffer[i].collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.isTrigger && c.GetComponent<ResourcePickup>() == null && !IsHarvestablePlantCollider(c, out _) && !IsFallenLogCollider(c))
                continue;

            if (hitBuffer[i].distance < minTerrainDist)
            {
                minTerrainDist = hitBuffer[i].distance;
                bestHit = hitBuffer[i];
                foundTerrain = true;
            }
        }
        return foundTerrain;
    }

    private void ProcessOverlapColliderDirectly(Collider col, Vector3 hitPoint)
    {
        if (col == null) return;
        if (inventory == null) inventory = InventoryAdvanced.Instance;

        WorldBlueprintSite bp = col.GetComponentInParent<WorldBlueprintSite>();
        if (bp != null)
        {
            bp.TryDepositResource(gameObject);
            return;
        }

        if (IsTreeCollider(col))
        {
            if (!EnsureAxeEquipped(inventory, true))
            {
                ShowToolRequiredForWoodWarning();
                return;
            }
            ItemData wd = ItemDatabase.GetItem(inventory.equippedMainHandId);
            float tm = wd != null ? Mathf.Max(2.0f, wd.harvestTreeMultiplier) : 2.2f;
            GameObject singleTree = FindSpecificTreeObject(col);
            if (singleTree != null)
            {
                HandleTreeChopAtPoint(hitPoint, Vector3.up, singleTree, tm, false);
                DegradeEquippedTool();
            }
            return;
        }

        if (IsFallenLogCollider(col))
        {
            if (!EnsureAxeEquipped(inventory, true))
            {
                ShowToolRequiredForWoodWarning();
                return;
            }
            ItemData wd = ItemDatabase.GetItem(inventory.equippedMainHandId);
            float tm = wd != null ? Mathf.Max(2.0f, wd.harvestTreeMultiplier) : 2.2f;
            HarvestFallenLogCollider(col, hitPoint, tm);
            return;
        }

        if (IsRockCollider(col))
        {
            if (!EnsureRockToolEquipped(inventory, true))
            {
                ShowToolRequiredForRockWarning();
                return;
            }
            ItemData wd = ItemDatabase.GetItem(inventory.equippedMainHandId);
            float rm = wd != null ? Mathf.Max(1.6f, wd.harvestRockMultiplier) : 1.6f;
            HandleRockStrikeAtPoint(col, hitPoint, Vector3.up, rm, false);
            DegradeEquippedTool();
            return;
        }

        if (IsHarvestablePlantCollider(col, out int plantKind))
        {
            HarvestPlantObject(col, plantKind, hitPoint);
        }
    }

    private void ProcessHit(RaycastHit hit, int weaponId, float damage, bool isHeavy, float treeMult, float rockMult, bool fromRightMouse)
    {
        Collider col = hit.collider;
        if (col == null) return;
        if (inventory == null) inventory = InventoryAdvanced.Instance;

        // 0. Se bateu em um Blueprint de Construcao (ex: Cabana de Toras / Refugio), deposita os recursos na obra!
        WorldBlueprintSite bpSite = col.GetComponentInParent<WorldBlueprintSite>();
        if (bpSite != null)
        {
            bpSite.TryDepositResource(gameObject);
            return;
        }

        // 1. Se clicou em um ResourcePickup no chao (Pedra Solta, Galho Solto): coleta manual!
        ResourcePickup pickup = col.GetComponent<ResourcePickup>() ?? col.GetComponentInParent<ResourcePickup>();
        if (pickup != null && inventory != null)
        {
            if (!col.gameObject.name.ToLower().Contains("felled_wood_log"))
            {
                int leftover = inventory.AddItem(pickup.itemID, pickup.amount);
                int collected = pickup.amount - leftover;
                if (collected > 0)
                {
                    ItemData d = ItemDatabase.GetItem(pickup.itemID);
                    int totalNow = inventory.GetItemCount(pickup.itemID);
                    if (HUDSurvivalOverlay.Instance != null)
                    {
                        HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: false);
                        HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{collected} {(d != null ? d.itemName : "Item")} coletado (Total na Mochila: {totalNow})");
                    }
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayPickupSoundForItem(pickup.itemID, pickup.transform.position);
                    if (FirstPersonViewmodelController.Instance != null)
                        FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
                }

                if (leftover > 0)
                {
                    pickup.amount = leftover;
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("Mochila cheia!");
                }
                else
                {
                    pickup.CompleteManualCollect();
                }
                return;
            }
        }

        // 2. Arvore: EXIGE Machado (300 ou 312) — aceita TANTO Clique Esquerdo quanto Clique Direito!
        if (IsTreeCollider(col))
        {
            if (!EnsureAxeEquipped(inventory, true))
            {
                ShowToolRequiredForWoodWarning();
                return;
            }

            ItemData eqData = ItemDatabase.GetItem(inventory.equippedMainHandId);
            float effectiveTreeMult = eqData != null ? Mathf.Max(2.0f, eqData.harvestTreeMultiplier) : Mathf.Max(2.0f, treeMult);

            GameObject singleTree = FindSpecificTreeObject(col);
            if (singleTree != null)
            {
                HandleTreeChop(hit, singleTree, effectiveTreeMult, isHeavy);
                DegradeEquippedTool();
            }
            return;
        }

        // 3. Rocha / Pedra Mineravel: EXIGE Machado (300/312) ou Picareta (301) — aceita TANTO Clique Esquerdo quanto Clique Direito!
        if (IsRockCollider(col))
        {
            if (!EnsureRockToolEquipped(inventory, true))
            {
                ShowToolRequiredForRockWarning();
                return;
            }

            ItemData eqData = ItemDatabase.GetItem(inventory.equippedMainHandId);
            float effectiveRockMult = eqData != null ? Mathf.Max(1.6f, eqData.harvestRockMultiplier) : Mathf.Max(1.6f, rockMult);

            HandleRockStrike(hit, effectiveRockMult, isHeavy);
            DegradeEquippedTool();
            return;
        }

        // 4. Tronco Caido: EXIGE Machado (300/312) — aceita TANTO Clique Esquerdo quanto Clique Direito!
        if (IsFallenLogCollider(col))
        {
            if (!EnsureAxeEquipped(inventory, true))
            {
                ShowToolRequiredForWoodWarning();
                return;
            }

            ItemData eqData = ItemDatabase.GetItem(inventory.equippedMainHandId);
            float effectiveTreeMult = eqData != null ? Mathf.Max(2.0f, eqData.harvestTreeMultiplier) : Mathf.Max(2.0f, treeMult);
            HarvestFallenLogCollider(col, hit.point, effectiveTreeMult);
            return;
        }

        // 5. Arbustos, Samambaias (Fentos), Bagas e Cogumelos (podem ser colhidos sem ferramenta!)
        if (IsHarvestablePlantCollider(col, out int plantKind))
        {
            HarvestPlantObject(col, plantKind, hit.point);
            return;
        }

        // 6. Entidades vivas, estruturas ou terreno
        HandleEntityStrike(hit, weaponId, damage, isHeavy);
    }

    public void HarvestFallenLogCollider(Collider col, Vector3 hitPoint, float treeMult)
    {
        if (col == null) return;
        if (inventory == null) inventory = InventoryAdvanced.Instance;

        if (!EnsureAxeEquipped(inventory, true))
        {
            ShowToolRequiredForWoodWarning();
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.ChopWood, hitPoint, 0.84f, 0.90f, 1.12f);
        }
        else if (audioSource != null && chopWoodClip != null)
        {
            audioSource.pitch = Random.Range(0.90f, 1.12f);
            audioSource.PlayOneShot(chopWoodClip, 0.85f);
        }
        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.TriggerImpactBounce(0.90f);
        SpawnWoodChipsVFX(hitPoint, Vector3.up);
        int logWood = treeMult >= 2.8f ? 3 : 2;
        AddOrDrop(100, logWood, hitPoint);
        bool gotResin = Random.value < 0.45f;
        if (gotResin) AddOrDrop(119, 1, hitPoint);
        int totalWood = inventory != null ? inventory.GetItemCount(100) : logWood;
        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: true);
            HUDSurvivalOverlay.Instance.ShowLootTooltip(gotResin
                ? $"+{logWood} Madeira (Total: {totalWood}), +1 Resina [Tronco Cortado]"
                : $"+{logWood} Madeira (Total: {totalWood}) [Tronco Cortado]");
        }

        GameObject logObj = FindSpecificPropObject(col);
        if (mapRendererCache == null) mapRendererCache = FindAnyObjectByType<MapRenderer>();
        if (mapRendererCache != null && logObj != null)
        {
            int gx = Mathf.RoundToInt(logObj.transform.position.x);
            int gz = Mathf.RoundToInt(logObj.transform.position.z);
            mapRendererCache.RemoveInstanceAt(gx, gz, 18);
        }
        if (logObj != null) Destroy(logObj);
        DegradeEquippedTool();
    }

    public void HarvestPlantObject(Collider col, int plantKind, Vector3 hitPos)
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (inventory == null || col == null) return;

        GameObject plantObj = FindSpecificPropObject(col);
        Vector3 pos = plantObj != null ? plantObj.transform.position : hitPos;
        int gx = Mathf.RoundToInt(pos.x);
        int gz = Mathf.RoundToInt(pos.z);

        if (VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.PlayBuildPlaceVFX(pos + Vector3.up * 0.35f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupHerb, pos, 0.72f);
        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.TriggerImpactBounce(0.55f);

        bool hasKnife = inventory.equippedMainHandId == 302;
        string bonusTag = hasKnife ? " [Bonus Faca]" : string.Empty;
        if (hasKnife) AddOrDrop(103, 1, pos);

        if (plantKind == 9) // Arbusto de Bagas
        {
            AddOrDrop(202, 2, pos);
            AddOrDrop(103, 1, pos);
            bool gotSeed = Random.value < 0.45f;
            if (gotSeed) AddOrDrop(112, 1, pos);
            int totalHerbs = inventory.GetItemCount(103);
            int totalBerries = inventory.GetItemCount(202);
            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: true);
                HUDSurvivalOverlay.Instance.ShowLootTooltip(gotSeed
                    ? $"+2 Bagas ({totalBerries}), +{(hasKnife ? 2 : 1)} Ervas ({totalHerbs}), +1 Semente{bonusTag}"
                    : $"+2 Bagas ({totalBerries}), +{(hasKnife ? 2 : 1)} Ervas (Total Ervas: {totalHerbs}){bonusTag}");
            }
        }
        else if (plantKind == 19) // Arbusto Espinhoso -> Ervas (Fibra)
        {
            AddOrDrop(103, 2, pos);
            int totalHerbs = inventory.GetItemCount(103);
            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: true);
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{(hasKnife ? 3 : 2)} Ervas Medicinais / Fibra (Total na Mochila: {totalHerbs}){bonusTag}");
            }
        }
        else if (plantKind == 20) // Cogumelo Luminoso
        {
            AddOrDrop(203, 1, pos);
            AddOrDrop(103, 1, pos);
            int totalHerbs = inventory.GetItemCount(103);
            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: true);
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"+1 Cogumelo, +{(hasKnife ? 2 : 1)} Ervas (Total Ervas: {totalHerbs}){bonusTag}");
            }
        }
        else // Fento / Samambaia / Cogumelo comum
        {
            string lower = (plantObj != null ? plantObj.name : col.gameObject.name).ToLower();
            if (lower.Contains("mushroom"))
            {
                AddOrDrop(203, 1, pos);
                AddOrDrop(103, 1, pos);
                int totalHerbs = inventory.GetItemCount(103);
                if (HUDSurvivalOverlay.Instance != null)
                {
                    HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: true);
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"+1 Cogumelo, +{(hasKnife ? 2 : 1)} Ervas (Total Ervas: {totalHerbs}){bonusTag}");
                }
            }
            else
            {
                AddOrDrop(103, 2, pos);
                bool gotSeed = Random.value < 0.40f;
                if (gotSeed) AddOrDrop(112, 1, pos);
                int totalHerbs = inventory.GetItemCount(103);
                if (HUDSurvivalOverlay.Instance != null)
                {
                    HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: true);
                    HUDSurvivalOverlay.Instance.ShowLootTooltip(gotSeed
                        ? $"+{(hasKnife ? 3 : 2)} Ervas (Total: {totalHerbs}), +1 Sementes Silvestres{bonusTag}"
                        : $"+{(hasKnife ? 3 : 2)} Ervas / Fibra p/ Corda (Total na Mochila: {totalHerbs}){bonusTag}");
                }
            }
        }

        if (mapRendererCache == null) mapRendererCache = FindAnyObjectByType<MapRenderer>();
        if (mapRendererCache != null)
        {
            mapRendererCache.RemoveInstanceAt(gx, gz, plantKind);
        }
        if (plantObj != null)
        {
            Destroy(plantObj);
        }
    }

    public GameObject FindSpecificPropObject(Collider col)
    {
        if (col == null) return null;
        Transform cur = col.transform;
        while (cur != null && cur.parent != null)
        {
            string pName = cur.parent.name.ToLower();
            if (pName.StartsWith("chunk_") || pName.Contains("environment") || pName.Contains("landmarks") || cur.parent == cur.root)
                return cur.gameObject;
            cur = cur.parent;
        }
        return col.gameObject;
    }

    private GameObject FindSpecificTreeObject(Collider col)
    {
        if (col == null) return null;

        GalicianProceduralTreeGenerator gen = col.GetComponentInParent<GalicianProceduralTreeGenerator>();
        if (gen != null) return gen.gameObject;

        return FindSpecificPropObject(col);
    }

    private void HandleTreeChop(RaycastHit hit, GameObject treeRoot, float treeMult, bool isHeavy)
    {
        HandleTreeChopAtPoint(hit.point, hit.normal, treeRoot, treeMult, isHeavy);
    }

    private void HandleTreeChopAtPoint(Vector3 hitPoint, Vector3 hitNormal, GameObject treeRoot, float treeMult, bool isHeavy)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.ChopWood, hitPoint, 0.86f, 0.88f, 1.12f);
        }
        else if (audioSource != null && chopWoodClip != null)
        {
            audioSource.pitch = Random.Range(0.88f, 1.12f);
            audioSource.PlayOneShot(chopWoodClip, 0.85f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(isHeavy ? 0.14f : 0.08f);

        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.TriggerHitstop(isHeavy ? 0.045f : 0.025f, 0.12f);

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.TriggerImpactBounce(isHeavy ? 1.30f : 0.95f);

        if (hapticCache != null) hapticCache.TriggerLightVibration();
        SpawnWoodChipsVFX(hitPoint, hitNormal);

        int treeId = treeRoot.GetHashCode();
        if (!treeHitsRemaining.ContainsKey(treeId))
            treeHitsRemaining[treeId] = 4;

        int chopPower = (treeMult >= 2.8f ? 2 : 1) + (isHeavy ? 1 : 0);
        treeHitsRemaining[treeId] -= chopPower;
        int remaining = Mathf.Max(0, treeHitsRemaining[treeId]);

        int woodGained = treeMult >= 2.8f ? 2 : 1;
        AddOrDrop(100, woodGained, hitPoint);

        bool gotResin = Random.value < (treeMult >= 1.8f ? 0.35f : 0.22f);
        if (gotResin) AddOrDrop(119, 1, hitPoint);

        int totalWood = inventory != null ? inventory.GetItemCount(100) : woodGained;

        if (remaining <= 0)
        {
            treeHitsRemaining.Remove(treeId);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.TreeFall, treeRoot.transform.position, 0.95f, 0.94f, 1.05f, 36f);
            }
            else if (audioSource != null && treeFallClip != null)
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
                    ? $"Arvore derrubada! +{woodGained} Madeira (Total: {totalWood}), +1 Resina, +1 Ervas"
                    : $"Arvore derrubada! +{woodGained} Madeira (Total: {totalWood}), +1 Resina");
            }

            SpawnTreeStump(treePos);
            Vector3 fallDir = (treePos - transform.position);
            fallDir.y = 0f;
            if (fallDir.sqrMagnitude < 0.01f) fallDir = transform.forward;
            fallDir.Normalize();

            int gridX = Mathf.RoundToInt(treePos.x);
            int gridZ = Mathf.RoundToInt(treePos.z);
            if (mapRendererCache == null) mapRendererCache = FindAnyObjectByType<MapRenderer>();
            if (mapRendererCache != null) mapRendererCache.RemoveInstanceAt(gridX, gridZ, 1);

            StartCoroutine(AnimateTreeFallAndDestroy(treeRoot, fallDir));
        }
        else
        {
            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: isHeavy, isKill: false);
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{woodGained} Madeira{(gotResin ? " +1 Resina" : "")} (Total Madeira: {totalWood}) [Corte {4 - remaining}/4]");
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
        HandleRockStrikeAtPoint(hit.collider, hit.point, hit.normal, rockMult, isHeavy);
    }

    private void HandleRockStrikeAtPoint(Collider col, Vector3 hitPoint, Vector3 hitNormal, float rockMult, bool isHeavy)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.StrikeRock, hitPoint, 0.82f, 0.92f, 1.14f);
        }
        else if (audioSource != null && hitRockClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.15f);
            audioSource.PlayOneShot(hitRockClip, 0.78f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(isHeavy ? 0.11f : 0.06f);

        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.TriggerHitstop(0.028f, 0.14f);

        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.TriggerImpactBounce(isHeavy ? 1.40f : 1.05f);

        if (hapticCache != null) hapticCache.TriggerLightVibration();
        if (VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.PlaySparkVFX(hitPoint, hitNormal);

        MineableRockNode node = col != null ? col.GetComponentInParent<MineableRockNode>() : null;
        if (node != null && node.isPermanent)
        {
            if (!node.TryConsumeStrike(out _, out int waitSec))
            {
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"Monumento esgotado temporariamente - aguarde {waitSec}s");
                return;
            }
        }
        else
        {
            if (resourceManagerCache == null) resourceManagerCache = FindAnyObjectByType<ResourceManager>();
            if (resourceManagerCache != null)
            {
                bool depleted = resourceManagerCache.TryMineRockFromCombat(col, hitPoint, node, rockMult, isHeavy);
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: rockMult >= 2.0f, isKill: depleted);
                return;
            }
        }

        int stoneYield = rockMult >= 2.3f ? (isHeavy ? 3 : 2) : 2;
        float ironChance = rockMult >= 2.0f ? 0.65f : 0.30f;
        float saltChance = rockMult >= 2.0f ? 0.42f : 0.20f;

        bool gotIron = Random.value < ironChance;
        bool gotSalt = Random.value < saltChance;

        AddOrDrop(101, stoneYield, hitPoint);
        if (gotIron) AddOrDrop(107, 1, hitPoint);
        if (gotSalt) AddOrDrop(106, 1, hitPoint);

        int totalStones = inventory != null ? inventory.GetItemCount(101) : stoneYield;
        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: rockMult >= 2.0f, isKill: false);
            string bonus = "";
            if (gotIron) bonus += ", +1 Ferro";
            if (gotSalt) bonus += ", +1 Sal";
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{stoneYield} Pedra{bonus} (Total Pedras: {totalStones})");
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
        bool stealthCrit = false;
        WildlifeAIEngine wildlife = hit.collider.GetComponentInParent<WildlifeAIEngine>();
        BossSystem boss = hit.collider.GetComponentInParent<BossSystem>();
        bool isTerrainOrGround = hit.collider.gameObject.name.ToLower().Contains("terrain");
        bool isLivingEnemy = !isTerrainOrGround && (wildlife != null || boss != null);

        if (isLivingEnemy && FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.IsHidden)
        {
            actualDamage *= (weaponId == 302 ? 2.55f : 2.25f);
            stealthCrit = true;
        }

        if (boss != null && inventory != null && inventory.equippedOffHandId == 303)
        {
            actualDamage *= 1.25f; // Bonus de fogo da tocha contra criaturas sombrias
        }

        if (weaponId == 311 && isLivingEnemy)
        {
            actualDamage *= 1.12f; // Clava de Osso aplica impacto contundente adicional
        }

        if (!isTerrainOrGround && PlayerMeleeCombatDefense.Instance != null && PlayerMeleeCombatDefense.Instance.hasParryBonus)
        {
            actualDamage *= PlayerMeleeCombatDefense.Instance.counterMultiplier;
            PlayerMeleeCombatDefense.Instance.ConsumeParryBonus();
            parryCrit = true;
        }

        if (isTerrainOrGround)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.StrikeRock, hit.point, 0.36f, 0.65f, 0.82f);
            }
            else if (audioSource != null && hitRockClip != null)
            {
                audioSource.pitch = Random.Range(0.65f, 0.82f);
                audioSource.PlayOneShot(hitRockClip, 0.36f);
            }
            if (VFXPoolManager.Instance != null)
                VFXPoolManager.Instance.PlayBuildPlaceVFX(hit.point);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(boss != null ? AudioManager.FoleyEventType.HitShadow : AudioManager.FoleyEventType.HitFlesh, hit.point, 0.85f, 0.90f, 1.10f);
        }
        else if (audioSource != null && hitFleshClip != null)
        {
            audioSource.pitch = Random.Range(0.90f, 1.10f);
            audioSource.PlayOneShot(hitFleshClip, 0.85f);
        }

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake((parryCrit || stealthCrit) ? 0.22f : (isHeavy ? 0.15f : 0.09f));

        if (!isTerrainOrGround && GameFeelManager.Instance != null)
            GameFeelManager.Instance.TriggerHitstop((parryCrit || stealthCrit || isHeavy) ? 0.055f : 0.032f, 0.08f);

        if (!isTerrainOrGround && FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.TriggerImpactBounce((parryCrit || stealthCrit) ? 1.35f : (isHeavy ? 1.15f : 0.85f));

        if (VFXPoolManager.Instance != null)
        {
            if (boss != null)
            {
                VFXPoolManager.Instance.PlayShadowHitVFX(hit.point);
                VFXPoolManager.Instance.PlayBloodDecalVFX(hit.point, true);
            }
            else if (wildlife != null)
            {
                VFXPoolManager.Instance.PlayBloodSplashVFX(hit.point, hit.normal);
                VFXPoolManager.Instance.PlayBloodDecalVFX(hit.point, false);
            }
            else
                VFXPoolManager.Instance.PlayBuildPlaceVFX(hit.point);
        }

        if (!isTerrainOrGround)
        {
            if (boss != null)
            {
                boss.TakeDamage(actualDamage);
            }
            else
            {
                hit.collider.SendMessageUpwards("TakeDamage", actualDamage, SendMessageOptions.DontRequireReceiver);
            }
            DegradeEquippedTool();

            if (HUDSurvivalOverlay.Instance != null)
            {
                bool killed = (wildlife != null && wildlife.currentState == WildlifeAIEngine.AIState.Dead) ||
                              (boss != null && !boss.IsActive);
                HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: parryCrit || stealthCrit || isHeavy, isKill: killed);

                if (parryCrit)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#55FFDD>CRITICO DE PARRY! ({actualDamage:F0} Dano)</color>");
                else if (stealthCrit)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#44FFCC>EMBOSCADA FURTIVA! ({actualDamage:F0} Dano)</color>");
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
            pickup.disableAutoMagnet = true;
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
        int sr = 22050; int len = (int)(sr * 0.24f); float[] samples = new float[len];
        uint seed = 55001u; float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            seed = seed * 1664525u + 1013904223u;
            float w = ((seed >> 16) / 65535f) * 2f - 1f;
            lp1 = Mathf.Lerp(lp1, w, 0.24f); lp2 = Mathf.Lerp(lp2, lp1, 0.06f);
            samples[i] = Mathf.Clamp((Mathf.Sin(2f * Mathf.PI * 110f * ((float)i / sr)) * Mathf.Exp(-18f * t) * 0.65f + (lp1 - lp2) * Mathf.Exp(-9f * t) * 0.55f) * 0.95f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("AxeChopWood_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateTreeFallSound()
    {
        int sr = 22050; int len = (int)(sr * 1.25f); float[] samples = new float[len];
        uint seed = 55009u; float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            seed = seed * 1664525u + 1013904223u;
            float w = ((seed >> 16) / 65535f) * 2f - 1f;
            lp1 = Mathf.Lerp(lp1, w, 0.22f); lp2 = Mathf.Lerp(lp2, lp1, 0.05f);
            float crack = (t < 0.35f) ? (lp1 - lp2) * Mathf.Sin(t * 120f) * 0.9f : 0f;
            float crash = (t >= 0.35f) ? (Mathf.Sin(2f * Mathf.PI * 55f * ((float)i / sr)) * 0.85f + lp2 * 0.35f) * Mathf.Exp(-4f * (t - 0.35f)) : 0f;
            samples[i] = Mathf.Clamp(crack * 0.7f + crash * 0.85f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("TreeFall_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateRockHitSound()
    {
        int sr = 22050; int len = (int)(sr * 0.18f); float[] samples = new float[len];
        uint seed = 55021u; float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len; float env = Mathf.Exp(-24f * t);
            seed = seed * 1664525u + 1013904223u;
            float w = ((seed >> 16) / 65535f) * 2f - 1f;
            lp = Mathf.Lerp(lp, w, 0.35f);
            samples[i] = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * 720f * ((float)i / sr)) * env * 0.65f + (w - lp) * env * 0.45f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("RockHit_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateSwingWhooshSound()
    {
        int sr = 22050; int len = (int)(sr * 0.16f); float[] samples = new float[len];
        uint seed = 55117u;
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            seed = seed * 1664525u + 1013904223u;
            float w = ((seed >> 16) / 65535f) * 2f - 1f;
            lp1 = Mathf.Lerp(lp1, w, 0.18f);
            lp2 = Mathf.Lerp(lp2, lp1, 0.06f);
            samples[i] = (lp1 - lp2) * Mathf.Sin(t * Mathf.PI) * 0.65f;
        }
        AudioClip clip = AudioClip.Create("AxeWhoosh_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateFleshHitSound()
    {
        int sr = 22050; int len = (int)(sr * 0.14f); float[] samples = new float[len];
        uint seed = 55033u; float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len; float env = Mathf.Exp(-16f * t);
            seed = seed * 1664525u + 1013904223u;
            float w = ((seed >> 16) / 65535f) * 2f - 1f;
            lp = Mathf.Lerp(lp, w, 0.18f);
            samples[i] = Mathf.Clamp((Mathf.Sin(2f * Mathf.PI * 140f * ((float)i / sr)) * 0.65f + lp * 0.35f) * env, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("FleshHit_Procedural", len, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
