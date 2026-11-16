using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// HUDSurvivalOverlay: HUD AAA de Sobrevivencia Responsivo (Zero-Emoji, Icones Vetoriais Profissionais).
/// Inclui anel de vida/estamina, 4 micro-dials (Fome, Sede, Sanidade, Temperatura),
/// hit-marker tatico no reticulo e adaptacao Mobile (Touch) <-> Desktop (PC).
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

    private Image iconPoison;
    private Image iconBleeding;
    private Image iconFracture;
    private Image iconHypothermia;

    private RectTransform compassContainer;
    private Text compassText;

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

    private Image weaponIconImg;
    private Text weaponText;

    private Image bonfireIndicator;
    private GameObject stealthIndicatorObj;
    private Text stealthIndicatorText;

    private Image crosshairDot;
    private Image crosshairRing;
    private RectTransform crosshairRingRT;
    private CanvasGroup hitMarkerGroup;
    private RectTransform hitMarkerRT;
    private readonly Image[] hitMarkerTicks = new Image[4];
    private float hitMarkerTimer = 0f;
    private Color hitMarkerColor = Color.white;
    private Color targetCrosshairColor = new Color(1f, 1f, 1f, 0.58f);
    private Color targetRingColor = new Color(1f, 1f, 1f, 0f);
    private float targetRingScale = 1.0f;

    // Pilula de Interacao Contextual
    private GameObject interactPromptObj;
    private Image interactPromptIcon;
    private Text interactPromptText;
    private Text interactKeyBadgeText;

    // Rastreador de Objetivos Iniciais (Fase 1)
    private GameObject objectiveRootObj;
    private Text objectiveTitleText;
    private Text objectiveDetailText;

    private InventoryAdvanced inventoryCache;
    private float nextUpdateTime;
    private float lastHealth = 100f;
    private float lastSanity = 100f;
    private bool layoutInitialized = false;
    private bool lastTouchLayoutActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        CreateHUDHierarchy();
        CreateStatusBars();
        CreateStatusEffectIcons();
        CreateCompass();
        CreateClock();
        CreateDamageIndicators();
        CreateLootTooltip();
        CreateWeaponIndicator();
        CreateBonfireIndicator();
        CreateStealthIndicator();
        CreateCrosshair();
        CreateInteractionPrompt();
        CreateObjectiveTracker();
    }

    private void Start()
    {
        FindMissingReferences();
        if (inventoryCache != null)
        {
            inventoryCache.OnEquipmentChanged += RefreshWeaponIndicatorFromInventory;
            RefreshWeaponIndicatorFromInventory();
        }
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.OnTouchLayoutChanged += OnTouchLayoutChanged;
        }
        SyncAdaptivePositions(true);
    }

    private void OnDestroy()
    {
        if (inventoryCache != null)
        {
            inventoryCache.OnEquipmentChanged -= RefreshWeaponIndicatorFromInventory;
        }
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.OnTouchLayoutChanged -= OnTouchLayoutChanged;
        }
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
        float startY = touchActive ? 455f : 185f;
        float spacing = 68f;
        for (int i = 0; i < statusIconRTs.Count; i++)
        {
            if (statusIconRTs[i] != null)
            {
                statusIconRTs[i].anchoredPosition = new Vector2(startX + (i * spacing), startY);
            }
        }
    }

    private void FindMissingReferences()
    {
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        if (playerController == null) playerController = PlayerController.Instance ?? FindAnyObjectByType<PlayerController>();
        if (equipmentSystem == null) equipmentSystem = EquipmentSystem.Instance ?? FindAnyObjectByType<EquipmentSystem>();
        if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
    }

    private void CreateHUDHierarchy()
    {
        Canvas targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null)
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>();
            foreach (var c in allCanvases)
            {
                if (c.isRootCanvas && c.gameObject.name.Contains("Mobile"))
                {
                    targetCanvas = c;
                    break;
                }
            }
            if (targetCanvas == null && allCanvases.Length > 0) targetCanvas = allCanvases[0];
        }

        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("UI_HUD_Canvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 50;
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

        var outline = text.gameObject.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
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
        if (s_outerRingSprite == null) s_outerRingSprite = GetRingSprite(128, 0.80f, 0.98f);
        if (s_innerRingSprite == null) s_innerRingSprite = GetRingSprite(128, 0.62f, 0.77f);
        if (s_coreDiskSprite == null) s_coreDiskSprite = GetDiskSprite(128, 0.58f);
        if (s_microRingSprite == null) s_microRingSprite = GetRingSprite(64, 0.68f, 0.98f);

        GameObject container = new GameObject("DiegeticVitalsWidget");
        container.transform.SetParent(hudRoot, false);
        RectTransform cRect = container.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0, 0);
        cRect.anchorMax = new Vector2(0, 0);
        cRect.pivot = new Vector2(0, 0);
        cRect.anchoredPosition = new Vector2(40f, 298f);
        cRect.sizeDelta = new Vector2(236f, 144f);
        vitalsContainerRT = cRect;

        statusGroup = container.AddComponent<CanvasGroup>();
        statusGroup.alpha = 0.85f;
        statusGroup.blocksRaycasts = false;

        GameObject ringAnchor = new GameObject("VitalRings");
        ringAnchor.transform.SetParent(container.transform, false);
        RectTransform rRect = ringAnchor.AddComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0, 0.5f);
        rRect.anchorMax = new Vector2(0, 0.5f);
        rRect.pivot = new Vector2(0, 0.5f);
        rRect.anchoredPosition = new Vector2(10f, 0f);
        rRect.sizeDelta = new Vector2(130f, 130f);

        GameObject coreObj = new GameObject("CoreDisk");
        coreObj.transform.SetParent(ringAnchor.transform, false);
        coreObj.AddComponent<RectTransform>().sizeDelta = new Vector2(130f, 130f);
        Image coreImg = coreObj.AddComponent<Image>();
        coreImg.sprite = s_coreDiskSprite;
        coreImg.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
        coreImg.raycastTarget = false;

        GameObject hpBgObj = new GameObject("HealthRing_BG");
        hpBgObj.transform.SetParent(ringAnchor.transform, false);
        hpBgObj.AddComponent<RectTransform>().sizeDelta = new Vector2(130f, 130f);
        Image hpBgImg = hpBgObj.AddComponent<Image>();
        hpBgImg.sprite = s_outerRingSprite;
        hpBgImg.color = new Color(0.25f, 0.04f, 0.04f, 0.55f);
        hpBgImg.raycastTarget = false;

        GameObject hpFillObj = new GameObject("HealthRing_Fill");
        hpFillObj.transform.SetParent(ringAnchor.transform, false);
        hpFillObj.AddComponent<RectTransform>().sizeDelta = new Vector2(130f, 130f);
        healthBar = hpFillObj.AddComponent<Image>();
        healthBar.sprite = s_outerRingSprite;
        healthBar.color = new Color(0.95f, 0.18f, 0.18f, 1.0f);
        healthBar.type = Image.Type.Filled;
        healthBar.fillMethod = Image.FillMethod.Radial360;
        healthBar.fillOrigin = (int)Image.Origin360.Top;
        healthBar.fillClockwise = true;
        healthBar.fillAmount = 1f;
        healthBar.raycastTarget = false;

        GameObject stBgObj = new GameObject("StaminaRing_BG");
        stBgObj.transform.SetParent(ringAnchor.transform, false);
        stBgObj.AddComponent<RectTransform>().sizeDelta = new Vector2(130f, 130f);
        Image stBgImg = stBgObj.AddComponent<Image>();
        stBgImg.sprite = s_innerRingSprite;
        stBgImg.color = new Color(0.04f, 0.20f, 0.25f, 0.45f);
        stBgImg.raycastTarget = false;

        GameObject stFillObj = new GameObject("StaminaRing_Fill");
        stFillObj.transform.SetParent(ringAnchor.transform, false);
        stFillObj.AddComponent<RectTransform>().sizeDelta = new Vector2(130f, 130f);
        staminaBar = stFillObj.AddComponent<Image>();
        staminaBar.sprite = s_innerRingSprite;
        staminaBar.color = new Color(0.28f, 0.90f, 0.98f, 1.0f);
        staminaBar.type = Image.Type.Filled;
        staminaBar.fillMethod = Image.FillMethod.Radial360;
        staminaBar.fillOrigin = (int)Image.Origin360.Top;
        staminaBar.fillClockwise = false;
        staminaBar.fillAmount = 1f;
        staminaBar.raycastTarget = false;

        GameObject hpTextObj = new GameObject("HealthNum");
        hpTextObj.transform.SetParent(ringAnchor.transform, false);
        hpTextObj.AddComponent<RectTransform>().sizeDelta = new Vector2(60f, 30f);
        healthText = hpTextObj.AddComponent<Text>();
        ConfigureHDText(healthText, 17, TextAnchor.MiddleCenter, new Color(0.98f, 0.98f, 0.98f, 1f), FontStyle.Bold);
        healthText.text = "100";

        GameObject dialsRack = new GameObject("MicroDialsRack");
        dialsRack.transform.SetParent(container.transform, false);
        RectTransform dRect = dialsRack.AddComponent<RectTransform>();
        dRect.anchorMin = new Vector2(1, 0.5f);
        dRect.anchorMax = new Vector2(1, 0.5f);
        dRect.pivot = new Vector2(1, 0.5f);
        dRect.anchoredPosition = new Vector2(-6f, 0f);
        dRect.sizeDelta = new Vector2(92f, 136f);

        hungerBar = CreateMicroDial(dialsRack.transform, "F", new Color(0.95f, 0.65f, 0.20f), 0, out hungerText);
        thirstBar = CreateMicroDial(dialsRack.transform, "S", new Color(0.22f, 0.75f, 0.98f), 1, out thirstText);
        sanityBar = CreateMicroDial(dialsRack.transform, "M", new Color(0.72f, 0.38f, 0.95f), 2, out sanityText);
        temperatureBar = CreateMicroDial(dialsRack.transform, "T", new Color(1.0f, 0.52f, 0.28f), 3, out temperatureText);
        staminaText = null;
    }

    private Image CreateMicroDial(Transform parent, string codeLetter, Color dialColor, int index, out Text dialValText)
    {
        float yPos = 45f - (index * 30f);

        GameObject dialRoot = new GameObject($"Dial_{index}");
        dialRoot.transform.SetParent(parent, false);
        RectTransform rt = dialRoot.AddComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0f, yPos);
        rt.sizeDelta = new Vector2(90f, 28f);

        GameObject bgObj = new GameObject("DialBG");
        bgObj.transform.SetParent(dialRoot.transform, false);
        RectTransform bgRT = bgObj.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f);
        bgRT.anchorMax = new Vector2(0, 0.5f);
        bgRT.pivot = new Vector2(0, 0.5f);
        bgRT.anchoredPosition = new Vector2(2f, 0f);
        bgRT.sizeDelta = new Vector2(24f, 24f);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = s_microRingSprite;
        bgImg.color = new Color(dialColor.r * 0.35f, dialColor.g * 0.35f, dialColor.b * 0.35f, 0.45f);
        bgImg.raycastTarget = false;

        GameObject fillObj = new GameObject("DialFill");
        fillObj.transform.SetParent(dialRoot.transform, false);
        RectTransform fillRT = fillObj.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0, 0.5f);
        fillRT.anchorMax = new Vector2(0, 0.5f);
        fillRT.pivot = new Vector2(0, 0.5f);
        fillRT.anchoredPosition = new Vector2(2f, 0f);
        fillRT.sizeDelta = new Vector2(24f, 24f);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = s_microRingSprite;
        fillImg.color = dialColor;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Radial360;
        fillImg.fillOrigin = (int)Image.Origin360.Top;
        fillImg.fillClockwise = true;
        fillImg.fillAmount = 1f;
        fillImg.raycastTarget = false;

        GameObject icoObj = new GameObject("CodeLabel");
        icoObj.transform.SetParent(dialRoot.transform, false);
        RectTransform icoRT = icoObj.AddComponent<RectTransform>();
        icoRT.anchorMin = new Vector2(0, 0.5f);
        icoRT.anchorMax = new Vector2(0, 0.5f);
        icoRT.pivot = new Vector2(0, 0.5f);
        icoRT.anchoredPosition = new Vector2(2f, 0f);
        icoRT.sizeDelta = new Vector2(24f, 24f);
        Text icoTxt = icoObj.AddComponent<Text>();
        ConfigureHDText(icoTxt, 10, TextAnchor.MiddleCenter, dialColor, FontStyle.Bold);
        icoTxt.text = codeLetter;

        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(dialRoot.transform, false);
        RectTransform valRT = valObj.AddComponent<RectTransform>();
        valRT.anchorMin = new Vector2(0, 0.5f);
        valRT.anchorMax = new Vector2(1, 0.5f);
        valRT.pivot = new Vector2(0, 0.5f);
        valRT.anchoredPosition = new Vector2(30f, 0f);
        valRT.sizeDelta = new Vector2(54f, 18f);
        dialValText = valObj.AddComponent<Text>();
        ConfigureHDText(dialValText, 12, TextAnchor.MiddleLeft, new Color(0.95f, 0.95f, 0.95f, 0.95f), FontStyle.Bold);
        dialValText.text = "100%";

        return fillImg;
    }

    private void CreateStatusEffectIcons()
    {
        statusIconRTs.Clear();
        float startX = 40f;
        float startY = 455f;
        float spacing = 68f;

        iconPoison = CreateStatusIcon("Poison", new Color(0.25f, 0.88f, 0.25f), "VENENO", 0, startX, startY, spacing);
        iconBleeding = CreateStatusIcon("Bleeding", new Color(0.95f, 0.22f, 0.22f), "SANGUE", 1, startX, startY, spacing);
        iconFracture = CreateStatusIcon("Fracture", new Color(0.96f, 0.82f, 0.24f), "FRAT", 2, startX, startY, spacing);
        iconHypothermia = CreateStatusIcon("Cold", new Color(0.42f, 0.80f, 1f), "FRIO", 3, startX, startY, spacing);
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
        rect.sizeDelta = new Vector2(62f, 22f);
        statusIconRTs.Add(rect);

        Image img = iconObj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetPillSprite();
        img.type = Image.Type.Sliced;
        img.color = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.85f);
        img.raycastTarget = false;

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(iconObj.transform, false);
        RectTransform txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        Text txt = txtObj.AddComponent<Text>();
        txt.text = label;
        ConfigureHDText(txt, 10, TextAnchor.MiddleCenter, color, FontStyle.Bold);

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
        compassContainer.sizeDelta = new Vector2(260f, 26f);

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
    }

    private void CreateClock()
    {
        GameObject clockObj = new GameObject("Clock");
        clockObj.transform.SetParent(hudRoot, false);
        RectTransform clockRect = clockObj.AddComponent<RectTransform>();
        clockRect.anchorMin = new Vector2(1, 1);
        clockRect.anchorMax = new Vector2(1, 1);
        clockRect.pivot = new Vector2(1, 1);
        clockRect.anchoredPosition = new Vector2(-25f, -15f);
        clockRect.sizeDelta = new Vector2(142f, 46f);

        Image clockBg = clockObj.AddComponent<Image>();
        clockBg.sprite = FragaProIconLibrary.GetPillSprite();
        clockBg.type = Image.Type.Sliced;
        clockBg.color = new Color(0.04f, 0.06f, 0.08f, 0.65f);
        clockBg.raycastTarget = false;

        GameObject hourObj = new GameObject("ClockText");
        hourObj.transform.SetParent(clockObj.transform, false);
        RectTransform hourRect = hourObj.AddComponent<RectTransform>();
        hourRect.anchorMin = Vector2.zero;
        hourRect.anchorMax = Vector2.one;
        hourRect.offsetMin = new Vector2(4, 14);
        hourRect.offsetMax = new Vector2(-4, 0);

        clockText = hourObj.AddComponent<Text>();
        ConfigureHDText(clockText, 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

        GameObject dayObj = new GameObject("DayText");
        dayObj.transform.SetParent(clockObj.transform, false);
        RectTransform dayRect = dayObj.AddComponent<RectTransform>();
        dayRect.anchorMin = Vector2.zero;
        dayRect.anchorMax = Vector2.one;
        dayRect.offsetMin = new Vector2(4, 0);
        dayRect.offsetMax = new Vector2(-4, -22);

        dayCounterText = dayObj.AddComponent<Text>();
        ConfigureHDText(dayCounterText, 12, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f, 0.9f), FontStyle.Bold);
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
        tooltipRect.anchorMin = new Vector2(0.5f, 0);
        tooltipRect.anchorMax = new Vector2(0.5f, 0);
        tooltipRect.pivot = new Vector2(0.5f, 0);
        tooltipRect.anchoredPosition = new Vector2(0, 114f);
        tooltipRect.sizeDelta = new Vector2(460f, 34f);

        lootTooltipGroup = tooltipObj.AddComponent<CanvasGroup>();
        lootTooltipGroup.alpha = 0f;
        lootTooltipGroup.blocksRaycasts = false;

        Image bg = tooltipObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.06f, 0.09f, 0.82f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("TooltipText");
        textObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14, 0);
        textRect.offsetMax = new Vector2(-14, 0);

        lootTooltipText = textObj.AddComponent<Text>();
        ConfigureHDText(lootTooltipText, 15, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.4f), FontStyle.Bold);
    }

    private void CreateWeaponIndicator()
    {
        GameObject weaponObj = new GameObject("WeaponIndicator");
        weaponObj.transform.SetParent(hudRoot, false);
        RectTransform rect = weaponObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-24f, 22f);
        rect.sizeDelta = new Vector2(190f, 34f);

        Image bg = weaponObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.06f, 0.09f, 0.72f);
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

    private void CreateBonfireIndicator()
    {
        GameObject bfObj = new GameObject("BonfireIndicator");
        bfObj.transform.SetParent(hudRoot, false);
        RectTransform rect = bfObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 154f);
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
        rect.anchoredPosition = new Vector2(0, 186f);
        rect.sizeDelta = new Vector2(190f, 24f);

        Image bg = stealthIndicatorObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.09f, 0.12f, 0.78f);
        bg.raycastTarget = false;

        stealthIndicatorText = stealthIndicatorObj.AddComponent<Text>();
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
        RectTransform rt = interactPromptObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -64f);
        rt.sizeDelta = new Vector2(380f, 38f);

        Image bg = interactPromptObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
        bg.raycastTarget = false;

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(interactPromptObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(10f, 0f);
        iRT.sizeDelta = new Vector2(26f, 26f);
        interactPromptIcon = icoObj.AddComponent<Image>();
        interactPromptIcon.preserveAspect = true;
        interactPromptIcon.raycastTarget = false;

        GameObject txtObj = new GameObject("PromptText");
        txtObj.transform.SetParent(interactPromptObj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(42f, 0f);
        tRT.offsetMax = new Vector2(-44f, 0f);
        interactPromptText = txtObj.AddComponent<Text>();
        ConfigureHDText(interactPromptText, 14, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);

        GameObject keyObj = new GameObject("KeyBadge");
        keyObj.transform.SetParent(interactPromptObj.transform, false);
        RectTransform kRT = keyObj.AddComponent<RectTransform>();
        kRT.anchorMin = new Vector2(1f, 0.5f);
        kRT.anchorMax = new Vector2(1f, 0.5f);
        kRT.pivot = new Vector2(1f, 0.5f);
        kRT.anchoredPosition = new Vector2(-10f, 0f);
        kRT.sizeDelta = new Vector2(28f, 24f);
        Image keyBg = keyObj.AddComponent<Image>();
        keyBg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        keyBg.type = Image.Type.Sliced;
        keyBg.color = new Color(0.18f, 0.42f, 0.58f, 0.92f);
        keyBg.raycastTarget = false;

        GameObject keyTxtObj = new GameObject("KeyText");
        keyTxtObj.transform.SetParent(keyObj.transform, false);
        RectTransform ktRT = keyTxtObj.AddComponent<RectTransform>();
        ktRT.anchorMin = Vector2.zero;
        ktRT.anchorMax = Vector2.one;
        ktRT.offsetMin = Vector2.zero;
        ktRT.offsetMax = Vector2.zero;
        interactKeyBadgeText = keyTxtObj.AddComponent<Text>();
        ConfigureHDText(interactKeyBadgeText, 13, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        interactKeyBadgeText.text = "E";

        interactPromptObj.SetActive(false);
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
        rt.sizeDelta = new Vector2(340f, 66f);

        Image bg = objectiveRootObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.07f, 0.11f, 0.78f);
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

    public void SetObjectiveDisplay(bool visible, string title, string detail)
    {
        if (objectiveRootObj == null) return;
        objectiveRootObj.SetActive(visible);
        if (!visible) return;
        if (objectiveTitleText != null) objectiveTitleText.text = title;
        if (objectiveDetailText != null) objectiveDetailText.text = detail;
    }

    private void RefreshWeaponIndicatorFromInventory()
    {
        if (inventoryCache == null) return;
        int id = inventoryCache.equippedMainHandId;
        if (id <= 0)
        {
            SetWeaponDisplay("PUNHOS");
            if (weaponIconImg != null)
                weaponIconImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.AttackFist);
        }
        else
        {
            ItemData d = ItemDatabase.GetItem(id);
            SetWeaponDisplay(d != null ? d.itemName : $"ITEM {id}");
            if (weaponIconImg != null)
                weaponIconImg.sprite = FragaProIconLibrary.GetItemIcon(id);
        }
    }

    private void Update()
    {
        UpdateHitMarker();
        UpdateCrosshairVisual();

        if (Time.time < nextUpdateTime) return;
        nextUpdateTime = Time.time + updateInterval;

        SyncAdaptivePositions(false);
        UpdateStatusBars();
        UpdateStatusEffects();
        UpdateCompass();
        UpdateClock();
        UpdateDamageIndicators();
        UpdateTooltip();
        UpdateBonfireIndicator();
        UpdateStealthIndicator();
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
        if (crosshairDot != null)
        {
            crosshairDot.color = Color.Lerp(crosshairDot.color, targetCrosshairColor, lerp);
        }
        if (crosshairRing != null)
        {
            crosshairRing.color = Color.Lerp(crosshairRing.color, targetRingColor, lerp);
        }
        if (crosshairRingRT != null)
        {
            float s = Mathf.Lerp(crosshairRingRT.localScale.x, targetRingScale, lerp);
            crosshairRingRT.localScale = new Vector3(s, s, 1f);
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
                healthBar.fillAmount = Mathf.Lerp(healthBar.fillAmount, Mathf.Clamp01(playerStatus.health / 100f), lerpSpeed);
                if (healthText != null)
                {
                    healthText.text = $"{(int)playerStatus.health}%";
                    healthText.color = playerStatus.health <= 25f ? new Color(1f, 0.35f, 0.35f) : new Color(0.98f, 0.98f, 0.98f, 1f);
                }
            }
            if (hungerBar != null)
            {
                hungerBar.fillAmount = Mathf.Lerp(hungerBar.fillAmount, Mathf.Clamp01(playerStatus.hunger / 100f), lerpSpeed);
                if (hungerText != null) hungerText.text = $"{(int)playerStatus.hunger}%";
            }
            if (thirstBar != null)
            {
                thirstBar.fillAmount = Mathf.Lerp(thirstBar.fillAmount, Mathf.Clamp01(playerStatus.thirst / 100f), lerpSpeed);
                if (thirstText != null) thirstText.text = $"{(int)playerStatus.thirst}%";
            }
            if (staminaBar != null)
            {
                staminaBar.fillAmount = Mathf.Lerp(staminaBar.fillAmount, Mathf.Clamp01(playerStatus.stamina / 100f), lerpSpeed);
                if (staminaText != null) staminaText.text = $"{(int)playerStatus.stamina}%";
            }
            if (temperatureBar != null)
            {
                float tempNorm = Mathf.InverseLerp(28f, 38f, playerStatus.bodyTemperature);
                temperatureBar.fillAmount = Mathf.Lerp(temperatureBar.fillAmount, tempNorm, lerpSpeed);
                bool cold = playerStatus.bodyTemperature <= 35.0f;
                temperatureBar.color = cold ? new Color(0.32f, 0.80f, 1.0f) : new Color(1.0f, 0.52f, 0.28f);
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
            if (sanityText != null) sanityText.text = $"{(int)sanitySystem.sanity}%";

            if (sanitySystem.sanity < 30f)
            {
                float pulse = Mathf.Sin(Time.time * 5f) * 0.5f + 0.5f;
                sanityBar.color = Color.Lerp(new Color(0.68f, 0.35f, 0.95f), new Color(0.95f, 0.15f, 0.15f), pulse);
            }
            else
            {
                sanityBar.color = new Color(0.68f, 0.35f, 0.95f);
            }

            if (sanitySystem.sanity < lastSanity - 2f) TriggerDamageIndicator();
            lastSanity = sanitySystem.sanity;
        }

        if (statusGroup != null)
        {
            bool isUnderStress = (playerStatus != null && (playerStatus.health < 95f || playerStatus.stamina < 85f || playerStatus.hunger < 75f || playerStatus.thirst < 75f || playerStatus.bodyTemperature <= 35.5f)) ||
                                 (sanitySystem != null && sanitySystem.sanity < 75f);
            float targetAlpha = isUnderStress ? 0.95f : 0.45f;
            statusGroup.alpha = Mathf.Lerp(statusGroup.alpha, targetAlpha, Time.deltaTime * 3.5f);
        }
    }

    private void UpdateStatusEffects()
    {
        if (playerStatus == null) return;

        if (iconPoison != null) iconPoison.gameObject.SetActive(playerStatus.isPoisoned || playerStatus.isSick);
        if (iconBleeding != null) iconBleeding.gameObject.SetActive(playerStatus.isBleeding);
        if (iconFracture != null) iconFracture.gameObject.SetActive(playerStatus.isFractured);
        if (iconHypothermia != null) iconHypothermia.gameObject.SetActive(playerStatus.bodyTemperature <= 35.0f);
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

        compassText.text = $"[ {dir}  {(int)heading}° ]";
    }

    private void UpdateClock()
    {
        if (dayNightCycle == null) return;

        if (clockText != null)
        {
            float norm = dayNightCycle.timeNormalized;
            int totalMinutes = (int)(norm * 1440f);
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
        if (tooltipFadeTimer > 0)
        {
            tooltipFadeTimer -= Time.deltaTime;
            lootTooltipGroup.alpha = Mathf.Lerp(lootTooltipGroup.alpha, 1f, Time.deltaTime * 8f);
        }
        else
        {
            lootTooltipGroup.alpha = Mathf.Lerp(lootTooltipGroup.alpha, 0f, Time.deltaTime * 5f);
        }
    }

    private void UpdateBonfireIndicator()
    {
        if (bonfireIndicator == null || playerStatus == null) return;
        bonfireIndicator.gameObject.SetActive(playerStatus.isNearBonfire);
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
        if (lootTooltipText != null)
        {
            lootTooltipText.text = text;
            tooltipFadeTimer = 3f;
        }
    }

    public void SetInteractionPrompt(bool visible, string label, FragaProIconLibrary.ControlIcon icon)
    {
        if (interactPromptObj == null) return;
        interactPromptObj.SetActive(visible);
        if (!visible) return;

        if (interactPromptText != null) interactPromptText.text = label;
        if (interactPromptIcon != null) interactPromptIcon.sprite = FragaProIconLibrary.GetControlIcon(icon);
        if (interactKeyBadgeText != null)
        {
            bool showKeyBadge = FragaInputRouter.Instance == null
                ? Input.touchCount == 0
                : !FragaInputRouter.Instance.IsTouchControlsActive;
            interactKeyBadgeText.transform.parent.gameObject.SetActive(showKeyBadge);
        }
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

    public void SetWeaponDisplay(string weaponName)
    {
        if (weaponText != null) weaponText.text = weaponName.ToUpper();
    }
}
