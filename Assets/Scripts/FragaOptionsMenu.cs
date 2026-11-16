using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// FragaOptionsMenu: Modal de Pausa e Opcoes em Runtime (Mobile + Desktop, Zero-Emoji).
/// Permite ajustar Volume, Qualidade Grafica, Sensibilidade da Camera, Layout de Controles e Salvar.
/// </summary>
public class FragaOptionsMenu : MonoBehaviour
{
    public static FragaOptionsMenu Instance { get; private set; }

    public bool IsOpen { get; private set; }

    private GameObject hudBtnObj;
    private GameObject modalRootObj;

    private Text volumeValText;
    private Image volumeFillImg;
    private Text sensValText;

    private Image[] qualityBtnBgs = new Image[3];
    private Image[] layoutBtnBgs = new Image[3];
    private Text saveStatusText;

    private bool wasOtherModalOrBuildOpen = false;
    private float savedTimeScaleBeforePause = 1f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        BuildUI();
        CloseImmediate();
    }

    private void Update()
    {
        bool otherModalOpen = SurvivalMapSystem.IsMapOpen ||
                              (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen) ||
                              (RadialQuickSelectUI.Instance != null && RadialQuickSelectUI.Instance.IsOpen) ||
                              (BlueprintPlacementManager.Instance != null && BlueprintPlacementManager.Instance.IsPlacingGhost) ||
                              (BuildingSystem.Instance != null && BuildingSystem.Instance.IsBuildModeActive) ||
                              StorageChestInteractable.IsAnyChestOpen;

        if (hudBtnObj != null)
        {
            bool shouldShowHudBtn = !IsOpen && !otherModalOpen;
            if (hudBtnObj.activeSelf != shouldShowHudBtn) hudBtnObj.SetActive(shouldShowHudBtn);
        }

        if (Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.O))
        {
            if (IsOpen) Close();
            else if (!otherModalOpen) Open();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsOpen)
            {
                Close();
            }
            else if (!otherModalOpen && !wasOtherModalOrBuildOpen)
            {
                Open();
            }
        }

        wasOtherModalOrBuildOpen = otherModalOpen;
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        savedTimeScaleBeforePause = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        if (modalRootObj != null)
        {
            modalRootObj.SetActive(true);
            modalRootObj.transform.SetAsLastSibling();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (saveStatusText != null) saveStatusText.text = string.Empty;
        RefreshReadouts();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (modalRootObj != null) modalRootObj.SetActive(false);
        Time.timeScale = savedTimeScaleBeforePause;

        bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
        if (!touchActive && Input.touchCount == 0 && IsDesktopPlatform())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void CloseImmediate()
    {
        IsOpen = false;
        if (modalRootObj != null) modalRootObj.SetActive(false);
    }

    private static bool IsDesktopPlatform()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer ||
               Application.platform == RuntimePlatform.WindowsEditor ||
               Application.platform == RuntimePlatform.OSXPlayer ||
               Application.platform == RuntimePlatform.OSXEditor ||
               Application.platform == RuntimePlatform.LinuxPlayer;
    }

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. Botao discreto no canto superior direito abaixo do relogio (PC + Mobile)
        hudBtnObj = new GameObject("Btn_HUD_Options");
        hudBtnObj.transform.SetParent(parent, false);
        RectTransform btnRT = hudBtnObj.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1f, 1f);
        btnRT.anchorMax = new Vector2(1f, 1f);
        btnRT.pivot = new Vector2(1f, 1f);
        btnRT.anchoredPosition = new Vector2(-25f, -68f);
        btnRT.sizeDelta = new Vector2(124f, 30f);

        Image btnBg = hudBtnObj.AddComponent<Image>();
        btnBg.sprite = FragaProIconLibrary.GetPillSprite();
        btnBg.type = Image.Type.Sliced;
        btnBg.color = new Color(0.06f, 0.09f, 0.13f, 0.82f);

        Outline btnOut = hudBtnObj.AddComponent<Outline>();
        btnOut.effectColor = new Color(0.38f, 0.72f, 0.90f, 0.50f);
        btnOut.effectDistance = new Vector2(1f, -1f);

        hudBtnObj.AddComponent<Button>().onClick.AddListener(Toggle);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(hudBtnObj.transform, false);
        RectTransform icoRT = icoObj.AddComponent<RectTransform>();
        icoRT.anchorMin = new Vector2(0f, 0.5f);
        icoRT.anchorMax = new Vector2(0f, 0.5f);
        icoRT.pivot = new Vector2(0f, 0.5f);
        icoRT.anchoredPosition = new Vector2(9f, 0f);
        icoRT.sizeDelta = new Vector2(16f, 16f);
        Image icoImg = icoObj.AddComponent<Image>();
        icoImg.sprite = FragaProIconLibrary.GetControlIcon(FragaProIconLibrary.ControlIcon.PauseMenu);
        icoImg.preserveAspect = true;
        icoImg.raycastTarget = false;

        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(hudBtnObj.transform, false);
        RectTransform lblRT = lblObj.AddComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(28f, 0f);
        lblRT.offsetMax = new Vector2(-6f, 0f);
        Text lblTxt = lblObj.AddComponent<Text>();
        lblTxt.font = font;
        lblTxt.fontSize = 11;
        lblTxt.fontStyle = FontStyle.Bold;
        lblTxt.alignment = TextAnchor.MiddleCenter;
        lblTxt.color = new Color(0.90f, 0.95f, 1f, 0.95f);
        lblTxt.text = "OPCOES [F10]";
        lblTxt.raycastTarget = false;

        // 2. Overlay Modal de Opcoes & Pausa
        modalRootObj = new GameObject("FragaOptionsModal");
        modalRootObj.transform.SetParent(parent, false);
        RectTransform rootRT = modalRootObj.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dimBg = modalRootObj.AddComponent<Image>();
        dimBg.color = new Color(0.01f, 0.02f, 0.04f, 0.78f);
        dimBg.raycastTarget = true;

        GameObject card = new GameObject("OptionsCard");
        card.transform.SetParent(modalRootObj.transform, false);
        RectTransform cardRT = card.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(540f, 470f);

        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.06f, 0.09f, 0.13f, 0.96f);
        Outline cardOut = card.AddComponent<Outline>();
        cardOut.effectColor = new Color(0.35f, 0.80f, 0.95f, 0.65f);
        cardOut.effectDistance = new Vector2(1.5f, -1.5f);

        CreateHeaderText(card.transform, font, "PAUSA & OPCOES", new Vector2(0f, 198f), 22, new Color(0.92f, 0.96f, 1f));

        // Linha 1: Volume
        CreateLabelText(card.transform, font, "VOLUME GERAL", new Vector2(-140f, 138f));
        CreateStepButton(card.transform, font, "-", new Vector2(20f, 138f), () => AdjustVolume(-0.1f));

        GameObject volBarBg = new GameObject("VolBar");
        volBarBg.transform.SetParent(card.transform, false);
        RectTransform vbRT = volBarBg.AddComponent<RectTransform>();
        vbRT.anchoredPosition = new Vector2(105f, 138f);
        vbRT.sizeDelta = new Vector2(115f, 16f);
        volBarBg.AddComponent<Image>().color = new Color(0.15f, 0.18f, 0.24f, 1f);

        GameObject volFill = new GameObject("Fill");
        volFill.transform.SetParent(volBarBg.transform, false);
        RectTransform vfRT = volFill.AddComponent<RectTransform>();
        vfRT.anchorMin = Vector2.zero;
        vfRT.anchorMax = Vector2.one;
        vfRT.offsetMin = Vector2.zero;
        vfRT.offsetMax = Vector2.zero;
        volumeFillImg = volFill.AddComponent<Image>();
        volumeFillImg.type = Image.Type.Filled;
        volumeFillImg.fillMethod = Image.FillMethod.Horizontal;
        volumeFillImg.color = new Color(0.35f, 0.85f, 0.95f, 1f);

        CreateStepButton(card.transform, font, "+", new Vector2(188f, 138f), () => AdjustVolume(0.1f));
        volumeValText = CreateValueText(card.transform, font, "100%", new Vector2(232f, 138f));

        // Linha 2: Qualidade Grafica
        CreateLabelText(card.transform, font, "QUALIDADE", new Vector2(-140f, 82f));
        qualityBtnBgs[0] = CreateChoiceButton(card.transform, font, "BAIXO", new Vector2(36f, 82f), 84f, () => SetQuality(0));
        qualityBtnBgs[1] = CreateChoiceButton(card.transform, font, "MEDIO", new Vector2(126f, 82f), 84f, () => SetQuality(1));
        qualityBtnBgs[2] = CreateChoiceButton(card.transform, font, "ALTO", new Vector2(216f, 82f), 84f, () => SetQuality(2));

        // Linha 3: Sensibilidade
        CreateLabelText(card.transform, font, "SENSIBILIDADE", new Vector2(-140f, 26f));
        CreateStepButton(card.transform, font, "-", new Vector2(20f, 26f), () => AdjustSensitivity(-0.25f));
        sensValText = CreateValueText(card.transform, font, "1.6x", new Vector2(105f, 26f));
        CreateStepButton(card.transform, font, "+", new Vector2(188f, 26f), () => AdjustSensitivity(0.25f));

        // Linha 4: Controles
        CreateLabelText(card.transform, font, "CONTROLES", new Vector2(-140f, -30f));
        layoutBtnBgs[0] = CreateChoiceButton(card.transform, font, "AUTO", new Vector2(36f, -30f), 84f, () => SetControlLayout(0));
        layoutBtnBgs[1] = CreateChoiceButton(card.transform, font, "MOBILE", new Vector2(126f, -30f), 84f, () => SetControlLayout(1));
        layoutBtnBgs[2] = CreateChoiceButton(card.transform, font, "PC", new Vector2(216f, -30f), 84f, () => SetControlLayout(2));

        // Botoes de acao
        CreateActionButton(card.transform, font, "SALVAR PROGRESSO", new Vector2(0f, -96f), new Vector2(360f, 40f),
            new Color(0.16f, 0.34f, 0.24f, 1f), OnSaveClicked);

        saveStatusText = CreateHeaderText(card.transform, font, "", new Vector2(0f, -128f), 12, new Color(0.55f, 0.95f, 0.65f));

        CreateActionButton(card.transform, font, "RETOMAR JOGO", new Vector2(-95f, -172f), new Vector2(175f, 42f),
            new Color(0.15f, 0.32f, 0.45f, 1f), Close);

        CreateActionButton(card.transform, font, "MENU PRINCIPAL", new Vector2(95f, -172f), new Vector2(175f, 42f),
            new Color(0.36f, 0.14f, 0.14f, 1f), ReturnToMainMenu);
    }

    private void AdjustVolume(float delta)
    {
        float next = Mathf.Clamp01(AudioListener.volume + delta);
        if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(next);
        else
        {
            AudioListener.volume = next;
            PlayerPrefs.SetFloat("Fraga_MasterVolume", next);
            PlayerPrefs.Save();
        }
        RefreshReadouts();
    }

    private void SetQuality(int tier)
    {
        if (GalicianOptimizationManager.Instance != null)
        {
            GalicianOptimizationManager.Instance.ApplyQualityPreset(tier);
        }
        else
        {
            PlayerPrefs.SetInt(GalicianOptimizationManager.QualityPrefKey, tier);
            PlayerPrefs.Save();
        }
        RefreshReadouts();
    }

    private void AdjustSensitivity(float delta)
    {
        FirstPersonCamera fp = FirstPersonCamera.Instance ?? FindAnyObjectByType<FirstPersonCamera>();
        float curMouse = fp != null ? fp.mouseSensitivity : PlayerPrefs.GetFloat("Fraga_MouseSensitivity", 1.6f);
        float nextMouse = Mathf.Clamp(curMouse + delta, 0.3f, 6.0f);
        float nextTouch = Mathf.Clamp(nextMouse * (0.22f / 1.6f), 0.05f, 1.0f);

        if (fp != null) fp.SetSensitivity(nextMouse, nextTouch);
        else
        {
            PlayerPrefs.SetFloat("Fraga_MouseSensitivity", nextMouse);
            PlayerPrefs.SetFloat("Fraga_TouchSensitivity", nextTouch);
            PlayerPrefs.Save();
        }
        RefreshReadouts();
    }

    private void SetControlLayout(int mode)
    {
        if (FragaInputRouter.Instance != null)
        {
            FragaInputRouter.Instance.SetControlLayoutMode((FragaInputRouter.ControlLayoutMode)mode);
        }
        else
        {
            PlayerPrefs.SetInt("Fraga_ControlLayoutMode", mode);
            PlayerPrefs.Save();
        }
        RefreshReadouts();
    }

    private void OnSaveClicked()
    {
        SaveSystem saveSys = FindAnyObjectByType<SaveSystem>();
        if (saveSys != null)
        {
            saveSys.SaveGame();
            if (saveStatusText != null) saveStatusText.text = "Progresso salvo com sucesso!";
        }
        else
        {
            if (saveStatusText != null) saveStatusText.text = "SaveSystem indisponivel nesta cena.";
        }
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void RefreshReadouts()
    {
        float vol = AudioListener.volume;
        if (volumeValText != null) volumeValText.text = $"{Mathf.RoundToInt(vol * 100f)}%";
        if (volumeFillImg != null) volumeFillImg.fillAmount = vol;

        float sens = FirstPersonCamera.Instance != null
            ? FirstPersonCamera.Instance.mouseSensitivity
            : PlayerPrefs.GetFloat("Fraga_MouseSensitivity", 1.6f);
        if (sensValText != null) sensValText.text = $"{sens:0.00}x";

        int qTier = GalicianOptimizationManager.Instance != null
            ? (int)GalicianOptimizationManager.Instance.CurrentQualityTier
            : PlayerPrefs.GetInt(GalicianOptimizationManager.QualityPrefKey, 1);

        Color activeCol = new Color(0.20f, 0.48f, 0.65f, 1f);
        Color inactiveCol = new Color(0.14f, 0.17f, 0.22f, 1f);
        for (int i = 0; i < 3; i++)
        {
            if (qualityBtnBgs[i] != null) qualityBtnBgs[i].color = (i == qTier) ? activeCol : inactiveCol;
        }

        int layout = FragaInputRouter.Instance != null
            ? (int)FragaInputRouter.Instance.LayoutMode
            : PlayerPrefs.GetInt("Fraga_ControlLayoutMode", 0);
        for (int i = 0; i < 3; i++)
        {
            if (layoutBtnBgs[i] != null) layoutBtnBgs[i].color = (i == layout) ? activeCol : inactiveCol;
        }
    }

    private static Text CreateHeaderText(Transform parent, Font font, string text, Vector2 pos, int size, Color color)
    {
        GameObject obj = new GameObject("Text_" + text);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(480f, 36f);
        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = text;
        return t;
    }

    private static void CreateLabelText(Transform parent, Font font, string text, Vector2 pos)
    {
        GameObject obj = new GameObject("Lbl_" + text);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(210f, 32f);
        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 14;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = new Color(0.84f, 0.89f, 0.96f);
        t.text = text;
    }

    private static Text CreateValueText(Transform parent, Font font, string text, Vector2 pos)
    {
        GameObject obj = new GameObject("Val");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(72f, 32f);
        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 14;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.95f, 0.97f, 1f);
        t.text = text;
        return t;
    }

    private static void CreateStepButton(Transform parent, Font font, string symbol, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject("Step_" + symbol);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(36f, 30f);
        obj.AddComponent<Image>().color = new Color(0.16f, 0.22f, 0.30f, 1f);
        obj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject txObj = new GameObject("Txt");
        txObj.transform.SetParent(obj.transform, false);
        RectTransform trt = txObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        Text t = txObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 18;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = symbol;
    }

    private static Image CreateChoiceButton(Transform parent, Font font, string label, Vector2 pos, float width, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject("Choice_" + label);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 32f);
        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.14f, 0.17f, 0.22f, 1f);
        obj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject txObj = new GameObject("Txt");
        txObj.transform.SetParent(obj.transform, false);
        RectTransform trt = txObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        Text t = txObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 12;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        return bg;
    }

    private static void CreateActionButton(Transform parent, Font font, string label, Vector2 pos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject("Action_" + label);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        obj.AddComponent<Image>().color = bgColor;
        Outline o = obj.AddComponent<Outline>();
        o.effectColor = new Color(1f, 1f, 1f, 0.28f);
        o.effectDistance = new Vector2(1f, -1f);
        obj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject txObj = new GameObject("Txt");
        txObj.transform.SetParent(obj.transform, false);
        RectTransform trt = txObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        Text t = txObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 13;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
    }
}
