using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Roteador Central de Entrada Hibrida (PC + Mobile) do Fraga.
/// Elimina conflitos de teclas (Space, Q, E, F, C, 1-6) e gerencia adaptacao automatica Desktop <-> Mobile.
/// </summary>
public class FragaInputRouter : MonoBehaviour
{
    public static FragaInputRouter Instance { get; private set; }

    public enum InputContext
    {
        Gameplay,
        InventoryUI,
        BuildPlacement,
        MapOpen,
        RadialOpen,
        DeadOrVictory
    }

    public enum ControlLayoutMode
    {
        Auto = 0,
        Mobile = 1,
        Desktop = 2
    }

    public InputContext CurrentContext { get; private set; } = InputContext.Gameplay;
    public ControlLayoutMode LayoutMode { get; private set; } = ControlLayoutMode.Auto;

    /// <summary>
    /// Verdadeiro quando os controles de toque (joystick virtual e botoes laterais) devem ficar visiveis.
    /// Atualiza automaticamente em dispositivos moveis, toque na tela ou alternancia via F2/Mochila.
    /// </summary>
    public bool IsTouchControlsActive { get; private set; }

    public Vector2 MoveInput { get; private set; }
    public bool WantsSprint { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool CrouchPressedThisFrame { get; private set; }
    public bool PrimaryDownThisFrame { get; private set; }
    public bool PrimaryHeld { get; private set; }
    public bool PrimaryUpThisFrame { get; private set; }
    public bool SecondaryDownThisFrame { get; private set; }
    public bool SecondaryHeld { get; private set; }
    public bool SecondaryUpThisFrame { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }
    public bool LightTogglePressedThisFrame { get; private set; }
    public int HotbarKeyPressedThisFrame { get; private set; } = -1; // 0..5

    public bool IsGameplayAllowed => CurrentContext == InputContext.Gameplay;
    public bool IsCameraLookAllowed => CurrentContext == InputContext.Gameplay || CurrentContext == InputContext.BuildPlacement;

    public event System.Action<bool> OnTouchLayoutChanged;

    private MobileInputController mobileInput;
    private bool touchDetectedInSession;
    private bool mobilePrimaryHeld;
    private bool mobileSecondaryHeld;
    private bool mobilePrimaryDownQueued;
    private bool mobilePrimaryUpQueued;
    private bool mobileSecondaryDownQueued;
    private bool mobileSecondaryUpQueued;
    private bool mobileInteractQueued;
    private bool mobileJumpQueued;
    private bool mobileCrouchQueued;
    private bool mobileLightQueued;
    private int mobileHotbarQueued = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        InventorySystem legacyInv = FindAnyObjectByType<InventorySystem>();
        if (legacyInv != null) legacyInv.enabled = false;

        int savedMode = PlayerPrefs.GetInt("Fraga_ControlLayoutMode", 0);
        if (savedMode >= 0 && savedMode <= 2) LayoutMode = (ControlLayoutMode)savedMode;
        RecomputeTouchActive(notify: false);
    }

    private void Start()
    {
        mobileInput = FindAnyObjectByType<MobileInputController>();
        RecomputeTouchActive(notify: true);
    }

    private void Update()
    {
        if (Input.touchCount > 0 && !touchDetectedInSession)
        {
            touchDetectedInSession = true;
            RecomputeTouchActive(notify: true);
        }

        // Atalho F2 para alternar rapidamente entre Layout Mobile (Touch) e Desktop (PC limpo)
        if (Input.GetKeyDown(KeyCode.F2))
        {
            ToggleTouchControls();
        }

        RefreshContext();
        PollInputs();
    }

    public void SetControlLayoutMode(ControlLayoutMode mode)
    {
        LayoutMode = mode;
        PlayerPrefs.SetInt("Fraga_ControlLayoutMode", (int)mode);
        PlayerPrefs.Save();
        RecomputeTouchActive(notify: true);
    }

    public void ToggleTouchControls()
    {
        SetControlLayoutMode(IsTouchControlsActive ? ControlLayoutMode.Desktop : ControlLayoutMode.Mobile);
        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(IsTouchControlsActive
                ? "Layout de Controles: MOBILE (Touch)"
                : "Layout de Controles: DESKTOP (Teclado/Mouse)");
        }
    }

    private void RecomputeTouchActive(bool notify)
    {
        bool next;
        switch (LayoutMode)
        {
            case ControlLayoutMode.Mobile:
                next = true;
                break;
            case ControlLayoutMode.Desktop:
                next = false;
                break;
            default:
                bool hardwareMobile = Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
#if UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
                bool editorMobileTarget = true;
#else
                bool editorMobileTarget = false;
#endif
                next = hardwareMobile || editorMobileTarget || touchDetectedInSession;
                break;
        }

        if (next != IsTouchControlsActive || notify)
        {
            IsTouchControlsActive = next;
            OnTouchLayoutChanged?.Invoke(IsTouchControlsActive);
        }
    }

    public static bool IsAnyPointerOverUI()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                    return true;
            }
            return false;
        }

        return es.IsPointerOverGameObject();
    }

    private void RefreshContext()
    {
        if (SurvivalMapSystem.IsMapOpen)
        {
            CurrentContext = InputContext.MapOpen;
        }
        else if (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen)
        {
            CurrentContext = InputContext.InventoryUI;
        }
        else if (RadialQuickSelectUI.Instance != null && RadialQuickSelectUI.Instance.IsOpen)
        {
            CurrentContext = InputContext.RadialOpen;
        }
        else if ((BuildingSystem.Instance != null && BuildingSystem.Instance.IsBuildModeActive) ||
                 (BlueprintPlacementManager.Instance != null && BlueprintPlacementManager.Instance.IsPlacingGhost))
        {
            CurrentContext = InputContext.BuildPlacement;
        }
        else
        {
            CurrentContext = InputContext.Gameplay;
        }
    }

    private void PollInputs()
    {
        // 1. Movimento unificado (Teclado + Joystick Mobile)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (mobileInput == null) mobileInput = FindAnyObjectByType<MobileInputController>();
        if (mobileInput != null && IsTouchControlsActive)
        {
            Vector2 m = mobileInput.GetInputVector();
            if (m.sqrMagnitude > 0.0001f)
            {
                h = m.x;
                v = m.y;
            }
        }
        Vector2 mv = new Vector2(h, v);
        if (mv.sqrMagnitude > 1f) mv.Normalize();
        MoveInput = IsGameplayAllowed || CurrentContext == InputContext.BuildPlacement ? mv : Vector2.zero;

        WantsSprint = (IsGameplayAllowed || CurrentContext == InputContext.BuildPlacement) &&
                      (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
                       (mobileInput != null && IsTouchControlsActive && mobileInput.IsSprinting));

        // 2. Botoes discretos
        JumpPressedThisFrame = IsGameplayAllowed && (Input.GetKeyDown(KeyCode.Space) || mobileJumpQueued);
        CrouchPressedThisFrame = IsGameplayAllowed && (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl) || mobileCrouchQueued);
        InteractPressedThisFrame = IsGameplayAllowed && (Input.GetKeyDown(KeyCode.E) || mobileInteractQueued);
        LightTogglePressedThisFrame = (IsGameplayAllowed || CurrentContext == InputContext.BuildPlacement) &&
                                      (Input.GetKeyDown(KeyCode.L) || mobileLightQueued);

        // 3. Mouse / Acao primaria e secundaria (bloqueada se clicar sobre UI ou com touch sobre UI)
        bool mouseOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() && Input.touchCount == 0;
        bool pcPlay = IsGameplayAllowed && !mouseOverUI && Input.touchCount == 0;

        PrimaryDownThisFrame = (pcPlay && Input.GetMouseButtonDown(0)) || (IsGameplayAllowed && mobilePrimaryDownQueued);
        PrimaryHeld = (pcPlay && Input.GetMouseButton(0)) || (IsGameplayAllowed && mobilePrimaryHeld);
        PrimaryUpThisFrame = (IsGameplayAllowed && Input.touchCount == 0 && Input.GetMouseButtonUp(0)) || (IsGameplayAllowed && mobilePrimaryUpQueued);

        SecondaryDownThisFrame = (pcPlay && Input.GetMouseButtonDown(1)) || (IsGameplayAllowed && mobileSecondaryDownQueued);
        SecondaryHeld = (pcPlay && Input.GetMouseButton(1)) || (IsGameplayAllowed && mobileSecondaryHeld);
        SecondaryUpThisFrame = (IsGameplayAllowed && Input.touchCount == 0 && Input.GetMouseButtonUp(1)) || (IsGameplayAllowed && mobileSecondaryUpQueued);

        // 4. Hotbar 1..6 no PC + Mobile
        HotbarKeyPressedThisFrame = mobileHotbarQueued;
        if (IsGameplayAllowed && HotbarKeyPressedThisFrame < 0)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) HotbarKeyPressedThisFrame = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) HotbarKeyPressedThisFrame = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) HotbarKeyPressedThisFrame = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) HotbarKeyPressedThisFrame = 3;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) HotbarKeyPressedThisFrame = 4;
            else if (Input.GetKeyDown(KeyCode.Alpha6)) HotbarKeyPressedThisFrame = 5;
        }

        // Limpar filas mobile
        mobileJumpQueued = false;
        mobileCrouchQueued = false;
        mobileInteractQueued = false;
        mobileLightQueued = false;
        mobilePrimaryDownQueued = false;
        mobilePrimaryUpQueued = false;
        mobileSecondaryDownQueued = false;
        mobileSecondaryUpQueued = false;
        mobileHotbarQueued = -1;
    }

    // =================== HOOKS PARA BOTOES MOBILE ===================

    public void TriggerMobilePrimaryDown() { mobilePrimaryHeld = true; mobilePrimaryDownQueued = true; }
    public void TriggerMobilePrimaryUp() { mobilePrimaryHeld = false; mobilePrimaryUpQueued = true; }
    public void TriggerMobileSecondaryDown() { mobileSecondaryHeld = true; mobileSecondaryDownQueued = true; }
    public void TriggerMobileSecondaryUp() { mobileSecondaryHeld = false; mobileSecondaryUpQueued = true; }
    public void TriggerMobileInteract() { mobileInteractQueued = true; }
    public void TriggerMobileJump() { mobileJumpQueued = true; }
    public void TriggerMobileCrouch() { mobileCrouchQueued = true; }
    public void TriggerMobileLight() { mobileLightQueued = true; }
    public void TriggerMobileHotbarSlot(int slotIndex) { mobileHotbarQueued = slotIndex; }
}
