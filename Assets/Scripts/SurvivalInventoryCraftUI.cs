using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// SurvivalInventoryCraftUI: Mochila Completa (24 slots), Paperdoll de Equipamentos (Mao Dir / Mao Esq / Vestuario),
/// Defesa e Isolamento Termico, Categorias, Troca/Divisao/Descarte, Atalhos Hotbar (1-6) e Fabricacao Integrada ao CraftingSystem (Tiers 0-3).
/// ZERO emojis — 100% icones vetoriais profissionais via FragaProIconLibrary.
/// Inclui alternador Desktop (PC) <-> Mobile (Touch) no cabecalho e filtro "SO FABRICAVEIS".
/// </summary>
public class SurvivalInventoryCraftUI : MonoBehaviour
{
    public static SurvivalInventoryCraftUI Instance { get; private set; }

    [Header("Dependencies")]
    public InventoryAdvanced inventory;
    public CraftingSystem craftingSystem;
    public PlayerStatus playerStatus;
    public CameraTouchController cameraTouchZone;

    private CanvasGroup canvasGroup;
    private bool isOpen = false;
    public bool IsOpen => isOpen;

    private GameObject rootPanel;
    private GameObject inventoryTabObj;
    private GameObject craftingTabObj;
    private Text tabInvText;
    private Text tabCraftText;
    private Text controlModeBtnText;
    private Image controlModeBtnBg;

    // Filtros
    private ItemCategory? invCategoryFilter = null;
    private CraftingStationType? craftStationFilter = null;
    private bool onlyCraftableFilter = false;
    private Image onlyCraftableBtnBg;
    private Text onlyCraftableBtnText;
    private readonly List<Button> stationFilterButtons = new List<Button>();
    private readonly List<Image> stationStatusDots = new List<Image>();

    // Paperdoll Equipamento & Status
    private Image mainHandGearIcon;
    private Text mainHandGearText;
    private Image offHandGearIcon;
    private Text offHandGearText;
    private Image armorGearIcon;
    private Text armorGearText;
    private Text gearStatsSummaryText;

    // Painel de Detalhes
    private GameObject detailPane;
    private Image detailIconImg;
    private Text detailTitle;
    private Text detailDesc;
    private Text detailStats;
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

    private int selectedSlotIndex = -1;
    private int pendingSwapSlot = -1;
    private int nextHotbarAssignIdx = 0;

    // Grid 24 slots
    private readonly List<GameObject> slotObjects = new List<GameObject>();
    private readonly List<int> displayedSlotIndices = new List<int>();

    // Crafting Scroll
    private RectTransform craftContentRT;
    private readonly List<GameObject> recipeCards = new List<GameObject>();
    private Image craftProgressFill;
    private float craftProgressTimer = 0f;
    private float craftProgressDuration = 0f;

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
        RefreshControlModeButton();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= RefreshUI;
            inventory.OnEquipmentChanged -= RefreshUI;
        }
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.OnTouchLayoutChanged -= OnControlLayoutChanged;
        }
    }

    private void OnControlLayoutChanged(bool _)
    {
        RefreshControlModeButton();
    }

    private void FindDependencies()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (craftingSystem == null) craftingSystem = CraftingSystem.Instance ?? FindAnyObjectByType<CraftingSystem>();
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
        if (cameraTouchZone == null) cameraTouchZone = FindAnyObjectByType<CameraTouchController>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleMenu();
        }

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
        }

        if (craftProgressTimer > 0f && craftProgressFill != null)
        {
            craftProgressTimer -= Time.unscaledDeltaTime;
            craftProgressFill.fillAmount = 1f - Mathf.Clamp01(craftProgressTimer / Mathf.Max(0.01f, craftProgressDuration));
            if (craftProgressTimer <= 0f)
            {
                craftProgressFill.fillAmount = 0f;
            }
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
        if (craftingTabObj != null && craftingTabObj.activeSelf)
        {
            RebuildCraftCards();
        }
        RefreshUI();
    }

    public void OpenCraftingAtStation(CraftingStationType station)
    {
        OpenMenu();
        craftStationFilter = station;
        SwitchToTab(1);
    }

    public void CloseMenu()
    {
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

    private void BuildUIHierarchy()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (parentCanvas == null) return;

        CanvasScaler scaler = parentCanvas.GetComponent<CanvasScaler>() ?? parentCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 2.5f;

        rootPanel = new GameObject("SurvivalInventory_Root");
        rootPanel.transform.SetParent(parentCanvas.transform, false);
        RectTransform rootRT = rootPanel.AddComponent<RectTransform>();
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
        winRT.anchorMin = new Vector2(0.5f, 0.5f);
        winRT.anchorMax = new Vector2(0.5f, 0.5f);
        winRT.pivot = new Vector2(0.5f, 0.5f);
        winRT.sizeDelta = new Vector2(1140f, 756f);

        Image winBg = windowObj.AddComponent<Image>();
        winBg.color = new Color(0.07f, 0.11f, 0.15f, 0.96f);
        winBg.raycastTarget = true;

        Outline winOutline = windowObj.AddComponent<Outline>();
        winOutline.effectColor = new Color(0.28f, 0.78f, 0.90f, 0.50f);
        winOutline.effectDistance = new Vector2(2f, -2f);

        BuildHeader(windowObj.transform);

        inventoryTabObj = new GameObject("Tab_Inventory");
        inventoryTabObj.transform.SetParent(windowObj.transform, false);
        RectTransform invRT = inventoryTabObj.AddComponent<RectTransform>();
        invRT.anchorMin = Vector2.zero;
        invRT.anchorMax = Vector2.one;
        invRT.offsetMin = new Vector2(20f, 20f);
        invRT.offsetMax = new Vector2(-20f, -84f);

        BuildInventoryTabContent(inventoryTabObj.transform);

        craftingTabObj = new GameObject("Tab_Crafting");
        craftingTabObj.transform.SetParent(windowObj.transform, false);
        RectTransform craftRT = craftingTabObj.AddComponent<RectTransform>();
        craftRT.anchorMin = Vector2.zero;
        craftRT.anchorMax = Vector2.one;
        craftRT.offsetMin = new Vector2(20f, 20f);
        craftRT.offsetMax = new Vector2(-20f, -84f);

        BuildCraftingTabContent(craftingTabObj.transform);
        craftingTabObj.SetActive(false);
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
            FragaProIconLibrary.ControlIcon.Backpack, new Vector2(10f, 0f), 238f, () => SwitchToTab(0), out tabInvText);

        CreateTabButton(headerObj.transform, "Btn_TabCraft", "FABRICACAO",
            FragaProIconLibrary.ControlIcon.Workbench, new Vector2(258f, 0f), 230f, () => SwitchToTab(1), out tabCraftText);

        GameObject modeBtnObj = new GameObject("Btn_ControlMode");
        modeBtnObj.transform.SetParent(headerObj.transform, false);
        RectTransform mRT = modeBtnObj.AddComponent<RectTransform>();
        mRT.anchorMin = new Vector2(1f, 0.5f);
        mRT.anchorMax = new Vector2(1f, 0.5f);
        mRT.pivot = new Vector2(1f, 0.5f);
        mRT.anchoredPosition = new Vector2(-68f, 0f);
        mRT.sizeDelta = new Vector2(228f, 44f);

        controlModeBtnBg = modeBtnObj.AddComponent<Image>();
        controlModeBtnBg.sprite = FragaProIconLibrary.GetPillSprite();
        controlModeBtnBg.type = Image.Type.Sliced;
        controlModeBtnBg.color = new Color(0.14f, 0.28f, 0.38f, 0.92f);
        modeBtnObj.AddComponent<Button>().onClick.AddListener(OnToggleControlModeClicked);
        controlModeBtnText = CreateText(modeBtnObj.transform, "MODO: AUTO [F2]", 12, FontStyle.Bold, Color.white);

        GameObject closeBtnObj = new GameObject("Btn_Close");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        RectTransform closeRT = closeBtnObj.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0.5f);
        closeRT.anchorMax = new Vector2(1f, 0.5f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.anchoredPosition = new Vector2(-8f, 0f);
        closeRT.sizeDelta = new Vector2(48f, 48f);

        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        closeImg.type = Image.Type.Sliced;
        closeImg.color = new Color(0.65f, 0.16f, 0.16f, 0.90f);
        closeBtnObj.AddComponent<Button>().onClick.AddListener(CloseMenu);

        GameObject xIco = new GameObject("XIcon");
        xIco.transform.SetParent(closeBtnObj.transform, false);
        xIco.AddComponent<RectTransform>().sizeDelta = new Vector2(26f, 26f);
        Image xImg = xIco.AddComponent<Image>();
        xImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.CancelCross);
        xImg.preserveAspect = true;
        xImg.raycastTarget = false;
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

    private GameObject CreateTabButton(Transform parent, string name, string title, FragaProIconLibrary.ControlIcon icon, Vector2 pos, float width, UnityEngine.Events.UnityAction onClick, out Text labelOut)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 48f);

        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.12f, 0.22f, 0.32f, 0.90f);
        btnObj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(btnObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(14f, 0f);
        iRT.sizeDelta = new Vector2(26f, 26f);
        Image ico = icoObj.AddComponent<Image>();
        ico.sprite = FragaProIconLibrary.GetControlIcon(icon);
        ico.preserveAspect = true;
        ico.raycastTarget = false;

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRT = tObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(46f, 0f);
        tRT.offsetMax = new Vector2(-10f, 0f);
        labelOut = tObj.AddComponent<Text>();
        labelOut.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelOut.fontSize = 15;
        labelOut.fontStyle = FontStyle.Bold;
        labelOut.alignment = TextAnchor.MiddleLeft;
        labelOut.color = Color.white;
        labelOut.text = title;
        labelOut.raycastTarget = false;

        return btnObj;
    }

    private void SwitchToTab(int tabIndex)
    {
        if (inventoryTabObj != null) inventoryTabObj.SetActive(tabIndex == 0);
        if (craftingTabObj != null) craftingTabObj.SetActive(tabIndex == 1);

        if (tabInvText != null) tabInvText.color = (tabIndex == 0) ? Color.white : new Color(0.6f, 0.68f, 0.75f);
        if (tabCraftText != null) tabCraftText.color = (tabIndex == 1) ? Color.white : new Color(0.6f, 0.68f, 0.75f);

        if (tabIndex == 1) RebuildCraftCards();
        RefreshUI();
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

        CreateGearCard(gearStrip.transform, "MAO DIREITA", 0f, () => { if (inventory != null) inventory.EquipItem(-1); }, out mainHandGearIcon, out mainHandGearText);
        CreateGearCard(gearStrip.transform, "MAO ESQUERDA", 232f, () => { if (inventory != null && inventory.equippedOffHandId > 0) inventory.EquipItem(inventory.equippedOffHandId); }, out offHandGearIcon, out offHandGearText);
        CreateGearCard(gearStrip.transform, "VESTUARIO", 464f, () => { if (inventory != null && inventory.equippedArmorId > 0) inventory.EquipItem(inventory.equippedArmorId); }, out armorGearIcon, out armorGearText);

        BuildInvCategoryFilters(parent);

        GameObject gridContainer = new GameObject("SlotsGrid");
        gridContainer.transform.SetParent(parent, false);
        RectTransform gridRT = gridContainer.AddComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0f, 0f);
        gridRT.anchorMax = new Vector2(0.65f, 1f);
        gridRT.offsetMin = new Vector2(0f, 44f);
        gridRT.offsetMax = new Vector2(0f, -114f);

        slotObjects.Clear();
        int cols = 6;
        float slotSize = 104f;
        float spacing = 12f;
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
            nRT.anchorMin = new Vector2(0f, 0.08f);
            nRT.anchorMax = new Vector2(1f, 0.30f);
            nRT.offsetMin = new Vector2(4f, 0f);
            nRT.offsetMax = new Vector2(-4f, 0f);
            Text nameTxt = nameObj.AddComponent<Text>();
            nameTxt.font = font;
            nameTxt.fontSize = 10;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = new Color(0.86f, 0.92f, 0.96f);
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
            bTxt.fontSize = 12;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.alignment = TextAnchor.MiddleRight;
            bTxt.color = new Color(0.38f, 0.94f, 0.98f);
            bTxt.raycastTarget = false;

            GameObject tagObj = new GameObject("Tag");
            tagObj.transform.SetParent(slot.transform, false);
            RectTransform tgRT = tagObj.AddComponent<RectTransform>();
            tgRT.anchorMin = new Vector2(0f, 1f);
            tgRT.anchorMax = new Vector2(0f, 1f);
            tgRT.pivot = new Vector2(0f, 1f);
            tgRT.anchoredPosition = new Vector2(5f, -4f);
            tgRT.sizeDelta = new Vector2(64f, 16f);
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
            dfImg.type = Image.Type.Filled;
            dfImg.fillMethod = Image.FillMethod.Horizontal;
            durBg.SetActive(false);

            slotObjects.Add(slot);
        }

        // Barra inferior de Protecao / Isolamento Termico
        GameObject statsBar = new GameObject("GearStatsBar");
        statsBar.transform.SetParent(parent, false);
        RectTransform sbRT = statsBar.AddComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(0f, 0f);
        sbRT.anchorMax = new Vector2(0f, 0f);
        sbRT.pivot = new Vector2(0f, 0f);
        sbRT.anchoredPosition = new Vector2(0f, 2f);
        sbRT.sizeDelta = new Vector2(686f, 34f);

        Image sbImg = statsBar.AddComponent<Image>();
        sbImg.sprite = FragaProIconLibrary.GetPillSprite();
        sbImg.type = Image.Type.Sliced;
        sbImg.color = new Color(0.09f, 0.14f, 0.20f, 0.92f);
        gearStatsSummaryText = CreateText(statsBar.transform, "DEFESA: 0%  |  FRIO: 0%  |  TEMP: 36.6 C", 12, FontStyle.Bold, new Color(0.68f, 0.92f, 0.98f));

        BuildDetailPane(parent);
    }

    private void CreateGearCard(Transform parent, string header, float x, UnityEngine.Events.UnityAction onClick, out Image iconOut, out Text textOut)
    {
        GameObject card = new GameObject($"Gear_{header}");
        card.transform.SetParent(parent, false);
        RectTransform rt = card.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(222f, 56f);

        Image bg = card.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.10f, 0.16f, 0.22f, 0.90f);
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
        hRT.anchorMin = new Vector2(0f, 0.55f);
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
        vRT.anchorMin = new Vector2(0f, 0.08f);
        vRT.anchorMax = new Vector2(1f, 0.55f);
        vRT.offsetMin = new Vector2(54f, 0f);
        vRT.offsetMax = new Vector2(-8f, 0f);
        textOut = valObj.AddComponent<Text>();
        textOut.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textOut.fontSize = 12;
        textOut.fontStyle = FontStyle.Bold;
        textOut.color = Color.white;
        textOut.raycastTarget = false;
    }

    private void BuildInvCategoryFilters(Transform parent)
    {
        GameObject bar = new GameObject("CategoryFilterBar");
        bar.transform.SetParent(parent, false);
        RectTransform rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0.65f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, -70f);
        rt.sizeDelta = new Vector2(0f, 34f);

        AddFilterPill(bar.transform, "TODOS", 0f, 100f, () => { invCategoryFilter = null; RefreshUI(); });
        AddFilterPill(bar.transform, "ARMAS", 108f, 110f, () => { invCategoryFilter = ItemCategory.ToolAndWeapon; RefreshUI(); });
        AddFilterPill(bar.transform, "CONSUMIVEIS", 226f, 134f, () => { invCategoryFilter = ItemCategory.Consumable; RefreshUI(); });
        AddFilterPill(bar.transform, "RECURSOS", 368f, 120f, () => { invCategoryFilter = ItemCategory.Resource; RefreshUI(); });
        AddFilterPill(bar.transform, "DEFESA/LUZ", 496f, 130f, () => { invCategoryFilter = ItemCategory.ArmorAndLight; RefreshUI(); });
    }

    private void AddFilterPill(Transform parent, string label, float x, float w, UnityEngine.Events.UnityAction onClick)
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
        obj.AddComponent<Button>().onClick.AddListener(onClick);
        CreateText(obj.transform, label, 11, FontStyle.Bold, Color.white);
    }

    private void BuildDetailPane(Transform parent)
    {
        detailPane = new GameObject("DetailPane");
        detailPane.transform.SetParent(parent, false);
        RectTransform dRT = detailPane.AddComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0.67f, 0f);
        dRT.anchorMax = new Vector2(1f, 1f);
        dRT.offsetMin = Vector2.zero;
        dRT.offsetMax = Vector2.zero;

        Image dImg = detailPane.AddComponent<Image>();
        dImg.color = new Color(0.10f, 0.14f, 0.19f, 0.95f);
        Outline dOut = detailPane.AddComponent<Outline>();
        dOut.effectColor = new Color(0.25f, 0.65f, 0.85f, 0.35f);

        GameObject icoObj = new GameObject("DetailIcon");
        icoObj.transform.SetParent(detailPane.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.5f, 1f);
        iRT.anchorMax = new Vector2(0.5f, 1f);
        iRT.pivot = new Vector2(0.5f, 1f);
        iRT.anchoredPosition = new Vector2(0f, -18f);
        iRT.sizeDelta = new Vector2(78f, 78f);
        detailIconImg = icoObj.AddComponent<Image>();
        detailIconImg.preserveAspect = true;
        detailIconImg.raycastTarget = false;

        detailTitle = CreateAnchoredText(detailPane.transform, "Title", new Vector2(16f, -104f), new Vector2(-32f, 32f), 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        detailDesc = CreateAnchoredText(detailPane.transform, "Desc", new Vector2(16f, -140f), new Vector2(-32f, 80f), 13, FontStyle.Normal, new Color(0.84f, 0.90f, 0.95f), TextAnchor.UpperLeft);
        detailStats = CreateAnchoredText(detailPane.transform, "Stats", new Vector2(16f, -222f), new Vector2(-32f, 118f), 13, FontStyle.Bold, new Color(0.42f, 0.95f, 0.65f), TextAnchor.UpperLeft);

        detailUseButton = CreateActionBtn(detailPane.transform, "Btn_Use", "USAR / EQUIPAR", new Vector2(0f, 192f), new Vector2(280f, 40f), new Color(0.18f, 0.62f, 0.42f), OnUseSelectedItem, out detailUseBtnText);
        detailRepairButton = CreateActionBtn(detailPane.transform, "Btn_Repair", "CONSERTAR", new Vector2(0f, 146f), new Vector2(280f, 38f), new Color(0.72f, 0.44f, 0.14f), OnRepairSelected, out detailRepairText);
        detailHotbarButton = CreateActionBtn(detailPane.transform, "Btn_Hotbar", "ATRIBUIR ATALHO (1)", new Vector2(0f, 100f), new Vector2(280f, 38f), new Color(0.18f, 0.42f, 0.64f), OnAssignHotbarSelected, out detailHotbarText);
        detailSplitButton = CreateActionBtn(detailPane.transform, "Btn_Split", "DIVIDIR", new Vector2(-72f, 54f), new Vector2(134f, 36f), new Color(0.24f, 0.30f, 0.38f), OnSplitSelected, out _);
        detailSwapButton = CreateActionBtn(detailPane.transform, "Btn_Swap", "MOVER SLOT", new Vector2(72f, 54f), new Vector2(134f, 36f), new Color(0.24f, 0.34f, 0.44f), OnBeginSwapSelected, out detailSwapText);
        detailDropButton = CreateActionBtn(detailPane.transform, "Btn_Drop", "SOLTAR NO CHAO (1)", new Vector2(0f, 10f), new Vector2(280f, 34f), new Color(0.55f, 0.20f, 0.20f), OnDropSelected, out _);

        detailPane.SetActive(false);
    }

    private Text CreateAnchoredText(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Text t = obj.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
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

        textOut = CreateText(obj.transform, label, 12, FontStyle.Bold, Color.white);
        return btn;
    }

    private void OnSlotClicked(int visualIndex)
    {
        if (visualIndex < 0 || visualIndex >= displayedSlotIndices.Count) return;
        int realIdx = displayedSlotIndices[visualIndex];
        if (realIdx < 0) return;

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

        SelectSlot(realIdx);
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
        detailTitle.text = data.itemName;
        detailDesc.text = data.description;

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
        if (data.staminaRestore > 0f) statsStr += $"+{data.staminaRestore:F0} Estamina  ";
        if (data.sanityRestore != 0f) statsStr += $"{(data.sanityRestore > 0 ? "+" : "")}{data.sanityRestore:F0} Sanidade  ";
        if (data.warmthBonus > 0f) statsStr += $"+{data.warmthBonus:F1}C Calor";

        if (!data.isPerishable && data.maxDurability > 0f && slot.durability >= 0f)
        {
            string durLabel = data.equipSlot != EquipSlotType.None ? "Durabilidade" : "Golos / Cargas";
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
            detailUseBtnText.text = slot.durability > 0f ? "BEBER GOLO" : "CANTIL VAZIO";
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
        if (inventory.SplitSlot(selectedSlotIndex))
        {
            RefreshUI();
            SelectSlot(selectedSlotIndex);
        }
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
        inventory.DropItemToWorld(selectedSlotIndex, 1);
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
        sbRT.sizeDelta = new Vector2(0f, 44f);

        stationFilterButtons.Clear();
        stationStatusDots.Clear();

        AddStationFilterBtn(stationBar.transform, "TODAS", null, 0f, 102f);
        AddStationFilterBtn(stationBar.transform, "MAOS", CraftingStationType.Hand, 110f, 126f);
        AddStationFilterBtn(stationBar.transform, "FOGUEIRA", CraftingStationType.Bonfire, 244f, 146f);
        AddStationFilterBtn(stationBar.transform, "BANCADA", CraftingStationType.Workbench, 398f, 146f);
        AddStationFilterBtn(stationBar.transform, "CALDEIRAO", CraftingStationType.Cauldron, 552f, 156f);

        // Botao Filtro "SO FABRICAVEIS"
        GameObject onlyBtnObj = new GameObject("Btn_OnlyCraftable");
        onlyBtnObj.transform.SetParent(stationBar.transform, false);
        RectTransform obRT = onlyBtnObj.AddComponent<RectTransform>();
        obRT.anchorMin = new Vector2(1f, 0.5f);
        obRT.anchorMax = new Vector2(1f, 0.5f);
        obRT.pivot = new Vector2(1f, 0.5f);
        obRT.anchoredPosition = new Vector2(0f, 0f);
        obRT.sizeDelta = new Vector2(198f, 38f);
        onlyCraftableBtnBg = onlyBtnObj.AddComponent<Image>();
        onlyCraftableBtnBg.sprite = FragaProIconLibrary.GetPillSprite();
        onlyCraftableBtnBg.type = Image.Type.Sliced;
        onlyCraftableBtnBg.color = new Color(0.14f, 0.22f, 0.30f, 0.90f);
        onlyBtnObj.AddComponent<Button>().onClick.AddListener(OnToggleOnlyCraftableClicked);
        onlyCraftableBtnText = CreateText(onlyBtnObj.transform, "SO DISPONIVEIS: OFF", 11, FontStyle.Bold, Color.white);

        // Barra de Progresso de Fabricacao
        GameObject progBg = new GameObject("CraftProgressBG");
        progBg.transform.SetParent(parent, false);
        RectTransform pbRT = progBg.AddComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0f, 1f);
        pbRT.anchorMax = new Vector2(1f, 1f);
        pbRT.pivot = new Vector2(0.5f, 1f);
        pbRT.anchoredPosition = new Vector2(0f, -50f);
        pbRT.sizeDelta = new Vector2(0f, 6f);
        progBg.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.16f, 0.85f);

        GameObject progFill = new GameObject("CraftProgressFill");
        progFill.transform.SetParent(progBg.transform, false);
        RectTransform pfRT = progFill.AddComponent<RectTransform>();
        pfRT.anchorMin = Vector2.zero;
        pfRT.anchorMax = Vector2.one;
        pfRT.offsetMin = Vector2.zero;
        pfRT.offsetMax = Vector2.zero;
        craftProgressFill = progFill.AddComponent<Image>();
        craftProgressFill.type = Image.Type.Filled;
        craftProgressFill.fillMethod = Image.FillMethod.Horizontal;
        craftProgressFill.fillAmount = 0f;
        craftProgressFill.color = new Color(0.32f, 0.90f, 0.56f, 1f);

        GameObject scrollObj = new GameObject("CraftScroll");
        scrollObj.transform.SetParent(parent, false);
        RectTransform scRT = scrollObj.AddComponent<RectTransform>();
        scRT.anchorMin = Vector2.zero;
        scRT.anchorMax = Vector2.one;
        scRT.offsetMin = Vector2.zero;
        scRT.offsetMax = new Vector2(0f, -64f);

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 36f;

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

        Text txt = CreateText(obj.transform, label, 12, FontStyle.Bold, Color.white);
        txt.rectTransform.offsetMin = new Vector2(station.HasValue ? 24f : 6f, 0f);
    }

    private void RebuildCraftCards()
    {
        foreach (GameObject c in recipeCards)
        {
            if (c != null) Destroy(c);
        }
        recipeCards.Clear();

        if (craftingSystem == null) craftingSystem = CraftingSystem.Instance ?? FindAnyObjectByType<CraftingSystem>();
        if (craftingSystem == null || craftContentRT == null) return;

        List<CraftRecipe> all = craftingSystem.GetAllRecipes();
        int visibleIdx = 0;
        int cols = 3;
        float cardW = 342f;
        float cardH = 182f;
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
            bg.color = new Color(0.11f, 0.16f, 0.22f, 0.92f);

            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.24f, 0.56f, 0.78f, 0.35f);

            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(card.transform, false);
            RectTransform iRT = icoObj.AddComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0f, 1f);
            iRT.anchorMax = new Vector2(0f, 1f);
            iRT.pivot = new Vector2(0f, 1f);
            iRT.anchoredPosition = new Vector2(10f, -10f);
            iRT.sizeDelta = new Vector2(44f, 44f);
            Image icoImg = icoObj.AddComponent<Image>();
            icoImg.sprite = FragaProIconLibrary.GetItemIcon(rec.outputItemId);
            icoImg.preserveAspect = true;
            icoImg.raycastTarget = false;

            Text titleTxt = CreateAnchoredText(card.transform, "Title", new Vector2(30f, -8f), new Vector2(-66f, 22f), 14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleTxt.rectTransform.offsetMin = new Vector2(62f, 0f);
            titleTxt.text = rec.recipeName;

            Text stTxt = CreateAnchoredText(card.transform, "Station", new Vector2(30f, -29f), new Vector2(-66f, 16f), 10, FontStyle.Bold, new Color(0.65f, 0.85f, 0.96f), TextAnchor.MiddleLeft);
            stTxt.rectTransform.offsetMin = new Vector2(62f, 0f);
            stTxt.text = GetStationLabel(rec.requiredStation);

            ItemData outData = ItemDatabase.GetItem(rec.outputItemId);
            Text effTxt = CreateAnchoredText(card.transform, "Effect", new Vector2(30f, -45f), new Vector2(-66f, 16f), 10, FontStyle.Normal, new Color(0.55f, 0.94f, 0.78f), TextAnchor.MiddleLeft);
            effTxt.rectTransform.offsetMin = new Vector2(62f, 0f);
            effTxt.text = GetRecipeBenefitPreview(outData);

            GameObject reqObj = new GameObject("Ingredients");
            reqObj.transform.SetParent(card.transform, false);
            RectTransform reqRT = reqObj.AddComponent<RectTransform>();
            reqRT.anchorMin = Vector2.zero;
            reqRT.anchorMax = Vector2.one;
            reqRT.offsetMin = new Vector2(12f, 44f);
            reqRT.offsetMax = new Vector2(-12f, -64f);
            Text reqTxt = reqObj.AddComponent<Text>();
            reqTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            reqTxt.fontSize = 11;
            reqTxt.supportRichText = true;
            reqTxt.color = Color.white;

            int recipeIndex = rIdx;
            bool allowBatch5 = outData == null || outData.maxStack > 1;
            CreateCraftActionButton(card.transform, "Btn_Craft1", "FABRICAR",
                new Vector2(10f, 8f), new Vector2(allowBatch5 ? 214f : 322f, 32f), () => ExecuteCraft(recipeIndex, 1));
            Button btn5 = CreateCraftActionButton(card.transform, "Btn_Craft5", "x5",
                new Vector2(232f, 8f), new Vector2(98f, 32f), () => ExecuteCraft(recipeIndex, 5));
            btn5.gameObject.SetActive(allowBatch5);

            recipeCards.Add(card);
        }

        int totalRows = Mathf.CeilToInt((float)visibleIdx / cols);
        craftContentRT.sizeDelta = new Vector2(0f, Mathf.Max(420f, totalRows * (cardH + gapY) + 20f));
    }

    private Button CreateCraftActionButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = obj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetPillSprite();
        img.type = Image.Type.Sliced;
        img.color = new Color(0.18f, 0.58f, 0.42f, 0.95f);
        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        CreateText(obj.transform, label, 12, FontStyle.Bold, Color.white);
        return btn;
    }

    private void ExecuteCraft(int recipeIndex, int batchCount)
    {
        if (craftingSystem == null) return;
        CraftRecipe rec = craftingSystem.GetRecipe(recipeIndex);
        if (rec == null) return;

        if (!craftingSystem.CanCraft(recipeIndex, out string reason))
        {
            if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(reason);
            return;
        }

        if (craftingSystem.Craft(recipeIndex, batchCount))
        {
            craftProgressDuration = rec.craftDuration;
            craftProgressTimer = rec.craftDuration;
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Fabricado: {rec.recipeName}");
            if (onlyCraftableFilter) RebuildCraftCards();
            RefreshUI();
        }
    }

    // =================== ATUALIZACAO ===================

    public void RefreshUI()
    {
        if (inventory == null) return;

        if (tabInvText != null)
            tabInvText.text = $"MOCHILA ({inventory.GetOccupiedSlotCount()}/{inventory.maxSlots})";

        // 1. Paperdoll & Protecao
        RefreshGearCard(mainHandGearIcon, mainHandGearText, inventory.equippedMainHandId, "DESARMADO", FragaProIconLibrary.ControlIcon.AttackFist);
        RefreshGearCard(offHandGearIcon, offHandGearText, inventory.equippedOffHandId, "VAZIO", FragaProIconLibrary.ControlIcon.ShieldBlock);
        RefreshGearCard(armorGearIcon, armorGearText, inventory.equippedArmorId, "SEM PELE", FragaProIconLibrary.ControlIcon.Backpack);

        if (gearStatsSummaryText != null)
        {
            ItemData armorData = inventory.equippedArmorId > 0 ? ItemDatabase.GetItem(inventory.equippedArmorId) : null;
            float defPct = armorData != null ? armorData.armorDefense * 100f : 0f;
            float insPct = armorData != null ? armorData.coldInsulation * 100f : 0f;
            float tempC = BodyTemperatureSystem.Instance != null ? BodyTemperatureSystem.Instance.currentTemperature : 36.6f;
            gearStatsSummaryText.text = $"DEFESA: {defPct:F0}%   |   ISOLAMENTO TERMICO: {insPct:F0}%   |   TEMP CORPORAL: {tempC:F1} C";
        }

        // 2. Grid de Slots (filtrado)
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

        Sprite emptyColorSprite = null;
        for (int v = 0; v < slotObjects.Count; v++)
        {
            GameObject slotObj = slotObjects[v];
            Image slotBg = slotObj.GetComponent<Image>();
            Image icoImg = slotObj.transform.Find("Icon").GetComponent<Image>();
            Text nameTxt = slotObj.transform.Find("Name").GetComponent<Text>();
            Text badgeTxt = slotObj.transform.Find("Quantity").GetComponent<Text>();
            Text tagTxt = slotObj.transform.Find("Tag").GetComponent<Text>();
            Transform durBg = slotObj.transform.Find("DurBG");
            Image durFill = durBg.Find("DurFill").GetComponent<Image>();

            if (v >= displayedSlotIndices.Count)
            {
                icoImg.enabled = false;
                nameTxt.text = "";
                badgeTxt.text = "";
                tagTxt.text = "";
                durBg.gameObject.SetActive(false);
                slotBg.color = new Color(0.08f, 0.11f, 0.15f, 0.45f);
                continue;
            }

            int realIdx = displayedSlotIndices[v];
            InventorySlot slot = inventory.slots[realIdx];
            bool isSelected = realIdx == selectedSlotIndex;
            slotBg.color = isSelected ? new Color(0.18f, 0.34f, 0.46f, 0.96f) : new Color(0.11f, 0.16f, 0.22f, 0.90f);

            if (slot.IsEmpty())
            {
                icoImg.sprite = emptyColorSprite;
                icoImg.enabled = false;
                nameTxt.text = "Vazio";
                badgeTxt.text = "";
                tagTxt.text = "";
                durBg.gameObject.SetActive(false);
            }
            else
            {
                ItemData data = ItemDatabase.GetItem(slot.itemId);
                icoImg.enabled = true;
                icoImg.sprite = FragaProIconLibrary.GetItemIcon(slot.itemId);
                nameTxt.text = data != null ? data.itemName : $"Item {slot.itemId}";
                badgeTxt.text = slot.quantity > 1 ? $"x{slot.quantity}" : "";

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
                    durBg.gameObject.SetActive(true);
                    durFill.fillAmount = frac;
                    durFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.35f, 0.92f, 0.45f), frac);
                    tagTxt.text = baseTag;
                }
                else if (data != null && !data.isPerishable && data.maxDurability > 0f && slot.durability >= 0f)
                {
                    float frac = Mathf.Clamp01(slot.durability / data.maxDurability);
                    durBg.gameObject.SetActive(true);
                    durFill.fillAmount = frac;
                    durFill.color = frac > 0f ? new Color(0.24f, 0.78f, 0.98f) : new Color(0.75f, 0.28f, 0.22f);
                    string charges = $"{Mathf.RoundToInt(slot.durability)}/{Mathf.RoundToInt(data.maxDurability)}";
                    tagTxt.text = string.IsNullOrEmpty(baseTag) ? charges : $"{baseTag} {charges}";
                }
                else
                {
                    durBg.gameObject.SetActive(false);
                    tagTxt.text = baseTag;
                }
            }
        }

        // 3. Status das estacoes de crafting + cards
        if (craftingSystem != null)
        {
            CraftingStationType?[] stationMap = { null, CraftingStationType.Hand, CraftingStationType.Bonfire, CraftingStationType.Workbench, CraftingStationType.Cauldron };
            for (int i = 0; i < stationStatusDots.Count && i < stationMap.Length; i++)
            {
                if (stationStatusDots[i] != null && stationMap[i].HasValue)
                {
                    bool near = craftingSystem.IsNearStation(stationMap[i].Value);
                    stationStatusDots[i].color = near ? new Color(0.32f, 0.95f, 0.52f) : new Color(0.92f, 0.62f, 0.22f);
                }
            }

            foreach (GameObject card in recipeCards)
            {
                if (card == null) continue;
                int rIdx = int.Parse(card.name.Replace("RecipeCard_", ""));
                CraftRecipe rec = craftingSystem.GetRecipe(rIdx);
                if (rec == null) continue;

                Text reqTxt = card.transform.Find("Ingredients").GetComponent<Text>();
                Button btn1 = card.transform.Find("Btn_Craft1").GetComponent<Button>();
                Button btn5 = card.transform.Find("Btn_Craft5").GetComponent<Button>();

                string reqStr = "";
                for (int j = 0; j < rec.ingredientIds.Length; j++)
                {
                    int have = inventory.GetItemCount(rec.ingredientIds[j]);
                    int need = rec.ingredientAmounts[j];
                    string ingName = GetItemName(rec.ingredientIds[j]);
                    reqStr += have >= need
                        ? $"<color=#66FF99>{ingName}: {have}/{need}</color>\n"
                        : $"<color=#FF6666>{ingName}: {have}/{need}</color>\n";
                }

                bool canCraft = craftingSystem.CanCraft(rIdx, out string reason);
                if (!canCraft && reason.StartsWith("Requer"))
                {
                    reqStr += $"<color=#FFBB44>{reason}</color>";
                }

                reqTxt.text = reqStr;
                btn1.interactable = canCraft;
                btn1.GetComponent<Image>().color = canCraft ? new Color(0.18f, 0.64f, 0.44f, 0.96f) : new Color(0.24f, 0.28f, 0.34f, 0.55f);
                btn5.interactable = canCraft;
                btn5.GetComponent<Image>().color = canCraft ? new Color(0.20f, 0.52f, 0.68f, 0.96f) : new Color(0.24f, 0.28f, 0.34f, 0.55f);
            }
        }
    }

    private void RefreshGearCard(Image ico, Text txt, int itemId, string emptyLabel, FragaProIconLibrary.ControlIcon fallbackIcon)
    {
        if (ico == null || txt == null) return;
        if (itemId > 0)
        {
            ItemData d = ItemDatabase.GetItem(itemId);
            ico.sprite = FragaProIconLibrary.GetItemIcon(itemId);
            txt.text = d != null ? d.itemName : $"Item {itemId}";
        }
        else
        {
            ico.sprite = FragaProIconLibrary.GetControlIcon(fallbackIcon);
            txt.text = emptyLabel;
        }
    }

    private static string GetRecipeBenefitPreview(ItemData d)
    {
        if (d == null) return "";
        if (d.equipSlot != EquipSlotType.None)
        {
            if (d.armorDefense > 0f) return $"Defesa +{d.armorDefense * 100f:F0}% | Frio +{d.coldInsulation * 100f:F0}%";
            if (d.harvestTreeMultiplier > 1.5f) return $"Dano {d.attackDamage:F0} | Corte x{d.harvestTreeMultiplier:F1}";
            if (d.harvestRockMultiplier > 1.5f) return $"Dano {d.attackDamage:F0} | Mineracao x{d.harvestRockMultiplier:F1}";
            return $"Dano {d.attackDamage:F0} | Alcance {d.attackRange:F1}m";
        }
        if (d.type == ItemType.Consumable)
        {
            string s = "";
            if (d.hungerRestore > 0f) s += $"+{d.hungerRestore:F0} Fome ";
            if (d.thirstRestore > 0f) s += $"+{d.thirstRestore:F0} Sede ";
            if (d.healthRestore > 0f) s += $"+{d.healthRestore:F0} Vida ";
            if (d.staminaRestore > 0f) s += $"+{d.staminaRestore:F0} Estam ";
            if (d.warmthBonus > 0f) s += $"+{d.warmthBonus:F1}C ";
            if (d.id == 210) s += "(3 Golos)";
            if (!d.isPerishable && d.id == 212) s += "(Nao estraga)";
            return string.IsNullOrEmpty(s) ? d.description : s.Trim();
        }
        return d.description.Length > 42 ? d.description.Substring(0, 40) + "..." : d.description;
    }

    private static string GetStationLabel(CraftingStationType s)
    {
        switch (s)
        {
            case CraftingStationType.Hand: return "[T0 Primitivo] Maos Livres";
            case CraftingStationType.Bonfire: return "[T1 Fogueira] Fogo / Cozinha";
            case CraftingStationType.Workbench: return "[T2 Bancada] Pedra / Osso / Ferro";
            case CraftingStationType.Cauldron: return "[T3 Caldeirao] Alquimia / Elixires";
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
