using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controlador de Joystick Virtual Mobile Híbrido AAA (Padrão COD Mobile / Wild Rift / Genshin Impact).
/// Suporta:
/// - Modo Híbrido: Dynamic Floating Follow (acompanha o polegar para resposta instantânea ao virar), Fixo ou Centralizado.
/// - Trava de Corrida (Sprint Lock): empurrar além do limite superior ativa corrida contínua sem cansar o polegar.
/// - Isolamento de UI: não rouba toques de botões da interface (mochila, ataque, pulo).
/// - Curva de resposta analógica cúbica para andar furtivo milimétrico e corrida firme.
/// - Opacidade dinâmica (fade sutil quando ocioso, brilhante quando ativo).
/// </summary>
public class MobileInputController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public enum JoystickType
    {
        DynamicFloatingFollow, // Recomendado: base surge no polegar e acompanha o arrasto além do raio
        Fixed,                 // Base fixa na posição padrão
        DynamicCentering       // Base surge no toque e fica estática
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

    [Header("Visual Feedback & Opacity")]
    public float idleAlpha = 0.35f;
    public float activeAlpha = 0.85f;
    public Color handleNormalColor = new Color(0.92f, 0.95f, 1.0f, 0.85f);
    public Color handleSprintColor = new Color(0.30f, 0.95f, 1.0f, 1.0f);
    public Color sprintLockActiveColor = new Color(0.30f, 1.0f, 0.60f, 1.0f);

    private Vector2 inputVector = Vector2.zero;
    private bool isDragging = false;
    private int activeFingerId = -1;
    private Vector2 touchStartScreenPos;
    private Vector2 defaultAnchoredPos;
    private Canvas parentCanvas;
    private RectTransform parentRectTransform;
    private CanvasGroup joystickCanvasGroup;

    private Image handleImage;
    private Image bgImage;
    private Image sprintIndicatorImage;
    private Text sprintIndicatorText;

    private bool isSprintLocked = false;

    private static Sprite s_ringSprite;
    private static Sprite s_handleSprite;
    private static Sprite s_arrowSprite;

    /// <summary>
    /// Retorna verdadeiro se o analógico estiver na faixa de corrida ou com Sprint Lock ativado.
    /// </summary>
    public bool IsSprinting => isSprintLocked || (inputVector.y > 0.62f && inputVector.magnitude > 0.80f);

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
        LoadPreferences();
        FindCanvasAndVisuals();
    }

    private void Start()
    {
        FindCanvasAndVisuals();
        ConfigureVisualStyles();
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
    }

    private void FindCanvasAndVisuals()
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) parentCanvas = FindAnyObjectByType<Canvas>();

        if (joystickBackground == null)
        {
            GameObject bgObj = GameObject.Find("Joystick_BG");
            if (bgObj != null) joystickBackground = bgObj.GetComponent<RectTransform>();
            else joystickBackground = GetComponent<RectTransform>();
        }

        if (joystickBackground != null)
        {
            defaultAnchoredPos = joystickBackground.anchoredPosition;
            if (defaultAnchoredPos.sqrMagnitude < 10f)
            {
                defaultAnchoredPos = new Vector2(160f, 160f);
            }
            parentRectTransform = joystickBackground.parent as RectTransform;

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

            // Criar ou estilizar o indicador de Sprint Lock se não existir
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
                textRT.sizeDelta = new Vector2(70, 20);
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
        // 1. Processamento de Toques Mobile Multi-Touch
        if (Input.touchCount > 0)
        {
            ProcessNativeTouches();
        }

        // 2. Transição suave de opacidade (sutil quando ocioso, destacado ao usar)
        if (joystickCanvasGroup != null)
        {
            float targetAlpha = isDragging ? activeAlpha : idleAlpha;
            joystickCanvasGroup.alpha = Mathf.Lerp(joystickCanvasGroup.alpha, targetAlpha, Time.deltaTime * 12f);
        }

        // 3. Retorno elástico (Spring-back) do manípulo e base
        if (!isDragging && joystickHandle != null)
        {
            if (joystickHandle.anchoredPosition.sqrMagnitude > 0.01f)
            {
                joystickHandle.anchoredPosition = Vector2.Lerp(joystickHandle.anchoredPosition, Vector2.zero, Time.deltaTime * 24f);
            }
            if (joystickBackground != null && (joystickBackground.anchoredPosition - defaultAnchoredPos).sqrMagnitude > 0.1f)
            {
                joystickBackground.anchoredPosition = Vector2.Lerp(joystickBackground.anchoredPosition, defaultAnchoredPos, Time.deltaTime * 10f);
            }
        }

        // 4. Feedback visual dinâmico do manípulo e do Sprint Lock
        if (handleImage != null)
        {
            Color targetCol = IsSprinting ? (isSprintLocked ? sprintLockActiveColor : handleSprintColor) : handleNormalColor;
            handleImage.color = Color.Lerp(handleImage.color, targetCol, Time.deltaTime * 12f);
        }

        if (sprintIndicatorImage != null)
        {
            Color targetLockCol = isSprintLocked ? sprintLockActiveColor : (IsSprinting ? handleSprintColor : new Color(0.4f, 0.8f, 1.0f, 0.35f));
            sprintIndicatorImage.color = Color.Lerp(sprintIndicatorImage.color, targetLockCol, Time.deltaTime * 12f);
            if (sprintIndicatorText != null)
            {
                sprintIndicatorText.color = sprintIndicatorImage.color;
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
                // Ignora se o toque estiver sobre botões de UI da esquerda (ex: menus suspensos)
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                {
                    continue;
                }

                if (t.position.x <= maxLeftAreaX && t.position.y <= maxLeftAreaY)
                {
                    if (t.phase == TouchPhase.Began)
                    {
                        StartJoystickDrag(t.fingerId, t.position);
                        break;
                    }
                }
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
                        UpdateJoystickPosition(t.position);
                    }
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        EndJoystickDrag();
                    }
                    break;
                }
            }

            if (!foundActiveFinger)
            {
                EndJoystickDrag();
            }
        }
    }

    private void StartJoystickDrag(int fingerId, Vector2 screenPos)
    {
        activeFingerId = fingerId;
        isDragging = true;

        if (joystickBackground != null && parentRectTransform != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRectTransform, screenPos, null, out Vector2 localPoint);

            if (joystickType == JoystickType.Fixed)
            {
                joystickBackground.anchoredPosition = defaultAnchoredPos;
                touchStartScreenPos = RectTransformUtility.WorldToScreenPoint(null, joystickBackground.position);
            }
            else
            {
                // DynamicFloatingFollow ou DynamicCentering: Ancoragem sob o polegar
                joystickBackground.anchoredPosition = localPoint;
                touchStartScreenPos = screenPos;
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

    private void UpdateJoystickPosition(Vector2 currentScreenPos)
    {
        Vector2 screenDelta = currentScreenPos - touchStartScreenPos;
        float scaleFactor = (parentCanvas != null && parentCanvas.scaleFactor > 0.01f) ? parentCanvas.scaleFactor : 1.0f;
        Vector2 canvasDelta = screenDelta / scaleFactor;

        float distance = canvasDelta.magnitude;

        // Zona morta para prevenir oscilação em repouso
        if (distance < deadZone)
        {
            inputVector = Vector2.zero;
            if (joystickHandle != null) joystickHandle.anchoredPosition = Vector2.zero;
            return;
        }

        Vector2 direction = canvasDelta / distance;

        // 1. Lógica Follow Thumb (COD Mobile / Wild Rift / Genshin):
        // Se o polegar arrastar além do raio do analógico, a base desliza acompanhando o dedo!
        if (joystickType == JoystickType.DynamicFloatingFollow && distance > joystickRadius)
        {
            Vector2 excessCanvas = canvasDelta - direction * joystickRadius;
            if (joystickBackground != null)
            {
                joystickBackground.anchoredPosition += excessCanvas;
                touchStartScreenPos = RectTransformUtility.WorldToScreenPoint(null, joystickBackground.position);
                screenDelta = currentScreenPos - touchStartScreenPos;
                canvasDelta = screenDelta / scaleFactor;
                distance = joystickRadius;
            }
        }

        // 2. Trava de Corrida (Sprint Lock)
        float sprintLockDist = joystickRadius * sprintLockThresholdMultiplier;
        if (canvasDelta.y > sprintLockDist && Mathf.Abs(direction.x) < 0.45f)
        {
            isSprintLocked = true;
        }
        else if (canvasDelta.y < -deadZone * 2f)
        {
            // Puxar para trás cancela o sprint lock
            isSprintLocked = false;
        }

        float clampedDistance = Mathf.Min(distance, joystickRadius);
        float normalized = Mathf.Clamp01((clampedDistance - deadZone) / (joystickRadius - deadZone));

        // 3. Curva de sensibilidade cúbica suave: micro-ajustes perfeitos para caminhada furtiva
        float eased = Mathf.Pow(normalized, 1.35f);
        inputVector = direction * eased;

        // Se Sprint Lock estiver engatado, força impulso total para a frente
        if (isSprintLocked)
        {
            inputVector.y = Mathf.Max(inputVector.y, 1.0f);
        }

        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = direction * clampedDistance;
        }
    }

    private void EndJoystickDrag()
    {
        activeFingerId = -1;
        isDragging = false;
        inputVector = Vector2.zero;
        isSprintLocked = false;
    }

    // =================== FALLBACK uGUI / MOUSE NO PC ===================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Input.touchCount > 0) return;
        StartJoystickDrag(eventData.pointerId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Input.touchCount > 0) return;
        if (!isDragging || eventData.pointerId != activeFingerId) return;
        UpdateJoystickPosition(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (Input.touchCount > 0) return;
        if (eventData.pointerId == activeFingerId)
        {
            EndJoystickDrag();
        }
    }

    public Vector2 GetInputVector()
    {
        return inputVector;
    }

    private void OnDisable()
    {
        EndJoystickDrag();
    }

    // =================== GERAÇÃO PROCEDURAL DE TEXTURAS ===================

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
                // Cria uma seta apontando para cima (▲)
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
