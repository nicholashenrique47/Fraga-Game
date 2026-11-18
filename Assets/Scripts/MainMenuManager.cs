using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// MainMenuManager: Menu Inicial AAA Nativo (Zero Load Times).
/// Sobrepoe uma interface tatica em vidro escuro ao mundo 3D ao vivo da Ilha da Fraga,
/// integrando telemetria de Save, configuracao rapida de Sala Co-op e opcoes PC/Mobile.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    private const string PrefQualityKey = "Fraga_QualityLevel";
    private const string PrefTargetFpsKey = "Fraga_TargetFPS";
    private const string PrefControlModeKey = "Fraga_ControlLayoutMode";

    [Header("Dependencies")]
    public CanvasGroup mainMenuCanvasGroup;
    public SaveSystem saveSystem;
    public UIManager uiManager;
    public AudioManager audioManager;
    public Camera mainCamera;

    [Header("UI Panels")]
    public GameObject mainPanel;
    public GameObject newGamePanel;
    public GameObject settingsPanel;
    public GameObject coopPanel;

    [Header("Main Panel Elements")]
    public Button playButton;
    public Button btnOpenNewGame;
    public Button btnOpenSettings;
    public Button btnOpenCoop;
    public Button btnQuitGame;
    public Text titleText;

    [Header("New Game Elements")]
    public InputField seedInput;
    public Button btnCreateWorld;
    public Button btnBackFromNewGame;

    [Header("Settings Elements")]
    public Slider masterVolumeSlider;
    public Button btnBackFromSettings;

    [Header("Game Reference")]
    public GameManager gameManager;

    private Vector3 gameCameraPos = new Vector3(384f, 18f, 374f);
    private Vector3 menuCameraPos = new Vector3(384f, 11f, 384f);
    private bool isFirstPersonCamera = false;
    private bool isTransitioning = false;
    private bool proMenuBuilt = false;

    // Referencias dinamicas de briefing e subpaineis AAA
    private Text playSubLabelText;
    private Text briefingSaveText;
    private Text briefingCoopText;
    private Text briefingControlsText;
    private Text coopStatusText;
    private Text coopAccessBtnLabel;
    private Text coopHostBtnLabel;
    private Text coopPreviewBtnLabel;
    private InputField coopHostIpInput;
    private Text coopNetFeedbackText;
    private float nextCoopPollTime = 0f;
    private Text volumeValueText;
    private Text qualityBtnLabel;
    private Text fpsBtnLabel;
    private Text controlModeBtnLabel;
    private Text settingsFeedbackText;

    // Pool Zero-GC de brasas/particulas atmosfericas no menu
    private const int EmberCount = 14;
    private RectTransform[] emberRTs;
    private Vector2[] emberVelocities;
    private float[] emberPhases;

    private void Start()
    {
        Time.timeScale = 0f;
        FragaWorldBlueprint.EnsureInitialized();
        ApplySavedSystemSettings();

        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        float cx = gameManager != null ? gameManager.mapWidth * 0.5f : FragaWorldBlueprint.CrashCampPos.x;
        float cz = gameManager != null ? gameManager.mapHeight * 0.5f : FragaWorldBlueprint.CrashCampPos.y;
        float groundY = TerrainMeshGenerator.GetTerrainHeight(cx, cz);
        menuCameraPos = new Vector3(cx, groundY + 5f, cz);
        gameCameraPos = new Vector3(cx, groundY + 15f, cz - 10f);

        if (mainCamera == null) mainCamera = Camera.main;

        FirstPersonCamera fpCam = mainCamera != null ? mainCamera.GetComponent<FirstPersonCamera>() : null;
        isFirstPersonCamera = (fpCam != null) || (mainCamera != null && mainCamera.transform.parent != null);
        if (isFirstPersonCamera && mainCamera != null)
        {
            mainCamera.orthographic = false;
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.transform.localPosition = new Vector3(0f, 1.75f, 0.15f);
            mainCamera.transform.localRotation = Quaternion.identity;
        }
        else if (mainCamera != null)
        {
            mainCamera.orthographic = false;
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.transform.position = menuCameraPos;
            mainCamera.transform.rotation = Quaternion.Euler(15f, 45f, 0f);
        }

        if (uiManager != null && uiManager.gameObject != this.gameObject)
        {
            uiManager.gameObject.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureProfessionalMenuUI();
        SetupPanels();
        SetupButtons();
        RefreshDynamicMenuTexts();
    }

    private void Update()
    {
        if (isTransitioning || mainMenuCanvasGroup == null || !mainMenuCanvasGroup.gameObject.activeInHierarchy)
            return;

        float dt = Time.unscaledDeltaTime;
        if (dt > 0.1f) dt = 0.016f;
        float now = Time.unscaledTime;

        // Balanco suave de camera no menu (Zero-GC)
        if (mainCamera != null && !isFirstPersonCamera)
        {
            float swayYaw = 45f + Mathf.Sin(now * 0.18f) * 4.5f;
            float swayPitch = 15f + Mathf.Cos(now * 0.24f) * 1.4f;
            mainCamera.transform.position = menuCameraPos + new Vector3(Mathf.Sin(now * 0.14f) * 0.45f, Mathf.Cos(now * 0.22f) * 0.18f, 0f);
            mainCamera.transform.rotation = Quaternion.Euler(swayPitch, swayYaw, 0f);
        }

        // Animacao das brasas ambientais
        if (emberRTs != null)
        {
            for (int i = 0; i < emberRTs.Length; i++)
            {
                RectTransform rt = emberRTs[i];
                if (rt == null) continue;
                Vector2 pos = rt.anchoredPosition;
                Vector2 vel = emberVelocities[i];
                pos.x += (vel.x + Mathf.Sin(now * 1.3f + emberPhases[i]) * 14f) * dt;
                pos.y += vel.y * dt;
                if (pos.y > 580f)
                {
                    pos.y = -580f;
                    pos.x = -860f + ((i * 137) % 1720);
                }
                rt.anchoredPosition = pos;
            }
        }

        if (coopPanel != null && coopPanel.activeInHierarchy && now >= nextCoopPollTime)
        {
            nextCoopPollTime = now + 0.5f;
            RefreshDynamicMenuTexts();
        }
    }

    private void ApplySavedSystemSettings()
    {
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Fraga_MasterVolume", 1.0f));
        int fps = PlayerPrefs.GetInt(PrefTargetFpsKey, 60);
        if (fps == 30 || fps == 60) Application.targetFrameRate = fps;

        if (PlayerPrefs.HasKey(PrefQualityKey) && QualitySettings.names != null && QualitySettings.names.Length > 0)
        {
            int q = Mathf.Clamp(PlayerPrefs.GetInt(PrefQualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(q, false);
        }
    }

    public void EnsureProfessionalMenuUI()
    {
        if (proMenuBuilt) return;
        proMenuBuilt = true;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        if (mainMenuCanvasGroup == null)
        {
            mainMenuCanvasGroup = GetComponent<CanvasGroup>();
            if (mainMenuCanvasGroup == null) mainMenuCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        mainMenuCanvasGroup.alpha = 1f;
        mainMenuCanvasGroup.interactable = true;
        mainMenuCanvasGroup.blocksRaycasts = true;

        // Remove hierarquia legada da cena para reconstruir o menu AAA completo com seguranca
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            child.name = "__Legacy_" + child.name;
            child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. Camada Atmosferica de Fundo (Scrim Lateral + Vinheta + Brasas)
        GameObject bgRoot = new GameObject("Menu_Background");
        bgRoot.transform.SetParent(transform, false);
        RectTransform bgRT = bgRoot.AddComponent<RectTransform>();
        StretchFull(bgRT);

        // Escurecimento suave geral para contraste do 3D
        Image dimImg = bgRoot.AddComponent<Image>();
        dimImg.color = new Color(0.02f, 0.03f, 0.05f, 0.36f);
        dimImg.raycastTarget = false;

        // Gradiente lateral esquerdo para destacar titulo e rail de navegacao
        GameObject leftScrim = new GameObject("LeftAtmosphericScrim");
        leftScrim.transform.SetParent(bgRoot.transform, false);
        RectTransform lsRT = leftScrim.AddComponent<RectTransform>();
        lsRT.anchorMin = new Vector2(0f, 0f);
        lsRT.anchorMax = new Vector2(0.54f, 1f);
        lsRT.offsetMin = Vector2.zero;
        lsRT.offsetMax = Vector2.zero;
        Image lsImg = leftScrim.AddComponent<Image>();
        lsImg.sprite = FragaProIconLibrary.GetVerticalGradientSprite();
        lsImg.color = new Color(0.03f, 0.05f, 0.08f, 0.90f);
        lsImg.raycastTarget = false;

        // Gradiente inferior de acabamento
        GameObject botVignette = new GameObject("BottomVignette");
        botVignette.transform.SetParent(bgRoot.transform, false);
        RectTransform bvRT = botVignette.AddComponent<RectTransform>();
        bvRT.anchorMin = new Vector2(0f, 0f);
        bvRT.anchorMax = new Vector2(1f, 0.24f);
        bvRT.offsetMin = Vector2.zero;
        bvRT.offsetMax = Vector2.zero;
        Image bvImg = botVignette.AddComponent<Image>();
        bvImg.sprite = FragaProIconLibrary.GetVerticalGradientSprite();
        bvImg.color = new Color(0.02f, 0.03f, 0.05f, 0.65f);
        bvImg.raycastTarget = false;

        BuildAmbientEmbers(bgRoot.transform);

        // 2. Painel Principal (Hero Esquerdo + Rail de Navegacao + Briefing Direito)
        mainPanel = new GameObject("Main_Panel");
        mainPanel.transform.SetParent(transform, false);
        StretchFull(mainPanel.AddComponent<RectTransform>());

        BuildHeroTitleBlock(mainPanel.transform, font);
        BuildLeftNavRail(mainPanel.transform, font);
        BuildBriefingDossierCard(mainPanel.transform, font);

        // Rodape tecnico discreto
        GameObject verObj = new GameObject("VersionFooter");
        verObj.transform.SetParent(mainPanel.transform, false);
        RectTransform vRT = verObj.AddComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0f, 0f);
        vRT.anchorMax = new Vector2(0f, 0f);
        vRT.pivot = new Vector2(0f, 0f);
        vRT.anchoredPosition = new Vector2(68f, 24f);
        vRT.sizeDelta = new Vector2(760f, 24f);
        Text vTxt = verObj.AddComponent<Text>();
        vTxt.font = font;
        vTxt.fontSize = 11;
        vTxt.fontStyle = FontStyle.Bold;
        vTxt.alignment = TextAnchor.MiddleLeft;
        vTxt.color = new Color(0.62f, 0.72f, 0.80f, 0.75f);
        vTxt.text = "FRAGA v2.4  |  MOTOR NATIVO C++ FLOWFIELD  |  ZERO-GC HUD  |  PC & MOBILE";
        vTxt.raycastTarget = false;

        // 3. Subpaineis Modais (Novo Mundo, Sala Co-op, Configuracoes)
        BuildNewGamePanel(font);
        BuildCoopLobbyPanel(font);
        BuildSettingsPanel(font);
    }

    private void BuildAmbientEmbers(Transform parent)
    {
        GameObject layer = new GameObject("MenuEmberFX");
        layer.transform.SetParent(parent, false);
        StretchFull(layer.AddComponent<RectTransform>());

        emberRTs = new RectTransform[EmberCount];
        emberVelocities = new Vector2[EmberCount];
        emberPhases = new float[EmberCount];
        Sprite dotSprite = FragaProIconLibrary.GetCircleBackground();

        for (int i = 0; i < EmberCount; i++)
        {
            GameObject eObj = new GameObject($"Ember_{i}");
            eObj.transform.SetParent(layer.transform, false);
            RectTransform rt = eObj.AddComponent<RectTransform>();
            float sz = 4f + (i % 4) * 2.2f;
            rt.sizeDelta = new Vector2(sz, sz);
            rt.anchoredPosition = new Vector2(-860f + ((i * 151) % 1720), -520f + ((i * 197) % 1040));

            Image img = eObj.AddComponent<Image>();
            img.sprite = dotSprite;
            img.color = (i % 3 == 0)
                ? new Color(0.98f, 0.56f, 0.22f, 0.34f)
                : new Color(0.98f, 0.80f, 0.42f, 0.24f);
            img.raycastTarget = false;

            emberRTs[i] = rt;
            emberVelocities[i] = new Vector2(8f + (i % 5) * 3f, 22f + (i % 4) * 11f);
            emberPhases[i] = i * 0.85f;
        }
    }

    private void BuildHeroTitleBlock(Transform parent, Font font)
    {
        GameObject hero = new GameObject("HeroHeader");
        hero.transform.SetParent(parent, false);
        RectTransform hRT = hero.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(0f, 1f);
        hRT.pivot = new Vector2(0f, 1f);
        hRT.anchoredPosition = new Vector2(68f, -52f);
        hRT.sizeDelta = new Vector2(760f, 250f);

        // Pill de edicao
        GameObject badge = new GameObject("EditionPill");
        badge.transform.SetParent(hero.transform, false);
        RectTransform bRT = badge.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 1f);
        bRT.anchorMax = new Vector2(0f, 1f);
        bRT.pivot = new Vector2(0f, 1f);
        bRT.anchoredPosition = Vector2.zero;
        bRT.sizeDelta = new Vector2(368f, 28f);
        Image bImg = badge.AddComponent<Image>();
        bImg.sprite = FragaProIconLibrary.GetPillSprite();
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.56f, 0.14f, 0.14f, 0.90f);
        bImg.raycastTarget = false;
        CreateLabel(badge.transform, "Lbl", "SOBREVIVENCIA CELTA  |  CO-OP READY", font, 11,
            TextAnchor.MiddleCenter, new Color(0.98f, 0.90f, 0.78f), Vector2.zero, new Vector2(352f, 24f));

        // Titulo Principal
        GameObject titleObj = new GameObject("Game_Title");
        titleObj.transform.SetParent(hero.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 1f);
        tRT.anchorMax = new Vector2(0f, 1f);
        tRT.pivot = new Vector2(0f, 1f);
        tRT.anchoredPosition = new Vector2(-2f, -34f);
        tRT.sizeDelta = new Vector2(720f, 148f);

        titleText = titleObj.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 84;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.supportRichText = true;
        titleText.color = new Color(0.94f, 0.18f, 0.18f);
        titleText.text = "FRAGA\n<size=32><color=#E8EEF5>A NOITE ESQUECIDA</color></size>";
        titleText.raycastTarget = false;

        Outline tOut = titleObj.AddComponent<Outline>();
        tOut.effectColor = new Color(0.02f, 0.02f, 0.03f, 0.96f);
        tOut.effectDistance = new Vector2(3f, -3f);

        // Linha divisoria carmesim + tagline
        GameObject accent = new GameObject("TitleAccentBar");
        accent.transform.SetParent(hero.transform, false);
        RectTransform aRT = accent.AddComponent<RectTransform>();
        aRT.anchorMin = new Vector2(0f, 1f);
        aRT.anchorMax = new Vector2(0f, 1f);
        aRT.pivot = new Vector2(0f, 1f);
        aRT.anchoredPosition = new Vector2(0f, -186f);
        aRT.sizeDelta = new Vector2(340f, 3f);
        Image aImg = accent.AddComponent<Image>();
        aImg.color = new Color(0.88f, 0.24f, 0.20f, 0.92f);
        aImg.raycastTarget = false;

        GameObject tagObj = new GameObject("Tagline");
        tagObj.transform.SetParent(hero.transform, false);
        RectTransform tgRT = tagObj.AddComponent<RectTransform>();
        tgRT.anchorMin = new Vector2(0f, 1f);
        tgRT.anchorMax = new Vector2(0f, 1f);
        tgRT.pivot = new Vector2(0f, 1f);
        tgRT.anchoredPosition = new Vector2(0f, -198f);
        tgRT.sizeDelta = new Vector2(640f, 44f);
        Text tgTxt = tagObj.AddComponent<Text>();
        tgTxt.font = font;
        tgTxt.fontSize = 15;
        tgTxt.fontStyle = FontStyle.Normal;
        tgTxt.alignment = TextAnchor.UpperLeft;
        tgTxt.color = new Color(0.82f, 0.88f, 0.94f, 0.92f);
        tgTxt.text = "Sobreviva a nevoa, erga sua fortaleza na mata e resista aos Vultos.";
        tgTxt.raycastTarget = false;
    }

    private void BuildLeftNavRail(Transform parent, Font font)
    {
        GameObject rail = new GameObject("NavRail");
        rail.transform.SetParent(parent, false);
        RectTransform rRT = rail.AddComponent<RectTransform>();
        rRT.anchorMin = new Vector2(0f, 0.5f);
        rRT.anchorMax = new Vector2(0f, 0.5f);
        rRT.pivot = new Vector2(0f, 0.5f);
        rRT.anchoredPosition = new Vector2(68f, -54f);
        rRT.sizeDelta = new Vector2(396f, 420f);

        playButton = CreateMenuActionButton(rail.transform, "Btn_Continue", "INICIAR JORNADA",
            "CARREGANDO TELEMETRIA...", FragaProIconLibrary.ControlIcon.PlayArrow,
            new Vector2(0f, 152f), 74f,
            new Color(0.11f, 0.34f, 0.24f, 0.96f), new Color(0.28f, 0.92f, 0.60f, 0.85f), font, out playSubLabelText);

        btnOpenNewGame = CreateMenuActionButton(rail.transform, "Btn_NewGame", "NOVO MUNDO",
            "ESCOLHER SEED & TERRENO", FragaProIconLibrary.ControlIcon.MapCompass,
            new Vector2(0f, 68f), 62f,
            new Color(0.08f, 0.13f, 0.20f, 0.94f), new Color(0.32f, 0.72f, 0.92f, 0.48f), font, out _);

        btnOpenCoop = CreateMenuActionButton(rail.transform, "Btn_OpenCoop", "SALA CO-OP (ESQUADRAO)",
            "PREPARAR GRUPO E CODIGO", FragaProIconLibrary.ControlIcon.UsersSquad,
            new Vector2(0f, -4f), 62f,
            new Color(0.08f, 0.14f, 0.21f, 0.94f), new Color(0.30f, 0.86f, 0.78f, 0.48f), font, out _);

        btnOpenSettings = CreateMenuActionButton(rail.transform, "Btn_Settings", "CONFIGURACOES",
            "AUDIO, GRAFICOS & CONTROLES", FragaProIconLibrary.ControlIcon.SettingsGear,
            new Vector2(0f, -76f), 62f,
            new Color(0.08f, 0.13f, 0.20f, 0.94f), new Color(0.56f, 0.68f, 0.82f, 0.40f), font, out _);

        btnQuitGame = CreateMenuActionButton(rail.transform, "Btn_Quit", "SAIR DO JOGO",
            "ENCERRAR SESSAO", FragaProIconLibrary.ControlIcon.CancelCross,
            new Vector2(0f, -148f), 54f,
            new Color(0.20f, 0.08f, 0.10f, 0.92f), new Color(0.86f, 0.28f, 0.28f, 0.48f), font, out _);
    }

    private void BuildBriefingDossierCard(Transform parent, Font font)
    {
        GameObject card = new GameObject("BriefingCard");
        card.transform.SetParent(parent, false);
        RectTransform rt = card.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-60f, 52f);
        rt.sizeDelta = new Vector2(420f, 246f);

        Image bg = card.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetGlassPanelSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.08f, 0.13f, 0.88f);
        Outline outl = card.AddComponent<Outline>();
        outl.effectColor = new Color(0.32f, 0.78f, 0.96f, 0.42f);
        outl.effectDistance = new Vector2(1.4f, -1.4f);

        CreateLabel(card.transform, "Hdr", "RELATORIO DE CAMPO DA ILHA", font, 12,
            TextAnchor.MiddleLeft, new Color(0.46f, 0.90f, 1.0f), new Vector2(24f, 94f), new Vector2(360f, 24f));

        GameObject dotObj = new GameObject("TelemetryLiveDot");
        dotObj.transform.SetParent(card.transform, false);
        RectTransform dRT = dotObj.AddComponent<RectTransform>();
        dRT.anchoredPosition = new Vector2(-186f, 94f);
        dRT.sizeDelta = new Vector2(8f, 8f);
        Image dImg = dotObj.AddComponent<Image>();
        dImg.sprite = FragaProIconLibrary.GetCircleBackground();
        dImg.color = new Color(0.28f, 0.96f, 0.64f, 0.95f);
        dImg.raycastTarget = false;

        GameObject sepObj = new GameObject("BriefingAccentLine");
        sepObj.transform.SetParent(card.transform, false);
        RectTransform sRT = sepObj.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 1f);
        sRT.anchorMax = new Vector2(1f, 1f);
        sRT.pivot = new Vector2(0.5f, 1f);
        sRT.offsetMin = new Vector2(14f, -43f);
        sRT.offsetMax = new Vector2(-14f, -41f);
        Image sImg = sepObj.AddComponent<Image>();
        sImg.color = new Color(0.32f, 0.82f, 0.98f, 0.42f);
        sImg.raycastTarget = false;

        briefingSaveText = CreateLabel(card.transform, "SaveTelemetry", "SAVE: VERIFICANDO...", font, 12,
            TextAnchor.UpperLeft, new Color(0.95f, 0.97f, 1.0f), new Vector2(14f, 44f), new Vector2(380f, 46f));
        briefingSaveText.supportRichText = true;

        briefingCoopText = CreateLabel(card.transform, "CoopTelemetry", "ESQUADRAO: MODO SOLO", font, 12,
            TextAnchor.UpperLeft, new Color(0.76f, 0.96f, 0.86f), new Vector2(14f, -12f), new Vector2(380f, 46f));
        briefingCoopText.supportRichText = true;

        briefingControlsText = CreateLabel(card.transform, "ControlsTelemetry",
            "ATALHOS: [I] MOCHILA  |  [B] LIVRO  |  [K] STATUS  |  [F2] PC/MOBILE", font, 11,
            TextAnchor.UpperLeft, new Color(0.78f, 0.84f, 0.92f), new Vector2(14f, -72f), new Vector2(380f, 44f));
    }

    private void BuildNewGamePanel(Font font)
    {
        newGamePanel = CreateGlassModal("NewGame_Panel", new Vector2(560f, 430f), new Color(0.28f, 0.78f, 0.96f, 0.52f));
        CreateLabel(newGamePanel.transform, "Title", "CRIAR NOVO MUNDO", font, 22,
            TextAnchor.MiddleCenter, new Color(0.96f, 0.98f, 1.0f), new Vector2(0f, 166f), new Vector2(500f, 34f));
        CreateLabel(newGamePanel.transform, "Warn", "Aviso: gerar um mundo novo substitui o save local atual.", font, 12,
            TextAnchor.MiddleCenter, new Color(0.98f, 0.78f, 0.42f), new Vector2(0f, 132f), new Vector2(500f, 24f));

        CreateLabel(newGamePanel.transform, "SeedLbl", "SEED DE GERACAO PROCEDURAL", font, 11,
            TextAnchor.MiddleLeft, new Color(0.62f, 0.88f, 0.98f), new Vector2(-60f, 84f), new Vector2(340f, 20f));

        GameObject seedInputObj = new GameObject("Seed_Input");
        seedInputObj.transform.SetParent(newGamePanel.transform, false);
        RectTransform siRT = seedInputObj.AddComponent<RectTransform>();
        siRT.anchoredPosition = new Vector2(-58f, 48f);
        siRT.sizeDelta = new Vector2(356f, 46f);
        Image seedBg = seedInputObj.AddComponent<Image>();
        seedBg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        seedBg.type = Image.Type.Sliced;
        seedBg.color = new Color(0.03f, 0.06f, 0.10f, 0.96f);

        seedInput = seedInputObj.AddComponent<InputField>();
        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(seedInputObj.transform, false);
        RectTransform phRT = phObj.AddComponent<RectTransform>();
        StretchFull(phRT, 14f, 4f);
        Text phTxt = phObj.AddComponent<Text>();
        phTxt.font = font;
        phTxt.fontSize = 15;
        phTxt.fontStyle = FontStyle.Italic;
        phTxt.alignment = TextAnchor.MiddleLeft;
        phTxt.color = new Color(0.55f, 0.65f, 0.74f, 0.75f);
        phTxt.text = "Ex: 1913 (ou nome personalizado)";

        GameObject stObj = new GameObject("Text");
        stObj.transform.SetParent(seedInputObj.transform, false);
        RectTransform stRT = stObj.AddComponent<RectTransform>();
        StretchFull(stRT, 14f, 4f);
        Text seedTxt = stObj.AddComponent<Text>();
        seedTxt.font = font;
        seedTxt.fontSize = 16;
        seedTxt.fontStyle = FontStyle.Bold;
        seedTxt.alignment = TextAnchor.MiddleLeft;
        seedTxt.color = Color.white;
        seedInput.placeholder = phTxt;
        seedInput.textComponent = seedTxt;
        seedInput.text = "1913";

        // Botao Seed Aleatoria
        Button rndBtn = CreateFlatButton(newGamePanel.transform, "Btn_RandomSeed", "ALEATORIA",
            new Vector2(184f, 48f), new Vector2(114f, 46f), new Color(0.16f, 0.30f, 0.44f, 0.96f), font, 12);
        rndBtn.onClick.AddListener(() =>
        {
            if (seedInput != null) seedInput.text = Random.Range(1000, 99999).ToString();
        });

        // Chips de Seeds Pre-definidas
        string[] presetCodes = { "1913", "4096", "7777" };
        string[] presetLabels = { "1913 CLASSICA", "4096 DENSA", "7777 EXTREMA" };
        for (int i = 0; i < presetCodes.Length; i++)
        {
            string code = presetCodes[i];
            Button chip = CreateFlatButton(newGamePanel.transform, $"Btn_SeedPreset_{i}", presetLabels[i],
                new Vector2(-156f + i * 156f, -10f), new Vector2(146f, 34f), new Color(0.10f, 0.18f, 0.28f, 0.94f), font, 11);
            chip.onClick.AddListener(() =>
            {
                if (seedInput != null) seedInput.text = code;
            });
        }

        btnCreateWorld = CreateFlatButton(newGamePanel.transform, "Btn_CreateWorld", "GERAR MUNDO & ENTRAR",
            new Vector2(0f, -84f), new Vector2(360f, 54f), new Color(0.14f, 0.50f, 0.28f, 0.98f), font, 15);
        btnBackFromNewGame = CreateFlatButton(newGamePanel.transform, "Btn_Back", "VOLTAR",
            new Vector2(0f, -152f), new Vector2(220f, 42f), new Color(0.20f, 0.24f, 0.30f, 0.94f), font, 13);
    }

    private void BuildCoopLobbyPanel(Font font)
    {
        coopPanel = CreateGlassModal("Coop_Panel", new Vector2(590f, 540f), new Color(0.26f, 0.88f, 0.74f, 0.52f));
        CreateLabel(coopPanel.transform, "Title", "SALA COOPERATIVA (ESQUADRAO)", font, 21,
            TextAnchor.MiddleCenter, new Color(0.96f, 0.98f, 1.0f), new Vector2(0f, 224f), new Vector2(520f, 34f));
        CreateLabel(coopPanel.transform, "Sub", "LAN / Wi-Fi direto (porta 7777) ou VPN (ZeroTier / Radmin)", font, 12,
            TextAnchor.MiddleCenter, new Color(0.58f, 0.92f, 0.86f), new Vector2(0f, 194f), new Vector2(520f, 22f));

        GameObject statusBox = new GameObject("CoopStatusBox");
        statusBox.transform.SetParent(coopPanel.transform, false);
        RectTransform sbRT = statusBox.AddComponent<RectTransform>();
        sbRT.anchoredPosition = new Vector2(0f, 128f);
        sbRT.sizeDelta = new Vector2(504f, 94f);
        Image sbImg = statusBox.AddComponent<Image>();
        sbImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        sbImg.type = Image.Type.Sliced;
        sbImg.color = new Color(0.03f, 0.07f, 0.11f, 0.92f);

        coopStatusText = CreateLabel(statusBox.transform, "StatusTxt", "CARREGANDO SESSAO CO-OP...", font, 12,
            TextAnchor.MiddleCenter, new Color(0.95f, 0.98f, 1.0f), Vector2.zero, new Vector2(484f, 84f));
        coopStatusText.supportRichText = true;

        Button callsignBtn = CreateFlatButton(coopPanel.transform, "Btn_CoopCallsign", "ALTERNAR CODINOME DE CAMPO",
            new Vector2(-128f, 44f), new Vector2(244f, 42f), new Color(0.14f, 0.26f, 0.38f, 0.96f), font, 11);
        callsignBtn.onClick.AddListener(() =>
        {
            FragaCoopSession.GetOrCreate().CycleDefaultCallsign();
            RefreshDynamicMenuTexts();
        });

        Button hostBtn = CreateFlatButton(coopPanel.transform, "Btn_CoopToggleHost", "ATIVAR SALA CO-OP",
            new Vector2(128f, 44f), new Vector2(244f, 42f), new Color(0.16f, 0.44f, 0.36f, 0.96f), font, 11);
        coopHostBtnLabel = hostBtn.GetComponentInChildren<Text>();
        hostBtn.onClick.AddListener(() =>
        {
            FragaCoopSession coop = FragaCoopSession.GetOrCreate();
            if (coop.IsCoopSessionActive) coop.SetSoloMode();
            else coop.StartLanHost();
            RefreshDynamicMenuTexts();
        });

        Button previewBtn = CreateFlatButton(coopPanel.transform, "Btn_CoopPreviewSquad", "SIMULAR ESQUADRAO (3 JOGADORES)",
            new Vector2(-128f, -8f), new Vector2(244f, 42f), new Color(0.24f, 0.28f, 0.46f, 0.96f), font, 11);
        coopPreviewBtnLabel = previewBtn.GetComponentInChildren<Text>();
        previewBtn.onClick.AddListener(() =>
        {
            FragaCoopSession.GetOrCreate().ToggleSquadPreview();
            RefreshDynamicMenuTexts();
        });

        Button accessBtn = CreateFlatButton(coopPanel.transform, "Btn_CoopBuildAccess", "OBRA: GRUPO CO-OP",
            new Vector2(128f, -8f), new Vector2(244f, 42f), new Color(0.20f, 0.34f, 0.28f, 0.96f), font, 11);
        coopAccessBtnLabel = accessBtn.GetComponentInChildren<Text>();
        accessBtn.onClick.AddListener(() =>
        {
            FragaCoopSession.GetOrCreate().ToggleDefaultBuildAccess();
            RefreshDynamicMenuTexts();
        });

        // Barra de conexao por IP / Descoberta LAN
        GameObject ipObj = new GameObject("HostIp_Input");
        ipObj.transform.SetParent(coopPanel.transform, false);
        RectTransform ipRT = ipObj.AddComponent<RectTransform>();
        ipRT.anchoredPosition = new Vector2(-114f, -66f);
        ipRT.sizeDelta = new Vector2(276f, 40f);
        Image ipBg = ipObj.AddComponent<Image>();
        ipBg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        ipBg.type = Image.Type.Sliced;
        ipBg.color = new Color(0.03f, 0.06f, 0.10f, 0.96f);

        coopHostIpInput = ipObj.AddComponent<InputField>();
        GameObject ipPhObj = new GameObject("Placeholder");
        ipPhObj.transform.SetParent(ipObj.transform, false);
        StretchFull(ipPhObj.AddComponent<RectTransform>(), 12f, 4f);
        Text ipPhTxt = ipPhObj.AddComponent<Text>();
        ipPhTxt.font = font;
        ipPhTxt.fontSize = 12;
        ipPhTxt.fontStyle = FontStyle.Italic;
        ipPhTxt.alignment = TextAnchor.MiddleLeft;
        ipPhTxt.color = new Color(0.55f, 0.65f, 0.74f, 0.78f);
        ipPhTxt.text = "IP do amigo (ex: 192.168.1.15)";

        GameObject ipTxtObj = new GameObject("Text");
        ipTxtObj.transform.SetParent(ipObj.transform, false);
        StretchFull(ipTxtObj.AddComponent<RectTransform>(), 12f, 4f);
        Text ipValTxt = ipTxtObj.AddComponent<Text>();
        ipValTxt.font = font;
        ipValTxt.fontSize = 13;
        ipValTxt.fontStyle = FontStyle.Bold;
        ipValTxt.alignment = TextAnchor.MiddleLeft;
        ipValTxt.color = Color.white;
        coopHostIpInput.placeholder = ipPhTxt;
        coopHostIpInput.textComponent = ipValTxt;
        coopHostIpInput.text = FragaLanCoopTransport.GetLocalIPv4Address();

        Button findLanBtn = CreateFlatButton(coopPanel.transform, "Btn_FindLanRoom", "BUSCAR LAN",
            new Vector2(84f, -66f), new Vector2(104f, 40f), new Color(0.16f, 0.30f, 0.44f, 0.96f), font, 10);
        findLanBtn.onClick.AddListener(OnFindLanRoomPressed);

        Button joinIpBtn = CreateFlatButton(coopPanel.transform, "Btn_JoinByIp", "ENTRAR (IP)",
            new Vector2(198f, -66f), new Vector2(108f, 40f), new Color(0.16f, 0.48f, 0.56f, 0.96f), font, 10);
        joinIpBtn.onClick.AddListener(OnJoinByIpPressed);

        coopNetFeedbackText = CreateLabel(coopPanel.transform, "CoopNetFeedback",
            "Mesmo Wi-Fi: Buscar LAN  |  Internet: digite IP da VPN (ZeroTier/Radmin) ou porta 7777",
            font, 10, TextAnchor.MiddleCenter, new Color(0.72f, 0.90f, 0.98f), new Vector2(0f, -104f), new Vector2(512f, 24f));

        Button enterCoopBtn = CreateFlatButton(coopPanel.transform, "Btn_EnterCoopWorld", "ENTRAR NA ILHA COM ESTE ESQUADRAO",
            new Vector2(0f, -152f), new Vector2(400f, 48f), new Color(0.14f, 0.52f, 0.30f, 0.98f), font, 14);
        enterCoopBtn.onClick.AddListener(OnPlayPressed);

        Button backCoopBtn = CreateFlatButton(coopPanel.transform, "Btn_BackFromCoop", "VOLTAR",
            new Vector2(0f, -210f), new Vector2(220f, 38f), new Color(0.20f, 0.24f, 0.30f, 0.94f), font, 13);
        backCoopBtn.onClick.AddListener(() => SwitchPanel(mainPanel));
    }

    private void OnFindLanRoomPressed()
    {
        FragaLanCoopTransport transport = FragaLanCoopTransport.GetOrCreate();
        transport.StartLanDiscovery();
        var rooms = transport.DiscoveredRooms;
        if (rooms.Count > 0)
        {
            DiscoveredLanRoom room = rooms[0];
            if (coopHostIpInput != null)
                coopHostIpInput.text = $"{room.hostIp}:{room.tcpPort}";
            if (coopNetFeedbackText != null)
                coopNetFeedbackText.text = $"Sala LAN encontrada: {room.hostName} ({room.hostIp}:{room.tcpPort}) - clique ENTRAR (IP)";
        }
        else if (coopNetFeedbackText != null)
        {
            coopNetFeedbackText.text = "Buscando salas na rede Wi-Fi/LAN (UDP 47777)... ou digite o IP do amigo.";
        }
        RefreshDynamicMenuTexts();
    }

    private void OnJoinByIpPressed()
    {
        string targetIp = coopHostIpInput != null ? coopHostIpInput.text : string.Empty;
        FragaCoopSession coop = FragaCoopSession.GetOrCreate();
        bool started = coop.JoinLanHost(targetIp);
        if (coopNetFeedbackText != null)
        {
            coopNetFeedbackText.text = started
                ? $"Conectando em {targetIp}... apos conectar, clique ENTRAR NA ILHA."
                : "Digite o IP valido do host (ex: 192.168.1.15).";
        }
        RefreshDynamicMenuTexts();
    }

    private void BuildSettingsPanel(Font font)
    {
        settingsPanel = CreateGlassModal("Settings_Panel", new Vector2(580f, 470f), new Color(0.46f, 0.74f, 0.96f, 0.48f));
        CreateLabel(settingsPanel.transform, "Title", "CONFIGURACOES DO SISTEMA", font, 22,
            TextAnchor.MiddleCenter, new Color(0.96f, 0.98f, 1.0f), new Vector2(0f, 186f), new Vector2(500f, 34f));

        // Linha Volume Mestre + Slider completo (Background / Fill / Handle)
        CreateLabel(settingsPanel.transform, "VolLbl", "VOLUME MESTRE", font, 13,
            TextAnchor.MiddleLeft, new Color(0.84f, 0.92f, 0.98f), new Vector2(-148f, 126f), new Vector2(200f, 26f));
        volumeValueText = CreateLabel(settingsPanel.transform, "VolPct", "100%", font, 13,
            TextAnchor.MiddleRight, new Color(0.42f, 0.94f, 0.98f), new Vector2(202f, 126f), new Vector2(88f, 26f));

        GameObject sliderObj = new GameObject("Volume_Slider");
        sliderObj.transform.SetParent(settingsPanel.transform, false);
        RectTransform slRT = sliderObj.AddComponent<RectTransform>();
        slRT.anchoredPosition = new Vector2(0f, 90f);
        slRT.sizeDelta = new Vector2(490f, 28f);

        GameObject slBg = new GameObject("Background");
        slBg.transform.SetParent(sliderObj.transform, false);
        RectTransform sbRT = slBg.AddComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(0f, 0.25f);
        sbRT.anchorMax = new Vector2(1f, 0.75f);
        sbRT.offsetMin = Vector2.zero;
        sbRT.offsetMax = Vector2.zero;
        Image sbImg = slBg.AddComponent<Image>();
        sbImg.sprite = FragaProIconLibrary.GetPillSprite();
        sbImg.type = Image.Type.Sliced;
        sbImg.color = new Color(0.08f, 0.13f, 0.18f, 0.95f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.25f);
        faRT.anchorMax = new Vector2(1f, 0.75f);
        faRT.offsetMin = new Vector2(6f, 0f);
        faRT.offsetMax = new Vector2(-6f, 0f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fRT = fillObj.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero;
        fRT.offsetMax = Vector2.zero;
        Image fImg = fillObj.AddComponent<Image>();
        fImg.sprite = FragaProIconLibrary.GetPillSprite();
        fImg.type = Image.Type.Sliced;
        fImg.color = new Color(0.24f, 0.84f, 0.96f, 0.96f);

        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform haRT = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10f, 0f);
        haRT.offsetMax = new Vector2(-10f, 0f);

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleArea.transform, false);
        RectTransform hRT = handleObj.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(22f, 22f);
        Image hImg = handleObj.AddComponent<Image>();
        hImg.sprite = FragaProIconLibrary.GetCircleBackground();
        hImg.color = new Color(0.95f, 0.98f, 1.0f, 1f);

        masterVolumeSlider = sliderObj.AddComponent<Slider>();
        masterVolumeSlider.fillRect = fRT;
        masterVolumeSlider.handleRect = hRT;
        masterVolumeSlider.targetGraphic = hImg;
        masterVolumeSlider.direction = Slider.Direction.LeftToRight;
        masterVolumeSlider.minValue = 0f;
        masterVolumeSlider.maxValue = 1f;

        // Qualidade Grafica
        Button qBtn = CreateFlatButton(settingsPanel.transform, "Btn_QualitySetting", "QUALIDADE: MEDIO",
            new Vector2(-128f, 24f), new Vector2(240f, 46f), new Color(0.11f, 0.19f, 0.28f, 0.96f), font, 12);
        qualityBtnLabel = qBtn.GetComponentInChildren<Text>();
        qBtn.onClick.AddListener(CycleQualityLevel);

        // Limite FPS (PC 60 / Mobile 30)
        Button fpsBtn = CreateFlatButton(settingsPanel.transform, "Btn_FpsSetting", "LIMITE FPS: 60 FPS",
            new Vector2(128f, 24f), new Vector2(240f, 46f), new Color(0.11f, 0.19f, 0.28f, 0.96f), font, 12);
        fpsBtnLabel = fpsBtn.GetComponentInChildren<Text>();
        fpsBtn.onClick.AddListener(ToggleTargetFps);

        // Layout de Controles (Auto / PC / Mobile)
        Button ctrlBtn = CreateFlatButton(settingsPanel.transform, "Btn_ControlModeSetting", "CONTROLES: AUTOMATICO",
            new Vector2(0f, -38f), new Vector2(496f, 46f), new Color(0.14f, 0.24f, 0.34f, 0.96f), font, 12);
        controlModeBtnLabel = ctrlBtn.GetComponentInChildren<Text>();
        ctrlBtn.onClick.AddListener(CycleControlLayoutMode);

        // Reiniciar Tutorial
        Button tutBtn = CreateFlatButton(settingsPanel.transform, "Btn_ResetTutorial", "REINICIAR DICAS & TUTORIAL DE CAMPO",
            new Vector2(0f, -98f), new Vector2(496f, 42f), new Color(0.26f, 0.20f, 0.14f, 0.94f), font, 12);
        tutBtn.onClick.AddListener(() =>
        {
            PlayerPrefs.DeleteKey("Fraga_TutorialStep");
            PlayerPrefs.Save();
            if (settingsFeedbackText != null)
                settingsFeedbackText.text = "Tutorial reiniciado para a proxima partida.";
        });

        settingsFeedbackText = CreateLabel(settingsPanel.transform, "SettingsFeedback", "", font, 11,
            TextAnchor.MiddleCenter, new Color(0.54f, 0.94f, 0.72f), new Vector2(0f, -134f), new Vector2(490f, 22f));

        btnBackFromSettings = CreateFlatButton(settingsPanel.transform, "Btn_Back", "SALVAR & VOLTAR",
            new Vector2(0f, -178f), new Vector2(260f, 44f), new Color(0.16f, 0.44f, 0.30f, 0.96f), font, 13);
    }

    private GameObject CreateGlassModal(string name, Vector2 size, Color outlineColor)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(transform, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        StretchFull(rt);

        // Backdrop escuro no root do modal
        Image backdrop = panel.AddComponent<Image>();
        backdrop.color = new Color(0.02f, 0.03f, 0.05f, 0.62f);

        // Moldura central em vidro escuro
        GameObject card = new GameObject("ModalFrame");
        card.transform.SetParent(panel.transform, false);
        RectTransform cRT = card.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.5f, 0.5f);
        cRT.anchorMax = new Vector2(0.5f, 0.5f);
        cRT.pivot = new Vector2(0.5f, 0.5f);
        cRT.sizeDelta = size;
        Image cImg = card.AddComponent<Image>();
        cImg.sprite = FragaProIconLibrary.GetGlassPanelSprite();
        cImg.type = Image.Type.Sliced;
        cImg.color = new Color(0.05f, 0.08f, 0.13f, 0.96f);
        Outline cOut = card.AddComponent<Outline>();
        cOut.effectColor = outlineColor;
        cOut.effectDistance = new Vector2(1.6f, -1.6f);
        cImg.raycastTarget = false;

        return panel;
    }

    private Button CreateMenuActionButton(Transform parent, string name, string mainLabel, string subLabel,
        FragaProIconLibrary.ControlIcon icon, Vector2 anchoredPos, float height, Color bgColor, Color borderColor,
        Font font, out Text subTextOut)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(390f, height);

        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetGlassPanelSprite();
        bg.type = Image.Type.Sliced;
        bg.color = bgColor;

        Outline outl = btnObj.AddComponent<Outline>();
        outl.effectColor = borderColor;
        outl.effectDistance = new Vector2(1.3f, -1.3f);

        Button btn = btnObj.AddComponent<Button>();
        btnObj.AddComponent<UIJuiceModifier>();

        // Barra lateral de destaque
        GameObject accent = new GameObject("Accent");
        accent.transform.SetParent(btnObj.transform, false);
        RectTransform aRT = accent.AddComponent<RectTransform>();
        aRT.anchorMin = new Vector2(0f, 0.14f);
        aRT.anchorMax = new Vector2(0f, 0.86f);
        aRT.pivot = new Vector2(0f, 0.5f);
        aRT.anchoredPosition = new Vector2(5f, 0f);
        aRT.sizeDelta = new Vector2(4f, 0f);
        Image aImg = accent.AddComponent<Image>();
        aImg.color = new Color(borderColor.r, borderColor.g, borderColor.b, 0.95f);
        aImg.raycastTarget = false;

        // Icone vetorial SDF
        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(btnObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(18f, 0f);
        iRT.sizeDelta = new Vector2(26f, 26f);
        Image icoImg = icoObj.AddComponent<Image>();
        icoImg.sprite = FragaProIconLibrary.GetControlIcon(icon);
        icoImg.preserveAspect = true;
        icoImg.raycastTarget = false;

        // Titulo principal (primeiro Text encontrado por GetComponentInChildren<Text>())
        GameObject lblObj = new GameObject("Text");
        lblObj.transform.SetParent(btnObj.transform, false);
        RectTransform lRT = lblObj.AddComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0.44f);
        lRT.anchorMax = new Vector2(1f, 0.94f);
        lRT.offsetMin = new Vector2(56f, 0f);
        lRT.offsetMax = new Vector2(-14f, 0f);
        Text lblTxt = lblObj.AddComponent<Text>();
        lblTxt.font = font;
        lblTxt.fontSize = height >= 70f ? 18 : 15;
        lblTxt.fontStyle = FontStyle.Bold;
        lblTxt.alignment = TextAnchor.MiddleLeft;
        lblTxt.color = Color.white;
        lblTxt.text = mainLabel;
        lblTxt.raycastTarget = false;

        // Subtitulo informativo
        GameObject subObj = new GameObject("SubLabel");
        subObj.transform.SetParent(btnObj.transform, false);
        RectTransform sRT = subObj.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0.08f);
        sRT.anchorMax = new Vector2(1f, 0.46f);
        sRT.offsetMin = new Vector2(56f, 0f);
        sRT.offsetMax = new Vector2(-14f, 0f);
        subTextOut = subObj.AddComponent<Text>();
        subTextOut.font = font;
        subTextOut.fontSize = 10;
        subTextOut.fontStyle = FontStyle.Bold;
        subTextOut.alignment = TextAnchor.MiddleLeft;
        subTextOut.color = new Color(0.76f, 0.88f, 0.96f, 0.88f);
        subTextOut.text = subLabel;
        subTextOut.raycastTarget = false;

        return btn;
    }

    private Button CreateFlatButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        Color bgColor, Font font, int fontSize)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = obj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetPillSprite();
        img.type = Image.Type.Sliced;
        img.color = bgColor;

        Button btn = obj.AddComponent<Button>();
        obj.AddComponent<UIJuiceModifier>();

        CreateLabel(obj.transform, "Text", label, font, fontSize, TextAnchor.MiddleCenter, Color.white,
            Vector2.zero, new Vector2(size.x - 16f, size.y - 6f));
        return btn;
    }

    private static Text CreateLabel(Transform parent, string name, string text, Font font, int fontSize,
        TextAnchor align, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = color;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void StretchFull(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    private void SetupPanels()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (newGamePanel != null) newGamePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (coopPanel != null) coopPanel.SetActive(false);
    }

    private void SetupButtons()
    {
        bool hasSave = HasAnySaveFile();
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayPressed);
            Text playLbl = playButton.GetComponentInChildren<Text>();
            if (playLbl != null) playLbl.text = hasSave ? "CONTINUAR JORNADA" : "INICIAR JORNADA";
            playButton.interactable = true;
        }

        if (btnOpenNewGame != null)
        {
            btnOpenNewGame.onClick.RemoveAllListeners();
            btnOpenNewGame.onClick.AddListener(() => SwitchPanel(newGamePanel));
        }
        if (btnOpenCoop != null)
        {
            btnOpenCoop.onClick.RemoveAllListeners();
            btnOpenCoop.onClick.AddListener(() => SwitchPanel(coopPanel));
        }
        if (btnOpenSettings != null)
        {
            btnOpenSettings.onClick.RemoveAllListeners();
            btnOpenSettings.onClick.AddListener(() => SwitchPanel(settingsPanel));
        }
        if (btnQuitGame != null)
        {
            btnQuitGame.onClick.RemoveAllListeners();
            btnQuitGame.onClick.AddListener(OnQuitPressed);
        }

        if (btnCreateWorld != null)
        {
            btnCreateWorld.onClick.RemoveAllListeners();
            btnCreateWorld.onClick.AddListener(OnCreateWorldPressed);
        }
        if (btnBackFromNewGame != null)
        {
            btnBackFromNewGame.onClick.RemoveAllListeners();
            btnBackFromNewGame.onClick.AddListener(() => SwitchPanel(mainPanel));
        }

        if (btnBackFromSettings != null)
        {
            btnBackFromSettings.onClick.RemoveAllListeners();
            btnBackFromSettings.onClick.AddListener(() =>
            {
                PlayerPrefs.Save();
                SwitchPanel(mainPanel);
            });
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.value = AudioListener.volume;
            masterVolumeSlider.onValueChanged.AddListener((val) =>
            {
                float clamped = Mathf.Clamp01(val);
                AudioListener.volume = clamped;
                PlayerPrefs.SetFloat("Fraga_MasterVolume", clamped);
                if (volumeValueText != null)
                    volumeValueText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            });
        }
    }

    public void SwitchPanel(GameObject targetPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (newGamePanel != null) newGamePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (coopPanel != null) coopPanel.SetActive(false);

        if (targetPanel != null) targetPanel.SetActive(true);
        RefreshDynamicMenuTexts();
    }

    private void RefreshDynamicMenuTexts()
    {
        bool hasSave = HasAnySaveFile();
        int day = 1, seed = 1913;
        float tod = 10f;
        bool hasMeta = saveSystem != null
            ? saveSystem.TryPeekSaveMetadata(out day, out seed, out tod)
            : SaveSystem.TryPeekDefaultSaveMetadata(out day, out seed, out tod);

        if (playSubLabelText != null)
        {
            playSubLabelText.text = (hasSave && hasMeta)
                ? $"DIA {day}  |  SEED {seed}  |  {FormatHour(tod)}"
                : (hasSave ? "CONTINUAR PROGRESSO SALVO" : "DESEMBARCAR NA ILHA  |  SEED 1913");
        }

        if (briefingSaveText != null)
        {
            briefingSaveText.text = (hasSave && hasMeta)
                ? $"SAVE ATIVO: <color=#66FFAA>DIA {day}</color> ({FormatHour(tod)})\nSEED DO MUNDO: {seed}"
                : "REGISTRO: <color=#FFD166>SEM SAVE LOCAL</color>\nPRONTO PARA NOVO DESEMBARQUE";
        }

        FragaCoopSession coop = FragaCoopSession.Instance;
        string playerCallsign = coop != null ? coop.localPlayerName : PlayerPrefs.GetString("Fraga_Coop_PlayerName", "Sobrevivente");
        bool coopActive = coop != null && coop.IsCoopSessionActive;
        string joinCode = coop != null ? coop.sessionJoinCode : "FRAGA-1913";
        bool sharedDefault = coop == null ? PlayerPrefs.GetInt("Fraga_Coop_SharedDefault", 0) == 0 : coop.DefaultStructuresShared;
        int memberCount = coop != null ? coop.Members.Count : 1;
        string localIp = coop != null ? coop.LocalLanAddress : FragaLanCoopTransport.GetLocalIPv4Address();
        FragaLanCoopTransport transport = FragaLanCoopTransport.Instance;
        string netState = transport != null ? transport.ConnectionStatus : "Desconectado (Solo)";

        if (briefingCoopText != null)
        {
            briefingCoopText.text = coopActive
                ? $"SALA CO-OP: <color=#55FFCC>{joinCode}</color> ({memberCount} MEMBROS)\nIP LOCAL: {localIp}:7777  |  OBRA: {(sharedDefault ? "GRUPO" : "PRIVADA")}"
                : $"MODO: SOLO  |  CODINOME: {playerCallsign}\nIP REDE: {localIp}:7777  |  OBRA: {(sharedDefault ? "GRUPO CO-OP" : "PRIVADA")}";
        }

        if (coopStatusText != null)
        {
            string modeLabel = !coopActive
                ? "<color=#FFD166>MODO SOLO</color>"
                : (coop != null && coop.sessionMode == CoopSessionMode.CoopClient
                    ? $"<color=#66DDFF>CLIENTE CO-OP</color>"
                    : $"<color=#55FFCC>ANFITRIAO CO-OP ({joinCode})</color>");
            coopStatusText.text =
                $"SESSAO: {modeLabel}  |  MEMBROS: {memberCount}/4\n" +
                $"SEU IP NA REDE: <color=#7CFFCC>{localIp}:7777</color>  |  STATUS: {netState}\n" +
                $"CODINOME: <color=#FFFFFF>{playerCallsign}</color>  |  ACESSO OBRA: {(sharedDefault ? "GRUPO CO-OP" : "PRIVADO")}";
        }
        if (transport != null && transport.DiscoveredRooms.Count > 0 && coopNetFeedbackText != null)
        {
            DiscoveredLanRoom firstRoom = transport.DiscoveredRooms[0];
            coopNetFeedbackText.text = $"Sala LAN detectada: {firstRoom.hostName} ({firstRoom.hostIp}:{firstRoom.tcpPort})";
        }
        if (coopHostBtnLabel != null)
            coopHostBtnLabel.text = coopActive ? "DESATIVAR CO-OP (IR SOLO)" : "ATIVAR SALA CO-OP";
        if (coopPreviewBtnLabel != null)
            coopPreviewBtnLabel.text = (coop != null && coop.IsSquadPreviewEnabled) ? "OCULTAR ESQUADRAO DEMO" : "SIMULAR ESQUADRAO (3P)";
        if (coopAccessBtnLabel != null)
            coopAccessBtnLabel.text = sharedDefault ? "OBRA: GRUPO CO-OP" : "OBRA: PRIVADA (SO EU)";

        int ctrlMode = PlayerPrefs.GetInt(PrefControlModeKey, 0);
        string ctrlText = ctrlMode == 1 ? "MOBILE (TOUCH)" : (ctrlMode == 2 ? "PC (TECLADO/MOUSE)" : "AUTOMATICO");
        if (briefingControlsText != null)
            briefingControlsText.text = $"CONTROLES: {ctrlText}\nATALHOS: [I] MOCHILA  |  [B] LIVRO  |  [K] STATUS  |  [F2] LAYOUT";
        if (controlModeBtnLabel != null)
            controlModeBtnLabel.text = $"CONTROLES: {ctrlText}";

        if (volumeValueText != null)
            volumeValueText.text = $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%";

        if (qualityBtnLabel != null)
        {
            string[] names = QualitySettings.names;
            int qIdx = QualitySettings.GetQualityLevel();
            string qName = (names != null && qIdx >= 0 && qIdx < names.Length) ? names[qIdx].ToUpper() : $"NIVEL {qIdx}";
            qualityBtnLabel.text = $"QUALIDADE: {qName}";
        }

        if (fpsBtnLabel != null)
        {
            int fps = PlayerPrefs.GetInt(PrefTargetFpsKey, 60);
            fpsBtnLabel.text = $"LIMITE FPS: {(fps == 30 ? "30 (ECONOMIA)" : "60 FPS")}";
        }
    }

    private void CycleQualityLevel()
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0) return;
        int next = (QualitySettings.GetQualityLevel() + 1) % names.Length;
        QualitySettings.SetQualityLevel(next, false);
        PlayerPrefs.SetInt(PrefQualityKey, next);
        PlayerPrefs.Save();
        RefreshDynamicMenuTexts();
    }

    private void ToggleTargetFps()
    {
        int current = PlayerPrefs.GetInt(PrefTargetFpsKey, 60);
        int next = current == 60 ? 30 : 60;
        Application.targetFrameRate = next;
        PlayerPrefs.SetInt(PrefTargetFpsKey, next);
        PlayerPrefs.Save();
        RefreshDynamicMenuTexts();
    }

    private void CycleControlLayoutMode()
    {
        int current = PlayerPrefs.GetInt(PrefControlModeKey, 0);
        int next = (current + 1) % 3;
        PlayerPrefs.SetInt(PrefControlModeKey, next);
        PlayerPrefs.Save();
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.SetControlLayoutMode((FragaInputRouter.ControlLayoutMode)next);
        }
        RefreshDynamicMenuTexts();
    }

    private bool HasAnySaveFile()
    {
        if (saveSystem != null) return saveSystem.HasSaveFile();
        string path = Application.persistentDataPath + "/fraga_save_v2.dat";
        return System.IO.File.Exists(path) || System.IO.File.Exists(path + ".bak");
    }

    private static string FormatHour(float hour)
    {
        float h = Mathf.Repeat(hour, 24f);
        int hh = Mathf.FloorToInt(h);
        int mm = Mathf.FloorToInt((h - hh) * 60f);
        return $"{hh:00}:{mm:00}";
    }

    private void OnPlayPressed()
    {
        if (HasAnySaveFile())
        {
            if (gameManager != null) gameManager.ContinueGame();
        }
        else
        {
            if (gameManager != null) gameManager.StartNewGame(1913);
        }
        StartCoroutine(TransitionToGame());
    }

    private void OnCreateWorldPressed()
    {
        string savePath = Application.persistentDataPath + "/fraga_save_v2.dat";
        if (System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath);
        if (System.IO.File.Exists(savePath + ".bak")) System.IO.File.Delete(savePath + ".bak");

        int parsedSeed = 1913;
        if (seedInput != null && !string.IsNullOrEmpty(seedInput.text))
        {
            if (!int.TryParse(seedInput.text, out parsedSeed))
            {
                parsedSeed = Mathf.Abs(seedInput.text.GetHashCode());
            }
        }

        if (gameManager != null) gameManager.StartNewGame(parsedSeed);
        StartCoroutine(TransitionToGame());
    }

    private void OnQuitPressed()
    {
#if UNITY_EDITOR
        Debug.Log("[MainMenuManager] Solicitacao de saida do jogo (Editor permanece ativo).");
#endif
        Application.Quit();
    }

    private IEnumerator TransitionToGame()
    {
        isTransitioning = true;
        SwitchPanel(null);

        if (audioManager != null) audioManager.PlayHorrorSting();

        FirstPersonCamera fpCam = mainCamera != null ? mainCamera.GetComponent<FirstPersonCamera>() : null;
        bool isFirstPerson = (fpCam != null) || (mainCamera != null && mainCamera.transform.parent != null);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 2.0f;
            if (mainMenuCanvasGroup != null) mainMenuCanvasGroup.alpha = Mathf.Clamp01(1f - t);

            if (mainCamera != null && !isFirstPerson)
            {
                mainCamera.transform.position = Vector3.Lerp(menuCameraPos, gameCameraPos, t);
                mainCamera.transform.rotation = Quaternion.Lerp(Quaternion.Euler(15f, 45f, 0f), Quaternion.Euler(45f, 45f, 0f), t);
            }

            yield return null;
        }

        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha = 0f;
            mainMenuCanvasGroup.interactable = false;
            mainMenuCanvasGroup.blocksRaycasts = false;
            mainMenuCanvasGroup.gameObject.SetActive(false);
        }

        if (mainCamera != null && isFirstPerson)
        {
            mainCamera.transform.localPosition = new Vector3(0f, 1.75f, 0.15f);
            mainCamera.transform.localRotation = Quaternion.identity;
        }

        if (uiManager != null) uiManager.gameObject.SetActive(true);
        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ForceRefreshNow();

        Time.timeScale = 1f;
        isTransitioning = false;

        var isoCam = mainCamera != null ? mainCamera.GetComponent<IsometricCamera>() : FindAnyObjectByType<IsometricCamera>();
        if (isoCam != null && !isFirstPerson)
        {
            isoCam.SnapToTarget();
        }
    }
}
