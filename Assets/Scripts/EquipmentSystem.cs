using UnityEngine;

/// <summary>
/// EquipmentSystem: Orquestrador central de Acao Primaria (ataque leve/pesado, pesca, enxada, arco)
/// e Acao Secundaria (mira de arco, arremesso de lanca, bloqueio/parry).
/// Zero teclas soltas — toda entrada vem exclusivamente do FragaInputRouter.
/// </summary>
public class EquipmentSystem : MonoBehaviour
{
    public static EquipmentSystem Instance { get; private set; }

    [Header("Dependencies")]
    public Transform playerHandTransform;
    public PlayerStatus playerStatus;
    public InventoryAdvanced inventory;

    [Header("Charge Settings")]
    public float heavyChargeThreshold = 0.42f;
    public float maxSpearChargeTime = 0.90f;

    public int CurrentWeaponID { get; private set; } = -1;

    private bool trackingMeleePress = false;
    private float meleePressTimer = 0f;

    private bool isAimingSpear = false;
    private float spearChargeTimer = 0f;

    private BowAndArrowSystem bowCache;
    private PlayerMeleeCombatDefense defenseCache;
    private ChoppingAndCombatSystem combatCache;
    private FishingSystem fishingCache;
    private FarmingSystem farmingCache;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();

        bowCache = BowAndArrowSystem.Instance ?? GetComponent<BowAndArrowSystem>() ?? FindAnyObjectByType<BowAndArrowSystem>();
        defenseCache = PlayerMeleeCombatDefense.Instance ?? GetComponent<PlayerMeleeCombatDefense>() ?? FindAnyObjectByType<PlayerMeleeCombatDefense>();
        combatCache = ChoppingAndCombatSystem.Instance ?? GetComponent<ChoppingAndCombatSystem>() ?? FindAnyObjectByType<ChoppingAndCombatSystem>();
        fishingCache = FishingSystem.Instance ?? FindAnyObjectByType<FishingSystem>();
        farmingCache = FarmingSystem.Instance ?? FindAnyObjectByType<FarmingSystem>();

        if (inventory != null)
        {
            inventory.OnEquipmentChanged += SyncEquippedFromInventory;
            SyncEquippedFromInventory();
        }
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnEquipmentChanged -= SyncEquippedFromInventory;
        }
    }

    private void SyncEquippedFromInventory()
    {
        if (inventory == null) return;
        CurrentWeaponID = inventory.equippedMainHandId;

        if (isAimingSpear && CurrentWeaponID != 308)
        {
            CancelSpearAim();
        }
        if (bowCache != null && bowCache.IsAiming && CurrentWeaponID != 305)
        {
            bowCache.CancelAim();
        }
    }

    public void EquipWeapon(int itemID, GameObject weaponPrefab)
    {
        CurrentWeaponID = itemID;
        if (inventory != null && inventory.equippedMainHandId != itemID)
        {
            inventory.EquipItem(itemID);
        }
    }

    public void Attack()
    {
        if (combatCache == null) combatCache = ChoppingAndCombatSystem.Instance;
        if (combatCache != null) combatCache.TriggerAttack(false);
    }

    private void Update()
    {
        FragaInputRouter router = FragaInputRouter.Instance;
        if (router == null) return;

        if (!router.IsGameplayAllowed)
        {
            trackingMeleePress = false;
            if (isAimingSpear) CancelSpearAim();
            if (bowCache != null && bowCache.IsAiming) bowCache.CancelAim();
            if (defenseCache != null && defenseCache.isBlocking) defenseCache.StopBlock();
            return;
        }

        if (inventory != null) CurrentWeaponID = inventory.equippedMainHandId;

        HandleSecondaryAction(router);
        HandlePrimaryAction(router);
    }

    private void HandlePrimaryAction(FragaInputRouter router)
    {
        // Arco (305): clicar primario enquanto mira solta a flecha; clicar sem mirar faz tiro rapido
        if (CurrentWeaponID == 305)
        {
            if (router.PrimaryDownThisFrame)
            {
                if (bowCache == null) bowCache = BowAndArrowSystem.Instance;
                if (bowCache != null)
                {
                    if (bowCache.IsAiming) bowCache.ReleaseArrow();
                    else bowCache.QuickShot();
                }
            }
            return;
        }

        // Vara de Pesca (306)
        if (CurrentWeaponID == 306)
        {
            if (router.PrimaryDownThisFrame)
            {
                if (fishingCache == null) fishingCache = FishingSystem.Instance;
                if (fishingCache != null) fishingCache.TryFish();
            }
            return;
        }

        // Enxada (307): tenta lavrar/plantar antes de golpear
        if (CurrentWeaponID == 307 && router.PrimaryDownThisFrame)
        {
            if (farmingCache == null) farmingCache = FarmingSystem.Instance;
            if (farmingCache != null && farmingCache.TryTillOrPlant())
            {
                if (FirstPersonViewmodelController.Instance != null)
                    FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(0, false);
                return;
            }
        }

        // Melee / Ferramentas / Punhos: toque rapido = ataque leve; segurar >= 0.42s = ataque pesado
        if (router.PrimaryDownThisFrame)
        {
            trackingMeleePress = true;
            meleePressTimer = 0f;
        }

        if (trackingMeleePress && router.PrimaryHeld)
        {
            meleePressTimer += Time.deltaTime;
        }

        if (trackingMeleePress && router.PrimaryUpThisFrame)
        {
            bool isHeavy = meleePressTimer >= heavyChargeThreshold;
            trackingMeleePress = false;
            meleePressTimer = 0f;

            if (combatCache == null) combatCache = ChoppingAndCombatSystem.Instance;
            if (combatCache != null) combatCache.TriggerAttack(isHeavy);
        }
    }

    private void HandleSecondaryAction(FragaInputRouter router)
    {
        // 1. Arco (305): Segurar para retesar, soltar para disparar
        if (CurrentWeaponID == 305)
        {
            if (defenseCache != null && defenseCache.isBlocking) defenseCache.StopBlock();
            if (bowCache == null) bowCache = BowAndArrowSystem.Instance;
            if (bowCache == null) return;

            if (router.SecondaryDownThisFrame) bowCache.StartAiming();
            if (router.SecondaryHeld && bowCache.IsAiming) bowCache.UpdateAimTick();
            if (router.SecondaryUpThisFrame && bowCache.IsAiming) bowCache.ReleaseArrow();
            return;
        }

        // 2. Lanca Celta (308): Segurar secundario para mirar e arremessar
        if (CurrentWeaponID == 308)
        {
            if (router.SecondaryDownThisFrame)
            {
                isAimingSpear = true;
                spearChargeTimer = 0f;
                if (FirstPersonViewmodelController.Instance != null)
                    FirstPersonViewmodelController.Instance.SetSpearAim(true, 0.1f);
            }

            if (isAimingSpear && router.SecondaryHeld)
            {
                spearChargeTimer = Mathf.Min(spearChargeTimer + Time.deltaTime, maxSpearChargeTime);
                float charge01 = spearChargeTimer / maxSpearChargeTime;
                if (FirstPersonViewmodelController.Instance != null)
                    FirstPersonViewmodelController.Instance.SetSpearAim(true, charge01);
                if (FirstPersonCamera.Instance != null)
                    FirstPersonCamera.Instance.SetAimFOVOverride(Mathf.Lerp(62f, 48f, charge01));
            }

            if (isAimingSpear && router.SecondaryUpThisFrame)
            {
                ThrowCelticSpear();
            }
            return;
        }

        // 3. Bloqueio e Parry (Escudo 309 / Armas brancas / Punhos)
        if (defenseCache == null) defenseCache = PlayerMeleeCombatDefense.Instance;
        if (defenseCache != null)
        {
            if (router.SecondaryDownThisFrame) defenseCache.StartBlock();
            else if (!router.SecondaryHeld && defenseCache.isBlocking) defenseCache.StopBlock();
        }
    }

    private void CancelSpearAim()
    {
        isAimingSpear = false;
        spearChargeTimer = 0f;
        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetSpearAim(false, 0f);
        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.SetAimFOVOverride(-1f);
    }

    private void ThrowCelticSpear()
    {
        float charge01 = Mathf.Clamp01(spearChargeTimer / maxSpearChargeTime);
        CancelSpearAim();

        if (charge01 < 0.18f) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        if (inventory != null)
        {
            inventory.RemoveItem(308, 1);
        }

        Vector3 dir = cam.transform.forward;
        Vector3 spawnPos = cam.transform.position + dir * 0.7f + cam.transform.right * 0.16f - cam.transform.up * 0.05f;
        float speed = Mathf.Lerp(18f, 34f, charge01);
        float dmg = Mathf.Lerp(40f, 75f, charge01);

        ThrownSpearProjectile.Spawn(spawnPos, dir, speed, dmg, transform);

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(0.08f);
    }
}
