using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HotbarHUD: Barra rapida unificada de 6 slots + badges de Mao Esquerda e Armadura.
/// Visivel e clicavel/tocavel tanto no PC ([1]..[6]) quanto no Mobile, com icones vetoriais profissionais (Zero-Emoji).
/// </summary>
public class HotbarHUD : MonoBehaviour
{
    public static HotbarHUD Instance { get; private set; }

    private const int SLOT_COUNT = 6;

    private RectTransform rootPanel;
    private Image[] slotFrames = new Image[SLOT_COUNT];
    private Outline[] slotOutlines = new Outline[SLOT_COUNT];
    private Image[] slotIcons = new Image[SLOT_COUNT];
    private Text[] slotQtyTexts = new Text[SLOT_COUNT];
    private Text[] slotKeyTexts = new Text[SLOT_COUNT];
    private Image[] slotDurabilityFills = new Image[SLOT_COUNT];

    private Image offHandIcon;
    private Text offHandText;
    private Image armorIcon;
    private Text armorText;

    private InventoryAdvanced inventoryCache;

    private static readonly Color FrameIdleColor = new Color(0.08f, 0.12f, 0.17f, 0.88f);
    private static readonly Color FrameActiveColor = new Color(0.14f, 0.30f, 0.42f, 0.96f);
    private static readonly Color OutlineIdleColor = new Color(0.30f, 0.65f, 0.85f, 0.35f);
    private static readonly Color OutlineActiveColor = new Color(0.38f, 0.90f, 1.0f, 0.95f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        BuildHotbarUI();
    }

    private void Start()
    {
        inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (inventoryCache != null)
        {
            inventoryCache.OnInventoryChanged += RefreshHotbar;
            inventoryCache.OnEquipmentChanged += RefreshHotbar;
        }
        RefreshHotbar();
    }

    private void OnDestroy()
    {
        if (inventoryCache != null)
        {
            inventoryCache.OnInventoryChanged -= RefreshHotbar;
            inventoryCache.OnEquipmentChanged -= RefreshHotbar;
        }
    }

    private void Update()
    {
        if (FragaInputRouter.Instance != null && FragaInputRouter.Instance.HotbarKeyPressedThisFrame >= 0)
        {
            int idx = FragaInputRouter.Instance.HotbarKeyPressedThisFrame;
            if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;
            if (inventoryCache != null)
            {
                inventoryCache.UseHotbarSlot(idx);
            }
        }
    }

    private void BuildHotbarUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("HotbarHUD_Root");
        if (existing != null) Destroy(existing.gameObject);

        GameObject rootObj = new GameObject("HotbarHUD_Root");
        rootObj.transform.SetParent(canvas.transform, false);
        rootPanel = rootObj.AddComponent<RectTransform>();
        rootPanel.anchorMin = new Vector2(0.5f, 0f);
        rootPanel.anchorMax = new Vector2(0.5f, 0f);
        rootPanel.pivot = new Vector2(0.5f, 0f);
        rootPanel.anchoredPosition = new Vector2(0f, 18f);
        rootPanel.sizeDelta = new Vector2(610f, 82f);

        Image panelBg = rootObj.AddComponent<Image>();
        panelBg.sprite = FragaProIconLibrary.GetPillSprite();
        panelBg.type = Image.Type.Sliced;
        panelBg.color = new Color(0.04f, 0.06f, 0.09f, 0.72f);
        panelBg.raycastTarget = false;

        float slotSize = 68f;
        float gap = 8f;
        float startX = -((SLOT_COUNT * slotSize + (SLOT_COUNT - 1) * gap) * 0.5f) - 52f + (slotSize * 0.5f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            int slotIndex = i;
            float x = startX + i * (slotSize + gap);

            GameObject slotObj = new GameObject($"HotbarSlot_{i + 1}");
            slotObj.transform.SetParent(rootPanel, false);
            RectTransform rt = slotObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(slotSize, slotSize);

            Image frame = slotObj.AddComponent<Image>();
            frame.sprite = FragaProIconLibrary.GetSlotFrameSprite();
            frame.type = Image.Type.Sliced;
            frame.color = FrameIdleColor;
            slotFrames[i] = frame;

            Outline outline = slotObj.AddComponent<Outline>();
            outline.effectColor = OutlineIdleColor;
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            slotOutlines[i] = outline;

            Button btn = slotObj.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;
                if (inventoryCache != null) inventoryCache.UseHotbarSlot(slotIndex);
            });

            // Icone vetorial
            GameObject icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(slotObj.transform, false);
            RectTransform icoRT = icoObj.AddComponent<RectTransform>();
            icoRT.anchorMin = new Vector2(0.5f, 0.5f);
            icoRT.anchorMax = new Vector2(0.5f, 0.5f);
            icoRT.sizeDelta = new Vector2(44f, 44f);
            Image icoImg = icoObj.AddComponent<Image>();
            icoImg.preserveAspect = true;
            icoImg.raycastTarget = false;
            icoImg.enabled = false;
            slotIcons[i] = icoImg;

            // Atalho 1..6
            GameObject keyObj = new GameObject("Key");
            keyObj.transform.SetParent(slotObj.transform, false);
            RectTransform kRT = keyObj.AddComponent<RectTransform>();
            kRT.anchorMin = new Vector2(0f, 1f);
            kRT.anchorMax = new Vector2(0f, 1f);
            kRT.pivot = new Vector2(0f, 1f);
            kRT.anchoredPosition = new Vector2(5f, -3f);
            kRT.sizeDelta = new Vector2(20f, 16f);
            Text kTxt = keyObj.AddComponent<Text>();
            kTxt.font = font;
            kTxt.fontSize = 11;
            kTxt.fontStyle = FontStyle.Bold;
            kTxt.color = new Color(0.68f, 0.85f, 0.95f, 0.9f);
            kTxt.text = (i + 1).ToString();
            kTxt.raycastTarget = false;
            slotKeyTexts[i] = kTxt;

            // Quantidade
            GameObject qtyObj = new GameObject("Qty");
            qtyObj.transform.SetParent(slotObj.transform, false);
            RectTransform qRT = qtyObj.AddComponent<RectTransform>();
            qRT.anchorMin = new Vector2(1f, 0f);
            qRT.anchorMax = new Vector2(1f, 0f);
            qRT.pivot = new Vector2(1f, 0f);
            qRT.anchoredPosition = new Vector2(-5f, 4f);
            qRT.sizeDelta = new Vector2(40f, 18f);
            Text qTxt = qtyObj.AddComponent<Text>();
            qTxt.font = font;
            qTxt.fontSize = 12;
            qTxt.fontStyle = FontStyle.Bold;
            qTxt.alignment = TextAnchor.LowerRight;
            qTxt.color = Color.white;
            qTxt.raycastTarget = false;
            Shadow sh = qtyObj.AddComponent<Shadow>();
            sh.effectColor = Color.black;
            sh.effectDistance = new Vector2(1f, -1f);
            slotQtyTexts[i] = qTxt;

            // Barra de durabilidade/perecibilidade
            GameObject durBg = new GameObject("DurBG");
            durBg.transform.SetParent(slotObj.transform, false);
            RectTransform dbRT = durBg.AddComponent<RectTransform>();
            dbRT.anchorMin = new Vector2(0.1f, 0f);
            dbRT.anchorMax = new Vector2(0.9f, 0f);
            dbRT.pivot = new Vector2(0.5f, 0f);
            dbRT.anchoredPosition = new Vector2(0f, 3f);
            dbRT.sizeDelta = new Vector2(0f, 4f);
            Image dbImg = durBg.AddComponent<Image>();
            dbImg.color = new Color(0f, 0f, 0f, 0.55f);
            dbImg.raycastTarget = false;

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
            dfImg.color = new Color(0.35f, 0.92f, 0.45f, 1f);
            dfImg.raycastTarget = false;
            durBg.SetActive(false);
            slotDurabilityFills[i] = dfImg;
        }

        // Mini-badges de Mao Esquerda e Armadura a direita da hotbar
        float gearX = startX + SLOT_COUNT * (slotSize + gap) + 18f;
        CreateGearBadge(rootPanel, "OffHandBadge", new Vector2(gearX, 17f), font, out offHandIcon, out offHandText);
        CreateGearBadge(rootPanel, "ArmorBadge", new Vector2(gearX, -17f), font, out armorIcon, out armorText);
    }

    private void CreateGearBadge(Transform parent, string name, Vector2 pos, Font font, out Image iconImg, out Text labelTxt)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(96f, 28f);

        Image bg = obj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.08f, 0.13f, 0.18f, 0.85f);
        bg.raycastTarget = false;

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(obj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(5f, 0f);
        iRT.sizeDelta = new Vector2(20f, 20f);
        iconImg = icoObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.offsetMin = new Vector2(28f, 0f);
        tRT.offsetMax = new Vector2(-4f, 0f);
        labelTxt = txtObj.AddComponent<Text>();
        labelTxt.font = font;
        labelTxt.fontSize = 10;
        labelTxt.fontStyle = FontStyle.Bold;
        labelTxt.alignment = TextAnchor.MiddleLeft;
        labelTxt.color = new Color(0.85f, 0.92f, 0.98f, 0.95f);
        labelTxt.raycastTarget = false;
    }

    public void RefreshHotbar()
    {
        if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;
        if (inventoryCache == null) return;

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            int itemId = (i < inventoryCache.hotbarItemIds.Length) ? inventoryCache.hotbarItemIds[i] : -1;
            int count = itemId > 0 ? inventoryCache.GetItemCount(itemId) : 0;
            bool isEquipped = itemId > 0 && (inventoryCache.equippedMainHandId == itemId || inventoryCache.equippedOffHandId == itemId || inventoryCache.equippedArmorId == itemId);
            bool isActive = (i == inventoryCache.activeHotbarIndex) || isEquipped;

            slotFrames[i].color = isActive ? FrameActiveColor : FrameIdleColor;
            slotOutlines[i].effectColor = isActive ? OutlineActiveColor : OutlineIdleColor;

            if (itemId > 0)
            {
                slotIcons[i].enabled = true;
                slotIcons[i].sprite = FragaProIconLibrary.GetItemIcon(itemId);
                slotIcons[i].color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.28f);
                slotQtyTexts[i].text = count > 1 ? $"x{count}" : (count == 1 ? "" : "0");
                slotQtyTexts[i].color = count > 0 ? Color.white : new Color(0.9f, 0.35f, 0.35f, 0.8f);

                // Durabilidade do primeiro slot desse item
                ItemData data = ItemDatabase.GetItem(itemId);
                int slotIdx = inventoryCache.FindFirstSlotIndex(itemId);
                if (data != null && data.isPerishable && slotIdx >= 0 && data.maxDurability > 0f)
                {
                    float frac = Mathf.Clamp01(inventoryCache.slots[slotIdx].durability / data.maxDurability);
                    slotDurabilityFills[i].transform.parent.gameObject.SetActive(true);
                    slotDurabilityFills[i].fillAmount = frac;
                    slotDurabilityFills[i].color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.35f, 0.92f, 0.45f), frac);
                }
                else
                {
                    slotDurabilityFills[i].transform.parent.gameObject.SetActive(false);
                }
            }
            else
            {
                slotIcons[i].enabled = false;
                slotQtyTexts[i].text = "";
                slotDurabilityFills[i].transform.parent.gameObject.SetActive(false);
            }
        }

        // Mao esquerda badge
        if (offHandIcon != null && offHandText != null)
        {
            if (inventoryCache.equippedOffHandId > 0)
            {
                ItemData oh = ItemDatabase.GetItem(inventoryCache.equippedOffHandId);
                offHandIcon.enabled = true;
                offHandIcon.sprite = FragaProIconLibrary.GetItemIcon(inventoryCache.equippedOffHandId);
                offHandText.text = oh != null ? oh.itemName.Split(' ')[0].ToUpper() : "MAO ESQ";
            }
            else
            {
                offHandIcon.enabled = true;
                offHandIcon.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.ShieldBlock);
                offHandText.text = "MAO ESQ";
            }
        }

        // Armadura badge
        if (armorIcon != null && armorText != null)
        {
            if (inventoryCache.equippedArmorId > 0)
            {
                ItemData ar = ItemDatabase.GetItem(inventoryCache.equippedArmorId);
                armorIcon.enabled = true;
                armorIcon.sprite = FragaProIconLibrary.GetItemIcon(inventoryCache.equippedArmorId);
                armorText.text = ar != null ? "CASACO" : "PELE";
            }
            else
            {
                armorIcon.enabled = false;
                armorText.text = "SEM PELE";
            }
        }
    }
}
