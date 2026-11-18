using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// InteractionDetector: Detector contextual unificado (PC [E] + Mobile Botao Interagir).
/// Opera em 12.5 Hz com buffers pre-alocados (Zero-GC) para manter 60 FPS no Mobile e 120 FPS no PC.
/// Suporta blueprints, abrigos (dormir/salvar), secador de carne, coletor de chuva, cesto de toras, bau de base, portas, carcacas, fogueiras, agua turva, rochas, arvores, troncos, arbustos e pedras/galhos soltos.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    public static InteractionDetector Instance { get; private set; }

    [Header("Detection Settings")]
    public float rayDistance = 4.2f;
    public float scanInterval = 0.08f; // 12.5 Hz

    public bool HasValidTarget { get; private set; }
    public string CurrentPromptLabel { get; private set; } = string.Empty;
    public FragaProIconLibrary.ControlIcon CurrentIcon { get; private set; } = FragaProIconLibrary.ControlIcon.Interact;
    public bool IsHoldTarget => HasValidTarget && GetCurrentHoldDuration() > 0.001f;
    public float HoldDuration => GetCurrentHoldDuration();
    public float HoldProgress01 { get; private set; }
    public bool IsHoldInProgress { get; private set; }
    public string HoldActionLabel => GetHoldActionVerb();
    public GameObject CurrentHighlightedObject => activeHighlightRoot;
    public WorldBlueprintSite NearbyBlueprintSite { get; private set; }
    public BonfireEntity NearbyBonfire { get; private set; }

    private enum TargetKind
    {
        None,
        BlueprintSite,
        Shelter,
        DryingRack,
        RainCollector,
        LogHolder,
        StorageChest,
        Door,
        LootChest,
        LoreNote,
        StructureRepair,
        Carcass,
        Trap,
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
        WorldTree,
        FallenLog,
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
    private DoorInteractable targetDoor;
    private LootContainer targetLootChest;
    private LoreNoteInteractable targetLoreNote;
    private StructureHealth targetStructureRepair;
    private SkinningCarcass targetCarcass;
    private TrapInteractable targetTrap;
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

    private readonly RaycastHit[] rayHits = new RaycastHit[16];
    private readonly Collider[] overlapCols = new Collider[24];
    private float cancelHoldTimer = 0f;
    private bool cancelHoldShownTip = false;
    public float CancelHoldProgress01 => NearbyBlueprintSite != null ? Mathf.Clamp01(cancelHoldTimer / 0.55f) : 0f;

    // Destaque 3D via MaterialPropertyBlock (Zero-GC, URP-safe, nunca destaca Chunks/Terreno)
    private struct HighlightRendererCache
    {
        public Renderer renderer;
        public Color baseColor;
        public Color legacyColor;
        public Color baseEmission;
        public bool hasBaseColor;
        public bool hasColor;
        public bool hasEmission;
        public bool hadMpbOverride;
    }
    private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorPropId = Shader.PropertyToID("_EmissionColor");
    private GameObject activeHighlightRoot;
    private readonly List<HighlightRendererCache> highlightRenderers = new List<HighlightRendererCache>(16);
    private readonly List<Renderer> tempRendererBuffer = new List<Renderer>(16);
    private MaterialPropertyBlock highlightMpb;
    private float highlightPulsePhase = 0f;

    private int activeHoldIdentity = 0;
    private float holdElapsed = 0f;
    private bool holdCompletedRequireRelease = false;
    private bool holdStartAudioPlayed = false;
    private bool cancelBlockedWarned = false;
    private bool lastPushedValid = false;
    private bool lastPushedTouch = false;
    private bool lastPushedHold = false;
    private FragaProIconLibrary.ControlIcon lastPushedIcon = FragaProIconLibrary.ControlIcon.Interact;
    private string lastPushedRawPrompt = null;
    private string lastPushedFormattedPrompt = string.Empty;

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { SafeDestroy(this); return; }
    }

    private void Start()
    {
        cam = Camera.main ?? FindAnyObjectByType<Camera>();
        gm = FindAnyObjectByType<GameManager>();
        inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        resourceManager = FindAnyObjectByType<ResourceManager>();
        scenerySystem = FindAnyObjectByType<SceneryInteractionSystem>();
        farmingSystem = FindAnyObjectByType<FarmingSystem>();
        fishingSystem = FindAnyObjectByType<FishingSystem>();
    }

    private void OnDisable()
    {
        ClearHighlight3D();
        ResetHoldState();
    }

    private void OnDestroy()
    {
        ClearHighlight3D();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            ScanContext();
        }

        UpdateHighlightPulse(Time.deltaTime);

        bool pressed = FragaInputRouter.Instance != null && FragaInputRouter.Instance.InteractPressedThisFrame;
        bool held = FragaInputRouter.Instance != null && FragaInputRouter.Instance.InteractHeld;
        bool up = FragaInputRouter.Instance != null && FragaInputRouter.Instance.InteractUpThisFrame;
        AdvanceHoldInteraction(pressed, held, up, Time.deltaTime);

        if (NearbyBonfire != null && NearbyBonfire.isLit && Input.GetKeyDown(KeyCode.F) &&
            (FragaInputRouter.Instance == null || FragaInputRouter.Instance.IsGameplayAllowed))
        {
            OpenNearbyBonfireRecipes();
        }

        if (NearbyBlueprintSite != null && Input.GetKey(KeyCode.X) &&
            (FragaInputRouter.Instance == null || FragaInputRouter.Instance.IsGameplayAllowed))
        {
            bool canCancel = FragaCoopSession.Instance == null ||
                             FragaCoopSession.Instance.CanLocalPlayerInteract(NearbyBlueprintSite.ownerPlayerId, NearbyBlueprintSite.isSharedWithGroup);
            if (!canCancel)
            {
                cancelHoldTimer = 0f;
                if (!cancelBlockedWarned)
                {
                    cancelBlockedWarned = true;
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF8866>Obra privada de {NearbyBlueprintSite.ownerPlayerName}!</color>");
                }
            }
            else
            {
                cancelHoldTimer += Time.deltaTime;
                if (!cancelHoldShownTip && cancelHoldTimer >= 0.08f)
                {
                    cancelHoldShownTip = true;
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FFAA55>Segure [X] para cancelar a obra...</color>");
                }
                if (cancelHoldTimer >= 0.55f)
                {
                    cancelHoldTimer = 0f;
                    cancelHoldShownTip = false;
                    CancelNearbyBlueprint();
                }
            }
        }
        else
        {
            cancelHoldTimer = 0f;
            cancelHoldShownTip = false;
            cancelBlockedWarned = false;
        }
    }

    public bool DepositToNearbyBlueprint()
    {
        WorldBlueprintSite site = NearbyBlueprintSite ?? targetBlueprint;
        if (site == null || site.IsComplete) return false;
        bool added = site.TryDepositResource(gameObject);
        nextScanTime = 0f;
        return added;
    }

    public bool CancelNearbyBlueprint()
    {
        WorldBlueprintSite site = NearbyBlueprintSite ?? targetBlueprint;
        if (site == null) return false;
        if (NearbyBlueprintSite == site) NearbyBlueprintSite = null;
        if (targetBlueprint == site) targetBlueprint = null;
        site.CancelAndRefund(gameObject);
        nextScanTime = 0f;
        return true;
    }

    public bool InteractWithNearbyBonfirePrimary()
    {
        BonfireEntity bf = targetBonfire ?? NearbyBonfire;
        if (bf == null) return false;
        InteractWithBonfire(bf);
        nextScanTime = 0f;
        return true;
    }

    public bool AddWoodToNearbyBonfire()
    {
        BonfireEntity bf = targetBonfire ?? NearbyBonfire;
        if (bf == null) return false;
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        bool added = bf.TryAddWoodFuel(inventory, out string msg);
        if (HUDSurvivalOverlay.Instance != null && !string.IsNullOrEmpty(msg))
            HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
        nextScanTime = 0f;
        return added;
    }

    public void OpenNearbyBonfireRecipes()
    {
        BonfireEntity bf = targetBonfire ?? NearbyBonfire;
        if (bf != null && !bf.isLit)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Acenda a fogueira primeiro para preparar receitas!");
            return;
        }
        if (SurvivalInventoryCraftUI.Instance != null)
        {
            SurvivalInventoryCraftUI.Instance.OpenCraftingAtStation(CraftingStationType.Bonfire);
        }
    }

    private void RefreshNearbyBlueprintSite()
    {
        WorldBlueprintSite best = null;
        float bestDist = 4.8f;
        for (int i = 0; i < WorldBlueprintSite.AllSites.Count; i++)
        {
            WorldBlueprintSite s = WorldBlueprintSite.AllSites[i];
            if (s == null || s.IsComplete) continue;
            float dist = s.GetDistanceToPlayer(transform.position);
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = s;
            }
        }
        NearbyBlueprintSite = best;
    }

    private void RefreshNearbyBonfire()
    {
        BonfireEntity best = null;
        float bestDistSq = 4.5f * 4.5f;
        Vector3 p = transform.position;

        for (int i = 0; i < BonfireEntity.AllBonfires.Count; i++)
        {
            BonfireEntity bf = BonfireEntity.AllBonfires[i];
            if (bf == null) continue;
            Vector3 d = bf.transform.position - p;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq <= bestDistSq)
            {
                bestDistSq = sq;
                best = bf;
            }
        }
        NearbyBonfire = best;
    }

    private static bool IsPriorityStructureCollider(Collider c)
    {
        if (c == null) return false;
        return c.GetComponentInParent<WorldBlueprintSite>() != null
            || c.GetComponentInParent<ShelterInteractable>() != null
            || c.GetComponentInParent<DryingRackInteractable>() != null
            || c.GetComponentInParent<RainCollectorInteractable>() != null
            || c.GetComponentInParent<LogHolderInteractable>() != null
            || c.GetComponentInParent<StorageChestInteractable>() != null
            || c.GetComponentInParent<DoorInteractable>() != null
            || c.GetComponentInParent<BonfireEntity>() != null
            || c.GetComponentInParent<CraftingStationMarker>() != null
            || c.GetComponentInParent<TrapInteractable>() != null;
    }

    public void ForceScanContextNow()
    {
        ScanContext();
    }

    private void ScanContext()
    {
        if (cam == null) cam = Camera.main ?? FindAnyObjectByType<Camera>();
        if (inventory == null) inventory = InventoryAdvanced.Instance;

        RefreshNearbyBlueprintSite();
        RefreshNearbyBonfire();
        ResetTarget();

        if (cam == null) return;

        // 1. SphereCast + Raycast na mira (avalia TODOS os hits ordenados por prioridade e distancia)
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        int hitCount = Physics.SphereCastNonAlloc(ray, 0.28f, rayHits, rayDistance, ~0, QueryTriggerInteraction.Collide);
        if (hitCount == 0)
        {
            hitCount = Physics.RaycastNonAlloc(ray, rayHits, rayDistance, ~0, QueryTriggerInteraction.Collide);
        }

        float bestValidScore = float.MaxValue;
        Collider bestValidCol = null;
        float closestRawDist = float.MaxValue;
        Collider closestRawCol = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = rayHits[i].collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.gameObject.name.Contains("Terrain")) continue;

            float d = rayHits[i].distance;
            if (d < closestRawDist)
            {
                closestRawDist = d;
                closestRawCol = c;
            }

            if (EvaluateColliderTarget(c))
            {
                float score = d - (IsPriorityStructureCollider(c) ? 1.5f : 0f);
                if (score < bestValidScore)
                {
                    bestValidScore = score;
                    bestValidCol = c;
                }
            }
        }

        ResetTarget();
        if (bestValidCol != null && EvaluateColliderTarget(bestValidCol))
        {
            RefreshHighlightForCurrentTarget();
            UpdateCrosshairFromCurrentKind();
            PushPromptToHUD();
            return;
        }

        if (closestRawCol != null)
        {
            string rayName = closestRawCol.gameObject.name;
            if (closestRawCol.GetComponentInParent<WildlifeAIEngine>() != null ||
                ContainsOrdinalIgnoreCase(rayName, "vulto") || ContainsOrdinalIgnoreCase(rayName, "trasno"))
                crosshairContext = HUDSurvivalOverlay.CrosshairContext.Hostile;
            else if (ChoppingAndCombatSystem.IsTreeCollider(closestRawCol))
                crosshairContext = HUDSurvivalOverlay.CrosshairContext.Tree;
            else if (ChoppingAndCombatSystem.IsRockCollider(closestRawCol))
                crosshairContext = HUDSurvivalOverlay.CrosshairContext.Rock;
        }

        // 2. OverlapSphere inteligente ao redor do jogador e do ponto a frente (funciona mesmo dentro da Cabana de Toras!)
        Vector3 probePos = transform.position + Vector3.up * 0.85f + transform.forward * 0.95f;
        int colCount = Physics.OverlapSphereNonAlloc(probePos, 2.75f, overlapCols, ~0, QueryTriggerInteraction.Collide);
        float bestScore = float.MaxValue;
        Collider bestNear = null;

        for (int i = 0; i < colCount; i++)
        {
            Collider c = overlapCols[i];
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.gameObject.name.Contains("Terrain")) continue;

            if (!EvaluateColliderTarget(c)) continue;

            Vector3 closestPt = c.ClosestPoint(cam.transform.position);
            Vector3 toTarget = closestPt - cam.transform.position;
            float dist = toTarget.magnitude;
            bool isStructure = IsPriorityStructureCollider(c);

            float align;
            if (dist < 0.25f)
            {
                align = 1f;
            }
            else
            {
                align = Vector3.Dot(cam.transform.forward, toTarget / dist);
                if (!isStructure && align < -0.15f) continue;
                if (isStructure && align < -0.65f && dist > 1.8f) continue;
            }

            bool isPickup = c.GetComponent<ResourcePickup>() != null;
            float score = dist - (align * 1.6f) - (isPickup ? 0.55f : 0f) - (isStructure ? 1.35f : 0f);
            if (score < bestScore)
            {
                bestScore = score;
                bestNear = c;
            }
        }

        ResetTarget();
        if (bestNear != null && EvaluateColliderTarget(bestNear))
        {
            RefreshHighlightForCurrentTarget();
            UpdateCrosshairFromCurrentKind();
            PushPromptToHUD();
            return;
        }

        // 2.5 Fallback Zero-GC direto nas listas estaticas de Blueprints e Estruturas Construidas (Cabana, Secador, Coletor, Cesto, Bau)
        if (NearbyBlueprintSite != null && NearbyBlueprintSite.GetDistanceToPlayer(transform.position) <= 4.6f)
        {
            currentKind = TargetKind.BlueprintSite;
            targetBlueprint = NearbyBlueprintSite;
            HasValidTarget = true;
            CurrentPromptLabel = FormatBlueprintPrompt(NearbyBlueprintSite);
            CurrentIcon = FragaProIconLibrary.ControlIcon.BuildHammer;
            RefreshHighlightForCurrentTarget();
            UpdateCrosshairFromCurrentKind();
            PushPromptToHUD();
            return;
        }

        StructureHealth nearestStruct = null;
        float bestStructDistSq = 3.2f * 3.2f;
        for (int i = 0; i < StructureHealth.AllStructures.Count; i++)
        {
            StructureHealth sh = StructureHealth.AllStructures[i];
            if (sh == null) continue;
            float dSq = (sh.transform.position - transform.position).sqrMagnitude;
            if (dSq < bestStructDistSq)
            {
                Collider sc = sh.GetComponent<Collider>() ?? sh.GetComponentInChildren<Collider>();
                if (sc != null && EvaluateColliderTarget(sc))
                {
                    bestStructDistSq = dSq;
                    nearestStruct = sh;
                }
            }
        }
        ResetTarget();
        if (nearestStruct != null)
        {
            Collider sc = nearestStruct.GetComponent<Collider>() ?? nearestStruct.GetComponentInChildren<Collider>();
            if (sc != null && EvaluateColliderTarget(sc))
            {
                RefreshHighlightForCurrentTarget();
                UpdateCrosshairFromCurrentKind();
                PushPromptToHUD();
                return;
            }
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
                    ClearHighlight3D();
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
                CurrentPromptLabel = "Coletar Agua Turva [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            }
            ClearHighlight3D();
            crosshairContext = HUDSurvivalOverlay.CrosshairContext.Interactable;
            PushPromptToHUD();
            return;
        }

        // 5. Fallback: soltar tora do ombro
        if (LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs > 0)
        {
            currentKind = TargetKind.DropCarriedLog;
            HasValidTarget = true;
            CurrentPromptLabel = "Soltar Tora [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.DropItem;
            crosshairContext = HUDSurvivalOverlay.CrosshairContext.Interactable;
        }

        ClearHighlight3D();
        PushPromptToHUD();
    }

    private void UpdateCrosshairFromCurrentKind()
    {
        if (currentKind == TargetKind.MineableRock)
            crosshairContext = HUDSurvivalOverlay.CrosshairContext.Rock;
        else if (currentKind == TargetKind.WorldTree || currentKind == TargetKind.FallenLog)
            crosshairContext = HUDSurvivalOverlay.CrosshairContext.Tree;
        else
            crosshairContext = HUDSurvivalOverlay.CrosshairContext.Interactable;
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
        targetDoor = null;
        targetLootChest = null;
        targetLoreNote = null;
        targetStructureRepair = null;
        targetCarcass = null;
        targetTrap = null;
        targetCollider = null;
        targetPickup = null;
        targetSpear = null;
        targetBonfire = null;
        targetRockNode = null;
        targetTileId = 0;
        crosshairContext = HUDSurvivalOverlay.CrosshairContext.Default;
    }

    private bool HasAxeAvailable()
    {
        if (ChoppingAndCombatSystem.IsAxeEquipped(inventory)) return true;
        return inventory != null && (inventory.HasItem(300) || inventory.HasItem(312));
    }

    private bool HasRockToolAvailable()
    {
        if (ChoppingAndCombatSystem.IsRockToolEquipped(inventory)) return true;
        return inventory != null && (inventory.HasItem(301) || inventory.HasItem(300) || inventory.HasItem(312));
    }

    private string FormatBlueprintPrompt(WorldBlueprintSite site)
    {
        int bagWood = inventory != null ? inventory.GetItemCount(100) : 0;
        int shoulderLogs = LogCarryingSystem.Instance != null ? LogCarryingSystem.Instance.currentLogs : 0;
        int bagStone = inventory != null ? inventory.GetItemCount(101) : 0;
        bool canDepositWood = site.currentLogs < site.neededLogs && (bagWood + shoulderLogs) > 0;
        bool canDepositStone = site.currentStones < site.neededStones && bagStone > 0;
        string verb = (canDepositWood || canDepositStone) ? "Depositar" : "Obra";
        return $"{verb} {site.structureName} ({site.currentLogs}/{site.neededLogs} Mad, {site.currentStones}/{site.neededStones} Ped) [E]";
    }

    private bool EvaluateColliderTarget(Collider c)
    {
        WorldBlueprintSite site = c.GetComponentInParent<WorldBlueprintSite>();
        if (site != null)
        {
            currentKind = TargetKind.BlueprintSite;
            targetBlueprint = site;
            HasValidTarget = true;
            CurrentPromptLabel = FormatBlueprintPrompt(site);
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

        DoorInteractable door = c.GetComponentInParent<DoorInteractable>();
        if (door != null)
        {
            currentKind = TargetKind.Door;
            targetDoor = door;
            HasValidTarget = true;
            CurrentPromptLabel = door.GetPromptLabel();
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        LootContainer chest = c.GetComponentInParent<LootContainer>();
        if (chest != null && !chest.IsOpened)
        {
            currentKind = TargetKind.LootChest;
            targetLootChest = chest;
            HasValidTarget = true;
            CurrentPromptLabel = $"Saquear {chest.containerName} [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        LoreNoteInteractable note = c.GetComponentInParent<LoreNoteInteractable>();
        if (note != null)
        {
            currentKind = TargetKind.LoreNote;
            targetLoreNote = note;
            HasValidTarget = true;
            CurrentPromptLabel = $"Ler {note.noteTitle} [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        StructureHealth structHp = c.GetComponentInParent<StructureHealth>();
        if (structHp != null && structHp.IsDamaged)
        {
            currentKind = TargetKind.StructureRepair;
            targetStructureRepair = structHp;
            HasValidTarget = true;
            CurrentPromptLabel = $"Consertar {structHp.structureName} ({Mathf.RoundToInt(structHp.HealthRatio * 100f)}% | 1 Mad) [E]";
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
            CurrentPromptLabel = hasKnife ? $"Esfolar {carcass.animalDisplayName} (Bonus Faca) [E]" : $"Esfolar {carcass.animalDisplayName} [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.AttackKnife;
            return true;
        }

        TrapInteractable trap = c.GetComponentInParent<TrapInteractable>();
        if (trap != null)
        {
            currentKind = TargetKind.Trap;
            targetTrap = trap;
            HasValidTarget = true;
            CurrentPromptLabel = trap.GetPromptLabel();
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        ThrownSpearProjectile spear = c.GetComponentInParent<ThrownSpearProjectile>();
        if (spear != null && spear.CanBePickedUp)
        {
            currentKind = TargetKind.SpearPickup;
            targetSpear = spear;
            HasValidTarget = true;
            CurrentPromptLabel = "Recuperar Lanca Celta [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.AttackSpear;
            return true;
        }

        if (IsLogCollider(c) && LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.CanPickupLog)
        {
            currentKind = TargetKind.LogPickup;
            targetCollider = c;
            HasValidTarget = true;
            CurrentPromptLabel = "Carregar Tora no Ombro [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        ResourcePickup pickup = c.GetComponent<ResourcePickup>() ?? c.GetComponentInParent<ResourcePickup>();
        if (pickup != null)
        {
            currentKind = TargetKind.WorldPickup;
            targetPickup = pickup;
            ItemData d = ItemDatabase.GetItem(pickup.itemID);
            HasValidTarget = true;
            string itemLabel = d != null ? d.itemName : "Item";
            CurrentPromptLabel = $"Pegar {itemLabel} no Chao (x{pickup.amount}) [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        BonfireEntity bonfire = c.GetComponentInParent<BonfireEntity>();
        if (bonfire != null)
        {
            currentKind = TargetKind.Bonfire;
            targetBonfire = bonfire;
            HasValidTarget = true;
            CurrentPromptLabel = bonfire.GetPromptLabel(inventory);
            CurrentIcon = FragaProIconLibrary.ControlIcon.CookFire;
            return true;
        }

        CraftingStationMarker stationMarker = c.GetComponentInParent<CraftingStationMarker>();
        if (stationMarker != null)
        {
            if (stationMarker.stationType == CraftingStationType.Workbench)
            {
                currentKind = TargetKind.Workbench;
                HasValidTarget = true;
                CurrentPromptLabel = "Mesa de Trabalho (Fabricar / Consertar) [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Workbench;
                return true;
            }
            if (stationMarker.stationType == CraftingStationType.Cauldron)
            {
                currentKind = TargetKind.Cauldron;
                HasValidTarget = true;
                CurrentPromptLabel = "Caldeirao Druida (Alquimia) [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Cauldron;
                return true;
            }
        }

        // Troncos Caidos -> Machado (Clique do Mouse ou [E])
        if (ChoppingAndCombatSystem.IsFallenLogCollider(c))
        {
            currentKind = TargetKind.FallenLog;
            targetCollider = c;
            HasValidTarget = true;
            bool axeAvail = HasAxeAvailable();
            CurrentPromptLabel = axeAvail
                ? "Cortar Tronco Caido com Machado [CLIQUE OU E]"
                : "Tronco Caido (Requer Machado - Pegue Galhos no Chao com [E])";
            CurrentIcon = FragaProIconLibrary.ControlIcon.AttackAxe;
            return true;
        }

        // Arbustos, Bagas, Cogumelos e Samambaias (Fentos) -> Coletaveis sem ferramenta com [E]!
        if (ChoppingAndCombatSystem.IsHarvestablePlantCollider(c, out int plantKind))
        {
            currentKind = TargetKind.BerryOrMushroom;
            targetCollider = c;
            targetGridPos = new Vector2Int(Mathf.RoundToInt(c.transform.position.x), Mathf.RoundToInt(c.transform.position.z));
            targetTileId = plantKind;
            HasValidTarget = true;
            if (plantKind == 9)
                CurrentPromptLabel = "Colher Bagas e Ervas [E]";
            else if (plantKind == 19)
                CurrentPromptLabel = "Colher Ervas / Fibra p/ Corda [E]";
            else if (plantKind == 20)
                CurrentPromptLabel = "Colher Cogumelo Luminoso e Ervas [E]";
            else if (ContainsOrdinalIgnoreCase(c.gameObject.name, "mushroom") || ContainsOrdinalIgnoreCase(c.transform.root.name, "mushroom"))
                CurrentPromptLabel = "Colher Cogumelo e Ervas [E]";
            else
                CurrentPromptLabel = "Colher Ervas (2 Ervas = 2 Cordas no menu [I]) [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        // Rochas e Monumentos Mineraveis -> Machado ou Picareta (Clique do Mouse ou [E])
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
                bool rockToolAvail = HasRockToolAvailable();
                CurrentPromptLabel = rockToolAvail
                    ? "Quebrar Rocha [CLIQUE DO MOUSE OU E]"
                    : "Rocha Grande (Requer Machado/Picareta - Pegue Pedras no Chao com [E])";
            }
            CurrentIcon = FragaProIconLibrary.ControlIcon.AttackPickaxe;
            return true;
        }

        // Arvores do Mundo (Carvalhos, Pinheiros, Arvores Secas) -> Machado (Clique do Mouse ou [E])
        if (ChoppingAndCombatSystem.IsTreeCollider(c))
        {
            currentKind = TargetKind.WorldTree;
            targetCollider = c;
            HasValidTarget = true;
            bool axeAvail = HasAxeAvailable();
            CurrentPromptLabel = axeAvail
                ? "Cortar Arvore com Machado [CLIQUE DO MOUSE OU E]"
                : "Arvore (Requer Machado - Pegue Galhos no Chao com [E])";
            CurrentIcon = FragaProIconLibrary.ControlIcon.AttackAxe;
            return true;
        }

        // Pedras Runicas e Sepulturas Antigas por nome
        string objName = c.gameObject.name;
        string rootName = c.transform.root.name;
        if (ContainsOrdinalIgnoreCase(objName, "runic") || ContainsOrdinalIgnoreCase(rootName, "runic"))
        {
            currentKind = TargetKind.SceneryTile;
            targetCollider = c;
            targetGridPos = new Vector2Int(Mathf.RoundToInt(c.transform.position.x), Mathf.RoundToInt(c.transform.position.z));
            targetTileId = 17;
            HasValidTarget = true;
            CurrentPromptLabel = "Ler Pedra Runica (+Sanidade) [E]";
            CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
            return true;
        }

        if (ContainsOrdinalIgnoreCase(objName, "grave") || ContainsOrdinalIgnoreCase(rootName, "grave"))
        {
            currentKind = TargetKind.SceneryTile;
            targetCollider = c;
            targetGridPos = new Vector2Int(Mathf.RoundToInt(c.transform.position.x), Mathf.RoundToInt(c.transform.position.z));
            targetTileId = 22;
            HasValidTarget = true;
            CurrentPromptLabel = "Escavar Sepultura Antiga [E]";
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
                targetBonfire = NearbyBonfire;
                HasValidTarget = true;
                CurrentPromptLabel = targetBonfire != null
                    ? targetBonfire.GetPromptLabel(inventory)
                    : "Fogueira (Cozinhar / Fabricar) [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.CookFire;
                return true;
            case 13:
                currentKind = TargetKind.Workbench;
                HasValidTarget = true;
                CurrentPromptLabel = "Mesa de Trabalho (Fabricar / Consertar) [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Workbench;
                return true;
            case 15:
                currentKind = TargetKind.Cauldron;
                HasValidTarget = true;
                CurrentPromptLabel = "Caldeirao Druida (Alquimia) [E]";
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
                    CurrentPromptLabel = "Coletar Agua Turva [E]";
                    CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                }
                return true;
            case 9:
                currentKind = TargetKind.BerryOrMushroom;
                HasValidTarget = true;
                CurrentPromptLabel = "Colher Bagas Silvestres [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 10:
            case 19:
            case 20:
                currentKind = TargetKind.BerryOrMushroom;
                HasValidTarget = true;
                CurrentPromptLabel = "Colher Ervas / Fibra p/ Corda [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 14:
                if (inventory != null && inventory.HasItem(112))
                {
                    currentKind = TargetKind.FarmPlot;
                    HasValidTarget = true;
                    CurrentPromptLabel = "Plantar Sementes Silvestres [E]";
                    CurrentIcon = FragaProIconLibrary.ControlIcon.AttackHoe;
                    return true;
                }
                return false;
            case 0:
                if (inventory != null && inventory.equippedMainHandId == 307)
                {
                    currentKind = TargetKind.FarmPlot;
                    HasValidTarget = true;
                    CurrentPromptLabel = "Lavrar Terra com Enxada [E]";
                    CurrentIcon = FragaProIconLibrary.ControlIcon.AttackHoe;
                    return true;
                }
                return false;
            case 17:
                currentKind = TargetKind.SceneryTile;
                HasValidTarget = true;
                CurrentPromptLabel = "Ler Pedra Runica (+Sanidade) [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 18:
                currentKind = TargetKind.SceneryTile;
                HasValidTarget = true;
                bool axeEq18 = HasAxeAvailable();
                CurrentPromptLabel = axeEq18
                    ? "Cortar Tronco Caido com Machado [CLIQUE OU E]"
                    : "Tronco Caido (Requer Machado - Pegue Galhos no Chao com [E])";
                CurrentIcon = FragaProIconLibrary.ControlIcon.AttackAxe;
                return true;
            case 22:
                currentKind = TargetKind.SceneryTile;
                HasValidTarget = true;
                CurrentPromptLabel = "Escavar Sepultura Antiga [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 30:
                currentKind = TargetKind.SceneryTile;
                HasValidTarget = true;
                CurrentPromptLabel = "Altar de Purificacao [E]";
                CurrentIcon = FragaProIconLibrary.ControlIcon.Interact;
                return true;
            case 1:
                currentKind = TargetKind.ResourceGrid;
                HasValidTarget = true;
                bool axeEq1 = HasAxeAvailable();
                CurrentPromptLabel = axeEq1
                    ? "Cortar Arvore com Machado [CLIQUE DO MOUSE OU E]"
                    : "Arvore (Requer Machado - Pegue Galhos no Chao com [E])";
                CurrentIcon = FragaProIconLibrary.ControlIcon.AttackAxe;
                return true;
            case 2:
            case 8:
                currentKind = TargetKind.ResourceGrid;
                HasValidTarget = true;
                bool rockEq = HasRockToolAvailable();
                CurrentPromptLabel = rockEq
                    ? "Quebrar Rocha [CLIQUE DO MOUSE OU E]"
                    : "Rocha Grande (Requer Machado/Picareta - Pegue Pedras no Chao com [E])";
                CurrentIcon = FragaProIconLibrary.ControlIcon.AttackPickaxe;
                return true;
        }

        return false;
    }

    private static bool ContainsOrdinalIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source) && source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsLogCollider(Collider c)
    {
        string n = c.gameObject.name;
        return ContainsOrdinalIgnoreCase(n, "felled_wood_log") || ContainsOrdinalIgnoreCase(n, "droppedwood");
    }

    public static string FormatPromptForDevice(string rawPrompt, bool isTouchActive, bool isHoldTarget)
    {
        if (string.IsNullOrEmpty(rawPrompt)) return string.Empty;
        if (!isTouchActive) return rawPrompt;

        string actionHint = isHoldTarget ? "(SEGURE)" : "(TOCAR)";
        string formatted = rawPrompt
            .Replace("[CLIQUE DO MOUSE OU E]", "(TOCAR / ATACAR)")
            .Replace("[E]", actionHint)
            .Replace("[X]", "(SEGURE)")
            .Replace("[I]", "(MOCHILA)")
            .Replace("[L]", "(LAMPIAO)");
        return formatted.Trim();
    }

    private void PushPromptToHUD()
    {
        if (HUDSurvivalOverlay.Instance != null)
        {
            bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
            bool holdTarget = IsHoldTarget;
            if (!lastPushedValid || !HasValidTarget || lastPushedTouch != touchActive || lastPushedHold != holdTarget || !string.Equals(lastPushedRawPrompt, CurrentPromptLabel, System.StringComparison.Ordinal))
            {
                lastPushedValid = HasValidTarget;
                lastPushedTouch = touchActive;
                lastPushedHold = holdTarget;
                lastPushedIcon = CurrentIcon;
                lastPushedRawPrompt = CurrentPromptLabel;
                lastPushedFormattedPrompt = HasValidTarget ? FormatPromptForDevice(CurrentPromptLabel, touchActive, holdTarget) : string.Empty;
            }
            HUDSurvivalOverlay.Instance.SetInteractionPrompt(HasValidTarget, lastPushedFormattedPrompt, lastPushedIcon, holdTarget);
            HUDSurvivalOverlay.Instance.SetCrosshairContext(crosshairContext);
        }
    }

    // =================== HIGHLIGHT 3D TATIL (URP-SAFE / ZERO-GC) ===================

    public static GameObject ResolveSafeHighlightRoot(Collider c)
    {
        if (c == null) return null;
        Transform cur = c.transform;
        while (cur != null)
        {
            string n = cur.name;
            if (n.StartsWith("Chunk_", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Environment_Instances", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Landmarks_Global", System.StringComparison.OrdinalIgnoreCase) ||
                n.Equals("MapProp_Pool", System.StringComparison.OrdinalIgnoreCase) ||
                n.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }

            if (cur.parent == null) break;
            string pn = cur.parent.name;
            if (pn.StartsWith("Chunk_", System.StringComparison.OrdinalIgnoreCase) ||
                pn.Equals("Environment_Instances", System.StringComparison.OrdinalIgnoreCase) ||
                pn.Equals("Landmarks_Global", System.StringComparison.OrdinalIgnoreCase) ||
                pn.Equals("MapProp_Pool", System.StringComparison.OrdinalIgnoreCase))
            {
                return cur.gameObject;
            }
            cur = cur.parent;
        }

        if (cur != null && (
            cur.name.StartsWith("Chunk_", System.StringComparison.OrdinalIgnoreCase) ||
            cur.name.Equals("Environment_Instances", System.StringComparison.OrdinalIgnoreCase) ||
            cur.name.Equals("Landmarks_Global", System.StringComparison.OrdinalIgnoreCase) ||
            cur.name.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return null;
        }
        return cur != null ? cur.gameObject : c.gameObject;
    }

    private GameObject ResolveCurrentHighlightRoot()
    {
        if (!HasValidTarget) return null;
        if (targetBlueprint != null) return targetBlueprint.gameObject;
        if (targetShelter != null) return targetShelter.gameObject;
        if (targetDryingRack != null) return targetDryingRack.gameObject;
        if (targetRainCollector != null) return targetRainCollector.gameObject;
        if (targetLogHolder != null) return targetLogHolder.gameObject;
        if (targetStorageChest != null) return targetStorageChest.gameObject;
        if (targetDoor != null) return targetDoor.gameObject;
        if (targetLootChest != null) return targetLootChest.gameObject;
        if (targetLoreNote != null) return targetLoreNote.gameObject;
        if (targetStructureRepair != null) return targetStructureRepair.gameObject;
        if (targetCarcass != null) return targetCarcass.gameObject;
        if (targetTrap != null) return targetTrap.gameObject;
        if (targetSpear != null) return targetSpear.gameObject;
        if (targetPickup != null) return targetPickup.gameObject;
        if (targetBonfire != null) return targetBonfire.gameObject;
        if (targetRockNode != null) return targetRockNode.gameObject;
        if (targetCollider != null) return ResolveSafeHighlightRoot(targetCollider);
        return null;
    }

    private void RefreshHighlightForCurrentTarget()
    {
        GameObject nextRoot = ResolveCurrentHighlightRoot();
        if (nextRoot == activeHighlightRoot) return;

        ClearHighlight3D();
        if (nextRoot == null) return;

        activeHighlightRoot = nextRoot;
        tempRendererBuffer.Clear();
        nextRoot.GetComponentsInChildren<Renderer>(false, tempRendererBuffer);
        if (highlightMpb == null) highlightMpb = new MaterialPropertyBlock();

        for (int i = 0; i < tempRendererBuffer.Count; i++)
        {
            Renderer r = tempRendererBuffer[i];
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Material mat = r.sharedMaterial;
            if (mat == null) continue;

            r.GetPropertyBlock(highlightMpb);
            bool hasBase = mat.HasProperty(BaseColorPropId);
            bool hasCol = mat.HasProperty(ColorPropId);
            bool hasEm = mat.HasProperty(EmissionColorPropId);
            if (!hasBase && !hasCol && !hasEm) continue;

            Color mpbBase = hasBase ? highlightMpb.GetColor(BaseColorPropId) : Color.clear;
            Color mpbCol = hasCol ? highlightMpb.GetColor(ColorPropId) : Color.clear;
            Color mpbEm = hasEm ? highlightMpb.GetColor(EmissionColorPropId) : Color.clear;
            bool hadOverride = mpbBase.a > 0.001f || mpbCol.a > 0.001f || (mpbEm.r + mpbEm.g + mpbEm.b) > 0.0001f;

            HighlightRendererCache entry = new HighlightRendererCache
            {
                renderer = r,
                hasBaseColor = hasBase,
                hasColor = hasCol,
                hasEmission = hasEm,
                baseColor = (hasBase && mpbBase.a > 0.001f) ? mpbBase : (hasBase ? mat.GetColor(BaseColorPropId) : Color.white),
                legacyColor = (hasCol && mpbCol.a > 0.001f) ? mpbCol : (hasCol ? mat.GetColor(ColorPropId) : Color.white),
                baseEmission = (hasEm && (mpbEm.r + mpbEm.g + mpbEm.b) > 0.0001f) ? mpbEm : (hasEm ? mat.GetColor(EmissionColorPropId) : Color.black),
                hadMpbOverride = hadOverride
            };
            highlightRenderers.Add(entry);
        }

        highlightPulsePhase = 0f;
        ApplyHighlightPulse(0.75f);
    }

    private void UpdateHighlightPulse(float dt)
    {
        if (activeHighlightRoot == null || highlightRenderers.Count == 0) return;
        highlightPulsePhase += dt * 4.5f;
        float pulse = 0.52f + 0.48f * Mathf.Sin(highlightPulsePhase);
        if (IsHoldInProgress) pulse = Mathf.Lerp(0.75f, 1.0f, HoldProgress01);
        ApplyHighlightPulse(pulse);
    }

    private Color GetHighlightTintForTarget()
    {
        if (currentKind == TargetKind.BlueprintSite || currentKind == TargetKind.StructureRepair || currentKind == TargetKind.Trap)
            return new Color(1.0f, 0.80f, 0.34f, 1f);
        if (currentKind == TargetKind.Carcass || currentKind == TargetKind.LootChest)
            return new Color(1.0f, 0.65f, 0.36f, 1f);
        return new Color(0.36f, 0.96f, 0.88f, 1f);
    }

    private void ApplyHighlightPulse(float pulse01)
    {
        if (highlightMpb == null) highlightMpb = new MaterialPropertyBlock();
        Color tint = GetHighlightTintForTarget();
        float strength = Mathf.Clamp01(pulse01) * (IsHoldInProgress ? 0.55f : 0.36f);

        for (int i = 0; i < highlightRenderers.Count; i++)
        {
            HighlightRendererCache c = highlightRenderers[i];
            if (c.renderer == null) continue;

            c.renderer.GetPropertyBlock(highlightMpb);
            if (c.hasBaseColor)
            {
                Color hi = Color.Lerp(c.baseColor, c.baseColor * 1.25f + tint * 0.28f, strength);
                hi.a = c.baseColor.a;
                highlightMpb.SetColor(BaseColorPropId, hi);
            }
            if (c.hasColor)
            {
                Color hi = Color.Lerp(c.legacyColor, c.legacyColor * 1.25f + tint * 0.28f, strength);
                hi.a = c.legacyColor.a;
                highlightMpb.SetColor(ColorPropId, hi);
            }
            if (c.hasEmission)
            {
                highlightMpb.SetColor(EmissionColorPropId, c.baseEmission + tint * (0.26f * strength));
            }
            c.renderer.SetPropertyBlock(highlightMpb);
        }
    }

    public void ClearHighlight3D()
    {
        if (highlightRenderers.Count > 0)
        {
            if (highlightMpb == null) highlightMpb = new MaterialPropertyBlock();
            for (int i = 0; i < highlightRenderers.Count; i++)
            {
                HighlightRendererCache c = highlightRenderers[i];
                if (c.renderer == null) continue;

                if (!c.hadMpbOverride)
                {
                    c.renderer.SetPropertyBlock(null);
                }
                else
                {
                    c.renderer.GetPropertyBlock(highlightMpb);
                    if (c.hasBaseColor) highlightMpb.SetColor(BaseColorPropId, c.baseColor);
                    if (c.hasColor) highlightMpb.SetColor(ColorPropId, c.legacyColor);
                    if (c.hasEmission) highlightMpb.SetColor(EmissionColorPropId, c.baseEmission);
                    c.renderer.SetPropertyBlock(highlightMpb);
                }
            }
            highlightRenderers.Clear();
        }
        activeHighlightRoot = null;
    }

    // =================== MAQUINA DE HOLD RADIAL ===================

    private float GetCurrentHoldDuration()
    {
        switch (currentKind)
        {
            case TargetKind.Carcass:
                return (inventory != null && inventory.equippedMainHandId == 302) ? 0.80f : 1.25f;
            case TargetKind.Trap:
                return 0.75f;
            case TargetKind.LootChest:
                return 0.65f;
            case TargetKind.WaterGather:
                return 0.65f;
            case TargetKind.RainCollector:
                return (targetRainCollector != null && targetRainCollector.waterCharges > 0) ? 0.65f : 0f;
            case TargetKind.StructureRepair:
                return 0.65f;
            case TargetKind.Shelter:
                return 0.75f;
            case TargetKind.SceneryTile:
                return targetTileId == 22 ? 0.90f : 0f;
            default:
                return 0f;
        }
    }

    private string GetHoldActionVerb()
    {
        switch (currentKind)
        {
            case TargetKind.Carcass: return "ESFOLANDO...";
            case TargetKind.Trap: return (targetTrap != null && targetTrap.isArmed) ? "RECOLHENDO..." : "ARMANDO...";
            case TargetKind.LootChest: return "SAQUEANDO...";
            case TargetKind.WaterGather: return "COLETANDO AGUA...";
            case TargetKind.RainCollector: return "COLETANDO AGUA...";
            case TargetKind.StructureRepair: return "CONSERTANDO...";
            case TargetKind.Shelter: return "DESCANSANDO...";
            case TargetKind.SceneryTile: return targetTileId == 22 ? "ESCAVANDO..." : "INTERAGINDO...";
            default: return string.Empty;
        }
    }

    private int GetCurrentTargetIdentityHash()
    {
        int h = (int)currentKind * 73856093;
        if (targetCarcass != null) return h ^ targetCarcass.GetHashCode();
        if (targetTrap != null) return h ^ targetTrap.GetHashCode();
        if (targetLootChest != null) return h ^ targetLootChest.GetHashCode();
        if (targetRainCollector != null) return h ^ targetRainCollector.GetHashCode();
        if (targetStructureRepair != null) return h ^ targetStructureRepair.GetHashCode();
        if (targetShelter != null) return h ^ targetShelter.GetHashCode();
        if (targetCollider != null) return h ^ targetCollider.GetHashCode();
        return h ^ (targetGridPos.x * 19349663) ^ (targetGridPos.y * 83492791) ^ targetTileId;
    }

    private void ResetHoldState()
    {
        activeHoldIdentity = 0;
        holdElapsed = 0f;
        HoldProgress01 = 0f;
        IsHoldInProgress = false;
        holdCompletedRequireRelease = false;
        holdStartAudioPlayed = false;
        if (FirstPersonViewmodelController.Instance != null)
            FirstPersonViewmodelController.Instance.SetInteractionHoldPose(false, 0f);
    }

    private void PlayHoldStartFoley()
    {
        if (holdStartAudioPlayed || AudioManager.Instance == null) return;
        holdStartAudioPlayed = true;
        Vector3 pos = activeHighlightRoot != null ? activeHighlightRoot.transform.position : transform.position;
        switch (currentKind)
        {
            case TargetKind.WaterGather:
            case TargetKind.RainCollector:
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupWater, pos, 0.55f, 0.92f, 1.08f);
                break;
            case TargetKind.LootChest:
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.ChestWoodLatch, pos, 0.50f, 0.95f, 1.05f);
                break;
            case TargetKind.Trap:
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.TrapMetalSnap, pos, 0.45f, 0.85f, 0.98f);
                break;
            default:
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.EquipWeapon, pos, 0.42f, 0.90f, 1.05f);
                break;
        }
    }

    public void AdvanceHoldInteraction(bool pressedThisFrame, bool isHeld, bool upThisFrame, float deltaTime)
    {
        if (upThisFrame || !isHeld)
        {
            holdCompletedRequireRelease = false;
        }

        if (!HasValidTarget)
        {
            if (IsHoldInProgress || holdElapsed > 0f)
            {
                holdElapsed = 0f;
                HoldProgress01 = 0f;
                IsHoldInProgress = false;
                activeHoldIdentity = 0;
                holdStartAudioPlayed = false;
                if (FirstPersonViewmodelController.Instance != null)
                    FirstPersonViewmodelController.Instance.SetInteractionHoldPose(false, 0f);
            }
            if (pressedThisFrame)
            {
                ExecuteCurrentInteraction();
            }
            return;
        }

        float requiredDuration = GetCurrentHoldDuration();
        if (requiredDuration <= 0.001f)
        {
            if (IsHoldInProgress || holdElapsed > 0f)
            {
                holdElapsed = 0f;
                HoldProgress01 = 0f;
                IsHoldInProgress = false;
                activeHoldIdentity = 0;
                holdStartAudioPlayed = false;
                if (FirstPersonViewmodelController.Instance != null)
                    FirstPersonViewmodelController.Instance.SetInteractionHoldPose(false, 0f);
            }
            if (pressedThisFrame)
            {
                ExecuteCurrentInteraction();
            }
            return;
        }

        int targetId = GetCurrentTargetIdentityHash();
        if (targetId != activeHoldIdentity)
        {
            activeHoldIdentity = targetId;
            holdElapsed = 0f;
            HoldProgress01 = 0f;
            IsHoldInProgress = false;
            holdCompletedRequireRelease = false;
            holdStartAudioPlayed = false;
        }

        if (isHeld && !holdCompletedRequireRelease)
        {
            IsHoldInProgress = true;
            PlayHoldStartFoley();
            holdElapsed += Mathf.Max(0f, deltaTime);
            HoldProgress01 = Mathf.Clamp01(holdElapsed / requiredDuration);
            if (FirstPersonViewmodelController.Instance != null)
                FirstPersonViewmodelController.Instance.SetInteractionHoldPose(true, HoldProgress01);

            if (holdElapsed >= requiredDuration)
            {
                holdElapsed = 0f;
                HoldProgress01 = 0f;
                IsHoldInProgress = false;
                holdCompletedRequireRelease = true;
                holdStartAudioPlayed = false;
                if (FirstPersonViewmodelController.Instance != null)
                {
                    FirstPersonViewmodelController.Instance.SetInteractionHoldPose(false, 0f);
                    FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
                }
                if (FirstPersonCamera.Instance != null)
                    FirstPersonCamera.Instance.AddShake(0.03f);
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.TriggerHitMarker(isCrit: false, isKill: false);
                ExecuteCurrentInteraction();
            }
        }
        else if (IsHoldInProgress || holdElapsed > 0f)
        {
            holdElapsed = 0f;
            HoldProgress01 = 0f;
            IsHoldInProgress = false;
            holdStartAudioPlayed = false;
            if (FirstPersonViewmodelController.Instance != null)
                FirstPersonViewmodelController.Instance.SetInteractionHoldPose(false, 0f);
        }
    }

    private void InteractWithBonfire(BonfireEntity bf)
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        bool hasTorch = inventory != null && inventory.equippedOffHandId == 303;
        bool hasLighter = PlayerController.Instance != null && PlayerController.Instance.IsLighterOn;

        if (bf != null && !bf.isLit)
        {
            if (hasTorch || hasLighter || (inventory != null && inventory.HasItem(205)))
            {
                if (!hasTorch && !hasLighter) inventory.RemoveItem(205, 1);
                bf.AddFuel(100f);
                bf.Ignite();
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.FireIgnite, bf.transform.position, 0.72f, 0.94f, 1.08f);
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip("Fogueira acesa!");
            }
            else if (HUDSurvivalOverlay.Instance != null)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.LighterFlick, bf.transform.position, 0.50f, 0.92f, 1.06f);
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Fogueira apagada! Use Fosforos, Tocha ou Lampiao [L] para acender.");
            }
            return;
        }

        if (bf != null && bf.isLit)
        {
            if (bf.ReadySlotCount > 0)
            {
                bf.TryCollectReadyItems(inventory, out string summary);
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip(summary);
                return;
            }

            if (bf.EmptySlotCount > 0 && bf.HasAnyAutoCookableInInventory(inventory))
            {
                if (bf.TryPlaceCookItemFromInventory(inventory, -1, out string placeMsg))
                {
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip(placeMsg);
                    return;
                }
            }

            if (bf.fuelLevel < 40f && inventory != null && inventory.HasItem(100))
            {
                if (bf.TryAddWoodFuel(inventory, out string fuelMsg))
                {
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip(fuelMsg);
                    return;
                }
            }
        }

        if (SurvivalInventoryCraftUI.Instance != null)
        {
            SurvivalInventoryCraftUI.Instance.OpenCraftingAtStation(CraftingStationType.Bonfire);
        }
        else
        {
            var cook = FindAnyObjectByType<CampfireCookingSystem>();
            if (cook != null) cook.TryCookFood();
        }
    }

    public void ExecuteCurrentInteraction()
    {
        if (!HasValidTarget)
        {
            if (NearbyBlueprintSite != null && !NearbyBlueprintSite.IsComplete)
            {
                NearbyBlueprintSite.TryDepositResource(gameObject);
                nextScanTime = 0f;
                return;
            }
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

            case TargetKind.Door:
                if (targetDoor != null)
                    targetDoor.ToggleDoor();
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

            case TargetKind.Trap:
                if (targetTrap != null)
                {
                    if (gm == null) gm = FindAnyObjectByType<GameManager>();
                    MapRenderer mr = FindAnyObjectByType<MapRenderer>();
                    targetTrap.Interact(inventory, gm, mr);
                }
                break;

            case TargetKind.SpearPickup:
                if (targetSpear != null)
                {
                    targetSpear.Collect(inventory);
                    if (FirstPersonViewmodelController.Instance != null)
                        FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayPickupSoundForItem(308, targetSpear.transform.position);
                }
                break;

            case TargetKind.LogPickup:
                if (targetCollider != null && LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.CanPickupLog)
                {
                    LogCarryingSystem.Instance.PickupLog();
                    if (FirstPersonViewmodelController.Instance != null)
                        FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupWood, targetCollider.transform.position, 0.72f);
                    ResourcePickup logPickup = targetCollider.GetComponentInParent<ResourcePickup>();
                    SafeDestroy(logPickup != null ? logPickup.gameObject : targetCollider.gameObject);
                }
                break;

            case TargetKind.WorldPickup:
                if (targetPickup != null && inventory != null)
                {
                    int leftover = inventory.AddItem(targetPickup.itemID, targetPickup.amount);
                    int collected = targetPickup.amount - leftover;
                    if (collected > 0 && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayPickupSoundForItem(targetPickup.itemID, targetPickup.transform.position);
                        if (FirstPersonViewmodelController.Instance != null)
                            FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
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
                InteractWithBonfire(targetBonfire ?? NearbyBonfire);
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
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupWater, transform.position, 0.70f);
                        if (FirstPersonViewmodelController.Instance != null)
                            FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip("Agua Turva coletada - ferva na Fogueira para purificar.");
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

            case TargetKind.WorldTree:
                if (targetCollider != null && ChoppingAndCombatSystem.Instance != null)
                {
                    ChoppingAndCombatSystem.Instance.HarvestTreeFromInteraction(targetCollider);
                }
                break;

            case TargetKind.FallenLog:
                if (!ChoppingAndCombatSystem.EnsureAxeEquipped(inventory, true))
                {
                    if (ChoppingAndCombatSystem.Instance != null)
                        ChoppingAndCombatSystem.Instance.ShowToolRequiredForWoodWarning();
                }
                else if (targetCollider != null && ChoppingAndCombatSystem.Instance != null)
                {
                    ItemData weaponData = inventory != null ? ItemDatabase.GetItem(inventory.equippedMainHandId) : null;
                    float treeMult = weaponData != null && weaponData.harvestTreeMultiplier > 1.01f ? weaponData.harvestTreeMultiplier : 2.2f;
                    ChoppingAndCombatSystem.Instance.HarvestFallenLogCollider(targetCollider, targetCollider.transform.position, treeMult);
                }
                break;

            case TargetKind.BerryOrMushroom:
                if (targetCollider != null && ChoppingAndCombatSystem.Instance != null)
                {
                    ChoppingAndCombatSystem.Instance.HarvestPlantObject(targetCollider, targetTileId > 0 ? targetTileId : 10, targetCollider.transform.position);
                }
                else if (gm != null && inventory != null)
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
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupHerb, transform.position, 0.72f);
                        if (FirstPersonViewmodelController.Instance != null)
                            FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
                    }
                    else
                    {
                        int leftover = inventory.AddItem(103, 2);
                        bool extraMush = Random.value > 0.55f;
                        int mushLeft = extraMush ? inventory.AddItem(203, 1) : 0;
                        if (leftover > 0 && LootManager.Instance != null)
                            LootManager.Instance.DropItem(103, leftover, transform.position + transform.forward);
                        if (mushLeft > 0 && LootManager.Instance != null)
                            LootManager.Instance.DropItem(203, mushLeft, transform.position + transform.forward);
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupHerb, transform.position, 0.72f);
                        if (FirstPersonViewmodelController.Instance != null)
                            FirstPersonViewmodelController.Instance.TriggerGatherAnimation();
                    }
                }
                break;

            case TargetKind.SceneryTile:
                if (scenerySystem == null) scenerySystem = FindAnyObjectByType<SceneryInteractionSystem>();
                if (scenerySystem != null)
                {
                    scenerySystem.TryInteractAtTileOrObject(targetGridPos.x, targetGridPos.y, targetTileId, targetCollider);
                }
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
