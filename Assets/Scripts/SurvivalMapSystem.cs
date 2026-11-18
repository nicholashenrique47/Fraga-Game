using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// SurvivalMapSystem: Carta Topográfica Interativa + Minimapa Dinâmico (Padrão The Forest).
/// - Minimapa no canto superior direito (posicionado abaixo do Relógio para zero sobreposição)
/// - Mapa Completo abrível pela tecla [M] ou toque no Minimapa / Botão MAPA
/// - Descoberta automática de 8 Pontos de Interesse + Marcação de Waypoint + Coordenadas & Elevação
/// </summary>
public class SurvivalMapSystem : MonoBehaviour
{
    public static SurvivalMapSystem Instance { get; private set; }
    public static bool IsMapOpen { get; private set; }

    public Transform playerTransform;

    private Texture2D mapTexture;
    private RectTransform minimapPanel;
    private RawImage minimapRaw;
    private RectTransform minimapPlayerArrow;
    private RectTransform minimapWaypointPip;
    private Text minimapRegionText;

    private GameObject fullscreenOverlay;
    private RawImage fullMapRaw;
    private RectTransform fullMapRect;
    private RectTransform fullPlayerArrow;
    private RectTransform fullWaypointRing;
    private Text fullCoordsText;
    private Text fullWaypointLabel;

    private readonly Dictionary<string, Image> poiMarkersFull = new Dictionary<string, Image>();
    private readonly Dictionary<string, Text> poiRowLabels = new Dictionary<string, Text>();
    private readonly Dictionary<string, Image> poiMarkersMini = new Dictionary<string, Image>();
    private readonly HashSet<string> discoveredPois = new HashSet<string>();

    // Marcadores Zero-GC para esquadrao Co-op e Sinais (Pings) no Minimapa e Mapa Completo
    private readonly Image[] coopMiniPips = new Image[FragaCoopSession.MaxPartySize];
    private readonly Image[] coopFullPips = new Image[FragaCoopSession.MaxPartySize];
    private readonly Text[] coopFullLabels = new Text[FragaCoopSession.MaxPartySize];
    private readonly Image[] pingMiniPips = new Image[FragaCoopSession.MaxPartySize];
    private readonly Image[] pingFullPips = new Image[FragaCoopSession.MaxPartySize];
    private readonly Text[] pingFullLabels = new Text[FragaCoopSession.MaxPartySize];

    private string activeWaypointId = "farol";
    private float nextDiscoveryCheckTime = 0f;
    private bool uiBuilt = false;

    private const float MinimapViewSpanMeters = 220f; // Janela de 220x220m ao redor do jogador

    private void Awake()
    {
        if (Instance == null) Instance = this;
        IsMapOpen = false;
    }

    private void Start()
    {
        FragaWorldBlueprint.EnsureInitialized();
        discoveredPois.Add("camp");
        ResolvePlayer();
        EnsureUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsMapOpen = false;
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null) return;
        if (PlayerController.Instance != null)
        {
            playerTransform = PlayerController.Instance.transform;
            return;
        }
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.playerTransform != null)
        {
            playerTransform = gm.playerTransform;
        }
    }

    private Canvas FindGameplayCanvas()
    {
        return HUDSurvivalOverlay.GetGameplayCanvas();
    }

    private static Sprite CreateArrowSprite()
    {
        int s = 32;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color[] px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x - s * 0.5f) / (s * 0.5f);
                float ny = (y - s * 0.5f) / (s * 0.5f);
                // Seta apontando para cima (+Y)
                bool inside = (ny <= 0.85f && ny >= -0.75f && Mathf.Abs(nx) <= (0.85f - ny) * 0.45f)
                              && !(ny < -0.30f && Mathf.Abs(nx) < (-0.30f - ny) * 0.7f);
                px[y * s + x] = inside ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateDotSprite()
    {
        int s = 24;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color[] px = new Color[s * s];
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = Mathf.Clamp01(s * 0.45f - d);
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    private void EnsureUI()
    {
        if (uiBuilt) return;
        Canvas targetCanvas = FindGameplayCanvas();
        if (targetCanvas == null) return;

        mapTexture = FragaWorldBlueprint.GetOrCreateWorldMapTexture(256);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sprite arrowSprite = CreateArrowSprite();
        Sprite dotSprite = CreateDotSprite();

        // ============================================================
        // 1. MINIMAPA NO CANTO SUPERIOR DIREITO (Abaixo do Relógio!)
        // Relógio ocupa y = -15..-59 em Top-Right; Minimapa começa em y = -74
        // ============================================================
        GameObject miniObj = new GameObject("HUD_Minimap_Widget");
        miniObj.transform.SetParent(targetCanvas.transform, false);
        minimapPanel = miniObj.AddComponent<RectTransform>();
        minimapPanel.anchorMin = new Vector2(1f, 1f);
        minimapPanel.anchorMax = new Vector2(1f, 1f);
        minimapPanel.pivot = new Vector2(1f, 1f);
        minimapPanel.anchoredPosition = new Vector2(-20f, -74f);
        minimapPanel.sizeDelta = new Vector2(178f, 178f);

        Image frameBg = miniObj.AddComponent<Image>();
        frameBg.color = new Color(0.06f, 0.08f, 0.10f, 0.88f);
        Outline frameOut = miniObj.AddComponent<Outline>();
        frameOut.effectColor = new Color(0.55f, 0.46f, 0.28f, 0.75f);
        frameOut.effectDistance = new Vector2(2f, -2f);

        // Botão invisível no minimapa para abrir mapa grande no toque Mobile
        Button miniBtn = miniObj.AddComponent<Button>();
        miniBtn.transition = Selectable.Transition.None;
        miniBtn.onClick.AddListener(ToggleMap);

        GameObject maskObj = new GameObject("Minimap_Mask");
        maskObj.transform.SetParent(miniObj.transform, false);
        RectTransform maskRT = maskObj.AddComponent<RectTransform>();
        maskRT.anchorMin = Vector2.zero;
        maskRT.anchorMax = Vector2.one;
        maskRT.offsetMin = new Vector2(4f, 4f);
        maskRT.offsetMax = new Vector2(-4f, -4f);
        maskObj.AddComponent<Image>().color = Color.white;
        Mask mask = maskObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject miniTexObj = new GameObject("Minimap_Texture");
        miniTexObj.transform.SetParent(maskObj.transform, false);
        RectTransform miniTexRT = miniTexObj.AddComponent<RectTransform>();
        miniTexRT.anchorMin = Vector2.zero;
        miniTexRT.anchorMax = Vector2.one;
        miniTexRT.offsetMin = Vector2.zero;
        miniTexRT.offsetMax = Vector2.zero;
        minimapRaw = miniTexObj.AddComponent<RawImage>();
        minimapRaw.texture = mapTexture;
        minimapRaw.raycastTarget = false;

        // Marcadores de POIs no Minimapa
        foreach (var poi in FragaWorldBlueprint.AllPois)
        {
            GameObject pObj = new GameObject("MiniPOI_" + poi.Id);
            pObj.transform.SetParent(maskObj.transform, false);
            RectTransform pRT = pObj.AddComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.5f, 0.5f);
            pRT.anchorMax = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(10f, 10f);
            Image pImg = pObj.AddComponent<Image>();
            pImg.sprite = dotSprite;
            pImg.color = poi.MarkerColor;
            pImg.raycastTarget = false;
            poiMarkersMini[poi.Id] = pImg;
        }

        // Anel do Waypoint no Minimapa
        GameObject mwObj = new GameObject("Minimap_Waypoint");
        mwObj.transform.SetParent(maskObj.transform, false);
        minimapWaypointPip = mwObj.AddComponent<RectTransform>();
        minimapWaypointPip.anchorMin = new Vector2(0.5f, 0.5f);
        minimapWaypointPip.anchorMax = new Vector2(0.5f, 0.5f);
        minimapWaypointPip.sizeDelta = new Vector2(14f, 14f);
        Image mwImg = mwObj.AddComponent<Image>();
        mwImg.sprite = dotSprite;
        mwImg.color = new Color(1f, 0.85f, 0.2f, 0.95f);
        mwImg.raycastTarget = false;

        // Marcadores de Aliados & Sinais Co-op no Minimapa (Zero-GC)
        for (int i = 0; i < FragaCoopSession.MaxPartySize; i++)
        {
            GameObject cpObj = new GameObject($"MiniCoopPip_{i}");
            cpObj.transform.SetParent(maskObj.transform, false);
            RectTransform cpRT = cpObj.AddComponent<RectTransform>();
            cpRT.anchorMin = new Vector2(0.5f, 0.5f); cpRT.anchorMax = new Vector2(0.5f, 0.5f);
            cpRT.sizeDelta = new Vector2(11f, 11f);
            Image cpImg = cpObj.AddComponent<Image>();
            cpImg.sprite = dotSprite;
            cpImg.raycastTarget = false;
            cpImg.enabled = false;
            coopMiniPips[i] = cpImg;

            GameObject ppObj = new GameObject($"MiniPingPip_{i}");
            ppObj.transform.SetParent(maskObj.transform, false);
            RectTransform ppRT = ppObj.AddComponent<RectTransform>();
            ppRT.anchorMin = new Vector2(0.5f, 0.5f); ppRT.anchorMax = new Vector2(0.5f, 0.5f);
            ppRT.sizeDelta = new Vector2(15f, 15f);
            Image ppImg = ppObj.AddComponent<Image>();
            ppImg.sprite = dotSprite;
            ppImg.raycastTarget = false;
            ppImg.enabled = false;
            pingMiniPips[i] = ppImg;
        }

        // Seta do Jogador no centro do Minimapa
        GameObject arrowObj = new GameObject("Minimap_PlayerArrow");
        arrowObj.transform.SetParent(maskObj.transform, false);
        minimapPlayerArrow = arrowObj.AddComponent<RectTransform>();
        minimapPlayerArrow.anchorMin = new Vector2(0.5f, 0.5f);
        minimapPlayerArrow.anchorMax = new Vector2(0.5f, 0.5f);
        minimapPlayerArrow.sizeDelta = new Vector2(16f, 16f);
        Image arrowImg = arrowObj.AddComponent<Image>();
        arrowImg.sprite = arrowSprite;
        arrowImg.color = new Color(1f, 0.96f, 0.85f, 1f);
        arrowImg.raycastTarget = false;

        // Legenda de região + dica [M] logo abaixo do minimapa
        GameObject regBgObj = new GameObject("Minimap_RegionBar");
        regBgObj.transform.SetParent(targetCanvas.transform, false);
        RectTransform regRT = regBgObj.AddComponent<RectTransform>();
        regRT.anchorMin = new Vector2(1f, 1f);
        regRT.anchorMax = new Vector2(1f, 1f);
        regRT.pivot = new Vector2(1f, 1f);
        regRT.anchoredPosition = new Vector2(-25f, -256f);
        regRT.sizeDelta = new Vector2(178f, 34f);
        Image regBg = regBgObj.AddComponent<Image>();
        regBg.color = new Color(0.05f, 0.07f, 0.09f, 0.78f);
        Button regBtn = regBgObj.AddComponent<Button>();
        regBtn.onClick.AddListener(ToggleMap);

        GameObject regTxtObj = new GameObject("Text");
        regTxtObj.transform.SetParent(regBgObj.transform, false);
        RectTransform rtTxt = regTxtObj.AddComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = new Vector2(4f, 0f);
        rtTxt.offsetMax = new Vector2(-4f, 0f);
        minimapRegionText = regTxtObj.AddComponent<Text>();
        minimapRegionText.font = font;
        minimapRegionText.fontSize = 11;
        minimapRegionText.fontStyle = FontStyle.Bold;
        minimapRegionText.alignment = TextAnchor.MiddleCenter;
        minimapRegionText.color = new Color(0.92f, 0.88f, 0.72f, 0.98f);
        minimapRegionText.text = "Acampamento\n[M] MAPA";
        minimapRegionText.raycastTarget = false;

        // ============================================================
        // 2. PAINEL DE MAPA COMPLETO (TECLA [M] / TOQUE NO MINIMAPA)
        // ============================================================
        fullscreenOverlay = new GameObject("Fullscreen_Survival_Map");
        fullscreenOverlay.transform.SetParent(targetCanvas.transform, false);
        RectTransform fsRT = fullscreenOverlay.AddComponent<RectTransform>();
        fsRT.anchorMin = Vector2.zero;
        fsRT.anchorMax = Vector2.one;
        fsRT.offsetMin = Vector2.zero;
        fsRT.offsetMax = Vector2.zero;
        Image fsBg = fullscreenOverlay.AddComponent<Image>();
        fsBg.color = new Color(0.02f, 0.03f, 0.04f, 0.86f);

        GameObject card = new GameObject("Map_Parchment_Card");
        card.transform.SetParent(fullscreenOverlay.transform, false);
        RectTransform cardRT = card.AddComponent<RectTransform>();
        cardRT.anchoredPosition = Vector2.zero;
        cardRT.sizeDelta = new Vector2(960f, 680f);
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.09f, 0.09f, 0.11f, 0.97f);
        Outline cardOut = card.AddComponent<Outline>();
        cardOut.effectColor = new Color(0.62f, 0.50f, 0.30f, 0.85f);
        cardOut.effectDistance = new Vector2(2f, -2f);

        // Título superior
        GameObject titleObj = new GameObject("Map_Title");
        titleObj.transform.SetParent(card.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f); tRT.pivot = new Vector2(0.5f, 1f);
        tRT.anchoredPosition = new Vector2(0f, -12f);
        tRT.sizeDelta = new Vector2(-40f, 36f);
        Text titleTxt = titleObj.AddComponent<Text>();
        titleTxt.font = font;
        titleTxt.fontSize = 22;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleLeft;
        titleTxt.color = new Color(0.95f, 0.84f, 0.56f, 1f);
        titleTxt.text = "CARTA DA ILHA DA FRAGA (768x768m)";

        // Botão fechar [X]
        GameObject closeObj = new GameObject("Btn_Close_Map");
        closeObj.transform.SetParent(card.transform, false);
        RectTransform cRT = closeObj.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(1f, 1f); cRT.anchorMax = new Vector2(1f, 1f); cRT.pivot = new Vector2(1f, 1f);
        cRT.anchoredPosition = new Vector2(-14f, -12f);
        cRT.sizeDelta = new Vector2(110f, 34f);
        closeObj.AddComponent<Image>().color = new Color(0.45f, 0.14f, 0.14f, 0.95f);
        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseMap);
        GameObject cTxtObj = new GameObject("Text");
        cTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform ctRT = cTxtObj.AddComponent<RectTransform>();
        ctRT.anchorMin = Vector2.zero; ctRT.anchorMax = Vector2.one; ctRT.offsetMin = Vector2.zero; ctRT.offsetMax = Vector2.zero;
        Text cTxt = cTxtObj.AddComponent<Text>();
        cTxt.font = font; cTxt.fontSize = 14; cTxt.fontStyle = FontStyle.Bold; cTxt.alignment = TextAnchor.MiddleCenter; cTxt.color = Color.white;
        cTxt.text = "FECHAR [M]";

        // Mapa Topográfico 580x580 à esquerda
        GameObject fullMapObj = new GameObject("Map_Texture_Container");
        fullMapObj.transform.SetParent(card.transform, false);
        fullMapRect = fullMapObj.AddComponent<RectTransform>();
        fullMapRect.anchorMin = new Vector2(0f, 0.5f); fullMapRect.anchorMax = new Vector2(0f, 0.5f); fullMapRect.pivot = new Vector2(0f, 0.5f);
        fullMapRect.anchoredPosition = new Vector2(22f, -14f);
        fullMapRect.sizeDelta = new Vector2(580f, 580f);
        fullMapRaw = fullMapObj.AddComponent<RawImage>();
        fullMapRaw.texture = mapTexture;

        Outline mapOut = fullMapObj.AddComponent<Outline>();
        mapOut.effectColor = new Color(0.45f, 0.36f, 0.22f, 0.9f);
        mapOut.effectDistance = new Vector2(2f, -2f);

        // Barra de coordenadas abaixo do mapa
        GameObject coordObj = new GameObject("Map_Coords_Text");
        coordObj.transform.SetParent(card.transform, false);
        RectTransform coRT = coordObj.AddComponent<RectTransform>();
        coRT.anchorMin = new Vector2(0f, 0f); coRT.anchorMax = new Vector2(0f, 0f); coRT.pivot = new Vector2(0f, 0f);
        coRT.anchoredPosition = new Vector2(22f, 8f);
        coRT.sizeDelta = new Vector2(580f, 24f);
        fullCoordsText = coordObj.AddComponent<Text>();
        fullCoordsText.font = font;
        fullCoordsText.fontSize = 14;
        fullCoordsText.alignment = TextAnchor.MiddleLeft;
        fullCoordsText.color = new Color(0.85f, 0.88f, 0.90f, 1f);

        // Marcadores de POIs sobre o Mapa Completo
        foreach (var poi in FragaWorldBlueprint.AllPois)
        {
            float nx = poi.Pos.x / FragaWorldBlueprint.Width;
            float nz = poi.Pos.y / FragaWorldBlueprint.Height;

            GameObject pipObj = new GameObject("FullPOI_" + poi.Id);
            pipObj.transform.SetParent(fullMapObj.transform, false);
            RectTransform pipRT = pipObj.AddComponent<RectTransform>();
            pipRT.anchorMin = new Vector2(nx, nz);
            pipRT.anchorMax = new Vector2(nx, nz);
            pipRT.pivot = new Vector2(0.5f, 0.5f);
            pipRT.anchoredPosition = Vector2.zero;
            pipRT.sizeDelta = new Vector2(14f, 14f);
            Image pipImg = pipObj.AddComponent<Image>();
            pipImg.sprite = dotSprite;
            pipImg.color = poi.MarkerColor;
            Outline pipOut = pipObj.AddComponent<Outline>();
            pipOut.effectColor = Color.black;
            pipOut.effectDistance = new Vector2(1f, -1f);
            poiMarkersFull[poi.Id] = pipImg;

            GameObject lblObj = new GameObject("Label");
            lblObj.transform.SetParent(pipObj.transform, false);
            RectTransform lRT = lblObj.AddComponent<RectTransform>();
            lRT.anchoredPosition = new Vector2(0f, 14f);
            lRT.sizeDelta = new Vector2(150f, 20f);
            Text lTxt = lblObj.AddComponent<Text>();
            lTxt.font = font; lTxt.fontSize = 12; lTxt.fontStyle = FontStyle.Bold;
            lTxt.alignment = TextAnchor.MiddleCenter; lTxt.color = new Color(0.96f, 0.94f, 0.86f, 1f);
            lTxt.text = poi.DisplayName;
            Shadow lSh = lblObj.AddComponent<Shadow>();
            lSh.effectColor = Color.black;
        }

        // Anel Waypoint no Mapa Completo
        GameObject fwpObj = new GameObject("FullMap_WaypointRing");
        fwpObj.transform.SetParent(fullMapObj.transform, false);
        fullWaypointRing = fwpObj.AddComponent<RectTransform>();
        fullWaypointRing.sizeDelta = new Vector2(24f, 24f);
        Image fwpImg = fwpObj.AddComponent<Image>();
        fwpImg.sprite = dotSprite;
        fwpImg.color = new Color(1f, 0.85f, 0.15f, 0.55f);
        fwpImg.raycastTarget = false;

        // Marcadores Co-op (Aliados e Sinais Ping) no Mapa Completo
        for (int i = 0; i < FragaCoopSession.MaxPartySize; i++)
        {
            GameObject fcObj = new GameObject($"FullCoopPip_{i}");
            fcObj.transform.SetParent(fullMapObj.transform, false);
            RectTransform fcRT = fcObj.AddComponent<RectTransform>();
            fcRT.sizeDelta = new Vector2(14f, 14f);
            Image fcImg = fcObj.AddComponent<Image>();
            fcImg.sprite = dotSprite;
            fcImg.raycastTarget = false;
            fcObj.SetActive(false);
            coopFullPips[i] = fcImg;

            GameObject fclObj = new GameObject("Name");
            fclObj.transform.SetParent(fcObj.transform, false);
            RectTransform fclRT = fclObj.AddComponent<RectTransform>();
            fclRT.anchoredPosition = new Vector2(0f, 13f); fclRT.sizeDelta = new Vector2(140f, 18f);
            Text fclTxt = fclObj.AddComponent<Text>();
            fclTxt.font = font; fclTxt.fontSize = 11; fclTxt.fontStyle = FontStyle.Bold;
            fclTxt.alignment = TextAnchor.MiddleCenter; fclTxt.raycastTarget = false;
            fclObj.AddComponent<Shadow>().effectColor = Color.black;
            coopFullLabels[i] = fclTxt;

            GameObject fpObj = new GameObject($"FullPingPip_{i}");
            fpObj.transform.SetParent(fullMapObj.transform, false);
            RectTransform fpRT = fpObj.AddComponent<RectTransform>();
            fpRT.sizeDelta = new Vector2(20f, 20f);
            Image fpImg = fpObj.AddComponent<Image>();
            fpImg.sprite = dotSprite;
            fpImg.raycastTarget = false;
            fpObj.SetActive(false);
            pingFullPips[i] = fpImg;

            GameObject fplObj = new GameObject("PingLabel");
            fplObj.transform.SetParent(fpObj.transform, false);
            RectTransform fplRT = fplObj.AddComponent<RectTransform>();
            fplRT.anchoredPosition = new Vector2(0f, 15f); fplRT.sizeDelta = new Vector2(150f, 18f);
            Text fplTxt = fplObj.AddComponent<Text>();
            fplTxt.font = font; fplTxt.fontSize = 11; fplTxt.fontStyle = FontStyle.Bold;
            fplTxt.alignment = TextAnchor.MiddleCenter; fplTxt.color = new Color(0.45f, 0.96f, 1f); fplTxt.raycastTarget = false;
            fplObj.AddComponent<Shadow>().effectColor = Color.black;
            pingFullLabels[i] = fplTxt;
        }

        // Seta do Jogador no Mapa Completo
        GameObject fArrObj = new GameObject("FullMap_PlayerArrow");
        fArrObj.transform.SetParent(fullMapObj.transform, false);
        fullPlayerArrow = fArrObj.AddComponent<RectTransform>();
        fullPlayerArrow.sizeDelta = new Vector2(20f, 20f);
        Image fArrImg = fArrObj.AddComponent<Image>();
        fArrImg.sprite = arrowSprite;
        fArrImg.color = Color.white;
        fArrImg.raycastTarget = false;

        // Lista Lateral de Pontos de Interesse (direita)
        GameObject sidePanel = new GameObject("Map_POI_Sidebar");
        sidePanel.transform.SetParent(card.transform, false);
        RectTransform spRT = sidePanel.AddComponent<RectTransform>();
        spRT.anchorMin = new Vector2(1f, 0.5f); spRT.anchorMax = new Vector2(1f, 0.5f); spRT.pivot = new Vector2(1f, 0.5f);
        spRT.anchoredPosition = new Vector2(-22f, -14f);
        spRT.sizeDelta = new Vector2(315f, 580f);
        sidePanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.85f);

        GameObject sideHeader = new GameObject("Sidebar_Header");
        sideHeader.transform.SetParent(sidePanel.transform, false);
        RectTransform shRT = sideHeader.AddComponent<RectTransform>();
        shRT.anchorMin = new Vector2(0f, 1f); shRT.anchorMax = new Vector2(1f, 1f); shRT.pivot = new Vector2(0.5f, 1f);
        shRT.anchoredPosition = new Vector2(0f, -10f);
        shRT.sizeDelta = new Vector2(-20f, 44f);
        fullWaypointLabel = sideHeader.AddComponent<Text>();
        fullWaypointLabel.font = font; fullWaypointLabel.fontSize = 13; fullWaypointLabel.fontStyle = FontStyle.Bold;
        fullWaypointLabel.alignment = TextAnchor.MiddleLeft;
        fullWaypointLabel.color = new Color(1f, 0.85f, 0.40f, 1f);
        fullWaypointLabel.text = "LOCAIS DA ILHA\n(Clique para marcar destino)";

        for (int i = 0; i < FragaWorldBlueprint.AllPois.Length; i++)
        {
            var poi = FragaWorldBlueprint.AllPois[i];
            string capturedId = poi.Id;

            GameObject row = new GameObject("POI_Row_" + poi.Id);
            row.transform.SetParent(sidePanel.transform, false);
            RectTransform rowRT = row.AddComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 1f); rowRT.anchorMax = new Vector2(1f, 1f); rowRT.pivot = new Vector2(0.5f, 1f);
            rowRT.anchoredPosition = new Vector2(0f, -66f - i * 60f);
            rowRT.sizeDelta = new Vector2(-20f, 52f);
            row.AddComponent<Image>().color = new Color(0.14f, 0.16f, 0.20f, 0.9f);
            Button rowBtn = row.AddComponent<Button>();
            rowBtn.onClick.AddListener(() => SelectWaypoint(capturedId));

            GameObject rowTxtObj = new GameObject("Text");
            rowTxtObj.transform.SetParent(row.transform, false);
            RectTransform rtxRT = rowTxtObj.AddComponent<RectTransform>();
            rtxRT.anchorMin = Vector2.zero; rtxRT.anchorMax = Vector2.one;
            rtxRT.offsetMin = new Vector2(10f, 2f); rtxRT.offsetMax = new Vector2(-10f, -2f);
            Text rTxt = rowTxtObj.AddComponent<Text>();
            rTxt.font = font; rTxt.fontSize = 13; rTxt.alignment = TextAnchor.MiddleLeft;
            rTxt.color = Color.white;
            rTxt.raycastTarget = false;
            poiRowLabels[poi.Id] = rTxt;
        }

        fullscreenOverlay.SetActive(false);
        uiBuilt = true;
    }

    public void SelectWaypoint(string poiId)
    {
        activeWaypointId = poiId;
        UpdateFullscreenMapVisuals();
    }

    public void ToggleMap()
    {
        if (IsMapOpen) CloseMap();
        else OpenMap();
    }

    public void OpenMap()
    {
        EnsureUI();
        if (fullscreenOverlay == null) return;
        IsMapOpen = true;
        fullscreenOverlay.SetActive(true);
        fullscreenOverlay.transform.SetAsLastSibling();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UpdateFullscreenMapVisuals();
    }

    public void CloseMap()
    {
        if (fullscreenOverlay != null) fullscreenOverlay.SetActive(false);
        IsMapOpen = false;
        if (Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.WindowsEditor ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.OSXEditor ||
            Application.platform == RuntimePlatform.LinuxPlayer)
        {
            if (Input.touchCount == 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f && !IsMapOpen) return;

        ResolvePlayer();
        if (!uiBuilt) EnsureUI();

        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMap();
        }
        else if (IsMapOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMap();
        }

        if (playerTransform == null || !uiBuilt) return;

        UpdateMinimap();

        if (Time.time >= nextDiscoveryCheckTime)
        {
            nextDiscoveryCheckTime = Time.time + 0.45f;
            CheckPoiDiscovery();
        }

        if (IsMapOpen)
        {
            UpdateFullscreenMapVisuals();
        }
    }

    private void CheckPoiDiscovery()
    {
        Vector2 p2 = new Vector2(playerTransform.position.x, playerTransform.position.z);
        for (int i = 0; i < FragaWorldBlueprint.AllPois.Length; i++)
        {
            var poi = FragaWorldBlueprint.AllPois[i];
            if (discoveredPois.Contains(poi.Id)) continue;

            if (Vector2.Distance(p2, poi.Pos) <= poi.DiscoverRadius)
            {
                discoveredPois.Add(poi.Id);
                UIManager ui = FindAnyObjectByType<UIManager>();
                if (ui != null)
                {
                    ui.SetLootTooltip($"[POI] Descoberto: {poi.DisplayName}");
                }
            }
        }
    }

    private void UpdateMinimap()
    {
        float px = playerTransform.position.x;
        float pz = playerTransform.position.z;
        float w = FragaWorldBlueprint.Width;
        float h = FragaWorldBlueprint.Height;

        if (minimapRaw != null)
        {
            float uvW = MinimapViewSpanMeters / w;
            float uvH = MinimapViewSpanMeters / h;
            float uvX = Mathf.Clamp((px / w) - uvW * 0.5f, 0f, 1f - uvW);
            float uvY = Mathf.Clamp((pz / h) - uvH * 0.5f, 0f, 1f - uvH);
            minimapRaw.uvRect = new Rect(uvX, uvY, uvW, uvH);

            float centerWorldX = (uvX + uvW * 0.5f) * w;
            float centerWorldZ = (uvY + uvH * 0.5f) * h;
            float pxPerMeter = (minimapPanel.rect.width - 8f) / MinimapViewSpanMeters;

            if (minimapPlayerArrow != null)
            {
                minimapPlayerArrow.anchoredPosition = new Vector2((px - centerWorldX) * pxPerMeter, (pz - centerWorldZ) * pxPerMeter);
                minimapPlayerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerTransform.eulerAngles.y);
            }

            float halfBox = (minimapPanel.rect.width - 14f) * 0.5f;
            for (int i = 0; i < FragaWorldBlueprint.AllPois.Length; i++)
            {
                var poi = FragaWorldBlueprint.AllPois[i];
                if (!poiMarkersMini.TryGetValue(poi.Id, out Image pip)) continue;

                float dx = (poi.Pos.x - centerWorldX) * pxPerMeter;
                float dz = (poi.Pos.y - centerWorldZ) * pxPerMeter;
                bool inside = Mathf.Abs(dx) <= halfBox && Mathf.Abs(dz) <= halfBox;
                pip.enabled = inside;
                if (inside)
                {
                    pip.rectTransform.anchoredPosition = new Vector2(dx, dz);
                }

                if (poi.Id == activeWaypointId && minimapWaypointPip != null)
                {
                    Vector2 clamped = new Vector2(Mathf.Clamp(dx, -halfBox, halfBox), Mathf.Clamp(dz, -halfBox, halfBox));
                    minimapWaypointPip.anchoredPosition = clamped;
                }
            }

            // Atualiza aliados e pings Co-op no Minimapa (Zero-GC)
            FragaCoopSession coop = FragaCoopSession.Instance;
            var members = coop != null ? coop.Members : null;
            float now = Time.unscaledTime;
            for (int i = 0; i < FragaCoopSession.MaxPartySize; i++)
            {
                Image cPip = coopMiniPips[i];
                Image pPip = pingMiniPips[i];
                bool hasMember = members != null && i < members.Count;
                if (!hasMember)
                {
                    if (cPip != null) cPip.enabled = false;
                    if (pPip != null) pPip.enabled = false;
                    continue;
                }

                CoopMemberState m = members[i];
                ColorUtility.TryParseHtmlString(m.colorHex, out Color mCol);
                if (cPip != null)
                {
                    bool showAlly = !m.isLocalPlayer && m.isConnected;
                    cPip.enabled = showAlly;
                    if (showAlly)
                    {
                        float cdx = Mathf.Clamp((m.worldPosition.x - centerWorldX) * pxPerMeter, -halfBox, halfBox);
                        float cdz = Mathf.Clamp((m.worldPosition.z - centerWorldZ) * pxPerMeter, -halfBox, halfBox);
                        cPip.rectTransform.anchoredPosition = new Vector2(cdx, cdz);
                        cPip.color = mCol;
                    }
                }
                if (pPip != null)
                {
                    bool showPing = m.hasActivePing && now < m.activePingExpiresAt;
                    pPip.enabled = showPing;
                    if (showPing)
                    {
                        float pdx = Mathf.Clamp((m.activePingPosition.x - centerWorldX) * pxPerMeter, -halfBox, halfBox);
                        float pdz = Mathf.Clamp((m.activePingPosition.z - centerWorldZ) * pxPerMeter, -halfBox, halfBox);
                        pPip.rectTransform.anchoredPosition = new Vector2(pdx, pdz);
                        float pScale = 0.85f + Mathf.Sin(now * 6f) * 0.25f;
                        pPip.rectTransform.localScale = new Vector3(pScale, pScale, 1f);
                        pPip.color = new Color(0.35f, 0.96f, 1f, 0.95f);
                    }
                }
            }
        }

        if (minimapRegionText != null)
        {
            FragaWorldBlueprint.WorldRegion reg = FragaWorldBlueprint.GetRegion(px, pz);
            minimapRegionText.text = $"{FragaWorldBlueprint.GetRegionDisplayName(reg)}\n[M] MAPA ({discoveredPois.Count}/{FragaWorldBlueprint.AllPois.Length})";
        }
    }

    private void UpdateFullscreenMapVisuals()
    {
        if (playerTransform == null || fullMapRect == null) return;

        float px = playerTransform.position.x;
        float pz = playerTransform.position.z;
        float nx = Mathf.Clamp01(px / FragaWorldBlueprint.Width);
        float nz = Mathf.Clamp01(pz / FragaWorldBlueprint.Height);

        if (fullPlayerArrow != null)
        {
            fullPlayerArrow.anchorMin = new Vector2(nx, nz);
            fullPlayerArrow.anchorMax = new Vector2(nx, nz);
            fullPlayerArrow.anchoredPosition = Vector2.zero;
            fullPlayerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerTransform.eulerAngles.y);
        }

        if (fullCoordsText != null)
        {
            FragaWorldBlueprint.WorldRegion reg = FragaWorldBlueprint.GetRegion(px, pz);
            fullCoordsText.text = $"Região: {FragaWorldBlueprint.GetRegionDisplayName(reg)}  |  Pos: ({px:F0}, {pz:F0})  |  Altitude: {playerTransform.position.y:F1}m";
        }

        Vector2 p2 = new Vector2(px, pz);
        for (int i = 0; i < FragaWorldBlueprint.AllPois.Length; i++)
        {
            var poi = FragaWorldBlueprint.AllPois[i];
            bool known = discoveredPois.Contains(poi.Id);
            bool isWp = (poi.Id == activeWaypointId);

            if (isWp && fullWaypointRing != null)
            {
                float wnx = poi.Pos.x / FragaWorldBlueprint.Width;
                float wnz = poi.Pos.y / FragaWorldBlueprint.Height;
                fullWaypointRing.anchorMin = new Vector2(wnx, wnz);
                fullWaypointRing.anchorMax = new Vector2(wnx, wnz);
                fullWaypointRing.anchoredPosition = Vector2.zero;
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.18f;
                fullWaypointRing.localScale = Vector3.one * pulse;
            }

            if (poiRowLabels.TryGetValue(poi.Id, out Text rTxt))
            {
                float dist = Vector2.Distance(p2, poi.Pos);
                string dir = GetCardinalDirection(poi.Pos - p2);
                string prefix = isWp ? "[WP] " : (known ? "[OK] " : "[?] ");
                string name = known ? poi.DisplayName : "Local Inexplorado";
                rTxt.text = $"{prefix}{name}\n<color=#B8C4CC>{dist:F0}m ({dir})</color>";
                rTxt.color = isWp ? new Color(1f, 0.88f, 0.35f, 1f) : (known ? Color.white : new Color(0.70f, 0.74f, 0.78f, 0.9f));
            }
        }

        // Atualiza aliados e pings Co-op no Mapa Completo
        FragaCoopSession coopFull = FragaCoopSession.Instance;
        var fullMembers = coopFull != null ? coopFull.Members : null;
        float nowFull = Time.unscaledTime;
        for (int i = 0; i < FragaCoopSession.MaxPartySize; i++)
        {
            Image cPip = coopFullPips[i];
            Image pPip = pingFullPips[i];
            bool hasMember = fullMembers != null && i < fullMembers.Count;
            if (!hasMember)
            {
                if (cPip != null) cPip.gameObject.SetActive(false);
                if (pPip != null) pPip.gameObject.SetActive(false);
                continue;
            }

            CoopMemberState m = fullMembers[i];
            ColorUtility.TryParseHtmlString(m.colorHex, out Color mCol);
            if (cPip != null)
            {
                bool showAlly = !m.isLocalPlayer && m.isConnected;
                cPip.gameObject.SetActive(showAlly);
                if (showAlly)
                {
                    float anx = Mathf.Clamp01(m.worldPosition.x / FragaWorldBlueprint.Width);
                    float anz = Mathf.Clamp01(m.worldPosition.z / FragaWorldBlueprint.Height);
                    cPip.rectTransform.anchorMin = new Vector2(anx, anz);
                    cPip.rectTransform.anchorMax = new Vector2(anx, anz);
                    cPip.rectTransform.anchoredPosition = Vector2.zero;
                    cPip.color = mCol;
                    if (coopFullLabels[i] != null)
                    {
                        coopFullLabels[i].text = m.displayName;
                        coopFullLabels[i].color = mCol;
                    }
                }
            }
            if (pPip != null)
            {
                bool showPing = m.hasActivePing && nowFull < m.activePingExpiresAt;
                pPip.gameObject.SetActive(showPing);
                if (showPing)
                {
                    float pnx = Mathf.Clamp01(m.activePingPosition.x / FragaWorldBlueprint.Width);
                    float pnz = Mathf.Clamp01(m.activePingPosition.z / FragaWorldBlueprint.Height);
                    pPip.rectTransform.anchorMin = new Vector2(pnx, pnz);
                    pPip.rectTransform.anchorMax = new Vector2(pnx, pnz);
                    pPip.rectTransform.anchoredPosition = Vector2.zero;
                    float pScale = 1.0f + Mathf.Sin(nowFull * 6f) * 0.25f;
                    pPip.rectTransform.localScale = new Vector3(pScale, pScale, 1f);
                    pPip.color = new Color(0.30f, 0.96f, 1f, 0.72f);
                    if (pingFullLabels[i] != null)
                        pingFullLabels[i].text = $"[SINAL] {m.activePingLabel}";
                }
            }
        }
    }

    private static string GetCardinalDirection(Vector2 delta)
    {
        float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        string[] dirs = { "N", "NE", "L", "SE", "S", "SO", "O", "NO" };
        int idx = Mathf.RoundToInt(angle / 45f) % 8;
        return dirs[idx];
    }
}
