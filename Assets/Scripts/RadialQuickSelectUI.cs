using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RadialQuickSelectUI: Roda Radial de Acesso Rapido a Armas e Itens (PC [Tab] + Mobile Drag).
/// Sincronizada com os 6 atalhos do InventoryAdvanced e icones vetoriais profissionais (Zero-Emoji).
/// </summary>
public class RadialQuickSelectUI : MonoBehaviour
{
    public static RadialQuickSelectUI Instance { get; private set; }

    [Header("Radial Settings")]
    public float deadzoneRadius = 35f;
    public float outerRadius = 158f;
    public int slotCount = 6;

    [Header("Dependencies")]
    public InventoryAdvanced inventory;
    public CameraTouchController cameraTouchZone;

    private GameObject radialContainer;
    private RectTransform radialRT;
    private Image[] sectorImages;
    private Image[] sectorIcons;
    private Text[] sectorTexts;
    private bool isRadialOpen = false;
    private int highlightedSector = -1;
    private Vector2 touchCenterScreen;

    private Vector2 twoFingerStartPos;
    private float twoFingerStartTime;

    public bool IsOpen => isRadialOpen;

    private void Awake()
    {
        Instance = this;
        BuildRadialUI();
    }

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (cameraTouchZone == null) cameraTouchZone = FindAnyObjectByType<CameraTouchController>();
    }

    private void BuildRadialUI()
    {
        Canvas canvas = HUDSurvivalOverlay.GetGameplayCanvas();
        if (canvas == null) return;

        radialContainer = new GameObject("RadialQuickSelect_Root");
        radialContainer.transform.SetParent(canvas.transform, false);
        radialRT = radialContainer.AddComponent<RectTransform>();
        radialRT.anchorMin = new Vector2(0.5f, 0.5f);
        radialRT.anchorMax = new Vector2(0.5f, 0.5f);
        radialRT.pivot = new Vector2(0.5f, 0.5f);
        radialRT.sizeDelta = new Vector2(outerRadius * 2.2f, outerRadius * 2.2f);

        sectorImages = new Image[slotCount];
        sectorIcons = new Image[slotCount];
        sectorTexts = new Text[slotCount];

        Sprite circleSprite = FragaProIconLibrary.GetCircleBackground();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        for (int i = 0; i < slotCount; i++)
        {
            float angle = i * (360f / slotCount);
            float rad = (angle - 90f) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)) * (outerRadius * 0.72f);

            GameObject sector = new GameObject($"Sector_{i}");
            sector.transform.SetParent(radialContainer.transform, false);
            RectTransform sRT = sector.AddComponent<RectTransform>();
            sRT.anchoredPosition = pos;
            sRT.sizeDelta = new Vector2(92f, 92f);

            Image img = sector.AddComponent<Image>();
            img.sprite = circleSprite;
            img.color = new Color(0.10f, 0.16f, 0.22f, 0.90f);
            sectorImages[i] = img;

            Outline outline = sector.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.80f, 0.96f, 0.55f);

            // Icone vetorial
            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(sector.transform, false);
            RectTransform iRT = icoObj.AddComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.5f, 0.60f);
            iRT.anchorMax = new Vector2(0.5f, 0.60f);
            iRT.sizeDelta = new Vector2(44f, 44f);
            Image ico = icoObj.AddComponent<Image>();
            ico.preserveAspect = true;
            ico.raycastTarget = false;
            sectorIcons[i] = ico;

            // Legenda curta
            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(sector.transform, false);
            RectTransform tRT = txtObj.AddComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0f, 0.08f);
            tRT.anchorMax = new Vector2(1f, 0.36f);
            tRT.offsetMin = new Vector2(4f, 0f);
            tRT.offsetMax = new Vector2(-4f, 0f);

            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 10;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            sectorTexts[i] = txt;
        }

        radialContainer.SetActive(false);
    }

    private void Update()
    {
        // Gesto de 2 dedos para giro de 180 graus
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

        // Tecla Tab no PC para segurar e abrir menu radial (sem conflito com Q/I)
        if (Input.GetKeyDown(KeyCode.Tab) && (SurvivalInventoryCraftUI.Instance == null || !SurvivalInventoryCraftUI.Instance.IsOpen))
        {
            OpenRadial(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }
        if (isRadialOpen && Input.GetKey(KeyCode.Tab))
        {
            UpdateRadialSelection(Input.mousePosition);
        }
        if (isRadialOpen && Input.GetKeyUp(KeyCode.Tab))
        {
            CloseRadialAndEquip();
        }
    }

    public void OpenRadial(Vector2 screenCenter)
    {
        isRadialOpen = true;
        touchCenterScreen = screenCenter;
        if (radialContainer != null) radialContainer.SetActive(true);
        if (radialRT != null) radialRT.position = screenCenter;
        if (cameraTouchZone != null) cameraTouchZone.enabled = false;

        if (Input.touchCount == 0 && (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        highlightedSector = -1;
        RefreshSectorContents();
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

        float angle = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg + 90f;
        if (angle < 0f) angle += 360f;

        highlightedSector = Mathf.FloorToInt(angle / (360f / slotCount)) % slotCount;
        RefreshSectorColors();
    }

    public void CloseRadialAndEquip()
    {
        if (!isRadialOpen) return;
        isRadialOpen = false;
        if (radialContainer != null) radialContainer.SetActive(false);
        if (cameraTouchZone != null) cameraTouchZone.enabled = true;

        if (Input.touchCount == 0 && (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (highlightedSector >= 0 && highlightedSector < slotCount)
        {
            EquipOrUseSlotItem(highlightedSector);
        }
    }

    private void RefreshSectorContents()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        for (int i = 0; i < slotCount; i++)
        {
            int itemId = (inventory != null && i < inventory.hotbarItemIds.Length) ? inventory.hotbarItemIds[i] : -1;
            bool hasItem = inventory != null && itemId > 0 && inventory.HasItem(itemId);
            if (itemId > 0)
            {
                ItemData d = ItemDatabase.GetItem(itemId);
                sectorIcons[i].enabled = true;
                sectorIcons[i].sprite = FragaProIconLibrary.GetItemIcon(itemId);
                sectorIcons[i].color = hasItem ? Color.white : new Color(1f, 1f, 1f, 0.32f);
                string shortName = d != null ? d.itemName.Split(' ')[0] : $"#{itemId}";
                int qty = inventory != null ? inventory.GetItemCount(itemId) : 0;
                sectorTexts[i].text = qty > 1 ? $"{shortName} x{qty}" : shortName;
            }
            else
            {
                sectorIcons[i].enabled = false;
                sectorTexts[i].text = $"SLOT {i + 1}";
            }
        }
    }

    private void RefreshSectorColors()
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (i == highlightedSector)
            {
                sectorImages[i].color = new Color(0.20f, 0.72f, 0.94f, 0.95f);
                sectorTexts[i].color = Color.black;
            }
            else
            {
                sectorImages[i].color = new Color(0.10f, 0.16f, 0.22f, 0.90f);
                sectorTexts[i].color = Color.white;
            }
        }
    }

    private void EquipOrUseSlotItem(int sectorIndex)
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (inventory == null) return;

        bool ok = inventory.UseHotbarSlot(sectorIndex);
        if (!ok && HUDSurvivalOverlay.Instance != null)
        {
            int itemId = sectorIndex < inventory.hotbarItemIds.Length ? inventory.hotbarItemIds[sectorIndex] : -1;
            ItemData d = itemId > 0 ? ItemDatabase.GetItem(itemId) : null;
            HUDSurvivalOverlay.Instance.ShowLootTooltip(d != null ? $"Falta na mochila: {d.itemName}" : "Slot rapido vazio");
        }
    }

    private void Perform180QuickTurn()
    {
        Transform playerTr = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        if (playerTr != null)
        {
            playerTr.Rotate(0, 180f, 0);
        }
        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.08f);
        }
    }
}
