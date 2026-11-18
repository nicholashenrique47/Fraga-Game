using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// HUDSurvivalOverlay: HUD AAA de Sobrevivencia Responsivo (Zero-Emoji, Icones Vetoriais Profissionais).
/// Inclui anel de vida/estamina, 4 micro-dials (Fome, Sede, Sanidade, Temperatura),
/// Barra Ao Vivo de Recursos na Mochila, Feed Empilhado de Coleta de Itens,
/// Cartao de Construcao Ativa acima da Hotbar, Relogio sempre visivel e adaptacao Mobile <-> Desktop.
/// </summary>
public class HUDSurvivalOverlay : MonoBehaviour
{
    public static HUDSurvivalOverlay Instance { get; private set; }

    public enum CrosshairContext
    {
        Default,
        Tree,
        Rock,
        Interactable,
        Hostile
    }

    [Header("Dependencies")]
    public PlayerStatus playerStatus;
    public SanitySystem sanitySystem;
    public DayNightCycle dayNightCycle;
    public PlayerController playerController;
    public EquipmentSystem equipmentSystem;

    [Header("Config")]
    public float updateInterval = 0.05f;

    private RectTransform hudRoot;
    private RectTransform vitalsContainerRT;
    private readonly List<RectTransform> statusIconRTs = new List<RectTransform>();
    private CanvasGroup statusGroup;
    private static Sprite s_outerRingSprite;
    private static Sprite s_innerRingSprite;
    private static Sprite s_coreDiskSprite;
    private static Sprite s_microRingSprite;

    private Image healthBar;
    private Image sanityBar;
    private Image hungerBar;
    private Image thirstBar;
    private Image staminaBar;
    private Image temperatureBar;

    private Text healthText;
    private Text sanityText;
    private Text hungerText;
    private Text thirstText;
    private Text staminaText;
    private Text temperatureText;
    private Text statusHeaderBadgeText;
    private Outline vitalsCardOutline;
    private readonly Image[] dialLinearFills = new Image[4];
    private readonly Color[] dialBaseColors = new Color[4];

    private Image iconPoison;
    private Image iconBleeding;
    private Image iconFracture;
    private Image iconHypothermia;
    private Image iconSickness;

    private RectTransform compassContainer;
    private Text compassText;
    private readonly StringBuilder compassSb = new StringBuilder(64);

    // Barra de Chefe (Boss Health Bar)
    private GameObject bossBarRootObj;
    private Image bossHpFill;
    private Text bossTitleText;
    private Text bossHpText;

    private Text clockText;
    private Text dayCounterText;

    private Image damageTop;
    private Image damageBottom;
    private Image damageLeft;
    private Image damageRight;
    private float[] damageAlphas = new float[4];

    private Text lootTooltipText;
    private CanvasGroup lootTooltipGroup;
    private float tooltipFadeTimer;

    // Feed Empilhado de Coleta de Itens (Zero-GC, 5 linhas pre-alocadas)
    private struct CollectionFeedRow
    {
        public GameObject root;
        public CanvasGroup cg;
        public Image icon;
        public Text text;
        public int itemId;
        public int accumulatedAdded;
        public int lastTotalCount;
        public float timer;
    }

    private const int COLLECTION_FEED_SLOTS = 5;
    private RectTransform collectionFeedRootRT;
    private readonly CollectionFeedRow[] collectionFeedRows = new CollectionFeedRow[COLLECTION_FEED_SLOTS];

    private RectTransform weaponIndicatorRT;
    private Image weaponIconImg;
    private Text weaponText;
    private int lastWeaponShownId = int.MinValue;
    private int lastWeaponDurPct = -2;
    private int lastWeaponAmmo = -2;
    private bool lastWeaponLayoutTouch = false;
    private GameObject desktopHintObj;

    private Image bonfireIndicator;
    private GameObject stealthIndicatorObj;
    private Text stealthIndicatorText;

    private Image crosshairDot;
    private Image crosshairRing;
    private RectTransform crosshairRingRT;
    private Image crosshairHoldRing;
    private RectTransform crosshairHoldRingRT;
    private CanvasGroup hitMarkerGroup;
    private RectTransform hitMarkerRT;
    private readonly Image[] hitMarkerTicks = new Image[4];
    private float hitMarkerTimer = 0f;
    private Color hitMarkerColor = Color.white;
    private Color targetCrosshairColor = new Color(1f, 1f, 1f, 0.58f);
    private Color targetRingColor = new Color(1f, 1f, 1f, 0f);
    private float targetRingScale = 1.0f;

    // Pilula de Interacao Contextual (clicavel no PC/Mobile)
    private GameObject interactPromptObj;
    private RectTransform interactPromptRT;
    private Image interactPromptIcon;
    private Text interactPromptText;
    private Text interactKeyBadgeText;
    private RectTransform interactKeyBadgeRT;
    private Image interactKeyBadgeBg;
    private Image interactHoldFillBar;
    private string cachedBasePromptLabel = string.Empty;
    private bool wasShowingHoldAction = false;

    // Cartao de Proximidade para Construcao Ativa (acima da Hotbar)
    private GameObject blueprintCardObj;
    private Image blueprintCardIcon;
    private Text blueprintCardTitleText;
    private Text blueprintCardMaterialsText;
    private Image blueprintCardProgressFill;
    private Text blueprintCardProgressText;
    private Text blueprintDepositBtnLabel;
    private Text blueprintCancelBtnLabel;

    // Cartao Diegetico de Fogueira Proxima (grelha 3 slots + lenha + acoes)
    private GameObject bonfireCardObj;
    private RectTransform bonfireCardRT;
    private Text bonfireCardTitleText;
    private Image bonfireFuelFill;
    private Text bonfireSlotsText;
    private Text bonfirePrimaryBtnLabel;
    private Image bonfirePrimaryBtnBg;
    private Text bonfireWoodBtnLabel;
    private Text bonfireRecipesBtnLabel;
    private readonly StringBuilder bonfireSlotsSb = new StringBuilder(128);

    // Pilula HUD de Fila de Fabricacao em Andamento (mochila fechada)
    private GameObject craftQueueHudObj;
    private RectTransform craftQueueHudRT;
    private Image craftQueueHudFill;
    private Text craftQueueHudText;
    private string lastCraftHudSignature = string.Empty;

    // Rastreador de Objetivos Iniciais
    private GameObject objectiveRootObj;
    private Text objectiveTitleText;
    private Text objectiveDetailText;

    // Rastreador de Projeto Fixado do Livro de Construcao (Zero-GC)
    private GameObject pinnedBlueprintObj;
    private Text pinnedBlueprintTitleText;
    private Text pinnedBlueprintMatsText;
    private Image pinnedBlueprintFill;
    private int pinnedProjectIndex = -1;
    private string pinnedProjectName = string.Empty;
    private int pinnedReqWood = 0;
    private int pinnedReqStone = 0;
    private int lastPinnedWood = -1;
    private int lastPinnedStone = -1;

    // Barra Ao Vivo de Recursos na Mochila (Madeira, Pedra, Ervas, Corda)
    private RectTransform resourceCounterBarRT;
    private readonly Text[] resourceCountTexts = new Text[4];
    private readonly int[] trackedResourceIds = { 100, 101, 103, 118 };
    private readonly string[] trackedResourceNames = { "MADEIRA", "PEDRA", "ERVAS", "CORDA" };
    private readonly int[] lastTrackedCounts = { -1, -1, -1, -1 };

    // Faixa Co-op do Esquadrao (Zero-GC, 3 slots remotos pre-alocados)
    private struct CoopHudMemberRow
    {
        public GameObject root;
        public Image accentBar;
        public Text nameDistText;
        public Image hpFill;
        public Text alertText;
    }

    private GameObject coopPartyHudObj;
    private Text coopPartyHeaderText;
    private readonly CoopHudMemberRow[] coopHudRows = new CoopHudMemberRow[FragaCoopSession.MaxPartySize - 1];
    private float nextCoopHudRefreshTime = 0f;

    private InventoryAdvanced inventoryCache;
    private InventoryAdvanced subscribedInventory;
    private float nextUpdateTime;
    private float lastHealth = 100f;
    private float lastSanity = 100f;
    private bool layoutInitialized = false;
    private bool lastTouchLayoutActive = false;

    /// <summary>
    /// Fonte unica autoritativa do Canvas de gameplay (ignora MainMenu_Canvas e Debug_Canvas).
    /// </summary>
    public static Canvas GetGameplayCanvas()
    {
        if (Instance != null)
        {
            Canvas own = Instance.GetComponentInParent<Canvas>();
            if (IsValidGameplayCanvas(own)) return own;
        }

        UIManager uiMgr = FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (uiMgr != null)
        {
            Canvas c = uiMgr.GetComponentInParent<Canvas>();
            if (IsValidGameplayCanvas(c)) return c;
        }

        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < allCanvases.Length; i++)
        {
            Canvas c = allCanvases[i];
            if (IsValidGameplayCanvas(c) && c.isRootCanvas && c.gameObject.name.Contains("Mobile"))
            {
                return c;
            }
        }
        for (int i = 0; i < allCanvases.Length; i++)
        {
            Canvas c = allCanvases[i];
            if (IsValidGameplayCanvas(c) && c.isRootCanvas)
            {
                return c;
            }
        }

        GameObject canvasObj = new GameObject("UI_HUD_Canvas");
        Canvas newCanvas = canvasObj.AddComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        newCanvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 2.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        return newCanvas;
    }

    private static bool IsValidGameplayCanvas(Canvas c)
    {
        if (c == null) return false;
        if (c.GetComponent<MainMenuManager>() != null) return false;
        string n = c.gameObject.name;
        if (n.Contains("MainMenu") || n.Contains("Debug")) return false;
        return true;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        CreateHUDHierarchy();
        CreateStatusBars();
        CreateStatusEffectIcons();
        CreateCompass();
        CreateBossHealthBar();
        CreateClock();
        CreateDamageIndicators();
        CreateLootTooltip();
        CreateWeaponIndicator();
        CreateDesktopControlsHint();
        CreateBonfireIndicator();
        CreateStealthIndicator();
        CreateCrosshair();
        CreateInteractionPrompt();
        CreateBlueprintSiteCard();
        CreateBonfireProximityCard();
        CreateCraftQueueHUD();
        CreateObjectiveTracker();
        CreateResourceCounterBar();
        CreatePinnedBlueprintTracker();
        CreateItemCollectionFeed();
        CreateCoopPartyHUD();
    }

    private void Start()
    {
        ForceRefreshNow();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (subscribedInventory != null)
        {
            subscribedInventory.OnEquipmentChanged -= RefreshWeaponIndicatorFromInventory;
            subscribedInventory.OnInventoryChanged -= RefreshResourceCounterBar;
            subscribedInventory.OnItemAdded -= OnInventoryItemAdded;
            subscribedInventory = null;
        }
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.OnTouchLayoutChanged -= OnTouchLayoutChanged;
        }
    }

    private void EnsureInventorySubscribed()
    {
        if (inventoryCache == null)
        {
            inventoryCache = InventoryAdvanced.Instance != null ? InventoryAdvanced.Instance : FindAnyObjectByType<InventoryAdvanced>();
        }
        if (inventoryCache != null && subscribedInventory != inventoryCache)
        {
            if (subscribedInventory != null)
            {
                subscribedInventory.OnEquipmentChanged -= RefreshWeaponIndicatorFromInventory;
                subscribedInventory.OnInventoryChanged -= RefreshResourceCounterBar;
                subscribedInventory.OnItemAdded -= OnInventoryItemAdded;
            }
            subscribedInventory = inventoryCache;
            subscribedInventory.OnEquipmentChanged += RefreshWeaponIndicatorFromInventory;
            subscribedInventory.OnInventoryChanged += RefreshResourceCounterBar;
            subscribedInventory.OnItemAdded += OnInventoryItemAdded;
        }
    }

    public void ForceRefreshNow()
    {
        FindMissingReferences();
        EnsureInventorySubscribed();

        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.OnTouchLayoutChanged -= OnTouchLayoutChanged;
            FragaInputRouter.Instance.OnTouchLayoutChanged += OnTouchLayoutChanged;
        }

        if (inventoryCache != null)
        {
            RefreshWeaponIndicatorFromInventory();
            RefreshResourceCounterBar();
        }
        UpdateClock();
        UpdateCompass();
        UpdateBlueprintCard();
        UpdateBonfireCard();
        UpdateCraftQueueHUD();
        SyncAdaptivePositions(true);
    }

    private void OnInventoryItemAdded(int itemId, int addedAmount, int totalCount)
    {
        ShowItemCollected(itemId, addedAmount, totalCount);
    }

    private void OnTouchLayoutChanged(bool touchActive)
    {
        layoutInitialized = true;
        lastTouchLayoutActive = touchActive;
        ApplyAdaptivePositions(touchActive);
    }

    private void SyncAdaptivePositions(bool force)
    {
        bool touchActive = FragaInputRouter.Instance != null
            ? FragaInputRouter.Instance.IsTouchControlsActive
            : (Application.isMobilePlatform || Input.touchCount > 0);
        if (!force && layoutInitialized && touchActive == lastTouchLayoutActive) return;

        layoutInitialized = true;
        lastTouchLayoutActive = touchActive;
        ApplyAdaptivePositions(touchActive);
    }

    private void ApplyAdaptivePositions(bool touchActive)
    {
        if (vitalsContainerRT != null)
        {
            vitalsContainerRT.anchoredPosition = touchActive
                ? new Vector2(40f, 298f)
                : new Vector2(32f, 30f);
        }

        float startX = touchActive ? 40f : 32f;
        float startY = touchActive ? 470f : 200f;
        float spacing = 84f;
        for (int i = 0; i < statusIconRTs.Count; i++)
        {
            if (statusIconRTs[i] != null)
            {
                statusIconRTs[i].anchoredPosition = new Vector2(startX + (i * spacing), startY);
            }
        }

        if (desktopHintObj != null)
        {
            desktopHintObj.SetActive(!touchActive);
        }
    }

    private void FindMissingReferences()
    {
        if (playerStatus == null) playerStatus = PlayerStatus.Instance != null ? PlayerStatus.Instance : FindAnyObjectByType<PlayerStatus>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (dayNightCycle == null) dayNightCycle = DayNightCycle.Instance != null ? DayNightCycle.Instance : FindAnyObjectByType<DayNightCycle>();
        if (playerController == null) playerController = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
        if (equipmentSystem == null) equipmentSystem = EquipmentSystem.Instance != null ? EquipmentSystem.Instance : FindAnyObjectByType<EquipmentSystem>();
        if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance != null ? InventoryAdvanced.Instance : FindAnyObjectByType<InventoryAdvanced>();
    }

    private void CreateHUDHierarchy()
    {
        Canvas targetCanvas = GetComponentInParent<Canvas>();
        if (!IsValidGameplayCanvas(targetCanvas))
        {
            targetCanvas = GetGameplayCanvas();
        }

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 2.5f;

        GameObject containerObj = new GameObject("HUD_SurvivalElements");
        containerObj.transform.SetParent(targetCanvas.transform, false);
        hudRoot = containerObj.AddComponent<RectTransform>();
        hudRoot.anchorMin = Vector2.zero;
        hudRoot.anchorMax = Vector2.one;
        hudRoot.pivot = new Vector2(0.5f, 0.5f);
        hudRoot.offsetMin = Vector2.zero;
        hudRoot.offsetMax = Vector2.zero;
    }

    private static void ConfigureHDText(Text text, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
    {
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = text.gameObject.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }

    private static Sprite GetRingSprite(int size, float rInNorm, float rOutNorm)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;
        float rIn = center * rInNorm;
        float rOut = center * rOutNorm;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = Mathf.SmoothStep(rIn - 1.2f, rIn, dist) * (1f - Mathf.SmoothStep(rOut - 1.2f, rOut, dist));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GetDiskSprite(int size, float radiusNorm)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;
        float r = center * radiusNorm;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = 1f - Mathf.SmoothStep(r - 1.2f, r, dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void CreateStatusBars()
    {
        Sprite barSprite = FragaProIconLibrary.GetBarFillSprite();
        Sprite pillSprite = FragaProIconLibrary.GetPillSprite();

        GameObject container = new GameObject("DiegeticVitalsWidget");
        container.transform.SetParent(hudRoot, false);
        RectTransform cRect = container.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0, 0);
        cRect.anchorMax = new Vector2(0, 0);
        cRect.pivot = new Vector2(0, 0);
        cRect.anchoredPosition = new Vector2(32f, 30f);
        cRect.sizeDelta = new Vector2(338f, 160f);
        vitalsContainerRT = cRect;

        Image cardBg = container.AddComponent<Image>();
        cardBg.sprite = FragaProIconLibrary.GetGlassPanelSprite();
        cardBg.type = Image.Type.Sliced;
        cardBg.color = new Color(0.04f, 0.07f, 0.11f, 0.90f);
        cardBg.raycastTarget = true;

        vitalsCardOutline = container.AddComponent<Outline>();
        vitalsCardOutline.effectColor = new Color(0.28f, 0.74f, 0.90f, 0.48f);
        vitalsCardOutline.effectDistance = new Vector2(1.2f, -1.2f);

        // Clique rapido no painel biometrico abre a aba Status [K]
        Button vitalsBtn = container.AddComponent<Button>();
        vitalsBtn.transition = Selectable.Transition.None;
        vitalsBtn.onClick.AddListener(() =>
        {
            if (SurvivalInventoryCraftUI.Instance != null)
                SurvivalInventoryCraftUI.Instance.OpenCharacterStatusTab();
        });

        statusGroup = container.AddComponent<CanvasGroup>();
        statusGroup.alpha = 0.92f;
        statusGroup.blocksRaycasts = true;

        // Cabecalho superior esquerdo + selo clicavel [K]
        GameObject titleObj = new GameObject("CardTitle");
        titleObj.transform.SetParent(container.transform, false);
        RectTransform ctRT = titleObj.AddComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(0f, 1f);
        ctRT.anchorMax = new Vector2(0f, 1f);
        ctRT.pivot = new Vector2(0f, 1f);
        ctRT.anchoredPosition = new Vector2(12f, -7f);
        ctRT.sizeDelta = new Vector2(150f, 18f);
        Text ctTxt = titleObj.AddComponent<Text>();
        ConfigureHDText(ctTxt, 10, TextAnchor.MiddleLeft, new Color(0.84f, 0.92f, 0.98f, 0.94f), FontStyle.Bold);
        ctTxt.text = "STATUS VITAL";

        GameObject badgeObj = new GameObject("BiometricHeaderPill");
        badgeObj.transform.SetParent(container.transform, false);
        RectTransform bRT = badgeObj.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(1f, 1f);
        bRT.anchorMax = new Vector2(1f, 1f);
        bRT.pivot = new Vector2(1f, 1f);
        bRT.anchoredPosition = new Vector2(-10f, -7f);
        bRT.sizeDelta = new Vector2(148f, 18f);
        Image bImg = badgeObj.AddComponent<Image>();
        bImg.sprite = pillSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.10f, 0.18f, 0.26f, 0.94f);
        bImg.raycastTarget = false;

        GameObject badgeTxtObj = new GameObject("Label");
        badgeTxtObj.transform.SetParent(badgeObj.transform, false);
        RectTransform btRT = badgeTxtObj.AddComponent<RectTransform>();
        btRT.anchorMin = Vector2.zero;
        btRT.anchorMax = Vector2.one;
        btRT.offsetMin = Vector2.zero;
        btRT.offsetMax = Vector2.zero;
        statusHeaderBadgeText = badgeTxtObj.AddComponent<Text>();
        ConfigureHDText(statusHeaderBadgeText, 9, TextAnchor.MiddleCenter, new Color(0.54f, 0.94f, 1.0f), FontStyle.Bold);
        statusHeaderBadgeText.text = "BIOMETRIA  [K]";

        GameObject hdrDiv = new GameObject("HeaderDivider");
        hdrDiv.transform.SetParent(container.transform, false);
        RectTransform hdRT = hdrDiv.AddComponent<RectTransform>();
        hdRT.anchorMin = new Vector2(0f, 1f);
        hdRT.anchorMax = new Vector2(1f, 1f);
        hdRT.pivot = new Vector2(0.5f, 1f);
        hdRT.offsetMin = new Vector2(12f, -28f);
        hdRT.offsetMax = new Vector2(-12f, -27f);
        Image hdImg = hdrDiv.AddComponent<Image>();
        hdImg.color = new Color(0.32f, 0.78f, 0.94f, 0.34f);
        hdImg.raycastTarget = false;

        // Barra Principal de Vida (Horizontal AAA nitida e legivel)
        GameObject hpRow = new GameObject("HealthBarRow");
        hpRow.transform.SetParent(container.transform, false);
        RectTransform hpRowRT = hpRow.AddComponent<RectTransform>();
        hpRowRT.anchorMin = new Vector2(0f, 1f);
        hpRowRT.anchorMax = new Vector2(1f, 1f);
        hpRowRT.pivot = new Vector2(0.5f, 1f);
        hpRowRT.offsetMin = new Vector2(12f, -68f);
        hpRowRT.offsetMax = new Vector2(-12f, -31f);

        GameObject heartBadge = new GameObject("HeartBadge");
        heartBadge.transform.SetParent(hpRow.transform, false);
        RectTransform hbRT = heartBadge.AddComponent<RectTransform>();
        hbRT.anchorMin = new Vector2(0f, 0.5f);
        hbRT.anchorMax = new Vector2(0f, 0.5f);
        hbRT.pivot = new Vector2(0f, 0.5f);
        hbRT.anchoredPosition = Vector2.zero;
        hbRT.sizeDelta = new Vector2(32f, 32f);
        Image hbImg = heartBadge.AddComponent<Image>();
        hbImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        hbImg.type = Image.Type.Sliced;
        hbImg.color = new Color(0.24f, 0.06f, 0.08f, 0.95f);
        hbImg.raycastTarget = false;

        GameObject heartObj = new GameObject("HeartIcon");
        heartObj.transform.SetParent(heartBadge.transform, false);
        RectTransform heartRT = heartObj.AddComponent<RectTransform>();
        heartRT.anchorMin = Vector2.zero;
        heartRT.anchorMax = Vector2.one;
        heartRT.offsetMin = new Vector2(5f, 5f);
        heartRT.offsetMax = new Vector2(-5f, -5f);
        Image heartImg = heartObj.AddComponent<Image>();
        heartImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.HeartPulse);
        heartImg.preserveAspect = true;
        heartImg.raycastTarget = false;

        GameObject hpLblObj = new GameObject("HealthLabel");
        hpLblObj.transform.SetParent(hpRow.transform, false);
        RectTransform hlRT = hpLblObj.AddComponent<RectTransform>();
        hlRT.anchorMin = new Vector2(0f, 0.54f);
        hlRT.anchorMax = new Vector2(0.55f, 1f);
        hlRT.offsetMin = new Vector2(40f, 0f);
        hlRT.offsetMax = Vector2.zero;
        Text hpTitle = hpLblObj.AddComponent<Text>();
        ConfigureHDText(hpTitle, 11, TextAnchor.MiddleLeft, new Color(1.0f, 0.72f, 0.72f), FontStyle.Bold);
        hpTitle.text = "VIDA";

        GameObject hpTextObj = new GameObject("HealthNum");
        hpTextObj.transform.SetParent(hpRow.transform, false);
        RectTransform hptRT = hpTextObj.AddComponent<RectTransform>();
        hptRT.anchorMin = new Vector2(0.45f, 0.52f);
        hptRT.anchorMax = new Vector2(1f, 1f);
        hptRT.offsetMin = Vector2.zero;
        hptRT.offsetMax = Vector2.zero;
        healthText = hpTextObj.AddComponent<Text>();
        ConfigureHDText(healthText, 14, TextAnchor.MiddleRight, new Color(0.98f, 0.98f, 0.98f, 1f), FontStyle.Bold);
        healthText.text = "100 / 100";

        GameObject hpBgObj = new GameObject("HealthBar_BG");
        hpBgObj.transform.SetParent(hpRow.transform, false);
        RectTransform hpBgRT = hpBgObj.AddComponent<RectTransform>();
        hpBgRT.anchorMin = new Vector2(0f, 0.04f);
        hpBgRT.anchorMax = new Vector2(1f, 0.46f);
        hpBgRT.offsetMin = new Vector2(40f, 0f);
        hpBgRT.offsetMax = Vector2.zero;
        Image hpBgImg = hpBgObj.AddComponent<Image>();
        hpBgImg.sprite = pillSprite;
        hpBgImg.type = Image.Type.Sliced;
        hpBgImg.color = new Color(0.16f, 0.04f, 0.06f, 0.94f);
        hpBgImg.raycastTarget = false;

        GameObject hpFillObj = new GameObject("HealthBar_Fill");
        hpFillObj.transform.SetParent(hpBgObj.transform, false);
        RectTransform hpFillRT = hpFillObj.AddComponent<RectTransform>();
        hpFillRT.anchorMin = Vector2.zero;
        hpFillRT.anchorMax = Vector2.one;
        hpFillRT.offsetMin = new Vector2(2f, 2f);
        hpFillRT.offsetMax = new Vector2(-2f, -2f);
        healthBar = hpFillObj.AddComponent<Image>();
        healthBar.sprite = barSprite;
        healthBar.color = new Color(0.92f, 0.22f, 0.24f, 1.0f);
        healthBar.type = Image.Type.Filled;
        healthBar.fillMethod = Image.FillMethod.Horizontal;
        healthBar.fillOrigin = 0;
        healthBar.fillAmount = 1f;
        healthBar.raycastTarget = false;

        // Barra Dedicada de Estamina / Folego
        GameObject stRow = new GameObject("StaminaBarRow");
        stRow.transform.SetParent(container.transform, false);
        RectTransform stRowRT = stRow.AddComponent<RectTransform>();
        stRowRT.anchorMin = new Vector2(0f, 1f);
        stRowRT.anchorMax = new Vector2(1f, 1f);
        stRowRT.pivot = new Vector2(0.5f, 1f);
        stRowRT.offsetMin = new Vector2(12f, -97f);
        stRowRT.offsetMax = new Vector2(-12f, -72f);

        GameObject stIcoObj = new GameObject("StaminaIcon");
        stIcoObj.transform.SetParent(stRow.transform, false);
        RectTransform stiRT = stIcoObj.AddComponent<RectTransform>();
        stiRT.anchorMin = new Vector2(0f, 0.5f);
        stiRT.anchorMax = new Vector2(0f, 0.5f);
        stiRT.pivot = new Vector2(0f, 0.5f);
        stiRT.anchoredPosition = new Vector2(4f, 0f);
        stiRT.sizeDelta = new Vector2(16f, 16f);
        Image stIcoImg = stIcoObj.AddComponent<Image>();
        stIcoImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.DodgeStep);
        stIcoImg.preserveAspect = true;
        stIcoImg.raycastTarget = false;

        GameObject stLblObj = new GameObject("StaminaLabel");
        stLblObj.transform.SetParent(stRow.transform, false);
        RectTransform stlRT = stLblObj.AddComponent<RectTransform>();
        stlRT.anchorMin = new Vector2(0f, 0.45f);
        stlRT.anchorMax = new Vector2(0.55f, 1f);
        stlRT.offsetMin = new Vector2(26f, 0f);
        stlRT.offsetMax = Vector2.zero;
        Text stTitle = stLblObj.AddComponent<Text>();
        ConfigureHDText(stTitle, 9, TextAnchor.MiddleLeft, new Color(0.58f, 0.92f, 1.0f), FontStyle.Bold);
        stTitle.text = "FOLEGO";

        GameObject stValObj = new GameObject("StaminaNum");
        stValObj.transform.SetParent(stRow.transform, false);
        RectTransform stvRT = stValObj.AddComponent<RectTransform>();
        stvRT.anchorMin = new Vector2(0.55f, 0.45f);
        stvRT.anchorMax = new Vector2(1f, 1f);
        stvRT.offsetMin = Vector2.zero;
        stvRT.offsetMax = Vector2.zero;
        staminaText = stValObj.AddComponent<Text>();
        ConfigureHDText(staminaText, 10, TextAnchor.MiddleRight, new Color(0.86f, 0.98f, 1.0f), FontStyle.Bold);
        staminaText.text = "100%";

        GameObject stBgObj = new GameObject("StaminaBar_BG");
        stBgObj.transform.SetParent(stRow.transform, false);
        RectTransform stBgRT = stBgObj.AddComponent<RectTransform>();
        stBgRT.anchorMin = new Vector2(0f, 0f);
        stBgRT.anchorMax = new Vector2(1f, 0.38f);
        stBgRT.offsetMin = new Vector2(26f, 0f);
        stBgRT.offsetMax = Vector2.zero;
        Image stBgImg = stBgObj.AddComponent<Image>();
        stBgImg.sprite = pillSprite;
        stBgImg.type = Image.Type.Sliced;
        stBgImg.color = new Color(0.05f, 0.14f, 0.18f, 0.94f);
        stBgImg.raycastTarget = false;

        GameObject stFillObj = new GameObject("StaminaBar_Fill");
        stFillObj.transform.SetParent(stBgObj.transform, false);
        RectTransform stFillRT = stFillObj.AddComponent<RectTransform>();
        stFillRT.anchorMin = Vector2.zero;
        stFillRT.anchorMax = Vector2.one;
        stFillRT.offsetMin = new Vector2(1f, 1f);
        stFillRT.offsetMax = new Vector2(-1f, -1f);
        staminaBar = stFillObj.AddComponent<Image>();
        staminaBar.sprite = barSprite;
        staminaBar.color = new Color(0.28f, 0.90f, 0.98f, 1.0f);
        staminaBar.type = Image.Type.Filled;
        staminaBar.fillMethod = Image.FillMethod.Horizontal;
        staminaBar.fillOrigin = 0;
        staminaBar.fillAmount = 1f;
        staminaBar.raycastTarget = false;

        // Grade 2x2 limpa para Fome, Sede, Sanidade e Temperatura
        GameObject dialsRack = new GameObject("MicroDialsRack");
        dialsRack.transform.SetParent(container.transform, false);
        RectTransform dRect = dialsRack.AddComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0f, 0f);
        dRect.anchorMax = new Vector2(1f, 0f);
        dRect.pivot = new Vector2(0.5f, 0f);
        dRect.offsetMin = new Vector2(12f, 8f);
        dRect.offsetMax = new Vector2(-12f, 58f);

        hungerBar = CreateMicroDial(dialsRack.transform, "FOME", FragaProIconLibrary.ControlIcon.HungerFork, new Color(0.95f, 0.65f, 0.20f), 0, out hungerText);
        thirstBar = CreateMicroDial(dialsRack.transform, "SEDE", FragaProIconLibrary.ControlIcon.WaterDrop, new Color(0.22f, 0.78f, 0.98f), 1, out thirstText);
        sanityBar = CreateMicroDial(dialsRack.transform, "MENTE", FragaProIconLibrary.ControlIcon.SanityEye, new Color(0.72f, 0.42f, 0.96f), 2, out sanityText);
        temperatureBar = CreateMicroDial(dialsRack.transform, "TEMP", FragaProIconLibrary.ControlIcon.Thermometer, new Color(1.0f, 0.52f, 0.28f), 3, out temperatureText);
    }

    private Image CreateMicroDial(Transform parent, string shortTitle, FragaProIconLibrary.ControlIcon iconType, Color dialColor, int index, out Text dialValText)
    {
        int col = index % 2;
        int row = index / 2;
        float xPos = col == 0 ? -78f : 78f;
        float yPos = row == 0 ? 12f : -13f;
        dialBaseColors[index] = dialColor;

        GameObject dialRoot = new GameObject($"Dial_{index}");
        dialRoot.transform.SetParent(parent, false);
        RectTransform rt = dialRoot.AddComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(xPos, yPos);
        rt.sizeDelta = new Vector2(150f, 22f);

        GameObject icoObj = new GameObject("CodeLabel");
        icoObj.transform.SetParent(dialRoot.transform, false);
        RectTransform icoRT = icoObj.AddComponent<RectTransform>();
        icoRT.anchorMin = new Vector2(0, 0.5f);
        icoRT.anchorMax = new Vector2(0, 0.5f);
        icoRT.pivot = new Vector2(0, 0.5f);
        icoRT.anchoredPosition = new Vector2(1f, 0f);
        icoRT.sizeDelta = new Vector2(15f, 15f);
        Image icoImg = icoObj.AddComponent<Image>();
        icoImg.sprite = FragaProIconLibrary.GetControlIcon(iconType);
        icoImg.preserveAspect = true;
        icoImg.raycastTarget = false;

        GameObject titleObj = new GameObject("NameLabel");
        titleObj.transform.SetParent(dialRoot.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0.45f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.offsetMin = new Vector2(20f, 0f);
        tRT.offsetMax = new Vector2(-42f, 0f);
        Text tTxt = titleObj.AddComponent<Text>();
        ConfigureHDText(tTxt, 9, TextAnchor.MiddleLeft, new Color(0.84f, 0.90f, 0.96f, 0.94f), FontStyle.Bold);
        tTxt.text = shortTitle;

        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(dialRoot.transform, false);
        RectTransform valRT = valObj.AddComponent<RectTransform>();
        valRT.anchorMin = new Vector2(1f, 0.45f);
        valRT.anchorMax = new Vector2(1f, 1f);
        valRT.pivot = new Vector2(1f, 0.5f);
        valRT.offsetMin = new Vector2(-44f, 0f);
        valRT.offsetMax = new Vector2(0f, 0f);
        dialValText = valObj.AddComponent<Text>();
        ConfigureHDText(dialValText, 10, TextAnchor.MiddleRight, new Color(0.98f, 0.98f, 0.98f, 0.98f), FontStyle.Bold);
        dialValText.text = "100%";

        GameObject barBg = new GameObject("LinearBG");
        barBg.transform.SetParent(dialRoot.transform, false);
        RectTransform lRT = barBg.AddComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0.04f);
        lRT.anchorMax = new Vector2(1f, 0.34f);
        lRT.offsetMin = new Vector2(20f, 0f);
        lRT.offsetMax = new Vector2(0f, 0f);
        Image lbImg = barBg.AddComponent<Image>();
        lbImg.color = new Color(0.08f, 0.12f, 0.16f, 0.94f);
        lbImg.raycastTarget = false;

        GameObject barFill = new GameObject("LinearFill");
        barFill.transform.SetParent(barBg.transform, false);
        RectTransform lfRT = barFill.AddComponent<RectTransform>();
        lfRT.anchorMin = Vector2.zero;
        lfRT.anchorMax = Vector2.one;
        lfRT.offsetMin = Vector2.zero;
        lfRT.offsetMax = Vector2.zero;
        Image lfImg = barFill.AddComponent<Image>();
        lfImg.sprite = FragaProIconLibrary.GetBarFillSprite();
        lfImg.type = Image.Type.Filled;
        lfImg.fillMethod = Image.FillMethod.Horizontal;
        lfImg.fillOrigin = 0;
        lfImg.fillAmount = 1f;
        lfImg.color = dialColor;
        lfImg.raycastTarget = false;
        dialLinearFills[index] = lfImg;

        return lfImg;
    }

    private void CreateStatusEffectIcons()
    {
        statusIconRTs.Clear();
        float startX = 32f;
        float startY = 194f;
        float spacing = 84f;

        iconPoison = CreateStatusIcon("Poison", new Color(0.25f, 0.88f, 0.25f), "VENENO", 0, startX, startY, spacing);
        iconBleeding = CreateStatusIcon("Bleeding", new Color(0.95f, 0.22f, 0.22f), "SANGUE", 1, startX, startY, spacing);
        iconFracture = CreateStatusIcon("Fracture", new Color(0.96f, 0.82f, 0.24f), "FRATURA", 2, startX, startY, spacing);
        iconHypothermia = CreateStatusIcon("Cold", new Color(0.42f, 0.80f, 1f), "HIPOTERM", 3, startX, startY, spacing);
        iconSickness = CreateStatusIcon("Sickness", new Color(0.68f, 0.88f, 0.24f), "DOENTE", 4, startX, startY, spacing);
    }

    private Image CreateStatusIcon(string name, Color color, string label, int index, float startX, float startY, float spacing)
    {
        GameObject iconObj = new GameObject($"StatusIcon_{name}");
        iconObj.transform.SetParent(hudRoot, false);
        RectTransform rect = iconObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0);
        rect.anchoredPosition = new Vector2(startX + (index * spacing), startY);
        rect.sizeDelta = new Vector2(78f, 24f);
        statusIconRTs.Add(rect);

        Image img = iconObj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetPillSprite();
        img.type = Image.Type.Sliced;
        img.color = new Color(color.r * 0.24f, color.g * 0.24f, color.b * 0.24f, 0.94f);
        img.raycastTarget = false;

        Outline outl = iconObj.AddComponent<Outline>();
        outl.effectColor = new Color(color.r, color.g, color.b, 0.88f);
        outl.effectDistance = new Vector2(1.1f, -1.1f);

        GameObject dotObj = new GameObject("Dot");
        dotObj.transform.SetParent(iconObj.transform, false);
        RectTransform dRT = dotObj.AddComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0f, 0.5f);
        dRT.anchorMax = new Vector2(0f, 0.5f);
        dRT.pivot = new Vector2(0f, 0.5f);
        dRT.anchoredPosition = new Vector2(7f, 0f);
        dRT.sizeDelta = new Vector2(8f, 8f);
        Image dotImg = dotObj.AddComponent<Image>();
        dotImg.sprite = FragaProIconLibrary.GetCircleBackground();
        dotImg.color = color;
        dotImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(iconObj.transform, false);
        RectTransform txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(16f, 0f);
        txtRect.offsetMax = new Vector2(-4f, 0f);

        Text txt = txtObj.AddComponent<Text>();
        txt.text = label;
        ConfigureHDText(txt, 9, TextAnchor.MiddleCenter, color, FontStyle.Bold);

        iconObj.SetActive(false);
        return img;
    }

    private void CreateCompass()
    {
        GameObject compassObj = new GameObject("Compass");
        compassObj.transform.SetParent(hudRoot, false);
        compassContainer = compassObj.AddComponent<RectTransform>();
        compassContainer.anchorMin = new Vector2(0.5f, 1);
        compassContainer.anchorMax = new Vector2(0.5f, 1);
        compassContainer.pivot = new Vector2(0.5f, 1);
        compassContainer.anchoredPosition = new Vector2(0, -15f);
        compassContainer.sizeDelta = new Vector2(360f, 26f);

        Image compassBg = compassObj.AddComponent<Image>();
        compassBg.sprite = FragaProIconLibrary.GetPillSprite();
        compassBg.type = Image.Type.Sliced;
        compassBg.color = new Color(0.04f, 0.06f, 0.08f, 0.65f);
        compassBg.raycastTarget = false;

        GameObject textObj = new GameObject("CompassText");
        textObj.transform.SetParent(compassObj.transform, false);
        RectTransform txtRect = textObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        compassText = textObj.AddComponent<Text>();
        ConfigureHDText(compassText, 15, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f, 0.95f), FontStyle.Bold);
        compassText.text = "[ N  0 ]";
    }

    private void CreateBossHealthBar()
    {
        bossBarRootObj = new GameObject("BossHealthBar");
        bossBarRootObj.transform.SetParent(hudRoot, false);
        RectTransform rt = bossBarRootObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -94f);
        rt.sizeDelta = new Vector2(460f, 34f);

        Image bg = bossBarRootObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.06f, 0.03f, 0.05f, 0.90f);
        bg.raycastTarget = false;

        Outline outline = bossBarRootObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.18f, 0.22f, 0.75f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        GameObject fillBgObj = new GameObject("HPTrack");
        fillBgObj.transform.SetParent(bossBarRootObj.transform, false);
        RectTransform fbRT = fillBgObj.AddComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(0f, 0f);
        fbRT.anchorMax = new Vector2(1f, 0f);
        fbRT.pivot = new Vector2(0.5f, 0f);
        fbRT.offsetMin = new Vector2(12f, 5f);
        fbRT.offsetMax = new Vector2(-12f, 13f);
        Image fbImg = fillBgObj.AddComponent<Image>();
        fbImg.color = new Color(0.18f, 0.05f, 0.07f, 0.92f);
        fbImg.raycastTarget = false;

        GameObject fillObj = new GameObject("HPFill");
        fillObj.transform.SetParent(fillBgObj.transform, false);
        RectTransform fRT = fillObj.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero;
        fRT.offsetMax = Vector2.zero;
        bossHpFill = fillObj.AddComponent<Image>();
        bossHpFill.sprite = FragaProIconLibrary.GetBarFillSprite();
        bossHpFill.color = new Color(0.82f, 0.14f, 0.20f, 0.96f);
        bossHpFill.type = Image.Type.Filled;
        bossHpFill.fillMethod = Image.FillMethod.Horizontal;
        bossHpFill.fillOrigin = 0;
        bossHpFill.fillAmount = 1f;
        bossHpFill.raycastTarget = false;

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(bossBarRootObj.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0.42f);
        tRT.anchorMax = new Vector2(0.70f, 1f);
        tRT.offsetMin = new Vector2(14f, 0f);
        tRT.offsetMax = new Vector2(0f, -2f);
        bossTitleText = titleObj.AddComponent<Text>();
        ConfigureHDText(bossTitleText, 11, TextAnchor.MiddleLeft, new Color(1.0f, 0.84f, 0.84f, 1f), FontStyle.Bold);

        GameObject hpObj = new GameObject("HPText");
        hpObj.transform.SetParent(bossBarRootObj.transform, false);
        RectTransform hRT = hpObj.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0.68f, 0.42f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.offsetMin = new Vector2(0f, 0f);
        hRT.offsetMax = new Vector2(-14f, -2f);
        bossHpText = hpObj.AddComponent<Text>();
        ConfigureHDText(bossHpText, 11, TextAnchor.MiddleRight, new Color(0.98f, 0.92f, 0.82f, 1f), FontStyle.Bold);

        bossBarRootObj.SetActive(false);
    }

    private void CreateClock()
    {
        GameObject clockObj = new GameObject("Clock");
        clockObj.transform.SetParent(hudRoot, false);
        RectTransform clockRect = clockObj.AddComponent<RectTransform>();
        clockRect.anchorMin = new Vector2(1, 1);
        clockRect.anchorMax = new Vector2(1, 1);
        clockRect.pivot = new Vector2(1, 1);
        clockRect.anchoredPosition = new Vector2(-20f, -14f);
        clockRect.sizeDelta = new Vector2(198f, 52f);

        Image clockBg = clockObj.AddComponent<Image>();
        clockBg.sprite = FragaProIconLibrary.GetPillSprite();
        clockBg.type = Image.Type.Sliced;
        clockBg.color = new Color(0.04f, 0.06f, 0.09f, 0.86f);
        clockBg.raycastTarget = false;

        Outline clockOut = clockObj.AddComponent<Outline>();
        clockOut.effectColor = new Color(0.32f, 0.64f, 0.82f, 0.45f);
        clockOut.effectDistance = new Vector2(1.1f, -1.1f);

        GameObject hourObj = new GameObject("ClockText");
        hourObj.transform.SetParent(clockObj.transform, false);
        RectTransform hourRect = hourObj.AddComponent<RectTransform>();
        hourRect.anchorMin = new Vector2(0f, 0.42f);
        hourRect.anchorMax = new Vector2(1f, 1f);
        hourRect.offsetMin = new Vector2(6f, 0f);
        hourRect.offsetMax = new Vector2(-6f, -3f);

        clockText = hourObj.AddComponent<Text>();
        ConfigureHDText(clockText, 18, TextAnchor.MiddleCenter, new Color(1f, 0.90f, 0.62f), FontStyle.Bold);
        clockText.text = "10:00  DIA";

        GameObject dayObj = new GameObject("DayText");
        dayObj.transform.SetParent(clockObj.transform, false);
        RectTransform dayRect = dayObj.AddComponent<RectTransform>();
        dayRect.anchorMin = new Vector2(0f, 0f);
        dayRect.anchorMax = new Vector2(1f, 0.44f);
        dayRect.offsetMin = new Vector2(6f, 3f);
        dayRect.offsetMax = new Vector2(-6f, 0f);

        dayCounterText = dayObj.AddComponent<Text>();
        ConfigureHDText(dayCounterText, 11, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 0.92f, 0.95f), FontStyle.Bold);
        dayCounterText.text = "SOBREVIVENCIA - DIA 1";
    }

    private void CreateDamageIndicators()
    {
        damageTop = CreateDamageArrow("DmgTop", new Vector2(0.5f, 1), new Vector2(0, -60f), 0f);
        damageBottom = CreateDamageArrow("DmgBottom", new Vector2(0.5f, 0), new Vector2(0, 60f), 180f);
        damageLeft = CreateDamageArrow("DmgLeft", new Vector2(0, 0.5f), new Vector2(60f, 0), 90f);
        damageRight = CreateDamageArrow("DmgRight", new Vector2(1, 0.5f), new Vector2(-60f, 0), -90f);
    }

    private Image CreateDamageArrow(string name, Vector2 anchor, Vector2 pos, float rotation)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(hudRoot, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(70f, 35f);
        rect.localRotation = Quaternion.Euler(0, 0, rotation);

        Image img = obj.AddComponent<Image>();
        img.color = new Color(1f, 0f, 0f, 0f);
        img.raycastTarget = false;
        return img;
    }

    private void CreateLootTooltip()
    {
        GameObject tooltipObj = new GameObject("LootTooltip");
        tooltipObj.transform.SetParent(hudRoot, false);
        RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 1f);
        tooltipRect.anchorMax = new Vector2(0.5f, 1f);
        tooltipRect.pivot = new Vector2(0.5f, 1f);
        tooltipRect.anchoredPosition = new Vector2(0f, -50f);
        tooltipRect.sizeDelta = new Vector2(640f, 38f);

        lootTooltipGroup = tooltipObj.AddComponent<CanvasGroup>();
        lootTooltipGroup.alpha = 0f;
        lootTooltipGroup.blocksRaycasts = false;

        Image bg = tooltipObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.03f, 0.06f, 0.10f, 0.90f);
        bg.raycastTarget = false;

        Outline outline = tooltipObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.80f, 0.30f, 0.55f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);

        GameObject textObj = new GameObject("TooltipText");
        textObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18, 0);
        textRect.offsetMax = new Vector2(-18, 0);

        lootTooltipText = textObj.AddComponent<Text>();
        ConfigureHDText(lootTooltipText, 15, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.45f), FontStyle.Bold);
        lootTooltipText.supportRichText = true;
    }

    private void CreateItemCollectionFeed()
    {
        GameObject feedRoot = new GameObject("ItemCollectionFeed");
        feedRoot.transform.SetParent(hudRoot, false);
        collectionFeedRootRT = feedRoot.AddComponent<RectTransform>();
        collectionFeedRootRT.anchorMin = new Vector2(0f, 1f);
        collectionFeedRootRT.anchorMax = new Vector2(0f, 1f);
        collectionFeedRootRT.pivot = new Vector2(0f, 1f);
        collectionFeedRootRT.anchoredPosition = new Vector2(24f, -62f);
        collectionFeedRootRT.sizeDelta = new Vector2(360f, 220f);

        for (int i = 0; i < COLLECTION_FEED_SLOTS; i++)
        {
            GameObject rowObj = new GameObject($"FeedRow_{i}");
            rowObj.transform.SetParent(feedRoot.transform, false);
            RectTransform rRT = rowObj.AddComponent<RectTransform>();
            rRT.anchorMin = new Vector2(0f, 1f);
            rRT.anchorMax = new Vector2(0f, 1f);
            rRT.pivot = new Vector2(0f, 1f);
            rRT.anchoredPosition = new Vector2(0f, -i * 44f);
            rRT.sizeDelta = new Vector2(356f, 38f);

            CanvasGroup cg = rowObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            Image bg = rowObj.AddComponent<Image>();
            bg.sprite = FragaProIconLibrary.GetPillSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.04f, 0.09f, 0.12f, 0.90f);
            bg.raycastTarget = false;

            Outline outl = rowObj.AddComponent<Outline>();
            outl.effectColor = new Color(0.35f, 0.88f, 0.62f, 0.62f);
            outl.effectDistance = new Vector2(1.1f, -1.1f);

            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(rowObj.transform, false);
            RectTransform iRT = icoObj.AddComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0f, 0.5f);
            iRT.anchorMax = new Vector2(0f, 0.5f);
            iRT.pivot = new Vector2(0f, 0.5f);
            iRT.anchoredPosition = new Vector2(10f, 0f);
            iRT.sizeDelta = new Vector2(24f, 24f);
            Image ico = icoObj.AddComponent<Image>();
            ico.preserveAspect = true;
            ico.raycastTarget = false;

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(rowObj.transform, false);
            RectTransform tRT = txtObj.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(40f, 0f);
            tRT.offsetMax = new Vector2(-10f, 0f);
            Text txt = txtObj.AddComponent<Text>();
            ConfigureHDText(txt, 13, TextAnchor.MiddleLeft, new Color(0.75f, 0.98f, 0.80f), FontStyle.Bold);
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;

            rowObj.SetActive(false);
            collectionFeedRows[i] = new CollectionFeedRow
            {
                root = rowObj,
                cg = cg,
                icon = ico,
                text = txt,
                itemId = -1,
                accumulatedAdded = 0,
                lastTotalCount = 0,
                timer = 0f
            };
        }
    }

    public void ShowItemCollected(int itemId, int addedAmount, int totalCount)
    {
        if (addedAmount <= 0 || collectionFeedRows[0].root == null) return;

        ItemData d = ItemDatabase.GetItem(itemId);
        string itemName = d != null ? d.itemName : $"Item {itemId}";
        Sprite icoSprite = FragaProIconLibrary.GetItemIcon(itemId);

        // Se ja ha uma linha recente para este mesmo item, acumula e atualiza sem duplicar
        for (int i = 0; i < COLLECTION_FEED_SLOTS; i++)
        {
            if (collectionFeedRows[i].root != null &&
                collectionFeedRows[i].root.activeSelf &&
                collectionFeedRows[i].itemId == itemId &&
                collectionFeedRows[i].timer > 0.35f)
            {
                var row = collectionFeedRows[i];
                row.accumulatedAdded += addedAmount;
                row.lastTotalCount = totalCount;
                row.timer = 4.0f;
                if (row.text != null)
                {
                    row.text.text = $"+{row.accumulatedAdded} {itemName}  <color=#A8D8FF>(Total: {totalCount})</color>";
                }
                if (row.cg != null) row.cg.alpha = 1f;
                collectionFeedRows[i] = row;
                RefreshResourceCounterBar();
                return;
            }
        }

        // Empurra linhas existentes para baixo para exibir o item mais recente no topo (Zero-GC)
        for (int i = COLLECTION_FEED_SLOTS - 1; i > 0; i--)
        {
            var prev = collectionFeedRows[i - 1];
            var dst = collectionFeedRows[i];
            dst.itemId = prev.itemId;
            dst.accumulatedAdded = prev.accumulatedAdded;
            dst.lastTotalCount = prev.lastTotalCount;
            dst.timer = prev.timer;
            if (prev.root != null && prev.root.activeSelf && prev.timer > 0f)
            {
                dst.root.SetActive(true);
                if (dst.icon != null && prev.icon != null) dst.icon.sprite = prev.icon.sprite;
                if (dst.text != null && prev.text != null) dst.text.text = prev.text.text;
                if (dst.cg != null && prev.cg != null) dst.cg.alpha = prev.cg.alpha;
            }
            else if (dst.root != null)
            {
                dst.root.SetActive(false);
                if (dst.cg != null) dst.cg.alpha = 0f;
            }
            collectionFeedRows[i] = dst;
        }

        var top = collectionFeedRows[0];
        top.itemId = itemId;
        top.accumulatedAdded = addedAmount;
        top.lastTotalCount = totalCount;
        top.timer = 4.0f;
        if (top.icon != null) top.icon.sprite = icoSprite;
        if (top.text != null)
        {
            top.text.text = $"+{addedAmount} {itemName}  <color=#A8D8FF>(Total: {totalCount})</color>";
        }
        if (top.root != null) top.root.SetActive(true);
        if (top.cg != null) top.cg.alpha = 1f;
        collectionFeedRows[0] = top;

        RefreshResourceCounterBar();
    }

    private void UpdateItemCollectionFeed()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < COLLECTION_FEED_SLOTS; i++)
        {
            var row = collectionFeedRows[i];
            if (row.root == null || !row.root.activeSelf) continue;

            row.timer -= dt;
            if (row.timer <= 0f)
            {
                row.timer = 0f;
                row.itemId = -1;
                row.accumulatedAdded = 0;
                if (row.cg != null) row.cg.alpha = 0f;
                row.root.SetActive(false);
            }
            else if (row.cg != null)
            {
                float targetAlpha = row.timer < 0.7f ? Mathf.Clamp01(row.timer / 0.7f) : 1f;
                row.cg.alpha = targetAlpha;
            }
            collectionFeedRows[i] = row;
        }
    }

    private void CreateWeaponIndicator()
    {
        GameObject weaponObj = new GameObject("WeaponIndicator");
        weaponObj.transform.SetParent(hudRoot, false);
        weaponIndicatorRT = weaponObj.AddComponent<RectTransform>();
        weaponIndicatorRT.anchorMin = new Vector2(1, 0);
        weaponIndicatorRT.anchorMax = new Vector2(1, 0);
        weaponIndicatorRT.pivot = new Vector2(1, 0);
        weaponIndicatorRT.anchoredPosition = new Vector2(-24f, 22f);
        weaponIndicatorRT.sizeDelta = new Vector2(288f, 36f);

        Image bg = weaponObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.06f, 0.09f, 0.78f);
        bg.raycastTarget = false;

        GameObject icoObj = new GameObject("WeaponIcon");
        icoObj.transform.SetParent(weaponObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(8f, 0f);
        iRT.sizeDelta = new Vector2(22f, 22f);
        weaponIconImg = icoObj.AddComponent<Image>();
        weaponIconImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.AttackFist);
        weaponIconImg.preserveAspect = true;
        weaponIconImg.raycastTarget = false;

        GameObject textObj = new GameObject("WeaponText");
        textObj.transform.SetParent(weaponObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(34f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        weaponText = textObj.AddComponent<Text>();
        ConfigureHDText(weaponText, 13, TextAnchor.MiddleLeft, new Color(0.95f, 0.95f, 0.95f, 0.95f), FontStyle.Bold);
        weaponText.text = "PUNHOS";
    }

    private void CreateDesktopControlsHint()
    {
        desktopHintObj = new GameObject("DesktopControlsHintStrip");
        desktopHintObj.transform.SetParent(hudRoot, false);
        RectTransform rect = desktopHintObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 64f);
        rect.sizeDelta = new Vector2(284f, 46f);

        Image bg = desktopHintObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.06f, 0.09f, 0.68f);
        bg.raycastTarget = false;

        GameObject txtObj = new GameObject("HintText");
        txtObj.transform.SetParent(desktopHintObj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(10f, 3f);
        tRT.offsetMax = new Vector2(-10f, -3f);

        Text txt = txtObj.AddComponent<Text>();
        ConfigureHDText(txt, 10, TextAnchor.MiddleRight, new Color(0.84f, 0.90f, 0.96f, 0.92f), FontStyle.Bold);
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.text = "[I] Mochila | [K] Status | [B] Obra | [M] Mapa\n[E] Usar | [L] Luz | [F10] Opcoes";
    }

    private void CreateBonfireIndicator()
    {
        GameObject bfObj = new GameObject("BonfireIndicator");
        bfObj.transform.SetParent(hudRoot, false);
        RectTransform rect = bfObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 218f);
        rect.sizeDelta = new Vector2(220f, 28f);

        bonfireIndicator = bfObj.AddComponent<Image>();
        bonfireIndicator.sprite = FragaProIconLibrary.GetPillSprite();
        bonfireIndicator.type = Image.Type.Sliced;
        bonfireIndicator.color = new Color(0.18f, 0.10f, 0.04f, 0.78f);
        bonfireIndicator.raycastTarget = false;

        GameObject icoObj = new GameObject("FireIcon");
        icoObj.transform.SetParent(bfObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(10f, 0f);
        iRT.sizeDelta = new Vector2(20f, 20f);
        Image fImg = icoObj.AddComponent<Image>();
        fImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.CookFire);
        fImg.preserveAspect = true;
        fImg.raycastTarget = false;

        GameObject textObj = new GameObject("BonfireText");
        textObj.transform.SetParent(bfObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(32f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        Text txt = textObj.AddComponent<Text>();
        ConfigureHDText(txt, 13, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f), FontStyle.Bold);
        txt.text = "AQUECIDO NA FOGUEIRA";

        bfObj.SetActive(false);
    }

    private void CreateStealthIndicator()
    {
        stealthIndicatorObj = new GameObject("StealthIndicator");
        stealthIndicatorObj.transform.SetParent(hudRoot, false);
        RectTransform rect = stealthIndicatorObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 250f);
        rect.sizeDelta = new Vector2(190f, 24f);

        Image bg = stealthIndicatorObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.09f, 0.12f, 0.78f);
        bg.raycastTarget = false;

        GameObject txtObj = new GameObject("StealthText");
        txtObj.transform.SetParent(stealthIndicatorObj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = Vector2.zero;
        tRT.offsetMax = Vector2.zero;

        stealthIndicatorText = txtObj.AddComponent<Text>();
        ConfigureHDText(stealthIndicatorText, 12, TextAnchor.MiddleCenter, new Color(0.45f, 0.92f, 0.80f), FontStyle.Bold);
        stealthIndicatorText.text = "OCULTO";
        stealthIndicatorObj.SetActive(false);
    }

    private void CreateCrosshair()
    {
        GameObject chObj = new GameObject("Crosshair");
        chObj.transform.SetParent(hudRoot, false);
        RectTransform rect = chObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(6f, 6f);

        crosshairDot = chObj.AddComponent<Image>();
        crosshairDot.sprite = FragaProIconLibrary.GetCircleBackground();
        crosshairDot.color = new Color(1f, 1f, 1f, 0.58f);
        crosshairDot.raycastTarget = false;

        GameObject ringObj = new GameObject("CrosshairRing");
        ringObj.transform.SetParent(hudRoot, false);
        crosshairRingRT = ringObj.AddComponent<RectTransform>();
        crosshairRingRT.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRingRT.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRingRT.pivot = new Vector2(0.5f, 0.5f);
        crosshairRingRT.anchoredPosition = Vector2.zero;
        crosshairRingRT.sizeDelta = new Vector2(20f, 20f);
        crosshairRing = ringObj.AddComponent<Image>();
        crosshairRing.sprite = s_microRingSprite != null ? s_microRingSprite : GetRingSprite(64, 0.72f, 0.96f);
        crosshairRing.color = new Color(1f, 1f, 1f, 0f);
        crosshairRing.raycastTarget = false;

        // Anel Radial 360 de Hold (esfolar / armar armadilha / beber / vasculhar)
        GameObject holdRingObj = new GameObject("CrosshairHoldRing");
        holdRingObj.transform.SetParent(hudRoot, false);
        crosshairHoldRingRT = holdRingObj.AddComponent<RectTransform>();
        crosshairHoldRingRT.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairHoldRingRT.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairHoldRingRT.pivot = new Vector2(0.5f, 0.5f);
        crosshairHoldRingRT.anchoredPosition = Vector2.zero;
        crosshairHoldRingRT.sizeDelta = new Vector2(36f, 36f);
        crosshairHoldRing = holdRingObj.AddComponent<Image>();
        crosshairHoldRing.sprite = s_microRingSprite != null ? s_microRingSprite : GetRingSprite(64, 0.72f, 0.96f);
        crosshairHoldRing.type = Image.Type.Filled;
        crosshairHoldRing.fillMethod = Image.FillMethod.Radial360;
        crosshairHoldRing.fillOrigin = (int)Image.Origin360.Top;
        crosshairHoldRing.fillClockwise = true;
        crosshairHoldRing.fillAmount = 0f;
        crosshairHoldRing.color = new Color(0.32f, 0.96f, 0.88f, 0f);
        crosshairHoldRing.raycastTarget = false;

        // Hit-Marker 4 ticks em X
        GameObject hmObj = new GameObject("Crosshair_HitMarker");
        hmObj.transform.SetParent(hudRoot, false);
        hitMarkerRT = hmObj.AddComponent<RectTransform>();
        hitMarkerRT.anchorMin = new Vector2(0.5f, 0.5f);
        hitMarkerRT.anchorMax = new Vector2(0.5f, 0.5f);
        hitMarkerRT.pivot = new Vector2(0.5f, 0.5f);
        hitMarkerRT.anchoredPosition = Vector2.zero;
        hitMarkerRT.sizeDelta = new Vector2(32f, 32f);

        hitMarkerGroup = hmObj.AddComponent<CanvasGroup>();
        hitMarkerGroup.alpha = 0f;
        hitMarkerGroup.blocksRaycasts = false;

        float[] angles = { 45f, -45f, 135f, -135f };
        Vector2[] offsets = { new Vector2(7f, 7f), new Vector2(-7f, 7f), new Vector2(7f, -7f), new Vector2(-7f, -7f) };
        for (int i = 0; i < 4; i++)
        {
            GameObject tick = new GameObject($"Tick_{i}");
            tick.transform.SetParent(hmObj.transform, false);
            RectTransform tRT = tick.AddComponent<RectTransform>();
            tRT.anchoredPosition = offsets[i];
            tRT.sizeDelta = new Vector2(2.6f, 10f);
            tRT.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
            Image tImg = tick.AddComponent<Image>();
            tImg.color = Color.white;
            tImg.raycastTarget = false;
            hitMarkerTicks[i] = tImg;
        }
    }

    public void TriggerHitMarker(bool isCrit = false, bool isKill = false)
    {
        if (hitMarkerGroup == null) return;
        hitMarkerTimer = isKill ? 0.28f : 0.18f;
        hitMarkerColor = isKill
            ? new Color(1.0f, 0.25f, 0.22f, 1f)
            : (isCrit ? new Color(0.32f, 1.0f, 0.85f, 1f) : Color.white);
        for (int i = 0; i < hitMarkerTicks.Length; i++)
        {
            if (hitMarkerTicks[i] != null) hitMarkerTicks[i].color = hitMarkerColor;
        }
        hitMarkerGroup.alpha = 1f;
        if (hitMarkerRT != null)
            hitMarkerRT.localScale = Vector3.one * (isKill ? 1.35f : (isCrit ? 1.22f : 1.08f));
    }

    private void CreateInteractionPrompt()
    {
        interactPromptObj = new GameObject("ContextualInteractionPrompt");
        interactPromptObj.transform.SetParent(hudRoot, false);
        interactPromptRT = interactPromptObj.AddComponent<RectTransform>();
        interactPromptRT.anchorMin = new Vector2(0.5f, 0.5f);
        interactPromptRT.anchorMax = new Vector2(0.5f, 0.5f);
        interactPromptRT.pivot = new Vector2(0.5f, 0.5f);
        interactPromptRT.anchoredPosition = new Vector2(0f, -90f);
        interactPromptRT.sizeDelta = new Vector2(560f, 40f);

        Image bg = interactPromptObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.07f, 0.11f, 0.90f);
        bg.raycastTarget = true;

        Outline pOut = interactPromptObj.AddComponent<Outline>();
        pOut.effectColor = new Color(0.35f, 0.78f, 0.95f, 0.48f);
        pOut.effectDistance = new Vector2(1.1f, -1.1f);

        EventTrigger promptTrig = interactPromptObj.AddComponent<EventTrigger>();
        var pDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pDown.callback.AddListener((_) =>
        {
            if (InteractionDetector.Instance != null && InteractionDetector.Instance.IsHoldTarget)
            {
                if (FragaInputRouter.Instance != null) FragaInputRouter.Instance.TriggerMobileInteractDown();
            }
            else
            {
                if (FragaInputRouter.Instance != null) FragaInputRouter.Instance.TriggerMobileInteract();
                else if (InteractionDetector.Instance != null) InteractionDetector.Instance.ExecuteCurrentInteraction();
            }
        });
        promptTrig.triggers.Add(pDown);

        var pUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pUp.callback.AddListener((_) =>
        {
            if (FragaInputRouter.Instance != null) FragaInputRouter.Instance.TriggerMobileInteractUp();
        });
        promptTrig.triggers.Add(pUp);

        var pExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        pExit.callback.AddListener((_) =>
        {
            if (FragaInputRouter.Instance != null) FragaInputRouter.Instance.TriggerMobileInteractUp();
        });
        promptTrig.triggers.Add(pExit);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(interactPromptObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(12f, 0f);
        iRT.sizeDelta = new Vector2(26f, 26f);
        interactPromptIcon = icoObj.AddComponent<Image>();
        interactPromptIcon.preserveAspect = true;
        interactPromptIcon.raycastTarget = false;

        GameObject txtObj = new GameObject("PromptText");
        txtObj.transform.SetParent(interactPromptObj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(44f, 0f);
        tRT.offsetMax = new Vector2(-64f, 0f);
        interactPromptText = txtObj.AddComponent<Text>();
        ConfigureHDText(interactPromptText, 14, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);

        GameObject keyObj = new GameObject("KeyBadge");
        keyObj.transform.SetParent(interactPromptObj.transform, false);
        interactKeyBadgeRT = keyObj.AddComponent<RectTransform>();
        interactKeyBadgeRT.anchorMin = new Vector2(1f, 0.5f);
        interactKeyBadgeRT.anchorMax = new Vector2(1f, 0.5f);
        interactKeyBadgeRT.pivot = new Vector2(1f, 0.5f);
        interactKeyBadgeRT.anchoredPosition = new Vector2(-10f, 0f);
        interactKeyBadgeRT.sizeDelta = new Vector2(46f, 24f);
        interactKeyBadgeBg = keyObj.AddComponent<Image>();
        interactKeyBadgeBg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        interactKeyBadgeBg.type = Image.Type.Sliced;
        interactKeyBadgeBg.color = new Color(0.18f, 0.42f, 0.58f, 0.92f);
        interactKeyBadgeBg.raycastTarget = false;

        GameObject keyTxtObj = new GameObject("KeyText");
        keyTxtObj.transform.SetParent(keyObj.transform, false);
        RectTransform ktRT = keyTxtObj.AddComponent<RectTransform>();
        ktRT.anchorMin = Vector2.zero;
        ktRT.anchorMax = Vector2.one;
        ktRT.offsetMin = Vector2.zero;
        ktRT.offsetMax = Vector2.zero;
        interactKeyBadgeText = keyTxtObj.AddComponent<Text>();
        ConfigureHDText(interactKeyBadgeText, 12, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        interactKeyBadgeText.text = "E";

        GameObject holdBarObj = new GameObject("HoldProgressFill");
        holdBarObj.transform.SetParent(interactPromptObj.transform, false);
        RectTransform hbRT = holdBarObj.AddComponent<RectTransform>();
        hbRT.anchorMin = new Vector2(0f, 0f);
        hbRT.anchorMax = new Vector2(1f, 0f);
        hbRT.pivot = new Vector2(0.5f, 0f);
        hbRT.offsetMin = new Vector2(14f, 2f);
        hbRT.offsetMax = new Vector2(-14f, 5f);
        interactHoldFillBar = holdBarObj.AddComponent<Image>();
        interactHoldFillBar.sprite = FragaProIconLibrary.GetBarFillSprite();
        interactHoldFillBar.type = Image.Type.Filled;
        interactHoldFillBar.fillMethod = Image.FillMethod.Horizontal;
        interactHoldFillBar.fillAmount = 0f;
        interactHoldFillBar.color = new Color(0.32f, 0.96f, 0.88f, 0.95f);
        interactHoldFillBar.raycastTarget = false;

        interactPromptObj.SetActive(false);
    }

    private void CreateBlueprintSiteCard()
    {
        blueprintCardObj = new GameObject("BlueprintSiteCard");
        blueprintCardObj.transform.SetParent(hudRoot, false);
        RectTransform rt = blueprintCardObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 114f);
        rt.sizeDelta = new Vector2(560f, 96f);

        Image bg = blueprintCardObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.09f, 0.14f, 0.94f);
        bg.raycastTarget = true;

        Outline cardOut = blueprintCardObj.AddComponent<Outline>();
        cardOut.effectColor = new Color(0.32f, 0.84f, 0.96f, 0.72f);
        cardOut.effectDistance = new Vector2(1.4f, -1.4f);

        GameObject icoObj = new GameObject("StructureIcon");
        icoObj.transform.SetParent(blueprintCardObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 1f);
        iRT.anchorMax = new Vector2(0f, 1f);
        iRT.pivot = new Vector2(0f, 1f);
        iRT.anchoredPosition = new Vector2(14f, -10f);
        iRT.sizeDelta = new Vector2(28f, 28f);
        blueprintCardIcon = icoObj.AddComponent<Image>();
        blueprintCardIcon.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.BuildHammer);
        blueprintCardIcon.preserveAspect = true;
        blueprintCardIcon.raycastTarget = false;

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(blueprintCardObj.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 1f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.pivot = new Vector2(0.5f, 1f);
        tRT.offsetMin = new Vector2(50f, -34f);
        tRT.offsetMax = new Vector2(-120f, -8f);
        blueprintCardTitleText = titleObj.AddComponent<Text>();
        ConfigureHDText(blueprintCardTitleText, 13, TextAnchor.MiddleLeft, new Color(0.55f, 0.94f, 1.0f), FontStyle.Bold);
        blueprintCardTitleText.text = "OBRA PROXIMA";

        // Barra de progresso no canto superior direito do cartao
        GameObject progBg = new GameObject("ProgressBG");
        progBg.transform.SetParent(blueprintCardObj.transform, false);
        RectTransform pbRT = progBg.AddComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(1f, 1f);
        pbRT.anchorMax = new Vector2(1f, 1f);
        pbRT.pivot = new Vector2(1f, 1f);
        pbRT.anchoredPosition = new Vector2(-14f, -14f);
        pbRT.sizeDelta = new Vector2(102f, 16f);
        Image pbImg = progBg.AddComponent<Image>();
        pbImg.color = new Color(0.10f, 0.14f, 0.18f, 0.95f);
        pbImg.raycastTarget = false;

        GameObject progFill = new GameObject("ProgressFill");
        progFill.transform.SetParent(progBg.transform, false);
        RectTransform pfRT = progFill.AddComponent<RectTransform>();
        pfRT.anchorMin = Vector2.zero;
        pfRT.anchorMax = Vector2.one;
        pfRT.offsetMin = Vector2.zero;
        pfRT.offsetMax = Vector2.zero;
        blueprintCardProgressFill = progFill.AddComponent<Image>();
        blueprintCardProgressFill.sprite = FragaProIconLibrary.GetBarFillSprite();
        blueprintCardProgressFill.color = new Color(0.28f, 0.88f, 0.56f, 0.95f);
        blueprintCardProgressFill.type = Image.Type.Filled;
        blueprintCardProgressFill.fillMethod = Image.FillMethod.Horizontal;
        blueprintCardProgressFill.fillOrigin = 0;
        blueprintCardProgressFill.fillAmount = 0f;
        blueprintCardProgressFill.raycastTarget = false;

        GameObject progTxt = new GameObject("ProgressText");
        progTxt.transform.SetParent(progBg.transform, false);
        RectTransform ptRT = progTxt.AddComponent<RectTransform>();
        ptRT.anchorMin = Vector2.zero;
        ptRT.anchorMax = Vector2.one;
        ptRT.offsetMin = Vector2.zero;
        ptRT.offsetMax = Vector2.zero;
        blueprintCardProgressText = progTxt.AddComponent<Text>();
        ConfigureHDText(blueprintCardProgressText, 10, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        blueprintCardProgressText.text = "0%";

        GameObject matObj = new GameObject("MaterialsText");
        matObj.transform.SetParent(blueprintCardObj.transform, false);
        RectTransform mRT = matObj.AddComponent<RectTransform>();
        mRT.anchorMin = new Vector2(0f, 0.42f);
        mRT.anchorMax = new Vector2(1f, 0.68f);
        mRT.offsetMin = new Vector2(50f, 0f);
        mRT.offsetMax = new Vector2(-14f, 0f);
        blueprintCardMaterialsText = matObj.AddComponent<Text>();
        ConfigureHDText(blueprintCardMaterialsText, 12, TextAnchor.MiddleLeft, new Color(0.95f, 0.92f, 0.72f), FontStyle.Bold);
        blueprintCardMaterialsText.supportRichText = true;
        blueprintCardMaterialsText.text = "Madeira: 0/0 | Pedra: 0/0";

        // Botao COLOCAR MATERIAIS [E]
        blueprintDepositBtnLabel = CreateBlueprintActionButton(blueprintCardObj.transform, "Btn_DepositMat", "COLOCAR MATERIAIS [E]",
            FragaProIconLibrary.ControlIcon.BuildHammer, new Vector2(-78f, 20f), 260f,
            new Color(0.14f, 0.50f, 0.34f, 0.96f), () =>
            {
                if (InteractionDetector.Instance != null)
                    InteractionDetector.Instance.DepositToNearbyBlueprint();
            });

        // Botao CANCELAR [X]
        blueprintCancelBtnLabel = CreateBlueprintActionButton(blueprintCardObj.transform, "Btn_CancelSite", "CANCELAR [X]",
            FragaProIconLibrary.ControlIcon.CancelCross, new Vector2(160f, 20f), 196f,
            new Color(0.56f, 0.18f, 0.18f, 0.96f), () =>
            {
                if (InteractionDetector.Instance != null)
                    InteractionDetector.Instance.CancelNearbyBlueprint();
            });

        blueprintCardObj.SetActive(false);
    }

    private static Text CreateBlueprintActionButton(Transform parent, string name, string label,
        FragaProIconLibrary.ControlIcon icon, Vector2 pos, float width, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 30f);

        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = bgColor;
        btnObj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(btnObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(10f, 0f);
        iRT.sizeDelta = new Vector2(18f, 18f);
        Image iImg = icoObj.AddComponent<Image>();
        iImg.sprite = FragaProIconLibrary.GetControlIcon(icon);
        iImg.preserveAspect = true;
        iImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(32f, 0f);
        tRT.offsetMax = new Vector2(-8f, 0f);
        Text t = txtObj.AddComponent<Text>();
        ConfigureHDText(t, 11, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        t.text = label;
        return t;
    }

    private void UpdateBlueprintCard()
    {
        if (blueprintCardObj == null) return;

        bool modalBlocking = (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen) ||
                             (BlueprintPlacementManager.Instance != null && (BlueprintPlacementManager.Instance.IsPlacingGhost || BlueprintPlacementManager.Instance.IsBookOpen)) ||
                             (FragaOptionsMenu.Instance != null && FragaOptionsMenu.Instance.IsOpen) ||
                             SurvivalMapSystem.IsMapOpen ||
                             StorageChestInteractable.IsAnyChestOpen ||
                             UIManager.IsEndgameModalOpen;

        WorldBlueprintSite site = (!modalBlocking && InteractionDetector.Instance != null)
            ? InteractionDetector.Instance.NearbyBlueprintSite
            : null;

        if (site == null || site.IsComplete)
        {
            if (blueprintCardObj.activeSelf) blueprintCardObj.SetActive(false);
            return;
        }

        if (!blueprintCardObj.activeSelf) blueprintCardObj.SetActive(true);

        if (blueprintCardTitleText != null)
        {
            bool showCoopTag = FragaCoopSession.Instance != null && FragaCoopSession.Instance.ShouldShowPartyHUD;
            string coopTag = !showCoopTag
                ? string.Empty
                : (site.isSharedWithGroup ? " [GRUPO]" : $" [PRIVADO: {site.ownerPlayerName}]");
            blueprintCardTitleText.text = $"OBRA PROXIMA: {site.structureName.ToUpper()}{coopTag}";
        }

        int bagWood = inventoryCache != null ? inventoryCache.GetItemCount(100) : 0;
        int bagStone = inventoryCache != null ? inventoryCache.GetItemCount(101) : 0;
        int shoulderLogs = LogCarryingSystem.Instance != null ? LogCarryingSystem.Instance.currentLogs : 0;
        int totalWoodAvail = bagWood + shoulderLogs;

        if (blueprintCardMaterialsText != null)
        {
            blueprintCardMaterialsText.text =
                $"Madeira: {site.currentLogs}/{site.neededLogs} <color=#99E8FF>(Tem: {totalWoodAvail})</color>   |   Pedra: {site.currentStones}/{site.neededStones} <color=#99E8FF>(Tem: {bagStone})</color>";
        }

        float pct = site.Progress01;
        if (blueprintCardProgressFill != null) blueprintCardProgressFill.fillAmount = pct;
        if (blueprintCardProgressText != null) blueprintCardProgressText.text = $"{Mathf.RoundToInt(pct * 100f)}%";

        bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
        if (blueprintDepositBtnLabel != null)
            blueprintDepositBtnLabel.text = touchActive ? "COLOCAR MATERIAIS" : "COLOCAR MATERIAIS [E]";
        if (blueprintCancelBtnLabel != null)
        {
            float cancelProg = InteractionDetector.Instance != null ? InteractionDetector.Instance.CancelHoldProgress01 : 0f;
            blueprintCancelBtnLabel.text = cancelProg > 0.01f
                ? $"CANCELAR... {Mathf.RoundToInt(cancelProg * 100f)}%"
                : (touchActive ? "CANCELAR" : "CANCELAR [X]");
        }
    }

    private void CreateBonfireProximityCard()
    {
        bonfireCardObj = new GameObject("BonfireProximityCard");
        bonfireCardObj.transform.SetParent(hudRoot, false);
        bonfireCardRT = bonfireCardObj.AddComponent<RectTransform>();
        bonfireCardRT.anchorMin = new Vector2(0.5f, 0f);
        bonfireCardRT.anchorMax = new Vector2(0.5f, 0f);
        bonfireCardRT.pivot = new Vector2(0.5f, 0f);
        bonfireCardRT.anchoredPosition = new Vector2(0f, 114f);
        bonfireCardRT.sizeDelta = new Vector2(560f, 102f);

        Image bg = bonfireCardObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.08f, 0.06f, 0.05f, 0.94f);
        bg.raycastTarget = true;

        Outline cardOut = bonfireCardObj.AddComponent<Outline>();
        cardOut.effectColor = new Color(0.98f, 0.62f, 0.22f, 0.72f);
        cardOut.effectDistance = new Vector2(1.4f, -1.4f);

        GameObject icoObj = new GameObject("FireIcon");
        icoObj.transform.SetParent(bonfireCardObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 1f);
        iRT.anchorMax = new Vector2(0f, 1f);
        iRT.pivot = new Vector2(0f, 1f);
        iRT.anchoredPosition = new Vector2(14f, -10f);
        iRT.sizeDelta = new Vector2(26f, 26f);
        Image ico = icoObj.AddComponent<Image>();
        ico.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.CookFire);
        ico.preserveAspect = true;
        ico.raycastTarget = false;

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(bonfireCardObj.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 1f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.pivot = new Vector2(0.5f, 1f);
        tRT.offsetMin = new Vector2(46f, -34f);
        tRT.offsetMax = new Vector2(-132f, -8f);
        bonfireCardTitleText = titleObj.AddComponent<Text>();
        ConfigureHDText(bonfireCardTitleText, 12, TextAnchor.MiddleLeft, new Color(1.0f, 0.84f, 0.42f), FontStyle.Bold);
        bonfireCardTitleText.text = "FOGUEIRA ACESA — LENHA 100%";

        // Barra de combustivel no canto superior direito
        GameObject fuelBg = new GameObject("FuelTrackBG");
        fuelBg.transform.SetParent(bonfireCardObj.transform, false);
        RectTransform fbRT = fuelBg.AddComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(1f, 1f);
        fbRT.anchorMax = new Vector2(1f, 1f);
        fbRT.pivot = new Vector2(1f, 1f);
        fbRT.anchoredPosition = new Vector2(-14f, -14f);
        fbRT.sizeDelta = new Vector2(110f, 12f);
        Image fbImg = fuelBg.AddComponent<Image>();
        fbImg.color = new Color(0.14f, 0.08f, 0.06f, 0.95f);
        fbImg.raycastTarget = false;

        GameObject fuelFillObj = new GameObject("FuelFill");
        fuelFillObj.transform.SetParent(fuelBg.transform, false);
        RectTransform ffRT = fuelFillObj.AddComponent<RectTransform>();
        ffRT.anchorMin = Vector2.zero;
        ffRT.anchorMax = Vector2.one;
        ffRT.offsetMin = new Vector2(1f, 1f);
        ffRT.offsetMax = new Vector2(-1f, -1f);
        bonfireFuelFill = fuelFillObj.AddComponent<Image>();
        bonfireFuelFill.sprite = FragaProIconLibrary.GetBarFillSprite();
        bonfireFuelFill.type = Image.Type.Filled;
        bonfireFuelFill.fillMethod = Image.FillMethod.Horizontal;
        bonfireFuelFill.fillAmount = 1f;
        bonfireFuelFill.color = new Color(1.0f, 0.62f, 0.18f, 0.96f);
        bonfireFuelFill.raycastTarget = false;

        GameObject slotsObj = new GameObject("GrillSlotsText");
        slotsObj.transform.SetParent(bonfireCardObj.transform, false);
        RectTransform sRT = slotsObj.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0.40f);
        sRT.anchorMax = new Vector2(1f, 0.68f);
        sRT.offsetMin = new Vector2(46f, 0f);
        sRT.offsetMax = new Vector2(-14f, 0f);
        bonfireSlotsText = slotsObj.AddComponent<Text>();
        ConfigureHDText(bonfireSlotsText, 11, TextAnchor.MiddleLeft, new Color(0.95f, 0.95f, 0.92f), FontStyle.Bold);
        bonfireSlotsText.supportRichText = true;
        bonfireSlotsText.text = "[1] Vazio   |   [2] Vazio   |   [3] Vazio";

        bonfirePrimaryBtnLabel = CreateBlueprintActionButton(bonfireCardObj.transform, "Btn_BonfirePrimary", "ASSAR / FERVER [E]",
            FragaProIconLibrary.ControlIcon.CookFire, new Vector2(-144f, 20f), 238f,
            new Color(0.16f, 0.52f, 0.34f, 0.96f), () =>
            {
                if (InteractionDetector.Instance != null)
                    InteractionDetector.Instance.InteractWithNearbyBonfirePrimary();
            });
        Transform primaryBtnT = bonfireCardObj.transform.Find("Btn_BonfirePrimary");
        if (primaryBtnT != null) bonfirePrimaryBtnBg = primaryBtnT.GetComponent<Image>();

        bonfireWoodBtnLabel = CreateBlueprintActionButton(bonfireCardObj.transform, "Btn_BonfireAddWood", "+LENHA",
            FragaProIconLibrary.ControlIcon.Backpack, new Vector2(52f, 20f), 124f,
            new Color(0.46f, 0.28f, 0.12f, 0.96f), () =>
            {
                if (InteractionDetector.Instance != null)
                    InteractionDetector.Instance.AddWoodToNearbyBonfire();
            });

        bonfireRecipesBtnLabel = CreateBlueprintActionButton(bonfireCardObj.transform, "Btn_BonfireRecipes", "RECEITAS [F]",
            FragaProIconLibrary.ControlIcon.Workbench, new Vector2(194f, 20f), 138f,
            new Color(0.16f, 0.38f, 0.56f, 0.96f), () =>
            {
                if (InteractionDetector.Instance != null)
                    InteractionDetector.Instance.OpenNearbyBonfireRecipes();
            });

        bonfireCardObj.SetActive(false);
    }

    private void UpdateBonfireCard()
    {
        if (bonfireCardObj == null) return;

        bool modalBlocking = (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen) ||
                             (BlueprintPlacementManager.Instance != null && (BlueprintPlacementManager.Instance.IsPlacingGhost || BlueprintPlacementManager.Instance.IsBookOpen)) ||
                             (FragaOptionsMenu.Instance != null && FragaOptionsMenu.Instance.IsOpen) ||
                             SurvivalMapSystem.IsMapOpen ||
                             StorageChestInteractable.IsAnyChestOpen ||
                             UIManager.IsEndgameModalOpen;

        BonfireEntity bf = (!modalBlocking && InteractionDetector.Instance != null)
            ? InteractionDetector.Instance.NearbyBonfire
            : null;

        if (bf == null)
        {
            if (bonfireCardObj.activeSelf) bonfireCardObj.SetActive(false);
            return;
        }

        if (!bonfireCardObj.activeSelf) bonfireCardObj.SetActive(true);

        bool blueprintVisible = blueprintCardObj != null && blueprintCardObj.activeSelf;
        if (bonfireCardRT != null)
        {
            bonfireCardRT.anchoredPosition = new Vector2(0f, blueprintVisible ? 218f : 114f);
        }

        int fuelPct = Mathf.Clamp(Mathf.RoundToInt(bf.fuelLevel), 0, 100);
        if (bonfireCardTitleText != null)
        {
            bonfireCardTitleText.text = bf.isLit
                ? $"FOGUEIRA ACESA — LENHA {fuelPct}%"
                : $"FOGUEIRA APAGADA — LENHA {fuelPct}%";
            bonfireCardTitleText.color = bf.isLit
                ? (fuelPct <= 25 ? new Color(1.0f, 0.55f, 0.34f) : new Color(1.0f, 0.84f, 0.42f))
                : new Color(0.75f, 0.80f, 0.86f);
        }

        if (bonfireFuelFill != null)
        {
            bonfireFuelFill.fillAmount = Mathf.Clamp01(bf.fuelLevel / 100f);
            bonfireFuelFill.color = !bf.isLit
                ? new Color(0.45f, 0.48f, 0.52f, 0.90f)
                : (fuelPct <= 25 ? new Color(0.98f, 0.32f, 0.22f, 0.96f) : new Color(1.0f, 0.62f, 0.18f, 0.96f));
        }

        if (bonfireSlotsText != null)
        {
            bonfireSlotsSb.Clear();
            for (int i = 0; i < BonfireEntity.MaxCookSlots; i++)
            {
                if (i > 0) bonfireSlotsSb.Append("   |   ");
                BonfireEntity.CookSlotState slot = bf.GetSlotState(i);
                if (!slot.IsOccupied)
                {
                    bonfireSlotsSb.Append("<color=#7E8D9B>[");
                    bonfireSlotsSb.Append(i + 1);
                    bonfireSlotsSb.Append("] Vazio</color>");
                    continue;
                }

                string shortName = GetShortCookItemName(slot.rawItemId);
                if (slot.stage == BonfireEntity.CookStage.Burnt)
                {
                    bonfireSlotsSb.Append("<color=#FF6655>[");
                    bonfireSlotsSb.Append(i + 1);
                    bonfireSlotsSb.Append("] Carvao (Queimou)</color>");
                }
                else if (slot.stage == BonfireEntity.CookStage.Ready)
                {
                    bonfireSlotsSb.Append("<color=#66FFAA>[");
                    bonfireSlotsSb.Append(i + 1);
                    bonfireSlotsSb.Append("] ");
                    bonfireSlotsSb.Append(shortName);
                    bonfireSlotsSb.Append(" PRONTO</color>");
                }
                else
                {
                    int pct = Mathf.Clamp(Mathf.RoundToInt(slot.Progress01 * 100f), 0, 100);
                    bonfireSlotsSb.Append("<color=#FFCC66>[");
                    bonfireSlotsSb.Append(i + 1);
                    bonfireSlotsSb.Append("] ");
                    bonfireSlotsSb.Append(shortName);
                    bonfireSlotsSb.Append(' ');
                    bonfireSlotsSb.Append(pct);
                    bonfireSlotsSb.Append("%</color>");
                }
            }
            bonfireSlotsText.text = bonfireSlotsSb.ToString();
        }

        bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
        string eSuffix = touchActive ? string.Empty : " [E]";
        string fSuffix = touchActive ? string.Empty : " [F]";

        if (bonfirePrimaryBtnLabel != null)
        {
            if (!bf.isLit)
            {
                bonfirePrimaryBtnLabel.text = "ACENDER FOGUEIRA" + eSuffix;
                if (bonfirePrimaryBtnBg != null) bonfirePrimaryBtnBg.color = new Color(0.66f, 0.36f, 0.14f, 0.96f);
            }
            else if (bf.ReadySlotCount > 0)
            {
                bonfirePrimaryBtnLabel.text = "RETIRAR PRONTOS" + eSuffix;
                if (bonfirePrimaryBtnBg != null) bonfirePrimaryBtnBg.color = new Color(0.16f, 0.58f, 0.36f, 0.96f);
            }
            else if (bf.EmptySlotCount > 0 && bf.HasAnyAutoCookableInInventory(inventoryCache))
            {
                bonfirePrimaryBtnLabel.text = "ASSAR / FERVER" + eSuffix;
                if (bonfirePrimaryBtnBg != null) bonfirePrimaryBtnBg.color = new Color(0.18f, 0.48f, 0.34f, 0.96f);
            }
            else
            {
                bonfirePrimaryBtnLabel.text = "ABRIR RECEITAS" + eSuffix;
                if (bonfirePrimaryBtnBg != null) bonfirePrimaryBtnBg.color = new Color(0.16f, 0.38f, 0.54f, 0.96f);
            }
        }

        if (bonfireWoodBtnLabel != null)
        {
            int woodCnt = inventoryCache != null ? inventoryCache.GetItemCount(100) : 0;
            bonfireWoodBtnLabel.text = woodCnt > 0 ? $"+LENHA ({woodCnt})" : "SEM LENHA";
        }

        if (bonfireRecipesBtnLabel != null)
        {
            bonfireRecipesBtnLabel.text = "RECEITAS" + fSuffix;
        }
    }

    private static string GetShortCookItemName(int rawItemId)
    {
        switch (rawItemId)
        {
            case 104: return "Carne";
            case 113: return "Coelho";
            case 111: return "Peixe";
            case 103: return "Sopa";
            case 123: return "Agua";
            default:
                ItemData d = ItemDatabase.GetItem(rawItemId);
                return d != null ? d.itemName : "Item";
        }
    }

    private void CreateCraftQueueHUD()
    {
        craftQueueHudObj = new GameObject("CraftQueueHUDCard");
        craftQueueHudObj.transform.SetParent(hudRoot, false);
        craftQueueHudRT = craftQueueHudObj.AddComponent<RectTransform>();
        craftQueueHudRT.anchorMin = new Vector2(0.5f, 1f);
        craftQueueHudRT.anchorMax = new Vector2(0.5f, 1f);
        craftQueueHudRT.pivot = new Vector2(0.5f, 1f);
        craftQueueHudRT.anchoredPosition = new Vector2(0f, -92f);
        craftQueueHudRT.sizeDelta = new Vector2(400f, 38f);

        Image bg = craftQueueHudObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.09f, 0.14f, 0.92f);
        bg.raycastTarget = true;

        Outline outl = craftQueueHudObj.AddComponent<Outline>();
        outl.effectColor = new Color(0.32f, 0.86f, 0.62f, 0.65f);
        outl.effectDistance = new Vector2(1.2f, -1.2f);

        GameObject fillObj = new GameObject("QueueFill");
        fillObj.transform.SetParent(craftQueueHudObj.transform, false);
        RectTransform fRT = fillObj.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = new Vector2(3f, 3f);
        fRT.offsetMax = new Vector2(-40f, -3f);
        craftQueueHudFill = fillObj.AddComponent<Image>();
        craftQueueHudFill.sprite = FragaProIconLibrary.GetBarFillSprite();
        craftQueueHudFill.type = Image.Type.Filled;
        craftQueueHudFill.fillMethod = Image.FillMethod.Horizontal;
        craftQueueHudFill.fillAmount = 0f;
        craftQueueHudFill.color = new Color(0.24f, 0.80f, 0.48f, 0.45f);
        craftQueueHudFill.raycastTarget = false;

        GameObject txtObj = new GameObject("QueueText");
        txtObj.transform.SetParent(craftQueueHudObj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(12f, 0f);
        tRT.offsetMax = new Vector2(-42f, 0f);
        craftQueueHudText = txtObj.AddComponent<Text>();
        ConfigureHDText(craftQueueHudText, 11, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        craftQueueHudText.text = "FABRICANDO...";

        GameObject cancelBtnObj = new GameObject("Btn_CancelCraftHud");
        cancelBtnObj.transform.SetParent(craftQueueHudObj.transform, false);
        RectTransform cRT = cancelBtnObj.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(1f, 0.5f);
        cRT.anchorMax = new Vector2(1f, 0.5f);
        cRT.pivot = new Vector2(1f, 0.5f);
        cRT.anchoredPosition = new Vector2(-6f, 0f);
        cRT.sizeDelta = new Vector2(28f, 26f);
        Image cBg = cancelBtnObj.AddComponent<Image>();
        cBg.sprite = FragaProIconLibrary.GetPillSprite();
        cBg.type = Image.Type.Sliced;
        cBg.color = new Color(0.62f, 0.18f, 0.18f, 0.94f);
        cancelBtnObj.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (CraftingSystem.Instance != null)
                CraftingSystem.Instance.CancelQueuedJob(0);
        });
        GameObject cIcoObj = new GameObject("XIcon");
        cIcoObj.transform.SetParent(cancelBtnObj.transform, false);
        cIcoObj.AddComponent<RectTransform>().sizeDelta = new Vector2(14f, 14f);
        Image cIco = cIcoObj.AddComponent<Image>();
        cIco.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.CancelCross);
        cIco.preserveAspect = true;
        cIco.raycastTarget = false;

        craftQueueHudObj.SetActive(false);
    }

    private void UpdateCraftQueueHUD()
    {
        if (craftQueueHudObj == null) return;

        bool modalBlocking = (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen) ||
                             (BlueprintPlacementManager.Instance != null && (BlueprintPlacementManager.Instance.IsPlacingGhost || BlueprintPlacementManager.Instance.IsBookOpen)) ||
                             (FragaOptionsMenu.Instance != null && FragaOptionsMenu.Instance.IsOpen) ||
                             SurvivalMapSystem.IsMapOpen ||
                             StorageChestInteractable.IsAnyChestOpen ||
                             UIManager.IsEndgameModalOpen;

        CraftingSystem craft = CraftingSystem.Instance;
        if (modalBlocking || craft == null || !craft.HasQueuedJobs)
        {
            if (craftQueueHudObj.activeSelf) craftQueueHudObj.SetActive(false);
            return;
        }

        if (!craftQueueHudObj.activeSelf) craftQueueHudObj.SetActive(true);

        if (craftQueueHudRT != null)
        {
            bool bossVisible = bossBarRootObj != null && bossBarRootObj.activeSelf;
            craftQueueHudRT.anchoredPosition = new Vector2(0f, bossVisible ? -134f : -92f);
        }

        CraftQueueJob job = craft.CurrentJob;
        float prog = craft.CurrentJobProgress01;
        bool paused = craft.IsCurrentJobPausedByStation;
        int queueCount = craft.CraftQueue.Count;
        int pct = Mathf.RoundToInt(prog * 100f);

        if (craftQueueHudFill != null)
        {
            craftQueueHudFill.fillAmount = prog;
            craftQueueHudFill.color = paused
                ? new Color(0.95f, 0.68f, 0.22f, 0.45f)
                : new Color(0.24f, 0.80f, 0.48f, 0.45f);
        }

        string sig = $"{job.recipeIndex}_{pct}_{paused}_{queueCount}_{job.remaining:F1}";
        if (sig != lastCraftHudSignature && craftQueueHudText != null)
        {
            lastCraftHudSignature = sig;
            string extra = queueCount > 1 ? $" (+{queueCount - 1})" : string.Empty;
            craftQueueHudText.text = paused
                ? $"PAUSADO{extra}: {job.recipeName} (Volte p/ estacao)"
                : $"FABRICANDO{extra}: {job.recipeName} ({pct}% | {job.remaining:F1}s)";
        }
    }

    private void CreateObjectiveTracker()
    {
        objectiveRootObj = new GameObject("ObjectiveTracker");
        objectiveRootObj.transform.SetParent(hudRoot, false);
        RectTransform rt = objectiveRootObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -15f);
        rt.sizeDelta = new Vector2(410f, 68f);

        Image bg = objectiveRootObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.07f, 0.11f, 0.84f);
        bg.raycastTarget = false;

        Outline outline = objectiveRootObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.78f, 0.92f, 0.42f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        GameObject icoObj = new GameObject("ObjIcon");
        icoObj.transform.SetParent(objectiveRootObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(10f, 0f);
        iRT.sizeDelta = new Vector2(26f, 26f);
        Image icoImg = icoObj.AddComponent<Image>();
        icoImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.MapCompass);
        icoImg.preserveAspect = true;
        icoImg.raycastTarget = false;

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(objectiveRootObj.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0.52f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.offsetMin = new Vector2(44f, 0f);
        tRT.offsetMax = new Vector2(-10f, -6f);
        objectiveTitleText = titleObj.AddComponent<Text>();
        ConfigureHDText(objectiveTitleText, 12, TextAnchor.MiddleLeft, new Color(0.42f, 0.92f, 1.0f), FontStyle.Bold);

        GameObject detailObj = new GameObject("Detail");
        detailObj.transform.SetParent(objectiveRootObj.transform, false);
        RectTransform dRT = detailObj.AddComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0f, 0f);
        dRT.anchorMax = new Vector2(1f, 0.54f);
        dRT.offsetMin = new Vector2(44f, 6f);
        dRT.offsetMax = new Vector2(-10f, 0f);
        objectiveDetailText = detailObj.AddComponent<Text>();
        ConfigureHDText(objectiveDetailText, 11, TextAnchor.MiddleLeft, new Color(0.92f, 0.95f, 0.98f), FontStyle.Normal);
        objectiveDetailText.horizontalOverflow = HorizontalWrapMode.Wrap;
        objectiveDetailText.verticalOverflow = VerticalWrapMode.Truncate;

        objectiveRootObj.SetActive(false);
    }

    private void CreateResourceCounterBar()
    {
        GameObject barObj = new GameObject("LiveResourceCounterBar");
        barObj.transform.SetParent(hudRoot, false);
        resourceCounterBarRT = barObj.AddComponent<RectTransform>();
        resourceCounterBarRT.anchorMin = new Vector2(0f, 1f);
        resourceCounterBarRT.anchorMax = new Vector2(0f, 1f);
        resourceCounterBarRT.pivot = new Vector2(0f, 1f);
        resourceCounterBarRT.anchoredPosition = new Vector2(24f, -15f);
        resourceCounterBarRT.sizeDelta = new Vector2(410f, 38f);

        Image bg = barObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.08f, 0.12f, 0.85f);
        bg.raycastTarget = false;

        Outline outline = barObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.32f, 0.72f, 0.52f, 0.45f);
        outline.effectDistance = new Vector2(1.1f, -1.1f);

        float cellW = 100f;
        for (int i = 0; i < 4; i++)
        {
            GameObject cell = new GameObject($"ResCell_{trackedResourceNames[i]}");
            cell.transform.SetParent(barObj.transform, false);
            RectTransform cRT = cell.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 0f);
            cRT.anchorMax = new Vector2(0f, 1f);
            cRT.pivot = new Vector2(0f, 0.5f);
            cRT.anchoredPosition = new Vector2(6f + i * cellW, 0f);
            cRT.sizeDelta = new Vector2(cellW - 4f, 0f);

            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(cell.transform, false);
            RectTransform iRT = icoObj.AddComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0f, 0.5f);
            iRT.anchorMax = new Vector2(0f, 0.5f);
            iRT.pivot = new Vector2(0f, 0.5f);
            iRT.anchoredPosition = new Vector2(2f, 0f);
            iRT.sizeDelta = new Vector2(22f, 22f);
            Image ico = icoObj.AddComponent<Image>();
            ico.sprite = FragaProIconLibrary.GetItemIcon(trackedResourceIds[i]);
            ico.preserveAspect = true;
            ico.raycastTarget = false;

            GameObject txtObj = new GameObject("Count");
            txtObj.transform.SetParent(cell.transform, false);
            RectTransform tRT = txtObj.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(26f, 0f);
            tRT.offsetMax = new Vector2(-2f, 0f);
            Text txt = txtObj.AddComponent<Text>();
            ConfigureHDText(txt, 11, TextAnchor.MiddleLeft, new Color(0.95f, 0.96f, 0.98f), FontStyle.Bold);
            txt.text = $"{trackedResourceNames[i]}: 0";
            resourceCountTexts[i] = txt;
        }
    }

    public void RefreshResourceCounterBar()
    {
        EnsureInventorySubscribed();
        if (inventoryCache == null) return;

        for (int i = 0; i < 4; i++)
        {
            if (resourceCountTexts[i] == null) continue;
            int cnt = inventoryCache.GetItemCount(trackedResourceIds[i]);
            if (cnt != lastTrackedCounts[i])
            {
                lastTrackedCounts[i] = cnt;
                resourceCountTexts[i].text = $"{trackedResourceNames[i]}: {cnt}";
                resourceCountTexts[i].color = cnt > 0
                    ? new Color(0.55f, 0.98f, 0.72f)
                    : new Color(0.72f, 0.76f, 0.80f);
            }
        }
        RefreshPinnedBlueprintTracker();
    }

    private void CreatePinnedBlueprintTracker()
    {
        pinnedBlueprintObj = new GameObject("PinnedBlueprintHUD");
        pinnedBlueprintObj.transform.SetParent(hudRoot, false);
        RectTransform rt = pinnedBlueprintObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(448f, -15f);
        rt.sizeDelta = new Vector2(304f, 38f);

        Image bg = pinnedBlueprintObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.10f, 0.14f, 0.88f);
        bg.raycastTarget = true;

        Outline outl = pinnedBlueprintObj.AddComponent<Outline>();
        outl.effectColor = new Color(0.96f, 0.76f, 0.28f, 0.58f);
        outl.effectDistance = new Vector2(1.1f, -1.1f);

        Button pinBtn = pinnedBlueprintObj.AddComponent<Button>();
        pinBtn.onClick.AddListener(() =>
        {
            if (BlueprintPlacementManager.Instance != null && pinnedProjectIndex >= 0)
            {
                BlueprintPlacementManager.Instance.StartPlacement(pinnedProjectIndex);
            }
        });

        GameObject icoObj = new GameObject("PinIcon");
        icoObj.transform.SetParent(pinnedBlueprintObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(8f, 0f);
        iRT.sizeDelta = new Vector2(18f, 18f);
        Image ico = icoObj.AddComponent<Image>();
        ico.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.BookmarkPin);
        ico.preserveAspect = true;
        ico.raycastTarget = false;

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(pinnedBlueprintObj.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0.52f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.offsetMin = new Vector2(30f, 0f);
        tRT.offsetMax = new Vector2(-8f, -2f);
        pinnedBlueprintTitleText = titleObj.AddComponent<Text>();
        ConfigureHDText(pinnedBlueprintTitleText, 10, TextAnchor.MiddleLeft, new Color(1.0f, 0.88f, 0.42f), FontStyle.Bold);

        GameObject matsObj = new GameObject("Mats");
        matsObj.transform.SetParent(pinnedBlueprintObj.transform, false);
        RectTransform mRT = matsObj.AddComponent<RectTransform>();
        mRT.anchorMin = new Vector2(0f, 0.08f);
        mRT.anchorMax = new Vector2(1f, 0.52f);
        mRT.offsetMin = new Vector2(30f, 4f);
        mRT.offsetMax = new Vector2(-8f, 0f);
        pinnedBlueprintMatsText = matsObj.AddComponent<Text>();
        ConfigureHDText(pinnedBlueprintMatsText, 10, TextAnchor.MiddleLeft, new Color(0.92f, 0.96f, 0.98f), FontStyle.Bold);

        GameObject barBg = new GameObject("BarBG");
        barBg.transform.SetParent(pinnedBlueprintObj.transform, false);
        RectTransform bRT = barBg.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 0f);
        bRT.anchorMax = new Vector2(1f, 0f);
        bRT.pivot = new Vector2(0.5f, 0f);
        bRT.offsetMin = new Vector2(14f, 2f);
        bRT.offsetMax = new Vector2(-14f, 5f);
        barBg.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.18f, 0.92f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barBg.transform, false);
        RectTransform fRT = fillObj.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero;
        fRT.offsetMax = Vector2.zero;
        pinnedBlueprintFill = fillObj.AddComponent<Image>();
        pinnedBlueprintFill.sprite = FragaProIconLibrary.GetBarFillSprite();
        pinnedBlueprintFill.type = Image.Type.Filled;
        pinnedBlueprintFill.fillMethod = Image.FillMethod.Horizontal;
        pinnedBlueprintFill.color = new Color(0.32f, 0.92f, 0.56f, 0.95f);
        pinnedBlueprintFill.raycastTarget = false;

        pinnedBlueprintObj.SetActive(false);
    }

    public void SetPinnedBlueprint(int projectIndex, string structureName, int reqWood, int reqStone)
    {
        pinnedProjectIndex = projectIndex;
        pinnedProjectName = structureName ?? string.Empty;
        pinnedReqWood = Mathf.Max(0, reqWood);
        pinnedReqStone = Mathf.Max(0, reqStone);
        lastPinnedWood = -1;
        lastPinnedStone = -1;
        RefreshPinnedBlueprintTracker();
    }

    public void ClearPinnedBlueprint()
    {
        pinnedProjectIndex = -1;
        pinnedProjectName = string.Empty;
        if (pinnedBlueprintObj != null) pinnedBlueprintObj.SetActive(false);
    }

    private void RefreshPinnedBlueprintTracker()
    {
        if (pinnedBlueprintObj == null) return;
        if (pinnedProjectIndex < 0 || string.IsNullOrEmpty(pinnedProjectName))
        {
            if (pinnedBlueprintObj.activeSelf) pinnedBlueprintObj.SetActive(false);
            return;
        }

        if (!pinnedBlueprintObj.activeSelf) pinnedBlueprintObj.SetActive(true);
        InventoryAdvanced inv = inventoryCache != null ? inventoryCache : InventoryAdvanced.Instance;
        int wood = (inv != null ? inv.GetItemCount(100) : 0) +
                   (LogCarryingSystem.Instance != null ? LogCarryingSystem.Instance.currentLogs : 0);
        int stone = inv != null ? inv.GetItemCount(101) : 0;
        if (wood == lastPinnedWood && stone == lastPinnedStone) return;

        lastPinnedWood = wood;
        lastPinnedStone = stone;

        float wRatio = pinnedReqWood > 0 ? Mathf.Clamp01((float)wood / pinnedReqWood) : 1f;
        float sRatio = pinnedReqStone > 0 ? Mathf.Clamp01((float)stone / pinnedReqStone) : 1f;
        float totalRatio = pinnedReqStone > 0 ? (wRatio * 0.5f + sRatio * 0.5f) : wRatio;
        bool ready = wood >= pinnedReqWood && stone >= pinnedReqStone;

        if (pinnedBlueprintTitleText != null)
            pinnedBlueprintTitleText.text = ready ? $"PRONTO P/ OBRA: {pinnedProjectName.ToUpper()}" : $"FIXADO: {pinnedProjectName.ToUpper()}";
        if (pinnedBlueprintMatsText != null)
            pinnedBlueprintMatsText.text = pinnedReqStone > 0
                ? $"Mad {wood}/{pinnedReqWood}  |  Ped {stone}/{pinnedReqStone}"
                : $"Mad {wood}/{pinnedReqWood}";
        if (pinnedBlueprintFill != null)
        {
            pinnedBlueprintFill.fillAmount = totalRatio;
            pinnedBlueprintFill.color = ready ? new Color(0.32f, 0.94f, 0.56f) : new Color(0.96f, 0.74f, 0.24f);
        }
    }

    private void CreateCoopPartyHUD()
    {
        coopPartyHudObj = new GameObject("CoopPartyHUD");
        coopPartyHudObj.transform.SetParent(hudRoot, false);
        RectTransform rt = coopPartyHudObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -330f);
        rt.sizeDelta = new Vector2(244f, 194f);

        Image bg = coopPartyHudObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.08f, 0.12f, 0.84f);
        bg.raycastTarget = true;

        Outline outl = coopPartyHudObj.AddComponent<Outline>();
        outl.effectColor = new Color(0.32f, 0.84f, 0.96f, 0.45f);
        outl.effectDistance = new Vector2(1.1f, -1.1f);

        coopPartyHudObj.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (SurvivalInventoryCraftUI.Instance != null)
                SurvivalInventoryCraftUI.Instance.OpenCoopTab();
        });

        GameObject hdrObj = new GameObject("Header");
        hdrObj.transform.SetParent(coopPartyHudObj.transform, false);
        RectTransform hRT = hdrObj.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = new Vector2(0f, -4f);
        hRT.sizeDelta = new Vector2(-16f, 20f);
        coopPartyHeaderText = hdrObj.AddComponent<Text>();
        ConfigureHDText(coopPartyHeaderText, 11, TextAnchor.MiddleLeft, new Color(0.45f, 0.92f, 1f), FontStyle.Bold);
        coopPartyHeaderText.text = "ESQUADRAO CO-OP (FRAGA-1913)";

        for (int i = 0; i < coopHudRows.Length; i++)
        {
            GameObject row = new GameObject($"CoopMemberSlot_{i}");
            row.transform.SetParent(coopPartyHudObj.transform, false);
            RectTransform rRT = row.AddComponent<RectTransform>();
            rRT.anchorMin = new Vector2(0.5f, 1f);
            rRT.anchorMax = new Vector2(0.5f, 1f);
            rRT.pivot = new Vector2(0.5f, 1f);
            rRT.anchoredPosition = new Vector2(0f, -26f - i * 54f);
            rRT.sizeDelta = new Vector2(228f, 48f);

            Image rBg = row.AddComponent<Image>();
            rBg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
            rBg.type = Image.Type.Sliced;
            rBg.color = new Color(0.08f, 0.13f, 0.19f, 0.90f);
            rBg.raycastTarget = false;

            GameObject barAccent = new GameObject("Accent");
            barAccent.transform.SetParent(row.transform, false);
            RectTransform aRT = barAccent.AddComponent<RectTransform>();
            aRT.anchorMin = new Vector2(0f, 0.1f);
            aRT.anchorMax = new Vector2(0f, 0.9f);
            aRT.pivot = new Vector2(0f, 0.5f);
            aRT.anchoredPosition = new Vector2(4f, 0f);
            aRT.sizeDelta = new Vector2(4f, 0f);
            Image accentImg = barAccent.AddComponent<Image>();
            accentImg.color = new Color(0.35f, 0.92f, 0.72f);
            accentImg.raycastTarget = false;

            GameObject nameObj = new GameObject("NameDist");
            nameObj.transform.SetParent(row.transform, false);
            RectTransform nRT = nameObj.AddComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0f, 0.50f);
            nRT.anchorMax = new Vector2(1f, 0.96f);
            nRT.offsetMin = new Vector2(14f, 0f);
            nRT.offsetMax = new Vector2(-6f, 0f);
            Text nameTxt = nameObj.AddComponent<Text>();
            ConfigureHDText(nameTxt, 11, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);

            GameObject hpBg = new GameObject("HPBG");
            hpBg.transform.SetParent(row.transform, false);
            RectTransform hbRT = hpBg.AddComponent<RectTransform>();
            hbRT.anchorMin = new Vector2(0f, 0.18f);
            hbRT.anchorMax = new Vector2(0f, 0.18f);
            hbRT.pivot = new Vector2(0f, 0.5f);
            hbRT.anchoredPosition = new Vector2(14f, 4f);
            hbRT.sizeDelta = new Vector2(124f, 8f);
            Image hbImg = hpBg.AddComponent<Image>();
            hbImg.color = new Color(0.12f, 0.16f, 0.20f, 1f);
            hbImg.raycastTarget = false;

            GameObject hpFillObj = new GameObject("HPFill");
            hpFillObj.transform.SetParent(hpBg.transform, false);
            RectTransform hfRT = hpFillObj.AddComponent<RectTransform>();
            hfRT.anchorMin = Vector2.zero;
            hfRT.anchorMax = Vector2.one;
            hfRT.offsetMin = Vector2.zero;
            hfRT.offsetMax = Vector2.zero;
            Image hpFillImg = hpFillObj.AddComponent<Image>();
            hpFillImg.sprite = FragaProIconLibrary.GetBarFillSprite();
            hpFillImg.type = Image.Type.Filled;
            hpFillImg.fillMethod = Image.FillMethod.Horizontal;
            hpFillImg.color = new Color(0.28f, 0.88f, 0.48f, 1f);
            hpFillImg.raycastTarget = false;

            GameObject alertObj = new GameObject("Alert");
            alertObj.transform.SetParent(row.transform, false);
            RectTransform alRT = alertObj.AddComponent<RectTransform>();
            alRT.anchorMin = new Vector2(1f, 0f);
            alRT.anchorMax = new Vector2(1f, 0.5f);
            alRT.pivot = new Vector2(1f, 0.5f);
            alRT.anchoredPosition = new Vector2(-8f, 4f);
            alRT.sizeDelta = new Vector2(78f, 18f);
            Text alertTxt = alertObj.AddComponent<Text>();
            ConfigureHDText(alertTxt, 10, TextAnchor.MiddleRight, new Color(0.66f, 0.96f, 0.78f), FontStyle.Bold);

            row.SetActive(false);
            coopHudRows[i] = new CoopHudMemberRow
            {
                root = row,
                accentBar = accentImg,
                nameDistText = nameTxt,
                hpFill = hpFillImg,
                alertText = alertTxt
            };
        }

        coopPartyHudObj.SetActive(false);
    }

    public void UpdateCoopPartyHUD()
    {
        if (coopPartyHudObj == null) return;
        FragaCoopSession coop = FragaCoopSession.Instance;
        bool modalBlocking = (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen) ||
                             (FragaOptionsMenu.Instance != null && FragaOptionsMenu.Instance.IsOpen) ||
                             SurvivalMapSystem.IsMapOpen ||
                             StorageChestInteractable.IsAnyChestOpen ||
                             UIManager.IsEndgameModalOpen;

        if (coop == null || !coop.ShouldShowPartyHUD || modalBlocking)
        {
            if (coopPartyHudObj.activeSelf) coopPartyHudObj.SetActive(false);
            return;
        }

        if (!coopPartyHudObj.activeSelf) coopPartyHudObj.SetActive(true);
        if (coopPartyHeaderText != null)
            coopPartyHeaderText.text = $"ESQUADRAO ({coop.CountConnectedMembers()}/{FragaCoopSession.MaxPartySize})  [{coop.sessionJoinCode}]";

        int slotIdx = 0;
        var members = coop.Members;
        for (int m = 0; m < members.Count && slotIdx < coopHudRows.Length; m++)
        {
            CoopMemberState st = members[m];
            if (st.isLocalPlayer) continue;
            var row = coopHudRows[slotIdx];
            if (row.root != null) row.root.SetActive(true);
            if (row.accentBar != null && ColorUtility.TryParseHtmlString(st.colorHex, out Color c))
                row.accentBar.color = c;
            float dist = coop.GetDistanceToMember(in st);
            if (row.nameDistText != null)
                row.nameDistText.text = $"{st.displayName}  ({dist:F0}m)";
            float hpNorm = st.maxHealth > 0f ? Mathf.Clamp01(st.health / st.maxHealth) : 1f;
            if (row.hpFill != null)
            {
                row.hpFill.fillAmount = hpNorm;
                row.hpFill.color = hpNorm < 0.35f ? new Color(0.96f, 0.30f, 0.24f) : new Color(0.28f, 0.88f, 0.48f);
            }
            if (row.alertText != null)
            {
                string alert = st.isDowned ? "CAIDO!"
                    : st.isBleeding ? "SANGUE"
                    : st.isPoisoned ? "VENENO"
                    : st.isHypothermic ? "FRIO"
                    : "ESTAVEL";
                row.alertText.text = alert;
                row.alertText.color = (st.isDowned || st.isBleeding || st.isPoisoned)
                    ? new Color(1f, 0.42f, 0.34f)
                    : (st.isHypothermic ? new Color(0.48f, 0.88f, 1f) : new Color(0.62f, 0.96f, 0.76f));
            }
            slotIdx++;
        }

        for (int i = slotIdx; i < coopHudRows.Length; i++)
        {
            if (coopHudRows[i].root != null && coopHudRows[i].root.activeSelf)
                coopHudRows[i].root.SetActive(false);
        }
    }
    public void SetObjectiveDisplay(bool visible, string title, string detail)
    {
        if (objectiveRootObj == null) return;
        objectiveRootObj.SetActive(visible);
        if (resourceCounterBarRT != null)
        {
            resourceCounterBarRT.anchoredPosition = visible ? new Vector2(24f, -88f) : new Vector2(24f, -15f);
        }
        if (collectionFeedRootRT != null)
        {
            collectionFeedRootRT.anchoredPosition = visible ? new Vector2(24f, -136f) : new Vector2(24f, -62f);
        }
        if (!visible) return;
        if (objectiveTitleText != null) objectiveTitleText.text = title;
        if (objectiveDetailText != null) objectiveDetailText.text = detail;
    }

    private void RefreshWeaponIndicatorFromInventory()
    {
        lastWeaponShownId = int.MinValue;
        lastWeaponDurPct = -2;
        lastWeaponAmmo = -2;
        UpdateWeaponTelemetry();
    }

    private void UpdateWeaponTelemetry()
    {
        if (inventoryCache == null) return;
        int id = inventoryCache.equippedMainHandId;
        bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;

        int durPct = -1;
        ItemData d = null;
        if (id > 0)
        {
            d = ItemDatabase.GetItem(id);
            if (d != null && !d.isPerishable && d.maxDurability > 0f)
            {
                int slotIdx = inventoryCache.FindUsableEquipmentSlot(id);
                if (inventoryCache.slots != null && slotIdx >= 0 && slotIdx < inventoryCache.slots.Length)
                {
                    float rawDur = inventoryCache.slots[slotIdx].durability < 0f
                        ? d.maxDurability
                        : inventoryCache.slots[slotIdx].durability;
                    durPct = Mathf.Clamp(Mathf.RoundToInt((rawDur / d.maxDurability) * 100f), 0, 100);
                }
            }
        }

        int ammo = -1;
        string ammoTag = string.Empty;
        if (id == 305)
        {
            ammo = inventoryCache.GetItemCount(117) + inventoryCache.GetItemCount(110);
            ammoTag = "FL";
        }
        else if (id == 314)
        {
            ammo = inventoryCache.GetItemCount(101);
            ammoTag = "PD";
        }
        else if (sanitySystem != null && (inventoryCache.equippedOffHandId == 304 || (playerController != null && playerController.IsLighterOn)))
        {
            ammo = Mathf.Clamp(Mathf.RoundToInt(sanitySystem.oil), 0, 100);
            ammoTag = "OLEO";
        }

        if (id == lastWeaponShownId &&
            durPct == lastWeaponDurPct &&
            ammo == lastWeaponAmmo &&
            touchActive == lastWeaponLayoutTouch)
        {
            return;
        }

        lastWeaponShownId = id;
        lastWeaponDurPct = durPct;
        lastWeaponAmmo = ammo;
        lastWeaponLayoutTouch = touchActive;

        if (id <= 0)
        {
            string unarmedLabel = ammoTag == "OLEO" ? $"PUNHOS | LUZ {ammo}%" : "PUNHOS (DESARMADO)";
            SetWeaponDisplay(unarmedLabel);
            if (weaponText != null)
                weaponText.color = (ammoTag == "OLEO" && ammo <= 15) ? new Color(1f, 0.45f, 0.36f, 1f) : new Color(0.95f, 0.95f, 0.95f, 0.95f);
            if (weaponIconImg != null)
                weaponIconImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.AttackFist);
        }
        else
        {
            string baseName = d != null ? d.itemName : $"ITEM {id}";
            string durPart = durPct >= 0 ? $" {durPct}%" : string.Empty;
            string ammoPart = ammo >= 0 ? (ammoTag == "OLEO" ? $" | LUZ {ammo}%" : $" | {ammo} {ammoTag}") : string.Empty;
            string suffix = (!touchActive && (id == 300 || id == 312 || id == 301)) ? " [CLIQUE]" : "";
            SetWeaponDisplay(baseName + durPart + ammoPart + suffix);
            if (weaponText != null)
                weaponText.color = ((durPct >= 0 && durPct <= 20) || (ammoTag != "" && ammo <= 0))
                    ? new Color(1f, 0.45f, 0.36f, 1f)
                    : new Color(0.95f, 0.95f, 0.95f, 0.95f);
            if (weaponIconImg != null)
                weaponIconImg.sprite = FragaProIconLibrary.GetItemIcon(id);
        }
    }

    private void Update()
    {
        UpdateHitMarker();
        UpdateCrosshairVisual();
        UpdateTooltip();
        UpdateItemCollectionFeed();

        if (Time.unscaledTime < nextUpdateTime) return;
        nextUpdateTime = Time.unscaledTime + updateInterval;

        EnsureInventorySubscribed();
        SyncAdaptivePositions(false);
        UpdateStatusBars();
        UpdateStatusEffects();
        UpdateCompass();
        UpdateBossHealthBar();
        UpdateClock();
        UpdateDamageIndicators();
        UpdateWeaponTelemetry();
        UpdateBonfireIndicator();
        UpdateStealthIndicator();
        UpdateBlueprintCard();
        UpdateBonfireCard();
        UpdateCraftQueueHUD();
        RefreshResourceCounterBar();

        if (Time.unscaledTime >= nextCoopHudRefreshTime)
        {
            nextCoopHudRefreshTime = Time.unscaledTime + 0.25f;
            UpdateCoopPartyHUD();
        }
    }

    private void UpdateHitMarker()
    {
        if (hitMarkerGroup == null || hitMarkerTimer <= 0f) return;
        hitMarkerTimer -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(hitMarkerTimer / 0.18f);
        hitMarkerGroup.alpha = t;
        if (hitMarkerRT != null)
        {
            hitMarkerRT.localScale = Vector3.Lerp(Vector3.one, hitMarkerRT.localScale, t);
        }
    }

    public void SetCrosshairContext(CrosshairContext ctx)
    {
        switch (ctx)
        {
            case CrosshairContext.Tree:
                targetCrosshairColor = new Color(0.96f, 0.72f, 0.30f, 0.92f);
                targetRingColor = new Color(0.96f, 0.72f, 0.30f, 0.65f);
                targetRingScale = 1.15f;
                break;
            case CrosshairContext.Rock:
                targetCrosshairColor = new Color(0.78f, 0.86f, 0.96f, 0.94f);
                targetRingColor = new Color(0.72f, 0.84f, 0.98f, 0.68f);
                targetRingScale = 1.18f;
                break;
            case CrosshairContext.Interactable:
                targetCrosshairColor = new Color(0.32f, 0.96f, 0.90f, 0.95f);
                targetRingColor = new Color(0.32f, 0.96f, 0.90f, 0.75f);
                targetRingScale = 1.28f;
                break;
            case CrosshairContext.Hostile:
                targetCrosshairColor = new Color(1.0f, 0.28f, 0.28f, 0.98f);
                targetRingColor = new Color(1.0f, 0.24f, 0.24f, 0.80f);
                targetRingScale = 1.35f;
                break;
            default:
                targetCrosshairColor = new Color(1f, 1f, 1f, 0.58f);
                targetRingColor = new Color(1f, 1f, 1f, 0f);
                targetRingScale = 0.85f;
                break;
        }
    }

    private void UpdateCrosshairVisual()
    {
        float lerp = Time.unscaledDeltaTime * 14f;
        Color desiredDot = targetCrosshairColor;
        Color desiredRing = targetRingColor;
        float desiredScale = targetRingScale;

        if (BowAndArrowSystem.Instance != null && BowAndArrowSystem.Instance.IsAiming)
        {
            float charge = Mathf.Clamp01(BowAndArrowSystem.Instance.DrawProgress);
            desiredScale = Mathf.Lerp(1.55f, 0.62f, charge);
            desiredRing = charge >= 0.95f ? new Color(1.0f, 0.86f, 0.25f, 0.96f) : new Color(0.38f, 0.94f, 1.0f, 0.85f);
            desiredDot = desiredRing;
        }
        else if (CelticSlingSystem.Instance != null && CelticSlingSystem.Instance.IsWindingUp)
        {
            float charge = Mathf.Clamp01(CelticSlingSystem.Instance.Windup01);
            desiredScale = Mathf.Lerp(1.55f, 0.62f, charge);
            desiredRing = charge >= 0.95f ? new Color(1.0f, 0.86f, 0.25f, 0.96f) : new Color(0.95f, 0.72f, 0.35f, 0.85f);
            desiredDot = desiredRing;
        }

        if (crosshairDot != null)
        {
            crosshairDot.color = Color.Lerp(crosshairDot.color, desiredDot, lerp);
        }
        if (crosshairRing != null)
        {
            crosshairRing.color = Color.Lerp(crosshairRing.color, desiredRing, lerp);
        }
        if (crosshairRingRT != null)
        {
            float s = Mathf.Lerp(crosshairRingRT.localScale.x, desiredScale, lerp);
            crosshairRingRT.localScale = new Vector3(s, s, 1f);
        }

        InteractionDetector det = InteractionDetector.Instance;
        bool holding = det != null && det.IsHoldInProgress && det.HoldProgress01 > 0.001f;
        float hold01 = holding ? Mathf.Clamp01(det.HoldProgress01) : 0f;

        if (crosshairHoldRing != null)
        {
            crosshairHoldRing.fillAmount = hold01;
            crosshairHoldRing.color = holding
                ? Color.Lerp(new Color(0.32f, 0.96f, 0.88f, 0.95f), new Color(1.0f, 0.86f, 0.30f, 1.0f), hold01)
                : new Color(0.32f, 0.96f, 0.88f, 0f);
            if (crosshairHoldRingRT != null)
                crosshairHoldRingRT.localScale = Vector3.one * Mathf.Lerp(1.14f, 0.94f, hold01);
        }

        if (interactHoldFillBar != null)
        {
            interactHoldFillBar.fillAmount = hold01;
            interactHoldFillBar.color = Color.Lerp(new Color(0.32f, 0.96f, 0.88f, 0.95f), new Color(1.0f, 0.86f, 0.30f, 1.0f), hold01);
        }

        if (interactPromptText != null && interactPromptObj != null && interactPromptObj.activeSelf)
        {
            if (holding && !string.IsNullOrEmpty(det.HoldActionLabel))
            {
                interactPromptText.text = det.HoldActionLabel;
                wasShowingHoldAction = true;
            }
            else if (wasShowingHoldAction)
            {
                interactPromptText.text = cachedBasePromptLabel;
                wasShowingHoldAction = false;
            }
        }
    }

    private void UpdateStatusBars()
    {
        if (playerStatus == null) FindMissingReferences();

        if (playerStatus != null)
        {
            float lerpSpeed = Time.deltaTime * 12f;

            if (healthBar != null)
            {
                float hpNorm = Mathf.Clamp01(playerStatus.health / 100f);
                healthBar.fillAmount = Mathf.Lerp(healthBar.fillAmount, hpNorm, lerpSpeed);
                if (playerStatus.health <= 28f)
                {
                    float pulse = Mathf.Sin(Time.unscaledTime * 7f) * 0.5f + 0.5f;
                    healthBar.color = Color.Lerp(new Color(0.88f, 0.14f, 0.18f, 1f), new Color(1.0f, 0.44f, 0.26f, 1f), pulse);
                }
                else
                {
                    healthBar.color = new Color(0.92f, 0.22f, 0.24f, 1f);
                }
                if (healthText != null)
                {
                    healthText.text = $"{Mathf.Max(0, Mathf.CeilToInt(playerStatus.health))} / 100";
                    healthText.color = playerStatus.health <= 28f ? new Color(1f, 0.42f, 0.38f) : new Color(0.98f, 0.98f, 0.98f, 1f);
                }
            }
            if (hungerBar != null)
            {
                float hNorm = Mathf.Clamp01(playerStatus.hunger / 100f);
                hungerBar.fillAmount = Mathf.Lerp(hungerBar.fillAmount, hNorm, lerpSpeed);
                if (dialLinearFills[0] != null) dialLinearFills[0].fillAmount = hungerBar.fillAmount;
                if (hungerText != null) hungerText.text = $"{(int)playerStatus.hunger}%";
            }
            if (thirstBar != null)
            {
                float tNorm = Mathf.Clamp01(playerStatus.thirst / 100f);
                thirstBar.fillAmount = Mathf.Lerp(thirstBar.fillAmount, tNorm, lerpSpeed);
                if (dialLinearFills[1] != null) dialLinearFills[1].fillAmount = thirstBar.fillAmount;
                if (thirstText != null) thirstText.text = $"{(int)playerStatus.thirst}%";
            }
            if (staminaBar != null)
            {
                staminaBar.fillAmount = Mathf.Lerp(staminaBar.fillAmount, Mathf.Clamp01(playerStatus.stamina / 100f), lerpSpeed);
                if (playerStatus.isExhausted)
                {
                    float pulse = Mathf.Sin(Time.unscaledTime * 8f) * 0.5f + 0.5f;
                    staminaBar.color = Color.Lerp(new Color(0.95f, 0.35f, 0.16f, 1f), new Color(1.0f, 0.72f, 0.22f, 1f), pulse);
                }
                else
                {
                    staminaBar.color = new Color(0.28f, 0.90f, 0.98f, 1.0f);
                }
                if (staminaText != null)
                {
                    int curStam = Mathf.Clamp(Mathf.RoundToInt(playerStatus.stamina), 0, 100);
                    int maxStam = Mathf.Clamp(Mathf.RoundToInt(playerStatus.EffectiveMaxStamina), 45, 100);
                    staminaText.text = maxStam < 100 ? $"{curStam}/{maxStam}" : $"{curStam}%";
                }
            }
            if (temperatureBar != null)
            {
                float tempNorm = Mathf.InverseLerp(28f, 38f, playerStatus.bodyTemperature);
                temperatureBar.fillAmount = Mathf.Lerp(temperatureBar.fillAmount, tempNorm, lerpSpeed);
                if (dialLinearFills[3] != null) dialLinearFills[3].fillAmount = temperatureBar.fillAmount;
                bool cold = playerStatus.bodyTemperature <= 35.0f;
                Color tempCol = cold ? new Color(0.32f, 0.80f, 1.0f) : new Color(1.0f, 0.52f, 0.28f);
                temperatureBar.color = tempCol;
                if (dialLinearFills[3] != null) dialLinearFills[3].color = tempCol;
                if (temperatureText != null)
                {
                    temperatureText.text = $"{playerStatus.bodyTemperature:F0}C";
                    temperatureText.color = cold ? new Color(0.55f, 0.88f, 1.0f) : new Color(0.95f, 0.95f, 0.95f, 0.95f);
                }
            }

            if (playerStatus.health < lastHealth - 1f) TriggerDamageIndicator();
            lastHealth = playerStatus.health;
        }

        if (sanitySystem != null && sanityBar != null)
        {
            sanityBar.fillAmount = Mathf.Lerp(sanityBar.fillAmount, Mathf.Clamp01(sanitySystem.sanity / 100f), Time.deltaTime * 12f);
            if (dialLinearFills[2] != null) dialLinearFills[2].fillAmount = sanityBar.fillAmount;
            if (sanityText != null) sanityText.text = $"{(int)sanitySystem.sanity}%";

            if (sanitySystem.sanity < 30f)
            {
                float pulse = Mathf.Sin(Time.time * 5f) * 0.5f + 0.5f;
                Color sCol = Color.Lerp(new Color(0.68f, 0.35f, 0.95f), new Color(0.95f, 0.15f, 0.15f), pulse);
                sanityBar.color = sCol;
                if (dialLinearFills[2] != null) dialLinearFills[2].color = sCol;
            }
            else
            {
                sanityBar.color = new Color(0.68f, 0.35f, 0.95f);
                if (dialLinearFills[2] != null) dialLinearFills[2].color = sanityBar.color;
            }

            if (sanitySystem.sanity < lastSanity - 2f) TriggerDamageIndicator();
            lastSanity = sanitySystem.sanity;
        }

        if (statusGroup != null)
        {
            bool hasTrauma = playerStatus != null && (playerStatus.isBleeding || playerStatus.isFractured || playerStatus.isPoisoned || playerStatus.isSick || playerStatus.bodyTemperature <= 35.0f);
            bool isUnderStress = hasTrauma || (playerStatus != null && (playerStatus.health < 95f || playerStatus.stamina < 85f || playerStatus.hunger < 75f || playerStatus.thirst < 75f || playerStatus.bodyTemperature <= 35.5f)) ||
                                 (sanitySystem != null && sanitySystem.sanity < 75f);
            float targetAlpha = isUnderStress ? 0.98f : 0.86f;
            statusGroup.alpha = Mathf.Lerp(statusGroup.alpha, targetAlpha, Time.deltaTime * 3.5f);

            if (vitalsCardOutline != null)
            {
                vitalsCardOutline.effectColor = hasTrauma
                    ? new Color(0.96f, 0.34f, 0.28f, 0.88f)
                    : new Color(0.28f, 0.74f, 0.90f, 0.48f);
            }
            if (statusHeaderBadgeText != null)
            {
                bool overLoaded = inventoryCache != null && inventoryCache.IsOverEncumbered;
                bool comfort = playerStatus != null && playerStatus.IsComfortRegenActive;
                if (hasTrauma)
                {
                    statusHeaderBadgeText.text = "ALERTA MEDICO [K]";
                    statusHeaderBadgeText.color = new Color(1.0f, 0.48f, 0.40f);
                }
                else if (overLoaded)
                {
                    statusHeaderBadgeText.text = "SOBRECARGA [K]";
                    statusHeaderBadgeText.color = new Color(1.0f, 0.74f, 0.30f);
                }
                else if (comfort)
                {
                    statusHeaderBadgeText.text = "REGEN. ATIVA [K]";
                    statusHeaderBadgeText.color = new Color(0.45f, 0.98f, 0.68f);
                }
                else
                {
                    statusHeaderBadgeText.text = "BIOMETRIA  [K]";
                    statusHeaderBadgeText.color = new Color(0.54f, 0.94f, 1.0f);
                }
            }
        }
    }

    private void UpdateStatusEffects()
    {
        if (playerStatus == null) return;

        if (iconPoison != null) iconPoison.gameObject.SetActive(playerStatus.isPoisoned);
        if (iconBleeding != null) iconBleeding.gameObject.SetActive(playerStatus.isBleeding);
        if (iconFracture != null) iconFracture.gameObject.SetActive(playerStatus.isFractured);
        if (iconHypothermia != null) iconHypothermia.gameObject.SetActive(playerStatus.bodyTemperature <= 35.0f);
        if (iconSickness != null) iconSickness.gameObject.SetActive(playerStatus.isSick);
    }

    private void UpdateCompass()
    {
        if (compassText == null) return;

        Camera cam = Camera.main;
        if (cam == null && playerController != null) cam = playerController.GetComponentInChildren<Camera>();
        if (cam == null) return;

        float heading = cam.transform.eulerAngles.y;
        string dir = "N";
        if (heading >= 337.5f || heading < 22.5f) dir = "N";
        else if (heading >= 22.5f && heading < 67.5f) dir = "NE";
        else if (heading >= 67.5f && heading < 112.5f) dir = "L";
        else if (heading >= 112.5f && heading < 157.5f) dir = "SE";
        else if (heading >= 157.5f && heading < 202.5f) dir = "S";
        else if (heading >= 202.5f && heading < 247.5f) dir = "SO";
        else if (heading >= 247.5f && heading < 292.5f) dir = "O";
        else if (heading >= 292.5f && heading < 337.5f) dir = "NO";

        compassSb.Clear();
        compassSb.Append("[ ");
        compassSb.Append(dir);
        compassSb.Append("  ");
        compassSb.Append((int)heading);
        compassSb.Append('°');

        Vector3 originPos = cam.transform.position;
        Vector3 fwdFlat = cam.transform.forward;
        fwdFlat.y = 0f;

        BossSystem boss = BossSystem.Instance;
        if (boss != null && boss.IsActive)
        {
            Vector3 toBoss = boss.BossPosition - originPos;
            toBoss.y = 0f;
            compassSb.Append(" | CHEFE ");
            compassSb.Append(Mathf.RoundToInt(toBoss.magnitude));
            compassSb.Append('m');
        }
        else if (FragaWorldBlueprint.IsInitialized && fwdFlat.sqrMagnitude > 0.0001f)
        {
            fwdFlat.Normalize();
            float bestAngle = 28f;
            string bestName = null;
            float bestDist = 0f;

            CheckCompassPoi(originPos, fwdFlat, FragaWorldBlueprint.CrashCampPos, "BASE", ref bestAngle, ref bestName, ref bestDist);
            CheckCompassPoi(originPos, fwdFlat, FragaWorldBlueprint.HighlandLakePos, "LAGO", ref bestAngle, ref bestName, ref bestDist);
            CheckCompassPoi(originPos, fwdFlat, FragaWorldBlueprint.SacredCavePos, "GRUTA", ref bestAngle, ref bestName, ref bestDist);
            CheckCompassPoi(originPos, fwdFlat, FragaWorldBlueprint.FarolBluffPos, "FAROL", ref bestAngle, ref bestName, ref bestDist);
            CheckCompassPoi(originPos, fwdFlat, FragaWorldBlueprint.CelticCastroPos, "CASTRO", ref bestAngle, ref bestName, ref bestDist);

            if (bestName != null)
            {
                compassSb.Append(" | ");
                compassSb.Append(bestName);
                compassSb.Append(' ');
                compassSb.Append(Mathf.RoundToInt(bestDist));
                compassSb.Append('m');
            }
        }

        compassSb.Append(" ]");
        compassText.text = compassSb.ToString();
    }

    private static void CheckCompassPoi(Vector3 originPos, Vector3 fwdFlat, Vector2 poiPos, string poiLabel,
        ref float bestAngle, ref string bestName, ref float bestDist)
    {
        float dx = poiPos.x - originPos.x;
        float dz = poiPos.y - originPos.z;
        float distSq = dx * dx + dz * dz;
        if (distSq < 64f) return;

        float dist = Mathf.Sqrt(distSq);
        float dot = Mathf.Clamp((fwdFlat.x * dx + fwdFlat.z * dz) / dist, -1f, 1f);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        if (angle <= bestAngle)
        {
            bestAngle = angle;
            bestName = poiLabel;
            bestDist = dist;
        }
    }

    private void UpdateBossHealthBar()
    {
        if (bossBarRootObj == null) return;
        BossSystem boss = BossSystem.Instance;
        if (boss == null || !boss.IsActive)
        {
            if (bossBarRootObj.activeSelf) bossBarRootObj.SetActive(false);
            return;
        }

        if (!bossBarRootObj.activeSelf) bossBarRootObj.SetActive(true);
        if (bossHpFill != null)
        {
            bossHpFill.fillAmount = boss.HealthPercentage;
            bossHpFill.color = boss.IsEnraged
                ? new Color(1.0f, 0.22f, 0.12f, 0.96f)
                : new Color(0.78f, 0.12f, 0.18f, 0.95f);
        }
        if (bossTitleText != null)
        {
            bossTitleText.text = boss.IsEnraged ? $"{boss.BossTitle} [ENFURECIDO]" : boss.BossTitle;
        }
        if (bossHpText != null)
        {
            bossHpText.text = $"{Mathf.CeilToInt(boss.currentHealth)}/{Mathf.RoundToInt(boss.maxHealth)} HP";
        }
    }

    private void UpdateClock()
    {
        if (dayNightCycle == null)
        {
            dayNightCycle = DayNightCycle.Instance != null ? DayNightCycle.Instance : FindAnyObjectByType<DayNightCycle>();
        }
        if (dayNightCycle == null) return;

        if (clockText != null)
        {
            float norm = dayNightCycle.timeNormalized;
            int totalMinutes = Mathf.Clamp(Mathf.FloorToInt(norm * 1440f + 0.001f), 0, 1439);
            int hours = (totalMinutes / 60) % 24;
            int minutes = totalMinutes % 60;

            bool isNight = dayNightCycle.isNight;
            string phase = isNight ? "NOITE" : "DIA";

            clockText.text = $"{hours:00}:{minutes:00}  {phase}";
            clockText.color = isNight ? new Color(0.68f, 0.80f, 1f) : new Color(1f, 0.90f, 0.62f);
        }

        if (dayCounterText != null)
        {
            dayCounterText.text = $"SOBREVIVENCIA - DIA {dayNightCycle.currentDay}";
        }
    }

    private void UpdateDamageIndicators()
    {
        float fadeSpeed = Time.deltaTime * 3f;
        for (int i = 0; i < 4; i++) damageAlphas[i] = Mathf.Max(0f, damageAlphas[i] - fadeSpeed);

        if (damageTop != null) damageTop.color = new Color(1, 0, 0, damageAlphas[0]);
        if (damageBottom != null) damageBottom.color = new Color(1, 0, 0, damageAlphas[1]);
        if (damageLeft != null) damageLeft.color = new Color(1, 0, 0, damageAlphas[2]);
        if (damageRight != null) damageRight.color = new Color(1, 0, 0, damageAlphas[3]);
    }

    private void UpdateTooltip()
    {
        if (lootTooltipGroup == null) return;
        float dt = Time.unscaledDeltaTime;
        if (tooltipFadeTimer > 0f)
        {
            tooltipFadeTimer -= dt;
            lootTooltipGroup.alpha = Mathf.Lerp(lootTooltipGroup.alpha, 1f, dt * 10f);
        }
        else
        {
            lootTooltipGroup.alpha = Mathf.Lerp(lootTooltipGroup.alpha, 0f, dt * 6f);
        }
    }

    private void UpdateBonfireIndicator()
    {
        if (bonfireIndicator == null || playerStatus == null) return;
        bool hideForCard = bonfireCardObj != null && bonfireCardObj.activeSelf;
        bonfireIndicator.gameObject.SetActive(playerStatus.isNearBonfire && !hideForCard);
    }

    private void UpdateStealthIndicator()
    {
        if (stealthIndicatorObj == null || stealthIndicatorText == null) return;
        var stealth = FragaStealthSystem.Instance;
        if (stealth == null)
        {
            stealthIndicatorObj.SetActive(false);
            return;
        }

        bool crouch = playerController != null && playerController.isCrouching;
        bool loud = stealth.Noise01 > 0.68f;
        if (!crouch && !loud)
        {
            stealthIndicatorObj.SetActive(false);
            return;
        }

        stealthIndicatorObj.SetActive(true);
        if (loud)
        {
            stealthIndicatorText.text = "RUIDO ALTO";
            stealthIndicatorText.color = new Color(1.0f, 0.42f, 0.34f);
        }
        else if (stealth.IsHidden)
        {
            stealthIndicatorText.text = "FURTIVO: OCULTO";
            stealthIndicatorText.color = new Color(0.42f, 0.95f, 0.78f);
        }
        else
        {
            stealthIndicatorText.text = "FURTIVO: VISIVEL";
            stealthIndicatorText.color = new Color(0.98f, 0.84f, 0.42f);
        }
    }

    public void ShowLootTooltip(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (lootTooltipText != null)
        {
            lootTooltipText.text = text;
            tooltipFadeTimer = 3.8f;
            if (lootTooltipGroup != null && lootTooltipGroup.alpha < 0.85f)
            {
                lootTooltipGroup.alpha = 1f;
            }
            RefreshResourceCounterBar();
        }
    }

    public void SetInteractionPrompt(bool visible, string label, FragaProIconLibrary.ControlIcon icon, bool requiresHold = false)
    {
        if (interactPromptObj == null) return;
        interactPromptObj.SetActive(visible);
        if (!visible)
        {
            cachedBasePromptLabel = string.Empty;
            wasShowingHoldAction = false;
            if (interactHoldFillBar != null) interactHoldFillBar.fillAmount = 0f;
            return;
        }

        bool touchActive = FragaInputRouter.Instance == null
            ? Input.touchCount > 0
            : FragaInputRouter.Instance.IsTouchControlsActive;

        string displayLabel = touchActive ? StripDesktopKeyHints(label) : label;
        cachedBasePromptLabel = displayLabel;
        if (interactPromptText != null && !wasShowingHoldAction)
            interactPromptText.text = displayLabel;
        if (interactPromptIcon != null) interactPromptIcon.sprite = FragaProIconLibrary.GetControlIcon(icon);
        if (interactKeyBadgeText != null)
        {
            interactKeyBadgeText.transform.parent.gameObject.SetActive(true);
            if (requiresHold)
                interactKeyBadgeText.text = touchActive ? "SEGURE" : "SEGURE E";
            else
                interactKeyBadgeText.text = touchActive
                    ? "TOQUE"
                    : (label.Contains("DIREITO") ? "RMB" : (label.Contains("CLIQUE") ? "LMB" : "E"));
        }
        if (interactKeyBadgeRT != null)
            interactKeyBadgeRT.sizeDelta = new Vector2(requiresHold ? 74f : 46f, 24f);
        if (interactKeyBadgeBg != null)
            interactKeyBadgeBg.color = requiresHold
                ? new Color(0.20f, 0.56f, 0.62f, 0.96f)
                : new Color(0.18f, 0.42f, 0.58f, 0.92f);
        if (interactPromptText != null)
            interactPromptText.rectTransform.offsetMax = new Vector2(requiresHold ? -92f : -64f, 0f);
    }

    private static string StripDesktopKeyHints(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace(" [CLIQUE DO MOUSE OU E]", "")
            .Replace(" [CLIQUE]", "")
            .Replace(" [E]", "")
            .Replace("[E] ", "")
            .Trim();
    }

    public void TriggerDamageIndicator(int direction = -1)
    {
        if (direction < 0)
        {
            for (int i = 0; i < 4; i++) damageAlphas[i] = 0.6f;
        }
        else
        {
            damageAlphas[Mathf.Clamp(direction, 0, 3)] = 0.8f;
        }
    }

    public void ShowDamageFromDirection(Vector3 sourceWorldPosition)
    {
        Transform refTransform = null;
        if (Camera.main != null) refTransform = Camera.main.transform;
        else if (playerController != null) refTransform = playerController.transform;
        else if (PlayerController.Instance != null) refTransform = PlayerController.Instance.transform;

        if (refTransform == null)
        {
            TriggerDamageIndicator(-1);
            return;
        }

        Vector3 fwd = refTransform.forward;
        fwd.y = 0f;
        Vector3 toSource = sourceWorldPosition - refTransform.position;
        toSource.y = 0f;

        if (fwd.sqrMagnitude < 0.0001f || toSource.sqrMagnitude < 0.0001f)
        {
            TriggerDamageIndicator(-1);
            return;
        }

        float angle = Vector3.SignedAngle(fwd.normalized, toSource.normalized, Vector3.up);
        if (angle >= -45f && angle <= 45f) TriggerDamageIndicator(0);
        else if (angle > 45f && angle <= 135f) TriggerDamageIndicator(3);
        else if (angle < -45f && angle >= -135f) TriggerDamageIndicator(2);
        else TriggerDamageIndicator(1);
    }

    public void SetWeaponDisplay(string weaponName)
    {
        if (weaponText != null) weaponText.text = weaponName.ToUpper();
    }
}
