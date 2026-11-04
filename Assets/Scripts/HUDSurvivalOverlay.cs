using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// HUDSurvivalOverlay: HUD AAA de Sobrevivência Responsivo (The Forest / Green Hell Standard).
/// Suporta telas horizontais (Landscape) em proporções 16:9, 19.5:9 e 20:9.
/// Mostra: Barras de Status no Top-Left, Bússola no Top-Center, Relógio no Top-Right,
/// Indicadores de Dano Direcional, Status Effects, Crosshair e Tooltips de Loot sem nunca bloquear toques na tela.
/// Configurado com renderização de fontes HD nítidas (dynamicPixelsPerUnit = 2.5f + contornos escuros).
/// </summary>
public class HUDSurvivalOverlay : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerStatus playerStatus;
    public SanitySystem sanitySystem;
    public DayNightCycle dayNightCycle;
    public PlayerController playerController;
    public EquipmentSystem equipmentSystem;

    [Header("Config")]
    public float updateInterval = 0.05f; // 20 FPS para atualização suave no mobile

    // Transform raiz que cobre toda a tela (1920x1080)
    private RectTransform hudRoot;
    private CanvasGroup statusGroup;
    private static Sprite s_outerRingSprite;
    private static Sprite s_innerRingSprite;
    private static Sprite s_coreDiskSprite;
    private static Sprite s_microRingSprite;

    // Barras de status (canto superior esquerdo - layout limpo e responsivo)
    private Image healthBar;
    private Image sanityBar;
    private Image hungerBar;
    private Image thirstBar;
    private Image staminaBar;
    private Image temperatureBar;

    // Textos de valor das barras
    private Text healthText;
    private Text sanityText;
    private Text hungerText;
    private Text thirstText;
    private Text staminaText;

    // Ícones de status effect
    private Image iconPoison;
    private Image iconBleeding;
    private Image iconFracture;
    private Image iconHypothermia;

    // Bússola (topo)
    private RectTransform compassContainer;
    private Text compassText;

    // Relógio dia/noite
    private Text clockText;
    private Text dayCounterText;

    // Indicadores de dano direcional
    private Image damageTop;
    private Image damageBottom;
    private Image damageLeft;
    private Image damageRight;
    private float[] damageAlphas = new float[4];

    // Tooltip de loot
    private Text lootTooltipText;
    private CanvasGroup lootTooltipGroup;
    private float tooltipFadeTimer;

    // Weapon/Tool indicator
    private Text weaponText;

    // Bonfire proximity indicator
    private Image bonfireIndicator;

    // Crosshair minimalista
    private Image crosshairDot;

    private float nextUpdateTime;
    private float lastHealth = 100f;
    private float lastSanity = 100f;

    private void Awake()
    {
        CreateHUDHierarchy();
        CreateStatusBars();
        CreateStatusEffectIcons();
        CreateCompass();
        CreateClock();
        CreateDamageIndicators();
        CreateLootTooltip();
        CreateWeaponIndicator();
        CreateBonfireIndicator();
        CreateCrosshair();
    }

    private void Start()
    {
        FindMissingReferences();
    }

    private void FindMissingReferences()
    {
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (equipmentSystem == null) equipmentSystem = FindAnyObjectByType<EquipmentSystem>();
    }

    private void CreateHUDHierarchy()
    {
        // 1. Procura o Canvas raiz da cena (UI_Mobile_Canvas)
        Canvas targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null)
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.isRootCanvas && c.gameObject.name.Contains("Mobile"))
                {
                    targetCanvas = c;
                    break;
                }
            }
            if (targetCanvas == null && allCanvases.Length > 0)
            {
                targetCanvas = allCanvases[0];
            }
        }

        // Se não encontrar nenhum Canvas, cria um Canvas raiz ScreenSpaceOverlay configurado em 1920x1080 Landscape
        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("UI_HUD_Canvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 50;
        }

        // Garante que o CanvasScaler configure resolução nativa 1920x1080 com alta densidade (HD)
        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 2.5f; // Força rasterização ultra-nítida de texto TTF

        // 2. Cria um container filho em tela cheia que estica de (0,0) a (1,1) no Canvas
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

        var outline = text.gameObject.GetComponent<Outline>();
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

    // =================== VITAIS DIEGÉTICOS (Padrão Sons of the Forest - Bottom-Right) ===================
    private void CreateStatusBars()
    {
        if (s_outerRingSprite == null) s_outerRingSprite = GetRingSprite(128, 0.80f, 0.98f);
        if (s_innerRingSprite == null) s_innerRingSprite = GetRingSprite(128, 0.62f, 0.77f);
        if (s_coreDiskSprite == null) s_coreDiskSprite = GetDiskSprite(128, 0.58f);
        if (s_microRingSprite == null) s_microRingSprite = GetRingSprite(64, 0.68f, 0.98f);

        // Container ancorado no Canto Inferior Esquerdo (Bottom-Left, acima do joystick)
        GameObject container = new GameObject("DiegeticVitalsWidget");
        container.transform.SetParent(hudRoot, false);
        RectTransform cRect = container.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0, 0);
        cRect.anchorMax = new Vector2(0, 0);
        cRect.pivot = new Vector2(0, 0);
        cRect.anchoredPosition = new Vector2(40f, 305f);
        cRect.sizeDelta = new Vector2(230f, 140f);

        statusGroup = container.AddComponent<CanvasGroup>();
        statusGroup.alpha = 0.85f;
        statusGroup.blocksRaycasts = false;

        // 1. Âncora dos Anéis Concéntricos de Vida e Stamina (130x130)
        GameObject ringAnchor = new GameObject("VitalRings");
        ringAnchor.transform.SetParent(container.transform, false);
        RectTransform rRect = ringAnchor.AddComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0, 0.5f);
        rRect.anchorMax = new Vector2(0, 0.5f);
        rRect.pivot = new Vector2(0, 0.5f);
        rRect.anchoredPosition = new Vector2(10f, 0f);
        rRect.sizeDelta = new Vector2(130f, 130f);

        // Núcleo translúcido estilo relógio
        GameObject coreObj = new GameObject("CoreDisk");
        coreObj.transform.SetParent(ringAnchor.transform, false);
        RectTransform coreRect = coreObj.AddComponent<RectTransform>();
        coreRect.sizeDelta = new Vector2(130f, 130f);
        Image coreImg = coreObj.AddComponent<Image>();
        coreImg.sprite = s_coreDiskSprite;
        coreImg.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
        coreImg.raycastTarget = false;

        // Anel de Fundo da Vida (Vermelho Escuro)
        GameObject hpBgObj = new GameObject("HealthRing_BG");
        hpBgObj.transform.SetParent(ringAnchor.transform, false);
        RectTransform hpBgRect = hpBgObj.AddComponent<RectTransform>();
        hpBgRect.sizeDelta = new Vector2(130f, 130f);
        Image hpBgImg = hpBgObj.AddComponent<Image>();
        hpBgImg.sprite = s_outerRingSprite;
        hpBgImg.color = new Color(0.25f, 0.04f, 0.04f, 0.55f);
        hpBgImg.raycastTarget = false;

        // Anel de Fill da Vida (Vermelho Sangue / Escarlate)
        GameObject hpFillObj = new GameObject("HealthRing_Fill");
        hpFillObj.transform.SetParent(ringAnchor.transform, false);
        RectTransform hpFillRect = hpFillObj.AddComponent<RectTransform>();
        hpFillRect.sizeDelta = new Vector2(130f, 130f);
        healthBar = hpFillObj.AddComponent<Image>();
        healthBar.sprite = s_outerRingSprite;
        healthBar.color = new Color(0.95f, 0.18f, 0.18f, 1.0f);
        healthBar.type = Image.Type.Filled;
        healthBar.fillMethod = Image.FillMethod.Radial360;
        healthBar.fillOrigin = (int)Image.Origin360.Top;
        healthBar.fillClockwise = true;
        healthBar.fillAmount = 1f;
        healthBar.raycastTarget = false;

        // Anel de Fundo da Stamina (Ciano Escuro)
        GameObject stBgObj = new GameObject("StaminaRing_BG");
        stBgObj.transform.SetParent(ringAnchor.transform, false);
        RectTransform stBgRect = stBgObj.AddComponent<RectTransform>();
        stBgRect.sizeDelta = new Vector2(130f, 130f);
        Image stBgImg = stBgObj.AddComponent<Image>();
        stBgImg.sprite = s_innerRingSprite;
        stBgImg.color = new Color(0.04f, 0.20f, 0.25f, 0.45f);
        stBgImg.raycastTarget = false;

        // Anel de Fill da Stamina (Aqua / Ciano)
        GameObject stFillObj = new GameObject("StaminaRing_Fill");
        stFillObj.transform.SetParent(ringAnchor.transform, false);
        RectTransform stFillRect = stFillObj.AddComponent<RectTransform>();
        stFillRect.sizeDelta = new Vector2(130f, 130f);
        staminaBar = stFillObj.AddComponent<Image>();
        staminaBar.sprite = s_innerRingSprite;
        staminaBar.color = new Color(0.28f, 0.90f, 0.98f, 1.0f);
        staminaBar.type = Image.Type.Filled;
        staminaBar.fillMethod = Image.FillMethod.Radial360;
        staminaBar.fillOrigin = (int)Image.Origin360.Top;
        staminaBar.fillClockwise = false;
        staminaBar.fillAmount = 1f;
        staminaBar.raycastTarget = false;

        // Texto Numérico Central
        GameObject hpTextObj = new GameObject("HealthNum");
        hpTextObj.transform.SetParent(ringAnchor.transform, false);
        RectTransform hpTxtRect = hpTextObj.AddComponent<RectTransform>();
        hpTxtRect.sizeDelta = new Vector2(60f, 30f);
        healthText = hpTextObj.AddComponent<Text>();
        ConfigureHDText(healthText, 17, TextAnchor.MiddleCenter, new Color(0.98f, 0.98f, 0.98f, 1f), FontStyle.Bold);
        healthText.text = "100";

        // 2. Micro-Dials Laterais (Fome, Sede, Sanidade)
        GameObject dialsRack = new GameObject("MicroDialsRack");
        dialsRack.transform.SetParent(container.transform, false);
        RectTransform dRect = dialsRack.AddComponent<RectTransform>();
        dRect.anchorMin = new Vector2(1, 0.5f);
        dRect.anchorMax = new Vector2(1, 0.5f);
        dRect.pivot = new Vector2(1, 0.5f);
        dRect.anchoredPosition = new Vector2(-10f, 0f);
        dRect.sizeDelta = new Vector2(85f, 130f);

        hungerBar = CreateMicroDial(dialsRack.transform, "◈", new Color(0.95f, 0.65f, 0.20f), 0, out hungerText);
        thirstBar = CreateMicroDial(dialsRack.transform, "◇", new Color(0.22f, 0.75f, 0.98f), 1, out thirstText);
        sanityBar = CreateMicroDial(dialsRack.transform, "◉", new Color(0.72f, 0.38f, 0.95f), 2, out sanityText);

        staminaText = null; // healthText é estritamente dedicado à Saúde
    }

    private Image CreateMicroDial(Transform parent, string iconSymbol, Color dialColor, int index, out Text dialValText)
    {
        float yPos = 38f - (index * 38f);

        GameObject dialRoot = new GameObject($"Dial_{index}");
        dialRoot.transform.SetParent(parent, false);
        RectTransform rt = dialRoot.AddComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0f, yPos);
        rt.sizeDelta = new Vector2(85f, 32f);

        // Anel de fundo
        GameObject bgObj = new GameObject("DialBG");
        bgObj.transform.SetParent(dialRoot.transform, false);
        RectTransform bgRT = bgObj.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f);
        bgRT.anchorMax = new Vector2(0, 0.5f);
        bgRT.pivot = new Vector2(0, 0.5f);
        bgRT.anchoredPosition = new Vector2(4f, 0f);
        bgRT.sizeDelta = new Vector2(28f, 28f);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = s_microRingSprite;
        bgImg.color = new Color(dialColor.r * 0.35f, dialColor.g * 0.35f, dialColor.b * 0.35f, 0.45f);
        bgImg.raycastTarget = false;

        // Anel de preenchimento radial
        GameObject fillObj = new GameObject("DialFill");
        fillObj.transform.SetParent(dialRoot.transform, false);
        RectTransform fillRT = fillObj.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0, 0.5f);
        fillRT.anchorMax = new Vector2(0, 0.5f);
        fillRT.pivot = new Vector2(0, 0.5f);
        fillRT.anchoredPosition = new Vector2(4f, 0f);
        fillRT.sizeDelta = new Vector2(28f, 28f);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = s_microRingSprite;
        fillImg.color = dialColor;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Radial360;
        fillImg.fillOrigin = (int)Image.Origin360.Top;
        fillImg.fillClockwise = true;
        fillImg.fillAmount = 1f;
        fillImg.raycastTarget = false;

        // Ícone no centro
        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(dialRoot.transform, false);
        RectTransform icoRT = icoObj.AddComponent<RectTransform>();
        icoRT.anchorMin = new Vector2(0, 0.5f);
        icoRT.anchorMax = new Vector2(0, 0.5f);
        icoRT.pivot = new Vector2(0, 0.5f);
        icoRT.anchoredPosition = new Vector2(4f, 0f);
        icoRT.sizeDelta = new Vector2(28f, 28f);
        Text icoTxt = icoObj.AddComponent<Text>();
        ConfigureHDText(icoTxt, 14, TextAnchor.MiddleCenter, dialColor, FontStyle.Bold);
        icoTxt.text = iconSymbol;

        // Valor numérico
        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(dialRoot.transform, false);
        RectTransform valRT = valObj.AddComponent<RectTransform>();
        valRT.anchorMin = new Vector2(0, 0.5f);
        valRT.anchorMax = new Vector2(1, 0.5f);
        valRT.pivot = new Vector2(0, 0.5f);
        valRT.anchoredPosition = new Vector2(36f, 0f);
        valRT.sizeDelta = new Vector2(48f, 20f);
        dialValText = valObj.AddComponent<Text>();
        ConfigureHDText(dialValText, 13, TextAnchor.MiddleLeft, new Color(0.95f, 0.95f, 0.95f, 0.95f), FontStyle.Bold);
        dialValText.text = "100%";

        return fillImg;
    }

    // =================== STATUS EFFECT ICONS ===================
    private void CreateStatusEffectIcons()
    {
        float iconSize = 28f;
        float startX = 40f;
        float startY = 465f; // Acima do widget de vitais no Canto Inferior Esquerdo
        float spacing = 34f;

        iconPoison = CreateStatusIcon("Poison", new Color(0.2f, 0.85f, 0.1f), "☠", 0, startX, startY, iconSize, spacing);
        iconBleeding = CreateStatusIcon("Bleeding", new Color(0.95f, 0.15f, 0.15f), "🩸", 1, startX, startY, iconSize, spacing);
        iconFracture = CreateStatusIcon("Fracture", new Color(0.95f, 0.95f, 0.2f), "🦴", 2, startX, startY, iconSize, spacing);
        iconHypothermia = CreateStatusIcon("Cold", new Color(0.4f, 0.75f, 1f), "❄", 3, startX, startY, iconSize, spacing);
    }

    private Image CreateStatusIcon(string name, Color color, string symbol, int index, float startX, float startY, float size, float spacing)
    {
        GameObject iconObj = new GameObject($"StatusIcon_{name}");
        iconObj.transform.SetParent(hudRoot, false);
        RectTransform rect = iconObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0);
        rect.anchoredPosition = new Vector2(startX + (index * spacing), startY);
        rect.sizeDelta = new Vector2(size, size);

        Image img = iconObj.AddComponent<Image>();
        img.color = new Color(color.r, color.g, color.b, 0f);
        img.raycastTarget = false;

        GameObject txtObj = new GameObject("IconText");
        txtObj.transform.SetParent(iconObj.transform, false);
        RectTransform txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        Text txt = txtObj.AddComponent<Text>();
        txt.text = symbol;
        ConfigureHDText(txt, 18, TextAnchor.MiddleCenter, color, FontStyle.Bold);

        iconObj.SetActive(false);
        return img;
    }

    // =================== BÚSSOLA (Top-Center) ===================
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
        compassBg.color = new Color(0.04f, 0.06f, 0.08f, 0.55f);
        compassBg.raycastTarget = false;

        GameObject textObj = new GameObject("CompassText");
        textObj.transform.SetParent(compassObj.transform, false);
        RectTransform txtRect = textObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        compassText = textObj.AddComponent<Text>();
        ConfigureHDText(compassText, 16, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f, 0.95f), FontStyle.Bold);
    }

    // =================== RELÓGIO (Top-Right) ===================
    private void CreateClock()
    {
        GameObject clockObj = new GameObject("Clock");
        clockObj.transform.SetParent(hudRoot, false);
        RectTransform clockRect = clockObj.AddComponent<RectTransform>();
        clockRect.anchorMin = new Vector2(1, 1);
        clockRect.anchorMax = new Vector2(1, 1);
        clockRect.pivot = new Vector2(1, 1);
        clockRect.anchoredPosition = new Vector2(-25f, -15f);
        clockRect.sizeDelta = new Vector2(130f, 44f);

        Image clockBg = clockObj.AddComponent<Image>();
        clockBg.color = new Color(0.04f, 0.06f, 0.08f, 0.55f);
        clockBg.raycastTarget = false;

        // Hora
        GameObject hourObj = new GameObject("ClockText");
        hourObj.transform.SetParent(clockObj.transform, false);
        RectTransform hourRect = hourObj.AddComponent<RectTransform>();
        hourRect.anchorMin = Vector2.zero;
        hourRect.anchorMax = Vector2.one;
        hourRect.offsetMin = new Vector2(4, 12);
        hourRect.offsetMax = new Vector2(-4, 0);

        clockText = hourObj.AddComponent<Text>();
        ConfigureHDText(clockText, 20, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

        // Dia
        GameObject dayObj = new GameObject("DayText");
        dayObj.transform.SetParent(clockObj.transform, false);
        RectTransform dayRect = dayObj.AddComponent<RectTransform>();
        dayRect.anchorMin = Vector2.zero;
        dayRect.anchorMax = Vector2.one;
        dayRect.offsetMin = new Vector2(4, 0);
        dayRect.offsetMax = new Vector2(-4, -20);

        dayCounterText = dayObj.AddComponent<Text>();
        ConfigureHDText(dayCounterText, 13, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f, 0.9f), FontStyle.Bold);
    }

    // =================== DANO DIRECIONAL ===================
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

    // =================== TOOLTIP DE LOOT (Bottom-Center) ===================
    private void CreateLootTooltip()
    {
        GameObject tooltipObj = new GameObject("LootTooltip");
        tooltipObj.transform.SetParent(hudRoot, false);
        RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0);
        tooltipRect.anchorMax = new Vector2(0.5f, 0);
        tooltipRect.pivot = new Vector2(0.5f, 0);
        tooltipRect.anchoredPosition = new Vector2(0, 140f);
        tooltipRect.sizeDelta = new Vector2(360f, 36f);

        lootTooltipGroup = tooltipObj.AddComponent<CanvasGroup>();
        lootTooltipGroup.alpha = 0f;
        lootTooltipGroup.blocksRaycasts = false;

        Image bg = tooltipObj.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.07f, 0.75f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("TooltipText");
        textObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 0);
        textRect.offsetMax = new Vector2(-8, 0);

        lootTooltipText = textObj.AddComponent<Text>();
        ConfigureHDText(lootTooltipText, 16, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.4f), FontStyle.Bold);
    }

    // =================== WEAPON INDICATOR (Bottom-Right) ===================
    private void CreateWeaponIndicator()
    {
        GameObject weaponObj = new GameObject("WeaponIndicator");
        weaponObj.transform.SetParent(hudRoot, false);
        RectTransform rect = weaponObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-25f, 35f);
        rect.sizeDelta = new Vector2(130f, 28f);

        Image bg = weaponObj.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.08f, 0.55f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("WeaponText");
        textObj.transform.SetParent(weaponObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        weaponText = textObj.AddComponent<Text>();
        ConfigureHDText(weaponText, 15, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f, 0.9f), FontStyle.Bold);
        weaponText.text = "⚔ PUNHOS";
    }

    // =================== BONFIRE INDICATOR ===================
    private void CreateBonfireIndicator()
    {
        GameObject bfObj = new GameObject("BonfireIndicator");
        bfObj.transform.SetParent(hudRoot, false);
        RectTransform rect = bfObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 185f);
        rect.sizeDelta = new Vector2(180f, 26f);

        bonfireIndicator = bfObj.AddComponent<Image>();
        bonfireIndicator.color = new Color(0.05f, 0.05f, 0.05f, 0.6f);
        bonfireIndicator.raycastTarget = false;

        GameObject textObj = new GameObject("BonfireText");
        textObj.transform.SetParent(bfObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text txt = textObj.AddComponent<Text>();
        ConfigureHDText(txt, 14, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f), FontStyle.Bold);
        txt.text = "🔥 PERTO DA FOGUEIRA";

        bfObj.SetActive(false);
    }

    // =================== CROSSHAIR ===================
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
        crosshairDot.color = new Color(1f, 1f, 1f, 0.45f);
        crosshairDot.raycastTarget = false;
    }

    // =================== UPDATE LOOP ===================
    private void Update()
    {
        if (Time.time < nextUpdateTime) return;
        nextUpdateTime = Time.time + updateInterval;

        UpdateStatusBars();
        UpdateStatusEffects();
        UpdateCompass();
        UpdateClock();
        UpdateDamageIndicators();
        UpdateTooltip();
        UpdateBonfireIndicator();
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
                temperatureBar.fillAmount = Mathf.Lerp(temperatureBar.fillAmount, Mathf.Clamp01(playerStatus.bodyTemperature / 40f), lerpSpeed);
            }

            if (playerStatus.health < lastHealth - 1f)
            {
                TriggerDamageIndicator();
            }
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

            if (sanitySystem.sanity < lastSanity - 2f)
            {
                TriggerDamageIndicator();
            }
            lastSanity = sanitySystem.sanity;
        }

        if (statusGroup != null)
        {
            bool isUnderStress = (playerStatus != null && (playerStatus.health < 95f || playerStatus.stamina < 85f || playerStatus.hunger < 75f || playerStatus.thirst < 75f)) ||
                                 (sanitySystem != null && sanitySystem.sanity < 75f);
            float targetAlpha = isUnderStress ? 0.95f : 0.35f;
            statusGroup.alpha = Mathf.Lerp(statusGroup.alpha, targetAlpha, Time.deltaTime * 3.5f);
        }
    }

    private void UpdateStatusEffects()
    {
        if (playerStatus == null) return;

        // Veneno
        if (iconPoison != null)
        {
            bool show = playerStatus.isPoisoned;
            iconPoison.gameObject.SetActive(show);
            if (show)
            {
                float pulse = Mathf.Sin(Time.time * 6f) * 0.3f + 0.7f;
                iconPoison.color = new Color(0.2f, 0.85f, 0.1f, pulse);
            }
        }

        // Sangramento
        if (iconBleeding != null)
        {
            bool show = playerStatus.isBleeding;
            iconBleeding.gameObject.SetActive(show);
            if (show)
            {
                float pulse = Mathf.Sin(Time.time * 8f) * 0.3f + 0.7f;
                iconBleeding.color = new Color(0.95f, 0.15f, 0.15f, pulse);
            }
        }

        // Fratura
        if (iconFracture != null)
        {
            bool show = playerStatus.isFractured;
            iconFracture.gameObject.SetActive(show);
            if (show)
            {
                iconFracture.color = new Color(0.95f, 0.95f, 0.2f, 0.85f);
            }
        }

        // Frio / Hipotermia
        if (iconHypothermia != null)
        {
            bool show = playerStatus.bodyTemperature <= 35.0f;
            iconHypothermia.gameObject.SetActive(show);
            if (show)
            {
                float pulse = Mathf.Sin(Time.time * 4f) * 0.3f + 0.7f;
                iconHypothermia.color = new Color(0.4f, 0.75f, 1f, pulse);
            }
        }
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
            string icon = isNight ? "☽" : "☼";

            clockText.text = $"{icon} {hours:00}:{minutes:00}";
            clockText.color = isNight ? new Color(0.7f, 0.75f, 1f) : new Color(1f, 0.9f, 0.65f);
        }

        if (dayCounterText != null)
        {
            dayCounterText.text = $"DIA {dayNightCycle.currentDay}";
        }
    }

    private void UpdateDamageIndicators()
    {
        float fadeSpeed = Time.deltaTime * 3f;

        for (int i = 0; i < 4; i++)
        {
            damageAlphas[i] = Mathf.Max(0f, damageAlphas[i] - fadeSpeed);
        }

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
            lootTooltipGroup.alpha = Mathf.Lerp(lootTooltipGroup.alpha, 1f, Time.deltaTime * 6f);
        }
        else
        {
            lootTooltipGroup.alpha = Mathf.Lerp(lootTooltipGroup.alpha, 0f, Time.deltaTime * 4f);
        }
    }

    private void UpdateBonfireIndicator()
    {
        if (bonfireIndicator == null || playerStatus == null) return;
        bonfireIndicator.gameObject.SetActive(playerStatus.isNearBonfire);
    }

    // =================== API PÚBLICA ===================

    public void ShowLootTooltip(string text)
    {
        if (lootTooltipText != null)
        {
            lootTooltipText.text = text;
            tooltipFadeTimer = 3f;
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
        if (weaponText != null) weaponText.text = $"⚔ {weaponName.ToUpper()}";
    }
}
