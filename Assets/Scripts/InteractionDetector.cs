using UnityEngine;

/// <summary>
/// InteractionDetector: Detector contextual unificado (PC [E] + Mobile Botao Interagir).
/// Opera em 12.5 Hz com buffers pre-alocados (Zero-GC) para manter 60 FPS no Mobile e 120 FPS no PC.
/// Suporta blueprints, abrigos (dormir/salvar), secador de carne, coletor de chuva, carcacas, fogueiras, agua turva, rochas e pedras/galhos soltos.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    public static InteractionDetector Instance { get; private set; }

    [Header("Detection Settings")]
    public float rayDistance = 3.4f;
    public float scanInterval = 0.08f; // 12.5 Hz

    public bool HasValidTarget { get; private set; }
    public string CurrentPromptLabel { get; private set; } = string.Empty;
    public FragaProIconLibrary.ControlIcon CurrentIcon { get; private set; } = FragaProIconLibrary.ControlIcon.Interact;

    private enum TargetKind
    {
        None,
        BlueprintSite,
        Shelter,
        DryingRack,
        RainCollector,
        LogHolder,
        StorageChest,
        LootChest,
        LoreNote,
        StructureRepair,
        Carcass,
        LogPickup,
        WorldPickup,
        SpearPickup,
        Bonfire,
        Workbench,
        Cauldron,
        WaterFishing,
        WaterGather,
        FarmPlot,
        BerryOrMushroom,
        SceneryTile,
        ResourceGrid,
        MineableRock,
        DropCarriedLog
    }

    private TargetKind currentKind = TargetKind.None;
    private WorldBlueprintSite targetBlueprint;
    private ShelterInteractable targetShelter;
    private DryingRackInteractable targetDryingRack;
    private RainCollectorInteractable targetRainCollector;
    private LogHolderInteractable targetLogHolder;
    private StorageChestInteractable targetStorageChest;
    private LootContainer targetLootChest;
    private LoreNoteInteractable targetLoreNote;
    private StructureHealth targetStructureRepair;
    private SkinningCarcass targetCarcass;
    private Collider targetCollider;
    private ResourcePickup targetPickup;
    private ThrownSpearProjectile targetSpear;
    private BonfireEntity targetBonfire;
    private MineableRockNode targetRockNode;
    private Vector2Int targetGridPos;
    private int targetTileId;
    private HUDSurvivalOverlay.CrosshairContext crosshairContext = HUDSurvivalOverlay.CrosshairContext.Default;

    private float nextScanTime;
    private float nextWaterGatherTime;
    private Camera cam;
    private GameManager gm;
    private InventoryAdvanced inventory;
    private ResourceManager resourceManager;
    private SceneryInteractionSystem scenerySystem;
    private FarmingSystem farmingSystem;
    private FishingSystem fishingSystem;

    private readonly RaycastHit[] rayHits = new RaycastHit[8];
    private readonly Collider[] overlapCols = new Collider[12];

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    private void Start()
    {
        cam = Camera.main;
        gm = FindAnyObjectByType<GameManager>();
        inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        resourceManager = FindAnyObjectByType<ResourceManager>();
        scenerySystem = FindAnyObjectByType<SceneryInteractionSystem>();
        farmingSystem = FindAnyObjectByType<FarmingSystem>();
        fishingSystem = FindAnyObjectByType<FishingSystem>();
    }

    private void Update()
    {
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            ScanContext();
        }

        if (FragaInputRouter.Instance != null && FragaInputRouter.Instance.InteractPressedThisFrame)
        {
            ExecuteCurrentInteraction();
        }
    }

    private void ScanContext()
    {
        if (cam == null) cam = Camera.main;
        if (inventory == null) inventory = InventoryAdvanced.Instance;

        ResetTarget();

        if (cam == null) return;

        // 1. Raycast direto na mira (ignora jogador)
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        int hitCount = Physics.RaycastNonAlloc(ray, rayHits, rayDistance, ~0, QueryTriggerInteraction.Collide);

        float bestDist = float.MaxValue;
        Collider bestRayCol = null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider c = rayHits[i].collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.gameObject.name.Contains("Terrain")) continue;
            if (rayHits[i].distance < bestDist)
            {
                bestDist = rayHits[i].distance;
                bestRayCol = c;
            }
        }

        if (bestRayCol != null)
        {
            if (EvaluateColliderTarget(bestRayCol))
            {
                crosshairContext = (currentKind == TargetKind.MineableRock)
                    ? HUDSurvivalOverlay.CrosshairContext.Rock
                    : HUDSurvivalOverlay.CrosshairContext.Interactable;
                PushPromptToHUD();
                return;
            }

            string rayName = bestRayCol.gameObject.name.ToLower();
            if (bestRayCol.GetComponentInParent<WildlifeAIEngine>() != null || rayName.Contains("vulto") || rayName.Contains("trasno"))
                crosshairContext = HUDSurvivalOverlay.CrosshairContext.Hostile;
            else if (!rayName.Contains("stump") && (rayName.Contains("tree") || rayName.Contains("trunk") || rayName.Contains("pine") || bestRayCol.GetComponentInParent<GalicianProceduralTreeGenerator>() != null))
                crosshairContext = HUDSurvivalOverlay.CrosshairContext.Tree;
            else if (ChoppingAndCombatSystem.IsRockCollider(bestRayCol))
                crosshairContext = HUDSurvivalOverlay.CrosshairContext.Rock;
        }

        // 2. OverlapSphere leve ao redor do ponto a frente (pickups, carcacas, blueprints, abrigos, fogueiras)
        Vector3 probePos = transform.position + Vector3.up * 0.8f + transform.forward * 1.0f;
        int colCount = Physics.OverlapSphereNonAlloc(probePos, 1.95f, overlapCols, ~0, QueryTriggerInteraction.Collide);
        float bestSq = float.MaxValue;
        Collider bestNear = null;
        for (int i = 0; i < colCount; i++)
        {
            Collider c = overlapCols[i];
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
        if (c.GetComponentInParent<WorldBlueprintSite>() != null ||
            c.GetComponentInParent<ShelterInteractable>() != null ||
            c.GetComponentInParent<DryingRackInteractable>() != null ||
            c.GetComponentInParent<RainCollectorInteractable>() != null ||
            c.GetComponentInParent<LogHolderInteractable>() != null ||
            c.GetComponentInParent<StorageChestInteractable>() != null ||
            c.GetComponentInParent<LootContainer>() != null ||
            c.GetComponentInParent<LoreNoteInteractable>() != null ||
            c.GetComponentInParent<StructureHealth>() != null ||
            c.GetComponentInParent<SkinningCarcass>() != null ||
                c.GetComponent<ResourcePickup>() != null ||
                c.GetComponent<ThrownSpearProjectile>() != null ||
                c.GetComponentInParent<BonfireEntity>() != null ||
                IsLogCollider(c))
            {
                float dSq = (c.transform.position - transform.position).sqrMagnitude;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    bestNear = c;
                }
            }
        }

        if (bestNear != null && EvaluateColliderTarget(bestNear))
        {
            crosshairContext = (currentKind == TargetKind.MineableRock)
                ? HUDSurvivalOverlay.CrosshairContext.Rock
                : HUDSurvivalOverlay.CrosshairContext.Interactable;
            PushPromptToHUD();
            return;
        }

        // 3. Consulta rapida no grid nativo a frente da mira
        if (gm == null) gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.mapDataPtr != System.IntPtr.Zero)
        {
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = transform.forward;
            fwd.Normalize();

            Vector3 checkPos = transform.position + fwd * 1.65f;
            int gx = Mathf.RoundToInt(checkPos.x);
            int gz = Mathf.RoundToInt(checkPos.z);

            if (gx >= 0 && gx < gm.mapWidth && gz >= 0 && gz < gm.mapHeight)
            {
                int tile = ResourceManager.GetMapCell(gm.mapDataPtr, gm.mapWidth, gm.mapHeight, gx, gz);
                if (EvaluateTileTarget(gx, gz, tile))
                {
                    crosshairContext = tile == 1 ? HUDSurvivalOverlay.CrosshairContext.Tree
                        : (tile == 2 || tile == 8) ? HUDSurvivalOverlay.CrosshairContext.Rock
                        : HUDSurvivalOverlay.CrosshairContext.Interactable;
                    PushPromptToHUD();
                    return;
                }
            }
        }

        // 4. Fallback: pesca ou coleta de agua turva por profundidade
        int equippedMain = inventory != null ? inventory.equippedMainHandId : -1;
        if (FragaWorldBlueprint.GetWaterDepth(transform.position.x + transform.forward.x * 2.5f, transform.position.z + transform.forward.z * 2.5f) > 0.15f)
        {
            if (equippedMain == 306)
            {
                currentKind = TargetKind.WaterFishing;
                HasValidTarget = true;
                CurrentPromptLabel = (fishingSystem != null && fishingSystem.HasBite) ? "PUXAR PEIXE!" : "Pescar na Agua";
                CurrentIcon = FragaProIconLibrary.ControlIcon.AttackRod;
            }
            else
            {
                currentKind = TargetKind.WaterGather;
                HasValidTarget = true;
                CurrentPromptLabel = "Coletar Agua Turva";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            }
            crosshairContext = HUDSurvivalOverlay.CrosshairContext.Interactable;
            PushPromptToHUD();
            return;
        }

        // 5. Fallback: soltar tora do ombro
        if (LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs > 0)
        {
            currentKind = TargetKind.DropCarriedLog;
            HasValidTarget = true;
            CurrentPromptLabel = "Soltar Tora";
            CurrentIcon = FragaProIconLibrary.ControlIcon.DropItem;
            crosshairContext = HUDSurvivalOverlay.CrosshairContext.Interactable;
        }

        PushPromptToHUD();
    }

    private void ResetTarget()
    {
        HasValidTarget = false;
        CurrentPromptLabel = string.Empty;
        CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
        currentKind = TargetKind.None;
        targetBlueprint = null;
        targetShelter = null;
        targetDryingRack = null;
        targetRainCollector = null;
        targetLogHolder = null;
        targetStorageChest = null;
        targetLootChest = null;
        targetLoreNote = null;
        targetStructureRepair = null;
        targetCarcass = null;
        targetCollider = null;
        targetPickup = null;
        targetSpear = null;
        targetBonfire = null;
        targetRockNode = null;
        targetTileId = 0;
        crosshairContext = HUDSurvivalOverlay.CrosshairContext.Default;
    }

    private bool EvaluateColliderTarget(Collider c)
    {
        WorldBlueprintSite site = c.GetComponentInParent<WorldBlueprintSite>();
        if (site != null)
        {
            currentKind = TargetKind.BlueprintSite;
            targetBlueprint = site;
            HasValidTarget = true;
            CurrentPromptLabel = $"Construir {site.structureName} ({site.currentLogs}/{site.neededLogs} Mad, {site.currentStones}/{site.neededStones} Ped)";
            CurrentIcon = FragaProIconLibrary.ControlIcon.BuildHammer;
            return true;
        }

        ShelterInteractable shelter = c.GetComponentInParent<ShelterInteractable>();
        if (shelter != null)
        {
            currentKind = TargetKind.Shelter;
            targetShelter = shelter;
            HasValidTarget = true;
            CurrentPromptLabel = shelter.GetPromptLabel();
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        DryingRackInteractable rack = c.GetComponentInParent<DryingRackInteractable>();
        if (rack != null)
        {
            currentKind = TargetKind.DryingRack;
            targetDryingRack = rack;
            HasValidTarget = true;
            CurrentPromptLabel = rack.GetPromptLabel(inventory);
            CurrentIcon = FragaProIconLibrary.ControlIcon.CookFire;
            return true;
        }

        RainCollectorInteractable collector = c.GetComponentInParent<RainCollectorInteractable>();
        if (collector != null)
        {
            currentKind = TargetKind.RainCollector;
            targetRainCollector = collector;
            HasValidTarget = true;
            CurrentPromptLabel = collector.GetPromptLabel();
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        LogHolderInteractable logHolder = c.GetComponentInParent<LogHolderInteractable>();
        if (logHolder != null)
        {
            currentKind = TargetKind.LogHolder;
            targetLogHolder = logHolder;
            HasValidTarget = true;
            CurrentPromptLabel = logHolder.GetPromptLabel();
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        StorageChestInteractable chestBase = c.GetComponentInParent<StorageChestInteractable>();
        if (chestBase != null)
        {
            currentKind = TargetKind.StorageChest;
            targetStorageChest = chestBase;
            HasValidTarget = true;
            CurrentPromptLabel = chestBase.GetPromptLabel();
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        LootContainer chest = c.GetComponentInParent<LootContainer>();
        if (chest != null && !chest.IsOpened)
        {
            currentKind = TargetKind.LootChest;
            targetLootChest = chest;
            HasValidTarget = true;
            CurrentPromptLabel = $"Saquear {chest.containerName}";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        LoreNoteInteractable note = c.GetComponentInParent<LoreNoteInteractable>();
        if (note != null)
        {
            currentKind = TargetKind.LoreNote;
            targetLoreNote = note;
            HasValidTarget = true;
            CurrentPromptLabel = $"Ler {note.noteTitle}";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        StructureHealth structHp = c.GetComponentInParent<StructureHealth>();
        if (structHp != null && structHp.IsDamaged)
        {
            currentKind = TargetKind.StructureRepair;
            targetStructureRepair = structHp;
            HasValidTarget = true;
            CurrentPromptLabel = $"Consertar {structHp.structureName} ({Mathf.RoundToInt(structHp.HealthRatio * 100f)}% | 1 Mad)";
            CurrentIcon = FragaProIconLibrary.ControlIcon.BuildHammer;
            return true;
        }

        SkinningCarcass carcass = c.GetComponentInParent<SkinningCarcass>();
        if (carcass != null && !carcass.IsDepleted)
        {
            currentKind = TargetKind.Carcass;
            targetCarcass = carcass;
            HasValidTarget = true;
            bool hasKnife = inventory != null && inventory.equippedMainHandId == 302;
            CurrentPromptLabel = hasKnife ? $"Esfolar {carcass.animalDisplayName} (Bonus Faca)" : $"Esfolar {carcass.animalDisplayName}";
            CurrentIcon = FragaProIconLibrary.ControlIcon.AttackKnife;
            return true;
        }

        ThrownSpearProjectile spear = c.GetComponentInParent<ThrownSpearProjectile>();
        if (spear != null && spear.CanBePickedUp)
        {
            currentKind = TargetKind.SpearPickup;
            targetSpear = spear;
            HasValidTarget = true;
            CurrentPromptLabel = "Recuperar Lanca Celta";
            CurrentIcon = FragaProIconLibrary.ControlIcon.AttackSpear;
            return true;
        }

        if (IsLogCollider(c) && LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.CanPickupLog)
        {
            currentKind = TargetKind.LogPickup;
            targetCollider = c;
            HasValidTarget = true;
            CurrentPromptLabel = "Carregar Tora no Ombro";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        ResourcePickup pickup = c.GetComponent<ResourcePickup>();
        if (pickup != null)
        {
            currentKind = TargetKind.WorldPickup;
            targetPickup = pickup;
            ItemData d = ItemDatabase.GetItem(pickup.itemID);
            HasValidTarget = true;
            CurrentPromptLabel = d != null ? $"Pegar {d.itemName} (x{pickup.amount})" : "Pegar Item";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        BonfireEntity bonfire = c.GetComponentInParent<BonfireEntity>();
        if (bonfire != null)
        {
            currentKind = TargetKind.Bonfire;
            targetBonfire = bonfire;
            HasValidTarget = true;
            if (!bonfire.isLit)
                CurrentPromptLabel = "Fogueira Apagada (Acender c/ Fosforo ou Tocha)";
            else if (bonfire.fuelLevel < 35f && inventory != null && inventory.HasItem(100))
                CurrentPromptLabel = "Fogueira (Adicionar Lenha / Cozinhar)";
            else
                CurrentPromptLabel = "Fogueira (Cozinhar / Aquecer)";
            CurrentIcon = FragaProIconLibrary.ControlIcon.CookFire;
            return true;
        }

        if (ChoppingAndCombatSystem.IsRockCollider(c))
        {
            currentKind = TargetKind.MineableRock;
            targetCollider = c;
            targetRockNode = c.GetComponentInParent<MineableRockNode>();
            HasValidTarget = true;
            if (targetRockNode != null && targetRockNode.isPermanent && targetRockNode.IsOnCooldown(out int waitSec))
            {
                CurrentPromptLabel = $"Monumento Esgotado (Aguarde {waitSec}s)";
            }
            else
            {
                bool hasPick = inventory != null && (inventory.equippedMainHandId == 301 || inventory.HasItem(301));
                CurrentPromptLabel = hasPick ? "Minerar Pedra (Picareta)" : "Lascar Pedra (Com as Maos)";
            }
            CurrentIcon = FragaProIconLibrary.ControlIcon.AttackPickaxe;
            return true;
        }

        string lowerName = c.gameObject.name.ToLower();
        if (lowerName.Contains("berry") || lowerName.Contains("mushroom") || lowerName.Contains("fento") || lowerName.Contains("fern"))
        {
            currentKind = TargetKind.BerryOrMushroom;
            targetCollider = c;
            targetGridPos = new Vector2Int(Mathf.RoundToInt(c.transform.position.x), Mathf.RoundToInt(c.transform.position.z));
            targetTileId = lowerName.Contains("berry") ? 9 : 10;
            HasValidTarget = true;
            if (targetTileId == 9)
                CurrentPromptLabel = "Colher Bagas e Ervas";
            else if (lowerName.Contains("fento") || lowerName.Contains("fern"))
                CurrentPromptLabel = "Colher Ervas / Fibra";
            else
                CurrentPromptLabel = "Colher Cogumelo e Ervas";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        return false;
    }

    private bool EvaluateTileTarget(int gx, int gz, int tile)
    {
        targetGridPos = new Vector2Int(gx, gz);
        targetTileId = tile;

        switch (tile)
        {
            case 4:
                currentKind = TargetKind.Bonfire;
                HasValidTarget = true;
                CurrentPromptLabel = "Fogueira (Cozinhar / Fabricar)";
                CurrentIcon = FragaProIconLibrary.ControlIcon.CookFire;
                return true;
            case 13:
                currentKind = TargetKind.Workbench;
                HasValidTarget = true;
                CurrentPromptLabel = "Mesa de Trabalho (Fabricar / Consertar)";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Workbench;
                return true;
            case 15:
                currentKind = TargetKind.Cauldron;
                HasValidTarget = true;
                CurrentPromptLabel = "Caldeirao Druida (Alquimia)";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Cauldron;
                return true;
            case 7:
            case 11:
                if (inventory != null && inventory.equippedMainHandId == 306)
                {
                    currentKind = TargetKind.WaterFishing;
                    HasValidTarget = true;
                    CurrentPromptLabel = (fishingSystem != null && fishingSystem.HasBite) ? "PUXAR PEIXE!" : "Pescar na Agua";
                    CurrentIcon = FragaProIconLibrary.ControlIcon.AttackRod;
                }
                else
                {
                    currentKind = TargetKind.WaterGather;
                    HasValidTarget = true;
                    CurrentPromptLabel = "Coletar Agua Turva";
                    CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                }
                return true;
            case 9:
                currentKind = TargetKind.BerryOrMushroom;
                HasValidTarget = true;
                CurrentPromptLabel = "Colher Bagas Silvestres";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 10:
            case 20:
                currentKind = TargetKind.BerryOrMushroom;
                HasValidTarget = true;
                CurrentPromptLabel = "Colher Cogumelo / Erva";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 14:
                if (inventory != null && inventory.HasItem(112))
                {
                    currentKind = TargetKind.FarmPlot;
                    HasValidTarget = true;
                    CurrentPromptLabel = "Plantar Sementes Silvestres";
                    CurrentIcon = FragaProIconLibrary.ControlIcon.AttackHoe;
                    return true;
                }
                return false;
            case 0:
                if (inventory != null && inventory.equippedMainHandId == 307)
                {
                    currentKind = TargetKind.FarmPlot;
                    HasValidTarget = true;
                    CurrentPromptLabel = "Lavrar Terra com Enxada";
                    CurrentIcon = FragaProIconLibrary.ControlIcon.AttackHoe;
                    return true;
                }
                return false;
            case 17:
                currentKind = TargetKind.SceneryTile;
                HasValidTarget = true;
                CurrentPromptLabel = "Ler Pedra Runica (+Sanidade)";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 18:
                currentKind = TargetKind.SceneryTile;
                HasValidTarget = true;
                CurrentPromptLabel = "Coletar Tronco Caido";
                CurrentIcon = FragaProIconLibrary.ControlIcon.AttackAxe;
                return true;
            case 22:
                currentKind = TargetKind.SceneryTile;
                HasValidTarget = true;
                CurrentPromptLabel = "Escavar Sepultura Antiga";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 30:
                currentKind = TargetKind.SceneryTile;
                HasValidTarget = true;
                CurrentPromptLabel = "Altar de Purificacao";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 1:
                currentKind = TargetKind.ResourceGrid;
                HasValidTarget = true;
                CurrentPromptLabel = "Cortar Arvore";
                CurrentIcon = FragaProIconLibrary.ControlIcon.AttackAxe;
                return true;
            case 2:
            case 8:
                currentKind = TargetKind.ResourceGrid;
                HasValidTarget = true;
                bool hasPick = inventory != null && (inventory.equippedMainHandId == 301 || inventory.HasItem(301));
                CurrentPromptLabel = hasPick ? "Minerar Pedra (Picareta)" : "Lascar Pedra (Com as Maos)";
                CurrentIcon = FragaProIconLibrary.ControlIcon.AttackPickaxe;
                return true;
        }

        return false;
    }

    private static bool IsLogCollider(Collider c)
    {
        string n = c.gameObject.name.ToLower();
        return n.Contains("felled_wood_log") || n.Contains("droppedwood");
    }

    private void PushPromptToHUD()
    {
        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.SetInteractionPrompt(HasValidTarget, CurrentPromptLabel, CurrentIcon);
            HUDSurvivalOverlay.Instance.SetCrosshairContext(crosshairContext);
        }
    }

    public void ExecuteCurrentInteraction()
    {
        if (!HasValidTarget)
        {
            if (LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs > 0)
            {
                LogCarryingSystem.Instance.DropLog(false);
            }
            return;
        }

        switch (currentKind)
        {
            case TargetKind.BlueprintSite:
                if (targetBlueprint != null)
                    targetBlueprint.TryDepositResource(gameObject);
                break;

            case TargetKind.Shelter:
                if (targetShelter != null)
                    targetShelter.Interact();
                break;

            case TargetKind.DryingRack:
                if (targetDryingRack != null)
                    targetDryingRack.Interact(inventory);
                break;

            case TargetKind.RainCollector:
                if (targetRainCollector != null)
                    targetRainCollector.Interact(inventory, PlayerStatus.Instance);
                break;

            case TargetKind.LogHolder:
                if (targetLogHolder != null)
                    targetLogHolder.Interact(inventory);
                break;

            case TargetKind.StorageChest:
                if (targetStorageChest != null)
                    targetStorageChest.Interact();
                break;

            case TargetKind.LootChest:
                if (targetLootChest != null)
                    targetLootChest.Interact(inventory);
                break;

            case TargetKind.LoreNote:
                if (targetLoreNote != null)
                    targetLoreNote.Interact();
                break;

            case TargetKind.StructureRepair:
                if (targetStructureRepair != null)
                    targetStructureRepair.TryRepair(inventory);
                break;

            case TargetKind.Carcass:
                if (targetCarcass != null)
                    targetCarcass.Harvest(inventory);
                break;

            case TargetKind.SpearPickup:
                if (targetSpear != null)
                    targetSpear.Collect(inventory);
                break;

            case TargetKind.LogPickup:
                if (targetCollider != null && LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.CanPickupLog)
                {
                    LogCarryingSystem.Instance.PickupLog();
                    Destroy(targetCollider.gameObject);
                }
                break;

            case TargetKind.WorldPickup:
                if (targetPickup != null && inventory != null)
                {
                    int leftover = inventory.AddItem(targetPickup.itemID, targetPickup.amount);
                    int collected = targetPickup.amount - leftover;
                    if (collected > 0)
                    {
                        ItemData d = ItemDatabase.GetItem(targetPickup.itemID);
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{collected} {(d != null ? d.itemName : "Item")}");
                        if (AudioManager.Instance != null)
                            AudioManager.Instance.PlayPickupSound();
                    }

                    if (leftover > 0)
                    {
                        targetPickup.amount = leftover;
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip("Mochila cheia!");
                    }
                    else
                    {
                        targetPickup.CompleteManualCollect();
                    }
                }
                break;

            case TargetKind.Bonfire:
                bool hasTorch = inventory != null && inventory.equippedOffHandId == 303;
                if (targetBonfire != null && !targetBonfire.isLit && (hasTorch || (inventory != null && inventory.HasItem(205))))
                {
                    if (!hasTorch) inventory.RemoveItem(205, 1);
                    targetBonfire.AddFuel(100f);
                    targetBonfire.Ignite();
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("Fogueira acesa!");
                }
                else if (targetBonfire != null && targetBonfire.isLit && targetBonfire.fuelLevel < 35f && inventory != null && inventory.HasItem(100))
                {
                    inventory.RemoveItem(100, 1);
                    targetBonfire.AddFuel(45f);
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("+Lenha na fogueira! (-1 Madeira)");
                }
                else if (SurvivalInventoryCraftUI.Instance != null)
                {
                    SurvivalInventoryCraftUI.Instance.OpenCraftingAtStation(CraftingStationType.Bonfire);
                }
                else
                {
                    var cook = FindAnyObjectByType<CampfireCookingSystem>();
                    if (cook != null) cook.TryCookFood();
                }
                break;

            case TargetKind.Workbench:
                if (SurvivalInventoryCraftUI.Instance != null)
                    SurvivalInventoryCraftUI.Instance.OpenCraftingAtStation(CraftingStationType.Workbench);
                break;

            case TargetKind.Cauldron:
                if (SurvivalInventoryCraftUI.Instance != null)
                    SurvivalInventoryCraftUI.Instance.OpenCraftingAtStation(CraftingStationType.Cauldron);
                break;

            case TargetKind.WaterFishing:
                if (fishingSystem == null) fishingSystem = FindAnyObjectByType<FishingSystem>();
                if (fishingSystem != null) fishingSystem.TryFish();
                break;

            case TargetKind.WaterGather:
                if (Time.time >= nextWaterGatherTime && inventory != null)
                {
                    nextWaterGatherTime = Time.time + 1.0f;
                    int leftover = inventory.AddItem(123, 1);
                    if (leftover == 0)
                    {
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip("+1 Agua Turva (Ferva na Fogueira ou Filtre c/ Carvao)");
                    }
                    else if (HUDSurvivalOverlay.Instance != null)
                    {
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("Mochila cheia para coletar agua!");
                    }
                }
                break;

            case TargetKind.FarmPlot:
                if (farmingSystem == null) farmingSystem = FindAnyObjectByType<FarmingSystem>();
                if (farmingSystem != null) farmingSystem.TryTillOrPlant();
                break;

            case TargetKind.BerryOrMushroom:
                if (gm != null && inventory != null)
                {
                    MapRenderer mr = FindAnyObjectByType<MapRenderer>();
                    int harvested = mr != null ? mr.TryHarvestPlantAt(targetGridPos.x, targetGridPos.y) : 0;
                    if (harvested == 0)
                    {
                        gm.UpdateMapCellNative(targetGridPos.x, targetGridPos.y, 0);
                        harvested = targetTileId;
                    }
                    if (harvested == 9)
                    {
                        int leftover = inventory.AddItem(202, 2);
                        int herbLeft = inventory.AddItem(103, 1);
                        if (leftover > 0 && LootManager.Instance != null)
                            LootManager.Instance.DropItem(202, leftover, transform.position + transform.forward);
                        if (herbLeft > 0 && LootManager.Instance != null)
                            LootManager.Instance.DropItem(103, herbLeft, transform.position + transform.forward);
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip("+2 Bagas Silvestres, +1 Ervas");
                    }
                    else
                    {
                        int leftover = inventory.AddItem(103, 1);
                        bool extraMush = Random.value > 0.55f;
                        int mushLeft = extraMush ? inventory.AddItem(203, 1) : 0;
                        if (leftover > 0 && LootManager.Instance != null)
                            LootManager.Instance.DropItem(103, leftover, transform.position + transform.forward);
                        if (mushLeft > 0 && LootManager.Instance != null)
                            LootManager.Instance.DropItem(203, mushLeft, transform.position + transform.forward);
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip(extraMush ? "+1 Ervas, +1 Cogumelo Estranho" : "+1 Ervas");
                    }
                }
                break;

            case TargetKind.SceneryTile:
                if (scenerySystem == null) scenerySystem = FindAnyObjectByType<SceneryInteractionSystem>();
                if (scenerySystem != null) scenerySystem.TryInteractAtTile(targetGridPos.x, targetGridPos.y);
                break;

            case TargetKind.ResourceGrid:
                if (resourceManager == null) resourceManager = FindAnyObjectByType<ResourceManager>();
                if (resourceManager != null) resourceManager.TryHarvestAtGrid(targetGridPos.x, targetGridPos.y);
                break;

            case TargetKind.MineableRock:
                if (resourceManager == null) resourceManager = FindAnyObjectByType<ResourceManager>();
                if (resourceManager != null)
                {
                    Vector3 hitPos = targetRockNode != null
                        ? targetRockNode.transform.position
                        : (targetCollider != null ? targetCollider.transform.position : transform.position + transform.forward);
                    resourceManager.TryHarvestWorldRock(hitPos, targetRockNode, targetCollider);
                }
                break;

            case TargetKind.DropCarriedLog:
                if (LogCarryingSystem.Instance != null) LogCarryingSystem.Instance.DropLog(false);
                break;
        }

        nextScanTime = 0f;
    }
}
