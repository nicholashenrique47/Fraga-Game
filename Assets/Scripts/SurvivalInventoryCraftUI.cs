using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// SurvivalInventoryCraftUI: Interface Completa de Inventário e Crafting (Padrão Sons of the Forest / The Forest).
/// Suporta:
/// - Abertura por botão mobile (🎒 Mochila) e teclas Tab/I no PC
/// - Aba 1: Mochila de Sobrevivência com visualização dos 20 slots, quantidade, ícones e botão de consumo/uso
/// - Aba 2: Esteira de Fabricação (Crafting Mat) com receitas de sobrevivência (Machado, Tochas, Fogueira, Ligaduras)
/// - Validação em tempo real de ingredientes com destaque verde (disponível) e vermelho (em falta)
/// - Pausa e restauração automática do toque de câmera para navegação fluida
/// - Configuração HD com fontes nítidas e sombras de alto contraste.
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

    // Componentes da UI
    private GameObject rootPanel;
    private GameObject inventoryTabObj;
    private GameObject craftingTabObj;
    private Text tabInvText;
    private Text tabCraftText;

    // Painel de Detalhes do Item Selecionado
    private GameObject detailPane;
    private Text detailTitle;
    private Text detailDesc;
    private Text detailStats;
    private Button detailUseButton;
    private Text detailUseBtnText;
    private int selectedSlotIndex = -1;

    // Slots visuais da mochila
    private List<GameObject> slotObjects = new List<GameObject>();

    // Lista de receitas visuais
    private List<GameObject> recipeCards = new List<GameObject>();

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
        }
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= RefreshUI;
        }
    }

    private void FindDependencies()
    {
        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
        if (craftingSystem == null) craftingSystem = FindAnyObjectByType<CraftingSystem>();
        if (playerStatus == null) playerStatus = FindAnyObjectByType<PlayerStatus>();
        if (cameraTouchZone == null) cameraTouchZone = FindAnyObjectByType<CameraTouchController>();
    }

    private void Update()
    {
        // Tecla Tab ou I no PC para alternar inventário
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            ToggleMenu();
        }

        // Tecla ESC para fechar se estiver aberto
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
        }
    }

    public void ToggleMenu()
    {
        if (isOpen) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        isOpen = true;
        rootPanel.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        // Pausa rotação da câmera enquanto o inventário estiver aberto
        if (cameraTouchZone != null) cameraTouchZone.enabled = false;

        // Libera cursor se estiver no PC
        if (Input.touchCount == 0 && (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        RefreshUI();
    }

    public void CloseMenu()
    {
        isOpen = false;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        rootPanel.SetActive(false);

        // Restaura rotação da câmera
        if (cameraTouchZone != null) cameraTouchZone.enabled = true;

        // Trava cursor no PC se estiver jogando
        if (Input.touchCount == 0 && (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

    // =================== CONSTRUÇÃO PROCEDURAL DA INTERFACE ===================

    private void BuildUIHierarchy()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) parentCanvas = FindAnyObjectByType<Canvas>();

        if (parentCanvas != null)
        {
            CanvasScaler scaler = parentCanvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = parentCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 2.5f;
        }

        // Painel Raiz (Cobre tela cheia com fundo semitransparente)
        rootPanel = new GameObject("SurvivalInventory_Root");
        rootPanel.transform.SetParent(parentCanvas.transform, false);
        RectTransform rootRT = rootPanel.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image bgDark = rootPanel.AddComponent<Image>();
        bgDark.color = new Color(0.04f, 0.07f, 0.10f, 0.90f);
        bgDark.raycastTarget = true; // Bloqueia cliques para não atravessar para o jogo

        canvasGroup = rootPanel.AddComponent<CanvasGroup>();

        // Container Central da Mochila (1100x720)
        GameObject windowObj = new GameObject("Window_Mat");
        windowObj.transform.SetParent(rootPanel.transform, false);
        RectTransform winRT = windowObj.AddComponent<RectTransform>();
        winRT.anchorMin = new Vector2(0.5f, 0.5f);
        winRT.anchorMax = new Vector2(0.5f, 0.5f);
        winRT.pivot = new Vector2(0.5f, 0.5f);
        winRT.sizeDelta = new Vector2(1060f, 680f);

        Image winBg = windowObj.AddComponent<Image>();
        winBg.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
        winBg.raycastTarget = true;

        Outline winOutline = windowObj.AddComponent<Outline>();
        winOutline.effectColor = new Color(0.25f, 0.75f, 0.85f, 0.45f);
        winOutline.effectDistance = new Vector2(2f, -2f);

        // --- CABEÇALHO COM ABAS E BOTÃO FECHAR ---
        BuildHeader(windowObj.transform);

        // --- ABA 1: MOCHILA / SLOTS ---
        inventoryTabObj = new GameObject("Tab_Inventory");
        inventoryTabObj.transform.SetParent(windowObj.transform, false);
        RectTransform invRT = inventoryTabObj.AddComponent<RectTransform>();
        invRT.anchorMin = new Vector2(0f, 0f);
        invRT.anchorMax = new Vector2(1f, 1f);
        invRT.offsetMin = new Vector2(20f, 20f);
        invRT.offsetMax = new Vector2(-20f, -80f);

        BuildInventoryGrid(inventoryTabObj.transform);
        BuildDetailPane(inventoryTabObj.transform);

        // --- ABA 2: FABRICAÇÃO / CRAFTING ---
        craftingTabObj = new GameObject("Tab_Crafting");
        craftingTabObj.transform.SetParent(windowObj.transform, false);
        RectTransform craftRT = craftingTabObj.AddComponent<RectTransform>();
        craftRT.anchorMin = new Vector2(0f, 0f);
        craftRT.anchorMax = new Vector2(1f, 1f);
        craftRT.offsetMin = new Vector2(20f, 20f);
        craftRT.offsetMax = new Vector2(-20f, -80f);

        BuildCraftingList(craftingTabObj.transform);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(parent, false);
        RectTransform hRT = headerObj.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = new Vector2(0f, -10f);
        hRT.sizeDelta = new Vector2(-40f, 60f);

        // Aba Mochila
        GameObject tabInv = new GameObject("Btn_TabInv");
        tabInv.transform.SetParent(headerObj.transform, false);
        RectTransform tInvRT = tabInv.AddComponent<RectTransform>();
        tInvRT.anchorMin = new Vector2(0f, 0.5f);
        tInvRT.anchorMax = new Vector2(0f, 0.5f);
        tInvRT.pivot = new Vector2(0f, 0.5f);
        tInvRT.anchoredPosition = new Vector2(10f, 0f);
        tInvRT.sizeDelta = new Vector2(220f, 48f);

        Image tInvImg = tabInv.AddComponent<Image>();
        tInvImg.color = new Color(0.12f, 0.22f, 0.30f, 0.85f);
        Button tInvBtn = tabInv.AddComponent<Button>();
        tInvBtn.onClick.AddListener(() => SwitchToTab(0));

        tabInvText = CreateText(tabInv.transform, "🎒 MOCHILA (20)", 16, FontStyle.Bold, Color.white);

        // Aba Crafting
        GameObject tabCraft = new GameObject("Btn_TabCraft");
        tabCraft.transform.SetParent(headerObj.transform, false);
        RectTransform tCraftRT = tabCraft.AddComponent<RectTransform>();
        tCraftRT.anchorMin = new Vector2(0f, 0.5f);
        tCraftRT.anchorMax = new Vector2(0f, 0.5f);
        tCraftRT.pivot = new Vector2(0f, 0.5f);
        tCraftRT.anchoredPosition = new Vector2(245f, 0f);
        tCraftRT.sizeDelta = new Vector2(240f, 48f);

        Image tCraftImg = tabCraft.AddComponent<Image>();
        tCraftImg.color = new Color(0.10f, 0.14f, 0.18f, 0.65f);
        Button tCraftBtn = tabCraft.AddComponent<Button>();
        tCraftBtn.onClick.AddListener(() => SwitchToTab(1));

        tabCraftText = CreateText(tabCraft.transform, "⚒️ FABRICAÇÃO", 16, FontStyle.Bold, new Color(0.7f, 0.75f, 0.8f));

        // Botão Fechar [X]
        GameObject closeBtnObj = new GameObject("Btn_Close");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        RectTransform closeRT = closeBtnObj.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0.5f);
        closeRT.anchorMax = new Vector2(1f, 0.5f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.anchoredPosition = new Vector2(-10f, 0f);
        closeRT.sizeDelta = new Vector2(50f, 50f);

        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = new Color(0.65f, 0.15f, 0.15f, 0.85f);
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseMenu);

        CreateText(closeBtnObj.transform, "✕", 22, FontStyle.Bold, Color.white);
    }

    private void SwitchToTab(int tabIndex)
    {
        if (inventoryTabObj != null) inventoryTabObj.SetActive(tabIndex == 0);
        if (craftingTabObj != null) craftingTabObj.SetActive(tabIndex == 1);

        if (tabInvText != null) tabInvText.color = (tabIndex == 0) ? Color.white : new Color(0.6f, 0.65f, 0.7f);
        if (tabCraftText != null) tabCraftText.color = (tabIndex == 1) ? Color.white : new Color(0.6f, 0.65f, 0.7f);

        RefreshUI();
    }

    // =================== GRID DE SLOTS DE ITENS (ABA 1) ===================

    private void BuildInventoryGrid(Transform parent)
    {
        GameObject gridContainer = new GameObject("SlotsGrid");
        gridContainer.transform.SetParent(parent, false);
        RectTransform gRT = gridContainer.AddComponent<RectTransform>();
        gRT.anchorMin = new Vector2(0f, 0f);
        gRT.anchorMax = new Vector2(0.65f, 1f);
        gRT.offsetMin = Vector2.zero;
        gRT.offsetMax = Vector2.zero;

        slotObjects.Clear();
        int cols = 5;
        float slotSize = 110f;
        float spacingX = 14f;
        float spacingY = 14f;

        for (int i = 0; i < 20; i++)
        {
            int r = i / cols;
            int c = i % cols;
            float x = c * (slotSize + spacingX);
            float y = -r * (slotSize + spacingY);

            GameObject slot = new GameObject($"Slot_{i}");
            slot.transform.SetParent(gridContainer.transform, false);
            RectTransform sRT = slot.AddComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 1f);
            sRT.anchorMax = new Vector2(0f, 1f);
            sRT.pivot = new Vector2(0f, 1f);
            sRT.anchoredPosition = new Vector2(x, y);
            sRT.sizeDelta = new Vector2(slotSize, slotSize);

            Image slotBg = slot.AddComponent<Image>();
            slotBg.color = new Color(0.12f, 0.16f, 0.20f, 0.85f);
            Button slotBtn = slot.AddComponent<Button>();
            int slotIdx = i;
            slotBtn.onClick.AddListener(() => SelectSlot(slotIdx));

            // Ícone do Item (Emoji / Letra)
            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(slot.transform, false);
            RectTransform icoRT = icoObj.AddComponent<RectTransform>();
            icoRT.anchorMin = new Vector2(0.5f, 0.55f);
            icoRT.anchorMax = new Vector2(0.5f, 0.55f);
            icoRT.sizeDelta = new Vector2(65f, 45f);
            Text icoTxt = icoObj.AddComponent<Text>();
            icoTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            icoTxt.fontSize = 28;
            icoTxt.alignment = TextAnchor.MiddleCenter;
            icoTxt.color = Color.white;
            icoTxt.raycastTarget = false;

            // Nome abreviado
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(slot.transform, false);
            RectTransform nRT = nameObj.AddComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0f, 0f);
            nRT.anchorMax = new Vector2(1f, 0.32f);
            nRT.offsetMin = new Vector2(4f, 2f);
            nRT.offsetMax = new Vector2(-4f, 0f);
            Text nameTxt = nameObj.AddComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 12;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = new Color(0.85f, 0.90f, 0.95f, 0.95f);
            nameTxt.raycastTarget = false;
            Shadow nShadow = nameObj.AddComponent<Shadow>();
            nShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            nShadow.effectDistance = new Vector2(1f, -1f);

            // Quantidade badge no canto superior direito
            GameObject badgeObj = new GameObject("Quantity");
            badgeObj.transform.SetParent(slot.transform, false);
            RectTransform bRT = badgeObj.AddComponent<RectTransform>();
            bRT.anchorMin = new Vector2(1f, 1f);
            bRT.anchorMax = new Vector2(1f, 1f);
            bRT.pivot = new Vector2(1f, 1f);
            bRT.anchoredPosition = new Vector2(-4f, -4f);
            bRT.sizeDelta = new Vector2(40f, 20f);
            Text bTxt = badgeObj.AddComponent<Text>();
            bTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bTxt.fontSize = 13;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.alignment = TextAnchor.MiddleRight;
            bTxt.color = new Color(0.35f, 0.95f, 0.95f, 1.0f);
            bTxt.raycastTarget = false;
            Shadow bShadow = badgeObj.AddComponent<Shadow>();
            bShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            bShadow.effectDistance = new Vector2(1f, -1f);

            slotObjects.Add(slot);
        }
    }

    private void BuildDetailPane(Transform parent)
    {
        detailPane = new GameObject("DetailPane");
        detailPane.transform.SetParent(parent, false);
        RectTransform dRT = detailPane.AddComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0.68f, 0f);
        dRT.anchorMax = new Vector2(1f, 1f);
        dRT.offsetMin = Vector2.zero;
        dRT.offsetMax = Vector2.zero;

        Image dImg = detailPane.AddComponent<Image>();
        dImg.color = new Color(0.10f, 0.14f, 0.18f, 0.95f);
        Outline dOutline = detailPane.AddComponent<Outline>();
        dOutline.effectColor = new Color(0.2f, 0.6f, 0.8f, 0.35f);

        // Título do Item
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(detailPane.transform, false);
        RectTransform tRT = tObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 1f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.pivot = new Vector2(0f, 1f);
        tRT.anchoredPosition = new Vector2(16f, -16f);
        tRT.sizeDelta = new Vector2(-32f, 40f);
        detailTitle = tObj.AddComponent<Text>();
        detailTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailTitle.fontSize = 20;
        detailTitle.fontStyle = FontStyle.Bold;
        detailTitle.color = Color.white;
        Shadow tShadow = tObj.AddComponent<Shadow>();
        tShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        tShadow.effectDistance = new Vector2(1.2f, -1.2f);

        // Descrição
        GameObject descObj = new GameObject("Desc");
        descObj.transform.SetParent(detailPane.transform, false);
        RectTransform descRT = descObj.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0.5f);
        descRT.anchorMax = new Vector2(1f, 1f);
        descRT.offsetMin = new Vector2(16f, 0f);
        descRT.offsetMax = new Vector2(-16f, -70f);
        detailDesc = descObj.AddComponent<Text>();
        detailDesc.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailDesc.fontSize = 14;
        detailDesc.color = new Color(0.85f, 0.9f, 0.95f);
        Shadow descShadow = descObj.AddComponent<Shadow>();
        descShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        descShadow.effectDistance = new Vector2(1f, -1f);

        // Stats restaurados
        GameObject statsObj = new GameObject("Stats");
        statsObj.transform.SetParent(detailPane.transform, false);
        RectTransform sRT = statsObj.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0.18f);
        sRT.anchorMax = new Vector2(1f, 0.5f);
        sRT.offsetMin = new Vector2(16f, 0f);
        sRT.offsetMax = new Vector2(-16f, 0f);
        detailStats = statsObj.AddComponent<Text>();
        detailStats.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        detailStats.fontSize = 14;
        detailStats.color = new Color(0.4f, 0.95f, 0.55f);
        Shadow statsShadow = statsObj.AddComponent<Shadow>();
        statsShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        statsShadow.effectDistance = new Vector2(1f, -1f);

        // Botão USAR / CONSUMIR
        GameObject useBtnObj = new GameObject("Btn_Use");
        useBtnObj.transform.SetParent(detailPane.transform, false);
        RectTransform uRT = useBtnObj.AddComponent<RectTransform>();
        uRT.anchorMin = new Vector2(0.5f, 0f);
        uRT.anchorMax = new Vector2(0.5f, 0f);
        uRT.pivot = new Vector2(0.5f, 0f);
        uRT.anchoredPosition = new Vector2(0f, 20f);
        uRT.sizeDelta = new Vector2(220f, 48f);

        Image uImg = useBtnObj.AddComponent<Image>();
        uImg.color = new Color(0.18f, 0.65f, 0.40f, 0.95f);
        detailUseButton = useBtnObj.AddComponent<Button>();
        detailUseButton.onClick.AddListener(OnUseSelectedItem);

        detailUseBtnText = CreateText(useBtnObj.transform, "USAR ITEM", 16, FontStyle.Bold, Color.white);

        detailPane.SetActive(false);
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
        detailTitle.text = data.itemName;
        detailDesc.text = data.description;

        string statsStr = "";
        if (data.hungerRestore > 0) statsStr += $"🍖 +{data.hungerRestore} Fome  ";
        if (data.thirstRestore > 0) statsStr += $"💧 +{data.thirstRestore} Sede  ";
        if (data.healthRestore > 0) statsStr += $"♥ +{data.healthRestore} Vida  ";
        if (data.sanityRestore > 0) statsStr += $"◉ +{data.sanityRestore} Sanidade";
        detailStats.text = statsStr;

        bool canUse = data.type == ItemType.Consumable || data.type == ItemType.Deployable;
        detailUseButton.interactable = canUse;
        detailUseBtnText.text = canUse ? (data.type == ItemType.Consumable ? "CONSUMIR" : "UTILIZAR") : "EQUIPADO / PASSIVO";
    }

    private void OnUseSelectedItem()
    {
        if (inventory == null || selectedSlotIndex < 0) return;
        inventory.UseItem(selectedSlotIndex);
        RefreshUI();
        SelectSlot(selectedSlotIndex);
    }

    // =================== LISTA DE CRAFTING (ABA 2) ===================

    private void BuildCraftingList(Transform parent)
    {
        // Container scrollável de receitas
        GameObject listObj = new GameObject("RecipeList");
        listObj.transform.SetParent(parent, false);
        RectTransform lRT = listObj.AddComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero;
        lRT.anchorMax = Vector2.one;
        lRT.offsetMin = Vector2.zero;
        lRT.offsetMax = Vector2.zero;

        // Lista de receitas padrão
        RegisterCraftCards(listObj.transform);
    }

    private void RegisterCraftCards(Transform parent)
    {
        recipeCards.Clear();

        // 9 Receitas Essenciais de Sobrevivência
        AddRecipeCard(parent, "Machado Rudimentar", 300, "🪓", new int[] { 100, 101 }, new int[] { 5, 3 }, 0);
        AddRecipeCard(parent, "Tocha Selvagem", 303, "🔦", new int[] { 100, 102 }, new int[] { 2, 1 }, 1);
        AddRecipeCard(parent, "Kit de Fogueira", 400, "🔥", new int[] { 100, 102 }, new int[] { 5, 2 }, 2);
        AddRecipeCard(parent, "Picareta de Pedra", 301, "⛏️", new int[] { 100, 101 }, new int[] { 3, 5 }, 3);
        AddRecipeCard(parent, "Faca de Caça", 302, "🗡️", new int[] { 101, 107 }, new int[] { 2, 3 }, 4);
        AddRecipeCard(parent, "Armadilha de Caça", 401, "🕸️", new int[] { 100, 107 }, new int[] { 3, 2 }, 5);
        AddRecipeCard(parent, "Círculo de Sal", 402, "🧂", new int[] { 106 }, new int[] { 3 }, 6);
        AddRecipeCard(parent, "Ligaduras Médicas", 206, "🩹", new int[] { 103 }, new int[] { 3 }, 7);
        AddRecipeCard(parent, "Tala de Madeira", 209, "🪵", new int[] { 100 }, new int[] { 4 }, 8);
    }

    private void AddRecipeCard(Transform parent, string title, int outputItemId, string icon, int[] ingIds, int[] ingAmounts, int index)
    {
        int col = index % 3;
        int row = index / 3;
        float cardW = 320f;
        float cardH = 160f;
        float spacingX = 20f;
        float spacingY = 18f;

        GameObject card = new GameObject($"Recipe_{outputItemId}");
        card.transform.SetParent(parent, false);
        RectTransform rt = card.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(col * (cardW + spacingX), -row * (cardH + spacingY));
        rt.sizeDelta = new Vector2(cardW, cardH);

        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.16f, 0.22f, 0.90f);
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.5f, 0.7f, 0.35f);

        // Ícone
        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(card.transform, false);
        RectTransform icoRT = icoObj.AddComponent<RectTransform>();
        icoRT.anchorMin = new Vector2(0f, 1f);
        icoRT.anchorMax = new Vector2(0f, 1f);
        icoRT.pivot = new Vector2(0f, 1f);
        icoRT.anchoredPosition = new Vector2(10f, -10f);
        icoRT.sizeDelta = new Vector2(40f, 40f);
        Text icoTxt = icoObj.AddComponent<Text>();
        icoTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        icoTxt.fontSize = 24;
        icoTxt.text = icon;
        icoTxt.alignment = TextAnchor.MiddleCenter;

        // Título
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(card.transform, false);
        RectTransform tRT = tObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 1f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.pivot = new Vector2(0f, 1f);
        tRT.anchoredPosition = new Vector2(55f, -10f);
        tRT.sizeDelta = new Vector2(-65f, 30f);
        Text tTxt = tObj.AddComponent<Text>();
        tTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tTxt.fontSize = 15;
        tTxt.fontStyle = FontStyle.Bold;
        tTxt.text = title;
        tTxt.color = Color.white;
        Shadow tShadow = tObj.AddComponent<Shadow>();
        tShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        tShadow.effectDistance = new Vector2(1f, -1f);

        // Requisitos de Ingredientes
        GameObject reqObj = new GameObject("Ingredients");
        reqObj.transform.SetParent(card.transform, false);
        RectTransform reqRT = reqObj.AddComponent<RectTransform>();
        reqRT.anchorMin = new Vector2(0f, 0f);
        reqRT.anchorMax = new Vector2(1f, 1f);
        reqRT.offsetMin = new Vector2(12f, 45f);
        reqRT.offsetMax = new Vector2(-12f, -45f);
        Text reqTxt = reqObj.AddComponent<Text>();
        reqTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        reqTxt.fontSize = 12;
        reqTxt.color = new Color(0.8f, 0.85f, 0.9f);
        Shadow reqShadow = reqObj.AddComponent<Shadow>();
        reqShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        reqShadow.effectDistance = new Vector2(1f, -1f);

        // Botão Fabricar
        GameObject btnObj = new GameObject("Btn_Craft");
        btnObj.transform.SetParent(card.transform, false);
        RectTransform bRT = btnObj.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 0f);
        bRT.anchorMax = new Vector2(1f, 0f);
        bRT.pivot = new Vector2(0.5f, 0f);
        bRT.anchoredPosition = new Vector2(0f, 8f);
        bRT.sizeDelta = new Vector2(-24f, 32f);

        Image bImg = btnObj.AddComponent<Image>();
        bImg.color = new Color(0.18f, 0.58f, 0.75f, 0.95f);
        Button btn = btnObj.AddComponent<Button>();
        CreateText(btnObj.transform, "FABRICAR", 13, FontStyle.Bold, Color.white);

        btn.onClick.AddListener(() =>
        {
            TryCraftItem(outputItemId, ingIds, ingAmounts, title);
        });

        recipeCards.Add(card);
    }

    private void TryCraftItem(int outputItemId, int[] ingIds, int[] ingAmounts, string title)
    {
        if (inventory == null) return;

        // Checa se tem todos os materiais
        bool canCraft = true;
        for (int i = 0; i < ingIds.Length; i++)
        {
            if (inventory.GetItemCount(ingIds[i]) < ingAmounts[i])
            {
                canCraft = false;
                break;
            }
        }

        if (canCraft)
        {
            // Consome ingredientes
            for (int i = 0; i < ingIds.Length; i++)
            {
                inventory.RemoveItem(ingIds[i], ingAmounts[i]);
            }

            // Adiciona item fabricado
            inventory.AddItem(outputItemId, 1);

            HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
            if (hud != null) hud.ShowLootTooltip($"Item Fabricado: {title}!");

            // Feedback tátil
            HapticManager haptic = FindAnyObjectByType<HapticManager>();
            if (haptic != null) haptic.TriggerLightVibration();

            RefreshUI();
        }
        else
        {
            HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
            if (hud != null) hud.ShowLootTooltip("Materiais insuficientes!");
        }
    }

    // =================== ATUALIZAÇÃO DOS ELEMENTOS ===================

    public void RefreshUI()
    {
        if (inventory == null) return;

        // 1. Atualiza Slots do Inventário
        for (int i = 0; i < slotObjects.Count; i++)
        {
            if (i >= inventory.slots.Length) break;
            InventorySlot slot = inventory.slots[i];
            GameObject slotObj = slotObjects[i];

            Text icoTxt = slotObj.transform.Find("Icon").GetComponent<Text>();
            Text nameTxt = slotObj.transform.Find("Name").GetComponent<Text>();
            Text badgeTxt = slotObj.transform.Find("Quantity").GetComponent<Text>();

            if (slot.IsEmpty())
            {
                icoTxt.text = "";
                nameTxt.text = "Vazio";
                badgeTxt.text = "";
            }
            else
            {
                ItemData data = ItemDatabase.GetItem(slot.itemId);
                icoTxt.text = GetItemEmoji(slot.itemId);
                nameTxt.text = data != null ? data.itemName : $"Item {slot.itemId}";
                badgeTxt.text = slot.quantity > 1 ? $"x{slot.quantity}" : "";
            }
        }

        // 2. Atualiza Receitas de Crafting com coloração de requisitos
        for (int i = 0; i < recipeCards.Count; i++)
        {
            GameObject card = recipeCards[i];
            Text reqTxt = card.transform.Find("Ingredients").GetComponent<Text>();
            Button btn = card.transform.Find("Btn_Craft").GetComponent<Button>();
            Image btnImg = btn.GetComponent<Image>();

            // Obtém os dados da receita
            int outputId = int.Parse(card.name.Replace("Recipe_", ""));
            CraftRecipe recipe = GetRecipeByOutput(outputId);
            if (recipe != null)
            {
                string reqStr = "";
                bool canCraftAll = true;

                for (int j = 0; j < recipe.ingredientIds.Length; j++)
                {
                    int have = inventory.GetItemCount(recipe.ingredientIds[j]);
                    int need = recipe.ingredientAmounts[j];
                    string ingName = GetItemName(recipe.ingredientIds[j]);

                    if (have >= need)
                    {
                        reqStr += $"<color=#55FF88>✓ {ingName}: {have}/{need}</color>\n";
                    }
                    else
                    {
                        reqStr += $"<color=#FF5555>✕ {ingName}: {have}/{need}</color>\n";
                        canCraftAll = false;
                    }
                }

                reqTxt.text = reqStr;
                btn.interactable = canCraftAll;
                btnImg.color = canCraftAll ? new Color(0.18f, 0.65f, 0.40f, 0.95f) : new Color(0.25f, 0.30f, 0.35f, 0.50f);
            }
        }
    }

    private CraftRecipe GetRecipeByOutput(int id)
    {
        // Mapeamento direto de receitas
        if (id == 300) return new CraftRecipe { outputItemId = 300, ingredientIds = new int[] { 100, 101 }, ingredientAmounts = new int[] { 5, 3 } };
        if (id == 303) return new CraftRecipe { outputItemId = 303, ingredientIds = new int[] { 100, 102 }, ingredientAmounts = new int[] { 2, 1 } };
        if (id == 400) return new CraftRecipe { outputItemId = 400, ingredientIds = new int[] { 100, 102 }, ingredientAmounts = new int[] { 5, 2 } };
        if (id == 301) return new CraftRecipe { outputItemId = 301, ingredientIds = new int[] { 100, 101 }, ingredientAmounts = new int[] { 3, 5 } };
        if (id == 302) return new CraftRecipe { outputItemId = 302, ingredientIds = new int[] { 101, 107 }, ingredientAmounts = new int[] { 2, 3 } };
        if (id == 401) return new CraftRecipe { outputItemId = 401, ingredientIds = new int[] { 100, 107 }, ingredientAmounts = new int[] { 3, 2 } };
        if (id == 402) return new CraftRecipe { outputItemId = 402, ingredientIds = new int[] { 106 }, ingredientAmounts = new int[] { 3 } };
        if (id == 206) return new CraftRecipe { outputItemId = 206, ingredientIds = new int[] { 103 }, ingredientAmounts = new int[] { 3 } };
        if (id == 209) return new CraftRecipe { outputItemId = 209, ingredientIds = new int[] { 100 }, ingredientAmounts = new int[] { 4 } };
        return null;
    }

    private string GetItemName(int itemId)
    {
        ItemData data = ItemDatabase.GetItem(itemId);
        return data != null ? data.itemName : $"Item {itemId}";
    }

    private string GetItemEmoji(int itemId)
    {
        switch (itemId)
        {
            case 100: return "🪵"; // Madeira
            case 101: return "🪨"; // Pedra
            case 102: return "🌿"; // Fibra
            case 103: return "🍃"; // Erva Medicinal
            case 104: return "🍄"; // Cogumelo
            case 105: return "🫐"; // Frutos Silvestres
            case 106: return "🧂"; // Sal Sagrado
            case 107: return "🦴"; // Osso
            case 108: return "🥩"; // Carne Crua
            case 109: return "🍖"; // Carne Assada
            case 200: return "🏺"; // Cantil Vazio
            case 201: return "💧"; // Cantil com Água
            case 202: return "✨"; // Cantil Água Benta
            case 205: return "🔥"; // Fósforos
            case 206: return "🩹"; // Ligaduras
            case 209: return "🪵"; // Tala de Madeira
            case 300: return "🪓"; // Machado
            case 301: return "⛏️"; // Picareta
            case 302: return "🗡️"; // Faca
            case 303: return "🔦"; // Tocha
            case 400: return "⛺"; // Fogueira
            default: return "📦";
        }
    }

    private Text CreateText(Transform parent, string content, int fontSize, FontStyle style, Color color)
    {
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(parent, false);
        RectTransform rt = txtObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text txt = txtObj.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        Shadow shadow = txtObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.2f, -1.2f);
        return txt;
    }
}
