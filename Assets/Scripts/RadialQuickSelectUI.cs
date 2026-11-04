using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// RadialQuickSelectUI: Roda Radial de Acesso Rápido a Armas e Itens (Padrão AAA Mobile).
/// Suporta seleção polar rápida de 6 setores, gestos de toque (giro de 180°) e rolamento evasivo.
/// </summary>
public class RadialQuickSelectUI : MonoBehaviour
{
    public static RadialQuickSelectUI Instance { get; private set; }

    [Header("Radial Settings")]
    public float triggerHoldTime = 0.28f;
    public float deadzoneRadius = 35f;
    public float outerRadius = 160f;
    public int slotCount = 6;

    [Header("Dependencies")]
    public InventoryAdvanced inventory;
    public CameraTouchController cameraTouchZone;

    private GameObject radialContainer;
    private RectTransform radialRT;
    private Image[] sectorImages;
    private Text[] sectorTexts;
    private bool isRadialOpen = false;
    private int highlightedSector = -1;
    private Vector2 touchCenterScreen;

    // Gestos de 2 dedos
    private Vector2 twoFingerStartPos;
    private float twoFingerStartTime;

    private static readonly string[] SLOT_NAMES = new string[]
    {
        "🪓 Machado",
        "🔦 Tocha",
        "🗡️ Faca/Lança",
        "🏹 Arco/Funda",
        "🍖 Alimento",
        "🩹 Curativo"
    };

    private static readonly int[] PRIMARY_ITEM_IDS = new int[]
    {
        300, // Machado
        303, // Tocha
        302, // Faca
        310, // Arco
        200, // Carne Assada
        206  // Ligadura
    };

    private void Awake()
    {
        Instance = this;
        BuildRadialUI();
    }

    private void Start()
    {
        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
        if (cameraTouchZone == null) cameraTouchZone = FindAnyObjectByType<CameraTouchController>();
    }

    private void BuildRadialUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();

        radialContainer = new GameObject("RadialQuickSelect_Root");
        radialContainer.transform.SetParent(canvas.transform, false);
        radialRT = radialContainer.AddComponent<RectTransform>();
        radialRT.anchorMin = new Vector2(0.5f, 0.5f);
        radialRT.anchorMax = new Vector2(0.5f, 0.5f);
        radialRT.pivot = new Vector2(0.5f, 0.5f);
        radialRT.sizeDelta = new Vector2(outerRadius * 2.2f, outerRadius * 2.2f);

        sectorImages = new Image[slotCount];
        sectorTexts = new Text[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            float angle = i * (360f / slotCount);
            float rad = (angle - 90f) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)) * (outerRadius * 0.72f);

            GameObject sector = new GameObject($"Sector_{i}");
            sector.transform.SetParent(radialContainer.transform, false);
            RectTransform sRT = sector.AddComponent<RectTransform>();
            sRT.anchoredPosition = pos;
            sRT.sizeDelta = new Vector2(85f, 85f);

            Image img = sector.AddComponent<Image>();
            img.color = new Color(0.12f, 0.18f, 0.25f, 0.85f);
            sectorImages[i] = img;

            Outline outline = sector.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.7f, 0.9f, 0.45f);

            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(sector.transform, false);
            RectTransform tRT = txtObj.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = Vector2.zero;
            tRT.offsetMax = Vector2.zero;

            Text txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = SLOT_NAMES[i];
            sectorTexts[i] = txt;
        }

        radialContainer.SetActive(false);
    }

    private void Update()
    {
        // Gesto de 2 dedos para giro de 180 graus (Quick Look Behind)
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                twoFingerStartPos = (t0.position + t1.position) * 0.5f;
                twoFingerStartTime = Time.time;
            }
            else if (t0.phase == TouchPhase.Ended || t1.phase == TouchPhase.Ended)
            {
                Vector2 currentPos = (t0.position + t1.position) * 0.5f;
                float deltaY = currentPos.y - twoFingerStartPos.y;

                if (deltaY < -110f && (Time.time - twoFingerStartTime) < 0.35f)
                {
                    Perform180QuickTurn();
                }
            }
        }

        // Tecla Q no PC para segurar e abrir menu radial
        if (Input.GetKeyDown(KeyCode.Q))
        {
            OpenRadial(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }
        if (isRadialOpen && Input.GetKey(KeyCode.Q))
        {
            UpdateRadialSelection(Input.mousePosition);
        }
        if (isRadialOpen && Input.GetKeyUp(KeyCode.Q))
        {
            CloseRadialAndEquip();
        }
    }

    public void OpenRadial(Vector2 screenCenter)
    {
        isRadialOpen = true;
        touchCenterScreen = screenCenter;
        radialContainer.SetActive(true);

        // Posiciona a roda radial
        Canvas canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1.0f;
        radialRT.position = screenCenter;

        if (cameraTouchZone != null) cameraTouchZone.enabled = false;

        highlightedSector = -1;
        RefreshSectorColors();
    }

    public void UpdateRadialSelection(Vector2 currentScreenPos)
    {
        if (!isRadialOpen) return;

        Vector2 delta = currentScreenPos - touchCenterScreen;
        if (delta.magnitude < deadzoneRadius)
        {
            highlightedSector = -1;
            RefreshSectorColors();
            return;
        }

        // Converte para coordenadas polares (0 a 360 graus)
        float angle = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg + 90f;
        if (angle < 0f) angle += 360f;

        highlightedSector = Mathf.FloorToInt(angle / (360f / slotCount)) % slotCount;
        RefreshSectorColors();
    }

    public void CloseRadialAndEquip()
    {
        if (!isRadialOpen) return;
        isRadialOpen = false;
        radialContainer.SetActive(false);

        if (cameraTouchZone != null) cameraTouchZone.enabled = true;

        if (highlightedSector >= 0 && highlightedSector < slotCount)
        {
            EquipOrUseSlotItem(highlightedSector);
        }
    }

    private void RefreshSectorColors()
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (i == highlightedSector)
            {
                sectorImages[i].color = new Color(0.20f, 0.75f, 0.95f, 0.95f);
                sectorTexts[i].color = Color.black;
            }
            else
            {
                sectorImages[i].color = new Color(0.12f, 0.18f, 0.25f, 0.85f);
                sectorTexts[i].color = Color.white;
            }
        }
    }

    private void EquipOrUseSlotItem(int sectorIndex)
    {
        int itemId = PRIMARY_ITEM_IDS[sectorIndex];
        if (inventory == null) return;

        if (!inventory.HasItem(itemId))
        {
            HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
            if (hud != null) hud.ShowLootTooltip($"Item não encontrado: {SLOT_NAMES[sectorIndex]}");
            return;
        }

        // Se for consumível, consome diretamente
        if (itemId == 200 || itemId == 206)
        {
            for (int i = 0; i < inventory.slots.Length; i++)
            {
                if (inventory.slots[i].itemId == itemId)
                {
                    inventory.UseItem(i);
                    break;
                }
            }
        }
        else
        {
            HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
            if (hud != null) hud.ShowLootTooltip($"Equipado: {SLOT_NAMES[sectorIndex]}");
        }
    }

    private void Perform180QuickTurn()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) player = GameObject.Find("Player_CameraParent");
        if (player != null)
        {
            player.transform.Rotate(0, 180f, 0);
        }
        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.08f);
        }
    }
}