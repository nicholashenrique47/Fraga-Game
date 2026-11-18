using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// BlueprintPlacementManager: Construcao Holografica Livre com LIVRO DE SOBREVIVENCIA E CONSTRUCAO estilo The Forest (Tecla B / Botao Obra)
/// e insercao rapida de Toras/Madeira/Pedras via InteractionDetector, Cartao da Obra ou golpe de Machado (Zero-GC, Zero-Find no Update).
/// Cada estrutura concluida ganha funcao real estilo The Forest: Abrigo/Cabana (Dormir/Descansar em qualquer horario + Salvar),
/// Secador de Alimentos (Charque nao-perecivel), Coletor de Chuva (reabastece Cantil), Bau de Base, Cesto de Toras, Porta articulada e Palicada defensiva.
/// </summary>
public class BlueprintPlacementManager : MonoBehaviour
{
    public static BlueprintPlacementManager Instance { get; private set; }

    [System.Serializable]
    public class StructureProject
    {
        public string structureName;
        public int requiredLogs;
        public int requiredStones;
        public GameObject completedPrefab;
    }

    [Header("Building Blueprints")]
    public StructureProject[] availableProjects;

    private int selectedProjectIndex = 0;
    private bool isPlacingGhost = false;
    public bool IsPlacingGhost => isPlacingGhost;
    public bool IsBookOpen => selectorStripObj != null && selectorStripObj.activeSelf;
    public bool snapEnabled = true;

    private GameObject activeGhostObj;
    private float ghostRotationY = 0f;
    private float placementConfirmReadyTime = 0f;
    private readonly RaycastHit[] ghostRayHits = new RaycastHit[16];
    private Material activeGhostMat;
    private bool isGhostSnapped = false;
    private bool isGhostPlacementValid = true;
    private string ghostInvalidReason = string.Empty;
    private float nextPlacementHeaderUpdateTime = 0f;

    private GameObject selectorStripObj;
    private GameObject placementBarObj;
    private Text placementHeaderText;
    private Text snapBtnLabel;
    private Text placementCoopBtnLabel;
    private Image placementCoopBtnImg;

    // UI do Livro de Sobrevivencia e Construcao (estilo The Forest)
    private int currentBookChapter = 0;
    private bool filterOnlyReadyBlueprints = false;
    private int pinnedProjectIndex = -1;
    private Transform rightPageContentRoot;
    private RectTransform rightPageContentRT;
    private ScrollRect rightPageScrollRect;
    private Text rightPageTitleText;
    private Text leftPageInventorySummaryText;
    private Text bookBaseStatsText;
    private Text bookCoopAccessBtnLabel;
    private Image bookCoopAccessBtnImg;
    private Text filterReadyBtnLabel;
    private Image filterReadyBtnImg;
    private Text rightReadySummaryText;
    private Text rightFooterText;
    private Image[] chapterTabImages;
    private Image[] bookmarkTabImages;

    public bool FilterOnlyReadyBlueprints => filterOnlyReadyBlueprints;
    public int PinnedProjectIndex => pinnedProjectIndex;

    private AudioSource audioSource;
    private AudioClip hammerHitClip;
    private AudioClip placeGhostClip;
    private AudioClip completeBuildClip;

    private void Awake()
    {
        Instance = this;
        SetupDefaultProjects();
        SetupAudio();
    }

    public static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.35f;
        audioSource.volume = 0.85f;

        hammerHitClip = GenerateHammerHitSound();
        placeGhostClip = GeneratePlaceGhostSound();
        completeBuildClip = GenerateCompleteBuildSound();
    }

    private void SetupDefaultProjects()
    {
        availableProjects = new StructureProject[]
        {
            new StructureProject { structureName = "Refugio de Folhas", requiredLogs = 4, requiredStones = 2 },
            new StructureProject { structureName = "Cabana de Toras", requiredLogs = 10, requiredStones = 6 },
            new StructureProject { structureName = "Palicada Pontiaguda", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Secador de Alimentos", requiredLogs = 2, requiredStones = 1 },
            new StructureProject { structureName = "Coletor de Chuva", requiredLogs = 2, requiredStones = 3 },
            new StructureProject { structureName = "Piso de Madeira", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Parede de Toras", requiredLogs = 4, requiredStones = 0 },
            new StructureProject { structureName = "Parede com Porta", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Teto de Madeira", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Escada de Toras", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Cesto de Toras", requiredLogs = 2, requiredStones = 1 },
            new StructureProject { structureName = "Bau de Base", requiredLogs = 2, requiredStones = 1 }
        };
    }

    public void ToggleBuildMenuOrPlacement()
    {
        if (isPlacingGhost)
        {
            CancelPlacement();
            return;
        }

        if (selectorStripObj != null && selectorStripObj.activeSelf)
        {
            CloseSurvivalBook();
            return;
        }

        ShowBuildSelectorStrip();
    }

    public void CloseSurvivalBook()
    {
        if (selectorStripObj != null)
            selectorStripObj.SetActive(false);

        bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
        bool otherModalOpen = (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen) ||
                              StorageChestInteractable.IsAnyChestOpen ||
                              SurvivalMapSystem.IsMapOpen;
        if (!touchActive && !otherModalOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ShowBuildSelectorStrip()
    {
        if (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen)
        {
            SurvivalInventoryCraftUI.Instance.CloseMenu();
        }

        if (selectorStripObj == null) BuildSelectorUI();
        if (selectorStripObj != null)
        {
            selectorStripObj.SetActive(true);
            selectorStripObj.transform.SetAsLastSibling();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshBookPage(currentBookChapter);
        }
    }

    private void BuildSelectorUI()
    {
        Canvas canvas = HUDSurvivalOverlay.GetGameplayCanvas();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        selectorStripObj = new GameObject("SurvivalConstructionBookModal");
        selectorStripObj.transform.SetParent(canvas.transform, false);
        RectTransform rootRT = selectorStripObj.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dimBg = selectorStripObj.AddComponent<Image>();
        dimBg.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);

        // Capa de Couro do Livro (Estilo The Forest)
        GameObject leatherCover = new GameObject("LeatherBookCover");
        leatherCover.transform.SetParent(selectorStripObj.transform, false);
        RectTransform coverRT = leatherCover.AddComponent<RectTransform>();
        coverRT.anchorMin = new Vector2(0.5f, 0.5f);
        coverRT.anchorMax = new Vector2(0.5f, 0.5f);
        coverRT.pivot = new Vector2(0.5f, 0.5f);
        coverRT.anchoredPosition = new Vector2(0f, 6f);
        coverRT.sizeDelta = new Vector2(1040f, 648f);

        Image coverImg = leatherCover.AddComponent<Image>();
        coverImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        coverImg.type = Image.Type.Sliced;
        coverImg.color = new Color(0.17f, 0.10f, 0.06f, 0.99f);

        Outline coverOutline = leatherCover.AddComponent<Outline>();
        coverOutline.effectColor = new Color(0.66f, 0.48f, 0.24f, 0.90f);
        coverOutline.effectDistance = new Vector2(2f, -2f);

        CreateBrassCorner(leatherCover.transform, "Corner_TL", new Vector2(0f, 1f), new Vector2(10f, -10f));
        CreateBrassCorner(leatherCover.transform, "Corner_TR", new Vector2(1f, 1f), new Vector2(-10f, -10f));
        CreateBrassCorner(leatherCover.transform, "Corner_BL", new Vector2(0f, 0f), new Vector2(10f, 10f));
        CreateBrassCorner(leatherCover.transform, "Corner_BR", new Vector2(1f, 0f), new Vector2(-10f, 10f));

        // Pagina Esquerda (Pergaminho)
        GameObject leftPage = new GameObject("LeftPage_Parchment");
        leftPage.transform.SetParent(leatherCover.transform, false);
        RectTransform lpRT = leftPage.AddComponent<RectTransform>();
        lpRT.anchorMin = new Vector2(0.5f, 0.5f);
        lpRT.anchorMax = new Vector2(0.5f, 0.5f);
        lpRT.pivot = new Vector2(0.5f, 0.5f);
        lpRT.anchoredPosition = new Vector2(-246f, -2f);
        lpRT.sizeDelta = new Vector2(478f, 594f);

        Image lpImg = leftPage.AddComponent<Image>();
        lpImg.sprite = FragaProIconLibrary.GetPillSprite();
        lpImg.type = Image.Type.Sliced;
        lpImg.color = new Color(0.86f, 0.80f, 0.67f, 0.98f);

        // Lombada Central do Livro
        GameObject spine = new GameObject("BookSpine");
        spine.transform.SetParent(leatherCover.transform, false);
        RectTransform spRT = spine.AddComponent<RectTransform>();
        spRT.anchorMin = new Vector2(0.5f, 0.5f);
        spRT.anchorMax = new Vector2(0.5f, 0.5f);
        spRT.sizeDelta = new Vector2(20f, 606f);
        spRT.anchoredPosition = new Vector2(0f, -2f);
        Image spImg = spine.AddComponent<Image>();
        spImg.color = new Color(0.12f, 0.07f, 0.04f, 0.96f);
        for (int r = 0; r < 5; r++)
        {
            GameObject ring = new GameObject($"SpineRing_{r}");
            ring.transform.SetParent(spine.transform, false);
            RectTransform rRT = ring.AddComponent<RectTransform>();
            rRT.anchoredPosition = new Vector2(0f, 210f - r * 105f);
            rRT.sizeDelta = new Vector2(26f, 10f);
            Image rImg = ring.AddComponent<Image>();
            rImg.sprite = FragaProIconLibrary.GetPillSprite();
            rImg.type = Image.Type.Sliced;
            rImg.color = new Color(0.72f, 0.54f, 0.26f, 0.95f);
            rImg.raycastTarget = false;
        }

        // Pagina Direita (Pergaminho)
        GameObject rightPage = new GameObject("RightPage_Parchment");
        rightPage.transform.SetParent(leatherCover.transform, false);
        RectTransform rpRT = rightPage.AddComponent<RectTransform>();
        rpRT.anchorMin = new Vector2(0.5f, 0.5f);
        rpRT.anchorMax = new Vector2(0.5f, 0.5f);
        rpRT.pivot = new Vector2(0.5f, 0.5f);
        rpRT.anchoredPosition = new Vector2(246f, -2f);
        rpRT.sizeDelta = new Vector2(478f, 594f);

        Image rpImg = rightPage.AddComponent<Image>();
        rpImg.sprite = FragaProIconLibrary.GetPillSprite();
        rpImg.type = Image.Type.Sliced;
        rpImg.color = new Color(0.88f, 0.82f, 0.69f, 0.98f);

        // Grade tecnica de rascunho no pergaminho direito
        GameObject gridObj = new GameObject("DraftingGridOverlay");
        gridObj.transform.SetParent(rightPage.transform, false);
        RectTransform gRT = gridObj.AddComponent<RectTransform>();
        gRT.anchorMin = Vector2.zero;
        gRT.anchorMax = Vector2.one;
        gRT.offsetMin = new Vector2(14f, 44f);
        gRT.offsetMax = new Vector2(-14f, -58f);
        Image gridImg = gridObj.AddComponent<Image>();
        gridImg.sprite = FragaProIconLibrary.GetBlueprintGridSprite();
        gridImg.type = Image.Type.Tiled;
        gridImg.color = new Color(0.38f, 0.30f, 0.20f, 0.24f);
        gridImg.raycastTarget = false;

        // Marcadores laterais na borda direita (acesso rapido entre capitulos)
        string[] bookmarkLabels = { "GUIA", "ABRIG", "AGUA", "DEFEN", "MODUL" };
        bookmarkTabImages = new Image[bookmarkLabels.Length];
        for (int b = 0; b < bookmarkLabels.Length; b++)
        {
            int capIdx = b;
            GameObject bm = new GameObject($"BookmarkTab_{b}");
            bm.transform.SetParent(rightPage.transform, false);
            RectTransform bmRT = bm.AddComponent<RectTransform>();
            bmRT.anchorMin = new Vector2(1f, 0.5f);
            bmRT.anchorMax = new Vector2(1f, 0.5f);
            bmRT.pivot = new Vector2(0f, 0.5f);
            bmRT.anchoredPosition = new Vector2(-4f, 165f - b * 76f);
            bmRT.sizeDelta = new Vector2(46f, 62f);

            Image bmImg = bm.AddComponent<Image>();
            bmImg.sprite = FragaProIconLibrary.GetPillSprite();
            bmImg.type = Image.Type.Sliced;
            bmImg.color = new Color(0.34f, 0.22f, 0.12f, 0.96f);
            bookmarkTabImages[b] = bmImg;
            bm.AddComponent<Button>().onClick.AddListener(() => RefreshBookPage(capIdx));

            CreateBookText(bm.transform, "Lbl", bookmarkLabels[b], font, 9,
                new Vector2(2f, 0f), new Vector2(40f, 54f), new Color(0.98f, 0.94f, 0.84f), TextAnchor.MiddleCenter);
        }

        // Cabecalho da Pagina Esquerda
        CreateBookText(leftPage.transform, "LeftHeader", "LIVRO DE SOBREVIVENCIA & OBRA", font, 16,
            new Vector2(0f, 254f), new Vector2(416f, 28f), new Color(0.24f, 0.14f, 0.06f), TextAnchor.MiddleCenter);
        CreateBookText(leftPage.transform, "LeftSub", "MANUAL DE CAMPO DA FRAGA - ESCOLHA UM CAPITULO", font, 11,
            new Vector2(0f, 232f), new Vector2(416f, 20f), new Color(0.42f, 0.28f, 0.15f), TextAnchor.MiddleCenter);

        string[] chapterTitles = new string[]
        {
            "1. GUIA: COMO COLETAR, FAZER CORDA E MACHADO",
            "2. ABRIGOS & CABANAS (DORMIR E SALVAR)",
            "3. AGUA POTAVEL, SECADOR & FOGUEIRA",
            "4. DEFESA (PALICADA) & ESTOCAGEM (BAU/CESTO)",
            "5. CONSTRUCAO MODULAR (PISOS, PAREDES E TETOS)"
        };

        chapterTabImages = new Image[chapterTitles.Length];
        for (int c = 0; c < chapterTitles.Length; c++)
        {
            int capIdx = c;
            GameObject tabBtn = new GameObject($"ChapterTab_{c}");
            tabBtn.transform.SetParent(leftPage.transform, false);
            RectTransform tRT = tabBtn.AddComponent<RectTransform>();
            tRT.anchoredPosition = new Vector2(0f, 182f - c * 46f);
            tRT.sizeDelta = new Vector2(414f, 38f);

            Image tImg = tabBtn.AddComponent<Image>();
            tImg.sprite = FragaProIconLibrary.GetPillSprite();
            tImg.type = Image.Type.Sliced;
            tImg.color = new Color(0.26f, 0.18f, 0.11f, 0.95f);
            chapterTabImages[c] = tImg;

            tabBtn.AddComponent<Button>().onClick.AddListener(() => RefreshBookPage(capIdx));

            CreateBookText(tabBtn.transform, "Txt", chapterTitles[c], font, 12,
                Vector2.zero, new Vector2(396f, 32f), new Color(0.98f, 0.93f, 0.82f), TextAnchor.MiddleLeft);
        }

        // Resumo do Fluxo de Sobrevivencia fixo na parte inferior da pagina esquerda
        GameObject flowBox = new GameObject("QuickFlowCard");
        flowBox.transform.SetParent(leftPage.transform, false);
        RectTransform fbRT = flowBox.AddComponent<RectTransform>();
        fbRT.anchoredPosition = new Vector2(0f, -72f);
        fbRT.sizeDelta = new Vector2(416f, 134f);
        Image fbImg = flowBox.AddComponent<Image>();
        fbImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        fbImg.type = Image.Type.Sliced;
        fbImg.color = new Color(0.76f, 0.69f, 0.55f, 0.95f);

        string flowRuleText =
            "REGRAS DE OURO DA SOBREVIVENCIA:\n" +
            "- Galhos Soltos, Pedras Pequenas e Ervas no chao: olhe e aperte [E].\n" +
            "- Como fazer CORDA: aperte [I] (Fabricacao) -> use 2x Ervas = 2x Cordas.\n" +
            "- Como fazer MACHADO: 3x Madeira + 2x Pedra + 1x Corda no menu [I].\n" +
            "- Cortar Arvores e Quebrar Rochas: segure o Machado e CLIQUE NO MOUSE!\n" +
            "- Apos posicionar um projeto azul, aproxime-se e aperte [E] (ou use o botao no HUD).";

        CreateBookText(flowBox.transform, "FlowTxt", flowRuleText, font, 10,
            Vector2.zero, new Vector2(396f, 124f), new Color(0.18f, 0.11f, 0.05f), TextAnchor.MiddleLeft);

        // Contador ao vivo da mochila no rodape da pagina esquerda
        GameObject bagFooter = new GameObject("BagStatusFooter");
        bagFooter.transform.SetParent(leftPage.transform, false);
        RectTransform bfRT = bagFooter.AddComponent<RectTransform>();
        bfRT.anchoredPosition = new Vector2(0f, -162f);
        bfRT.sizeDelta = new Vector2(416f, 32f);
        Image bfImg = bagFooter.AddComponent<Image>();
        bfImg.sprite = FragaProIconLibrary.GetPillSprite();
        bfImg.type = Image.Type.Sliced;
        bfImg.color = new Color(0.16f, 0.11f, 0.07f, 0.94f);

        leftPageInventorySummaryText = CreateBookText(bagFooter.transform, "InvSummary",
            "NA MOCHILA: Madeira: 0 | Pedra: 0 | Ervas: 0 | Corda: 0", font, 11,
            Vector2.zero, new Vector2(400f, 28f), new Color(0.98f, 0.88f, 0.52f), TextAnchor.MiddleCenter);

        // Barra de Telemetria da Base + Permissao Co-op (Compartilhada vs Privada)
        GameObject baseStatsBox = new GameObject("BaseStatsBox");
        baseStatsBox.transform.SetParent(leftPage.transform, false);
        RectTransform bsRT = baseStatsBox.AddComponent<RectTransform>();
        bsRT.anchoredPosition = new Vector2(-106f, -206f);
        bsRT.sizeDelta = new Vector2(202f, 34f);
        Image bsImg = baseStatsBox.AddComponent<Image>();
        bsImg.sprite = FragaProIconLibrary.GetPillSprite();
        bsImg.type = Image.Type.Sliced;
        bsImg.color = new Color(0.22f, 0.16f, 0.10f, 0.94f);
        bookBaseStatsText = CreateBookText(baseStatsBox.transform, "Txt",
            "Base: 0 prontas | 0 obras", font, 11,
            Vector2.zero, new Vector2(190f, 28f), new Color(0.96f, 0.90f, 0.72f), TextAnchor.MiddleCenter);

        GameObject coopAccessBtn = new GameObject("Btn_BookCoopAccess");
        coopAccessBtn.transform.SetParent(leftPage.transform, false);
        RectTransform caRT = coopAccessBtn.AddComponent<RectTransform>();
        caRT.anchoredPosition = new Vector2(106f, -206f);
        caRT.sizeDelta = new Vector2(202f, 34f);
        bookCoopAccessBtnImg = coopAccessBtn.AddComponent<Image>();
        bookCoopAccessBtnImg.sprite = FragaProIconLibrary.GetPillSprite();
        bookCoopAccessBtnImg.type = Image.Type.Sliced;
        bookCoopAccessBtnImg.color = new Color(0.14f, 0.42f, 0.36f, 0.96f);
        coopAccessBtn.AddComponent<Button>().onClick.AddListener(ToggleBuildCoopAccessMode);
        bookCoopAccessBtnLabel = CreateBookText(coopAccessBtn.transform, "Lbl",
            "OBRA: GRUPO CO-OP", font, 11,
            Vector2.zero, new Vector2(192f, 28f), Color.white, TextAnchor.MiddleCenter);

        // Botao para ir direto para a aba de Fabricacao [I] (fazer Corda / Machado)
        GameObject openCraftBtn = new GameObject("Btn_GoToCraft");
        openCraftBtn.transform.SetParent(leftPage.transform, false);
        RectTransform ocRT = openCraftBtn.AddComponent<RectTransform>();
        ocRT.anchoredPosition = new Vector2(-106f, -254f);
        ocRT.sizeDelta = new Vector2(202f, 36f);
        Image ocImg = openCraftBtn.AddComponent<Image>();
        ocImg.sprite = FragaProIconLibrary.GetPillSprite();
        ocImg.type = Image.Type.Sliced;
        ocImg.color = new Color(0.18f, 0.44f, 0.26f, 0.96f);
        openCraftBtn.AddComponent<Button>().onClick.AddListener(() =>
        {
            CloseSurvivalBook();
            if (SurvivalInventoryCraftUI.Instance != null)
                SurvivalInventoryCraftUI.Instance.OpenCraftingAtStation(CraftingStationType.Hand);
        });
        CreateBookText(openCraftBtn.transform, "Lbl", "ABRIR FABRICACAO [I]", font, 12,
            Vector2.zero, new Vector2(190f, 30f), Color.white, TextAnchor.MiddleCenter);

        // Botao Fechar Livro
        GameObject closeBookBtn = new GameObject("Btn_CloseBook");
        closeBookBtn.transform.SetParent(leftPage.transform, false);
        RectTransform cbRT = closeBookBtn.AddComponent<RectTransform>();
        cbRT.anchoredPosition = new Vector2(106f, -254f);
        cbRT.sizeDelta = new Vector2(202f, 36f);
        Image cbImg = closeBookBtn.AddComponent<Image>();
        cbImg.sprite = FragaProIconLibrary.GetPillSprite();
        cbImg.type = Image.Type.Sliced;
        cbImg.color = new Color(0.56f, 0.18f, 0.16f, 0.96f);
        closeBookBtn.AddComponent<Button>().onClick.AddListener(CloseSurvivalBook);
        CreateBookText(closeBookBtn.transform, "Lbl", "FECHAR LIVRO [B / ESC]", font, 12,
            Vector2.zero, new Vector2(190f, 30f), Color.white, TextAnchor.MiddleCenter);

        // Cabecalho da Pagina Direita
        rightPageTitleText = CreateBookText(rightPage.transform, "RightHeader", "PROJETOS DO CAPITULO", font, 15,
            new Vector2(0f, 262f), new Vector2(442f, 28f), new Color(0.24f, 0.14f, 0.06f), TextAnchor.MiddleCenter);

        // Barra de Filtro e Prontidao da Pagina Direita
        GameObject toolbar = new GameObject("RightFilterToolbar");
        toolbar.transform.SetParent(rightPage.transform, false);
        RectTransform tbRT = toolbar.AddComponent<RectTransform>();
        tbRT.anchoredPosition = new Vector2(0f, 226f);
        tbRT.sizeDelta = new Vector2(446f, 30f);

        GameObject sumPill = new GameObject("ReadySummaryPill");
        sumPill.transform.SetParent(toolbar.transform, false);
        RectTransform sp2RT = sumPill.AddComponent<RectTransform>();
        sp2RT.anchorMin = new Vector2(0f, 0.5f);
        sp2RT.anchorMax = new Vector2(0f, 0.5f);
        sp2RT.pivot = new Vector2(0f, 0.5f);
        sp2RT.anchoredPosition = Vector2.zero;
        sp2RT.sizeDelta = new Vector2(276f, 26f);
        Image sumBg = sumPill.AddComponent<Image>();
        sumBg.sprite = FragaProIconLibrary.GetPillSprite();
        sumBg.type = Image.Type.Sliced;
        sumBg.color = new Color(0.24f, 0.17f, 0.10f, 0.92f);
        rightReadySummaryText = CreateBookText(sumPill.transform, "SummaryTxt", "PRONTOS P/ OBRA: 0/0", font, 10,
            Vector2.zero, new Vector2(264f, 22f), new Color(0.96f, 0.90f, 0.72f), TextAnchor.MiddleLeft);

        GameObject filterBtn = new GameObject("Btn_FilterReady");
        filterBtn.transform.SetParent(toolbar.transform, false);
        RectTransform fBtnRT = filterBtn.AddComponent<RectTransform>();
        fBtnRT.anchorMin = new Vector2(1f, 0.5f);
        fBtnRT.anchorMax = new Vector2(1f, 0.5f);
        fBtnRT.pivot = new Vector2(1f, 0.5f);
        fBtnRT.anchoredPosition = Vector2.zero;
        fBtnRT.sizeDelta = new Vector2(162f, 26f);
        filterReadyBtnImg = filterBtn.AddComponent<Image>();
        filterReadyBtnImg.sprite = FragaProIconLibrary.GetPillSprite();
        filterReadyBtnImg.type = Image.Type.Sliced;
        filterReadyBtnImg.color = new Color(0.28f, 0.21f, 0.14f, 0.94f);
        filterBtn.AddComponent<Button>().onClick.AddListener(ToggleFilterOnlyReadyBlueprints);
        filterReadyBtnLabel = CreateBookText(filterBtn.transform, "Lbl", "SO PRONTOS: OFF", font, 10,
            Vector2.zero, new Vector2(150f, 22f), Color.white, TextAnchor.MiddleCenter);

        // Viewport com ScrollRect na pagina direita para suportar todos os projetos sem corte
        GameObject scrollViewObj = new GameObject("RightPageScrollView");
        scrollViewObj.transform.SetParent(rightPage.transform, false);
        RectTransform svRT = scrollViewObj.AddComponent<RectTransform>();
        svRT.anchorMin = new Vector2(0.5f, 0.5f);
        svRT.anchorMax = new Vector2(0.5f, 0.5f);
        svRT.anchoredPosition = new Vector2(0f, -24f);
        svRT.sizeDelta = new Vector2(452f, 456f);

        Image svImg = scrollViewObj.AddComponent<Image>();
        svImg.color = new Color(0f, 0f, 0f, 0.01f);
        svImg.raycastTarget = true;
        scrollViewObj.AddComponent<RectMask2D>();

        rightPageScrollRect = scrollViewObj.AddComponent<ScrollRect>();
        rightPageScrollRect.horizontal = false;
        rightPageScrollRect.vertical = true;
        rightPageScrollRect.movementType = ScrollRect.MovementType.Clamped;
        rightPageScrollRect.scrollSensitivity = 36f;
        rightPageScrollRect.viewport = svRT;

        GameObject rightRoot = new GameObject("RightPageContent");
        rightRoot.transform.SetParent(scrollViewObj.transform, false);
        RectTransform rrRT = rightRoot.AddComponent<RectTransform>();
        rrRT.anchorMin = new Vector2(0f, 1f);
        rrRT.anchorMax = new Vector2(1f, 1f);
        rrRT.pivot = new Vector2(0.5f, 1f);
        rrRT.anchoredPosition = Vector2.zero;
        rrRT.sizeDelta = new Vector2(0f, 456f);
        rightPageScrollRect.content = rrRT;
        rightPageContentRoot = rightRoot.transform;
        rightPageContentRT = rrRT;

        GameObject footerBar = new GameObject("RightPageFooter");
        footerBar.transform.SetParent(rightPage.transform, false);
        RectTransform ftRT = footerBar.AddComponent<RectTransform>();
        ftRT.anchoredPosition = new Vector2(0f, -270f);
        ftRT.sizeDelta = new Vector2(446f, 24f);
        Image ftImg = footerBar.AddComponent<Image>();
        ftImg.sprite = FragaProIconLibrary.GetPillSprite();
        ftImg.type = Image.Type.Sliced;
        ftImg.color = new Color(0.20f, 0.14f, 0.08f, 0.90f);
        rightFooterText = CreateBookText(footerBar.transform, "FooterTxt",
            "FIXE UM PROJETO PARA ACOMPANHAR MATERIAIS NO HUD", font, 10,
            Vector2.zero, new Vector2(432f, 20f), new Color(0.96f, 0.88f, 0.64f), TextAnchor.MiddleCenter);
    }

    private static void CreateBrassCorner(Transform parent, string name, Vector2 anchor, Vector2 offset)
    {
        GameObject corner = new GameObject(name);
        corner.transform.SetParent(parent, false);
        RectTransform rt = corner.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(28f, 28f);
        Image img = corner.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        img.type = Image.Type.Sliced;
        img.color = new Color(0.74f, 0.56f, 0.24f, 0.88f);
        img.raycastTarget = false;
    }

    public void ToggleFilterOnlyReadyBlueprints()
    {
        filterOnlyReadyBlueprints = !filterOnlyReadyBlueprints;
        RefreshBookPage(currentBookChapter);
    }

    public void TogglePinBlueprint(int projectIndex)
    {
        if (availableProjects == null || projectIndex < 0 || projectIndex >= availableProjects.Length) return;

        if (pinnedProjectIndex == projectIndex)
        {
            pinnedProjectIndex = -1;
            if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ClearPinnedBlueprint();
        }
        else
        {
            pinnedProjectIndex = projectIndex;
            StructureProject proj = availableProjects[projectIndex];
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.SetPinnedBlueprint(projectIndex, proj.structureName, proj.requiredLogs, proj.requiredStones);
        }

        if (IsBookOpen) RefreshBookPage(currentBookChapter);
    }

    private Text CreateBookText(Transform parent, string name, string content, Font font, int fontSize,
        Vector2 anchoredPos, Vector2 size, Color textColor, TextAnchor align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Text t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = textColor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = content;
        t.raycastTarget = false;
        return t;
    }

    public void ToggleBuildCoopAccessMode()
    {
        FragaCoopSession coop = FragaCoopSession.GetOrCreate();
        coop.ToggleDefaultBuildAccess();
        RefreshCoopAccessIndicators();
        if (isPlacingGhost && activeGhostObj != null)
        {
            UpdatePlacementHeader(activeGhostObj.transform.eulerAngles.y);
        }
    }

    private void RefreshCoopAccessIndicators()
    {
        bool shared = FragaCoopSession.Instance == null || FragaCoopSession.Instance.DefaultStructuresShared;
        if (bookCoopAccessBtnLabel != null)
            bookCoopAccessBtnLabel.text = shared ? "OBRA: GRUPO CO-OP" : "OBRA: PRIVADA (SO EU)";
        if (bookCoopAccessBtnImg != null)
            bookCoopAccessBtnImg.color = shared
                ? new Color(0.14f, 0.44f, 0.36f, 0.96f)
                : new Color(0.46f, 0.28f, 0.14f, 0.96f);

        if (placementCoopBtnLabel != null)
            placementCoopBtnLabel.text = shared ? "ACESSO: GRUPO" : "ACESSO: PRIVADO";
        if (placementCoopBtnImg != null)
            placementCoopBtnImg.color = shared
                ? new Color(0.16f, 0.44f, 0.38f, 0.94f)
                : new Color(0.48f, 0.30f, 0.14f, 0.94f);
    }

    private void RefreshBookPage(int chapterIndex)
    {
        currentBookChapter = Mathf.Clamp(chapterIndex, 0, 4);

        InventoryAdvanced inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        int wood = inv != null ? inv.GetItemCount(100) : 0;
        int stone = inv != null ? inv.GetItemCount(101) : 0;
        int herbs = inv != null ? inv.GetItemCount(103) : 0;
        int rope = inv != null ? inv.GetItemCount(118) : 0;
        int carriedLogs = LogCarryingSystem.Instance != null ? LogCarryingSystem.Instance.currentLogs : 0;

        if (leftPageInventorySummaryText != null)
        {
            leftPageInventorySummaryText.text =
                $"MOCHILA: Madeira {wood} (+{carriedLogs} Toras) | Pedra {stone} | Ervas {herbs} | Corda {rope}";
        }
        if (bookBaseStatsText != null)
        {
            bookBaseStatsText.text =
                $"Base: {StructureHealth.AllStructures.Count} prontas | {WorldBlueprintSite.AllSites.Count} obras";
        }
        RefreshCoopAccessIndicators();

        if (chapterTabImages != null)
        {
            for (int i = 0; i < chapterTabImages.Length; i++)
            {
                if (chapterTabImages[i] == null) continue;
                chapterTabImages[i].color = (i == currentBookChapter)
                    ? new Color(0.52f, 0.32f, 0.14f, 0.98f)
                    : new Color(0.24f, 0.16f, 0.10f, 0.92f);
            }
        }

        if (bookmarkTabImages != null)
        {
            for (int i = 0; i < bookmarkTabImages.Length; i++)
            {
                if (bookmarkTabImages[i] == null) continue;
                bookmarkTabImages[i].color = (i == currentBookChapter)
                    ? new Color(0.68f, 0.42f, 0.16f, 0.99f)
                    : new Color(0.30f, 0.19f, 0.11f, 0.94f);
            }
        }

        if (filterReadyBtnLabel != null)
            filterReadyBtnLabel.text = filterOnlyReadyBlueprints ? "SO PRONTOS: ON" : "SO PRONTOS: OFF";
        if (filterReadyBtnImg != null)
            filterReadyBtnImg.color = filterOnlyReadyBlueprints
                ? new Color(0.16f, 0.48f, 0.30f, 0.98f)
                : new Color(0.28f, 0.21f, 0.14f, 0.94f);

        if (rightPageContentRoot == null) return;
        for (int i = rightPageContentRoot.childCount - 1; i >= 0; i--)
        {
            SafeDestroy(rightPageContentRoot.GetChild(i).gameObject);
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (currentBookChapter == 0)
        {
            if (rightPageTitleText != null)
                rightPageTitleText.text = "CAPITULO 1: FLUXOGRAMA DE SOBREVIVENCIA";
            if (rightReadySummaryText != null)
                rightReadySummaryText.text = "MANUAL PROGRESSIVO DA ILHA";
            if (rightFooterText != null)
                rightFooterText.text = "SIGA OS 5 PASSOS OU ESCOLHA UM CAPITULO AO LADO";

            BuildSurvivalGuideRightPage(font, wood, stone, herbs, rope);
            return;
        }

        int[] projIndices;
        switch (currentBookChapter)
        {
            case 1:
                if (rightPageTitleText != null) rightPageTitleText.text = "CAPITULO 2: ABRIGOS & CABANAS";
                projIndices = new int[] { 0, 1 };
                break;
            case 2:
                if (rightPageTitleText != null) rightPageTitleText.text = "CAPITULO 3: AGUA, COMIDA & FOGO";
                projIndices = new int[] { 3, 4 };
                break;
            case 3:
                if (rightPageTitleText != null) rightPageTitleText.text = "CAPITULO 4: DEFESA & ESTOCAGEM";
                projIndices = new int[] { 2, 10, 11 };
                break;
            default:
                if (rightPageTitleText != null) rightPageTitleText.text = "CAPITULO 5: ESTRUTURAS MODULARES";
                projIndices = new int[] { 5, 6, 7, 8, 9 };
                break;
        }

        int count = projIndices.Length;
        int readyCount = 0;
        int totalWood = wood + carriedLogs;
        List<int> visibleIndices = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            int idx = projIndices[i];
            if (idx < 0 || idx >= availableProjects.Length) continue;
            StructureProject proj = availableProjects[idx];
            bool ready = totalWood >= proj.requiredLogs && stone >= proj.requiredStones;
            if (ready) readyCount++;
            if (!filterOnlyReadyBlueprints || ready) visibleIndices.Add(idx);
        }

        if (rightReadySummaryText != null)
            rightReadySummaryText.text = $"PRONTOS P/ OBRA: {readyCount}/{count}  |  MAD {totalWood}  PED {stone}";
        if (rightFooterText != null)
        {
            rightFooterText.text = pinnedProjectIndex >= 0 && pinnedProjectIndex < availableProjects.Length
                ? $"FIXADO NO HUD: {availableProjects[pinnedProjectIndex].structureName.ToUpper()}"
                : "FIXE UM PROJETO PARA ACOMPANHAR MATERIAIS NO HUD";
        }

        if (visibleIndices.Count == 0)
        {
            GameObject emptyCard = new GameObject("EmptyFilterCard");
            emptyCard.transform.SetParent(rightPageContentRoot, false);
            RectTransform ecRT = emptyCard.AddComponent<RectTransform>();
            ecRT.anchorMin = new Vector2(0.5f, 1f);
            ecRT.anchorMax = new Vector2(0.5f, 1f);
            ecRT.pivot = new Vector2(0.5f, 0.5f);
            ecRT.anchoredPosition = new Vector2(0f, -96f);
            ecRT.sizeDelta = new Vector2(430f, 132f);
            Image ecImg = emptyCard.AddComponent<Image>();
            ecImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
            ecImg.type = Image.Type.Sliced;
            ecImg.color = new Color(0.78f, 0.71f, 0.57f, 0.96f);

            CreateBookText(emptyCard.transform, "EmptyTxt",
                "NENHUM PROJETO PRONTO NESTE CAPITULO.\nCOLETE MAIS MADEIRA OU PEDRA OU DESATIVE O FILTRO.",
                font, 12, new Vector2(0f, 20f), new Vector2(396f, 54f), new Color(0.24f, 0.14f, 0.06f), TextAnchor.MiddleCenter);

            GameObject resetBtn = new GameObject("Btn_ShowAllProjects");
            resetBtn.transform.SetParent(emptyCard.transform, false);
            RectTransform rbRT = resetBtn.AddComponent<RectTransform>();
            rbRT.anchoredPosition = new Vector2(0f, -34f);
            rbRT.sizeDelta = new Vector2(210f, 32f);
            Image rbImg = resetBtn.AddComponent<Image>();
            rbImg.sprite = FragaProIconLibrary.GetPillSprite();
            rbImg.type = Image.Type.Sliced;
            rbImg.color = new Color(0.24f, 0.38f, 0.48f, 0.96f);
            resetBtn.AddComponent<Button>().onClick.AddListener(ToggleFilterOnlyReadyBlueprints);
            CreateBookText(resetBtn.transform, "Lbl", "MOSTRAR TODOS OS PROJETOS", font, 11,
                Vector2.zero, new Vector2(196f, 26f), Color.white, TextAnchor.MiddleCenter);

            if (rightPageContentRT != null) rightPageContentRT.sizeDelta = new Vector2(0f, 456f);
            if (rightPageScrollRect != null) rightPageScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        float cardH = visibleIndices.Count <= 3 ? 138f : 114f;
        float gap = 10f;
        float startY = -8f - cardH * 0.5f;

        for (int i = 0; i < visibleIndices.Count; i++)
        {
            int projIdx = visibleIndices[i];
            StructureProject proj = availableProjects[projIdx];
            float y = startY - i * (cardH + gap);
            CreateBlueprintBookCard(rightPageContentRoot, projIdx, proj, y, cardH, font, totalWood, stone);
        }

        if (rightPageContentRT != null)
        {
            float contentH = Mathf.Max(456f, visibleIndices.Count * (cardH + gap) + 16f);
            rightPageContentRT.sizeDelta = new Vector2(0f, contentH);
        }
        if (rightPageScrollRect != null) rightPageScrollRect.verticalNormalizedPosition = 1f;
    }

    private void BuildSurvivalGuideRightPage(Font font, int wood, int stone, int herbs, int rope)
    {
        InventoryAdvanced inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        bool hasAxe = inv != null && (inv.GetItemCount(302) > 0 || inv.GetItemCount(300) > 0);
        int carriedLogs = LogCarryingSystem.Instance != null ? LogCarryingSystem.Instance.currentLogs : 0;
        bool[] stepDone = new bool[]
        {
            wood > 0 || stone > 0 || herbs > 0,
            rope > 0 || hasAxe,
            hasAxe,
            wood + carriedLogs >= 3,
            StructureHealth.AllStructures.Count > 0 || WorldBlueprintSite.AllSites.Count > 0
        };

        string[] steps = new string[]
        {
            "PASSO 1 - COLETA MANUAL NO CHAO [TECLA E]\n" +
            "Sem ferramentas voce NAO consegue derrubar arvores nem quebrar rochas.\n" +
            "Olhe para o chao e aperte [E] em Pedras Pequenas, Galhos Soltos e Arbustos de Ervas.",

            "PASSO 2 - COMO FAZER CORDA DE FIBRA [TECLA I]\n" +
            $"Voce tem {herbs} Erva(s) e {rope} Corda(s).\n" +
            "Aperte [I] para abrir a FABRICACAO -> clique em 'Corda de Fibra (x2)' (custa 2 Ervas).",

            "PASSO 3 - COMO FAZER O MACHADO DE PEDRA [TECLA I]\n" +
            $"Requer: 3 Madeiras (tem {wood}), 2 Pedras (tem {stone}) e 1 Corda (tem {rope}).\n" +
            "Na aba FABRICACAO [I], clique em 'Machado de Pedra' - ele equipa automaticamente na mao!",

            "PASSO 4 - CORTAR ARVORES E ROCHAS [CLIQUE DO MOUSE]\n" +
            "Com o Machado ou Picareta na mao, chegue perto de uma Arvore, Tronco Caido ou Rocha\n" +
            "e CLIQUE NO MOUSE (Botao Esquerdo ou Direito) para golpear e derrubar toras e minerios!",

            "PASSO 5 - CONSTRUIR CABANA, ABRIGO, SECADOR E COLETOR [TECLA B]\n" +
            "Escolha um projeto nos Capitulos 2 a 5, clique em POSICIONAR PROJETO,\n" +
            "fixe o molde azul no chao e aperte [E] (ou clique em COLOCAR MATERIAIS) para concluir!"
        };

        for (int i = 0; i < steps.Length; i++)
        {
            GameObject stepCard = new GameObject($"GuideStep_{i}");
            stepCard.transform.SetParent(rightPageContentRoot, false);
            RectTransform rt = stepCard.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -74f - i * 102f);
            rt.sizeDelta = new Vector2(436f, 84f);

            bool done = stepDone[i];
            Image bg = stepCard.AddComponent<Image>();
            bg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
            bg.type = Image.Type.Sliced;
            bg.color = done
                ? new Color(0.74f, 0.78f, 0.60f, 0.96f)
                : new Color(0.79f, 0.72f, 0.58f, 0.96f);

            GameObject badge = new GameObject("StepBadge");
            badge.transform.SetParent(stepCard.transform, false);
            RectTransform bRT = badge.AddComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0f, 0.5f);
            bRT.anchorMax = new Vector2(0f, 0.5f);
            bRT.pivot = new Vector2(0f, 0.5f);
            bRT.anchoredPosition = new Vector2(8f, 0f);
            bRT.sizeDelta = new Vector2(36f, 58f);
            Image bImg = badge.AddComponent<Image>();
            bImg.sprite = FragaProIconLibrary.GetPillSprite();
            bImg.type = Image.Type.Sliced;
            bImg.color = done
                ? new Color(0.14f, 0.48f, 0.28f, 0.96f)
                : new Color(0.28f, 0.19f, 0.11f, 0.92f);
            CreateBookText(badge.transform, "Num", done ? "OK" : $"0{i + 1}", font, 12,
                Vector2.zero, new Vector2(32f, 40f), Color.white, TextAnchor.MiddleCenter);

            bool hasInlineBtn = (i == 1 || i == 2 || i == 4);
            CreateBookText(stepCard.transform, "StepTxt", steps[i], font, 10,
                new Vector2(hasInlineBtn ? -22f : 18f, 0f),
                new Vector2(hasInlineBtn ? 286f : 374f, 74f),
                new Color(0.16f, 0.10f, 0.04f), TextAnchor.MiddleLeft);

            if (hasInlineBtn)
            {
                GameObject qBtn = new GameObject("Btn_StepAction");
                qBtn.transform.SetParent(stepCard.transform, false);
                RectTransform qbRT = qBtn.AddComponent<RectTransform>();
                qbRT.anchorMin = new Vector2(1f, 0.5f);
                qbRT.anchorMax = new Vector2(1f, 0.5f);
                qbRT.pivot = new Vector2(1f, 0.5f);
                qbRT.anchoredPosition = new Vector2(-8f, 0f);
                qbRT.sizeDelta = new Vector2(86f, 34f);
                Image qbImg = qBtn.AddComponent<Image>();
                qbImg.sprite = FragaProIconLibrary.GetPillSprite();
                qbImg.type = Image.Type.Sliced;
                qbImg.color = (i == 4)
                    ? new Color(0.20f, 0.42f, 0.54f, 0.96f)
                    : new Color(0.18f, 0.46f, 0.28f, 0.96f);
                int stepIdx = i;
                qBtn.AddComponent<Button>().onClick.AddListener(() =>
                {
                    if (stepIdx == 4)
                    {
                        RefreshBookPage(1);
                    }
                    else
                    {
                        CloseSurvivalBook();
                        if (SurvivalInventoryCraftUI.Instance != null)
                            SurvivalInventoryCraftUI.Instance.OpenCraftingAtStation(CraftingStationType.Hand);
                    }
                });
                CreateBookText(qBtn.transform, "Lbl", i == 4 ? "ABRIGOS ->" : "FABRICAR [I]", font, 10,
                    Vector2.zero, new Vector2(78f, 28f), Color.white, TextAnchor.MiddleCenter);
            }

            if (i < steps.Length - 1)
            {
                GameObject arrowObj = new GameObject($"FlowConnector_{i}");
                arrowObj.transform.SetParent(rightPageContentRoot, false);
                RectTransform arRT = arrowObj.AddComponent<RectTransform>();
                arRT.anchorMin = new Vector2(0.5f, 1f); arRT.anchorMax = new Vector2(0.5f, 1f);
                arRT.pivot = new Vector2(0.5f, 0.5f);
                arRT.anchoredPosition = new Vector2(-190f, -125f - i * 102f);
                arRT.sizeDelta = new Vector2(34f, 18f);
                CreateBookText(arrowObj.transform, "Arrow", "v", font, 12,
                    Vector2.zero, new Vector2(30f, 16f),
                    done ? new Color(0.14f, 0.48f, 0.28f) : new Color(0.32f, 0.22f, 0.12f),
                    TextAnchor.MiddleCenter);
            }
        }
        int doneCount = 0;
        for (int i = 0; i < stepDone.Length; i++) if (stepDone[i]) doneCount++;
        GameObject progPill = new GameObject("GuideFlowProgressBar");
        progPill.transform.SetParent(rightPageContentRoot, false);
        RectTransform ppRT = progPill.AddComponent<RectTransform>();
        ppRT.anchorMin = new Vector2(0.5f, 1f); ppRT.anchorMax = new Vector2(0.5f, 1f);
        ppRT.pivot = new Vector2(0.5f, 0.5f);
        ppRT.anchoredPosition = new Vector2(0f, -16f);
        ppRT.sizeDelta = new Vector2(436f, 24f);
        Image ppImg = progPill.AddComponent<Image>();
        ppImg.sprite = FragaProIconLibrary.GetPillSprite();
        ppImg.type = Image.Type.Sliced;
        ppImg.color = new Color(0.22f, 0.16f, 0.09f, 0.92f);
        CreateBookText(progPill.transform, "FlowHdr",
            $"FLUXOGRAMA DE SOBREVIVENCIA  |  ETAPAS PRONTAS: {doneCount}/5",
            font, 10, Vector2.zero, new Vector2(420f, 20f), new Color(0.96f, 0.88f, 0.66f), TextAnchor.MiddleCenter);
        if (rightPageContentRT != null)
        {
            rightPageContentRT.sizeDelta = new Vector2(0f, Mathf.Max(486f, steps.Length * 102f + 36f));
        }
        if (rightPageScrollRect != null) rightPageScrollRect.verticalNormalizedPosition = 1f;
    }

    private void CreateBlueprintBookCard(Transform parent, int projIdx, StructureProject proj, float yPos, float cardH, Font font, int totalWood, int totalStone)
    {
        GameObject card = new GameObject($"BookProj_{projIdx}");
        card.transform.SetParent(parent, false);
        RectTransform cRT = card.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.5f, 1f);
        cRT.anchorMax = new Vector2(0.5f, 1f);
        cRT.pivot = new Vector2(0.5f, 0.5f);
        cRT.anchoredPosition = new Vector2(0f, yPos);
        cRT.sizeDelta = new Vector2(436f, cardH);

        bool hasWood = totalWood >= proj.requiredLogs;
        bool hasStone = totalStone >= proj.requiredStones;
        bool canBuildNow = hasWood && hasStone;
        bool isPinned = pinnedProjectIndex == projIdx;

        Image cImg = card.AddComponent<Image>();
        cImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        cImg.type = Image.Type.Sliced;
        cImg.color = canBuildNow
            ? new Color(0.76f, 0.76f, 0.58f, 0.98f)
            : new Color(0.78f, 0.71f, 0.57f, 0.98f);

        Outline cOut = card.AddComponent<Outline>();
        cOut.effectColor = isPinned
            ? new Color(0.86f, 0.62f, 0.14f, 0.92f)
            : (canBuildNow
                ? new Color(0.16f, 0.54f, 0.28f, 0.78f)
                : new Color(0.45f, 0.30f, 0.16f, 0.45f));
        cOut.effectDistance = isPinned ? new Vector2(1.8f, -1.8f) : new Vector2(1.2f, -1.2f);

        // Icone Ilustrado da Estrutura
        GameObject iconBg = new GameObject("IconFrame");
        iconBg.transform.SetParent(card.transform, false);
        RectTransform ibRT = iconBg.AddComponent<RectTransform>();
        ibRT.anchorMin = new Vector2(0f, 0.5f);
        ibRT.anchorMax = new Vector2(0f, 0.5f);
        ibRT.pivot = new Vector2(0f, 0.5f);
        ibRT.anchoredPosition = new Vector2(10f, 0f);
        float iconSz = Mathf.Min(64f, cardH - 18f);
        ibRT.sizeDelta = new Vector2(iconSz, iconSz);
        Image ibImg = iconBg.AddComponent<Image>();
        ibImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
        ibImg.type = Image.Type.Sliced;
        ibImg.color = new Color(0.22f, 0.15f, 0.09f, 0.95f);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(iconBg.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = Vector2.zero;
        iRT.anchorMax = Vector2.one;
        iRT.offsetMin = new Vector2(6f, 6f);
        iRT.offsetMax = new Vector2(-6f, -6f);
        Image icoImg = icoObj.AddComponent<Image>();
        icoImg.sprite = GetProjectIconSprite(proj.structureName);
        icoImg.preserveAspect = true;

        // Nome da Estrutura + Tier/HP + Descricao Funcional + Custo
        string tierLabel = GetProjectTierLabel(proj.structureName);
        string desc = GetProjectDescription(proj.structureName);
        string woodTag = $"<color={(hasWood ? "#125E2C" : "#8F1D1D")}>Madeira {totalWood}/{proj.requiredLogs}</color>";
        string stoneTag = proj.requiredStones > 0
            ? $"  |  <color={(hasStone ? "#125E2C" : "#8F1D1D")}>Pedra {totalStone}/{proj.requiredStones}</color>"
            : string.Empty;
        string readyBadge = canBuildNow ? " <color=#125E2C>[PRONTO]</color>" : " <color=#7A4A10>[COLETAR]</color>";

        GameObject infoObj = new GameObject("InfoText");
        infoObj.transform.SetParent(card.transform, false);
        RectTransform infoRT = infoObj.AddComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0f, 0f);
        infoRT.anchorMax = new Vector2(1f, 1f);
        infoRT.offsetMin = new Vector2(iconSz + 18f, 16f);
        infoRT.offsetMax = new Vector2(-128f, -6f);

        Text infoTxt = infoObj.AddComponent<Text>();
        infoTxt.font = font;
        infoTxt.fontSize = cardH >= 120f ? 11 : 10;
        infoTxt.fontStyle = FontStyle.Bold;
        infoTxt.alignment = TextAnchor.MiddleLeft;
        infoTxt.color = new Color(0.16f, 0.10f, 0.04f);
        infoTxt.supportRichText = true;
        infoTxt.text = $"<size={(cardH >= 120f ? 13 : 12)}><color=#4A2508>{proj.structureName.ToUpper()}</color></size>{readyBadge}\n<size=9><color=#5E4022>{tierLabel}</color></size> - {desc}\n{woodTag}{stoneTag}";
        infoTxt.raycastTarget = false;

        // Barra de prontidao de materiais do card
        float wRatio = proj.requiredLogs > 0 ? Mathf.Clamp01((float)totalWood / proj.requiredLogs) : 1f;
        float sRatio = proj.requiredStones > 0 ? Mathf.Clamp01((float)totalStone / proj.requiredStones) : 1f;
        float matProgress = proj.requiredStones > 0 ? (wRatio * 0.5f + sRatio * 0.5f) : wRatio;
        GameObject matBarBg = new GameObject("MatProgressBG");
        matBarBg.transform.SetParent(card.transform, false);
        RectTransform mbRT = matBarBg.AddComponent<RectTransform>();
        mbRT.anchorMin = new Vector2(0f, 0f);
        mbRT.anchorMax = new Vector2(1f, 0f);
        mbRT.pivot = new Vector2(0.5f, 0f);
        mbRT.offsetMin = new Vector2(iconSz + 18f, 6f);
        mbRT.offsetMax = new Vector2(-132f, 12f);
        Image mbImg = matBarBg.AddComponent<Image>();
        mbImg.color = new Color(0.18f, 0.12f, 0.06f, 0.48f);
        mbImg.raycastTarget = false;

        GameObject matBarFill = new GameObject("MatProgressFill");
        matBarFill.transform.SetParent(matBarBg.transform, false);
        RectTransform mfRT = matBarFill.AddComponent<RectTransform>();
        mfRT.anchorMin = Vector2.zero;
        mfRT.anchorMax = Vector2.one;
        mfRT.offsetMin = Vector2.zero;
        mfRT.offsetMax = Vector2.zero;
        Image mfImg = matBarFill.AddComponent<Image>();
        mfImg.sprite = FragaProIconLibrary.GetBarFillSprite();
        mfImg.type = Image.Type.Filled;
        mfImg.fillMethod = Image.FillMethod.Horizontal;
        mfImg.fillAmount = matProgress;
        mfImg.color = canBuildNow ? new Color(0.16f, 0.64f, 0.34f, 0.95f) : new Color(0.84f, 0.56f, 0.16f, 0.95f);
        mfImg.raycastTarget = false;

        // Botao Posicionar Projeto
        GameObject placeBtn = new GameObject("Btn_PlaceBlueprint");
        placeBtn.transform.SetParent(card.transform, false);
        RectTransform pbRT = placeBtn.AddComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(1f, 0.5f);
        pbRT.anchorMax = new Vector2(1f, 0.5f);
        pbRT.pivot = new Vector2(1f, 0.5f);
        pbRT.anchoredPosition = new Vector2(-10f, 16f);
        pbRT.sizeDelta = new Vector2(112f, Mathf.Min(42f, cardH * 0.38f));

        Image pbImg = placeBtn.AddComponent<Image>();
        pbImg.sprite = FragaProIconLibrary.GetPillSprite();
        pbImg.type = Image.Type.Sliced;
        pbImg.color = canBuildNow
            ? new Color(0.14f, 0.52f, 0.30f, 0.98f)
            : new Color(0.24f, 0.34f, 0.44f, 0.95f);

        placeBtn.AddComponent<Button>().onClick.AddListener(() =>
        {
            CloseSurvivalBook();
            StartPlacement(projIdx);
        });

        CreateBookText(placeBtn.transform, "BtnLbl", "POSICIONAR\nNO MUNDO", font, 10,
            Vector2.zero, new Vector2(104f, 34f), Color.white, TextAnchor.MiddleCenter);

        // Botao Fixar no HUD (Pinned Blueprint Tracker)
        GameObject pinBtn = new GameObject("Btn_PinBlueprint");
        pinBtn.transform.SetParent(card.transform, false);
        RectTransform pinRT = pinBtn.AddComponent<RectTransform>();
        pinRT.anchorMin = new Vector2(1f, 0.5f);
        pinRT.anchorMax = new Vector2(1f, 0.5f);
        pinRT.pivot = new Vector2(1f, 0.5f);
        pinRT.anchoredPosition = new Vector2(-10f, -26f);
        pinRT.sizeDelta = new Vector2(112f, 28f);
        Image pinImg = pinBtn.AddComponent<Image>();
        pinImg.sprite = FragaProIconLibrary.GetPillSprite();
        pinImg.type = Image.Type.Sliced;
        pinImg.color = isPinned
            ? new Color(0.72f, 0.48f, 0.12f, 0.98f)
            : new Color(0.28f, 0.21f, 0.14f, 0.94f);
        pinBtn.AddComponent<Button>().onClick.AddListener(() => TogglePinBlueprint(projIdx));

        GameObject pinIco = new GameObject("PinIcon");
        pinIco.transform.SetParent(pinBtn.transform, false);
        RectTransform piRT = pinIco.AddComponent<RectTransform>();
        piRT.anchorMin = new Vector2(0f, 0.5f);
        piRT.anchorMax = new Vector2(0f, 0.5f);
        piRT.pivot = new Vector2(0f, 0.5f);
        piRT.anchoredPosition = new Vector2(7f, 0f);
        piRT.sizeDelta = new Vector2(15f, 15f);
        Image piImg = pinIco.AddComponent<Image>();
        piImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.BookmarkPin);
        piImg.preserveAspect = true;
        piImg.raycastTarget = false;

        CreateBookText(pinBtn.transform, "PinLbl", isPinned ? "FIXADO HUD" : "FIXAR HUD", font, 9,
            new Vector2(9f, 0f), new Vector2(86f, 22f), Color.white, TextAnchor.MiddleCenter);
    }

    private static string GetProjectTierLabel(string structureName)
    {
        if (structureName.Contains("Cabana")) return "RESIDENCIA | 550 HP";
        if (structureName.Contains("Refugio")) return "ABRIGO | 280 HP";
        if (structureName.Contains("Palicada")) return "DEFESA | 450 HP";
        if (structureName.Contains("Secador") || structureName.Contains("Coletor")) return "UTILIDADE | 240 HP";
        if (structureName.Contains("Bau") || structureName.Contains("Cesto")) return "ESTOQUE | 320 HP";
        return "ESTRUTURAL | 340 HP";
    }

    private static string GetProjectDescription(string structureName)
    {
        if (structureName.Contains("Refugio"))
            return "Permite DORMIR / DESCANSAR [E], recuperar Vida/Sanidade e SALVAR o jogo.";
        if (structureName.Contains("Cabana"))
            return "Cabana de toras com cama. Use [E] para DORMIR / DESCANSAR (+55% Vida/Sanidade) e SALVAR.";
        if (structureName.Contains("Palicada"))
            return "Muro defensivo com espinhos frontais que ferem e bloqueiam Vultos a noite.";
        if (structureName.Contains("Secador"))
            return "Pendure Carne Crua ou Peixe com [E] para produzir Charque que NAO apodrece.";
        if (structureName.Contains("Coletor"))
            return "Acumula Agua Limpa da chuva/orvalho e reabastece seu Cantil de Couro [E].";
        if (structureName.Contains("Cesto"))
            return "Armazena ate 12 Toras de Madeira empilhadas para organizar sua base [E].";
        if (structureName.Contains("Bau"))
            return "Bau de Base com 16 pilhas para guardar recursos, ferramentas e mantimentos [E].";
        if (structureName.Contains("Piso"))
            return "Fundacao nivelada de madeira com encaixe automatico (Snap) para paredes.";
        if (structureName.Contains("Porta"))
            return "Parede de toras com porta articulada que abre e fecha com [E].";
        if (structureName.Contains("Parede"))
            return "Parede solida de toras que encaixa nas bordas dos pisos de madeira.";
        if (structureName.Contains("Teto"))
            return "Cobertura de madeira que encaixa no topo das paredes contra chuva e frio.";
        if (structureName.Contains("Escada"))
            return "Rampa/escada de toras para subir em pisos elevados ou segundo andar.";
        return "Estrutura de sobrevivencia construivel no mundo.";
    }

    private static Sprite GetProjectIconSprite(string structureName)
    {
        if (structureName.Contains("Refugio") || structureName.Contains("Cabana"))
            return FragaProIconLibrary.GetItemIcon(401);
        if (structureName.Contains("Coletor"))
            return FragaProIconLibrary.GetItemIcon(109);
        if (structureName.Contains("Secador"))
            return FragaProIconLibrary.GetItemIcon(212);
        if (structureName.Contains("Palicada"))
            return FragaProIconLibrary.GetItemIcon(302);
        if (structureName.Contains("Bau") || structureName.Contains("Cesto"))
            return FragaProIconLibrary.GetItemIcon(100);
        return FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.BuildHammer);
    }

    private void ShowPlacementBar(bool show)
    {
        if (show && placementBarObj == null) BuildPlacementBarUI();
        if (placementBarObj != null)
        {
            placementBarObj.SetActive(show);
            if (show) placementBarObj.transform.SetAsLastSibling();
        }
    }

    private void BuildPlacementBarUI()
    {
        Canvas canvas = HUDSurvivalOverlay.GetGameplayCanvas();
        if (canvas == null) return;

        placementBarObj = new GameObject("BlueprintPlacementBar");
        placementBarObj.transform.SetParent(canvas.transform, false);
        RectTransform rt = placementBarObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 118f);
        rt.sizeDelta = new Vector2(796f, 56f);

        Image bg = placementBarObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);

        // Cabecalho compacto logo acima da barra indicando o projeto atual e os atalhos
        GameObject hdrObj = new GameObject("PlacementHeader");
        hdrObj.transform.SetParent(placementBarObj.transform, false);
        RectTransform hRT = hdrObj.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0.5f, 1f);
        hRT.anchorMax = new Vector2(0.5f, 1f);
        hRT.pivot = new Vector2(0.5f, 0f);
        hRT.anchoredPosition = new Vector2(0f, 6f);
        hRT.sizeDelta = new Vector2(780f, 28f);

        Image hBg = hdrObj.AddComponent<Image>();
        hBg.sprite = FragaProIconLibrary.GetPillSprite();
        hBg.type = Image.Type.Sliced;
        hBg.color = new Color(0.04f, 0.09f, 0.14f, 0.90f);
        hBg.raycastTarget = false;

        GameObject hTxtObj = new GameObject("HeaderText");
        hTxtObj.transform.SetParent(hdrObj.transform, false);
        RectTransform htRT = hTxtObj.AddComponent<RectTransform>();
        htRT.anchorMin = Vector2.zero;
        htRT.anchorMax = Vector2.one;
        htRT.offsetMin = new Vector2(10f, 0f);
        htRT.offsetMax = new Vector2(-10f, 0f);
        placementHeaderText = hTxtObj.AddComponent<Text>();
        placementHeaderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placementHeaderText.fontSize = 12;
        placementHeaderText.fontStyle = FontStyle.Bold;
        placementHeaderText.alignment = TextAnchor.MiddleCenter;
        placementHeaderText.color = new Color(0.58f, 0.95f, 1.0f);
        placementHeaderText.supportRichText = true;
        placementHeaderText.raycastTarget = false;

        CreatePlacementBtn(placementBarObj.transform, "Btn_RotL", "GIRAR -", FragaProIconLibrary.ControlIcon.RotateLeft,
            new Vector2(-324f, 0f), 110f, new Color(0.16f, 0.28f, 0.40f, 0.94f), () => RotateGhost(-22.5f));
        CreatePlacementBtn(placementBarObj.transform, "Btn_RotR", "GIRAR +", FragaProIconLibrary.ControlIcon.RotateRight,
            new Vector2(-208f, 0f), 110f, new Color(0.16f, 0.28f, 0.40f, 0.94f), () => RotateGhost(22.5f));
        snapBtnLabel = CreatePlacementBtn(placementBarObj.transform, "Btn_Snap", snapEnabled ? "SNAP: ON" : "SNAP: OFF", FragaProIconLibrary.ControlIcon.BuildHammer,
            new Vector2(-90f, 0f), 114f, new Color(0.20f, 0.36f, 0.46f, 0.94f), ToggleSnap);
        placementCoopBtnLabel = CreatePlacementBtn(placementBarObj.transform, "Btn_CoopAccess", "ACESSO: GRUPO", FragaProIconLibrary.ControlIcon.ShieldBlock,
            new Vector2(36f, 0f), 126f, new Color(0.16f, 0.44f, 0.38f, 0.94f), ToggleBuildCoopAccessMode);
        if (placementCoopBtnLabel != null) placementCoopBtnImg = placementCoopBtnLabel.transform.parent.GetComponent<Image>();
        CreatePlacementBtn(placementBarObj.transform, "Btn_Confirm", "CONFIRMAR", FragaProIconLibrary.ControlIcon.ConfirmCheck,
            new Vector2(174f, 0f), 134f, new Color(0.16f, 0.56f, 0.36f, 0.96f), ConfirmPlacement);
        CreatePlacementBtn(placementBarObj.transform, "Btn_Cancel", "CANCELAR", FragaProIconLibrary.ControlIcon.CancelCross,
            new Vector2(310f, 0f), 118f, new Color(0.58f, 0.18f, 0.18f, 0.94f), CancelPlacement);
        RefreshCoopAccessIndicators();
    }

    private Text CreatePlacementBtn(Transform parent, string name, string label, FragaProIconLibrary.ControlIcon icon,
        Vector2 pos, float width, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 40f);

        Image img = obj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetPillSprite();
        img.type = Image.Type.Sliced;
        img.color = bgColor;
        obj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(obj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(10f, 0f);
        iRT.sizeDelta = new Vector2(20f, 20f);
        Image icoImg = icoObj.AddComponent<Image>();
        icoImg.sprite = FragaProIconLibrary.GetControlIcon(icon);
        icoImg.preserveAspect = true;
        icoImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(obj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(32f, 0f);
        tRT.offsetMax = new Vector2(-6f, 0f);
        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 11;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;
        return txt;
    }

    public void ToggleSnap()
    {
        snapEnabled = !snapEnabled;
        if (snapBtnLabel != null)
            snapBtnLabel.text = snapEnabled ? "SNAP: ON" : "SNAP: OFF";
        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip(snapEnabled ? "Encaixe automatico (Snap) ATIVADO" : "Encaixe automatico (Snap) DESATIVADO");
    }

    public void RotateGhost(float deltaDegrees)
    {
        ghostRotationY += deltaDegrees;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B) && (SurvivalInventoryCraftUI.Instance == null || !SurvivalInventoryCraftUI.Instance.IsOpen))
        {
            ToggleBuildMenuOrPlacement();
        }

        if (IsBookOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseSurvivalBook();
            return;
        }

        if (isPlacingGhost && activeGhostObj != null)
        {
            if (Input.GetKeyDown(KeyCode.G)) ToggleSnap();
            UpdateGhostPosition();

            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.Q)) ghostRotationY -= Time.deltaTime * 90f;
            if (Input.GetKey(KeyCode.E)) ghostRotationY += Time.deltaTime * 90f;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                ghostRotationY += scroll * 15f;
            }

            bool overUI = FragaInputRouter.IsAnyPointerOverUI();
            if (!overUI && Input.touchCount == 0 && Time.unscaledTime >= placementConfirmReadyTime && Input.GetMouseButtonDown(0))
            {
                ConfirmPlacement();
            }
            if (Input.GetKeyDown(KeyCode.Escape) || (Input.touchCount == 0 && Input.GetMouseButtonDown(1)))
            {
                CancelPlacement();
            }
        }
    }

    public void StartPlacement(int projectIndex)
    {
        selectedProjectIndex = Mathf.Clamp(projectIndex, 0, availableProjects.Length - 1);
        StructureProject proj = availableProjects[selectedProjectIndex];
        isPlacingGhost = true;
        ghostRotationY = transform.eulerAngles.y;
        placementConfirmReadyTime = Time.unscaledTime + 0.22f;
        isGhostSnapped = false;
        isGhostPlacementValid = true;
        ghostInvalidReason = string.Empty;
        nextPlacementHeaderUpdateTime = 0f;

        if (activeGhostObj != null) SafeDestroy(activeGhostObj);
        activeGhostObj = CreateGhostVisual(proj.structureName);
        MeshRenderer ghostRend = activeGhostObj != null ? activeGhostObj.GetComponentInChildren<MeshRenderer>() : null;
        activeGhostMat = ghostRend != null ? ghostRend.sharedMaterial : null;
        ShowPlacementBar(true);
        RefreshCoopAccessIndicators();
        UpdatePlacementHeader(ghostRotationY);

        bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
        if (HUDSurvivalOverlay.Instance != null)
        {
            string hint = touchActive
                ? $"Construcao: {proj.structureName} - Mire no chao e toque em CONFIRMAR"
                : $"Construcao: {proj.structureName} [Q/E ou Roda Girar | Clique Esq Fixar | Botao Dir Cancelar]";
            HUDSurvivalOverlay.Instance.ShowLootTooltip(hint);
        }
    }

    private void UpdatePlacementHeader(float yawDeg)
    {
        if (placementHeaderText == null || availableProjects == null || availableProjects.Length == 0) return;
        StructureProject proj = availableProjects[Mathf.Clamp(selectedProjectIndex, 0, availableProjects.Length - 1)];
        int rot = Mathf.RoundToInt(Mathf.Repeat(yawDeg, 360f));
        bool shared = FragaCoopSession.Instance == null || FragaCoopSession.Instance.DefaultStructuresShared;
        string accessTag = shared ? "<color=#7CFFCC>GRUPO</color>" : "<color=#FFD166>PRIVADO</color>";
        string snapTag = isGhostSnapped
            ? "<color=#55FF99>ENCAIXADO</color>"
            : (snapEnabled ? "SNAP: ON" : "SNAP: OFF");
        string stateTag = isGhostPlacementValid
            ? snapTag
            : $"<color=#FF6655>BLOQUEADO: {ghostInvalidReason}</color>";
        string cost = proj.requiredStones > 0
            ? $"{proj.requiredLogs} Mad | {proj.requiredStones} Ped"
            : $"{proj.requiredLogs} Mad";

        placementHeaderText.text = $"{proj.structureName.ToUpper()} ({cost})  |  ROT: {rot:000}°  |  {stateTag}  |  {accessTag}";
    }

    private void UpdateGhostPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        int hitCount = Physics.RaycastNonAlloc(ray, ghostRayHits, 8.5f, ~0, QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        RaycastHit bestHit = default;
        bool hasValidHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = ghostRayHits[i].collider;
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
            if (activeGhostObj != null && (col.transform == activeGhostObj.transform || col.transform.IsChildOf(activeGhostObj.transform))) continue;

            if (ghostRayHits[i].distance < closestDist)
            {
                closestDist = ghostRayHits[i].distance;
                bestHit = ghostRayHits[i];
                hasValidHit = true;
            }
        }

        Vector3 targetPos;
        if (hasValidHit)
        {
            targetPos = bestHit.point;
        }
        else
        {
            targetPos = cam.transform.position + cam.transform.forward * 4.5f;
            targetPos.y = TerrainMeshGenerator.GetTerrainHeight(targetPos.x, targetPos.z);
        }

        // Mantem distancia minima segura para o molde nao aparecer dentro do jogador
        Vector3 delta = targetPos - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 2.1f * 2.1f)
        {
            Vector3 flatFwd = cam.transform.forward;
            flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = transform.forward;
            flatFwd.Normalize();
            targetPos = transform.position + flatFwd * 2.4f;
            targetPos.y = TerrainMeshGenerator.GetTerrainHeight(targetPos.x, targetPos.z);
        }

        float finalYaw = ghostRotationY;
        isGhostSnapped = false;
        if (snapEnabled)
        {
            finalYaw = Mathf.Round(finalYaw / 15f) * 15f;
            string projName = availableProjects[selectedProjectIndex].structureName;
            if (projName.Contains("Palicada"))
            {
                Transform bestNeighbor = FindClosestBuildingMatch(targetPos, 12.25f, "Palicada", null);

                if (bestNeighbor != null)
                {
                    Vector3 sideDir = bestNeighbor.right;
                    float dot = Vector3.Dot(targetPos - bestNeighbor.position, sideDir);
                    targetPos = bestNeighbor.position + sideDir * (dot >= 0f ? 2.15f : -2.15f);
                    targetPos.y = TerrainMeshGenerator.GetTerrainHeight(targetPos.x, targetPos.z);
                    finalYaw = bestNeighbor.eulerAngles.y;
                    isGhostSnapped = true;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else if (projName.Contains("Piso"))
            {
                Transform neighbor = FindClosestBuildingMatch(targetPos, 16f, "Piso", null);
                if (neighbor != null)
                {
                    Vector3 localDelta = neighbor.InverseTransformPoint(targetPos);
                    Vector3 offset = Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z)
                        ? neighbor.right * (localDelta.x >= 0f ? 3.0f : -3.0f)
                        : neighbor.forward * (localDelta.z >= 0f ? 3.0f : -3.0f);
                    targetPos = neighbor.position + offset;
                    finalYaw = neighbor.eulerAngles.y;
                    isGhostSnapped = true;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else if (projName.Contains("Parede") || projName.Contains("Porta"))
            {
                Transform floorSnap = FindClosestBuildingMatch(targetPos, 11.5f, "Piso", null);
                Transform wallSnap = FindClosestBuildingMatch(targetPos, 12.25f, "Parede", "Porta");
                if (floorSnap != null)
                {
                    Vector3 localDelta = floorSnap.InverseTransformPoint(targetPos);
                    if (Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z))
                    {
                        targetPos = floorSnap.position + floorSnap.right * (localDelta.x >= 0f ? 1.48f : -1.48f);
                        finalYaw = floorSnap.eulerAngles.y + 90f;
                    }
                    else
                    {
                        targetPos = floorSnap.position + floorSnap.forward * (localDelta.z >= 0f ? 1.48f : -1.48f);
                        finalYaw = floorSnap.eulerAngles.y;
                    }
                    targetPos.y += 0.14f;
                    isGhostSnapped = true;
                }
                else if (wallSnap != null)
                {
                    float dot = Vector3.Dot(targetPos - wallSnap.position, wallSnap.right);
                    targetPos = wallSnap.position + wallSnap.right * (dot >= 0f ? 3.0f : -3.0f);
                    finalYaw = wallSnap.eulerAngles.y;
                    isGhostSnapped = true;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else if (projName.Contains("Teto"))
            {
                Transform roofSnap = FindClosestBuildingMatch(targetPos, 14f, "Teto", null);
                Transform floorSnap = FindClosestBuildingMatch(targetPos, 11.5f, "Piso", null);
                if (roofSnap != null)
                {
                    Vector3 localDelta = roofSnap.InverseTransformPoint(targetPos);
                    Vector3 offset = Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z)
                        ? roofSnap.right * (localDelta.x >= 0f ? 3.0f : -3.0f)
                        : roofSnap.forward * (localDelta.z >= 0f ? 3.0f : -3.0f);
                    targetPos = roofSnap.position + offset;
                    finalYaw = roofSnap.eulerAngles.y;
                    isGhostSnapped = true;
                }
                else if (floorSnap != null)
                {
                    targetPos = floorSnap.position + Vector3.up * 2.52f;
                    finalYaw = floorSnap.eulerAngles.y;
                    isGhostSnapped = true;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else if (projName.Contains("Escada"))
            {
                Transform floorSnap = FindClosestBuildingMatch(targetPos, 12.25f, "Piso", null);
                if (floorSnap != null)
                {
                    Vector3 localDelta = floorSnap.InverseTransformPoint(targetPos);
                    if (Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z))
                    {
                        float s = localDelta.x >= 0f ? 1f : -1f;
                        targetPos = floorSnap.position + floorSnap.right * (s * 2.1f);
                        finalYaw = floorSnap.eulerAngles.y + (s > 0f ? -90f : 90f);
                    }
                    else
                    {
                        float s = localDelta.z >= 0f ? 1f : -1f;
                        targetPos = floorSnap.position + floorSnap.forward * (s * 2.1f);
                        finalYaw = floorSnap.eulerAngles.y + (s > 0f ? 180f : 0f);
                    }
                    targetPos.y = TerrainMeshGenerator.GetTerrainHeight(targetPos.x, targetPos.z);
                    isGhostSnapped = true;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else
            {
                SnapToGridXZ(ref targetPos);
            }
        }

        activeGhostObj.transform.position = targetPos;
        activeGhostObj.transform.rotation = Quaternion.Euler(0, finalYaw, 0);

        bool insideIsland = !FragaWorldBlueprint.IsInitialized || FragaWorldBlueprint.IsInsideIsland(targetPos.x, targetPos.z);
        float waterDepth = FragaWorldBlueprint.IsInitialized ? FragaWorldBlueprint.GetWaterDepth(targetPos.x, targetPos.z) : 0f;
        isGhostPlacementValid = insideIsland && waterDepth < 0.45f;
        ghostInvalidReason = !insideIsland ? "FORA DA ILHA" : (waterDepth >= 0.45f ? "AGUA PROFUNDA" : string.Empty);

        if (activeGhostMat != null)
        {
            activeGhostMat.color = !isGhostPlacementValid
                ? new Color(1.0f, 0.28f, 0.22f, 0.54f)
                : (isGhostSnapped
                    ? new Color(0.24f, 1.0f, 0.68f, 0.52f)
                    : new Color(0.20f, 0.78f, 1.0f, 0.48f));
        }

        if (Time.unscaledTime >= nextPlacementHeaderUpdateTime)
        {
            nextPlacementHeaderUpdateTime = Time.unscaledTime + 0.10f;
            UpdatePlacementHeader(finalYaw);
        }
    }

    private static void SnapToGridXZ(ref Vector3 pos)
    {
        pos.x = Mathf.Round(pos.x * 2f) * 0.5f;
        pos.z = Mathf.Round(pos.z * 2f) * 0.5f;
        pos.y = TerrainMeshGenerator.GetTerrainHeight(pos.x, pos.z);
    }

    private static Transform FindClosestBuildingMatch(Vector3 targetPos, float maxDistSq, string filterA, string filterB)
    {
        Transform best = null;
        float bestSq = maxDistSq;
        var structs = StructureHealth.AllStructures;
        for (int i = 0; i < structs.Count; i++)
        {
            var s = structs[i];
            if (s == null) continue;
            if (!s.structureName.Contains(filterA) && (filterB == null || !s.structureName.Contains(filterB))) continue;
            float dSq = (s.transform.position - targetPos).sqrMagnitude;
            if (dSq < bestSq) { bestSq = dSq; best = s.transform; }
        }
        var sites = WorldBlueprintSite.AllSites;
        for (int i = 0; i < sites.Count; i++)
        {
            var st = sites[i];
            if (st == null) continue;
            if (!st.structureName.Contains(filterA) && (filterB == null || !st.structureName.Contains(filterB))) continue;
            float dSq = (st.transform.position - targetPos).sqrMagnitude;
            if (dSq < bestSq) { bestSq = dSq; best = st.transform; }
        }
        return best;
    }

    public GameObject SpawnStructureObject(string structureName, Vector3 pos, float rotY, int neededLogs, int neededStones)
    {
        GameObject blueprintObj = CreateGhostVisual(structureName);
        blueprintObj.name = $"Blueprint_{structureName}";
        blueprintObj.transform.position = pos;
        blueprintObj.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

        BoxCollider siteCol = blueprintObj.AddComponent<BoxCollider>();
        siteCol.center = new Vector3(0f, 1.25f, 0f);
        siteCol.size = structureName.Contains("Cabana")
            ? new Vector3(4.0f, 2.8f, 4.0f)
            : new Vector3(3.4f, 2.6f, 3.4f);
        siteCol.isTrigger = true;

        WorldBlueprintSite site = blueprintObj.AddComponent<WorldBlueprintSite>();
        site.structureName = structureName;
        site.neededLogs = neededLogs;
        site.neededStones = neededStones;
        site.structureId = System.Guid.NewGuid().ToString("N");
        FragaCoopSession coop = FragaCoopSession.Instance;
        site.ownerPlayerId = coop != null ? coop.localPlayerId : "P1-LOCAL";
        site.ownerPlayerName = coop != null ? coop.localPlayerName : "Sobrevivente";
        site.isSharedWithGroup = coop == null || coop.DefaultStructuresShared;
        site.UpdateBlueprintProgressVisuals();
        site.RegisterInGlobalList();
        return blueprintObj;
    }

    public void ConfirmPlacement()
    {
        if (activeGhostObj == null) return;
        if (!isGhostPlacementValid)
        {
            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip(
                    $"<color=#FF7766>Local invalido ({ghostInvalidReason})! Mova o molde para terra firme.</color>");
            }
            return;
        }

        StructureProject proj = availableProjects[selectedProjectIndex];
        Vector3 pos = activeGhostObj.transform.position;
        float rotY = activeGhostObj.transform.eulerAngles.y;

        SafeDestroy(activeGhostObj);
        activeGhostObj = null;
        activeGhostMat = null;
        isPlacingGhost = false;
        ShowPlacementBar(false);

        GameObject blueprintObj = SpawnStructureObject(proj.structureName, pos, rotY, proj.requiredLogs, proj.requiredStones);
        WorldBlueprintSite site = blueprintObj.GetComponent<WorldBlueprintSite>();

        if (audioSource != null && placeGhostClip != null)
            audioSource.PlayOneShot(placeGhostClip, 0.75f);

        if (site != null && FragaCoopSession.Instance != null)
        {
            FragaCoopSession.Instance.BroadcastBuildEvent(new CoopBuildSyncEvent
            {
                structureNetId = site.structureId,
                ownerPlayerId = site.ownerPlayerId,
                ownerName = site.ownerPlayerName,
                structureName = site.structureName,
                actionType = "Place",
                position = pos,
                rotationY = rotY,
                currentLogs = site.currentLogs,
                neededLogs = site.neededLogs,
                currentStones = site.currentStones,
                neededStones = site.neededStones,
                isComplete = false,
                isSharedWithGroup = site.isSharedWithGroup
            });
        }

        // Se o jogador ja tiver recursos na mochila ao posicionar, deposita automaticamente o que tiver!
        site.TryDepositResource(gameObject, silentIfEmpty: true);

        if (site != null && !site.IsComplete)
        {
            site.UpdateBlueprintProgressVisuals();
            if (HUDSurvivalOverlay.Instance != null)
            {
                bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
                HUDSurvivalOverlay.Instance.ShowLootTooltip(touchActive
                    ? $"Projeto {proj.structureName} ({site.currentLogs}/{site.neededLogs} Mad, {site.currentStones}/{site.neededStones} Ped) - toque em COLOCAR MATERIAIS!"
                    : $"Projeto {proj.structureName} ({site.currentLogs}/{site.neededLogs} Mad, {site.currentStones}/{site.neededStones} Ped) - aperte [E] ou clique no cartao para concluir!");
            }
        }
    }

    public void CancelPlacement()
    {
        isPlacingGhost = false;
        if (activeGhostObj != null)
        {
            SafeDestroy(activeGhostObj);
            activeGhostObj = null;
            activeGhostMat = null;
        }
        CloseSurvivalBook();
        ShowPlacementBar(false);
    }

    public void ClearAllPlacedStructures()
    {
        if (!Application.isPlaying)
        {
            foreach (StructureHealth s in FindObjectsByType<StructureHealth>())
                if (s != null) s.RegisterInGlobalList();
            foreach (WorldBlueprintSite st in FindObjectsByType<WorldBlueprintSite>())
                if (st != null) st.RegisterInGlobalList();
        }

        for (int i = StructureHealth.AllStructures.Count - 1; i >= 0; i--)
        {
            StructureHealth sh = StructureHealth.AllStructures[i];
            if (sh != null)
            {
                sh.UnregisterFromGlobalList();
                SafeDestroy(sh.gameObject);
            }
        }
        StructureHealth.AllStructures.RemoveAll(s => s == null);

        for (int i = WorldBlueprintSite.AllSites.Count - 1; i >= 0; i--)
        {
            WorldBlueprintSite site = WorldBlueprintSite.AllSites[i];
            if (site != null)
            {
                site.UnregisterFromGlobalList();
                SafeDestroy(site.gameObject);
            }
        }
        WorldBlueprintSite.AllSites.RemoveAll(s => s == null);
    }

    public void FindProjectSpec(string structureName, out int reqLogs, out int reqStones)
    {
        if (availableProjects == null || availableProjects.Length == 0) SetupDefaultProjects();
        if (availableProjects != null && !string.IsNullOrEmpty(structureName))
        {
            for (int i = 0; i < availableProjects.Length; i++)
            {
                if (availableProjects[i].structureName == structureName)
                {
                    reqLogs = availableProjects[i].requiredLogs;
                    reqStones = availableProjects[i].requiredStones;
                    return;
                }
            }
        }
        reqLogs = 3;
        reqStones = 0;
    }

    public void ExportStructures(List<SavedBlueprintStructureData> outList)
    {
        if (outList == null) return;
        outList.Clear();

        if (!Application.isPlaying)
        {
            foreach (StructureHealth s in FindObjectsByType<StructureHealth>())
                if (s != null) s.RegisterInGlobalList();
            foreach (WorldBlueprintSite st in FindObjectsByType<WorldBlueprintSite>())
                if (st != null) st.RegisterInGlobalList();
        }

        for (int i = 0; i < StructureHealth.AllStructures.Count; i++)
        {
            StructureHealth sh = StructureHealth.AllStructures[i];
            if (sh == null) continue;

            FindProjectSpec(sh.structureName, out int reqLogs, out int reqStones);
            SavedBlueprintStructureData data = new SavedBlueprintStructureData
            {
                structureName = sh.structureName,
                structureId = sh.structureId,
                ownerPlayerId = sh.ownerPlayerId,
                ownerPlayerName = sh.ownerPlayerName,
                isSharedWithGroup = sh.isSharedWithGroup,
                posX = sh.transform.position.x,
                posY = sh.transform.position.y,
                posZ = sh.transform.position.z,
                rotY = sh.transform.eulerAngles.y,
                neededLogs = reqLogs,
                neededStones = reqStones,
                currentLogs = reqLogs,
                currentStones = reqStones,
                isComplete = true,
                currentHealth = sh.currentHealth
            };

            DoorInteractable door = sh.GetComponent<DoorInteractable>();
            if (door != null) data.doorIsOpen = door.isOpen;

            LogHolderInteractable holder = sh.GetComponent<LogHolderInteractable>();
            if (holder != null) data.storedLogs = holder.storedLogs;

            RainCollectorInteractable collector = sh.GetComponent<RainCollectorInteractable>();
            if (collector != null) data.waterCharges = collector.waterCharges;

            DryingRackInteractable rack = sh.GetComponent<DryingRackInteractable>();
            if (rack != null) rack.ExportState(out data.dryingRawItemIds, out data.dryingTimers, out data.dryingIsDried);

            StorageChestInteractable chest = sh.GetComponent<StorageChestInteractable>();
            if (chest != null)
            {
                for (int s = 0; s < chest.stacks.Count; s++)
                {
                    data.chestStacks.Add(new SavedChestStackData
                    {
                        itemId = chest.stacks[s].itemId,
                        amount = chest.stacks[s].amount
                    });
                }
            }

            outList.Add(data);
        }

        for (int i = 0; i < WorldBlueprintSite.AllSites.Count; i++)
        {
            WorldBlueprintSite site = WorldBlueprintSite.AllSites[i];
            if (site == null || site.IsComplete || site.GetComponent<StructureHealth>() != null) continue;

            outList.Add(new SavedBlueprintStructureData
            {
                structureName = site.structureName,
                structureId = site.structureId,
                ownerPlayerId = site.ownerPlayerId,
                ownerPlayerName = site.ownerPlayerName,
                isSharedWithGroup = site.isSharedWithGroup,
                posX = site.transform.position.x,
                posY = site.transform.position.y,
                posZ = site.transform.position.z,
                rotY = site.transform.eulerAngles.y,
                neededLogs = site.neededLogs,
                neededStones = site.neededStones,
                currentLogs = site.currentLogs,
                currentStones = site.currentStones,
                isComplete = false
            });
        }
    }

    public void ImportStructures(List<SavedBlueprintStructureData> savedList)
    {
        ClearAllPlacedStructures();
        if (savedList == null || savedList.Count == 0) return;

        for (int i = 0; i < savedList.Count; i++)
        {
            SavedBlueprintStructureData d = savedList[i];
            if (d == null || string.IsNullOrEmpty(d.structureName)) continue;

            FindProjectSpec(d.structureName, out int fallbackLogs, out int fallbackStones);
            int reqLogs = d.neededLogs > 0 ? d.neededLogs : fallbackLogs;
            int reqStones = d.neededStones >= 0 ? d.neededStones : fallbackStones;

            GameObject obj = SpawnStructureObject(
                d.structureName,
                new Vector3(d.posX, d.posY, d.posZ),
                d.rotY,
                reqLogs,
                reqStones);
            WorldBlueprintSite site = obj.GetComponent<WorldBlueprintSite>();
            if (site == null) continue;

            if (!string.IsNullOrEmpty(d.structureId)) site.structureId = d.structureId;
            if (!string.IsNullOrEmpty(d.ownerPlayerId)) site.ownerPlayerId = d.ownerPlayerId;
            if (!string.IsNullOrEmpty(d.ownerPlayerName)) site.ownerPlayerName = d.ownerPlayerName;
            site.isSharedWithGroup = d.isSharedWithGroup;

            if (d.isComplete)
            {
                site.ForceCompleteFromSave(d.currentHealth);
                StructureHealth loadedHp = obj.GetComponent<StructureHealth>();
                if (loadedHp != null)
                {
                    loadedHp.isSharedWithGroup = d.isSharedWithGroup;
                }

                DoorInteractable door = obj.GetComponent<DoorInteractable>();
                if (door != null) door.SetOpenState(d.doorIsOpen, silent: true);

                LogHolderInteractable holder = obj.GetComponent<LogHolderInteractable>();
                if (holder != null) holder.SetStoredLogs(d.storedLogs);

                RainCollectorInteractable collector = obj.GetComponent<RainCollectorInteractable>();
                if (collector != null) collector.SetWaterCharges(d.waterCharges);

                DryingRackInteractable rack = obj.GetComponent<DryingRackInteractable>();
                if (rack != null) rack.ImportState(d.dryingRawItemIds, d.dryingTimers, d.dryingIsDried);

                StorageChestInteractable chest = obj.GetComponent<StorageChestInteractable>();
                if (chest != null) chest.ImportStacks(d.chestStacks);
            }
            else
            {
                site.currentLogs = Mathf.Clamp(d.currentLogs, 0, reqLogs);
                site.currentStones = Mathf.Clamp(d.currentStones, 0, reqStones);
                site.UpdateBlueprintProgressVisuals();
            }
        }
    }

    private GameObject CreateGhostVisual(string structureName)
    {
        GameObject ghost = new GameObject($"Ghost_{structureName}");
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material ghostMat = new Material(s);
        ghostMat.color = new Color(0.20f, 0.75f, 1.0f, 0.48f);

        if (structureName.Contains("Palicada"))
        {
            for (int i = -2; i <= 2; i++)
            {
                AddGhostPart(ghost.transform, $"Log_{i}", PrimitiveType.Cylinder,
                    new Vector3(i * 0.44f, 1.15f, 0f), new Vector3(0.26f, 1.15f, 0.26f), Vector3.zero, ghostMat);
                AddGhostPart(ghost.transform, $"Spike_{i}", PrimitiveType.Cylinder,
                    new Vector3(i * 0.44f, 1.45f, 0.36f), new Vector3(0.08f, 0.55f, 0.08f), new Vector3(42f, 0f, 0f), ghostMat);
            }
            AddGhostPart(ghost.transform, "CrossBrace_Low", PrimitiveType.Cube,
                new Vector3(0f, 0.65f, -0.12f), new Vector3(2.15f, 0.14f, 0.14f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "CrossBrace_High", PrimitiveType.Cube,
                new Vector3(0f, 1.65f, -0.12f), new Vector3(2.15f, 0.14f, 0.14f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Lashing_Low", PrimitiveType.Cylinder,
                new Vector3(0f, 0.65f, 0.04f), new Vector3(2.22f, 0.04f, 0.22f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Lashing_High", PrimitiveType.Cylinder,
                new Vector3(0f, 1.65f, 0.04f), new Vector3(2.22f, 0.04f, 0.22f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Secador"))
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side * 0.85f;
                AddGhostPart(ghost.transform, $"AFrame_F_{side}", PrimitiveType.Cylinder,
                    new Vector3(sx, 0.92f, 0.35f), new Vector3(0.11f, 0.96f, 0.11f), new Vector3(-20f, 0f, 0f), ghostMat);
                AddGhostPart(ghost.transform, $"AFrame_B_{side}", PrimitiveType.Cylinder,
                    new Vector3(sx, 0.92f, -0.35f), new Vector3(0.11f, 0.96f, 0.11f), new Vector3(20f, 0f, 0f), ghostMat);
                AddGhostPart(ghost.transform, $"RackLashing_{side}", PrimitiveType.Sphere,
                    new Vector3(sx, 1.72f, 0f), new Vector3(0.18f, 0.14f, 0.18f), Vector3.zero, ghostMat);
            }
            AddGhostPart(ghost.transform, "DryingBar_Top", PrimitiveType.Cylinder,
                new Vector3(0f, 1.72f, 0f), new Vector3(0.08f, 1.0f, 0.08f), new Vector3(0f, 0f, 90f), ghostMat);
            AddGhostPart(ghost.transform, "DryingBar_Mid", PrimitiveType.Cylinder,
                new Vector3(0f, 1.18f, 0f), new Vector3(0.07f, 0.95f, 0.07f), new Vector3(0f, 0f, 90f), ghostMat);
        }
        else if (structureName.Contains("Coletor"))
        {
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 120f;
                Vector3 off = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0.62f, 0.55f);
                AddGhostPart(ghost.transform, $"TripodLeg_{i}", PrimitiveType.Cylinder,
                    off, new Vector3(0.09f, 0.70f, 0.09f), new Vector3(22f, ang, 0f), ghostMat);
            }
            AddGhostPart(ghost.transform, "Basin_Shell", PrimitiveType.Cylinder,
                new Vector3(0f, 0.98f, 0f), new Vector3(1.12f, 0.18f, 1.12f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Basin_StoneRim", PrimitiveType.Cylinder,
                new Vector3(0f, 1.08f, 0f), new Vector3(1.20f, 0.05f, 1.20f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Collector_Foliage", PrimitiveType.Cube,
                new Vector3(0f, 1.25f, 0.38f), new Vector3(1.05f, 0.08f, 0.85f), new Vector3(-28f, 0f, 0f), ghostMat);
        }
        else if (structureName.Contains("Piso"))
        {
            AddGhostPart(ghost.transform, "Deck_Planks", PrimitiveType.Cube,
                new Vector3(0f, 0.12f, 0f), new Vector3(3.0f, 0.16f, 3.0f), Vector3.zero, ghostMat);
            for (int c = 0; c < 4; c++)
            {
                float px = (c % 2 == 0) ? -1.36f : 1.36f;
                float pz = (c < 2) ? -1.36f : 1.36f;
                AddGhostPart(ghost.transform, $"FloorStilt_{c}", PrimitiveType.Cylinder,
                    new Vector3(px, -0.15f, pz), new Vector3(0.20f, 0.32f, 0.20f), Vector3.zero, ghostMat);
            }
        }
        else if (structureName.Contains("Porta"))
        {
            AddGhostPart(ghost.transform, "Wall_LeftFrame", PrimitiveType.Cube,
                new Vector3(-1.02f, 1.20f, 0f), new Vector3(0.96f, 2.40f, 0.22f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Wall_RightFrame", PrimitiveType.Cube,
                new Vector3(1.02f, 1.20f, 0f), new Vector3(0.96f, 2.40f, 0.22f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Wall_TopLintel", PrimitiveType.Cube,
                new Vector3(0f, 2.15f, 0f), new Vector3(1.12f, 0.50f, 0.22f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Door_Leaf", PrimitiveType.Cube,
                new Vector3(0f, 0.95f, 0f), new Vector3(1.02f, 1.88f, 0.10f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Parede"))
        {
            for (int r = 0; r < 6; r++)
            {
                AddGhostPart(ghost.transform, $"WallLog_{r}", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.22f + r * 0.40f, 0f), new Vector3(0.22f, 1.50f, 0.22f), new Vector3(0f, 0f, 90f), ghostMat);
            }
            AddGhostPart(ghost.transform, "WallPost_L", PrimitiveType.Cylinder,
                new Vector3(-1.42f, 1.20f, 0f), new Vector3(0.18f, 1.20f, 0.18f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "WallPost_R", PrimitiveType.Cylinder,
                new Vector3(1.42f, 1.20f, 0f), new Vector3(0.18f, 1.20f, 0.18f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Teto"))
        {
            AddGhostPart(ghost.transform, "Roof_Deck", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, 0f), new Vector3(3.15f, 0.16f, 3.15f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Roof_BeamL", PrimitiveType.Cylinder,
                new Vector3(-1.35f, -0.02f, 0f), new Vector3(0.18f, 1.50f, 0.18f), new Vector3(90f, 0f, 0f), ghostMat);
            AddGhostPart(ghost.transform, "Roof_BeamR", PrimitiveType.Cylinder,
                new Vector3(1.35f, -0.02f, 0f), new Vector3(0.18f, 1.50f, 0.18f), new Vector3(90f, 0f, 0f), ghostMat);
        }
        else if (structureName.Contains("Escada"))
        {
            for (int sIdx = 0; sIdx < 7; sIdx++)
            {
                float sy = 0.18f + sIdx * 0.34f;
                float sz = -0.95f + sIdx * 0.32f;
                AddGhostPart(ghost.transform, $"Step_{sIdx}", PrimitiveType.Cube,
                    new Vector3(0f, sy, sz), new Vector3(1.40f, 0.14f, 0.42f), Vector3.zero, ghostMat);
            }
        }
        else if (structureName.Contains("Cesto"))
        {
            AddGhostPart(ghost.transform, "Holder_FrameL", PrimitiveType.Cube,
                new Vector3(-0.78f, 0.55f, 0f), new Vector3(0.14f, 1.10f, 0.95f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Holder_FrameR", PrimitiveType.Cube,
                new Vector3(0.78f, 0.55f, 0f), new Vector3(0.14f, 1.10f, 0.95f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Holder_Base", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, 0f), new Vector3(1.70f, 0.12f, 0.92f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Bau"))
        {
            AddGhostPart(ghost.transform, "Chest_Body", PrimitiveType.Cube,
                new Vector3(0f, 0.32f, 0f), new Vector3(1.15f, 0.58f, 0.72f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Chest_Lid", PrimitiveType.Cube,
                new Vector3(0f, 0.66f, 0f), new Vector3(1.20f, 0.14f, 0.76f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Chest_IronBand_L", PrimitiveType.Cube,
                new Vector3(-0.38f, 0.42f, 0f), new Vector3(0.08f, 0.66f, 0.78f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Chest_IronBand_R", PrimitiveType.Cube,
                new Vector3(0.38f, 0.42f, 0f), new Vector3(0.08f, 0.66f, 0.78f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Chest_IronLockplate", PrimitiveType.Cube,
                new Vector3(0f, 0.56f, 0.38f), new Vector3(0.18f, 0.18f, 0.05f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Cabana"))
        {
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1.35f : 1.35f;
                float z = (i < 2) ? -1.15f : 1.15f;
                AddGhostPart(ghost.transform, $"Post_{i}", PrimitiveType.Cylinder,
                    new Vector3(x, 1.15f, z), new Vector3(0.20f, 1.15f, 0.20f), Vector3.zero, ghostMat);
            }
            AddGhostPart(ghost.transform, "Wall_Back", PrimitiveType.Cube,
                new Vector3(0f, 1.05f, -1.15f), new Vector3(2.75f, 2.0f, 0.18f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Wall_Left", PrimitiveType.Cube,
                new Vector3(-1.35f, 1.05f, 0f), new Vector3(0.18f, 2.0f, 2.35f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Wall_Right", PrimitiveType.Cube,
                new Vector3(1.35f, 1.05f, 0f), new Vector3(0.18f, 2.0f, 2.35f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Cabin_Doorframe_L", PrimitiveType.Cube,
                new Vector3(-0.88f, 1.05f, 1.15f), new Vector3(0.95f, 2.0f, 0.18f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Cabin_Doorframe_R", PrimitiveType.Cube,
                new Vector3(0.88f, 1.05f, 1.15f), new Vector3(0.95f, 2.0f, 0.18f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Cabin_RidgeLog", PrimitiveType.Cylinder,
                new Vector3(0f, 2.86f, 0f), new Vector3(0.16f, 1.40f, 0.16f), new Vector3(90f, 0f, 0f), ghostMat);
            AddGhostPart(ghost.transform, "Roof_Left", PrimitiveType.Cube,
                new Vector3(-0.76f, 2.48f, 0f), new Vector3(1.78f, 0.14f, 2.65f), new Vector3(0f, 0f, 26f), ghostMat);
            AddGhostPart(ghost.transform, "Roof_Right", PrimitiveType.Cube,
                new Vector3(0.76f, 2.48f, 0f), new Vector3(1.78f, 0.14f, 2.65f), new Vector3(0f, 0f, -26f), ghostMat);
            AddGhostPart(ghost.transform, "Cabin_StoneHearth", PrimitiveType.Cylinder,
                new Vector3(0.85f, 0.12f, -0.55f), new Vector3(0.64f, 0.12f, 0.64f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Bed_Mat", PrimitiveType.Cube,
                new Vector3(-0.4f, 0.15f, -0.25f), new Vector3(1.35f, 0.22f, 1.75f), Vector3.zero, ghostMat);
        }
        else
        {
            AddGhostPart(ghost.transform, "Post_FL", PrimitiveType.Cylinder,
                new Vector3(-1.1f, 0.85f, 0.85f), new Vector3(0.15f, 0.85f, 0.15f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Post_FR", PrimitiveType.Cylinder,
                new Vector3(1.1f, 0.85f, 0.85f), new Vector3(0.15f, 0.85f, 0.15f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Ridge_Log", PrimitiveType.Cylinder,
                new Vector3(0f, 1.65f, 0.85f), new Vector3(0.14f, 1.2f, 0.14f), new Vector3(0f, 0f, 90f), ghostMat);
            AddGhostPart(ghost.transform, "Roof_Fern", PrimitiveType.Cube,
                new Vector3(0f, 0.90f, -0.05f), new Vector3(2.45f, 0.12f, 2.35f), new Vector3(38f, 0f, 0f), ghostMat);
            AddGhostPart(ghost.transform, "Roof_FernUpper", PrimitiveType.Cube,
                new Vector3(0f, 1.18f, 0.26f), new Vector3(2.35f, 0.10f, 1.25f), new Vector3(38f, 0f, 0f), ghostMat);
            AddGhostPart(ghost.transform, "Bed_FernMat", PrimitiveType.Cube,
                new Vector3(0f, 0.12f, 0.0f), new Vector3(1.75f, 0.20f, 1.50f), Vector3.zero, ghostMat);
        }

        return ghost;
    }

    private void AddGhostPart(Transform parent, string partName, PrimitiveType type, Vector3 localPos, Vector3 localScale, Vector3 localEuler, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        SafeDestroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;
        part.transform.localRotation = Quaternion.Euler(localEuler);
        part.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    public void PlayHammerSound(Vector3 pos)
    {
        if (audioSource != null && hammerHitClip != null)
        {
            audioSource.pitch = Random.Range(0.88f, 1.12f);
            audioSource.PlayOneShot(hammerHitClip, 0.85f);
        }
    }

    public void PlayCompleteSound(Vector3 pos)
    {
        if (audioSource != null && completeBuildClip != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(completeBuildClip, 1.0f);
        }
    }

    private AudioClip GenerateHammerHitSound()
    {
        int sr = 44100; int count = (int)(sr * 0.22f); float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 140f * ((float)i / sr)) * Mathf.Exp(-16f * t) * 0.75f + Mathf.Sin(2f * Mathf.PI * 950f * ((float)i / sr)) * Mathf.Exp(-32f * t) * 0.45f;
        }
        AudioClip clip = AudioClip.Create("HammerHit_Procedural", count, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GeneratePlaceGhostSound()
    {
        int sr = 44100; int count = (int)(sr * 0.25f); float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 520f * ((float)i / sr)) * Mathf.Exp(-12f * t) * 0.6f;
        }
        AudioClip clip = AudioClip.Create("PlaceGhost_Procedural", count, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateCompleteBuildSound()
    {
        int sr = 44100; int count = (int)(sr * 0.45f); float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 220f * ((float)i / sr)) * Mathf.Exp(-5f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 440f * ((float)i / sr)) * Mathf.Exp(-6f * t) * 0.4f;
        }
        AudioClip clip = AudioClip.Create("CompleteBuild_Procedural", count, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// WorldBlueprintSite: Sitio de construcao fisica no mundo (interacao via InteractionDetector, Cartao da Obra ou golpe, Zero-Find no Update).
/// Deposita todos os recursos disponiveis na mochila de uma vez e suporta feedback visual progressivo e cancelamento com reembolso.
/// </summary>
public class WorldBlueprintSite : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<WorldBlueprintSite> AllSites = new System.Collections.Generic.List<WorldBlueprintSite>();
    public string structureName;
    public string structureId;
    public string ownerPlayerId = "P1-LOCAL";
    public string ownerPlayerName = "Sobrevivente";
    public bool isSharedWithGroup = true;
    public int neededLogs;
    public int neededStones;

    public int currentLogs = 0;
    public int currentStones = 0;

    private Material builtPartMat;
    private Material pendingPartMat;

    public bool IsComplete => currentLogs >= neededLogs && currentStones >= neededStones;

    public float Progress01
    {
        get
        {
            int totalNeed = Mathf.Max(1, neededLogs + neededStones);
            int totalHave = Mathf.Min(neededLogs, currentLogs) + Mathf.Min(neededStones, currentStones);
            return Mathf.Clamp01((float)totalHave / totalNeed);
        }
    }

    public void RegisterInGlobalList() { if (!AllSites.Contains(this)) AllSites.Add(this); }
    public void UnregisterFromGlobalList() { AllSites.Remove(this); }
    private void Awake() { RegisterInGlobalList(); }
    private void OnEnable() { RegisterInGlobalList(); }
    private void OnDisable() { UnregisterFromGlobalList(); }
    private void OnDestroy() { UnregisterFromGlobalList(); }

    public float GetDistanceToPlayer(Vector3 playerPos)
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Vector3 closest = col.ClosestPoint(playerPos);
            return Vector3.Distance(closest, playerPos);
        }
        return Vector3.Distance(transform.position, playerPos);
    }

    public void UpdateBlueprintProgressVisuals()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        if (renderers == null || renderers.Length == 0) return;

        if (builtPartMat == null || pendingPartMat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (s != null)
            {
                builtPartMat = new Material(s);
                builtPartMat.color = new Color(0.74f, 0.56f, 0.28f, 0.94f);
                pendingPartMat = new Material(s);
                pendingPartMat.color = new Color(0.20f, 0.78f, 1.0f, 0.45f);
            }
        }

        if (builtPartMat == null || pendingPartMat == null) return;

        int builtCount = Mathf.RoundToInt(Progress01 * renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].sharedMaterial = (i < builtCount) ? builtPartMat : pendingPartMat;
            }
        }
    }

    public void CancelAndRefund(GameObject player)
    {
        if (FragaCoopSession.Instance != null && !FragaCoopSession.Instance.CanLocalPlayerInteract(ownerPlayerId, isSharedWithGroup))
        {
            if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF8866>Obra privada de {ownerPlayerName}!</color>");
            }
            return;
        }

        InventoryAdvanced inv = InventoryAdvanced.Instance != null ? InventoryAdvanced.Instance : FindAnyObjectByType<InventoryAdvanced>();
        int refundedLogs = currentLogs;
        int refundedStones = currentStones;

        if (inv != null)
        {
            if (refundedLogs > 0)
            {
                int left = inv.AddItem(100, refundedLogs);
                if (left > 0 && LootManager.Instance != null)
                    LootManager.Instance.DropItem(100, left, transform.position + Vector3.up * 0.4f);
            }
            if (refundedStones > 0)
            {
                int left = inv.AddItem(101, refundedStones);
                if (left > 0 && LootManager.Instance != null)
                    LootManager.Instance.DropItem(101, left, transform.position + Vector3.up * 0.4f);
            }
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupWood, transform.position, 0.72f, 0.90f, 1.06f);
        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(
                (refundedLogs > 0 || refundedStones > 0)
                    ? $"Obra cancelada: {structureName} (Devolvido: {refundedLogs} Mad, {refundedStones} Ped)"
                    : $"Projeto removido: {structureName}");
        }

        if (FragaCoopSession.Instance != null)
        {
            FragaCoopSession.Instance.BroadcastBuildEvent(new CoopBuildSyncEvent
            {
                structureNetId = structureId,
                ownerPlayerId = ownerPlayerId,
                ownerName = ownerPlayerName,
                structureName = structureName,
                actionType = "Cancel",
                position = transform.position,
                rotationY = transform.eulerAngles.y,
                isSharedWithGroup = isSharedWithGroup
            });
        }

        BlueprintPlacementManager.SafeDestroy(gameObject);
    }

    public bool TryDepositResource(GameObject player)
    {
        return TryDepositResource(player, silentIfEmpty: false);
    }

    public bool TryDepositResource(GameObject player, bool silentIfEmpty)
    {
        int addedLogs = 0;
        int addedStones = 0;

        LogCarryingSystem logCarry = LogCarryingSystem.Instance;
        while (logCarry != null && currentLogs < neededLogs && logCarry.ConsumeOneLog())
        {
            currentLogs++;
            addedLogs++;
        }

        InventoryAdvanced inv = InventoryAdvanced.Instance != null ? InventoryAdvanced.Instance : FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null)
        {
            int missingStones = neededStones - currentStones;
            if (missingStones > 0)
            {
                int availStones = inv.GetItemCount(101);
                int toTake = Mathf.Min(missingStones, availStones);
                if (toTake > 0 && inv.RemoveItem(101, toTake))
                {
                    currentStones += toTake;
                    addedStones += toTake;
                }
            }

            int missingLogs = neededLogs - currentLogs;
            if (missingLogs > 0)
            {
                int availWood = inv.GetItemCount(100);
                int toTake = Mathf.Min(missingLogs, availWood);
                if (toTake > 0 && inv.RemoveItem(100, toTake))
                {
                    currentLogs += toTake;
                    addedLogs += toTake;
                }
            }
        }

        if (addedLogs > 0 || addedStones > 0)
        {
            OnResourcesAdded(addedLogs, addedStones);
            return true;
        }

        if (!silentIfEmpty && HUDSurvivalOverlay.Instance != null)
        {
            int remLogs = Mathf.Max(0, neededLogs - currentLogs);
            int remStones = Mathf.Max(0, neededStones - currentStones);
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Para concluir {structureName}: faltam {remLogs}x Madeira/Tora e {remStones}x Pedra na mochila!");
        }
        return false;
    }

    private void OnResourcesAdded(int addedLogs, int addedStones)
    {
        if (BlueprintPlacementManager.Instance != null)
            BlueprintPlacementManager.Instance.PlayHammerSound(transform.position);

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(0.06f);

        if (IsComplete)
        {
            CompleteStructure(silent: false, overrideHealth: -1f);
            return;
        }

        UpdateBlueprintProgressVisuals();

        if (FragaCoopSession.Instance != null)
        {
            FragaCoopSession.Instance.BroadcastBuildEvent(new CoopBuildSyncEvent
            {
                structureNetId = structureId,
                ownerPlayerId = ownerPlayerId,
                ownerName = ownerPlayerName,
                structureName = structureName,
                actionType = "Deposit",
                position = transform.position,
                rotationY = transform.eulerAngles.y,
                currentLogs = currentLogs,
                neededLogs = neededLogs,
                currentStones = currentStones,
                neededStones = neededStones,
                isComplete = false,
                isSharedWithGroup = isSharedWithGroup
            });
        }

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(
                $"Alocado em {structureName}: ({currentLogs}/{neededLogs} Madeira | {currentStones}/{neededStones} Pedra)");
        }
    }

    public void ForceCompleteFromSave(float savedHealth = -1f)
    {
        currentLogs = neededLogs;
        currentStones = neededStones;
        CompleteStructure(silent: true, overrideHealth: savedHealth);
    }

    private void CompleteStructure(bool silent, float overrideHealth)
    {
        if (!silent)
        {
            if (BlueprintPlacementManager.Instance != null)
                BlueprintPlacementManager.Instance.PlayCompleteSound(transform.position);

            if (VFXPoolManager.Instance != null)
                VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position + Vector3.up * 0.6f);
        }

        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark")
            ?? Resources.Load<Material>("Materials/Wood_Bark");
        Material fernMat = Resources.Load<Material>("Materials/Mat_OakFoliage")
            ?? Resources.Load<Material>("Materials/Bush_Leaves")
            ?? Resources.Load<Material>("Materials/Mat_PineNeedle");
        Material stoneMat = Resources.Load<Material>("Materials/Ruin_Stones")
            ?? Resources.Load<Material>("Materials/Celtic_Stone");
        Material ironMat = Resources.Load<Material>("Materials/Mat_Ruins_Iron") ?? stoneMat;
        Material peltMat = Resources.Load<Material>("Materials/Deer_Pelt") ?? fernMat ?? barkMat;

        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            string n = r.gameObject.name;
            if (n.Contains("Bed"))
            {
                if (peltMat != null) r.sharedMaterial = peltMat;
            }
            else if (n.Contains("Fern") || n.Contains("Foliage"))
            {
                if (fernMat != null) r.sharedMaterial = fernMat;
                else if (barkMat != null) r.sharedMaterial = barkMat;
            }
            else if (n.Contains("Iron") || n.Contains("Lock"))
            {
                if (ironMat != null) r.sharedMaterial = ironMat;
            }
            else if ((n.Contains("Basin") || n.Contains("Stone")) && stoneMat != null)
            {
                r.sharedMaterial = stoneMat;
            }
            else if (barkMat != null)
            {
                r.sharedMaterial = barkMat;
            }

            if (r.GetComponent<Collider>() == null)
            {
                BoxCollider partCol = r.gameObject.AddComponent<BoxCollider>();
                if (n.Contains("Bed"))
                {
                    // Cama da cabana ou refugio tem trigger/collider detectavel pela mira
                    partCol.isTrigger = true;
                }
            }
        }

        BoxCollider rootTrig = GetComponent<BoxCollider>();
        if (rootTrig == null) rootTrig = gameObject.AddComponent<BoxCollider>();
        rootTrig.isTrigger = true;
        rootTrig.center = new Vector3(0f, 1.25f, 0f);
        rootTrig.size = new Vector3(3.6f, 2.7f, 3.6f);

        StructureHealth hp = gameObject.AddComponent<StructureHealth>();
        hp.structureName = structureName;
        hp.structureId = string.IsNullOrEmpty(structureId) ? System.Guid.NewGuid().ToString("N") : structureId;
        hp.ownerPlayerId = string.IsNullOrEmpty(ownerPlayerId) ? "P1-LOCAL" : ownerPlayerId;
        hp.ownerPlayerName = string.IsNullOrEmpty(ownerPlayerName) ? "Sobrevivente" : ownerPlayerName;
        hp.isSharedWithGroup = isSharedWithGroup;
        hp.RegisterInGlobalList();
        hp.maxHealth = structureName.Contains("Cabana") ? 240f
            : (structureName.Contains("Parede") || structureName.Contains("Porta") || structureName.Contains("Piso") || structureName.Contains("Teto") || structureName.Contains("Escada") ? 200f
            : (structureName.Contains("Palicada") ? 180f : 145f));
        hp.currentHealth = (overrideHealth > 0f)
            ? Mathf.Clamp(overrideHealth, 1f, hp.maxHealth)
            : hp.maxHealth;

        if (structureName.Contains("Palicada"))
        {
            var spike = gameObject.AddComponent<PalisadeSpikeHazard>();
            spike.structureHealth = hp;
        }
        else if (structureName.Contains("Secador"))
        {
            var rack = gameObject.AddComponent<DryingRackInteractable>();
            rack.BuildHangingSlots();
        }
        else if (structureName.Contains("Coletor"))
        {
            var collector = gameObject.AddComponent<RainCollectorInteractable>();
            collector.BuildWaterVisual();
        }
        else if (structureName.Contains("Cesto"))
        {
            gameObject.AddComponent<LogHolderInteractable>();
        }
        else if (structureName.Contains("Bau"))
        {
            gameObject.AddComponent<StorageChestInteractable>();
        }
        else if (structureName.Contains("Porta"))
        {
            var door = gameObject.AddComponent<DoorInteractable>();
            Transform leaf = transform.Find("Door_Leaf");
            if (leaf != null) door.doorLeaf = leaf;
        }
        else if (structureName.Contains("Piso") || structureName.Contains("Parede") || structureName.Contains("Teto") || structureName.Contains("Escada"))
        {
            // Peca modular estrutural com reparo via StructureHealth
        }
        else
        {
            var shelter = gameObject.AddComponent<ShelterInteractable>();
            shelter.isCabin = structureName.Contains("Cabana");
        }

        if (!silent && HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#55FF99>CONSTRUCAO CONCLUIDA: {structureName}! Use [E] para interagir.</color>");

        if (!silent && FragaCoopSession.Instance != null)
        {
            FragaCoopSession.Instance.BroadcastBuildEvent(new CoopBuildSyncEvent
            {
                structureNetId = hp.structureId,
                ownerPlayerId = hp.ownerPlayerId,
                ownerName = hp.ownerPlayerName,
                structureName = structureName,
                actionType = "Complete",
                position = transform.position,
                rotationY = transform.eulerAngles.y,
                currentLogs = neededLogs,
                neededLogs = neededLogs,
                currentStones = neededStones,
                neededStones = neededStones,
                isComplete = true,
                isSharedWithGroup = isSharedWithGroup
            });
        }

        UnregisterFromGlobalList();
        BlueprintPlacementManager.SafeDestroy(this);
    }
}

/// <summary>
/// DoorInteractable: Porta articulada para Parede com Porta (abre e fecha com [E]).
/// </summary>
public class DoorInteractable : MonoBehaviour
{
    public Transform doorLeaf;
    public bool isOpen = false;

    public string GetPromptLabel()
    {
        return isOpen ? "Fechar Porta de Madeira [E]" : "Abrir Porta de Madeira [E]";
    }

    public void ToggleDoor()
    {
        SetOpenState(!isOpen, silent: false);
    }

    public void SetOpenState(bool open, bool silent = true)
    {
        isOpen = open;
        if (doorLeaf == null) doorLeaf = transform.Find("Door_Leaf");
        if (doorLeaf != null)
        {
            if (isOpen)
            {
                doorLeaf.localPosition = new Vector3(-0.46f, 0.95f, 0.46f);
                doorLeaf.localRotation = Quaternion.Euler(0f, -86f, 0f);
            }
            else
            {
                doorLeaf.localPosition = new Vector3(0f, 0.95f, 0f);
                doorLeaf.localRotation = Quaternion.identity;
            }
        }
        if (!silent)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.DoorWoodCreak, transform.position, 0.74f, 0.90f, 1.08f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip(isOpen ? "Porta aberta" : "Porta fechada");
        }
    }
}

/// <summary>
/// ShelterInteractable: Permite dormir/descansar (a noite dorme ate 06:30 e limpa Vultos; de dia descansa 2.5h e restaura Vida/Estamina/Sanidade) e SEMPRE salva o jogo!
/// </summary>
public class ShelterInteractable : MonoBehaviour
{
    public bool isCabin = false;

    public string GetPromptLabel()
    {
        DayNightCycle dnc = DayNightCycle.Instance;
        bool isNight = dnc != null && (dnc.currentTime >= 18.0f || dnc.currentTime < 5.5f);
        if (isCabin)
        {
            return isNight
                ? "Dormir ate Amanhecer e Salvar Jogo na Cabana [E]"
                : "Descansar (+Vida/Sanidade) e Salvar Jogo na Cabana [E]";
        }
        return isNight
            ? "Dormir ate Amanhecer e Salvar Jogo no Refugio [E]"
            : "Descansar (+Vida/Sanidade) e Salvar Jogo no Refugio [E]";
    }

    public void Interact()
    {
        DayNightCycle dnc = DayNightCycle.Instance != null ? DayNightCycle.Instance : FindAnyObjectByType<DayNightCycle>();
        PlayerStatus status = PlayerStatus.Instance != null ? PlayerStatus.Instance : FindAnyObjectByType<PlayerStatus>();
        SanitySystem sanity = FindAnyObjectByType<SanitySystem>();
        SaveSystem save = FindAnyObjectByType<SaveSystem>();

        bool sleptThroughNight = false;
        if (dnc != null)
        {
            if (dnc.currentTime >= 18.0f || dnc.currentTime < 5.5f)
            {
                if (dnc.currentTime >= 18.0f) dnc.currentDay++;
                dnc.currentTime = 6.5f;
                sleptThroughNight = true;
            }
            else
            {
                // Descanso diurno: avanca 2.5 horas de descanso na cama da cabana/refugio
                dnc.currentTime += 2.5f;
                if (dnc.currentTime >= 24f)
                {
                    dnc.currentTime -= 24f;
                    dnc.currentDay++;
                }
            }
        }

        if (status != null)
        {
            status.health = Mathf.Min(100f, status.health + (isCabin ? 55f : 35f));
            status.stamina = 100f;
            status.isBleeding = false;
            float hungerDrop = sleptThroughNight ? 12f : 4f;
            float thirstDrop = sleptThroughNight ? 14f : 5f;
            if (status.hunger > 18f) status.hunger = Mathf.Max(18f, status.hunger - hungerDrop);
            if (status.thirst > 18f) status.thirst = Mathf.Max(18f, status.thirst - thirstDrop);
        }
        if (sanity != null)
        {
            sanity.sanity = Mathf.Min(100f, sanity.sanity + (isCabin ? 60f : 40f));
        }
        if (BodyTemperatureSystem.Instance != null)
        {
            BodyTemperatureSystem.Instance.ResetTemperature();
        }
        if (VultoManager.Instance != null)
        {
            VultoManager.Instance.ClearNearbyVultos(transform.position, 35f);
        }

        // Avanca secadores de carne e coletores de chuva proporcionais ao tempo dormido
        float elapsedSimSec = sleptThroughNight ? 180f : 60f;
        if (!Application.isPlaying)
        {
            foreach (DryingRackInteractable r in FindObjectsByType<DryingRackInteractable>())
                if (r != null) r.RegisterInGlobalList();
            foreach (RainCollectorInteractable c in FindObjectsByType<RainCollectorInteractable>())
                if (c != null) c.RegisterInGlobalList();
            foreach (BonfireEntity bf in FindObjectsByType<BonfireEntity>())
                if (bf != null) bf.RegisterInGlobalList();
        }
        for (int i = 0; i < DryingRackInteractable.AllRacks.Count; i++)
        {
            DryingRackInteractable rack = DryingRackInteractable.AllRacks[i];
            if (rack != null) rack.AdvanceDrying(elapsedSimSec);
        }
        for (int i = 0; i < RainCollectorInteractable.AllCollectors.Count; i++)
        {
            RainCollectorInteractable collector = RainCollectorInteractable.AllCollectors[i];
            if (collector != null) collector.AdvanceCollection(elapsedSimSec);
        }
        GalicianWeatherEngine sleepWeather = FindAnyObjectByType<GalicianWeatherEngine>();
        bool isRainingDuringSleep = sleepWeather != null && sleepWeather.isRaining;
        for (int i = BonfireEntity.AllBonfires.Count - 1; i >= 0; i--)
        {
            BonfireEntity bf = BonfireEntity.AllBonfires[i];
            if (bf != null && bf.isLit) bf.AdvanceSimulation(elapsedSimSec, isRainingDuringSleep, allowOvercookBurn: false);
        }

        if (save != null) save.SaveGame();

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(0.05f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.EquipWeapon, transform.position, 0.56f, 0.86f, 0.96f);

        if (HUDSurvivalOverlay.Instance != null)
        {
            string placeName = isCabin ? "Cabana de Toras" : "Refugio";
            HUDSurvivalOverlay.Instance.ShowLootTooltip(sleptThroughNight
                ? $"<color=#88FFAA>Dormiu na {placeName} ate 06:30 (Dia {(dnc != null ? dnc.currentDay : 1)})! Vida/Sanidade restauradas e JOGO SALVO!</color>"
                : $"<color=#88FFAA>Descansou na {placeName}! (+Vida, +Folego, +Sanidade) e JOGO SALVO COM SUCESSO!</color>");
        }
    }
}

/// <summary>
/// DryingRackInteractable: Seca carne crua (104/113 -> 212 Charque Salgado nao-perecivel) ou peixe cru (111 -> 207) estilo The Forest.
/// </summary>
public class DryingRackInteractable : MonoBehaviour
{
    public static readonly List<DryingRackInteractable> AllRacks = new List<DryingRackInteractable>();

    public const int MaxSlots = 3;
    public float dryDuration = 20f;

    private readonly int[] rawItemIds = new int[MaxSlots];
    private readonly float[] dryTimers = new float[MaxSlots];
    private readonly bool[] isDried = new bool[MaxSlots];
    private readonly MeshRenderer[] slotRenderers = new MeshRenderer[MaxSlots];
    private Material rawMeatMat;
    private Material driedMeatMat;

    public void RegisterInGlobalList() { if (!AllRacks.Contains(this)) AllRacks.Add(this); }
    public void UnregisterFromGlobalList() { AllRacks.Remove(this); }
    private void Awake() { RegisterInGlobalList(); }
    private void OnEnable() { RegisterInGlobalList(); }
    private void OnDisable() { UnregisterFromGlobalList(); }
    private void OnDestroy() { UnregisterFromGlobalList(); }

    public void BuildHangingSlots()
    {
        RegisterInGlobalList();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        rawMeatMat = new Material(lit);
        rawMeatMat.color = new Color(0.68f, 0.16f, 0.14f);
        driedMeatMat = new Material(lit);
        driedMeatMat.color = new Color(0.45f, 0.22f, 0.12f);

        for (int i = 0; i < MaxSlots; i++)
        {
            GameObject meatObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meatObj.name = $"HangingMeat_{i}";
            BlueprintPlacementManager.SafeDestroy(meatObj.GetComponent<Collider>());
            meatObj.transform.SetParent(transform, false);
            meatObj.transform.localPosition = new Vector3((i - 1) * 0.42f, 1.44f, 0f);
            meatObj.transform.localScale = new Vector3(0.14f, 0.34f, 0.08f);
            MeshRenderer mr = meatObj.GetComponent<MeshRenderer>();
            mr.sharedMaterial = rawMeatMat;
            meatObj.SetActive(false);
            slotRenderers[i] = mr;
        }
    }

    private void Update()
    {
        bool raining = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining;
        float speedMult = raining ? 0.5f : 1.0f;
        AdvanceDrying(Time.deltaTime * speedMult);
    }

    public void AdvanceDrying(float seconds)
    {
        if (seconds <= 0f) return;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (rawItemIds[i] <= 0 || isDried[i]) continue;
            dryTimers[i] += seconds;
            if (dryTimers[i] >= dryDuration)
            {
                isDried[i] = true;
                if (slotRenderers[i] != null && driedMeatMat != null)
                    slotRenderers[i].sharedMaterial = driedMeatMat;
            }
        }
    }

    public void ExportState(out int[] ids, out float[] timers, out bool[] dried)
    {
        ids = new int[MaxSlots];
        timers = new float[MaxSlots];
        dried = new bool[MaxSlots];
        for (int i = 0; i < MaxSlots; i++)
        {
            ids[i] = rawItemIds[i];
            timers[i] = dryTimers[i];
            dried[i] = isDried[i];
        }
    }

    public void ImportState(int[] ids, float[] timers, bool[] dried)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            rawItemIds[i] = (ids != null && i < ids.Length) ? ids[i] : 0;
            dryTimers[i] = (timers != null && i < timers.Length) ? timers[i] : 0f;
            isDried[i] = (dried != null && i < dried.Length) && dried[i];
            if (rawItemIds[i] > 0 && dryTimers[i] >= dryDuration) isDried[i] = true;

            if (slotRenderers[i] != null)
            {
                bool occupied = rawItemIds[i] > 0;
                slotRenderers[i].gameObject.SetActive(occupied);
                if (occupied)
                {
                    Material targetMat = isDried[i] ? driedMeatMat : rawMeatMat;
                    if (targetMat != null) slotRenderers[i].sharedMaterial = targetMat;
                }
            }
        }
    }

    public string GetPromptLabel(InventoryAdvanced inv)
    {
        int readyCount = 0;
        int dryingCount = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (rawItemIds[i] > 0)
            {
                if (isDried[i]) readyCount++;
                else dryingCount++;
            }
        }

        if (readyCount > 0) return $"Coletar Charque / Peixe Seco ({readyCount} pronto) [E]";
        bool hasRaw = inv != null && (inv.HasItem(104) || inv.HasItem(113) || inv.HasItem(111));
        if (dryingCount < MaxSlots && hasRaw) return $"Pendurar Carne/Peixe p/ Secar ({dryingCount}/{MaxSlots}) [E]";
        return dryingCount > 0 ? $"Secador: {dryingCount}/{MaxSlots} curando..." : "Secador Vazio (Requer Carne ou Peixe) [E]";
    }

    public void Interact(InventoryAdvanced inv)
    {
        if (inv == null) inv = InventoryAdvanced.Instance != null ? InventoryAdvanced.Instance : FindAnyObjectByType<InventoryAdvanced>();
        if (inv == null) return;

        int collected = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (rawItemIds[i] > 0 && isDried[i])
            {
                int outId = (rawItemIds[i] == 111) ? 207 : 212; // 212 = Charque Salgado (nao apodrece!)
                int leftover = inv.AddItem(outId, 1);
                if (leftover > 0 && LootManager.Instance != null)
                {
                    LootManager.Instance.DropItem(outId, leftover, transform.position + Vector3.up * 0.5f);
                }
                rawItemIds[i] = 0;
                dryTimers[i] = 0f;
                isDried[i] = false;
                if (slotRenderers[i] != null) slotRenderers[i].gameObject.SetActive(false);
                collected++;
            }
        }

        if (collected > 0)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupHerb, transform.position, 0.68f, 0.92f, 1.08f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{collected} Charque / Peixe Seco coletado!");
            return;
        }

        int emptySlot = -1;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (rawItemIds[i] <= 0) { emptySlot = i; break; }
        }

        if (emptySlot < 0)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Secador cheio - aguarde curar!");
            return;
        }

        int rawId = inv.HasItem(104) ? 104 : (inv.HasItem(111) ? 111 : (inv.HasItem(113) ? 113 : -1));
        if (rawId < 0)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Sem carne crua ou peixe para pendurar!");
            return;
        }

        inv.RemoveItem(rawId, 1);
        rawItemIds[emptySlot] = rawId;
        dryTimers[emptySlot] = 0f;
        isDried[emptySlot] = false;
        if (slotRenderers[emptySlot] != null)
        {
            slotRenderers[emptySlot].sharedMaterial = rawMeatMat;
            slotRenderers[emptySlot].gameObject.SetActive(true);
        }

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("Pendurado para secar! (~20s)");
    }
}

/// <summary>
/// RainCollectorInteractable: Acumula agua potavel da chuva/orvalho, entrega Agua Limpa (109) + reabastece Cantil (210).
/// </summary>
public class RainCollectorInteractable : MonoBehaviour
{
    public static readonly List<RainCollectorInteractable> AllCollectors = new List<RainCollectorInteractable>();

    public int maxCharges = 3;
    public int waterCharges = 1;
    private float fillTimer = 0f;
    private GameObject waterSurfaceObj;

    public void RegisterInGlobalList() { if (!AllCollectors.Contains(this)) AllCollectors.Add(this); }
    public void UnregisterFromGlobalList() { AllCollectors.Remove(this); }
    private void Awake() { RegisterInGlobalList(); }
    private void OnEnable() { RegisterInGlobalList(); }
    private void OnDisable() { UnregisterFromGlobalList(); }
    private void OnDestroy() { UnregisterFromGlobalList(); }

    public void BuildWaterVisual()
    {
        RegisterInGlobalList();
        waterSurfaceObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waterSurfaceObj.name = "CollectedWaterSurface";
        BlueprintPlacementManager.SafeDestroy(waterSurfaceObj.GetComponent<Collider>());
        waterSurfaceObj.transform.SetParent(transform, false);
        waterSurfaceObj.transform.localPosition = new Vector3(0f, 1.06f, 0f);
        waterSurfaceObj.transform.localScale = new Vector3(0.92f, 0.03f, 0.92f);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.18f, 0.62f, 0.88f, 0.90f);
        mat.SetFloat("_Smoothness", 0.9f);
        waterSurfaceObj.GetComponent<MeshRenderer>().sharedMaterial = mat;
        RefreshVisual();
    }

    private void Update()
    {
        bool raining = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining;
        AdvanceCollection(Time.deltaTime * (raining ? 3.0f : 1.0f));
    }

    public void AdvanceCollection(float seconds)
    {
        if (waterCharges >= maxCharges || seconds <= 0f) return;
        fillTimer += seconds;
        while (fillTimer >= 34f && waterCharges < maxCharges)
        {
            fillTimer -= 34f;
            waterCharges = Mathf.Min(maxCharges, waterCharges + 1);
        }
        RefreshVisual();
    }

    public void SetWaterCharges(int charges)
    {
        waterCharges = Mathf.Clamp(charges, 0, maxCharges);
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (waterSurfaceObj != null)
            waterSurfaceObj.SetActive(waterCharges > 0);
    }

    public string GetPromptLabel()
    {
        return waterCharges > 0
            ? $"Coletar Agua Limpa ({waterCharges}/{maxCharges}) [E]"
            : "Coletor Vazio (Aguardando Chuva/Orvalho) [E]";
    }

    public void Interact(InventoryAdvanced inv, PlayerStatus status)
    {
        if (waterCharges <= 0)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Coletor ainda sem agua acumulada.");
            return;
        }

        waterCharges--;
        RefreshVisual();

        bool refilledCanteen = false;
        if (inv != null)
        {
            int leftover = inv.AddItem(109, 1);
            if (leftover > 0 && LootManager.Instance != null)
                LootManager.Instance.DropItem(109, leftover, transform.position + Vector3.up * 0.5f);

            refilledCanteen = inv.TryRefillCanteen(out _);
        }

        if (status != null) status.thirst = Mathf.Min(100f, status.thirst + 20f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.DrinkGulp, transform.position, 0.72f, 0.94f, 1.08f);

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(refilledCanteen
                ? "+1 Agua Limpa, +20% Sede & Cantil 3/3!"
                : "+1 Agua Limpa e +20% Sede!");
        }
    }
}

/// <summary>
/// PalisadeSpikeHazard: Espinhos frontais da palicada ferem e repelem Vultos que se aproximam.
/// </summary>
public class PalisadeSpikeHazard : MonoBehaviour
{
    public StructureHealth structureHealth;
    private float nextTickTime = 0f;

    private void Update()
    {
        if (Time.time < nextTickTime) return;
        nextTickTime = Time.time + 0.85f;

        if (VultoManager.Instance != null)
        {
            if (VultoManager.Instance.TryDamageVultoAt(transform.position, 28f) && structureHealth != null)
            {
                structureHealth.ApplyDamage(3f);
            }
        }
    }
}

/// <summary>
/// StructureHealth: Integridade e reparo de estruturas construidas (Palicadas, Cabanas, Refugios, Secadores).
/// </summary>
public class StructureHealth : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<StructureHealth> AllStructures = new System.Collections.Generic.List<StructureHealth>();

    public string structureName = "Estrutura";
    public string structureId;
    public string ownerPlayerId = "P1-LOCAL";
    public string ownerPlayerName = "Sobrevivente";
    public bool isSharedWithGroup = true;
    public float maxHealth = 160f;
    public float currentHealth = 160f;
    private bool alertedDamaged = false;

    public bool IsDamaged => currentHealth < maxHealth - 1f;
    public float HealthRatio => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 1f;

    public string GetOwnershipBadge()
    {
        return isSharedWithGroup ? "[GRUPO]" : $"[PRIVADO: {(string.IsNullOrEmpty(ownerPlayerName) ? "P1" : ownerPlayerName)}]";
    }

    public void RegisterInGlobalList() { if (!AllStructures.Contains(this)) AllStructures.Add(this); }
    public void UnregisterFromGlobalList() { AllStructures.Remove(this); }
    private void Awake() { RegisterInGlobalList(); }
    private void OnEnable() { RegisterInGlobalList(); }
    private void OnDisable() { UnregisterFromGlobalList(); }
    private void OnDestroy() { UnregisterFromGlobalList(); }

    public void ApplyDamage(float dmg)
    {
        if (dmg <= 0f || currentHealth <= 0f) return;
        currentHealth -= dmg;

        if (!alertedDamaged && currentHealth < maxHealth * 0.65f)
        {
            alertedDamaged = true;
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF9944>{structureName} sob ataque! Conserte com Madeira.</color>");
        }

        if (currentHealth <= 0f)
        {
            if (VFXPoolManager.Instance != null)
                VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position + Vector3.up * 0.5f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF4444>{structureName} foi destruida!</color>");
            if (FragaCoopSession.Instance != null && !string.IsNullOrEmpty(structureId))
            {
                FragaCoopSession.Instance.BroadcastBuildEvent(new CoopBuildSyncEvent
                {
                    structureNetId = structureId,
                    ownerPlayerId = ownerPlayerId,
                    ownerName = ownerPlayerName,
                    structureName = structureName,
                    actionType = "Destroy",
                    position = transform.position,
                    rotationY = transform.eulerAngles.y,
                    isSharedWithGroup = isSharedWithGroup
                });
            }
            BlueprintPlacementManager.SafeDestroy(gameObject);
        }
    }

    public bool TryRepair(InventoryAdvanced inv)
    {
        if (!IsDamaged) return false;
        if (inv == null || !inv.HasItem(100))
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Precisa de 1x Madeira para consertar {structureName}.");
            return false;
        }

        inv.RemoveItem(100, 1);
        currentHealth = Mathf.Min(maxHealth, currentHealth + 65f);
        if (currentHealth >= maxHealth * 0.7f) alertedDamaged = false;

        if (BlueprintPlacementManager.Instance != null)
            BlueprintPlacementManager.Instance.PlayHammerSound(transform.position);
        if (VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position + Vector3.up * 0.5f);
        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Consertado: {structureName} ({Mathf.RoundToInt(HealthRatio * 100f)}%)");
        if (FragaCoopSession.Instance != null)
        {
            FragaCoopSession.Instance.BroadcastBuildEvent(new CoopBuildSyncEvent
            {
                structureNetId = structureId,
                ownerPlayerId = ownerPlayerId,
                ownerName = ownerPlayerName,
                structureName = structureName,
                actionType = "Repair",
                position = transform.position,
                rotationY = transform.eulerAngles.y,
                isComplete = true,
                isSharedWithGroup = isSharedWithGroup
            });
        }
        return true;
    }
}

/// <summary>
/// LogHolderInteractable: Cesto de Toras da base (armazena ate 12 toras com visual empilhado).
/// </summary>
public class LogHolderInteractable : MonoBehaviour
{
    public const int MaxLogs = 12;
    public int storedLogs = 0;
    private readonly GameObject[] logVisuals = new GameObject[MaxLogs];

    private void Awake()
    {
        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark")
            ?? Resources.Load<Material>("Materials/Wood_Bark");
        for (int i = 0; i < MaxLogs; i++)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = $"StoredLog_{i}";
            BlueprintPlacementManager.SafeDestroy(log.GetComponent<Collider>());
            log.transform.SetParent(transform, false);
            int row = i / 4;
            int col = i % 4;
            log.transform.localPosition = new Vector3(0f, 0.28f + row * 0.24f, -0.30f + col * 0.20f);
            log.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
            log.transform.localScale = new Vector3(0.18f, 0.68f, 0.18f);
            if (barkMat != null) log.GetComponent<MeshRenderer>().sharedMaterial = barkMat;
            log.SetActive(false);
            logVisuals[i] = log;
        }
    }

    public string GetPromptLabel()
    {
        bool carrying = LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs > 0;
        if (carrying && storedLogs < MaxLogs)
            return $"Guardar Tora no Cesto ({storedLogs}/{MaxLogs}) [E]";
        if (storedLogs > 0)
            return $"Pegar / Guardar Tora no Cesto ({storedLogs}/{MaxLogs}) [E]";
        return $"Guardar Madeira no Cesto de Toras (0/{MaxLogs}) [E]";
    }

    public void Interact(InventoryAdvanced inv)
    {
        var carry = LogCarryingSystem.Instance;
        if (carry != null && carry.currentLogs > 0 && storedLogs < MaxLogs && carry.ConsumeOneLog())
        {
            storedLogs++;
            RefreshVisuals();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupWood, transform.position, 0.72f, 0.90f, 1.08f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Tora guardada no Cesto ({storedLogs}/{MaxLogs})");
            return;
        }

        if (inv != null && storedLogs < MaxLogs && inv.HasItem(100))
        {
            inv.RemoveItem(100, 1);
            storedLogs++;
            RefreshVisuals();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupWood, transform.position, 0.72f, 0.90f, 1.08f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Madeira armazenada no Cesto ({storedLogs}/{MaxLogs})");
            return;
        }

        if (storedLogs > 0)
        {
            storedLogs--;
            if (inv != null && inv.AddItem(100, 1) == 0)
            {
                RefreshVisuals();
                if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.PickupWood, transform.position, 0.72f, 0.90f, 1.08f);
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"+1 Madeira retirada do Cesto ({storedLogs}/{MaxLogs})");
                return;
            }
            if (carry != null && carry.CanPickupLog)
            {
                carry.PickupLog();
                RefreshVisuals();
                if (HUDSurvivalOverlay.Instance != null)
                    HUDSurvivalOverlay.Instance.ShowLootTooltip($"Retirou Tora no ombro ({storedLogs}/{MaxLogs})");
                return;
            }
            storedLogs++;
        }
    }

    public void SetStoredLogs(int count)
    {
        storedLogs = Mathf.Clamp(count, 0, MaxLogs);
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        for (int i = 0; i < MaxLogs; i++)
        {
            if (logVisuals[i] != null) logVisuals[i].SetActive(i < storedLogs);
        }
    }
}

/// <summary>
/// StorageChestInteractable: Bau de Base com deposito/retirada de itens via modal ou interacao rapida.
/// </summary>
public class StorageChestInteractable : MonoBehaviour
{
    public static bool IsAnyChestOpen { get; private set; }

    [System.Serializable]
    public struct StoredStack
    {
        public int itemId;
        public int amount;
    }

    public const int MaxStacks = 16;
    public readonly System.Collections.Generic.List<StoredStack> stacks = new System.Collections.Generic.List<StoredStack>(MaxStacks);

    private static GameObject chestModalObj;
    private static Transform chestGridParent;
    private static Transform bagGridParent;
    private static RectTransform bagContentRT;
    private static RectTransform chestContentRT;
    private static Text chestTitleText;
    private static Text chestAccessBtnText;
    private static Image chestAccessBtnBg;
    private static StorageChestInteractable activeChest;

    private static readonly int[] ResourceItemIds = { 100, 101, 102, 103, 105, 106, 107, 118, 119, 120, 121, 122 };

    public string GetPromptLabel()
    {
        StructureHealth sh = GetComponent<StructureHealth>();
        bool coopActive = FragaCoopSession.Instance != null && FragaCoopSession.Instance.ShouldShowPartyHUD;
        string accessTag = (sh != null && !sh.isSharedWithGroup) ? " [PRIVADO]" : (coopActive ? " [GRUPO]" : string.Empty);
        return $"Abrir Bau de Base ({stacks.Count}/{MaxStacks} Pilhas){accessTag} [E]";
    }

    public bool CanLocalPlayerAccess()
    {
        StructureHealth sh = GetComponent<StructureHealth>();
        if (sh == null || FragaCoopSession.Instance == null) return true;
        return FragaCoopSession.Instance.CanLocalPlayerInteract(sh.ownerPlayerId, sh.isSharedWithGroup);
    }

    public bool ToggleCoopAccessLock()
    {
        StructureHealth sh = GetComponent<StructureHealth>();
        if (sh == null) return true;
        if (FragaCoopSession.Instance != null && !FragaCoopSession.Instance.CanLocalPlayerInteract(sh.ownerPlayerId, isSharedWithGroup: false) && !sh.isSharedWithGroup)
            return false;
        sh.isSharedWithGroup = !sh.isSharedWithGroup;
        RefreshModalRows();
        return true;
    }

    public void Interact()
    {
        if (!CanLocalPlayerAccess())
        {
            StructureHealth sh = GetComponent<StructureHealth>();
            string owner = sh != null && !string.IsNullOrEmpty(sh.ownerPlayerName) ? sh.ownerPlayerName : "outro jogador";
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF8866>Bau privado de {owner}! Acesso bloqueado.</color>");
            return;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.ChestWoodLatch, transform.position, 0.70f, 0.92f, 1.08f);
        activeChest = this;
        OpenModal();
    }

    private static void OpenModal()
    {
        EnsureModalUI();
        IsAnyChestOpen = true;
        if (chestModalObj != null)
        {
            chestModalObj.SetActive(true);
            chestModalObj.transform.SetAsLastSibling();
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshModalRows();
    }

    public static void CloseModal()
    {
        IsAnyChestOpen = false;
        if (chestModalObj != null) chestModalObj.SetActive(false);
        bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
        if (!touchActive)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (IsAnyChestOpen && activeChest == this && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseModal();
        }
    }

    private static void EnsureModalUI()
    {
        if (chestModalObj != null) return;
        Canvas canvas = HUDSurvivalOverlay.GetGameplayCanvas();
        if (canvas == null) return;

        chestModalObj = new GameObject("BaseStorageChestModal");
        chestModalObj.transform.SetParent(canvas.transform, false);
        RectTransform rootRT = chestModalObj.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dim = chestModalObj.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.04f, 0.06f, 0.72f);

        GameObject win = new GameObject("Window");
        win.transform.SetParent(chestModalObj.transform, false);
        RectTransform wRT = win.AddComponent<RectTransform>();
        wRT.anchorMin = new Vector2(0.5f, 0.5f);
        wRT.anchorMax = new Vector2(0.5f, 0.5f);
        wRT.sizeDelta = new Vector2(780f, 520f);
        Image wImg = win.AddComponent<Image>();
        wImg.sprite = FragaProIconLibrary.GetPillSprite();
        wImg.type = Image.Type.Sliced;
        wImg.color = new Color(0.07f, 0.10f, 0.15f, 0.97f);

        Outline wOut = win.AddComponent<Outline>();
        wOut.effectColor = new Color(0.32f, 0.78f, 0.94f, 0.52f);
        wOut.effectDistance = new Vector2(1.5f, -1.5f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        chestTitleText = CreateLabel(win.transform, "Title", "BAU DE BASE (0/16 PILHAS)", font, 16,
            new Vector2(-90f, 226f), new Vector2(520f, 30f), new Color(0.95f, 0.85f, 0.45f));

        GameObject accessBtn = new GameObject("Btn_ToggleChestAccess");
        accessBtn.transform.SetParent(win.transform, false);
        RectTransform aRT = accessBtn.AddComponent<RectTransform>();
        aRT.anchoredPosition = new Vector2(266f, 226f);
        aRT.sizeDelta = new Vector2(196f, 30f);
        chestAccessBtnBg = accessBtn.AddComponent<Image>();
        chestAccessBtnBg.sprite = FragaProIconLibrary.GetPillSprite();
        chestAccessBtnBg.type = Image.Type.Sliced;
        chestAccessBtnBg.color = new Color(0.14f, 0.44f, 0.36f, 0.95f);
        accessBtn.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (activeChest != null) activeChest.ToggleCoopAccessLock();
        });
        chestAccessBtnText = CreateLabel(accessBtn.transform, "Lbl", "ACESSO: GRUPO", font, 11, Vector2.zero, new Vector2(184f, 26f), Color.white);

        CreateLabel(win.transform, "BagHdr", "MOCHILA (CLIQUE P/ GUARDAR 1x)", font, 13,
            new Vector2(-185f, 184f), new Vector2(340f, 24f), new Color(0.75f, 0.92f, 1f));
        CreateLabel(win.transform, "ChestHdr", "NO BAU (CLIQUE P/ RETIRAR 1x)", font, 13,
            new Vector2(185f, 184f), new Vector2(340f, 24f), new Color(0.65f, 1f, 0.78f));

        bagGridParent = CreateScrollColumn(win.transform, "BagCol", new Vector2(-185f, -14f), new Vector2(346f, 348f), out bagContentRT);
        chestGridParent = CreateScrollColumn(win.transform, "ChestCol", new Vector2(185f, -14f), new Vector2(346f, 348f), out chestContentRT);

        GameObject storeResBtn = new GameObject("Btn_StoreResources");
        storeResBtn.transform.SetParent(win.transform, false);
        RectTransform srRT = storeResBtn.AddComponent<RectTransform>();
        srRT.anchoredPosition = new Vector2(-150f, -220f);
        srRT.sizeDelta = new Vector2(236f, 36f);
        Image srImg = storeResBtn.AddComponent<Image>();
        srImg.sprite = FragaProIconLibrary.GetPillSprite();
        srImg.type = Image.Type.Sliced;
        srImg.color = new Color(0.16f, 0.42f, 0.28f, 0.96f);
        storeResBtn.AddComponent<Button>().onClick.AddListener(() =>
        {
            InventoryAdvanced inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
            if (activeChest != null && inv != null)
            {
                int moved = activeChest.DepositAllResourcesFromBag(inv);
                if (HUDSurvivalOverlay.Instance != null)
                {
                    HUDSurvivalOverlay.Instance.ShowLootTooltip(moved > 0
                        ? $"Guardou {moved} recursos no Bau de Base!"
                        : "Sem espaco no Bau ou sem recursos na mochila.");
                }
            }
        });
        CreateLabel(storeResBtn.transform, "Lbl", "GUARDAR RECURSOS", font, 12, Vector2.zero, new Vector2(220f, 30f), Color.white);

        GameObject closeBtn = new GameObject("Btn_Close");
        closeBtn.transform.SetParent(win.transform, false);
        RectTransform cRT = closeBtn.AddComponent<RectTransform>();
        cRT.anchoredPosition = new Vector2(150f, -220f);
        cRT.sizeDelta = new Vector2(200f, 36f);
        Image cImg = closeBtn.AddComponent<Image>();
        cImg.sprite = FragaProIconLibrary.GetPillSprite();
        cImg.type = Image.Type.Sliced;
        cImg.color = new Color(0.56f, 0.18f, 0.18f, 0.95f);
        closeBtn.AddComponent<Button>().onClick.AddListener(CloseModal);
        CreateLabel(closeBtn.transform, "Lbl", "FECHAR [ESC]", font, 13, Vector2.zero, new Vector2(180f, 28f), Color.white);
    }

    private static Transform CreateScrollColumn(Transform parent, string name, Vector2 pos, Vector2 size, out RectTransform contentRT)
    {
        GameObject col = new GameObject(name);
        col.transform.SetParent(parent, false);
        RectTransform rt = col.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image bg = col.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.09f, 0.88f);
        col.AddComponent<RectMask2D>();

        ScrollRect sr = col.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 32f;
        sr.viewport = rt;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(col.transform, false);
        contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, size.y);
        sr.content = contentRT;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content.transform;
    }

    private static Text CreateLabel(Transform parent, string name, string text, Font font, int size, Vector2 pos, Vector2 box, Color col, TextAnchor align = TextAnchor.MiddleCenter)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        Text t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = col;
        t.text = text;
        t.raycastTarget = false;
        return t;
    }

    private static void RefreshModalRows()
    {
        if (activeChest == null || bagGridParent == null || chestGridParent == null) return;
        for (int i = bagGridParent.childCount - 1; i >= 0; i--) BlueprintPlacementManager.SafeDestroy(bagGridParent.GetChild(i).gameObject);
        for (int i = chestGridParent.childCount - 1; i >= 0; i--) BlueprintPlacementManager.SafeDestroy(chestGridParent.GetChild(i).gameObject);

        StructureHealth sh = activeChest.GetComponent<StructureHealth>();
        bool isShared = sh == null || sh.isSharedWithGroup;
        if (chestTitleText != null)
            chestTitleText.text = $"BAU DE BASE ({activeChest.stacks.Count}/{MaxStacks} PILHAS)";
        if (chestAccessBtnText != null)
            chestAccessBtnText.text = isShared ? "ACESSO: GRUPO CO-OP" : "ACESSO: PRIVADO";
        if (chestAccessBtnBg != null)
            chestAccessBtnBg.color = isShared ? new Color(0.14f, 0.44f, 0.36f, 0.95f) : new Color(0.50f, 0.28f, 0.14f, 0.95f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        InventoryAdvanced inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null && inv.slots != null)
        {
            for (int i = 0; i < inv.slots.Length; i++)
            {
                var sl = inv.slots[i];
                if (sl.IsEmpty()) continue;
                ItemData d = ItemDatabase.GetItem(sl.itemId);
                if (d == null) continue;
                int itemId = sl.itemId;
                int slotIdx = i;
                CreateRowButton(bagGridParent, itemId, $"{d.itemName} (x{sl.quantity})", font, new Color(0.14f, 0.25f, 0.35f, 0.92f), () =>
                {
                    activeChest.DepositOneFromSlot(inv, slotIdx, itemId);
                });
            }
        }

        for (int i = 0; i < activeChest.stacks.Count; i++)
        {
            int idx = i;
            var st = activeChest.stacks[i];
            ItemData d = ItemDatabase.GetItem(st.itemId);
            string label = d != null ? $"{d.itemName} (x{st.amount})" : $"Item {st.itemId} (x{st.amount})";
            CreateRowButton(chestGridParent, st.itemId, label, font, new Color(0.18f, 0.38f, 0.26f, 0.92f), () =>
            {
                if (inv != null) activeChest.WithdrawOneToBag(inv, idx);
            });
        }

        if (bagContentRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(bagContentRT);
        if (chestContentRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(chestContentRT);
    }

    private static void CreateRowButton(Transform parent, int itemId, string label, Font font, Color bgCol, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject("RowBtn");
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(326f, 32f);
        Image img = btnObj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetPillSprite();
        img.type = Image.Type.Sliced;
        img.color = bgCol;
        btnObj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(btnObj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(8f, 0f);
        iRT.sizeDelta = new Vector2(22f, 22f);
        Image icoImg = icoObj.AddComponent<Image>();
        icoImg.sprite = FragaProIconLibrary.GetItemIcon(itemId);
        icoImg.preserveAspect = true;
        icoImg.raycastTarget = false;

        CreateLabel(btnObj.transform, "Txt", label, font, 12, new Vector2(14f, 0f), new Vector2(282f, 26f), Color.white, TextAnchor.MiddleLeft);
    }

    public int DepositAllResourcesFromBag(InventoryAdvanced inv)
    {
        if (inv == null || inv.slots == null) return 0;
        int totalMoved = 0;
        for (int r = 0; r < ResourceItemIds.Length; r++)
        {
            int itemId = ResourceItemIds[r];
            int avail = inv.GetItemCount(itemId);
            if (avail <= 0) continue;

            int stackIdx = -1;
            for (int s = 0; s < stacks.Count; s++)
            {
                if (stacks[s].itemId == itemId) { stackIdx = s; break; }
            }
            if (stackIdx < 0 && stacks.Count >= MaxStacks) continue;

            if (!inv.RemoveItem(itemId, avail)) continue;
            if (stackIdx >= 0)
            {
                var st = stacks[stackIdx];
                st.amount += avail;
                stacks[stackIdx] = st;
            }
            else
            {
                stacks.Add(new StoredStack { itemId = itemId, amount = avail });
            }
            totalMoved += avail;
        }
        if (totalMoved > 0) RefreshModalRows();
        return totalMoved;
    }

    public void ImportStacks(List<SavedChestStackData> savedStacks)
    {
        stacks.Clear();
        if (savedStacks == null) return;
        for (int i = 0; i < savedStacks.Count && stacks.Count < MaxStacks; i++)
        {
            SavedChestStackData s = savedStacks[i];
            if (s != null && s.itemId > 0 && s.amount > 0)
            {
                stacks.Add(new StoredStack { itemId = s.itemId, amount = s.amount });
            }
        }
    }

    private void DepositOneFromSlot(InventoryAdvanced inv, int slotIdx, int itemId)
    {
        if (stacks.Count >= MaxStacks)
        {
            bool hasExisting = false;
            for (int i = 0; i < stacks.Count; i++) if (stacks[i].itemId == itemId) { hasExisting = true; break; }
            if (!hasExisting) return;
        }
        if (!inv.RemoveItem(itemId, 1)) return;

        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].itemId == itemId)
            {
                var st = stacks[i];
                st.amount++;
                stacks[i] = st;
                RefreshModalRows();
                return;
            }
        }
        stacks.Add(new StoredStack { itemId = itemId, amount = 1 });
        RefreshModalRows();
    }

    private void WithdrawOneToBag(InventoryAdvanced inv, int stackIdx)
    {
        if (stackIdx < 0 || stackIdx >= stacks.Count) return;
        var st = stacks[stackIdx];
        if (inv.AddItem(st.itemId, 1) > 0) return;
        st.amount--;
        if (st.amount <= 0) stacks.RemoveAt(stackIdx);
        else stacks[stackIdx] = st;
        RefreshModalRows();
    }
}
