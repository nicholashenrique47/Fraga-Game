using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controlador de Joystick Virtual Mobile Hibrido AAA (Padrao COD Mobile / Wild Rift / Genshin Impact).
/// Suporta:
/// - Modo Hibrido: Dynamic Floating Follow (acompanha o polegar para resposta instantanea ao virar), Fixo ou Centralizado.
/// - Trava de Corrida (Sprint Lock): empurrar alem do limite superior ou manter impulso total para frente ativa corrida continua.
/// - Isolamento real de UI: raycast Zero-GC permite tocar na Joystick_TouchZone sem roubar toques de botoes da interface (Hotbar, Obra, Acao).
/// - Conversao exata de coordenadas Screen -> AnchoredPosition independente de ancora/CanvasScaler.
/// </summary>
public class MobileInputController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public static MobileInputController Instance { get; private set; }

    public enum JoystickType
    {
        DynamicFloatingFollow, // Recomendado: base surge no polegar e acompanha o arrasto alem do raio
        Fixed,                 // Base fixa na posicao padrao
        DynamicCentering       // Base surge no toque e fica estatica
    }

    [Header("Joystick Mode")]
    public JoystickType joystickType = JoystickType.DynamicFloatingFollow;

    [Header("Joystick Visuals")]
    public RectTransform joystickBackground;
    public RectTransform joystickHandle;
    public RectTransform sprintLockIndicator;

    [Header("Dimensions & Physics")]
    public float joystickRadius = 75f;
    public float deadZone = 6f;
    public float sprintLockThresholdMultiplier = 1.35f;
    public float sprintLockHoldDuration = 0.55f;

    [Header("Visual Feedback & Opacity")]
    public float idleAlpha = 0.35f;
    public float activeAlpha = 0.85f;
    public Color handleNormalColor = new Color(0.92f, 0.95f, 1.0f, 0.85f);
    public Color handleSprintColor = new Color(0.30f, 0.95f, 1.0f, 1f);
    public Color sprintLockActiveColor = new Color(0.30f, 1.0f, 0.60f, 1f);

    private Vector2 inputVector = Vector2.zero;
    private bool isDragging = false;
    private int activeFingerId = -1;
    private Vector2 touchStartScreenPos;
    private Vector2 dragOriginScreenPos;
    private Vector2 defaultAnchoredPos = new Vector2(160f, 160f);
    private Canvas parentCanvas;
    private RectTransform parentRectTransform;
    private CanvasGroup joystickCanvasGroup;

    private Image handleImage;
    private Image bgImage;
    private Image sprintIndicatorImage;
    private Text sprintIndicatorText;

    private bool isSprintLocked = false;
    private float forwardSprintTimer = 0f;

    private readonly List<RaycastResult> uiRaycastHits = new List<RaycastResult>(8);
    private PointerEventData cachedPointerData;

    private static Sprite s_ringSprite;
    private static Sprite s_handleSprite;
    private static Sprite s_arrowSprite;

    public bool IsDragging => isDragging;
    public bool IsSprintLocked => isSprintLocked;

    /// <summary>
    /// Retorna verdadeiro se o analogico estiver na faixa de corrida ou com Sprint Lock ativado.
    /// </summary>
    public bool IsSprinting => isSprintLocked || (inputVector.y > 0.62f && inputVector.magnitude > 0.78f);

    public bool dynamicCentering
    {
        get => joystickType != JoystickType.Fixed;
        set => joystickType = value ? JoystickType.DynamicFloatingFollow : JoystickType.Fixed;
    }

    public bool floatingJoystick
    {
        get => joystickType == JoystickType.DynamicFloatingFollow;
        set => joystickType = value ? JoystickType.DynamicFloatingFollow : JoystickType.Fixed;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        LoadPreferences();
        FindCanvasAndVisuals();
    }

    private void Start()
    {
        FindCanvasAndVisuals();
        ConfigureVisualStyles();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LoadPreferences()
    {
        if (PlayerPrefs.HasKey("Fraga_JoystickMode"))
        {
            int modeIdx = PlayerPrefs.GetInt("Fraga_JoystickMode", 0);
            if (modeIdx >= 0 && modeIdx <= 2)
            {
                joystickType = (JoystickType)modeIdx;
            }
        }
    }

    public void SetJoystickMode(JoystickType mode)
    {
        joystickType = mode;
        PlayerPrefs.SetInt("Fraga_JoystickMode", (int)mode);
        PlayerPrefs.Save();
        EndDrag();
        if (joystickBackground != null && mode == JoystickType.Fixed)
        {
            joystickBackground.anchoredPosition = defaultAnchoredPos;
        }
    }

    public void RefreshReferences()
    {
        FindCanvasAndVisuals();
        ConfigureVisualStyles();
    }

    private void FindCanvasAndVisuals()
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) parentCanvas = HUDSurvivalOverlay.GetGameplayCanvas();

        if (joystickBackground == null)
        {
            GameObject bgObj = GameObject.Find("Joystick_BG");
            if (bgObj != null) joystickBackground = bgObj.GetComponent<RectTransform>();
            else joystickBackground = GetComponent<RectTransform>();
        }

        if (joystickBackground != null)
        {
            Vector2 currentAnchor = joystickBackground.anchoredPosition;
            defaultAnchoredPos = currentAnchor.sqrMagnitude < 10f ? new Vector2(160f, 160f) : currentAnchor;
            parentRectTransform = joystickBackground.parent as RectTransform;
            if (parentRectTransform == null && parentCanvas != null)
            {
                parentRectTransform = parentCanvas.GetComponent<RectTransform>();
            }

            joystickCanvasGroup = joystickBackground.GetComponent<CanvasGroup>();
            if (joystickCanvasGroup == null)
            {
                joystickCanvasGroup = joystickBackground.gameObject.AddComponent<CanvasGroup>();
            }
            joystickCanvasGroup.alpha = idleAlpha;
            joystickCanvasGroup.blocksRaycasts = false;

            if (joystickHandle == null)
            {
                Transform handle = joystickBackground.Find("Joystick_Handle");
                if (handle != null) joystickHandle = handle as RectTransform;
            }

            if (sprintLockIndicator == null)
            {
                Transform lockInd = joystickBackground.Find("Sprint_Lock_Indicator");
                if (lockInd != null) sprintLockIndicator = lockInd as RectTransform;
            }
        }
    }

    private void ConfigureVisualStyles()
    {
        if (s_ringSprite == null) s_ringSprite = CreateRingTexture(128);
        if (s_handleSprite == null) s_handleSprite = CreateHandleTexture(96);
        if (s_arrowSprite == null) s_arrowSprite = CreateArrowTexture(64);

        if (joystickBackground != null)
        {
            bgImage = joystickBackground.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.sprite = s_ringSprite;
                bgImage.color = new Color(0.10f, 0.16f, 0.22f, 0.65f);
                bgImage.raycastTarget = false;
            }

            if (sprintLockIndicator == null)
            {
                GameObject lockObj = new GameObject("Sprint_Lock_Indicator");
                lockObj.transform.SetParent(joystickBackground, false);
                sprintLockIndicator = lockObj.AddComponent<RectTransform>();
                sprintLockIndicator.sizeDelta = new Vector2(36, 36);
                sprintLockIndicator.anchoredPosition = new Vector2(0, joystickRadius * 1.35f);

                sprintIndicatorImage = lockObj.AddComponent<Image>();
                sprintIndicatorImage.sprite = s_arrowSprite;
                sprintIndicatorImage.color = new Color(0.4f, 0.8f, 1.0f, 0.35f);
                sprintIndicatorImage.raycastTarget = false;

                GameObject lockTextObj = new GameObject("Text");
                lockTextObj.transform.SetParent(lockObj.transform, false);
                RectTransform textRT = lockTextObj.AddComponent<RectTransform>();
                textRT.sizeDelta = new Vector2(88, 20);
                textRT.anchoredPosition = new Vector2(0, 22);

                sprintIndicatorText = lockTextObj.AddComponent<Text>();
                sprintIndicatorText.text = "CORRER";
                sprintIndicatorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                sprintIndicatorText.fontSize = 11;
                sprintIndicatorText.fontStyle = FontStyle.Bold;
                sprintIndicatorText.alignment = TextAnchor.MiddleCenter;
                sprintIndicatorText.color = new Color(0.4f, 0.8f, 1.0f, 0.35f);
                sprintIndicatorText.raycastTarget = false;
            }
            else
            {
                sprintIndicatorImage = sprintLockIndicator.GetComponent<Image>();
                sprintIndicatorText = sprintLockIndicator.GetComponentInChildren<Text>();
            }
        }

        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
            handleImage = joystickHandle.GetComponent<Image>();
            if (handleImage != null)
            {
                handleImage.sprite = s_handleSprite;
                handleImage.raycastTarget = false;
                handleImage.color = handleNormalColor;
            }
        }
    }

    private void Update()
    {
        bool blockedByContext = FragaInputRouter.Instance != null &&
                                (!FragaInputRouter.Instance.IsTouchControlsActive ||
                                 (!FragaInputRouter.Instance.IsGameplayAllowed &&
                                  FragaInputRouter.Instance.CurrentContext != FragaInputRouter.InputContext.BuildPlacement));

        if (blockedByContext)
        {
            if (isDragging) EndDrag();
        }
        else if (Input.touchCount > 0)
        {
            ProcessNativeTouches();
        }

        float dt = Time.unscaledDeltaTime;

        // Transicao suave de opacidade
        if (joystickCanvasGroup != null)
        {
            float targetAlpha = isDragging ? activeAlpha : idleAlpha;
            joystickCanvasGroup.alpha = Mathf.Lerp(joystickCanvasGroup.alpha, targetAlpha, dt * 12f);
        }

        // Retorno elastico (Spring-back) do manipulo e base
        if (!isDragging && joystickHandle != null)
        {
            if (joystickHandle.anchoredPosition.sqrMagnitude > 0.01f)
            {
                joystickHandle.anchoredPosition = Vector2.Lerp(joystickHandle.anchoredPosition, Vector2.zero, dt * 24f);
            }
            if (joystickBackground != null && (joystickBackground.anchoredPosition - defaultAnchoredPos).sqrMagnitude > 0.1f)
            {
                joystickBackground.anchoredPosition = Vector2.Lerp(joystickBackground.anchoredPosition, defaultAnchoredPos, dt * 10f);
            }
        }

        // Feedback visual dinamico do manipulo e do Sprint Lock
        if (handleImage != null)
        {
            Color targetCol = IsSprinting ? (isSprintLocked ? sprintLockActiveColor : handleSprintColor) : handleNormalColor;
            handleImage.color = Color.Lerp(handleImage.color, targetCol, dt * 12f);
        }

        if (sprintIndicatorImage != null)
        {
            Color targetLockCol = isSprintLocked
                ? sprintLockActiveColor
                : (IsSprinting ? handleSprintColor : new Color(0.4f, 0.8f, 1.0f, 0.35f));
            sprintIndicatorImage.color = Color.Lerp(sprintIndicatorImage.color, targetLockCol, dt * 12f);
            if (sprintIndicatorText != null)
            {
                sprintIndicatorText.color = sprintIndicatorImage.color;
                sprintIndicatorText.text = isSprintLocked ? "TRAVA CORRER" : "CORRER";
            }
        }
    }

    private void ProcessNativeTouches()
    {
        float maxLeftAreaX = Screen.width * 0.48f;
        float maxLeftAreaY = Screen.height * 0.72f;

        if (activeFingerId == -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                if (t.position.x > maxLeftAreaX || t.position.y > maxLeftAreaY) continue;
                if (IsTouchOverBlockingUI(t.position)) continue;

                BeginDragAt(t.fingerId, t.position);
                break;
            }
        }
        else
        {
            bool foundActiveFinger = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId == activeFingerId)
                {
                    foundActiveFinger = true;
                    if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                    {
                        UpdateDragAt(t.position);
                    }
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        EndDrag();
                    }
                    break;
                }
            }

            if (!foundActiveFinger)
            {
                EndDrag();
            }
        }
    }

    private bool IsTouchOverBlockingUI(Vector2 screenPos)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        if (cachedPointerData == null) cachedPointerData = new PointerEventData(es);
        cachedPointerData.position = screenPos;
        uiRaycastHits.Clear();
        es.RaycastAll(cachedPointerData, uiRaycastHits);

        for (int i = 0; i < uiRaycastHits.Count; i++)
        {
            GameObject hitGo = uiRaycastHits[i].gameObject;
            if (hitGo == null || hitGo == gameObject) continue;
            if (hitGo.name == "Joystick_TouchZone" || hitGo.name == "CameraTouchZone") continue;
            if (joystickBackground != null && (hitGo == joystickBackground.gameObject || hitGo.transform.IsChildOf(joystickBackground))) continue;

            return true;
        }
        return false;
    }

    private Camera GetUICamera()
    {
        return (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;
    }

    private float GetCanvasScaleFactor()
    {
        return (parentCanvas != null && parentCanvas.scaleFactor > 0.01f) ? parentCanvas.scaleFactor : 1.0f;
    }

    private Vector2 ScreenToTargetAnchoredPos(Vector2 screenPos, out Vector2 unclampedAnchoredPos)
    {
        if (joystickBackground == null || parentRectTransform == null)
        {
            unclampedAnchoredPos = Vector2.zero;
            return Vector2.zero;
        }

        Camera uiCam = GetUICamera();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRectTransform, screenPos, uiCam, out Vector2 localPoint))
        {
            unclampedAnchoredPos = joystickBackground.anchoredPosition;
            return joystickBackground.anchoredPosition;
        }

        Rect r = parentRectTransform.rect;
        if (r.width < 10f || r.height < 10f)
        {
            r = new Rect(-960f, -540f, 1920f, 1080f);
        }

        Vector2 anchorMid = (joystickBackground.anchorMin + joystickBackground.anchorMax) * 0.5f;
        Vector2 anchorOrigin = new Vector2(r.xMin + r.width * anchorMid.x, r.yMin + r.height * anchorMid.y);

        unclampedAnchoredPos = localPoint - anchorOrigin;
        return ClampAnchoredToLeftZone(unclampedAnchoredPos, r, anchorOrigin);
    }

    private Vector2 ClampAnchoredToLeftZone(Vector2 anchoredPos, Rect parentRect, Vector2 anchorOrigin)
    {
        Vector2 localPoint = anchoredPos + anchorOrigin;
        float pad = Mathf.Max(24f, joystickRadius + 14f);
        float minX = parentRect.xMin + pad;
        float maxX = Mathf.Max(minX, parentRect.xMin + parentRect.width * 0.46f - joystickRadius * 0.4f);
        float minY = parentRect.yMin + pad;
        float maxY = Mathf.Max(minY, parentRect.yMin + parentRect.height * 0.66f - joystickRadius * 0.4f);

        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);
        return localPoint - anchorOrigin;
    }

    public void BeginDragAt(int fingerId, Vector2 screenPos)
    {
        if (parentRectTransform == null || joystickBackground == null) FindCanvasAndVisuals();

        activeFingerId = fingerId;
        isDragging = true;
        isSprintLocked = false;
        forwardSprintTimer = 0f;
        dragOriginScreenPos = screenPos;

        float scaleFactor = GetCanvasScaleFactor();

        if (joystickBackground != null && parentRectTransform != null)
        {
            if (joystickType == JoystickType.Fixed)
            {
                joystickBackground.anchoredPosition = defaultAnchoredPos;
                touchStartScreenPos = RectTransformUtility.WorldToScreenPoint(GetUICamera(), joystickBackground.position);
            }
            else
            {
                Vector2 clamped = ScreenToTargetAnchoredPos(screenPos, out Vector2 unclamped);
                joystickBackground.anchoredPosition = clamped;
                touchStartScreenPos = screenPos + (clamped - unclamped) * scaleFactor;
            }
        }
        else
        {
            touchStartScreenPos = screenPos;
        }

        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }

        inputVector = Vector2.zero;
    }

    public void UpdateDragAt(Vector2 currentScreenPos)
    {
        UpdateDragAt(currentScreenPos, Time.unscaledDeltaTime);
    }

    public void UpdateDragAt(Vector2 currentScreenPos, float deltaTime)
    {
        float scaleFactor = GetCanvasScaleFactor();
        Vector2 rawScreenDelta = currentScreenPos - touchStartScreenPos;
        Vector2 rawCanvasDelta = rawScreenDelta / scaleFactor;
        float rawDistance = rawCanvasDelta.magnitude;

        if (rawDistance < deadZone)
        {
            inputVector = isSprintLocked ? new Vector2(0f, 1f) : Vector2.zero;
            if (!isSprintLocked) forwardSprintTimer = 0f;
            if (joystickHandle != null) joystickHandle.anchoredPosition = Vector2.zero;
            return;
        }

        Vector2 direction = rawCanvasDelta / rawDistance;

        // 1. Trava de Corrida (Sprint Lock): avaliada ANTES do Follow Thumb limitar o delta
        float sprintLockDist = joystickRadius * sprintLockThresholdMultiplier;
        float totalUpFromOrigin = (currentScreenPos.y - dragOriginScreenPos.y) / scaleFactor;
        bool straightForwardPush = direction.y > 0.85f && Mathf.Abs(direction.x) < 0.45f;

        if ((rawCanvasDelta.y > sprintLockDist || (joystickType == JoystickType.DynamicFloatingFollow && totalUpFromOrigin > sprintLockDist * 1.15f))
            && Mathf.Abs(direction.x) < 0.45f)
        {
            isSprintLocked = true;
        }
        else if (joystickType == JoystickType.DynamicFloatingFollow && straightForwardPush && rawDistance >= joystickRadius * 0.90f)
        {
            forwardSprintTimer += Mathf.Max(0f, deltaTime);
            if (forwardSprintTimer >= sprintLockHoldDuration)
            {
                isSprintLocked = true;
            }
        }
        else
        {
            forwardSprintTimer = Mathf.Max(0f, forwardSprintTimer - Mathf.Max(0f, deltaTime) * 2f);
        }

        if (rawCanvasDelta.y < -deadZone * 2f)
        {
            isSprintLocked = false;
            forwardSprintTimer = 0f;
        }

        // 2. Follow Thumb: base acompanha suavemente o polegar alem do raio, respeitando limites seguros da tela
        float distance = rawDistance;
        if (joystickType == JoystickType.DynamicFloatingFollow && rawDistance > joystickRadius && joystickBackground != null && parentRectTransform != null)
        {
            Rect r = parentRectTransform.rect;
            if (r.width < 10f || r.height < 10f) r = new Rect(-960f, -540f, 1920f, 1080f);
            Vector2 anchorMid = (joystickBackground.anchorMin + joystickBackground.anchorMax) * 0.5f;
            Vector2 anchorOrigin = new Vector2(r.xMin + r.width * anchorMid.x, r.yMin + r.height * anchorMid.y);

            Vector2 excessCanvas = rawCanvasDelta - direction * joystickRadius;
            Vector2 prevPos = joystickBackground.anchoredPosition;
            Vector2 nextPos = ClampAnchoredToLeftZone(prevPos + excessCanvas, r, anchorOrigin);
            Vector2 appliedDelta = nextPos - prevPos;

            joystickBackground.anchoredPosition = nextPos;
            touchStartScreenPos += appliedDelta * scaleFactor;

            Vector2 updatedCanvasDelta = (currentScreenPos - touchStartScreenPos) / scaleFactor;
            distance = updatedCanvasDelta.magnitude;
            if (distance > 0.001f)
            {
                direction = updatedCanvasDelta / distance;
            }
        }

        float clampedDistance = Mathf.Min(distance, joystickRadius);
        float normalized = Mathf.Clamp01((clampedDistance - deadZone) / Mathf.Max(1f, joystickRadius - deadZone));

        // Curva suave de resposta para controle preciso em caminhada e corrida
        float eased = Mathf.Pow(normalized, 1.35f);
        inputVector = direction * eased;

        if (isSprintLocked)
        {
            inputVector.y = Mathf.Max(inputVector.y, 1.0f);
            if (inputVector.sqrMagnitude > 1f) inputVector.Normalize();
        }

        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = direction * clampedDistance;
        }
    }

    public void EndDrag()
    {
        activeFingerId = -1;
        isDragging = false;
        inputVector = Vector2.zero;
        isSprintLocked = false;
        forwardSprintTimer = 0f;
    }

    // =================== FALLBACK uGUI / MOUSE NO PC (MODO MOBILE F2) ===================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Input.touchCount > 0) return;
        BeginDragAt(eventData.pointerId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Input.touchCount > 0) return;
        if (!isDragging || eventData.pointerId != activeFingerId) return;
        UpdateDragAt(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (Input.touchCount > 0) return;
        if (eventData.pointerId == activeFingerId)
        {
            EndDrag();
        }
    }

    public Vector2 GetInputVector()
    {
        return inputVector;
    }

    private void OnDisable()
    {
        EndDrag();
    }

    // =================== GERACAO PROCEDURAL DE TEXTURAS ===================

    private static Sprite CreateRingTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;
        float rOut = center - 2f;
        float rIn = center * 0.72f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alphaRing = Mathf.SmoothStep(rIn - 1.2f, rIn, dist) * (1f - Mathf.SmoothStep(rOut - 1.2f, rOut, dist));
                float alphaFill = (dist < rIn) ? 0.18f : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(alphaRing * 0.90f, alphaFill));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateHandleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;
        float radius = center - 2f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = 1f - Mathf.SmoothStep(radius - 1.5f, radius, dist);
                float centerGlow = Mathf.Clamp01(1f - (dist / (center * 0.55f))) * 0.35f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * 0.88f + centerGlow);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateArrowTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        float cx = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Seta apontando para cima
                float normY = (float)y / size;
                float halfWidth = (1.0f - normY) * (size * 0.45f);
                float distFromCenter = Mathf.Abs(x - cx);
                float alpha = (distFromCenter <= halfWidth && normY >= 0.15f) ? 1.0f : 0.0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
