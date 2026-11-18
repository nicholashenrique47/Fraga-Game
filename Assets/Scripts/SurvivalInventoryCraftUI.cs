using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

/// <summary>
/// SurvivalInventoryCraftUI: Hub Completo AAA de Mochila (24 slots), Paperdoll com Durabilidade,
/// Organizacao/Peso, Fabricacao em Tempo Real (Tiers 0-3), Status Clinico do Personagem (Anatomia + Cura Rapida)
/// e Painel de Grupo Co-op (Multiplayer-Ready). ZERO emojis — 100% icones vetoriais via FragaProIconLibrary.
/// </summary>
public class SurvivalInventoryCraftUI : MonoBehaviour
{
    public static SurvivalInventoryCraftUI Instance { get; private set; }

    [Header("Dependencies")]
    public InventoryAdvanced inventory;
    public CraftingSystem craftingSystem;
    public PlayerStatus playerStatus;
    public CameraTouchController cameraTouchZone;

    private struct SlotViewRefs
    {
        public GameObject root;
        public RectTransform rect;
        public Image bg;
        public Outline outline;
        public Image icon;
        public Text nameText;
        public Text qtyText;
        public Text tagText;
        public GameObject durBg;
        public Image durFill;
    }

    private struct HotbarDockRefs
    {
        public GameObject root;
        public RectTransform rect;
        public Image bg;
        public Outline outline;
        public Image icon;
        public Text keyText;
        public Text qtyText;
        public GameObject durBg;
        public Image durFill;
    }

    private enum DragSourceKind { None, BackpackSlot, HotbarDockSlot }

    private struct CraftCardRefs
    {
        public int recipeIndex;
        public GameObject root;
        public Image bg;
        public Outline outline;
        public Text reqText;
        public Button btnCraft1;
        public Image btnCraft1Bg;
        public Button btnCraft5;
        public Image btnCraft5Bg;
    }

    private struct VitalGaugeRefs
    {
        public Image fill;
        public Text valueText;
    }

    private struct TraumaCardRefs
    {
        public Image bg;
        public Outline outline;
        public Image accentBar;
        public Text stateText;
        public Text detailText;
        public Button treatBtn;
        public Image treatBtnBg;
        public Text treatBtnText;
        public Image remedyIcon;
    }

    private struct CoopMemberCardRefs
    {
        public GameObject root;
        public Image bg;
        public Image colorBadge;
        public Text nameText;
        public Text roleDistText;
        public Image hpFill;
        public Text statsText;
    }

    private CanvasGroup canvasGroup;
    private bool isOpen = false;
    public bool IsOpen => isOpen;
    public int ActiveTabIndex => activeTabIndex;

    private int activeTabIndex = 0;
    private GameObject rootPanel;
    private GameObject inventoryTabObj;
    private GameObject craftingTabObj;
    private GameObject characterTabObj;
    private GameObject coopTabObj;

    private readonly Image[] tabBgImages = new Image[4];
    private readonly Outline[] tabOutlines = new Outline[4];
    private readonly Text[] tabLabels = new Text[4];
    private Text tabInvText;
    private Text tabCraftText;
    private Text controlModeBtnText;
    private Image controlModeBtnBg;

    // Filtros Mochila & Fabricacao
    private ItemCategory? invCategoryFilter = null;
    private readonly List<Image> invFilterPillBgs = new List<Image>();
    private readonly List<ItemCategory?> invFilterPillKeys = new List<ItemCategory?>();
    private CraftingStationType? craftStationFilter = null;
    private bool onlyCraftableFilter = false;
    private Image onlyCraftableBtnBg;
    private Text onlyCraftableBtnText;
    private Text craftCountBannerText;
    private readonly List<Button> stationFilterButtons = new List<Button>();
    private readonly List<Image> stationFilterBgs = new List<Image>();
    private readonly List<Image> stationStatusDots = new List<Image>();

    // Paperdoll Equipamento & Status
    private Image mainHandGearIcon;
    private Text mainHandGearText;
    private Image mainHandDurFill;
    private Image offHandGearIcon;
    private Text offHandGearText;
    private Image offHandDurFill;
    private Image armorGearIcon;
    private Text armorGearText;
    private Image armorDurFill;
    private Text gearStatsSummaryText;

    // Painel de Detalhes
    private GameObject detailPane;
    private Image detailIconImg;
    private Text detailTitle;
    private Text detailDesc;
    private Text detailStats;
    private Image detailDurFill;
    private GameObject detailDurBg;
    private Button detailUseButton;
    private Text detailUseBtnText;
    private Button detailRepairButton;
    private Text detailRepairText;
    private Button detailHotbarButton;
    private Text detailHotbarText;
    private Button detailSplitButton;
    private Button detailSwapButton;
    private Text detailSwapText;
    private Button detailDropButton;
    private Text detailDropText;
    private Button detailShareButton;
    private Text detailShareText;

    private int selectedSlotIndex = -1;
    private int pendingSwapSlot = -1;
    private int nextHotbarAssignIdx = 0;
    private int lastClickedSlotIndex = -1;
    private float lastSlotClickTime = -10f;

    // Grid 24 slots cacheados (Zero-Find em RefreshUI)
    private readonly List<SlotViewRefs> slotViews = new List<SlotViewRefs>();
    private readonly List<int> displayedSlotIndices = new List<int>();
    private readonly HotbarDockRefs[] hotbarDockViews = new HotbarDockRefs[6];
    private readonly RectTransform[] gearCardRects = new RectTransform[3];
    private readonly Image[] gearCardBgs = new Image[3];
    private readonly Outline[] gearCardOutlines = new Outline[3];

    // Drag & Drop (PC + Mobile Touch) + Zona de Descarte
    private RectTransform rootRect;
    private RectTransform windowRect;
    private RectTransform discardDropRect;
    private Image discardDropBg;
    private Outline discardDropOutline;
    private Text discardDropLabel;
    private GameObject dragGhostObj;
    private RectTransform dragGhostRect;
    private Image dragGhostIcon;
    private Text dragGhostQty;
    private bool dragActive = false;
    private DragSourceKind dragSourceKind = DragSourceKind.None;
    private int dragSourceIndex = -1;
    private int dragItemId = -1;
    private int dragItemQty = 0;
    private int lastDragEndFrame = -1;

    // Modal AAA de Dividir / Descartar Pilha
    private GameObject stackModalRoot;
    private Image stackModalIcon;
    private Text stackModalHeaderText;
    private Text stackModalItemText;
    private Text stackModalAmountText;
    private Slider stackModalSlider;
    private Button stackModalBtnSplit;
    private Text stackModalSplitText;
    private Button stackModalBtnDrop;
    private Text stackModalDropText;
    private int stackModalSlotIndex = -1;
    private int stackModalSelectedAmount = 1;
    private bool stackModalDefaultDiscard = false;
    private bool stackModalSuppressSlider = false;

    public bool IsStackModalOpen => stackModalRoot != null && stackModalRoot.activeSelf;
    public int StackModalSlotIndex => stackModalSlotIndex;
    public int StackModalAmount => stackModalSelectedAmount;

    // Crafting Scroll & Guide cacheados
    private Text craftQuickGuideText;
    private RectTransform craftContentRT;
    private readonly List<CraftCardRefs> craftCards = new List<CraftCardRefs>();
    private Image craftProgressFill;
    private Text craftProgressLabel;
    private Text craftQueueDetailLabel;
    private Button btnCancelCurrentCraft;
    private Image btnCancelCurrentCraftBg;
    private Button btnCancelAllCraft;
    private Image btnCancelAllCraftBg;
    private CraftingSystem subscribedCrafting;
    private string lastCraftQueueBarSignature = string.Empty;

    // Aba 3: Status do Personagem
    private Image charConditionBannerBg;
    private Outline charConditionBannerOutline;
    private Text charConditionBannerText;
    private Image anatomyBodyImg;
    private Image anatomyHeadDot;
    private Image anatomyTorsoDot;
    private Image anatomyLimbDot;
    private Text anatomySummaryText;
    private Button btnTreatAllTraumas;
    private Image btnTreatAllBg;
    private Text btnTreatAllText;
    private Image charWeaponSlotIcon;
    private Image charArmorSlotIcon;
    private Image charDefenseMeterFill;
    private Image charInsulationMeterFill;
    private Image charVisibilityMeterFill;
    private Image charNoiseMeterFill;
    private Image charLanternOilMeterFill;
    private VitalGaugeRefs gaugeHealth;
    private VitalGaugeRefs gaugeStamina;
    private VitalGaugeRefs gaugeHunger;
    private VitalGaugeRefs gaugeThirst;
    private VitalGaugeRefs gaugeSanity;
    private VitalGaugeRefs gaugeTemp;
    private VitalGaugeRefs gaugeWetness;
    private Text charPhysiologyNoteText;
    private TraumaCardRefs traumaBleeding;
    private TraumaCardRefs traumaFracture;
    private TraumaCardRefs traumaPoison;
    private TraumaCardRefs traumaSickness;
    private TraumaCardRefs traumaHypothermia;
    private Text charCombatStatsText;
    private Text charStealthStatsText;
    private Text charWorldThreatText;

    // Aba 4: Grupo / Co-op
    private Text coopHeaderStatusText;
    private Text coopAccessBtnText;
    private Image coopAccessBtnBg;
    private Text coopPreviewBtnText;
    private Image coopPreviewBtnBg;
    private Text coopHostBtnText;
    private InputField coopIpInput;
    private readonly CoopMemberCardRefs[] coopMemberCards = new CoopMemberCardRefs[FragaCoopSession.MaxPartySize];

    private SanitySystem sanityCache;
    private float nextLiveRefreshTime = 0f;

    private void Awake()
    {
        Instance = this;
        FindDependencies();
        BuildUIHierarchy();
        CloseMenuInstant();
    }

    private void Start()
    {
        FindDependencies();
        if (inventory != null)
        {
            inventory.OnInventoryChanged += RefreshUI;
            inventory.OnEquipmentChanged += RefreshUI;
        }
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.OnTouchLayoutChanged += OnControlLayoutChanged;
        }
        if (FragaCoopSession.Instance != null)
        {
            FragaCoopSession.Instance.OnPartyUpdated += RefreshCoopTab;
        }
        RefreshControlModeButton();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= RefreshUI;
            inventory.OnEquipmentChanged -= RefreshUI;
        }
        if (subscribedCrafting != null)
        {
            subscribedCrafting.OnCraftQueueChanged -= OnCraftQueueUpdated;
            subscribedCrafting = null;
        }
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.OnTouchLayoutChanged -= OnControlLayoutChanged;
        }
        if (FragaCoopSession.Instance != null)
        {
            FragaCoopSession.Instance.OnPartyUpdated -= RefreshCoopTab;
        }
    }

    private void OnControlLayoutChanged(bool _)
    {
        RefreshControlModeButton();
    }

    private void OnCraftQueueUpdated()
    {
        if (!isOpen) return;
        if (activeTabIndex == 1)
        {
            if (onlyCraftableFilter) RebuildCraftCards();
            RefreshCraftQueueBar(true);
        }
        RefreshUI();
    }

    private void FindDependencies()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (craftingSystem == null) craftingSystem = CraftingSystem.Instance ?? FindAnyObjectByType<CraftingSystem>();
        if (craftingSystem != null && subscribedCrafting != craftingSystem)
        {
            if (subscribedCrafting != null)
                subscribedCrafting.OnCraftQueueChanged -= OnCraftQueueUpdated;
            subscribedCrafting = craftingSystem;
            subscribedCrafting.OnCraftQueueChanged += OnCraftQueueUpdated;
        }
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
        if (sanityCache == null) sanityCache = FindAnyObjectByType<SanitySystem>();
        if (cameraTouchZone == null) cameraTouchZone = FindAnyObjectByType<CameraTouchController>();
        FragaCoopSession.GetOrCreate();
    }

    private void Update()
    {
        bool blockedByOtherModal = SurvivalMapSystem.IsMapOpen ||
                                   StorageChestInteractable.IsAnyChestOpen ||
                                   UIManager.IsEndgameModalOpen ||
                                   (FragaOptionsMenu.Instance != null && FragaOptionsMenu.Instance.IsOpen) ||
                                   (BlueprintPlacementManager.Instance != null && (BlueprintPlacementManager.Instance.IsBookOpen || BlueprintPlacementManager.Instance.IsPlacingGhost));

        if (!blockedByOtherModal && Input.GetKeyDown(KeyCode.I))
        {
            ToggleMenu();
        }
        else if (!blockedByOtherModal && Input.GetKeyDown(KeyCode.K))
        {
            if (isOpen && activeTabIndex == 2) CloseMenu();
            else OpenCharacterStatusTab();
        }

        if (isOpen && IsStackModalOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseStackActionModal();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConfirmStackModalAction(stackModalSelectedAmount, stackModalDefaultDiscard);
                return;
            }
        }
        else if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
        }
        else if (isOpen && activeTabIndex == 0 && inventory != null && selectedSlotIndex >= 0 &&
                 selectedSlotIndex < inventory.slots.Length && !inventory.slots[selectedSlotIndex].IsEmpty())
        {
            for (int k = 0; k < 6; k++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + k))
                {
                    TryDropSlotToHotbar(selectedSlotIndex, k);
                    break;
                }
            }
            if (Input.GetKeyDown(KeyCode.Delete)) OnDropSelected();
        }

        if (isOpen && activeTabIndex == 1 && craftingSystem != null)
        {
            RefreshCraftQueueBar(false);
        }

        if (isOpen && (activeTabIndex == 2 || activeTabIndex == 3) && Time.unscaledTime >= nextLiveRefreshTime)
        {
            nextLiveRefreshTime = Time.unscaledTime + 0.20f;
            if (activeTabIndex == 2) RefreshCharacterStatusTab();
            else if (activeTabIndex == 3) RefreshCoopTab();
        }
    }

    public void ToggleMenu()
    {
        if (isOpen) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        FindDependencies();
        isOpen = true;
        pendingSwapSlot = -1;
        rootPanel.SetActive(true);
        rootPanel.transform.SetAsLastSibling();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (cameraTouchZone != null) cameraTouchZone.enabled = false;

        if (Input.touchCount == 0 && !Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        RefreshControlModeButton();
        if (activeTabIndex == 1)
        {
            RebuildCraftCards();
            RefreshCraftQueueBar(true);
        }
        RefreshUI();
    }

    public void OpenCraftingAtStation(CraftingStationType station)
    {
        OpenMenu();
        craftStationFilter = station;
        SwitchToTab(1);
    }

    public void OpenCharacterStatusTab()
    {
        OpenMenu();
        SwitchToTab(2);
    }

    public void OpenCoopTab()
    {
        OpenMenu();
        SwitchToTab(3);
    }

    public void CloseMenu()
    {
        CancelActiveDrag();
        CloseStackActionModal();
        isOpen = false;
        pendingSwapSlot = -1;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        rootPanel.SetActive(false);

        if (cameraTouchZone != null) cameraTouchZone.enabled = true;

        bool touchActive = FragaInputRouter.Instance != null
            ? FragaInputRouter.Instance.IsTouchControlsActive
            : Input.touchCount > 0;

        if (!touchActive && Input.touchCount == 0 && !Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void CloseMenuInstant()
    {
        isOpen = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        if (rootPanel != null) rootPanel.SetActive(false);
    }

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void BuildUIHierarchy()
    {
        Canvas parentCanvas = HUDSurvivalOverlay.GetGameplayCanvas();
        if (parentCanvas == null) return;

        CanvasScaler scaler = parentCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = parentCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 2.5f;

        Transform existingRoot = parentCanvas.transform.Find("SurvivalInventory_Root");
        if (existingRoot != null)
        {
            SafeDestroy(existingRoot.gameObject);
        }

        rootPanel = new GameObject("SurvivalInventory_Root");
        rootPanel.transform.SetParent(parentCanvas.transform, false);
        RectTransform rootRT = rootPanel.AddComponent<RectTransform>();
        rootRect = rootRT;
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image bgDark = rootPanel.AddComponent<Image>();
        bgDark.color = new Color(0.03f, 0.05f, 0.08f, 0.88f);
        bgDark.raycastTarget = true;
        canvasGroup = rootPanel.AddComponent<CanvasGroup>();

        GameObject windowObj = new GameObject("Window_Mat");
        windowObj.transform.SetParent(rootPanel.transform, false);
        RectTransform winRT = windowObj.AddComponent<RectTransform>();
        windowRect = winRT;
        winRT.anchorMin = new Vector2(0.5f, 0.5f);
        winRT.anchorMax = new Vector2(0.5f, 0.5f);
        winRT.pivot = new Vector2(0.5f, 0.5f);
        winRT.sizeDelta = new Vector2(1180f, 772f);

        Image winBg = windowObj.AddComponent<Image>();
        winBg.color = new Color(0.06f, 0.10f, 0.14f, 0.97f);
        winBg.raycastTarget = true;

        Outline winOutline = windowObj.AddComponent<Outline>();
        winOutline.effectColor = new Color(0.28f, 0.78f, 0.90f, 0.55f);
        winOutline.effectDistance = new Vector2(2f, -2f);

        BuildHeader(windowObj.transform);

        inventoryTabObj = CreateTabBodyContainer(windowObj.transform, "Tab_Inventory");
        BuildInventoryTabContent(inventoryTabObj.transform);

        craftingTabObj = CreateTabBodyContainer(windowObj.transform, "Tab_Crafting");
        BuildCraftingTabContent(craftingTabObj.transform);
        craftingTabObj.SetActive(false);

        characterTabObj = CreateTabBodyContainer(windowObj.transform, "Tab_CharacterStatus");
        BuildCharacterStatusTabContent(characterTabObj.transform);
        characterTabObj.SetActive(false);

        coopTabObj = CreateTabBodyContainer(windowObj.transform, "Tab_CoopGroup");
        BuildCoopTabContent(coopTabObj.transform);
        coopTabObj.SetActive(false);

        UpdateTabVisualStates();
        BuildStackActionModal(rootPanel.transform);
        BuildDragGhost(rootPanel.transform);
    }

    private static GameObject CreateTabBodyContainer(Transform parent, string name)
    {
        GameObject tab = new GameObject(name);
        tab.transform.SetParent(parent, false);
        RectTransform rt = tab.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(20f, 18f);
        rt.offsetMax = new Vector2(-20f, -84f);
        return tab;
    }

    private void BuildHeader(Transform parent)
    {
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(parent, false);
        RectTransform hRT = headerObj.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = new Vector2(0f, -12f);
        hRT.sizeDelta = new Vector2(-40f, 58f);

        CreateTabButton(headerObj.transform, "Btn_TabInv", "MOCHILA (0/24)",
            FragaProIconLibrary.ControlIcon.Backpack, new Vector2(4f, 0f), 198f, () => SwitchToTab(0),
            out tabInvText, out tabBgImages[0], out tabOutlines[0]);
        tabLabels[0] = tabInvText;

        CreateTabButton(headerObj.transform, "Btn_TabCraft", "FABRICACAO",
            FragaProIconLibrary.ControlIcon.Workbench, new Vector2(208f, 0f), 170f, () => SwitchToTab(1),
            out tabCraftText, out tabBgImages[1], out tabOutlines[1]);
        tabLabels[1] = tabCraftText;

        CreateTabButton(headerObj.transform, "Btn_TabChar", "PERSONAGEM [K]",
            FragaProIconLibrary.ControlIcon.ShieldBlock, new Vector2(384f, 0f), 182f, () => SwitchToTab(2),
            out tabLabels[2], out tabBgImages[2], out tabOutlines[2]);

        CreateTabButton(headerObj.transform, "Btn_TabCoop", "GRUPO CO-OP",
            FragaProIconLibrary.ControlIcon.MapCompass, new Vector2(572f, 0f), 166f, () => SwitchToTab(3),
            out tabLabels[3], out tabBgImages[3], out tabOutlines[3]);

        CreateTabButton(headerObj.transform, "Btn_OpenBook", "LIVRO [B]",
            FragaProIconLibrary.ControlIcon.BuildHammer, new Vector2(746f, 0f), 132f, () =>
            {
                CloseMenu();
                if (BlueprintPlacementManager.Instance != null)
                    BlueprintPlacementManager.Instance.ToggleBuildMenuOrPlacement();
            }, out _, out _, out _);

        GameObject modeBtnObj = new GameObject("Btn_ControlMode");
        modeBtnObj.transform.SetParent(headerObj.transform, false);
        RectTransform mRT = modeBtnObj.AddComponent<RectTransform>();
        mRT.anchorMin = new Vector2(1f, 0.5f);
        mRT.anchorMax = new Vector2(1f, 0.5f);
        mRT.pivot = new Vector2(1f, 0.5f);
        mRT.anchoredPosition = new Vector2(-62f, 0f);
        mRT.sizeDelta = new Vector2(188f, 44f);

        controlModeBtnBg = modeBtnObj.AddComponent<Image>();
        controlModeBtnBg.sprite = FragaProIconLibrary.GetPillSprite();
        controlModeBtnBg.type = Image.Type.Sliced;
        controlModeBtnBg.color = new Color(0.14f, 0.28f, 0.38f, 0.92f);
        modeBtnObj.AddComponent<Button>().onClick.AddListener(OnToggleControlModeClicked);
        controlModeBtnText = CreateText(modeBtnObj.transform, "MODO: AUTO [F2]", 11, FontStyle.Bold, Color.white);

        GameObject closeBtnObj = new GameObject("Btn_Close");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        RectTransform closeRT = closeBtnObj.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0.5f);
        closeRT.anchorMax = new Vector2(1f, 0.5f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.anchoredPosition = new Vector2(-4f, 0f);
        closeRT.sizeDelta = new Vector2(46f, 46f);

        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        closeImg.type = Image.Type.Sliced;
        closeImg.color = new Color(0.65f, 0.16f, 0.16f, 0.92f);
        closeBtnObj.AddComponent<Button>().onClick.AddListener(CloseMenu);

        GameObject xIco = new GameObject("XIcon");
        xIco.transform.SetParent(closeBtnObj.transform, false);
        xIco.AddComponent<RectTransform>().sizeDelta = new Vector2(24f, 24f);
        Image xImg = xIco.AddComponent<Image>();
        xImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.CancelCross);
        xImg.preserveAspect = true;
        xImg.raycastTarget = false;

        GameObject hDiv = new GameObject("HeaderBottomAccent");
        hDiv.transform.SetParent(headerObj.transform, false);
        RectTransform hdRT = hDiv.AddComponent<RectTransform>();
        hdRT.anchorMin = new Vector2(0f, 0f);
        hdRT.anchorMax = new Vector2(1f, 0f);
        hdRT.pivot = new Vector2(0.5f, 0f);
        hdRT.offsetMin = new Vector2(4f, -6f);
        hdRT.offsetMax = new Vector2(-4f, -4f);
        Image hdImg = hDiv.AddComponent<Image>();
        hdImg.color = new Color(0.30f, 0.78f, 0.94f, 0.38f);
        hdImg.raycastTarget = false;
    }

    private void OnToggleControlModeClicked()
    {
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.ToggleTouchControls();
        }
        RefreshControlModeButton();
    }

    private void RefreshControlModeButton()
    {
        if (controlModeBtnText == null) return;
        bool touchActive = FragaInputRouter.Instance != null
            ? FragaInputRouter.Instance.IsTouchControlsActive
            : (Application.isMobilePlatform || Input.touchCount > 0);
        controlModeBtnText.text = touchActive ? "CONTROLES: TOUCH [F2]" : "CONTROLES: PC [F2]";
        if (controlModeBtnBg != null)
        {
            controlModeBtnBg.color = touchActive
                ? new Color(0.16f, 0.44f, 0.54f, 0.94f)
                : new Color(0.18f, 0.28f, 0.38f, 0.92f);
        }
    }

    private GameObject CreateTabButton(Transform parent, string name, string title, FragaProIconLibrary.ControlIcon icon,
        Vector2 pos, float width, UnityEngine.Events.UnityAction onClick, out Text labelOut, out Image bgOut, out Outline outlOut)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 46f);

        bgOut = btnObj.AddComponent<Image>();
        bgOut.sprite = FragaProIconLibrary.GetPillSprite();
        bgOut.type = Image.Type.Sliced;
        bgOut.color = new Color(0.12f, 0.22f, 0.32f, 0.90f);

        outlOut = btnObj.AddComponent<Outline>();
        outlOut.effectColor = new Color(0.32f, 0.78f, 0.96f, 0.30f);
        outlOut.effectDistance = new Vector2(1.2f, -1.2f);

        btnObj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(btnObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(10f, 0f);
        iRT.sizeDelta = new Vector2(22f, 22f);
        Image ico = icoObj.AddComponent<Image>();
        ico.sprite = FragaProIconLibrary.GetControlIcon(icon);
        ico.preserveAspect = true;
        ico.raycastTarget = false;

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRT = tObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(36f, 0f);
        tRT.offsetMax = new Vector2(-8f, 0f);
        labelOut = tObj.AddComponent<Text>();
        labelOut.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelOut.fontSize = 12;
        labelOut.fontStyle = FontStyle.Bold;
        labelOut.alignment = TextAnchor.MiddleLeft;
        labelOut.color = Color.white;
        labelOut.text = title;
        labelOut.raycastTarget = false;

        return btnObj;
    }

    public void SwitchToTab(int tabIndex)
    {
        activeTabIndex = Mathf.Clamp(tabIndex, 0, 3);
        if (inventoryTabObj != null) inventoryTabObj.SetActive(activeTabIndex == 0);
        if (craftingTabObj != null) craftingTabObj.SetActive(activeTabIndex == 1);
        if (characterTabObj != null) characterTabObj.SetActive(activeTabIndex == 2);
        if (coopTabObj != null) coopTabObj.SetActive(activeTabIndex == 3);

        UpdateTabVisualStates();

        if (activeTabIndex == 1)
        {
            RebuildCraftCards();
            RefreshCraftQueueBar(true);
        }
        RefreshUI();
    }

    private void UpdateTabVisualStates()
    {
        for (int i = 0; i < 4; i++)
        {
            bool active = i == activeTabIndex;
            if (tabLabels[i] != null)
                tabLabels[i].color = active ? Color.white : new Color(0.65f, 0.74f, 0.82f);
            if (tabBgImages[i] != null)
                tabBgImages[i].color = active ? new Color(0.16f, 0.42f, 0.56f, 0.96f) : new Color(0.10f, 0.16f, 0.24f, 0.88f);
            if (tabOutlines[i] != null)
                tabOutlines[i].effectColor = active ? new Color(0.42f, 0.92f, 1.0f, 0.85f) : new Color(0.24f, 0.52f, 0.70f, 0.25f);
        }
    }

    // =================== ABA 1: MOCHILA + PAPERDOLL ===================

    private void BuildInventoryTabContent(Transform parent)
    {
        GameObject gearStrip = new GameObject("PaperdollStrip");
        gearStrip.transform.SetParent(parent, false);
        RectTransform gRT = gearStrip.AddComponent<RectTransform>();
        gRT.anchorMin = new Vector2(0f, 1f);
        gRT.anchorMax = new Vector2(0.65f, 1f);
        gRT.pivot = new Vector2(0f, 1f);
        gRT.anchoredPosition = Vector2.zero;
        gRT.sizeDelta = new Vector2(0f, 64f);

        CreateGearCard(gearStrip.transform, "MAO DIREITA", 0f, () => { if (inventory != null) inventory.EquipItem(-1); },
            out mainHandGearIcon, out mainHandGearText, out mainHandDurFill, out gearCardRects[0], out gearCardBgs[0], out gearCardOutlines[0]);
        CreateGearCard(gearStrip.transform, "MAO ESQUERDA", 242f, () => { if (inventory != null && inventory.equippedOffHandId > 0) inventory.EquipItem(inventory.equippedOffHandId); },
            out offHandGearIcon, out offHandGearText, out offHandDurFill, out gearCardRects[1], out gearCardBgs[1], out gearCardOutlines[1]);
        CreateGearCard(gearStrip.transform, "ARMADURA", 484f, () => { if (inventory != null && inventory.equippedArmorId > 0) inventory.EquipItem(inventory.equippedArmorId); },
            out armorGearIcon, out armorGearText, out armorDurFill, out gearCardRects[2], out gearCardBgs[2], out gearCardOutlines[2]);

        BuildInvCategoryFilters(parent);

        GameObject gridContainer = new GameObject("SlotsGrid");
        gridContainer.transform.SetParent(parent, false);
        RectTransform gridRT = gridContainer.AddComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0f, 0f);
        gridRT.anchorMax = new Vector2(0.65f, 1f);
        gridRT.offsetMin = new Vector2(0f, 114f);
        gridRT.offsetMax = new Vector2(0f, -108f);

        slotViews.Clear();
        int cols = 6;
        float slotSize = 98f;
        float spacing = 10f;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sprite slotSprite = FragaProIconLibrary.GetSlotFrameSprite();

        for (int i = 0; i < 24; i++)
        {
            int r = i / cols;
            int c = i % cols;
            float x = c * (slotSize + spacing);
            float y = -r * (slotSize + spacing);

            GameObject slot = new GameObject($"Slot_{i}");
            slot.transform.SetParent(gridContainer.transform, false);
            RectTransform sRT = slot.AddComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 1f);
            sRT.anchorMax = new Vector2(0f, 1f);
            sRT.pivot = new Vector2(0f, 1f);
            sRT.anchoredPosition = new Vector2(x, y);
            sRT.sizeDelta = new Vector2(slotSize, slotSize);

            Image slotBg = slot.AddComponent<Image>();
            slotBg.sprite = slotSprite;
            slotBg.type = Image.Type.Sliced;
            slotBg.color = new Color(0.11f, 0.16f, 0.22f, 0.90f);

            Outline slotOut = slot.AddComponent<Outline>();
            slotOut.effectColor = new Color(0.26f, 0.62f, 0.80f, 0.30f);

            int visualIdx = i;
            slot.AddComponent<Button>().onClick.AddListener(() => OnSlotClicked(visualIdx));

            InventoryDragRelay slotDrag = slot.AddComponent<InventoryDragRelay>();
            slotDrag.Bind(this, DragSourceKind.BackpackSlot, visualIdx);

            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(slot.transform, false);
            RectTransform icoRT = icoObj.AddComponent<RectTransform>();
            icoRT.anchorMin = new Vector2(0.5f, 0.58f);
            icoRT.anchorMax = new Vector2(0.5f, 0.58f);
            icoRT.sizeDelta = new Vector2(54f, 54f);
            Image icoImg = icoObj.AddComponent<Image>();
            icoImg.preserveAspect = true;
            icoImg.raycastTarget = false;
            icoImg.enabled = false;

            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(slot.transform, false);
            RectTransform nRT = nameObj.AddComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0f, 0.06f);
            nRT.anchorMax = new Vector2(1f, 0.32f);
            nRT.offsetMin = new Vector2(4f, 0f);
            nRT.offsetMax = new Vector2(-4f, 0f);
            Text nameTxt = nameObj.AddComponent<Text>();
            nameTxt.font = font;
            nameTxt.fontSize = 11;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = new Color(0.96f, 0.98f, 1.0f);
            nameTxt.raycastTarget = false;

            GameObject badgeObj = new GameObject("Quantity");
            badgeObj.transform.SetParent(slot.transform, false);
            RectTransform bRT = badgeObj.AddComponent<RectTransform>();
            bRT.anchorMin = new Vector2(1f, 1f);
            bRT.anchorMax = new Vector2(1f, 1f);
            bRT.pivot = new Vector2(1f, 1f);
            bRT.anchoredPosition = new Vector2(-5f, -4f);
            bRT.sizeDelta = new Vector2(44f, 18f);
            Text bTxt = badgeObj.AddComponent<Text>();
            bTxt.font = font;
            bTxt.fontSize = 13;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.alignment = TextAnchor.MiddleRight;
            bTxt.color = new Color(0.38f, 0.96f, 0.98f);
            bTxt.raycastTarget = false;

            GameObject tagObj = new GameObject("Tag");
            tagObj.transform.SetParent(slot.transform, false);
            RectTransform tgRT = tagObj.AddComponent<RectTransform>();
            tgRT.anchorMin = new Vector2(0f, 1f);
            tgRT.anchorMax = new Vector2(0f, 1f);
            tgRT.pivot = new Vector2(0f, 1f);
            tgRT.anchoredPosition = new Vector2(5f, -4f);
            tgRT.sizeDelta = new Vector2(68f, 16f);
            Text tgTxt = tagObj.AddComponent<Text>();
            tgTxt.font = font;
            tgTxt.fontSize = 10;
            tgTxt.fontStyle = FontStyle.Bold;
            tgTxt.color = new Color(1f, 0.84f, 0.30f);
            tgTxt.raycastTarget = false;

            GameObject durBg = new GameObject("DurBG");
            durBg.transform.SetParent(slot.transform, false);
            RectTransform dbRT = durBg.AddComponent<RectTransform>();
            dbRT.anchorMin = new Vector2(0.08f, 0f);
            dbRT.anchorMax = new Vector2(0.92f, 0f);
            dbRT.pivot = new Vector2(0.5f, 0f);
            dbRT.anchoredPosition = new Vector2(0f, 4f);
            dbRT.sizeDelta = new Vector2(0f, 4f);
            durBg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            GameObject durFill = new GameObject("DurFill");
            durFill.transform.SetParent(durBg.transform, false);
            RectTransform dfRT = durFill.AddComponent<RectTransform>();
            dfRT.anchorMin = Vector2.zero;
            dfRT.anchorMax = Vector2.one;
            dfRT.offsetMin = Vector2.zero;
            dfRT.offsetMax = Vector2.zero;
            Image dfImg = durFill.AddComponent<Image>();
            dfImg.sprite = FragaProIconLibrary.GetBarFillSprite();
            dfImg.type = Image.Type.Filled;
            dfImg.fillMethod = Image.FillMethod.Horizontal;
            durBg.SetActive(false);

            slotViews.Add(new SlotViewRefs
            {
                root = slot,
                rect = sRT,
                bg = slotBg,
                outline = slotOut,
                icon = icoImg,
                nameText = nameTxt,
                qtyText = bTxt,
                tagText = tgTxt,
                durBg = durBg,
                durFill = dfImg
            });
        }

        // Barra integrada de Atalhos Rapidos [1..6] + Zona de Descarte (Drag & Drop direto na Mochila)
        BuildInventoryHotbarDock(parent, font, slotSprite);

        // Barra inferior de Protecao / Carga / Temperatura
        GameObject statsBar = new GameObject("GearStatsBar");
        statsBar.transform.SetParent(parent, false);
        RectTransform sbRT = statsBar.AddComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(0f, 0f);
        sbRT.anchorMax = new Vector2(0f, 0f);
        sbRT.pivot = new Vector2(0f, 0f);
        sbRT.anchoredPosition = new Vector2(0f, 2f);
        sbRT.sizeDelta = new Vector2(720f, 36f);

        Image sbImg = statsBar.AddComponent<Image>();
        sbImg.sprite = FragaProIconLibrary.GetPillSprite();
        sbImg.type = Image.Type.Sliced;
        sbImg.color = new Color(0.09f, 0.14f, 0.20f, 0.94f);
        gearStatsSummaryText = CreateText(statsBar.transform, "CARGA: 0.0 kg | DEFESA: 0% | FRIO: 0% | TEMP: 36.6 C", 12, FontStyle.Bold, new Color(0.72f, 0.94f, 1.0f));

        BuildDetailPane(parent);
    }

    private void CreateGearCard(Transform parent, string header, float x, UnityEngine.Events.UnityAction onClick,
        out Image iconOut, out Text textOut, out Image durFillOut,
        out RectTransform cardRectOut, out Image bgOut, out Outline outlineOut)
    {
        GameObject card = new GameObject($"Gear_{header}");
        card.transform.SetParent(parent, false);
        RectTransform rt = card.AddComponent<RectTransform>();
        cardRectOut = rt;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(232f, 58f);

        Image bg = card.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.10f, 0.16f, 0.22f, 0.92f);
        bgOut = bg;
        Outline outl = card.AddComponent<Outline>();
        outl.effectColor = new Color(0.26f, 0.65f, 0.85f, 0.35f);
        outlineOut = outl;
        card.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(card.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(10f, 0f);
        iRT.sizeDelta = new Vector2(36f, 36f);
        iconOut = icoObj.AddComponent<Image>();
        iconOut.preserveAspect = true;
        iconOut.raycastTarget = false;

        GameObject titleObj = new GameObject("Header");
        titleObj.transform.SetParent(card.transform, false);
        RectTransform hRT = titleObj.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 0.58f);
        hRT.anchorMax = new Vector2(1f, 0.95f);
        hRT.offsetMin = new Vector2(54f, 0f);
        hRT.offsetMax = new Vector2(-8f, 0f);
        Text hTxt = titleObj.AddComponent<Text>();
        hTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hTxt.fontSize = 10;
        hTxt.fontStyle = FontStyle.Bold;
        hTxt.color = new Color(0.45f, 0.85f, 0.96f);
        hTxt.text = header;
        hTxt.raycastTarget = false;

        GameObject valObj = new GameObject("Value");
        valObj.transform.SetParent(card.transform, false);
        RectTransform vRT = valObj.AddComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0f, 0.16f);
        vRT.anchorMax = new Vector2(1f, 0.58f);
        vRT.offsetMin = new Vector2(54f, 0f);
        vRT.offsetMax = new Vector2(-8f, 0f);
        textOut = valObj.AddComponent<Text>();
        textOut.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textOut.fontSize = 12;
        textOut.fontStyle = FontStyle.Bold;
        textOut.color = Color.white;
        textOut.raycastTarget = false;

        GameObject durBg = new GameObject("GearDurBG");
        durBg.transform.SetParent(card.transform, false);
        RectTransform dbRT = durBg.AddComponent<RectTransform>();
        dbRT.anchorMin = new Vector2(0f, 0f);
        dbRT.anchorMax = new Vector2(1f, 0f);
        dbRT.pivot = new Vector2(0.5f, 0f);
        dbRT.offsetMin = new Vector2(54f, 4f);
        dbRT.offsetMax = new Vector2(-12f, 8f);
        durBg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        GameObject durFill = new GameObject("Fill");
        durFill.transform.SetParent(durBg.transform, false);
        RectTransform dfRT = durFill.AddComponent<RectTransform>();
        dfRT.anchorMin = Vector2.zero;
        dfRT.anchorMax = Vector2.one;
        dfRT.offsetMin = Vector2.zero;
        dfRT.offsetMax = Vector2.zero;
        durFillOut = durFill.AddComponent<Image>();
        durFillOut.sprite = FragaProIconLibrary.GetBarFillSprite();
        durFillOut.type = Image.Type.Filled;
        durFillOut.fillMethod = Image.FillMethod.Horizontal;
        durFillOut.fillAmount = 0f;
        durFillOut.color = new Color(0.32f, 0.88f, 0.96f);
    }

    private void BuildInvCategoryFilters(Transform parent)
    {
        invFilterPillBgs.Clear();
        invFilterPillKeys.Clear();

        GameObject bar = new GameObject("CategoryFilterBar");
        bar.transform.SetParent(parent, false);
        RectTransform rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0.65f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, -70f);
        rt.sizeDelta = new Vector2(0f, 34f);

        AddFilterPill(bar.transform, "TODOS", null, 0f, 86f);
        AddFilterPill(bar.transform, "ARMAS", ItemCategory.ToolAndWeapon, 92f, 94f);
        AddFilterPill(bar.transform, "CONSUMIVEIS", ItemCategory.Consumable, 192f, 118f);
        AddFilterPill(bar.transform, "RECURSOS", ItemCategory.Resource, 316f, 102f);
        AddFilterPill(bar.transform, "DEFESA/LUZ", ItemCategory.ArmorAndLight, 424f, 114f);

        // Botao Organizar & Empilhar Mochila
        GameObject sortObj = new GameObject("Btn_SortInventory");
        sortObj.transform.SetParent(bar.transform, false);
        RectTransform srt = sortObj.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0.5f);
        srt.anchorMax = new Vector2(0f, 0.5f);
        srt.pivot = new Vector2(0f, 0.5f);
        srt.anchoredPosition = new Vector2(548f, 0f);
        srt.sizeDelta = new Vector2(166f, 30f);

        Image sBg = sortObj.AddComponent<Image>();
        sBg.sprite = FragaProIconLibrary.GetPillSprite();
        sBg.type = Image.Type.Sliced;
        sBg.color = new Color(0.18f, 0.42f, 0.34f, 0.95f);
        sortObj.AddComponent<Button>().onClick.AddListener(OnSortInventoryClicked);
        CreateText(sortObj.transform, "ORGANIZAR MOCHILA", 10, FontStyle.Bold, Color.white);
    }

    private void AddFilterPill(Transform parent, string label, ItemCategory? filterKey, float x, float w)
    {
        GameObject obj = new GameObject($"Filter_{label}");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(w, 30f);

        Image bg = obj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.14f, 0.22f, 0.30f, 0.88f);
        invFilterPillBgs.Add(bg);
        invFilterPillKeys.Add(filterKey);

        obj.AddComponent<Button>().onClick.AddListener(() =>
        {
            invCategoryFilter = filterKey;
            RefreshUI();
        });
        CreateText(obj.transform, label, 10, FontStyle.Bold, Color.white);
    }

    private void OnSortInventoryClicked()
    {
        if (inventory == null) return;
        inventory.SortAndCompact();
        selectedSlotIndex = -1;
        pendingSwapSlot = -1;
        detailPane.SetActive(false);
        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("Mochila organizada e pilhas compactadas!");
        RefreshUI();
    }

    private void BuildDetailPane(Transform parent)
    {
        detailPane = new GameObject("DetailPane");
        detailPane.transform.SetParent(parent, false);
        RectTransform dRT = detailPane.AddComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0.665f, 0f);
        dRT.anchorMax = new Vector2(1f, 1f);
        dRT.offsetMin = Vector2.zero;
        dRT.offsetMax = Vector2.zero;

        Image dImg = detailPane.AddComponent<Image>();
        dImg.color = new Color(0.09f, 0.13f, 0.18f, 0.96f);
        Outline dOut = detailPane.AddComponent<Outline>();
        dOut.effectColor = new Color(0.28f, 0.72f, 0.90f, 0.42f);

        GameObject icoObj = new GameObject("DetailIcon");
        icoObj.transform.SetParent(detailPane.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.5f, 1f);
        iRT.anchorMax = new Vector2(0.5f, 1f);
        iRT.pivot = new Vector2(0.5f, 1f);
        iRT.anchoredPosition = new Vector2(0f, -14f);
        iRT.sizeDelta = new Vector2(72f, 72f);
        detailIconImg = icoObj.AddComponent<Image>();
        detailIconImg.preserveAspect = true;
        detailIconImg.raycastTarget = false;

        detailTitle = CreateCardBoxText(detailPane.transform, "Title", 16f, -90f, 16f, 30f, 18, FontStyle.Bold, new Color(1.0f, 0.90f, 0.45f), TextAnchor.MiddleCenter);
        detailDesc = CreateCardBoxText(detailPane.transform, "Desc", 16f, -124f, 16f, 72f, 12, FontStyle.Normal, new Color(0.88f, 0.93f, 0.98f), TextAnchor.UpperLeft);

        detailDurBg = new GameObject("DetailDurBG");
        detailDurBg.transform.SetParent(detailPane.transform, false);
        RectTransform ddbRT = detailDurBg.AddComponent<RectTransform>();
        ddbRT.anchorMin = new Vector2(0f, 1f);
        ddbRT.anchorMax = new Vector2(1f, 1f);
        ddbRT.pivot = new Vector2(0.5f, 1f);
        ddbRT.offsetMin = new Vector2(16f, -206f);
        ddbRT.offsetMax = new Vector2(-16f, -198f);
        detailDurBg.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.85f);

        GameObject durF = new GameObject("Fill");
        durF.transform.SetParent(detailDurBg.transform, false);
        RectTransform ddfRT = durF.AddComponent<RectTransform>();
        ddfRT.anchorMin = Vector2.zero;
        ddfRT.anchorMax = Vector2.one;
        ddfRT.offsetMin = Vector2.zero;
        ddfRT.offsetMax = Vector2.zero;
        detailDurFill = durF.AddComponent<Image>();
        detailDurFill.sprite = FragaProIconLibrary.GetBarFillSprite();
        detailDurFill.type = Image.Type.Filled;
        detailDurFill.fillMethod = Image.FillMethod.Horizontal;
        detailDurBg.SetActive(false);

        detailStats = CreateCardBoxText(detailPane.transform, "Stats", 16f, -212f, 16f, 118f, 12, FontStyle.Bold, new Color(0.42f, 0.95f, 0.65f), TextAnchor.UpperLeft);

        detailUseButton = CreateActionBtn(detailPane.transform, "Btn_Use", "USAR / EQUIPAR", new Vector2(0f, 206f), new Vector2(310f, 40f), new Color(0.18f, 0.62f, 0.42f), OnUseSelectedItem, out detailUseBtnText);
        detailRepairButton = CreateActionBtn(detailPane.transform, "Btn_Repair", "CONSERTAR", new Vector2(0f, 160f), new Vector2(310f, 36f), new Color(0.72f, 0.44f, 0.14f), OnRepairSelected, out detailRepairText);
        detailHotbarButton = CreateActionBtn(detailPane.transform, "Btn_Hotbar", "ATRIBUIR ATALHO (1)", new Vector2(0f, 116f), new Vector2(310f, 36f), new Color(0.18f, 0.42f, 0.64f), OnAssignHotbarSelected, out detailHotbarText);
        detailSplitButton = CreateActionBtn(detailPane.transform, "Btn_Split", "DIVIDIR PILHA", new Vector2(-79f, 70f), new Vector2(150f, 36f), new Color(0.24f, 0.30f, 0.38f), OnSplitSelected, out _);
        detailSwapButton = CreateActionBtn(detailPane.transform, "Btn_Swap", "MOVER SLOT", new Vector2(79f, 70f), new Vector2(150f, 36f), new Color(0.24f, 0.34f, 0.44f), OnBeginSwapSelected, out detailSwapText);
        detailDropButton = CreateActionBtn(detailPane.transform, "Btn_Drop", "SOLTAR (1)", new Vector2(-79f, 16f), new Vector2(150f, 38f), new Color(0.55f, 0.20f, 0.20f), OnDropSelected, out detailDropText);
        detailShareButton = CreateActionBtn(detailPane.transform, "Btn_ShareCoop", "DAR CO-OP", new Vector2(79f, 16f), new Vector2(150f, 38f), new Color(0.22f, 0.46f, 0.56f), OnShareSelectedCoop, out detailShareText);

        detailPane.SetActive(false);
    }

    private void BuildInventoryHotbarDock(Transform parent, Font font, Sprite slotSprite)
    {
        GameObject dockObj = new GameObject("HotbarDockBar");
        dockObj.transform.SetParent(parent, false);
        RectTransform dockRT = dockObj.AddComponent<RectTransform>();
        dockRT.anchorMin = new Vector2(0f, 0f);
        dockRT.anchorMax = new Vector2(0f, 0f);
        dockRT.pivot = new Vector2(0f, 0f);
        dockRT.anchoredPosition = new Vector2(0f, 40f);
        dockRT.sizeDelta = new Vector2(720f, 66f);

        float slotSize = 60f;
        float gap = 7f;
        for (int h = 0; h < 6; h++)
        {
            int hotbarIdx = h;
            float x = h * (slotSize + gap);

            GameObject hObj = new GameObject($"HotbarDockSlot_{h + 1}");
            hObj.transform.SetParent(dockObj.transform, false);
            RectTransform hRT = hObj.AddComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0f, 0.5f);
            hRT.anchorMax = new Vector2(0f, 0.5f);
            hRT.pivot = new Vector2(0f, 0.5f);
            hRT.anchoredPosition = new Vector2(x, 0f);
            hRT.sizeDelta = new Vector2(slotSize, slotSize);

            Image bg = hObj.AddComponent<Image>();
            bg.sprite = slotSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.08f, 0.13f, 0.19f, 0.94f);

            Outline outl = hObj.AddComponent<Outline>();
            outl.effectColor = new Color(0.28f, 0.65f, 0.85f, 0.40f);
            outl.effectDistance = new Vector2(1.2f, -1.2f);

            hObj.AddComponent<Button>().onClick.AddListener(() => OnHotbarDockClicked(hotbarIdx));
            InventoryDragRelay relay = hObj.AddComponent<InventoryDragRelay>();
            relay.Bind(this, DragSourceKind.HotbarDockSlot, hotbarIdx);

            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(hObj.transform, false);
            RectTransform iRT = icoObj.AddComponent<RectTransform>();
            iRT.sizeDelta = new Vector2(36f, 36f);
            Image ico = icoObj.AddComponent<Image>();
            ico.preserveAspect = true;
            ico.raycastTarget = false;
            ico.enabled = false;

            GameObject kObj = new GameObject("Key");
            kObj.transform.SetParent(hObj.transform, false);
            RectTransform kRT = kObj.AddComponent<RectTransform>();
            kRT.anchorMin = new Vector2(0f, 1f);
            kRT.anchorMax = new Vector2(0f, 1f);
            kRT.pivot = new Vector2(0f, 1f);
            kRT.anchoredPosition = new Vector2(4f, -2f);
            kRT.sizeDelta = new Vector2(18f, 15f);
            Text kTxt = kObj.AddComponent<Text>();
            kTxt.font = font;
            kTxt.fontSize = 10;
            kTxt.fontStyle = FontStyle.Bold;
            kTxt.color = new Color(0.62f, 0.88f, 1.0f);
            kTxt.text = (h + 1).ToString();
            kTxt.raycastTarget = false;

            GameObject qObj = new GameObject("Qty");
            qObj.transform.SetParent(hObj.transform, false);
            RectTransform qRT = qObj.AddComponent<RectTransform>();
            qRT.anchorMin = new Vector2(1f, 0f);
            qRT.anchorMax = new Vector2(1f, 0f);
            qRT.pivot = new Vector2(1f, 0f);
            qRT.anchoredPosition = new Vector2(-4f, 4f);
            qRT.sizeDelta = new Vector2(36f, 16f);
            Text qTxt = qObj.AddComponent<Text>();
            qTxt.font = font;
            qTxt.fontSize = 11;
            qTxt.fontStyle = FontStyle.Bold;
            qTxt.alignment = TextAnchor.LowerRight;
            qTxt.color = new Color(0.45f, 0.96f, 0.98f);
            qTxt.raycastTarget = false;

            GameObject durBg = new GameObject("DurBG");
            durBg.transform.SetParent(hObj.transform, false);
            RectTransform dbRT = durBg.AddComponent<RectTransform>();
            dbRT.anchorMin = new Vector2(0.1f, 0f);
            dbRT.anchorMax = new Vector2(0.9f, 0f);
            dbRT.pivot = new Vector2(0.5f, 0f);
            dbRT.anchoredPosition = new Vector2(0f, 3f);
            dbRT.sizeDelta = new Vector2(0f, 3f);
            durBg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            GameObject durFill = new GameObject("Fill");
            durFill.transform.SetParent(durBg.transform, false);
            RectTransform dfRT = durFill.AddComponent<RectTransform>();
            dfRT.anchorMin = Vector2.zero;
            dfRT.anchorMax = Vector2.one;
            dfRT.offsetMin = Vector2.zero;
            dfRT.offsetMax = Vector2.zero;
            Image dfImg = durFill.AddComponent<Image>();
            dfImg.sprite = FragaProIconLibrary.GetBarFillSprite();
            dfImg.type = Image.Type.Filled;
            dfImg.fillMethod = Image.FillMethod.Horizontal;
            durBg.SetActive(false);

            hotbarDockViews[h] = new HotbarDockRefs
            {
                root = hObj,
                rect = hRT,
                bg = bg,
                outline = outl,
                icon = ico,
                keyText = kTxt,
                qtyText = qTxt,
                durBg = durBg,
                durFill = dfImg
            };
        }

        // Zona de Descarte Rapido ao lado da Hotbar Dock
        float discardX = 6 * (slotSize + gap) + 8f;
        GameObject dropObj = new GameObject("DiscardDropZone");
        dropObj.transform.SetParent(dockObj.transform, false);
        discardDropRect = dropObj.AddComponent<RectTransform>();
        discardDropRect.anchorMin = new Vector2(0f, 0.5f);
        discardDropRect.anchorMax = new Vector2(0f, 0.5f);
        discardDropRect.pivot = new Vector2(0f, 0.5f);
        discardDropRect.anchoredPosition = new Vector2(discardX, 0f);
        discardDropRect.sizeDelta = new Vector2(720f - discardX, 60f);

        discardDropBg = dropObj.AddComponent<Image>();
        discardDropBg.sprite = FragaProIconLibrary.GetPillSprite();
        discardDropBg.type = Image.Type.Sliced;
        discardDropBg.color = new Color(0.22f, 0.10f, 0.12f, 0.90f);
        discardDropOutline = dropObj.AddComponent<Outline>();
        discardDropOutline.effectColor = new Color(0.92f, 0.34f, 0.30f, 0.45f);
        discardDropOutline.effectDistance = new Vector2(1.2f, -1.2f);
        dropObj.AddComponent<Button>().onClick.AddListener(OnDropSelected);

        GameObject dIco = new GameObject("Icon");
        dIco.transform.SetParent(dropObj.transform, false);
        RectTransform diRT = dIco.AddComponent<RectTransform>();
        diRT.anchorMin = new Vector2(0f, 0.5f);
        diRT.anchorMax = new Vector2(0f, 0.5f);
        diRT.pivot = new Vector2(0f, 0.5f);
        diRT.anchoredPosition = new Vector2(12f, 0f);
        diRT.sizeDelta = new Vector2(26f, 26f);
        Image diImg = dIco.AddComponent<Image>();
        diImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.CancelCross);
        diImg.color = new Color(1f, 0.52f, 0.48f);
        diImg.preserveAspect = true;
        diImg.raycastTarget = false;

        discardDropLabel = CreateCardBoxText(dropObj.transform, "Label", 44f, -6f, 8f, 48f, 11, FontStyle.Bold,
            new Color(1.0f, 0.80f, 0.78f), TextAnchor.MiddleLeft);
        discardDropLabel.text = "SOLTAR NO CHAO\nArraste o item ou use [Del]";
    }

    private void BuildDragGhost(Transform parent)
    {
        dragGhostObj = new GameObject("InventoryDragGhost");
        dragGhostObj.transform.SetParent(parent, false);
        dragGhostRect = dragGhostObj.AddComponent<RectTransform>();
        dragGhostRect.sizeDelta = new Vector2(82f, 82f);

        Image bg = dragGhostObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.14f, 0.38f, 0.52f, 0.90f);
        bg.raycastTarget = false;

        Outline outl = dragGhostObj.AddComponent<Outline>();
        outl.effectColor = new Color(0.45f, 0.94f, 1.0f, 0.95f);
        outl.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(dragGhostObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.sizeDelta = new Vector2(54f, 54f);
        dragGhostIcon = icoObj.AddComponent<Image>();
        dragGhostIcon.preserveAspect = true;
        dragGhostIcon.raycastTarget = false;

        dragGhostQty = CreateCardBoxText(dragGhostObj.transform, "Qty", 6f, -4f, 6f, 20f, 13, FontStyle.Bold,
            new Color(0.42f, 0.98f, 1.0f), TextAnchor.MiddleRight);
        dragGhostObj.SetActive(false);
    }

    private void BuildStackActionModal(Transform parent)
    {
        stackModalRoot = new GameObject("StackActionModal");
        stackModalRoot.transform.SetParent(parent, false);
        RectTransform bgRT = stackModalRoot.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        Image dim = stackModalRoot.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.04f, 0.06f, 0.72f);
        dim.raycastTarget = true;

        GameObject card = new GameObject("ModalCard");
        card.transform.SetParent(stackModalRoot.transform, false);
        RectTransform cRT = card.AddComponent<RectTransform>();
        cRT.sizeDelta = new Vector2(468f, 326f);
        Image cBg = card.AddComponent<Image>();
        cBg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        cBg.type = Image.Type.Sliced;
        cBg.color = new Color(0.08f, 0.13f, 0.19f, 0.98f);
        Outline cOut = card.AddComponent<Outline>();
        cOut.effectColor = new Color(0.36f, 0.86f, 0.98f, 0.80f);
        cOut.effectDistance = new Vector2(2f, -2f);

        stackModalHeaderText = CreateCardBoxText(card.transform, "Header", 20f, -14f, 20f, 26f, 15, FontStyle.Bold,
            new Color(0.45f, 0.92f, 1.0f), TextAnchor.MiddleCenter);

        GameObject icoObj = new GameObject("ItemIcon");
        icoObj.transform.SetParent(card.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 1f);
        iRT.anchorMax = new Vector2(0f, 1f);
        iRT.pivot = new Vector2(0f, 1f);
        iRT.anchoredPosition = new Vector2(22f, -52f);
        iRT.sizeDelta = new Vector2(54f, 54f);
        stackModalIcon = icoObj.AddComponent<Image>();
        stackModalIcon.preserveAspect = true;
        stackModalIcon.raycastTarget = false;

        stackModalItemText = CreateCardBoxText(card.transform, "ItemLabel", 88f, -52f, 20f, 28f, 15, FontStyle.Bold,
            Color.white, TextAnchor.MiddleLeft);
        stackModalAmountText = CreateCardBoxText(card.transform, "AmountLabel", 88f, -82f, 20f, 26f, 14, FontStyle.Bold,
            new Color(1.0f, 0.88f, 0.42f), TextAnchor.MiddleLeft);

        // Controle [-] Slider [+]
        CreateActionBtn(card.transform, "Btn_Minus", "-", new Vector2(-176f, 158f), new Vector2(52f, 40f),
            new Color(0.18f, 0.28f, 0.38f), () => AdjustStackModalAmount(-1), out _);

        GameObject sliderObj = new GameObject("AmountSlider");
        sliderObj.transform.SetParent(card.transform, false);
        RectTransform sRT = sliderObj.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0.5f, 0f);
        sRT.anchorMax = new Vector2(0.5f, 0f);
        sRT.pivot = new Vector2(0.5f, 0f);
        sRT.anchoredPosition = new Vector2(0f, 166f);
        sRT.sizeDelta = new Vector2(280f, 24f);

        GameObject sBg = new GameObject("Background");
        sBg.transform.SetParent(sliderObj.transform, false);
        RectTransform sBgRT = sBg.AddComponent<RectTransform>();
        sBgRT.anchorMin = new Vector2(0f, 0.30f);
        sBgRT.anchorMax = new Vector2(1f, 0.70f);
        sBgRT.offsetMin = Vector2.zero;
        sBgRT.offsetMax = Vector2.zero;
        sBg.AddComponent<Image>().color = new Color(0.03f, 0.06f, 0.10f, 0.95f);

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.30f);
        faRT.anchorMax = new Vector2(1f, 0.70f);
        faRT.offsetMin = Vector2.zero;
        faRT.offsetMax = Vector2.zero;
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fRT = fillObj.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero;
        fRT.offsetMax = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.30f, 0.85f, 0.96f, 0.95f);

        GameObject handleArea = new GameObject("HandleSlideArea");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform haRT = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(8f, 0f);
        haRT.offsetMax = new Vector2(-8f, 0f);
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleArea.transform, false);
        RectTransform hRT = handleObj.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(22f, 26f);
        Image hImg = handleObj.AddComponent<Image>();
        hImg.sprite = FragaProIconLibrary.GetCircleBackground();
        hImg.color = Color.white;

        stackModalSlider = sliderObj.AddComponent<Slider>();
        stackModalSlider.fillRect = fRT;
        stackModalSlider.handleRect = hRT;
        stackModalSlider.wholeNumbers = true;
        stackModalSlider.minValue = 1;
        stackModalSlider.maxValue = 1;
        stackModalSlider.onValueChanged.AddListener(v =>
        {
            if (stackModalSuppressSlider) return;
            SetStackModalAmount(Mathf.RoundToInt(v));
        });

        CreateActionBtn(card.transform, "Btn_Plus", "+", new Vector2(176f, 158f), new Vector2(52f, 40f),
            new Color(0.18f, 0.28f, 0.38f), () => AdjustStackModalAmount(1), out _);

        // Presets rapidos: 1 | METADE | TUDO
        CreateActionBtn(card.transform, "Btn_Preset1", "1 UNIDADE", new Vector2(-138f, 102f), new Vector2(126f, 38f),
            new Color(0.16f, 0.24f, 0.34f), () => SetStackModalAmount(1), out _);
        CreateActionBtn(card.transform, "Btn_PresetHalf", "METADE", new Vector2(0f, 102f), new Vector2(126f, 38f),
            new Color(0.18f, 0.34f, 0.44f), SetStackModalHalf, out _);
        CreateActionBtn(card.transform, "Btn_PresetAll", "MAXIMO", new Vector2(138f, 102f), new Vector2(126f, 38f),
            new Color(0.24f, 0.34f, 0.44f), SetStackModalMax, out _);

        // Botoes Finais: DIVIDIR PILHA | DESCARTAR | CANCELAR
        stackModalBtnSplit = CreateActionBtn(card.transform, "Btn_ConfirmSplit", "DIVIDIR", new Vector2(-140f, 24f), new Vector2(134f, 46f),
            new Color(0.18f, 0.56f, 0.44f), () => ConfirmStackModalAction(stackModalSelectedAmount, false), out stackModalSplitText);
        stackModalBtnDrop = CreateActionBtn(card.transform, "Btn_ConfirmDrop", "DESCARTAR", new Vector2(0f, 24f), new Vector2(134f, 46f),
            new Color(0.66f, 0.22f, 0.22f), () => ConfirmStackModalAction(stackModalSelectedAmount, true), out stackModalDropText);
        CreateActionBtn(card.transform, "Btn_ModalCancel", "CANCELAR", new Vector2(140f, 24f), new Vector2(122f, 46f),
            new Color(0.22f, 0.26f, 0.32f), CloseStackActionModal, out _);

        stackModalRoot.SetActive(false);
    }

    private Text CreateCardBoxText(Transform parent, string name, float leftMargin, float topOffset, float rightMargin, float height, int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(leftMargin, topOffset - height);
        rt.offsetMax = new Vector2(-rightMargin, topOffset);

        Text t = obj.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.raycastTarget = false;
        Shadow sh = obj.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    private Button CreateActionBtn(Transform parent, string name, string label, Vector2 bottomPos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick, out Text textOut)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = bottomPos;
        rt.sizeDelta = size;

        Image img = obj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetPillSprite();
        img.type = Image.Type.Sliced;
        img.color = bgColor;
        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        textOut = CreateText(obj.transform, label, 11, FontStyle.Bold, Color.white);
        return btn;
    }

    private void OnSlotClicked(int visualIndex)
    {
        if (Time.frameCount == lastDragEndFrame || dragActive) return;
        if (visualIndex < 0 || visualIndex >= displayedSlotIndices.Count) return;
        int realIdx = displayedSlotIndices[visualIndex];
        if (realIdx < 0) return;

        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
            inventory != null && !inventory.slots[realIdx].IsEmpty() && inventory.slots[realIdx].quantity >= 2)
        {
            if (inventory.SplitSlot(realIdx))
            {
                SelectSlot(realIdx);
                RefreshUI();
            }
            return;
        }

        if (pendingSwapSlot >= 0)
        {
            if (inventory != null && pendingSwapSlot != realIdx)
            {
                inventory.SwapSlots(pendingSwapSlot, realIdx);
            }
            pendingSwapSlot = -1;
            SelectSlot(realIdx);
            RefreshUI();
            return;
        }

        bool isDoubleTap = realIdx == lastClickedSlotIndex && (Time.unscaledTime - lastSlotClickTime) <= 0.36f;
        lastClickedSlotIndex = realIdx;
        lastSlotClickTime = Time.unscaledTime;

        SelectSlot(realIdx);
        if (isDoubleTap && inventory != null && !inventory.slots[realIdx].IsEmpty())
        {
            OnUseSelectedItem();
        }
    }

    private void SelectSlot(int index)
    {
        if (inventory == null || index < 0 || index >= inventory.slots.Length) return;
        selectedSlotIndex = index;

        InventorySlot slot = inventory.slots[index];
        if (slot.IsEmpty())
        {
            detailPane.SetActive(false);
            return;
        }

        ItemData data = ItemDatabase.GetItem(slot.itemId);
        if (data == null) return;

        detailPane.SetActive(true);
        detailIconImg.sprite = FragaProIconLibrary.GetItemIcon(data.id);
        detailTitle.text = $"{data.itemName.ToUpper()} (x{slot.quantity})";
        detailDesc.text = data.description;

        if (detailDurBg != null && detailDurFill != null)
        {
            if (data.maxDurability > 0f && slot.durability >= 0f)
            {
                float frac = Mathf.Clamp01(slot.durability / data.maxDurability);
                detailDurBg.SetActive(true);
                detailDurFill.fillAmount = frac;
                detailDurFill.color = data.isPerishable
                    ? Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.35f, 0.92f, 0.45f), frac)
                    : (frac > 0.2f ? new Color(0.28f, 0.82f, 0.98f) : new Color(0.95f, 0.35f, 0.25f));
            }
            else
            {
                detailDurBg.SetActive(false);
            }
        }

        string statsStr = "";
        if (data.equipSlot != EquipSlotType.None)
        {
            statsStr += $"Dano: {data.attackDamage:F0}  Alcance: {data.attackRange:F1}m\n";
            if (data.harvestTreeMultiplier > 1.05f || data.harvestRockMultiplier > 1.05f)
            {
                if (data.harvestTreeMultiplier > 1.05f) statsStr += $"Corte x{data.harvestTreeMultiplier:F1}  ";
                if (data.harvestRockMultiplier > 1.05f) statsStr += $"Mineracao x{data.harvestRockMultiplier:F1}";
                statsStr += "\n";
            }
        }
        if (data.armorDefense > 0f)
            statsStr += $"Armadura: +{data.armorDefense * 100f:F0}%  ";
        if (data.coldInsulation > 0f)
            statsStr += $"Isolamento: +{data.coldInsulation * 100f:F0}%\n";

        if (data.hungerRestore > 0f) statsStr += $"+{data.hungerRestore:F0} Fome  ";
        if (data.thirstRestore > 0f) statsStr += $"+{data.thirstRestore:F0} Sede  ";
        if (data.healthRestore > 0f) statsStr += $"+{data.healthRestore:F0} Vida  ";
        if (data.staminaRestore > 0f) statsStr += $"+{data.staminaRestore:F0} Folego  ";
        if (data.sanityRestore != 0f) statsStr += $"{(data.sanityRestore > 0 ? "+" : "")}{data.sanityRestore:F0} Sanidade  ";
        if (data.warmthBonus > 0f) statsStr += $"+{data.warmthBonus:F1}C Calor";

        if (!data.isPerishable && data.maxDurability > 0f && slot.durability >= 0f)
        {
            string durLabel = data.equipSlot != EquipSlotType.None ? "Durabilidade" : "Goles / Cargas";
            statsStr += $"\n{durLabel}: {Mathf.CeilToInt(slot.durability)}/{Mathf.RoundToInt(data.maxDurability)}";
            if (data.equipSlot != EquipSlotType.None && slot.durability <= 0f)
                statsStr += " <color=#FF6655>(QUEBRADA)</color>";
        }
        if (data.poisonDuration < 0f) statsStr += "\nCura Envenenamento";
        else if (data.poisonDuration > 0f) statsStr += "\nRisco de Envenenamento!";
        if (data.curesBleeding) statsStr += "\nEstanca Sangramento";
        if (data.curesFracture) statsStr += "\nCura Fratura";
        detailStats.text = statsStr.Trim();

        bool canUse = data.type == ItemType.Consumable || data.type == ItemType.Deployable || data.type == ItemType.Tool || data.equipSlot != EquipSlotType.None;
        detailUseButton.interactable = canUse;
        if (data.equipSlot != EquipSlotType.None)
        {
            bool eq = inventory.equippedMainHandId == data.id || inventory.equippedOffHandId == data.id || inventory.equippedArmorId == data.id;
            if (!data.isPerishable && data.maxDurability > 0f && slot.durability <= 0f)
            {
                detailUseButton.interactable = false;
                detailUseBtnText.text = "QUEBRADA (CONSERTAR)";
            }
            else detailUseBtnText.text = eq ? "EQUIPADO" : "EQUIPAR";
        }
        else if (data.id == 210)
        {
            detailUseBtnText.text = slot.durability > 0f ? "BEBER GOLE" : "CANTIL VAZIO";
        }
        else
        {
            detailUseBtnText.text = data.type == ItemType.Consumable ? "CONSUMIR" : (data.type == ItemType.Deployable ? "POSICIONAR" : "PASSIVO");
        }

        if (detailRepairButton != null)
        {
            bool isRepairable = data.equipSlot != EquipSlotType.None && !data.isPerishable && data.maxDurability > 0f;
            detailRepairButton.gameObject.SetActive(isRepairable);
            if (isRepairable)
            {
                bool needsRepair = slot.durability >= 0f && slot.durability < data.maxDurability - 0.1f;
                inventory.GetRepairIngredientId(data.id, out string matName);
                detailRepairButton.interactable = needsRepair;
                detailRepairText.text = needsRepair ? $"CONSERTAR (1x {matName.ToUpper()})" : "INTEIRA (100%)";
            }
        }

        detailHotbarText.text = $"ATRIBUIR ATALHO ({nextHotbarAssignIdx + 1})";
        detailSplitButton.interactable = slot.quantity >= 2;
        detailSwapText.text = pendingSwapSlot == index ? "CLIQUE DESTINO..." : "MOVER SLOT";
        if (detailDropText != null)
        {
            detailDropText.text = slot.quantity > 1 ? $"SOLTAR (1/{slot.quantity})" : "SOLTAR (1)";
        }
        if (detailShareText != null)
        {
            int shareAmt = slot.quantity > 1 ? Mathf.Max(1, slot.quantity / 2) : 1;
            detailShareText.text = slot.quantity > 1 ? $"COMPARTILHAR ({shareAmt})" : "COMPARTILHAR CO-OP";
        }
    }

    private void OnRepairSelected()
    {
        if (inventory == null || selectedSlotIndex < 0) return;
        bool ok = inventory.RepairItemSlot(selectedSlotIndex, out string msg);
        if (HUDSurvivalOverlay.Instance != null && !string.IsNullOrEmpty(msg))
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(ok ? $"<color=#66FFAA>{msg}</color>" : $"<color=#FFBB55>{msg}</color>");
        }
        RefreshUI();
        SelectSlot(selectedSlotIndex);
    }

    private void OnUseSelectedItem()
    {
        if (inventory == null || selectedSlotIndex < 0) return;
        inventory.UseItem(selectedSlotIndex);
        RefreshUI();
        SelectSlot(selectedSlotIndex);
    }

    private void OnAssignHotbarSelected()
    {
        if (inventory == null || selectedSlotIndex < 0) return;
        InventorySlot slot = inventory.slots[selectedSlotIndex];
        if (slot.IsEmpty()) return;

        inventory.AssignHotbarSlot(nextHotbarAssignIdx, slot.itemId);
        if (HUDSurvivalOverlay.Instance != null)
        {
            ItemData d = ItemDatabase.GetItem(slot.itemId);
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"{(d != null ? d.itemName : "Item")} -> Atalho [{nextHotbarAssignIdx + 1}]");
        }
        nextHotbarAssignIdx = (nextHotbarAssignIdx + 1) % 6;
        SelectSlot(selectedSlotIndex);
        RefreshUI();
    }

    private void OnSplitSelected()
    {
        if (inventory == null || selectedSlotIndex < 0) return;
        InventorySlot slot = inventory.slots[selectedSlotIndex];
        if (slot.IsEmpty() || slot.quantity < 2) return;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (inventory.SplitSlot(selectedSlotIndex))
            {
                RefreshUI();
                SelectSlot(selectedSlotIndex);
            }
            return;
        }
        OpenStackActionModal(selectedSlotIndex, defaultDiscardMode: false);
    }

    private void OnBeginSwapSelected()
    {
        if (selectedSlotIndex < 0) return;
        pendingSwapSlot = (pendingSwapSlot == selectedSlotIndex) ? -1 : selectedSlotIndex;
        SelectSlot(selectedSlotIndex);
    }

    private void OnDropSelected()
    {
        if (inventory == null || selectedSlotIndex < 0) return;
        InventorySlot slot = inventory.slots[selectedSlotIndex];
        if (slot.IsEmpty()) return;

        if (slot.quantity > 1)
        {
            OpenStackActionModal(selectedSlotIndex, defaultDiscardMode: true);
            return;
        }

        inventory.DropItemToWorld(selectedSlotIndex, 1);
        RefreshUI();
        SelectSlot(selectedSlotIndex);
    }

    private void OnHotbarDockClicked(int hotbarIndex)
    {
        if (Time.frameCount == lastDragEndFrame || dragActive) return;
        if (inventory == null || hotbarIndex < 0 || hotbarIndex >= 6) return;

        if (selectedSlotIndex >= 0 && selectedSlotIndex < inventory.slots.Length && !inventory.slots[selectedSlotIndex].IsEmpty())
        {
            TryDropSlotToHotbar(selectedSlotIndex, hotbarIndex);
            return;
        }

        inventory.UseHotbarSlot(hotbarIndex);
        RefreshUI();
    }

    // =================== API DE DRAG & DROP + MODAL DE PILHAS ===================

    public bool TryExecuteSlotDrop(int fromRealSlot, int toRealSlot, int moveAmount = -1)
    {
        if (inventory == null || fromRealSlot < 0 || toRealSlot < 0 || fromRealSlot == toRealSlot) return false;
        if (fromRealSlot >= inventory.slots.Length || toRealSlot >= inventory.slots.Length) return false;
        if (inventory.slots[fromRealSlot].IsEmpty()) return false;

        bool ok = inventory.MoveOrMergeSlot(fromRealSlot, toRealSlot, moveAmount);
        if (ok)
        {
            pendingSwapSlot = -1;
            SelectSlot(toRealSlot);
            RefreshUI();
        }
        return ok;
    }

    public bool TryDropSlotToHotbar(int fromRealSlot, int hotbarIndex)
    {
        if (inventory == null || fromRealSlot < 0 || fromRealSlot >= inventory.slots.Length) return false;
        if (hotbarIndex < 0 || hotbarIndex >= 6 || inventory.slots[fromRealSlot].IsEmpty()) return false;

        int itemId = inventory.slots[fromRealSlot].itemId;
        if (!inventory.AssignSlotToHotbar(fromRealSlot, hotbarIndex)) return false;

        if (HUDSurvivalOverlay.Instance != null)
        {
            ItemData d = ItemDatabase.GetItem(itemId);
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"{(d != null ? d.itemName : "Item")} -> Atalho [{hotbarIndex + 1}]");
        }
        SelectSlot(fromRealSlot);
        RefreshUI();
        return true;
    }

    public bool TryDropHotbarToHotbar(int fromHotbar, int toHotbar)
    {
        if (inventory == null) return false;
        bool ok = inventory.SwapHotbarSlots(fromHotbar, toHotbar);
        if (ok) RefreshUI();
        return ok;
    }

    public bool TryDropSlotToGear(int fromRealSlot, EquipSlotType targetGearSlot)
    {
        if (inventory == null || fromRealSlot < 0 || fromRealSlot >= inventory.slots.Length) return false;
        InventorySlot slot = inventory.slots[fromRealSlot];
        if (slot.IsEmpty()) return false;

        ItemData d = ItemDatabase.GetItem(slot.itemId);
        if (d == null || d.equipSlot != targetGearSlot)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FFAA55>Este item nao pertence a esse slot de equipamento.</color>");
            return false;
        }

        bool ok = inventory.EquipFromSlot(fromRealSlot, targetGearSlot);
        if (ok && HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Equipado: {d.itemName}");
        }
        RefreshUI();
        SelectSlot(fromRealSlot);
        return ok;
    }

    public bool TryDropSlotToDiscard(int fromRealSlot, int dropAmount = -1)
    {
        if (inventory == null || fromRealSlot < 0 || fromRealSlot >= inventory.slots.Length) return false;
        InventorySlot slot = inventory.slots[fromRealSlot];
        if (slot.IsEmpty()) return false;

        if (dropAmount <= 0 && slot.quantity > 1)
        {
            OpenStackActionModal(fromRealSlot, defaultDiscardMode: true);
            return true;
        }

        int toDrop = dropAmount <= 0 ? 1 : Mathf.Clamp(dropAmount, 1, slot.quantity);
        ItemData d = ItemDatabase.GetItem(slot.itemId);
        inventory.DropItemToWorld(fromRealSlot, toDrop);
        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Descartado: {toDrop}x {(d != null ? d.itemName : "Item")}");
        RefreshUI();
        SelectSlot(fromRealSlot);
        return true;
    }

    public void OpenStackActionModal(int slotIndex, bool defaultDiscardMode = false)
    {
        if (inventory == null || stackModalRoot == null || slotIndex < 0 || slotIndex >= inventory.slots.Length) return;
        InventorySlot slot = inventory.slots[slotIndex];
        if (slot.IsEmpty()) return;

        stackModalSlotIndex = slotIndex;
        stackModalDefaultDiscard = defaultDiscardMode;
        ItemData d = ItemDatabase.GetItem(slot.itemId);
        if (stackModalIcon != null) stackModalIcon.sprite = FragaProIconLibrary.GetItemIcon(slot.itemId);
        if (stackModalHeaderText != null)
            stackModalHeaderText.text = defaultDiscardMode ? "DESCARTAR PILHA NO CHAO" : "DIVIDIR OU DESCARTAR PILHA";
        if (stackModalItemText != null)
            stackModalItemText.text = $"{(d != null ? d.itemName.ToUpper() : "ITEM")} (Total: x{slot.quantity})";

        int initialAmt = slot.quantity > 1 ? Mathf.Max(1, slot.quantity / 2) : 1;
        if (stackModalSlider != null)
        {
            stackModalSuppressSlider = true;
            stackModalSlider.minValue = 1;
            stackModalSlider.maxValue = Mathf.Max(1, slot.quantity);
            stackModalSlider.value = initialAmt;
            stackModalSuppressSlider = false;
        }

        stackModalRoot.SetActive(true);
        stackModalRoot.transform.SetAsLastSibling();
        SetStackModalAmount(initialAmt);
    }

    public void CloseStackActionModal()
    {
        stackModalSlotIndex = -1;
        if (stackModalRoot != null) stackModalRoot.SetActive(false);
    }

    private void AdjustStackModalAmount(int delta)
    {
        SetStackModalAmount(stackModalSelectedAmount + delta);
    }

    private void SetStackModalHalf()
    {
        if (inventory == null || stackModalSlotIndex < 0 || stackModalSlotIndex >= inventory.slots.Length) return;
        int qty = inventory.slots[stackModalSlotIndex].quantity;
        SetStackModalAmount(Mathf.Max(1, qty / 2));
    }

    private void SetStackModalMax()
    {
        if (inventory == null || stackModalSlotIndex < 0 || stackModalSlotIndex >= inventory.slots.Length) return;
        int qty = inventory.slots[stackModalSlotIndex].quantity;
        int maxVal = stackModalDefaultDiscard ? qty : Mathf.Max(1, qty - 1);
        SetStackModalAmount(maxVal);
    }

    private void SetStackModalAmount(int amount)
    {
        if (inventory == null || stackModalSlotIndex < 0 || stackModalSlotIndex >= inventory.slots.Length) return;
        int total = Mathf.Max(1, inventory.slots[stackModalSlotIndex].quantity);
        stackModalSelectedAmount = Mathf.Clamp(amount, 1, total);

        if (stackModalSlider != null)
        {
            stackModalSuppressSlider = true;
            stackModalSlider.value = stackModalSelectedAmount;
            stackModalSuppressSlider = false;
        }
        if (stackModalAmountText != null)
        {
            stackModalAmountText.text = $"Quantidade selecionada: {stackModalSelectedAmount} / {total}";
        }
        if (stackModalBtnSplit != null)
        {
            bool hasEmpty = false;
            for (int i = 0; i < inventory.slots.Length; i++)
            {
                if (inventory.slots[i].IsEmpty()) { hasEmpty = true; break; }
            }
            bool canSplit = total >= 2 && stackModalSelectedAmount >= 1 && stackModalSelectedAmount < total && hasEmpty;
            stackModalBtnSplit.interactable = canSplit;
            if (stackModalSplitText != null)
                stackModalSplitText.text = canSplit ? $"DIVIDIR ({stackModalSelectedAmount})" : (hasEmpty ? "DIVIDIR" : "MOCHILA CHEIA");
        }
        if (stackModalDropText != null)
        {
            stackModalDropText.text = $"DESCARTAR ({stackModalSelectedAmount})";
        }
    }

    public bool ConfirmStackModalAction(int amount, bool discardMode)
    {
        if (inventory == null || stackModalSlotIndex < 0 || stackModalSlotIndex >= inventory.slots.Length)
        {
            CloseStackActionModal();
            return false;
        }

        int slotIdx = stackModalSlotIndex;
        InventorySlot slot = inventory.slots[slotIdx];
        if (slot.IsEmpty())
        {
            CloseStackActionModal();
            return false;
        }

        int clamped = Mathf.Clamp(amount, 1, slot.quantity);
        bool ok;
        if (discardMode)
        {
            ItemData d = ItemDatabase.GetItem(slot.itemId);
            inventory.DropItemToWorld(slotIdx, clamped);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Descartado: {clamped}x {(d != null ? d.itemName : "Item")}");
            ok = true;
        }
        else
        {
            ok = inventory.SplitSlot(slotIdx, clamped, -1);
            if (!ok && HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FFAA55>Nao foi possivel dividir (sem slot vazio ou quantidade invalida).</color>");
        }

        CloseStackActionModal();
        RefreshUI();
        SelectSlot(slotIdx);
        return ok;
    }

    private void HandleRelayBeginDrag(DragSourceKind kind, int sourceVisualOrHotbarIndex, PointerEventData eventData)
    {
        if (!isOpen || IsStackModalOpen || inventory == null) return;

        if (kind == DragSourceKind.BackpackSlot)
        {
            if (sourceVisualOrHotbarIndex < 0 || sourceVisualOrHotbarIndex >= displayedSlotIndices.Count) return;
            int realIdx = displayedSlotIndices[sourceVisualOrHotbarIndex];
            if (realIdx < 0 || realIdx >= inventory.slots.Length || inventory.slots[realIdx].IsEmpty()) return;

            dragActive = true;
            dragSourceKind = DragSourceKind.BackpackSlot;
            dragSourceIndex = realIdx;
            dragItemId = inventory.slots[realIdx].itemId;
            dragItemQty = inventory.slots[realIdx].quantity;
        }
        else if (kind == DragSourceKind.HotbarDockSlot)
        {
            if (sourceVisualOrHotbarIndex < 0 || sourceVisualOrHotbarIndex >= inventory.hotbarItemIds.Length) return;
            int itemId = inventory.hotbarItemIds[sourceVisualOrHotbarIndex];
            if (itemId <= 0) return;

            dragActive = true;
            dragSourceKind = DragSourceKind.HotbarDockSlot;
            dragSourceIndex = sourceVisualOrHotbarIndex;
            dragItemId = itemId;
            dragItemQty = inventory.GetItemCount(itemId);
        }
        else return;

        if (dragGhostObj != null)
        {
            dragGhostObj.SetActive(true);
            dragGhostObj.transform.SetAsLastSibling();
            if (dragGhostIcon != null) dragGhostIcon.sprite = FragaProIconLibrary.GetItemIcon(dragItemId);
            if (dragGhostQty != null) dragGhostQty.text = dragItemQty > 1 ? $"x{dragItemQty}" : "";
            UpdateDragGhostScreenPos(eventData.position, eventData.pressEventCamera);
        }
        UpdateDragHoverFeedback(eventData.position, eventData.pressEventCamera);
    }

    private void HandleRelayDrag(PointerEventData eventData)
    {
        if (!dragActive) return;
        UpdateDragGhostScreenPos(eventData.position, eventData.pressEventCamera);
        UpdateDragHoverFeedback(eventData.position, eventData.pressEventCamera);
    }

    private void HandleRelayEndDrag(PointerEventData eventData)
    {
        if (!dragActive) return;
        lastDragEndFrame = Time.frameCount;

        DragSourceKind srcKind = dragSourceKind;
        int srcIdx = dragSourceIndex;
        CancelActiveDrag();

        CompleteDragDropAtScreenPoint(srcKind, srcIdx, eventData.position, eventData.pressEventCamera);
        RefreshUI();
    }

    private void CancelActiveDrag()
    {
        dragActive = false;
        dragSourceKind = DragSourceKind.None;
        dragSourceIndex = -1;
        dragItemId = -1;
        dragItemQty = 0;
        if (dragGhostObj != null) dragGhostObj.SetActive(false);
        ResetDropZoneIdleHighlights();
    }

    private void UpdateDragGhostScreenPos(Vector2 screenPos, Camera eventCamera)
    {
        if (rootRect == null || dragGhostRect == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPos, eventCamera, out Vector2 localPt))
        {
            dragGhostRect.anchoredPosition = localPt;
        }
    }

    private void ResetDropZoneIdleHighlights()
    {
        for (int g = 0; g < gearCardOutlines.Length; g++)
        {
            if (gearCardOutlines[g] != null) gearCardOutlines[g].effectColor = new Color(0.26f, 0.65f, 0.85f, 0.35f);
        }
        if (discardDropOutline != null) discardDropOutline.effectColor = new Color(0.92f, 0.34f, 0.30f, 0.45f);
        if (discardDropBg != null) discardDropBg.color = new Color(0.22f, 0.10f, 0.12f, 0.90f);
    }

    private void UpdateDragHoverFeedback(Vector2 screenPos, Camera eventCamera)
    {
        ResetDropZoneIdleHighlights();

        for (int v = 0; v < slotViews.Count && v < displayedSlotIndices.Count; v++)
        {
            if (slotViews[v].rect == null || slotViews[v].outline == null) continue;
            bool hit = dragSourceKind == DragSourceKind.BackpackSlot &&
                       RectTransformUtility.RectangleContainsScreenPoint(slotViews[v].rect, screenPos, eventCamera);
            if (hit)
                slotViews[v].outline.effectColor = new Color(0.45f, 1.0f, 0.72f, 0.98f);
            else
                slotViews[v].outline.effectColor = displayedSlotIndices[v] == selectedSlotIndex
                    ? new Color(0.42f, 0.92f, 1.0f, 0.92f)
                    : new Color(0.26f, 0.62f, 0.80f, 0.30f);
        }

        for (int h = 0; h < hotbarDockViews.Length; h++)
        {
            if (hotbarDockViews[h].rect == null || hotbarDockViews[h].outline == null) continue;
            bool hit = RectTransformUtility.RectangleContainsScreenPoint(hotbarDockViews[h].rect, screenPos, eventCamera);
            hotbarDockViews[h].outline.effectColor = hit
                ? new Color(0.42f, 1.0f, 0.68f, 0.98f)
                : new Color(0.28f, 0.65f, 0.85f, 0.40f);
        }

        if (dragSourceKind == DragSourceKind.BackpackSlot)
        {
            ItemData dragItemData = dragItemId > 0 ? ItemDatabase.GetItem(dragItemId) : null;
            EquipSlotType[] gearTypes = { EquipSlotType.MainHand, EquipSlotType.OffHand, EquipSlotType.BodyArmor };
            for (int g = 0; g < gearCardRects.Length; g++)
            {
                if (gearCardRects[g] == null || gearCardOutlines[g] == null) continue;
                bool matches = dragItemData != null && dragItemData.equipSlot == gearTypes[g];
                bool hit = RectTransformUtility.RectangleContainsScreenPoint(gearCardRects[g], screenPos, eventCamera);
                if (hit)
                    gearCardOutlines[g].effectColor = matches ? new Color(0.36f, 1.0f, 0.62f, 1f) : new Color(0.95f, 0.30f, 0.28f, 0.95f);
                else if (matches)
                    gearCardOutlines[g].effectColor = new Color(0.40f, 0.92f, 1.0f, 0.85f);
            }
        }

        if (discardDropRect != null && RectTransformUtility.RectangleContainsScreenPoint(discardDropRect, screenPos, eventCamera))
        {
            if (discardDropOutline != null) discardDropOutline.effectColor = new Color(1.0f, 0.42f, 0.36f, 1.0f);
            if (discardDropBg != null) discardDropBg.color = new Color(0.44f, 0.14f, 0.16f, 0.96f);
        }
    }

    private void CompleteDragDropAtScreenPoint(DragSourceKind srcKind, int srcIdx, Vector2 screenPos, Camera eventCamera)
    {
        if (inventory == null || srcIdx < 0) return;

        if (srcKind == DragSourceKind.BackpackSlot)
        {
            for (int v = 0; v < slotViews.Count && v < displayedSlotIndices.Count; v++)
            {
                if (slotViews[v].rect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(slotViews[v].rect, screenPos, eventCamera))
                {
                    int targetReal = displayedSlotIndices[v];
                    if (targetReal != srcIdx)
                    {
                        bool shiftSplit = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                                          inventory.slots[srcIdx].quantity > 1 &&
                                          (inventory.slots[targetReal].IsEmpty() || inventory.slots[targetReal].itemId == inventory.slots[srcIdx].itemId);
                        int moveAmt = shiftSplit ? Mathf.Max(1, inventory.slots[srcIdx].quantity / 2) : -1;
                        TryExecuteSlotDrop(srcIdx, targetReal, moveAmt);
                    }
                    return;
                }
            }

            for (int h = 0; h < hotbarDockViews.Length; h++)
            {
                if (hotbarDockViews[h].rect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(hotbarDockViews[h].rect, screenPos, eventCamera))
                {
                    TryDropSlotToHotbar(srcIdx, h);
                    return;
                }
            }

            EquipSlotType[] gearTypes = { EquipSlotType.MainHand, EquipSlotType.OffHand, EquipSlotType.BodyArmor };
            for (int g = 0; g < gearCardRects.Length; g++)
            {
                if (gearCardRects[g] != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(gearCardRects[g], screenPos, eventCamera))
                {
                    TryDropSlotToGear(srcIdx, gearTypes[g]);
                    return;
                }
            }

            bool droppedOnDiscard = discardDropRect != null &&
                                    RectTransformUtility.RectangleContainsScreenPoint(discardDropRect, screenPos, eventCamera);
            bool droppedOutsideWindow = windowRect != null &&
                                        !RectTransformUtility.RectangleContainsScreenPoint(windowRect, screenPos, eventCamera);
            if (droppedOnDiscard || droppedOutsideWindow)
            {
                TryDropSlotToDiscard(srcIdx, -1);
            }
        }
        else if (srcKind == DragSourceKind.HotbarDockSlot)
        {
            for (int h = 0; h < hotbarDockViews.Length; h++)
            {
                if (hotbarDockViews[h].rect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(hotbarDockViews[h].rect, screenPos, eventCamera))
                {
                    if (h != srcIdx) TryDropHotbarToHotbar(srcIdx, h);
                    return;
                }
            }

            bool droppedOnDiscard = discardDropRect != null &&
                                    RectTransformUtility.RectangleContainsScreenPoint(discardDropRect, screenPos, eventCamera);
            bool droppedOutsideWindow = windowRect != null &&
                                        !RectTransformUtility.RectangleContainsScreenPoint(windowRect, screenPos, eventCamera);
            if (droppedOnDiscard || droppedOutsideWindow)
            {
                inventory.ClearHotbarSlot(srcIdx);
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"Atalho [{srcIdx + 1}] removido.");
            }
        }
    }

    private sealed class InventoryDragRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private SurvivalInventoryCraftUI owner;
        private DragSourceKind kind;
        private int index;

        public void Bind(SurvivalInventoryCraftUI ui, DragSourceKind sourceKind, int idx)
        {
            owner = ui;
            kind = sourceKind;
            index = idx;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (owner != null) owner.HandleRelayBeginDrag(kind, index, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (owner != null) owner.HandleRelayDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (owner != null) owner.HandleRelayEndDrag(eventData);
        }
    }

    private void OnShareSelectedCoop()
    {
        if (inventory == null || selectedSlotIndex < 0) return;
        InventorySlot slot = inventory.slots[selectedSlotIndex];
        if (slot.IsEmpty()) return;
        int shareAmt = slot.quantity > 1 ? Mathf.Max(1, slot.quantity / 2) : 1;
        FragaCoopSession.GetOrCreate().TryShareOrDropItem(inventory, selectedSlotIndex, shareAmt);
        RefreshUI();
        SelectSlot(selectedSlotIndex);
    }

    // =================== ABA 2: FABRICACAO (CRAFTING SYSTEM INTEGRADO) ===================

    private void BuildCraftingTabContent(Transform parent)
    {
        GameObject stationBar = new GameObject("StationBar");
        stationBar.transform.SetParent(parent, false);
        RectTransform sbRT = stationBar.AddComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(0f, 1f);
        sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.pivot = new Vector2(0.5f, 1f);
        sbRT.anchoredPosition = Vector2.zero;
        sbRT.sizeDelta = new Vector2(0f, 42f);

        stationFilterButtons.Clear();
        stationFilterBgs.Clear();
        stationStatusDots.Clear();

        AddStationFilterBtn(stationBar.transform, "TODAS", null, 0f, 96f);
        AddStationFilterBtn(stationBar.transform, "MAOS LIVRES", CraftingStationType.Hand, 102f, 138f);
        AddStationFilterBtn(stationBar.transform, "FOGUEIRA", CraftingStationType.Bonfire, 246f, 134f);
        AddStationFilterBtn(stationBar.transform, "BANCADA", CraftingStationType.Workbench, 386f, 134f);
        AddStationFilterBtn(stationBar.transform, "CALDEIRAO", CraftingStationType.Cauldron, 526f, 140f);

        GameObject countPill = new GameObject("CraftCountPill");
        countPill.transform.SetParent(stationBar.transform, false);
        RectTransform cpRT = countPill.AddComponent<RectTransform>();
        cpRT.anchorMin = new Vector2(1f, 0.5f);
        cpRT.anchorMax = new Vector2(1f, 0.5f);
        cpRT.pivot = new Vector2(1f, 0.5f);
        cpRT.anchoredPosition = new Vector2(-214f, 0f);
        cpRT.sizeDelta = new Vector2(160f, 36f);
        Image cpBg = countPill.AddComponent<Image>();
        cpBg.sprite = FragaProIconLibrary.GetPillSprite();
        cpBg.type = Image.Type.Sliced;
        cpBg.color = new Color(0.08f, 0.14f, 0.20f, 0.90f);
        craftCountBannerText = CreateText(countPill.transform, "PRONTAS: 0/0", 11, FontStyle.Bold, new Color(0.62f, 0.95f, 0.78f));

        // Botao Filtro "SO FABRICAVEIS"
        GameObject onlyBtnObj = new GameObject("Btn_OnlyCraftable");
        onlyBtnObj.transform.SetParent(stationBar.transform, false);
        RectTransform obRT = onlyBtnObj.AddComponent<RectTransform>();
        obRT.anchorMin = new Vector2(1f, 0.5f);
        obRT.anchorMax = new Vector2(1f, 0.5f);
        obRT.pivot = new Vector2(1f, 0.5f);
        obRT.anchoredPosition = Vector2.zero;
        obRT.sizeDelta = new Vector2(204f, 38f);
        onlyCraftableBtnBg = onlyBtnObj.AddComponent<Image>();
        onlyCraftableBtnBg.sprite = FragaProIconLibrary.GetPillSprite();
        onlyCraftableBtnBg.type = Image.Type.Sliced;
        onlyCraftableBtnBg.color = new Color(0.14f, 0.22f, 0.30f, 0.90f);
        onlyBtnObj.AddComponent<Button>().onClick.AddListener(OnToggleOnlyCraftableClicked);
        onlyCraftableBtnText = CreateText(onlyBtnObj.transform, "SO DISPONIVEIS: OFF", 11, FontStyle.Bold, Color.white);

        // Banner de Guia Passo a Passo
        GameObject guideBanner = new GameObject("SurvivalCraftGuideBanner");
        guideBanner.transform.SetParent(parent, false);
        RectTransform gbRT = guideBanner.AddComponent<RectTransform>();
        gbRT.anchorMin = new Vector2(0f, 1f);
        gbRT.anchorMax = new Vector2(1f, 1f);
        gbRT.pivot = new Vector2(0.5f, 1f);
        gbRT.anchoredPosition = new Vector2(0f, -46f);
        gbRT.sizeDelta = new Vector2(0f, 40f);
        Image gbImg = guideBanner.AddComponent<Image>();
        gbImg.sprite = FragaProIconLibrary.GetPillSprite();
        gbImg.type = Image.Type.Sliced;
        gbImg.color = new Color(0.10f, 0.19f, 0.15f, 0.94f);
        Outline gbOut = guideBanner.AddComponent<Outline>();
        gbOut.effectColor = new Color(0.35f, 0.85f, 0.58f, 0.45f);

        craftQuickGuideText = CreateCardBoxText(guideBanner.transform, "GuideText", 14f, -2f, 14f, 36f, 11, FontStyle.Bold, new Color(0.92f, 0.98f, 0.92f), TextAnchor.MiddleLeft);
        craftQuickGuideText.text =
            "<color=#FFDD55>FLUXO INICIAL:</color> 1) Pegue <b>Ervas, Galhos e Pedras Soltas</b> com <b>[E]</b>  ->  " +
            "2) Crie <b>CORDA (2 Ervas)</b>  ->  3) Crie <b>MACHADO DE PEDRA</b> para cortar arvores/rochas!";

        // Barra de Progresso de Fabricacao
        GameObject progBg = new GameObject("CraftProgressBG");
        progBg.transform.SetParent(parent, false);
        RectTransform pbRT = progBg.AddComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0f, 1f);
        pbRT.anchorMax = new Vector2(1f, 1f);
        pbRT.pivot = new Vector2(0.5f, 1f);
        pbRT.anchoredPosition = new Vector2(0f, -90f);
        pbRT.sizeDelta = new Vector2(0f, 38f);
        Image pbImg = progBg.AddComponent<Image>();
        pbImg.sprite = FragaProIconLibrary.GetPillSprite();
        pbImg.type = Image.Type.Sliced;
        pbImg.color = new Color(0.07f, 0.11f, 0.16f, 0.94f);
        Outline pbOut = progBg.AddComponent<Outline>();
        pbOut.effectColor = new Color(0.28f, 0.68f, 0.84f, 0.35f);
        pbOut.effectDistance = new Vector2(1f, -1f);

        GameObject progFill = new GameObject("CraftProgressFill");
        progFill.transform.SetParent(progBg.transform, false);
        RectTransform pfRT = progFill.AddComponent<RectTransform>();
        pfRT.anchorMin = Vector2.zero;
        pfRT.anchorMax = Vector2.one;
        pfRT.offsetMin = new Vector2(4f, 4f);
        pfRT.offsetMax = new Vector2(-284f, -4f);
        craftProgressFill = progFill.AddComponent<Image>();
        craftProgressFill.sprite = FragaProIconLibrary.GetBarFillSprite();
        craftProgressFill.type = Image.Type.Filled;
        craftProgressFill.fillMethod = Image.FillMethod.Horizontal;
        craftProgressFill.fillAmount = 0f;
        craftProgressFill.color = new Color(0.28f, 0.86f, 0.52f, 0.95f);
        craftProgressFill.raycastTarget = false;

        craftProgressLabel = CreateText(progBg.transform, "BANCADA PRONTA — Selecione uma receita", 11, FontStyle.Bold, new Color(0.90f, 0.98f, 0.94f));
        craftProgressLabel.alignment = TextAnchor.MiddleLeft;
        craftProgressLabel.rectTransform.offsetMin = new Vector2(12f, 17f);
        craftProgressLabel.rectTransform.offsetMax = new Vector2(-290f, -2f);

        craftQueueDetailLabel = CreateText(progBg.transform, "Fila: 0/8 | Producao em tempo real com cancelamento e reembolso 100%", 10, FontStyle.Normal, new Color(0.70f, 0.86f, 0.95f));
        craftQueueDetailLabel.alignment = TextAnchor.MiddleLeft;
        craftQueueDetailLabel.rectTransform.offsetMin = new Vector2(12f, 2f);
        craftQueueDetailLabel.rectTransform.offsetMax = new Vector2(-290f, -18f);

        btnCancelCurrentCraft = CreateQueueControlButton(progBg.transform, "Btn_CancelCurrentCraft", "CANCELAR ATUAL",
            new Vector2(-132f, 0f), new Vector2(142f, 28f), new Color(0.62f, 0.28f, 0.16f, 0.95f),
            CancelCurrentQueuedCraft, out btnCancelCurrentCraftBg);
        btnCancelAllCraft = CreateQueueControlButton(progBg.transform, "Btn_CancelAllCraft", "LIMPAR FILA",
            new Vector2(-6f, 0f), new Vector2(120f, 28f), new Color(0.52f, 0.14f, 0.16f, 0.95f),
            CancelAllQueuedCraft, out btnCancelAllCraftBg);

        GameObject scrollObj = new GameObject("CraftScroll");
        scrollObj.transform.SetParent(parent, false);
        RectTransform scRT = scrollObj.AddComponent<RectTransform>();
        scRT.anchorMin = Vector2.zero;
        scRT.anchorMax = Vector2.one;
        scRT.offsetMin = Vector2.zero;
        scRT.offsetMax = new Vector2(0f, -134f);

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 38f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0.01f);

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        craftContentRT = content.AddComponent<RectTransform>();
        craftContentRT.anchorMin = new Vector2(0f, 1f);
        craftContentRT.anchorMax = new Vector2(1f, 1f);
        craftContentRT.pivot = new Vector2(0.5f, 1f);
        craftContentRT.sizeDelta = new Vector2(0f, 1000f);

        scroll.viewport = vpRT;
        scroll.content = craftContentRT;

        RebuildCraftCards();
    }

    private void OnToggleOnlyCraftableClicked()
    {
        onlyCraftableFilter = !onlyCraftableFilter;
        if (onlyCraftableBtnText != null)
            onlyCraftableBtnText.text = onlyCraftableFilter ? "SO DISPONIVEIS: ON" : "SO DISPONIVEIS: OFF";
        if (onlyCraftableBtnBg != null)
            onlyCraftableBtnBg.color = onlyCraftableFilter ? new Color(0.18f, 0.56f, 0.42f, 0.96f) : new Color(0.14f, 0.22f, 0.30f, 0.90f);

        RebuildCraftCards();
        RefreshUI();
    }

    private void AddStationFilterBtn(Transform parent, string label, CraftingStationType? station, float x, float w)
    {
        GameObject obj = new GameObject($"Station_{label}");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(w, 38f);

        Image bg = obj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.12f, 0.20f, 0.28f, 0.90f);
        stationFilterBgs.Add(bg);

        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            craftStationFilter = station;
            RebuildCraftCards();
            RefreshUI();
        });
        stationFilterButtons.Add(btn);

        if (station.HasValue)
        {
            GameObject dot = new GameObject("StatusDot");
            dot.transform.SetParent(obj.transform, false);
            RectTransform dRT = dot.AddComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0f, 0.5f);
            dRT.anchorMax = new Vector2(0f, 0.5f);
            dRT.pivot = new Vector2(0f, 0.5f);
            dRT.anchoredPosition = new Vector2(10f, 0f);
            dRT.sizeDelta = new Vector2(12f, 12f);
            Image dImg = dot.AddComponent<Image>();
            dImg.sprite = FragaProIconLibrary.GetCircleBackground();
            dImg.color = Color.gray;
            dImg.raycastTarget = false;
            stationStatusDots.Add(dImg);
        }
        else
        {
            stationStatusDots.Add(null);
        }

        Text txt = CreateText(obj.transform, label, 11, FontStyle.Bold, Color.white);
        txt.rectTransform.offsetMin = new Vector2(station.HasValue ? 24f : 6f, 0f);
    }

    private void RebuildCraftCards()
    {
        for (int i = 0; i < craftCards.Count; i++)
        {
            if (craftCards[i].root != null) SafeDestroy(craftCards[i].root);
        }
        craftCards.Clear();

        if (craftingSystem == null) craftingSystem = CraftingSystem.Instance ?? FindAnyObjectByType<CraftingSystem>();
        if (craftingSystem == null || craftContentRT == null) return;

        List<CraftRecipe> all = craftingSystem.GetAllRecipes();
        int visibleIdx = 0;
        int cols = 3;
        float cardW = 358f;
        float cardH = 196f;
        float gapX = 16f;
        float gapY = 14f;

        for (int rIdx = 0; rIdx < all.Count; rIdx++)
        {
            CraftRecipe rec = all[rIdx];
            if (craftStationFilter.HasValue && rec.requiredStation != craftStationFilter.Value) continue;
            if (onlyCraftableFilter && !craftingSystem.CanCraft(rIdx)) continue;

            int col = visibleIdx % cols;
            int row = visibleIdx / cols;
            visibleIdx++;

            GameObject card = new GameObject($"RecipeCard_{rIdx}");
            card.transform.SetParent(craftContentRT, false);
            RectTransform rt = card.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(col * (cardW + gapX), -row * (cardH + gapY));
            rt.sizeDelta = new Vector2(cardW, cardH);

            Image bg = card.AddComponent<Image>();
            bg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.11f, 0.16f, 0.22f, 0.94f);

            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.28f, 0.62f, 0.84f, 0.40f);

            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(card.transform, false);
            RectTransform iRT = icoObj.AddComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0f, 1f);
            iRT.anchorMax = new Vector2(0f, 1f);
            iRT.pivot = new Vector2(0f, 1f);
            iRT.anchoredPosition = new Vector2(10f, -10f);
            iRT.sizeDelta = new Vector2(48f, 48f);
            Image icoImg = icoObj.AddComponent<Image>();
            icoImg.sprite = FragaProIconLibrary.GetItemIcon(rec.outputItemId);
            icoImg.preserveAspect = true;
            icoImg.raycastTarget = false;

            string qtySuffix = rec.outputAmount > 1 ? $" x{rec.outputAmount}" : "";
            Text titleTxt = CreateCardBoxText(card.transform, "Title", 66f, -6f, 8f, 24f, 14, FontStyle.Bold, new Color(1.0f, 0.92f, 0.50f), TextAnchor.MiddleLeft);
            titleTxt.text = rec.recipeName.ToUpper() + qtySuffix;

            Text stTxt = CreateCardBoxText(card.transform, "Station", 66f, -30f, 8f, 18f, 11, FontStyle.Bold, new Color(0.65f, 0.88f, 0.98f), TextAnchor.MiddleLeft);
            stTxt.text = $"{GetStationLabel(rec.requiredStation)}  ({rec.craftDuration:F1}s)";

            ItemData outData = ItemDatabase.GetItem(rec.outputItemId);
            Text effTxt = CreateCardBoxText(card.transform, "Effect", 66f, -48f, 8f, 20f, 11, FontStyle.Normal, new Color(0.58f, 0.96f, 0.78f), TextAnchor.MiddleLeft);
            effTxt.text = GetRecipeBenefitPreview(outData);

            GameObject reqObj = new GameObject("Ingredients");
            reqObj.transform.SetParent(card.transform, false);
            RectTransform reqRT = reqObj.AddComponent<RectTransform>();
            reqRT.anchorMin = Vector2.zero;
            reqRT.anchorMax = Vector2.one;
            reqRT.offsetMin = new Vector2(12f, 44f);
            reqRT.offsetMax = new Vector2(-12f, -70f);
            Text reqTxt = reqObj.AddComponent<Text>();
            reqTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            reqTxt.fontSize = 12;
            reqTxt.fontStyle = FontStyle.Bold;
            reqTxt.supportRichText = true;
            reqTxt.color = Color.white;
            Shadow reqSh = reqObj.AddComponent<Shadow>();
            reqSh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            reqSh.effectDistance = new Vector2(1f, -1f);

            int recipeIndex = rIdx;
            bool allowBatch5 = outData == null || outData.maxStack > 1;
            Button btn1 = CreateCraftActionButton(card.transform, "Btn_Craft1", $"CRIAR {rec.recipeName.ToUpper()}",
                new Vector2(10f, 8f), new Vector2(allowBatch5 ? 232f : 338f, 32f), () => ExecuteCraft(recipeIndex, 1), out Image btn1Bg);
            Button btn5 = CreateCraftActionButton(card.transform, "Btn_Craft5", "CRIAR x5",
                new Vector2(248f, 8f), new Vector2(100f, 32f), () => ExecuteCraft(recipeIndex, 5), out Image btn5Bg);
            btn5.gameObject.SetActive(allowBatch5);

            craftCards.Add(new CraftCardRefs
            {
                recipeIndex = rIdx,
                root = card,
                bg = bg,
                outline = outline,
                reqText = reqTxt,
                btnCraft1 = btn1,
                btnCraft1Bg = btn1Bg,
                btnCraft5 = btn5,
                btnCraft5Bg = btn5Bg
            });
        }

        int totalRows = Mathf.CeilToInt((float)visibleIdx / cols);
        craftContentRT.sizeDelta = new Vector2(0f, Mathf.Max(420f, totalRows * (cardH + gapY) + 20f));
    }

    private Button CreateCraftActionButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick, out Image bgOut)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        bgOut = obj.AddComponent<Image>();
        bgOut.sprite = FragaProIconLibrary.GetPillSprite();
        bgOut.type = Image.Type.Sliced;
        bgOut.color = new Color(0.18f, 0.58f, 0.42f, 0.95f);
        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        string shortLabel = label.Length > 22 ? label.Substring(0, 22) : label;
        CreateText(obj.transform, shortLabel, 11, FontStyle.Bold, Color.white);
        return btn;
    }

    private void ExecuteCraft(int recipeIndex, int batchCount)
    {
        if (craftingSystem == null) return;
        CraftRecipe rec = craftingSystem.GetRecipe(recipeIndex);
        if (rec == null) return;

        if (!craftingSystem.EnqueueCraft(recipeIndex, batchCount, out string reason))
        {
            if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(reason);
            return;
        }

        RefreshCraftQueueBar(true);
        if (HUDSurvivalOverlay.Instance != null)
        {
            string batchLabel = batchCount > 1 ? $" (x{batchCount})" : string.Empty;
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#77EEFF>Na fila{batchLabel}: {rec.recipeName} ({rec.craftDuration:F1}s)</color>");
        }
        if (onlyCraftableFilter) RebuildCraftCards();
        RefreshUI();
    }

    private static Button CreateQueueControlButton(Transform parent, string name, string label, Vector2 anchoredPos,
        Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick, out Image bgOut)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        bgOut = obj.AddComponent<Image>();
        bgOut.sprite = FragaProIconLibrary.GetPillSprite();
        bgOut.type = Image.Type.Sliced;
        bgOut.color = bgColor;
        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        CreateText(obj.transform, label, 10, FontStyle.Bold, Color.white);
        return btn;
    }

    private void CancelCurrentQueuedCraft()
    {
        if (craftingSystem == null) return;
        if (craftingSystem.CancelQueuedJob(0))
        {
            RefreshCraftQueueBar(true);
            if (onlyCraftableFilter) RebuildCraftCards();
            RefreshUI();
        }
    }

    private void CancelAllQueuedCraft()
    {
        if (craftingSystem == null) return;
        if (craftingSystem.CancelAllQueuedJobs() > 0)
        {
            RefreshCraftQueueBar(true);
            if (onlyCraftableFilter) RebuildCraftCards();
            RefreshUI();
        }
    }

    public void RefreshCraftQueueBar(bool force)
    {
        if (craftProgressFill == null || craftProgressLabel == null) return;
        if (craftingSystem == null || !craftingSystem.HasQueuedJobs)
        {
            if (force || lastCraftQueueBarSignature != "IDLE")
            {
                lastCraftQueueBarSignature = "IDLE";
                craftProgressFill.fillAmount = 0f;
                craftProgressFill.color = new Color(0.28f, 0.86f, 0.52f, 0.95f);
                craftProgressLabel.text = "BANCADA PRONTA — Clique em CRIAR para iniciar";
                if (craftQueueDetailLabel != null)
                    craftQueueDetailLabel.text = $"Fila: 0/{CraftingSystem.MaxCraftQueueSize} | Producao continua ao fechar mochila (reembolso 100% ao cancelar)";
                if (btnCancelCurrentCraft != null) btnCancelCurrentCraft.interactable = false;
                if (btnCancelCurrentCraftBg != null) btnCancelCurrentCraftBg.color = new Color(0.22f, 0.26f, 0.30f, 0.55f);
                if (btnCancelAllCraft != null) btnCancelAllCraft.interactable = false;
                if (btnCancelAllCraftBg != null) btnCancelAllCraftBg.color = new Color(0.22f, 0.26f, 0.30f, 0.55f);
            }
            return;
        }

        CraftQueueJob current = craftingSystem.CurrentJob;
        float prog = craftingSystem.CurrentJobProgress01;
        int pct = Mathf.RoundToInt(prog * 100f);
        bool paused = craftingSystem.IsCurrentJobPausedByStation;
        int queueCount = craftingSystem.CraftQueue.Count;
        string sig = $"{current.recipeIndex}_{pct}_{paused}_{queueCount}_{current.remaining:F1}";

        craftProgressFill.fillAmount = prog;
        craftProgressFill.color = paused
            ? new Color(0.95f, 0.68f, 0.22f, 0.95f)
            : new Color(0.28f, 0.88f, 0.54f, 0.95f);

        if (!force && sig == lastCraftQueueBarSignature) return;
        lastCraftQueueBarSignature = sig;

        craftProgressLabel.text = paused
            ? $"PAUSADO ({pct}%): {current.recipeName.ToUpper()} — Aproxime-se da estacao!"
            : $"FABRICANDO: {current.recipeName.ToUpper()} ({pct}% | {current.remaining:F1}s)";

        if (craftQueueDetailLabel != null)
        {
            string nextName = queueCount > 1 ? $" | Proximo: {craftingSystem.CraftQueue[1].recipeName}" : string.Empty;
            craftQueueDetailLabel.text = $"Fila: {queueCount}/{CraftingSystem.MaxCraftQueueSize}{nextName}";
        }

        if (btnCancelCurrentCraft != null) btnCancelCurrentCraft.interactable = true;
        if (btnCancelCurrentCraftBg != null) btnCancelCurrentCraftBg.color = new Color(0.68f, 0.34f, 0.16f, 0.95f);
        if (btnCancelAllCraft != null) btnCancelAllCraft.interactable = true;
        if (btnCancelAllCraftBg != null) btnCancelAllCraftBg.color = new Color(0.58f, 0.16f, 0.18f, 0.95f);
    }

    // =================== ABA 3: STATUS DO PERSONAGEM (FISIOLOGIA, TRAUMAS E COMBATE) ===================

    private void BuildCharacterStatusTabContent(Transform parent)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Coluna 1 (Esquerda): Fisiologia & Termorregulacao
        GameObject physCol = CreateSectionColumn(parent, "PhysiologyColumn", new Vector2(0f, 0f), new Vector2(0.33f, 1f), "FISIOLOGIA & SINAIS VITAIS", font);

        GameObject condBanner = CreateSubPanelBox(physCol.transform, "ConditionBanner", -48f, 40f, new Color(0.10f, 0.19f, 0.16f, 0.95f));
        charConditionBannerBg = condBanner.GetComponent<Image>();
        charConditionBannerOutline = condBanner.AddComponent<Outline>();
        charConditionBannerOutline.effectColor = new Color(0.34f, 0.92f, 0.58f, 0.55f);
        charConditionBannerText = CreateCardBoxText(condBanner.transform, "Txt", 12f, -4f, 12f, 32f, 11, FontStyle.Bold, new Color(0.78f, 0.98f, 0.86f), TextAnchor.MiddleLeft);

        gaugeHealth = CreateVitalGaugeRow(physCol.transform, "VIDA FISICA", FragaProIconLibrary.ControlIcon.HeartPulse, -94f, new Color(0.92f, 0.28f, 0.28f), font);
        gaugeStamina = CreateVitalGaugeRow(physCol.transform, "FOLEGO / ENERGIA", FragaProIconLibrary.ControlIcon.DodgeStep, -152f, new Color(0.96f, 0.78f, 0.24f), font);
        gaugeHunger = CreateVitalGaugeRow(physCol.transform, "FOME / NUTRICAO", FragaProIconLibrary.ControlIcon.HungerFork, -210f, new Color(0.92f, 0.56f, 0.22f), font);
        gaugeThirst = CreateVitalGaugeRow(physCol.transform, "SEDE / HIDRATACAO", FragaProIconLibrary.ControlIcon.WaterDrop, -268f, new Color(0.28f, 0.74f, 0.98f), font);
        gaugeSanity = CreateVitalGaugeRow(physCol.transform, "SANIDADE MENTAL", FragaProIconLibrary.ControlIcon.SanityEye, -326f, new Color(0.62f, 0.46f, 0.96f), font);
        gaugeTemp = CreateVitalGaugeRow(physCol.transform, "TEMPERATURA", FragaProIconLibrary.ControlIcon.Thermometer, -384f, new Color(0.32f, 0.90f, 0.80f), font);
        gaugeWetness = CreateVitalGaugeRow(physCol.transform, "UMIDADE ROUPAS", FragaProIconLibrary.ControlIcon.WaterDrop, -442f, new Color(0.38f, 0.64f, 0.96f), font);

        GameObject physNoteBox = CreateSubPanelBox(physCol.transform, "PhysNoteBox", -504f, 94f, new Color(0.06f, 0.10f, 0.15f, 0.94f));
        charPhysiologyNoteText = CreateCardBoxText(physNoteBox.transform, "PhysNote", 12f, -8f, 12f, 78f, 11, FontStyle.Bold, new Color(0.80f, 0.93f, 0.98f), TextAnchor.UpperLeft);

        // Coluna 2 (Centro): Anatomia, Traumas & Tratamento Rapido
        GameObject traumaCol = CreateSectionColumn(parent, "TraumaColumn", new Vector2(0.345f, 0f), new Vector2(0.685f, 1f), "ANATOMIA, TRAUMAS & CURA RAPIDA", font);

        GameObject anatomyPanel = CreateSubPanelBox(traumaCol.transform, "AnatomyDiagramPanel", -48f, 142f, new Color(0.07f, 0.12f, 0.17f, 0.96f));
        Outline anatOut = anatomyPanel.AddComponent<Outline>();
        anatOut.effectColor = new Color(0.30f, 0.78f, 0.92f, 0.40f);

        GameObject silObj = new GameObject("AnatomySilhouette");
        silObj.transform.SetParent(anatomyPanel.transform, false);
        RectTransform silRT = silObj.AddComponent<RectTransform>();
        silRT.anchorMin = new Vector2(0f, 0.5f);
        silRT.anchorMax = new Vector2(0f, 0.5f);
        silRT.pivot = new Vector2(0f, 0.5f);
        silRT.anchoredPosition = new Vector2(14f, 0f);
        silRT.sizeDelta = new Vector2(84f, 126f);
        anatomyBodyImg = silObj.AddComponent<Image>();
        anatomyBodyImg.sprite = FragaProIconLibrary.GetAnatomySilhouetteSprite();
        anatomyBodyImg.color = new Color(0.45f, 0.92f, 0.78f, 0.92f);
        anatomyBodyImg.preserveAspect = true;
        anatomyBodyImg.raycastTarget = false;

        anatomyHeadDot = CreateAnatomyZoneDot(silObj.transform, "HeadDot", new Vector2(0f, 44f));
        anatomyTorsoDot = CreateAnatomyZoneDot(silObj.transform, "TorsoDot", new Vector2(0f, 14f));
        anatomyLimbDot = CreateAnatomyZoneDot(silObj.transform, "LimbDot", new Vector2(11f, -34f));

        anatomySummaryText = CreateCardBoxText(anatomyPanel.transform, "AnatomySummary", 108f, -10f, 12f, 82f, 11, FontStyle.Bold, new Color(0.90f, 0.96f, 1f), TextAnchor.UpperLeft);

        GameObject treatAllObj = new GameObject("Btn_TreatAllTraumas");
        treatAllObj.transform.SetParent(anatomyPanel.transform, false);
        RectTransform taRT = treatAllObj.AddComponent<RectTransform>();
        taRT.anchorMin = new Vector2(1f, 0f);
        taRT.anchorMax = new Vector2(1f, 0f);
        taRT.pivot = new Vector2(1f, 0f);
        taRT.anchoredPosition = new Vector2(-12f, 10f);
        taRT.sizeDelta = new Vector2(238f, 32f);
        btnTreatAllBg = treatAllObj.AddComponent<Image>();
        btnTreatAllBg.sprite = FragaProIconLibrary.GetPillSprite();
        btnTreatAllBg.type = Image.Type.Sliced;
        btnTreatAllBg.color = new Color(0.16f, 0.36f, 0.28f, 0.95f);
        btnTreatAllTraumas = treatAllObj.AddComponent<Button>();
        btnTreatAllTraumas.onClick.AddListener(TryTreatAllActiveConditions);
        btnTreatAllText = CreateText(treatAllObj.transform, "SEM TRAUMAS ATIVOS", 10, FontStyle.Bold, Color.white);

        traumaBleeding = CreateTraumaCardRow(traumaCol.transform, "SANGRAMENTO", 206, -198f, () => TryQuickTreatCondition("bleeding"), font);
        traumaFracture = CreateTraumaCardRow(traumaCol.transform, "FRATURA OSSEA", 209, -278f, () => TryQuickTreatCondition("fracture"), font);
        traumaPoison = CreateTraumaCardRow(traumaCol.transform, "ENVENENAMENTO", 213, -358f, () => TryQuickTreatCondition("poison"), font);
        traumaSickness = CreateTraumaCardRow(traumaCol.transform, "DOENCA / MAL-ESTAR", 211, -438f, () => TryQuickTreatCondition("sickness"), font);
        traumaHypothermia = CreateTraumaCardRow(traumaCol.transform, "HIPOTERMIA", 211, -518f, () => TryQuickTreatCondition("hypothermia"), font);

        // Coluna 3 (Direita): Atributos de Combate, Furtividade e Ameaca
        GameObject combatCol = CreateSectionColumn(parent, "CombatAttributesColumn", new Vector2(0.70f, 0f), new Vector2(1f, 1f), "ATRIBUTOS & PERFIL TATICO", font);

        GameObject combatBox = CreateSubPanelBox(combatCol.transform, "CombatBox", -48f, 212f, new Color(0.11f, 0.16f, 0.22f, 0.95f));
        CreateCardBoxText(combatBox.transform, "Hdr", 12f, -8f, 12f, 22f, 12, FontStyle.Bold, new Color(1.0f, 0.86f, 0.42f), TextAnchor.MiddleLeft).text = "COMBATE & PROTECAO";
        charWeaponSlotIcon = CreateMiniBadgeIcon(combatBox.transform, "WeaponMini", new Vector2(14f, -36f));
        charArmorSlotIcon = CreateMiniBadgeIcon(combatBox.transform, "ArmorMini", new Vector2(54f, -36f));
        charCombatStatsText = CreateCardBoxText(combatBox.transform, "Body", 98f, -34f, 12f, 108f, 11, FontStyle.Bold, new Color(0.90f, 0.95f, 0.99f), TextAnchor.UpperLeft);
        charDefenseMeterFill = CreateMiniLabeledBar(combatBox.transform, "ARMADURA", -148f, new Color(0.42f, 0.86f, 0.98f));
        charInsulationMeterFill = CreateMiniLabeledBar(combatBox.transform, "ISOLAMENTO", -178f, new Color(0.98f, 0.72f, 0.34f));

        GameObject stealthBox = CreateSubPanelBox(combatCol.transform, "StealthBox", -272f, 174f, new Color(0.10f, 0.17f, 0.20f, 0.95f));
        CreateCardBoxText(stealthBox.transform, "Hdr", 12f, -8f, 12f, 22f, 12, FontStyle.Bold, new Color(0.46f, 0.94f, 0.84f), TextAnchor.MiddleLeft).text = "FURTIVIDADE & LUZ";
        charStealthStatsText = CreateCardBoxText(stealthBox.transform, "Body", 12f, -34f, 12f, 44f, 11, FontStyle.Bold, new Color(0.88f, 0.95f, 0.94f), TextAnchor.UpperLeft);
        charVisibilityMeterFill = CreateMiniLabeledBar(stealthBox.transform, "VISIBILIDADE", -84f, new Color(0.98f, 0.80f, 0.36f));
        charNoiseMeterFill = CreateMiniLabeledBar(stealthBox.transform, "RUIDO", -112f, new Color(0.96f, 0.42f, 0.36f));
        charLanternOilMeterFill = CreateMiniLabeledBar(stealthBox.transform, "OLEO LAMPIAO", -140f, new Color(0.42f, 0.92f, 0.72f));

        GameObject threatBox = CreateSubPanelBox(combatCol.transform, "ThreatBox", -458f, 142f, new Color(0.16f, 0.11f, 0.14f, 0.95f));
        CreateCardBoxText(threatBox.transform, "Hdr", 12f, -8f, 12f, 22f, 12, FontStyle.Bold, new Color(1.0f, 0.56f, 0.48f), TextAnchor.MiddleLeft).text = "AMEACA NA SERRA DA FRAGA";
        charWorldThreatText = CreateCardBoxText(threatBox.transform, "Body", 12f, -34f, 12f, 98f, 11, FontStyle.Bold, new Color(0.96f, 0.90f, 0.92f), TextAnchor.UpperLeft);
    }

    private static Image CreateAnatomyZoneDot(Transform parent, string name, Vector2 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(12f, 12f);
        Image img = obj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetCircleBackground();
        img.color = new Color(0.32f, 0.95f, 0.58f);
        img.raycastTarget = false;
        return img;
    }

    private static Image CreateMiniBadgeIcon(Transform parent, string name, Vector2 topLeftPos)
    {
        GameObject frame = new GameObject(name);
        frame.transform.SetParent(parent, false);
        RectTransform rt = frame.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeftPos;
        rt.sizeDelta = new Vector2(34f, 34f);
        Image bg = frame.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.06f, 0.10f, 0.15f, 0.96f);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(frame.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = Vector2.zero;
        iRT.anchorMax = Vector2.one;
        iRT.offsetMin = new Vector2(4f, 4f);
        iRT.offsetMax = new Vector2(-4f, -4f);
        Image ico = icoObj.AddComponent<Image>();
        ico.preserveAspect = true;
        ico.raycastTarget = false;
        return ico;
    }

    private Image CreateMiniLabeledBar(Transform parent, string label, float topY, Color barColor)
    {
        GameObject row = new GameObject($"MiniBar_{label}");
        row.transform.SetParent(parent, false);
        RectTransform rt = row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(12f, topY - 24f);
        rt.offsetMax = new Vector2(-12f, topY);

        Text lbl = CreateCardBoxText(row.transform, "Lbl", 0f, -2f, 120f, 20f, 9, FontStyle.Bold, new Color(0.76f, 0.88f, 0.95f), TextAnchor.MiddleLeft);
        lbl.text = label;

        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(row.transform, false);
        RectTransform bRT = bgObj.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 0.5f);
        bRT.anchorMax = new Vector2(1f, 0.5f);
        bRT.pivot = new Vector2(0.5f, 0.5f);
        bRT.offsetMin = new Vector2(92f, -5f);
        bRT.offsetMax = new Vector2(0f, 5f);
        bgObj.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.92f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(bgObj.transform, false);
        RectTransform fRT = fillObj.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero;
        fRT.offsetMax = Vector2.zero;
        Image fill = fillObj.AddComponent<Image>();
        fill.sprite = FragaProIconLibrary.GetBarFillSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.color = barColor;
        fill.fillAmount = 0f;
        return fill;
    }

    private GameObject CreateSectionColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string title, Font font)
    {
        GameObject col = new GameObject(name);
        col.transform.SetParent(parent, false);
        RectTransform rt = col.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = col.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.17f, 0.94f);
        Outline outl = col.AddComponent<Outline>();
        outl.effectColor = new Color(0.24f, 0.62f, 0.82f, 0.32f);

        GameObject hdr = new GameObject("HeaderStrip");
        hdr.transform.SetParent(col.transform, false);
        RectTransform hRT = hdr.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = new Vector2(0f, -8f);
        hRT.sizeDelta = new Vector2(-16f, 34f);
        Image hBg = hdr.AddComponent<Image>();
        hBg.sprite = FragaProIconLibrary.GetPillSprite();
        hBg.type = Image.Type.Sliced;
        hBg.color = new Color(0.12f, 0.22f, 0.32f, 0.92f);

        Text t = CreateText(hdr.transform, title, 12, FontStyle.Bold, new Color(0.58f, 0.94f, 1.0f));
        t.font = font;
        return col;
    }

    private static GameObject CreateSubPanelBox(Transform parent, string name, float topY, float height, Color bgColor)
    {
        GameObject box = new GameObject(name);
        box.transform.SetParent(parent, false);
        RectTransform rt = box.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(12f, topY - height);
        rt.offsetMax = new Vector2(-12f, topY);
        Image bg = box.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        bg.type = Image.Type.Sliced;
        bg.color = bgColor;
        return box;
    }

    private VitalGaugeRefs CreateVitalGaugeRow(Transform parent, string label, FragaProIconLibrary.ControlIcon iconType, float topY, Color barColor, Font font)
    {
        GameObject row = new GameObject($"Gauge_{label}");
        row.transform.SetParent(parent, false);
        RectTransform rt = row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(14f, topY - 50f);
        rt.offsetMax = new Vector2(-14f, topY);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(row.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 1f);
        iRT.anchorMax = new Vector2(0f, 1f);
        iRT.pivot = new Vector2(0f, 1f);
        iRT.anchoredPosition = new Vector2(0f, -2f);
        iRT.sizeDelta = new Vector2(18f, 18f);
        Image ico = icoObj.AddComponent<Image>();
        ico.sprite = FragaProIconLibrary.GetControlIcon(iconType);
        ico.preserveAspect = true;
        ico.raycastTarget = false;

        Text lbl = CreateCardBoxText(row.transform, "Label", 24f, 0f, 110f, 22f, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        lbl.font = font;
        lbl.text = label;

        Text valTxt = CreateCardBoxText(row.transform, "Value", 120f, 0f, 0f, 22f, 11, FontStyle.Bold, new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleRight);
        valTxt.font = font;

        GameObject barBg = new GameObject("BarBG");
        barBg.transform.SetParent(row.transform, false);
        RectTransform bRT = barBg.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 0f);
        bRT.anchorMax = new Vector2(1f, 0f);
        bRT.pivot = new Vector2(0.5f, 0f);
        bRT.anchoredPosition = new Vector2(0f, 6f);
        bRT.sizeDelta = new Vector2(0f, 16f);
        Image bImg = barBg.AddComponent<Image>();
        bImg.sprite = FragaProIconLibrary.GetPillSprite();
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.04f, 0.06f, 0.09f, 0.92f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barBg.transform, false);
        RectTransform fRT = fillObj.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = new Vector2(2f, 2f);
        fRT.offsetMax = new Vector2(-2f, -2f);
        Image fill = fillObj.AddComponent<Image>();
        fill.sprite = FragaProIconLibrary.GetBarFillSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.color = barColor;
        fill.fillAmount = 1f;

        return new VitalGaugeRefs { fill = fill, valueText = valTxt };
    }

    private TraumaCardRefs CreateTraumaCardRow(Transform parent, string title, int remedyItemId, float topY, UnityEngine.Events.UnityAction onTreat, Font font)
    {
        GameObject card = new GameObject($"Trauma_{title}");
        card.transform.SetParent(parent, false);
        RectTransform rt = card.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(12f, topY - 72f);
        rt.offsetMax = new Vector2(-12f, topY);

        Image bg = card.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.11f, 0.16f, 0.22f, 0.94f);

        Outline outl = card.AddComponent<Outline>();
        outl.effectColor = new Color(0.28f, 0.72f, 0.56f, 0.45f);

        GameObject accent = new GameObject("AccentBar");
        accent.transform.SetParent(card.transform, false);
        RectTransform acRT = accent.AddComponent<RectTransform>();
        acRT.anchorMin = new Vector2(0f, 0.12f);
        acRT.anchorMax = new Vector2(0f, 0.88f);
        acRT.pivot = new Vector2(0f, 0.5f);
        acRT.anchoredPosition = new Vector2(4f, 0f);
        acRT.sizeDelta = new Vector2(4f, 0f);
        Image accentImg = accent.AddComponent<Image>();
        accentImg.color = new Color(0.28f, 0.86f, 0.54f);
        accentImg.raycastTarget = false;

        Text titleTxt = CreateCardBoxText(card.transform, "Title", 16f, -6f, 150f, 20f, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        titleTxt.font = font;
        titleTxt.text = title;

        Text stateTxt = CreateCardBoxText(card.transform, "State", 150f, -6f, 148f, 20f, 11, FontStyle.Bold, new Color(0.45f, 0.96f, 0.62f), TextAnchor.MiddleRight);
        stateTxt.font = font;

        Text detailTxt = CreateCardBoxText(card.transform, "Detail", 16f, -28f, 152f, 40f, 10, FontStyle.Normal, new Color(0.84f, 0.90f, 0.96f), TextAnchor.UpperLeft);
        detailTxt.font = font;

        GameObject btnObj = new GameObject("Btn_QuickTreat");
        btnObj.transform.SetParent(card.transform, false);
        RectTransform brt = btnObj.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 0.5f);
        brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(-10f, 0f);
        brt.sizeDelta = new Vector2(134f, 44f);
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.sprite = FragaProIconLibrary.GetPillSprite();
        btnBg.type = Image.Type.Sliced;
        btnBg.color = new Color(0.18f, 0.48f, 0.34f, 0.95f);
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onTreat);

        GameObject remIcoObj = new GameObject("RemedyIcon");
        remIcoObj.transform.SetParent(btnObj.transform, false);
        RectTransform riRT = remIcoObj.AddComponent<RectTransform>();
        riRT.anchorMin = new Vector2(0f, 0.5f);
        riRT.anchorMax = new Vector2(0f, 0.5f);
        riRT.pivot = new Vector2(0f, 0.5f);
        riRT.anchoredPosition = new Vector2(8f, 0f);
        riRT.sizeDelta = new Vector2(22f, 22f);
        Image remImg = remIcoObj.AddComponent<Image>();
        remImg.sprite = FragaProIconLibrary.GetItemIcon(remedyItemId);
        remImg.preserveAspect = true;
        remImg.raycastTarget = false;

        Text btnTxt = CreateText(btnObj.transform, "ESTAVEL", 9, FontStyle.Bold, Color.white);
        btnTxt.rectTransform.offsetMin = new Vector2(32f, 0f);
        btnTxt.rectTransform.offsetMax = new Vector2(-6f, 0f);

        return new TraumaCardRefs
        {
            bg = bg,
            outline = outl,
            accentBar = accentImg,
            stateText = stateTxt,
            detailText = detailTxt,
            treatBtn = btn,
            treatBtnBg = btnBg,
            treatBtnText = btnTxt,
            remedyIcon = remImg
        };
    }

    public void TryTreatAllActiveConditions()
    {
        bool anyCured = false;
        anyCured |= TryQuickTreatCondition("bleeding");
        anyCured |= TryQuickTreatCondition("fracture");
        anyCured |= TryQuickTreatCondition("poison");
        anyCured |= TryQuickTreatCondition("sickness");
        if (BodyTemperatureSystem.Instance != null && BodyTemperatureSystem.Instance.currentTemperature <= BodyTemperatureSystem.TIER1_SHIVER_TEMP)
        {
            anyCured |= TryQuickTreatCondition("hypothermia");
        }
        if (!anyCured && HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#FFCC66>Nenhum remedio disponivel para os traumas ativos.</color>");
        }
        RefreshCharacterStatusTab();
    }

    public bool TryQuickTreatCondition(string conditionKey)
    {
        FindDependencies();
        if (inventory == null || playerStatus == null) return false;

        int[] candidateIds;
        switch (conditionKey)
        {
            case "bleeding":
                if (!playerStatus.isBleeding) return false;
                candidateIds = new[] { 206, 213 }; // Ligaduras, Pomada
                break;
            case "fracture":
                if (!playerStatus.isFractured) return false;
                candidateIds = new[] { 209 }; // Tala de Madeira
                break;
            case "poison":
                if (!playerStatus.isPoisoned) return false;
                candidateIds = new[] { 213, 201, 211, 103 }; // Pomada, Sopa, Cha, Ervas
                break;
            case "sickness":
                if (!playerStatus.isSick) return false;
                candidateIds = new[] { 211, 201, 208 }; // Cha, Sopa, Ensopado
                break;
            case "hypothermia":
                candidateIds = new[] { 211, 208, 201 }; // Cha Quente, Ensopado, Sopa
                break;
            default:
                return false;
        }

        for (int i = 0; i < candidateIds.Length; i++)
        {
            int slotIdx = inventory.FindFirstSlotIndex(candidateIds[i]);
            if (slotIdx >= 0 && inventory.UseItem(slotIdx))
            {
                ItemData d = ItemDatabase.GetItem(candidateIds[i]);
                if (HUDSurvivalOverlay.Instance != null)
                {
                    HUDSurvivalOverlay.Instance.ShowLootTooltip(
                        $"<color=#66FFAA>Tratamento aplicado: {(d != null ? d.itemName : "Remedio")}</color>");
                }
                RefreshUI();
                return true;
            }
        }

        return false;
    }

    // =================== ABA 4: GRUPO / CO-OP (MULTIPLAYER-READY) ===================

    private void BuildCoopTabContent(Transform parent)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject banner = new GameObject("CoopSessionBanner");
        banner.transform.SetParent(parent, false);
        RectTransform bRT = banner.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 1f);
        bRT.anchorMax = new Vector2(1f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = Vector2.zero;
        bRT.sizeDelta = new Vector2(0f, 162f);

        Image bImg = banner.AddComponent<Image>();
        bImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.08f, 0.14f, 0.20f, 0.96f);
        Outline bOut = banner.AddComponent<Outline>();
        bOut.effectColor = new Color(0.32f, 0.84f, 0.95f, 0.45f);

        coopHeaderStatusText = CreateCardBoxText(banner.transform, "SessionInfo", 16f, -10f, 590f, 96f, 12, FontStyle.Bold, new Color(0.90f, 0.96f, 1.0f), TextAnchor.UpperLeft);
        coopHeaderStatusText.font = font;

        // Botoes de sessao Co-op no lado direito do banner
        CreateCoopBannerButton(banner.transform, "Btn_CycleName", "TROCAR CODINOME", new Vector2(-396f, -14f), 178f,
            new Color(0.18f, 0.34f, 0.48f, 0.95f), () => { FragaCoopSession.GetOrCreate().CycleDefaultCallsign(); RefreshCoopTab(); }, out _);

        CreateCoopBannerButton(banner.transform, "Btn_HostRoom", "ABRIR SALA CO-OP", new Vector2(-206f, -14f), 192f,
            new Color(0.16f, 0.52f, 0.36f, 0.96f), OnToggleCoopHostClicked, out coopHostBtnText);

        CreateCoopBannerButton(banner.transform, "Btn_BuildAccess", "BASE: GRUPO", new Vector2(-396f, -68f), 178f,
            new Color(0.24f, 0.40f, 0.28f, 0.95f), () => { FragaCoopSession.GetOrCreate().ToggleDefaultBuildAccess(); RefreshCoopTab(); },
            out coopAccessBtnText, out coopAccessBtnBg);

        CreateCoopBannerButton(banner.transform, "Btn_PreviewSquad", "SIMULAR ESQUADRAO", new Vector2(-206f, -68f), 192f,
            new Color(0.22f, 0.34f, 0.52f, 0.95f), () => { FragaCoopSession.GetOrCreate().ToggleSquadPreview(); RefreshCoopTab(); },
            out coopPreviewBtnText, out coopPreviewBtnBg);

        CreateCoopBannerButton(banner.transform, "Btn_PingPos", "PINGAR POSICAO", new Vector2(-14f, -40f), 178f,
            new Color(0.56f, 0.38f, 0.14f, 0.96f), () =>
            {
                Vector3 p = PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero;
                FragaCoopSession.GetOrCreate().SendQuickPing(p, "Ponto de Reuniao");
                RefreshCoopTab();
            }, out _);

        // Barra inferior de conexao direta por IP / Descoberta LAN
        GameObject ipObj = new GameObject("CoopIp_Input");
        ipObj.transform.SetParent(banner.transform, false);
        RectTransform ipRT = ipObj.AddComponent<RectTransform>();
        ipRT.anchorMin = new Vector2(0f, 1f);
        ipRT.anchorMax = new Vector2(0f, 1f);
        ipRT.pivot = new Vector2(0f, 1f);
        ipRT.anchoredPosition = new Vector2(16f, -114f);
        ipRT.sizeDelta = new Vector2(280f, 36f);
        Image ipBg = ipObj.AddComponent<Image>();
        ipBg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        ipBg.type = Image.Type.Sliced;
        ipBg.color = new Color(0.03f, 0.06f, 0.10f, 0.96f);

        coopIpInput = ipObj.AddComponent<InputField>();
        GameObject ipPh = new GameObject("Placeholder");
        ipPh.transform.SetParent(ipObj.transform, false);
        RectTransform phRT = ipPh.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(10f, 2f); phRT.offsetMax = new Vector2(-10f, -2f);
        Text phTxt = ipPh.AddComponent<Text>();
        phTxt.font = font; phTxt.fontSize = 11; phTxt.fontStyle = FontStyle.Italic;
        phTxt.alignment = TextAnchor.MiddleLeft; phTxt.color = new Color(0.55f, 0.65f, 0.74f, 0.78f);
        phTxt.text = "IP do amigo (ex: 192.168.1.15)";

        GameObject ipTxt = new GameObject("Text");
        ipTxt.transform.SetParent(ipObj.transform, false);
        RectTransform tRT = ipTxt.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(10f, 2f); tRT.offsetMax = new Vector2(-10f, -2f);
        Text valTxt = ipTxt.AddComponent<Text>();
        valTxt.font = font; valTxt.fontSize = 12; valTxt.fontStyle = FontStyle.Bold;
        valTxt.alignment = TextAnchor.MiddleLeft; valTxt.color = Color.white;
        coopIpInput.placeholder = phTxt;
        coopIpInput.textComponent = valTxt;
        coopIpInput.text = FragaLanCoopTransport.GetLocalIPv4Address();

        CreateCoopBannerButton(banner.transform, "Btn_FindLan", "BUSCAR LAN", new Vector2(-716f, -114f), 124f,
            new Color(0.16f, 0.30f, 0.44f, 0.96f), () =>
            {
                FragaLanCoopTransport t = FragaLanCoopTransport.GetOrCreate();
                t.StartLanDiscovery();
                if (t.DiscoveredRooms.Count > 0 && coopIpInput != null)
                {
                    coopIpInput.text = $"{t.DiscoveredRooms[0].hostIp}:{t.DiscoveredRooms[0].tcpPort}";
                }
                RefreshCoopTab();
            }, out _);

        CreateCoopBannerButton(banner.transform, "Btn_JoinIp", "ENTRAR (IP)", new Vector2(-584f, -114f), 124f,
            new Color(0.16f, 0.50f, 0.58f, 0.96f), () =>
            {
                string ip = coopIpInput != null ? coopIpInput.text : string.Empty;
                FragaCoopSession.GetOrCreate().JoinLanHost(ip);
                RefreshCoopTab();
            }, out _);

        // Lista de Membros do Esquadrao (4 Slots)
        GameObject rosterPanel = new GameObject("SquadRoster");
        rosterPanel.transform.SetParent(parent, false);
        RectTransform rRT = rosterPanel.AddComponent<RectTransform>();
        rRT.anchorMin = Vector2.zero;
        rRT.anchorMax = Vector2.one;
        rRT.offsetMin = Vector2.zero;
        rRT.offsetMax = new Vector2(0f, -172f);

        for (int i = 0; i < FragaCoopSession.MaxPartySize; i++)
        {
            coopMemberCards[i] = CreateCoopMemberCard(rosterPanel.transform, i, font);
        }
    }

    private void CreateCoopBannerButton(Transform parent, string name, string label, Vector2 topRightPos, float width,
        Color bgColor, UnityEngine.Events.UnityAction onClick, out Text textOut)
    {
        CreateCoopBannerButton(parent, name, label, topRightPos, width, bgColor, onClick, out textOut, out _);
    }

    private void CreateCoopBannerButton(Transform parent, string name, string label, Vector2 topRightPos, float width,
        Color bgColor, UnityEngine.Events.UnityAction onClick, out Text textOut, out Image bgOut)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = topRightPos;
        rt.sizeDelta = new Vector2(width, 42f);

        bgOut = obj.AddComponent<Image>();
        bgOut.sprite = FragaProIconLibrary.GetPillSprite();
        bgOut.type = Image.Type.Sliced;
        bgOut.color = bgColor;
        obj.AddComponent<Button>().onClick.AddListener(onClick);
        textOut = CreateText(obj.transform, label, 11, FontStyle.Bold, Color.white);
    }

    private CoopMemberCardRefs CreateCoopMemberCard(Transform parent, int index, Font font)
    {
        int col = index % 2;
        int row = index / 2;
        float w = 556f;
        float h = 236f;
        float x = col * (w + 24f);
        float y = -row * (h + 18f);

        GameObject card = new GameObject($"CoopMemberSlot_{index}");
        card.transform.SetParent(parent, false);
        RectTransform rt = card.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);

        Image bg = card.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.09f, 0.13f, 0.18f, 0.94f);
        Outline outl = card.AddComponent<Outline>();
        outl.effectColor = new Color(0.28f, 0.65f, 0.85f, 0.35f);

        GameObject badge = new GameObject("ColorBadge");
        badge.transform.SetParent(card.transform, false);
        RectTransform bRT = badge.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 1f);
        bRT.anchorMax = new Vector2(0f, 1f);
        bRT.pivot = new Vector2(0f, 1f);
        bRT.anchoredPosition = new Vector2(14f, -14f);
        bRT.sizeDelta = new Vector2(22f, 22f);
        Image badgeImg = badge.AddComponent<Image>();
        badgeImg.sprite = FragaProIconLibrary.GetCircleBackground();
        badgeImg.color = new Color(0.32f, 0.90f, 1.0f);

        Text nameTxt = CreateCardBoxText(card.transform, "Name", 44f, -12f, 160f, 26f, 15, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        nameTxt.font = font;

        Text roleTxt = CreateCardBoxText(card.transform, "RoleDist", 220f, -12f, 14f, 26f, 12, FontStyle.Bold, new Color(0.58f, 0.92f, 1.0f), TextAnchor.MiddleRight);
        roleTxt.font = font;

        GameObject hpBg = new GameObject("HpBarBG");
        hpBg.transform.SetParent(card.transform, false);
        RectTransform hpRT = hpBg.AddComponent<RectTransform>();
        hpRT.anchorMin = new Vector2(0f, 1f);
        hpRT.anchorMax = new Vector2(1f, 1f);
        hpRT.pivot = new Vector2(0.5f, 1f);
        hpRT.offsetMin = new Vector2(14f, -64f);
        hpRT.offsetMax = new Vector2(-14f, -46f);
        hpBg.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.90f);

        GameObject hpF = new GameObject("Fill");
        hpF.transform.SetParent(hpBg.transform, false);
        RectTransform hfRT = hpF.AddComponent<RectTransform>();
        hfRT.anchorMin = Vector2.zero;
        hfRT.anchorMax = Vector2.one;
        hfRT.offsetMin = Vector2.zero;
        hfRT.offsetMax = Vector2.zero;
        Image hpFill = hpF.AddComponent<Image>();
        hpFill.sprite = FragaProIconLibrary.GetBarFillSprite();
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.color = new Color(0.28f, 0.88f, 0.52f);

        Text statsTxt = CreateCardBoxText(card.transform, "Stats", 14f, -74f, 14f, 148f, 12, FontStyle.Bold, new Color(0.86f, 0.94f, 0.98f), TextAnchor.UpperLeft);
        statsTxt.font = font;

        return new CoopMemberCardRefs
        {
            root = card,
            bg = bg,
            colorBadge = badgeImg,
            nameText = nameTxt,
            roleDistText = roleTxt,
            hpFill = hpFill,
            statsText = statsTxt
        };
    }

    private void OnToggleCoopHostClicked()
    {
        FragaCoopSession coop = FragaCoopSession.GetOrCreate();
        if (coop.IsCoopSessionActive) coop.SetSoloMode();
        else coop.StartLanHost();
        RefreshCoopTab();
    }

    // =================== ATUALIZACAO ===================

    public void RefreshUI()
    {
        if (inventory == null) return;

        if (tabInvText != null)
            tabInvText.text = $"MOCHILA ({inventory.GetOccupiedSlotCount()}/{inventory.maxSlots})";

        // 0. Filtro ativo na Mochila
        for (int i = 0; i < invFilterPillBgs.Count; i++)
        {
            bool match = invFilterPillKeys[i] == invCategoryFilter;
            if (invFilterPillBgs[i] != null)
            {
                invFilterPillBgs[i].color = match
                    ? new Color(0.18f, 0.52f, 0.64f, 0.96f)
                    : new Color(0.14f, 0.22f, 0.30f, 0.88f);
            }
        }

        // 1. Paperdoll & Protecao + Durabilidade
        RefreshGearCard(mainHandGearIcon, mainHandGearText, mainHandDurFill, inventory.equippedMainHandId, "DESARMADO", FragaProIconLibrary.ControlIcon.AttackFist);
        RefreshGearCard(offHandGearIcon, offHandGearText, offHandDurFill, inventory.equippedOffHandId, "VAZIO", FragaProIconLibrary.ControlIcon.ShieldBlock);
        RefreshGearCard(armorGearIcon, armorGearText, armorDurFill, inventory.equippedArmorId, "SEM PELE", FragaProIconLibrary.ControlIcon.Backpack);

        if (gearStatsSummaryText != null)
        {
            float defPct = inventory.GetTotalArmorDefense() * 100f;
            float insPct = inventory.GetColdInsulation() * 100f;
            float weightKg = inventory.GetEstimatedWeightKg();
            float tempC = BodyTemperatureSystem.Instance != null ? BodyTemperatureSystem.Instance.currentTemperature : 36.6f;
            string loadTag = inventory.IsOverEncumbered
                ? "<color=#FF5555>SOBRECARGA</color>"
                : (inventory.IsHeavyLoad ? "<color=#FFBB44>PESADO</color>" : "<color=#77FFAA>NORMAL</color>");
            gearStatsSummaryText.text = $"CARGA: {weightKg:F1}/{InventoryAdvanced.MaxCarryWeightKg:F0}kg [{loadTag}]  |  DEF: {defPct:F0}%  |  FRIO: {insPct:F0}%  |  TEMP: {tempC:F1}C";
        }

        // 2. Grid de Slots (Zero-Find com cache SlotViewRefs)
        displayedSlotIndices.Clear();
        for (int i = 0; i < inventory.slots.Length; i++)
        {
            if (invCategoryFilter.HasValue)
            {
                if (inventory.slots[i].IsEmpty()) continue;
                ItemData d = ItemDatabase.GetItem(inventory.slots[i].itemId);
                if (d == null || d.category != invCategoryFilter.Value) continue;
            }
            displayedSlotIndices.Add(i);
        }

        for (int v = 0; v < slotViews.Count; v++)
        {
            SlotViewRefs view = slotViews[v];
            if (v >= displayedSlotIndices.Count)
            {
                view.icon.enabled = false;
                view.nameText.text = "";
                view.qtyText.text = "";
                view.tagText.text = "";
                view.durBg.SetActive(false);
                view.bg.color = new Color(0.08f, 0.11f, 0.15f, 0.45f);
                view.outline.effectColor = new Color(0.20f, 0.32f, 0.42f, 0.20f);
                continue;
            }

            int realIdx = displayedSlotIndices[v];
            InventorySlot slot = inventory.slots[realIdx];
            bool isSelected = realIdx == selectedSlotIndex;
            bool isSwapSource = realIdx == pendingSwapSlot;
            view.bg.color = isSelected ? new Color(0.18f, 0.36f, 0.48f, 0.96f) : new Color(0.11f, 0.16f, 0.22f, 0.90f);
            view.outline.effectColor = isSwapSource
                ? new Color(1.0f, 0.80f, 0.24f, 0.95f)
                : (isSelected ? new Color(0.42f, 0.92f, 1.0f, 0.92f) : new Color(0.26f, 0.62f, 0.80f, 0.30f));

            if (slot.IsEmpty())
            {
                view.icon.sprite = null;
                view.icon.enabled = false;
                view.nameText.text = "Vazio";
                view.qtyText.text = "";
                view.tagText.text = "";
                view.durBg.SetActive(false);
            }
            else
            {
                ItemData data = ItemDatabase.GetItem(slot.itemId);
                view.icon.enabled = true;
                view.icon.sprite = FragaProIconLibrary.GetItemIcon(slot.itemId);
                view.nameText.text = data != null ? data.itemName : $"Item {slot.itemId}";
                view.qtyText.text = $"x{slot.quantity}";

                bool isEquipped = inventory.equippedMainHandId == slot.itemId || inventory.equippedOffHandId == slot.itemId || inventory.equippedArmorId == slot.itemId;
                int hotbarPos = -1;
                for (int h = 0; h < inventory.hotbarItemIds.Length; h++)
                {
                    if (inventory.hotbarItemIds[h] == slot.itemId) { hotbarPos = h + 1; break; }
                }
                string baseTag = isEquipped ? "EQ" : (hotbarPos > 0 ? $"[{hotbarPos}]" : "");

                if (data != null && data.isPerishable && data.maxDurability > 0f && slot.durability > 0f)
                {
                    float frac = Mathf.Clamp01(slot.durability / data.maxDurability);
                    view.durBg.SetActive(true);
                    view.durFill.fillAmount = frac;
                    view.durFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.35f, 0.92f, 0.45f), frac);
                    view.tagText.text = baseTag;
                }
                else if (data != null && !data.isPerishable && data.maxDurability > 0f && slot.durability >= 0f)
                {
                    float frac = Mathf.Clamp01(slot.durability / data.maxDurability);
                    view.durBg.SetActive(true);
                    view.durFill.fillAmount = frac;
                    view.durFill.color = frac > 0f ? new Color(0.24f, 0.78f, 0.98f) : new Color(0.75f, 0.28f, 0.22f);
                    string charges = $"{Mathf.RoundToInt(slot.durability)}/{Mathf.RoundToInt(data.maxDurability)}";
                    view.tagText.text = string.IsNullOrEmpty(baseTag) ? charges : $"{baseTag} {charges}";
                }
                else
                {
                    view.durBg.SetActive(false);
                    view.tagText.text = baseTag;
                }
            }
        }

        // 2b. Sincronizar Hotbar Dock [1..6] integrada a Mochila
        for (int h = 0; h < hotbarDockViews.Length; h++)
        {
            HotbarDockRefs dock = hotbarDockViews[h];
            if (dock.root == null) continue;
            int itemId = (inventory.hotbarItemIds != null && h < inventory.hotbarItemIds.Length) ? inventory.hotbarItemIds[h] : -1;
            int count = itemId > 0 ? inventory.GetItemCount(itemId) : 0;
            bool isActive = (h == inventory.activeHotbarIndex) ||
                            (itemId > 0 && (inventory.equippedMainHandId == itemId || inventory.equippedOffHandId == itemId || inventory.equippedArmorId == itemId));
            dock.bg.color = isActive ? new Color(0.14f, 0.34f, 0.46f, 0.96f) : new Color(0.08f, 0.13f, 0.19f, 0.94f);
            dock.outline.effectColor = isActive ? new Color(0.42f, 0.92f, 1.0f, 0.95f) : new Color(0.28f, 0.65f, 0.85f, 0.40f);

            if (itemId > 0)
            {
                dock.icon.enabled = true;
                dock.icon.sprite = FragaProIconLibrary.GetItemIcon(itemId);
                dock.icon.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.30f);
                dock.qtyText.text = count > 1 ? $"x{count}" : (count == 1 ? "" : "0");
                dock.qtyText.color = count > 0 ? new Color(0.45f, 0.96f, 0.98f) : new Color(0.95f, 0.35f, 0.35f);

                ItemData d = ItemDatabase.GetItem(itemId);
                int slotIdx = inventory.FindFirstSlotIndex(itemId);
                if (d != null && slotIdx >= 0 && d.maxDurability > 0f && inventory.slots[slotIdx].durability >= 0f)
                {
                    float frac = Mathf.Clamp01(inventory.slots[slotIdx].durability / d.maxDurability);
                    dock.durBg.SetActive(true);
                    dock.durFill.fillAmount = frac;
                    dock.durFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.35f, 0.92f, 0.45f), frac);
                }
                else
                {
                    dock.durBg.SetActive(false);
                }
            }
            else
            {
                dock.icon.enabled = false;
                dock.qtyText.text = "";
                dock.durBg.SetActive(false);
            }
        }

        // 3. Status das estacoes de crafting + cards cacheados
        if (craftingSystem != null)
        {
            CraftingStationType?[] stationMap = { null, CraftingStationType.Hand, CraftingStationType.Bonfire, CraftingStationType.Workbench, CraftingStationType.Cauldron };
            for (int i = 0; i < stationFilterBgs.Count && i < stationMap.Length; i++)
            {
                if (stationFilterBgs[i] != null)
                {
                    bool selectedStation = stationMap[i] == craftStationFilter;
                    stationFilterBgs[i].color = selectedStation
                        ? new Color(0.18f, 0.48f, 0.62f, 0.96f)
                        : new Color(0.12f, 0.20f, 0.28f, 0.90f);
                }
                if (i < stationStatusDots.Count && stationStatusDots[i] != null && stationMap[i].HasValue)
                {
                    bool near = craftingSystem.IsNearStation(stationMap[i].Value);
                    stationStatusDots[i].color = near ? new Color(0.32f, 0.95f, 0.52f) : new Color(0.92f, 0.62f, 0.22f);
                }
            }

            List<CraftRecipe> allRecipes = craftingSystem.GetAllRecipes();
            int craftableNowCount = 0;
            for (int r = 0; r < allRecipes.Count; r++)
            {
                if (craftingSystem.CanCraft(r)) craftableNowCount++;
            }
            if (craftCountBannerText != null)
                craftCountBannerText.text = $"PRONTAS: {craftableNowCount}/{allRecipes.Count}";

            for (int c = 0; c < craftCards.Count; c++)
            {
                CraftCardRefs card = craftCards[c];
                CraftRecipe rec = craftingSystem.GetRecipe(card.recipeIndex);
                if (rec == null) continue;

                string reqStr = "INGREDIENTES:\n";
                for (int j = 0; j < rec.ingredientIds.Length; j++)
                {
                    int have = inventory.GetItemCount(rec.ingredientIds[j]);
                    int need = rec.ingredientAmounts[j];
                    string ingName = GetItemName(rec.ingredientIds[j]);
                    reqStr += have >= need
                        ? $"<color=#66FF99>- {ingName}: {have}/{need} [OK]</color>\n"
                        : $"<color=#FF7777>- {ingName}: {have}/{need} (Faltam {need - have})</color>\n";
                }

                bool canCraft = craftingSystem.CanCraft(card.recipeIndex, out string reason);
                int maxBatch = craftingSystem.GetMaxCraftableCount(card.recipeIndex);
                if (!canCraft && (reason.StartsWith("Requer") || reason.StartsWith("Cantil")))
                {
                    reqStr += $"<color=#FFBB44>{reason}</color>";
                }

                card.reqText.text = reqStr.TrimEnd();
                card.outline.effectColor = canCraft
                    ? new Color(0.32f, 0.90f, 0.58f, 0.72f)
                    : new Color(0.28f, 0.52f, 0.70f, 0.30f);
                card.btnCraft1.interactable = canCraft;
                card.btnCraft1Bg.color = canCraft ? new Color(0.18f, 0.64f, 0.44f, 0.96f) : new Color(0.24f, 0.28f, 0.34f, 0.55f);
                bool canBatch = maxBatch >= 2;
                card.btnCraft5.interactable = canBatch;
                card.btnCraft5Bg.color = canBatch ? new Color(0.20f, 0.52f, 0.68f, 0.96f) : new Color(0.24f, 0.28f, 0.34f, 0.55f);
                Text b5Txt = card.btnCraft5.GetComponentInChildren<Text>();
                if (b5Txt != null)
                {
                    b5Txt.text = canBatch ? $"CRIAR x{Mathf.Clamp(maxBatch, 2, 5)}" : "CRIAR x5";
                }
            }
        }

        // 4. Painel Personagem & Co-op se visiveis
        if (activeTabIndex == 2) RefreshCharacterStatusTab();
        else if (activeTabIndex == 3) RefreshCoopTab();
    }

    private void RefreshCharacterStatusTab()
    {
        FindDependencies();
        float hp = playerStatus != null ? playerStatus.health : 100f;
        float stamina = playerStatus != null ? playerStatus.stamina : 100f;
        float hunger = playerStatus != null ? playerStatus.hunger : 100f;
        float thirst = playerStatus != null ? playerStatus.thirst : 100f;
        float sanity = sanityCache != null ? sanityCache.sanity : 100f;
        float tempC = BodyTemperatureSystem.Instance != null
            ? BodyTemperatureSystem.Instance.currentTemperature
            : (playerStatus != null ? playerStatus.bodyTemperature : 37f);
        float wetness = BodyTemperatureSystem.Instance != null ? BodyTemperatureSystem.Instance.clothesWetness : 0f;

        SetGauge(gaugeHealth, hp / 100f, $"{hp:F0} / 100");
        float maxStam = playerStatus != null ? playerStatus.EffectiveMaxStamina : 100f;
        string stamExtra = (playerStatus != null && playerStatus.isExhausted) ? " [EXAUSTO]" : "";
        string stamText = maxStam < 99.5f ? $"{stamina:F0}/{maxStam:F0}{stamExtra}" : $"{stamina:F0}%{stamExtra}";
        SetGauge(gaugeStamina, stamina / 100f, stamText);
        SetGauge(gaugeHunger, hunger / 100f, $"{hunger:F0}%");
        SetGauge(gaugeThirst, thirst / 100f, $"{thirst:F0}%");
        SetGauge(gaugeSanity, sanity / 100f, $"{sanity:F0}%");
        SetGauge(gaugeTemp, Mathf.InverseLerp(28f, 38f, tempC), $"{tempC:F1} C");
        SetGauge(gaugeWetness, wetness, $"{wetness * 100f:F0}%");

        if (charPhysiologyNoteText != null)
        {
            string tempStage = "TERMONEUTRO";
            if (tempC <= BodyTemperatureSystem.TIER3_DAMAGE_TEMP) tempStage = "<color=#FF5555>HIPOTERMIA SEVERA</color>";
            else if (tempC <= BodyTemperatureSystem.TIER2_FATIGUE_TEMP) tempStage = "<color=#FFBB44>FADIGA TERMICA</color>";
            else if (tempC <= BodyTemperatureSystem.TIER1_SHIVER_TEMP) tempStage = "<color=#66CCFF>CALAFRIO INICIAL</color>";

            bool nearFire = playerStatus != null && playerStatus.isNearBonfire;
            bool comfortRegen = playerStatus != null && playerStatus.IsComfortRegenActive;
            string loadStr = inventory != null ? inventory.GetEncumbranceLabel() : "NORMAL";
            float regen = playerStatus != null ? playerStatus.staminaRegenRate : 8f;
            charPhysiologyNoteText.text =
                $"Estado Termico: {tempStage}  |  Carga: {loadStr}\n" +
                $"Fogueira: {(nearFire ? "<color=#66FF99>AQUECENDO</color>" : "Sem calor proximo")}  |  Cura: {(comfortRegen ? "<color=#66FF99>ATIVA</color>" : "Inativa")}\n" +
                $"Regen. Folego: {regen:F1}/s  |  Velocidade: {(playerStatus != null && (playerStatus.isFractured || (inventory != null && inventory.IsOverEncumbered)) ? "Reduzida" : "Normal")}";
        }

        // Traumas & Curas Rapidas
        bool bleeding = playerStatus != null && playerStatus.isBleeding;
        bool fractured = playerStatus != null && playerStatus.isFractured;
        bool poisoned = playerStatus != null && playerStatus.isPoisoned;
        bool sick = playerStatus != null && playerStatus.isSick;
        bool hypo = tempC <= BodyTemperatureSystem.TIER1_SHIVER_TEMP;

        int activeTraumaCount = (bleeding ? 1 : 0) + (fractured ? 1 : 0) + (poisoned ? 1 : 0) + (sick ? 1 : 0) + (hypo ? 1 : 0);
        if (charConditionBannerText != null)
        {
            float weightKg = inventory != null ? inventory.GetEstimatedWeightKg() : 0f;
            string condTag = activeTraumaCount == 0 && hp >= 70f
                ? "<color=#66FFAA>OPERACIONAL</color>"
                : (hp < 35f || activeTraumaCount >= 2 ? "<color=#FF5555>ESTADO CRITICO</color>" : "<color=#FFCC55>ALERTA MEDICO</color>");
            charConditionBannerText.text = $"CONDICAO: {condTag}   |   TRAUMAS: {activeTraumaCount}   |   CARGA: {weightKg:F1} kg";
            if (charConditionBannerBg != null)
                charConditionBannerBg.color = activeTraumaCount > 0 ? new Color(0.22f, 0.11f, 0.12f, 0.96f) : new Color(0.09f, 0.18f, 0.15f, 0.95f);
            if (charConditionBannerOutline != null)
                charConditionBannerOutline.effectColor = activeTraumaCount > 0 ? new Color(0.96f, 0.36f, 0.28f, 0.72f) : new Color(0.34f, 0.92f, 0.58f, 0.55f);
        }

        // Diagrama Anatomico Clinico
        bool headAlert = poisoned || sanity < 45f;
        bool torsoAlert = bleeding || sick || hypo;
        bool limbAlert = fractured || (playerStatus != null && playerStatus.isExhausted);
        Color okCol = new Color(0.32f, 0.95f, 0.58f);
        Color warnCol = new Color(1.0f, 0.34f, 0.28f);
        if (anatomyHeadDot != null) anatomyHeadDot.color = headAlert ? warnCol : okCol;
        if (anatomyTorsoDot != null) anatomyTorsoDot.color = torsoAlert ? warnCol : okCol;
        if (anatomyLimbDot != null) anatomyLimbDot.color = limbAlert ? warnCol : okCol;
        if (anatomyBodyImg != null)
        {
            anatomyBodyImg.color = activeTraumaCount >= 2
                ? new Color(1.0f, 0.42f, 0.38f, 0.92f)
                : (activeTraumaCount == 1 ? new Color(0.98f, 0.78f, 0.34f, 0.92f) : new Color(0.45f, 0.92f, 0.78f, 0.92f));
        }
        if (anatomySummaryText != null)
        {
            int integrityPct = Mathf.Clamp(Mathf.RoundToInt(hp - activeTraumaCount * 10f), 0, 100);
            anatomySummaryText.text =
                $"INTEGRIDADE FISICA: <color={(integrityPct >= 70 ? "#66FFAA" : "#FF7766")}>{integrityPct}%</color>\n" +
                $"CABECA / MENTE: {(headAlert ? "<color=#FF7766>INSTAVEL</color>" : "<color=#77FFAA>ESTAVEL</color>")}\n" +
                $"TORAX / ORGAOS: {(torsoAlert ? "<color=#FF7766>COMPROMETIDO</color>" : "<color=#77FFAA>INTACTO</color>")}\n" +
                $"MEMBROS / OSSOS: {(limbAlert ? "<color=#FFBB44>LIMITADO</color>" : "<color=#77FFAA>NORMAL</color>")}";
        }
        if (btnTreatAllTraumas != null)
        {
            btnTreatAllTraumas.interactable = activeTraumaCount > 0;
            if (btnTreatAllBg != null)
                btnTreatAllBg.color = activeTraumaCount > 0 ? new Color(0.18f, 0.62f, 0.40f, 0.96f) : new Color(0.14f, 0.22f, 0.28f, 0.75f);
            if (btnTreatAllText != null)
                btnTreatAllText.text = activeTraumaCount > 0 ? $"APLICAR CURAS DISPONIVEIS ({activeTraumaCount})" : "ORGANISMO ESTAVEL";
        }

        UpdateTraumaCard(traumaBleeding, bleeding,
            "Hemorragia ativa: perde Vida por segundo.",
            "Sem hemorragias.",
            FindBestRemedyLabel(206, 213), "Precisa Atadura/Pomada");

        UpdateTraumaCard(traumaFracture, fractured,
            "Osso quebrado: -30% velocidade, sem corrida/esquiva.",
            "Mobilidade ossea intacta.",
            FindBestRemedyLabel(209), "Precisa Tala (209)");

        UpdateTraumaCard(traumaPoison, poisoned,
            $"Toxina ativa ({(playerStatus != null ? playerStatus.poisonTimer : 0f):F0}s): drena sanidade.",
            "Sem toxinas no organismo.",
            FindBestRemedyLabel(213, 201, 211, 103), "Precisa Sopa/Cha/Ervas");

        UpdateTraumaCard(traumaSickness, sick,
            $"Febre/Mal-estar ({(playerStatus != null ? playerStatus.sicknessTimer : 0f):F0}s): -35% regen. de folego.",
            "Sistema imunologico estavel.",
            FindBestRemedyLabel(211, 201, 208), "Precisa Cha/Sopa");

        UpdateTraumaCard(traumaHypothermia, hypo,
            $"Temperatura baixa ({tempC:F1} C): esquente-se na fogueira ou tome Cha.",
            $"Termorregulacao normal ({tempC:F1} C).",
            FindBestRemedyLabel(211, 208, 201), "Aquecer na Fogueira");

        // Atributos de Combate, Furtividade e Ameaca
        if (charCombatStatsText != null && inventory != null)
        {
            ItemData main = inventory.equippedMainHandId > 0 ? ItemDatabase.GetItem(inventory.equippedMainHandId) : null;
            ItemData armor = inventory.equippedArmorId > 0 ? ItemDatabase.GetItem(inventory.equippedArmorId) : null;
            float dmg = main != null ? main.attackDamage : 10f;
            float range = main != null ? main.attackRange : 2.1f;
            float defNorm = Mathf.Clamp01(inventory.GetTotalArmorDefense());
            float insNorm = Mathf.Clamp01(inventory.GetColdInsulation());
            float defPct = defNorm * 100f;
            float insPct = insNorm * 100f;
            if (charWeaponSlotIcon != null)
                charWeaponSlotIcon.sprite = main != null ? FragaProIconLibrary.GetItemIcon(main.id) : FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.AttackFist);
            if (charArmorSlotIcon != null)
                charArmorSlotIcon.sprite = armor != null ? FragaProIconLibrary.GetItemIcon(armor.id) : FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.ShieldBlock);
            if (charDefenseMeterFill != null) charDefenseMeterFill.fillAmount = defNorm;
            if (charInsulationMeterFill != null) charInsulationMeterFill.fillAmount = insNorm;
            charCombatStatsText.text =
                $"Arma Ativa: {(main != null ? main.itemName : "Punhos")}\n" +
                $"Dano Base: {dmg:F0}  |  Alcance: {range:F1}m\n" +
                $"Defesa: {defPct:F0}%  |  Frio: {insPct:F0}%\n" +
                $"Escudo: {(inventory.equippedOffHandId == 309 ? "85% ATIVO" : "Parry Manual")}";
        }

        if (charStealthStatsText != null)
        {
            float visNorm = FragaStealthSystem.Instance != null ? Mathf.Clamp01(FragaStealthSystem.Instance.Visibility01) : 0.35f;
            float noiseNorm = FragaStealthSystem.Instance != null ? Mathf.Clamp01(FragaStealthSystem.Instance.Noise01) : 0.10f;
            float vis = visNorm * 100f;
            float noise = noiseNorm * 100f;
            bool hidden = FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.IsHidden;
            float oil = sanityCache != null ? sanityCache.oil : 100f;
            if (charVisibilityMeterFill != null) charVisibilityMeterFill.fillAmount = visNorm;
            if (charNoiseMeterFill != null) charNoiseMeterFill.fillAmount = noiseNorm;
            if (charLanternOilMeterFill != null) charLanternOilMeterFill.fillAmount = Mathf.Clamp01(oil / 100f);
            charStealthStatsText.text =
                $"Postura: {(hidden ? "<color=#66FFAA>OCULTO</color>" : "<color=#FFCC55>EXPOSTO</color>")}  |  Luz: {(sanityCache != null && sanityCache.isLampOn ? "LIGADA" : "APAGADA")}\n" +
                $"Visibilidade: {vis:F0}%  |  Ruido: {noise:F0}%  |  Oleo: {oil:F0}%";
        }

        if (charWorldThreatText != null)
        {
            CoopWorldThreatSnapshot threat = FragaCoopSession.GetOrCreate().GetThreatSnapshot();
            int day = DayNightCycle.Instance != null ? DayNightCycle.Instance.currentDay : 1;
            float hour = DayNightCycle.Instance != null ? DayNightCycle.Instance.currentTime : 10f;
            int hh = Mathf.FloorToInt(hour);
            int mm = Mathf.FloorToInt((hour - hh) * 60f);
            string bossStr = threat.bossActive
                ? (threat.bossEnraged ? "<color=#FF4444>VULTO ANCIAO ENFURECIDO!</color>" : "<color=#FF8844>VULTO ANCIAO ATIVO</color>")
                : "<color=#77EEAA>Inativo</color>";

            charWorldThreatText.text =
                $"Jornada: Dia {day} ({hh:00}:{mm:00})\n" +
                $"Vultos Proximos: {threat.activeVultoCount}\n" +
                $"Chefe: {bossStr}\n" +
                $"Estruturas Base: {StructureHealth.AllStructures.Count}  |  Obras: {WorldBlueprintSite.AllSites.Count}";
        }
    }

    private string FindBestRemedyLabel(params int[] itemIds)
    {
        if (inventory == null) return string.Empty;
        for (int i = 0; i < itemIds.Length; i++)
        {
            if (inventory.HasItem(itemIds[i]))
            {
                ItemData d = ItemDatabase.GetItem(itemIds[i]);
                return d != null ? d.itemName : "Remedio";
            }
        }
        return string.Empty;
    }

    private static void SetGauge(VitalGaugeRefs gauge, float norm, string text)
    {
        if (gauge.fill != null) gauge.fill.fillAmount = Mathf.Clamp01(norm);
        if (gauge.valueText != null) gauge.valueText.text = text;
    }

    private static void UpdateTraumaCard(TraumaCardRefs card, bool active, string activeDesc, string safeDesc,
        string availableRemedyName, string missingRemedyLabel)
    {
        bool hasRemedy = !string.IsNullOrEmpty(availableRemedyName);
        if (card.stateText != null)
        {
            card.stateText.text = active ? "<color=#FF6655>ATIVO</color>" : "<color=#66FFAA>ESTAVEL</color>";
        }
        if (card.detailText != null)
        {
            card.detailText.text = active ? activeDesc : safeDesc;
        }
        if (card.outline != null)
        {
            card.outline.effectColor = active
                ? new Color(0.95f, 0.34f, 0.30f, 0.85f)
                : new Color(0.28f, 0.72f, 0.56f, 0.35f);
        }
        if (card.accentBar != null)
        {
            card.accentBar.color = active ? new Color(0.98f, 0.32f, 0.28f) : new Color(0.28f, 0.86f, 0.54f);
        }
        if (card.treatBtn != null)
        {
            card.treatBtn.interactable = active && hasRemedy;
            card.treatBtnBg.color = !active
                ? new Color(0.16f, 0.24f, 0.30f, 0.65f)
                : (hasRemedy ? new Color(0.18f, 0.62f, 0.42f, 0.96f) : new Color(0.45f, 0.24f, 0.22f, 0.85f));
            card.treatBtnText.text = !active
                ? "ESTAVEL"
                : (hasRemedy ? $"TRATAR ({availableRemedyName.Split(' ')[0].ToUpper()})" : missingRemedyLabel.ToUpper());
        }
    }

    private void RefreshCoopTab()
    {
        FragaCoopSession coop = FragaCoopSession.GetOrCreate();
        coop.SyncLocalVitalsNow();

        if (coopHeaderStatusText != null)
        {
            string modeLabel = coop.sessionMode == CoopSessionMode.Solo
                ? "SOLO (OFFLINE)"
                : (coop.sessionMode == CoopSessionMode.CoopHost ? "HOST CO-OP ATIVO" : "CLIENTE CO-OP");
            string netStatus = FragaLanCoopTransport.Instance != null
                ? FragaLanCoopTransport.Instance.ConnectionStatus
                : "Desconectado";
            coopHeaderStatusText.text =
                $"<color=#55EEFF>MODO DE SESSAO: {modeLabel}</color>\n" +
                $"Codinome: {coop.localPlayerName}  |  Sala: <b>{coop.sessionJoinCode}</b>\n" +
                $"Seu IP LAN: <color=#7CFFCC><b>{coop.LocalLanAddress}:7777</b></color>  |  Esquadrao: {coop.CountConnectedMembers()}/{FragaCoopSession.MaxPartySize}\n" +
                $"Rede: {netStatus}  (Wi-Fi direto ou VPN ZeroTier/Radmin)";
        }

        if (coopHostBtnText != null)
            coopHostBtnText.text = coop.IsCoopSessionActive ? "VOLTAR P/ SOLO" : "ABRIR SALA CO-OP";

        if (coopAccessBtnText != null)
            coopAccessBtnText.text = coop.DefaultStructuresShared ? "BASE: GRUPO (LIVRE)" : "BASE: SOMENTE DONO";
        if (coopAccessBtnBg != null)
            coopAccessBtnBg.color = coop.DefaultStructuresShared
                ? new Color(0.18f, 0.48f, 0.34f, 0.95f)
                : new Color(0.52f, 0.32f, 0.16f, 0.95f);

        if (coopPreviewBtnText != null)
            coopPreviewBtnText.text = coop.IsSquadPreviewEnabled ? "OCULTAR SIMULACAO" : "SIMULAR ESQUADRAO";
        if (coopPreviewBtnBg != null)
            coopPreviewBtnBg.color = coop.IsSquadPreviewEnabled
                ? new Color(0.20f, 0.52f, 0.64f, 0.96f)
                : new Color(0.22f, 0.34f, 0.52f, 0.95f);

        var members = coop.Members;
        for (int i = 0; i < coopMemberCards.Length; i++)
        {
            CoopMemberCardRefs card = coopMemberCards[i];
            if (card.root == null) continue;

            if (i < members.Count)
            {
                CoopMemberState m = members[i];
                if (ColorUtility.TryParseHtmlString(m.colorHex, out Color parsedCol))
                    card.colorBadge.color = parsedCol;

                card.nameText.text = m.isLocalPlayer ? $"{m.displayName} (VOCE)" : m.displayName;
                float dist = coop.GetDistanceToMember(in m);
                card.roleDistText.text = m.isLocalPlayer
                    ? (coop.sessionMode == CoopSessionMode.CoopHost ? "HOST | 0ms" : "LOCAL | 0ms")
                    : $"{dist:F0}m | {m.pingMs}ms";

                float hpFrac = m.maxHealth > 0f ? Mathf.Clamp01(m.health / m.maxHealth) : 1f;
                card.hpFill.fillAmount = hpFrac;
                card.hpFill.color = hpFrac > 0.45f ? new Color(0.28f, 0.88f, 0.52f) : new Color(0.95f, 0.32f, 0.28f);

                string alerts = "";
                if (m.isBleeding) alerts += "<color=#FF6655>[SANGRANDO] </color>";
                if (m.isHypothermic) alerts += "<color=#66CCFF>[FRIO] </color>";
                if (m.isFractured) alerts += "<color=#FFBB44>[FRATURA] </color>";
                if (m.isSick) alerts += "<color=#BBDD55>[DOENTE] </color>";
                if (string.IsNullOrEmpty(alerts)) alerts = "<color=#77FFAA>Condicao Estavel</color>";

                ItemData weapon = m.equippedMainHandId > 0 ? ItemDatabase.GetItem(m.equippedMainHandId) : null;
                card.statsText.text =
                    $"Vida: {m.health:F0}/{m.maxHealth:F0}  |  Folego: {m.stamina:F0}%  |  Temp: {m.bodyTemp:F1} C\n" +
                    $"Fome: {m.hunger:F0}%  |  Sede: {m.thirst:F0}%  |  Sanidade: {m.sanity:F0}%\n" +
                    $"Equipado: {(weapon != null ? weapon.itemName : "Punhos")}\n" +
                    $"Alertas: {alerts}";
                card.bg.color = new Color(0.10f, 0.16f, 0.22f, 0.95f);
            }
            else
            {
                card.colorBadge.color = new Color(0.32f, 0.38f, 0.44f);
                card.nameText.text = $"SLOT {i + 1} — VAGA ABERTA";
                card.roleDistText.text = coop.IsCoopSessionActive ? "AGUARDANDO CONEXAO" : "MODO SOLO";
                card.hpFill.fillAmount = 0f;
                card.statsText.text =
                    $"Convide ate {FragaCoopSession.MaxPartySize} jogadores usando o codigo <b>{coop.sessionJoinCode}</b>.\n" +
                    "Todos os baus compartilhados, fogueiras e obras do grupo ficam visiveis em tempo real.";
                card.bg.color = new Color(0.07f, 0.10f, 0.14f, 0.72f);
            }
        }
    }

    private void RefreshGearCard(Image ico, Text txt, Image durFill, int itemId, string emptyLabel, FragaProIconLibrary.ControlIcon fallbackIcon)
    {
        if (ico == null || txt == null) return;
        if (itemId > 0)
        {
            ItemData d = ItemDatabase.GetItem(itemId);
            ico.sprite = FragaProIconLibrary.GetItemIcon(itemId);
            txt.text = d != null ? d.itemName : $"Item {itemId}";

            if (durFill != null && d != null && !d.isPerishable && d.maxDurability > 0f && inventory != null)
            {
                int slotIdx = inventory.FindUsableEquipmentSlot(itemId);
                if (slotIdx >= 0 && inventory.slots != null && slotIdx < inventory.slots.Length)
                {
                    float rawDur = inventory.slots[slotIdx].durability < 0f ? d.maxDurability : inventory.slots[slotIdx].durability;
                    float frac = Mathf.Clamp01(rawDur / d.maxDurability);
                    durFill.fillAmount = frac;
                    durFill.color = frac > 0.25f ? new Color(0.30f, 0.88f, 0.96f) : new Color(0.95f, 0.34f, 0.24f);
                }
                else
                {
                    durFill.fillAmount = 1f;
                }
            }
            else if (durFill != null)
            {
                durFill.fillAmount = 1f;
            }
        }
        else
        {
            ico.sprite = FragaProIconLibrary.GetControlIcon(fallbackIcon);
            txt.text = emptyLabel;
            if (durFill != null) durFill.fillAmount = 0f;
        }
    }

    private static string GetRecipeBenefitPreview(ItemData d)
    {
        if (d == null) return "";
        if (d.id == 118) return "Amarra Machado, Picareta e Arco (Feita c/ 2 Ervas)";
        if (d.id == 300) return "Corta Arvores e Quebra Rochas [Clique do Mouse]";
        if (d.id == 301) return "Quebra Rochas e Minera Ferro [Clique do Mouse]";
        if (d.equipSlot != EquipSlotType.None)
        {
            if (d.armorDefense > 0f) return $"Defesa +{d.armorDefense * 100f:F0}% | Frio +{d.coldInsulation * 100f:F0}%";
            if (d.harvestTreeMultiplier > 1.5f) return $"Corta Arvores e Rochas (Dano {d.attackDamage:F0})";
            if (d.harvestRockMultiplier > 1.5f) return $"Minera Rochas (Dano {d.attackDamage:F0})";
            return $"Dano {d.attackDamage:F0} | Alcance {d.attackRange:F1}m";
        }
        if (d.type == ItemType.Consumable)
        {
            string s = "";
            if (d.hungerRestore > 0f) s += $"+{d.hungerRestore:F0} Fome ";
            if (d.thirstRestore > 0f) s += $"+{d.thirstRestore:F0} Sede ";
            if (d.healthRestore > 0f) s += $"+{d.healthRestore:F0} Vida ";
            if (d.staminaRestore > 0f) s += $"+{d.staminaRestore:F0} Folego ";
            if (d.warmthBonus > 0f) s += $"+{d.warmthBonus:F1}C ";
            if (d.id == 210) s += "(3 Goles)";
            if (!d.isPerishable && d.id == 212) s += "(Nao estraga)";
            return string.IsNullOrEmpty(s) ? d.description : s.Trim();
        }
        return d.description.Length > 44 ? d.description.Substring(0, 42) + "..." : d.description;
    }

    private static string GetStationLabel(CraftingStationType s)
    {
        switch (s)
        {
            case CraftingStationType.Hand: return "[Maos Livres] Crie direto na Mochila";
            case CraftingStationType.Bonfire: return "[Fogueira] Perto do Fogo Aceso";
            case CraftingStationType.Workbench: return "[Bancada] Perto da Mesa de Trabalho";
            case CraftingStationType.Cauldron: return "[Caldeirao] Alquimia Druida";
            default: return "Sobrevivencia";
        }
    }

    private static string GetItemName(int itemId)
    {
        ItemData data = ItemDatabase.GetItem(itemId);
        return data != null ? data.itemName : $"Item {itemId}";
    }

    private static Text CreateText(Transform parent, string content, int fontSize, FontStyle style, Color color)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text txt = obj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        txt.text = content;
        txt.raycastTarget = false;
        Shadow sh = obj.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(1f, -1f);
        return txt;
    }
}
