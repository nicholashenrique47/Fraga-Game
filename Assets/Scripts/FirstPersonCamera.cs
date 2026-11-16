using UnityEngine;

/// <summary>
/// Camera em Primeira Pessoa AAA (The Forest Standard).
/// Suporta Mouse Look PC, Touch Look Mobile, Headbob proporcional, Dynamic FOV (incluindo zoom de arco)
/// e bloqueio seguro de cursor sincronizado com FragaInputRouter.
/// </summary>
public class FirstPersonCamera : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerBody;
    public CameraTouchController touchController;
    private Camera cam;
    private PlayerController playerController;

    [Header("Settings")]
    public float mouseSensitivity = 1.6f;
    public float touchSensitivity = 0.22f;
    public float topClamp = -80f;
    public float bottomClamp = 80f;
    public bool autoLockCursorOnPC = true;

    public float sensitivity { get { return mouseSensitivity; } set { mouseSensitivity = value; } }

    private float xRotation = 0f;
    private Vector2 smoothedLookDelta;

    [Header("Headbob Settings")]
    public float bobSpeed = 10f;
    public float bobAmountWalk = 0.035f;
    public float bobAmountSprint = 0.07f;
    public float eyeHeightStanding = 1.75f;
    public float eyeHeightCrouching = 1.05f;
    public float eyeHeight { get { return eyeHeightStanding; } set { eyeHeightStanding = value; } }

    private float timer = 0f;
    private float currentEyeHeight;

    [Header("AAA Camera Polish")]
    public float baseFOV = 65f;
    public float sprintFOV = 76f;
    public float bowAimFOV = 52f;
    public float strafeTiltMax = 1.6f;

    private float shakeIntensity = 0f;
    private float tiltAngle = 0f;
    private float targetTiltAngle = 0f;
    private float landingDipOffset = 0f;
    private float customFOVOverride = -1f;

    public static FirstPersonCamera Instance;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = baseFOV;
        ApplySavedSensitivity();

        if (playerBody == null && transform.parent != null)
        {
            playerBody = transform.parent;
        }

        if (playerBody != null)
        {
            playerController = playerBody.GetComponent<PlayerController>();
        }

        currentEyeHeight = eyeHeightStanding;
    }

    private void Start()
    {
        ApplySavedSensitivity();
        transform.localPosition = new Vector3(0, currentEyeHeight, 0.15f);

        if (autoLockCursorOnPC && Input.touchCount == 0 && IsDesktopPlatform() &&
            (FragaInputRouter.Instance == null || !FragaInputRouter.Instance.IsTouchControlsActive))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (playerBody != null)
        {
            Renderer[] rends = playerBody.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r.transform.IsChildOf(transform)) continue;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
    }

    private static bool IsDesktopPlatform()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer ||
               Application.platform == RuntimePlatform.WindowsEditor ||
               Application.platform == RuntimePlatform.OSXPlayer ||
               Application.platform == RuntimePlatform.OSXEditor ||
               Application.platform == RuntimePlatform.LinuxPlayer;
    }

    public void ApplySavedSensitivity()
    {
        mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("Fraga_MouseSensitivity", 1.6f), 0.3f, 6.0f);
        touchSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("Fraga_TouchSensitivity", 0.22f), 0.05f, 1.0f);
    }

    public void SetSensitivity(float newMouseSens, float newTouchSens)
    {
        mouseSensitivity = Mathf.Clamp(newMouseSens, 0.3f, 6.0f);
        touchSensitivity = Mathf.Clamp(newTouchSens, 0.05f, 1.0f);
        PlayerPrefs.SetFloat("Fraga_MouseSensitivity", mouseSensitivity);
        PlayerPrefs.SetFloat("Fraga_TouchSensitivity", touchSensitivity);
        PlayerPrefs.Save();
    }

    public void AddShake(float intensity)
    {
        shakeIntensity = Mathf.Min(shakeIntensity + intensity, 0.45f);
    }

    public void SetTilt(float angle)
    {
        targetTiltAngle = angle;
    }

    public void AddJumpImpulse()
    {
        landingDipOffset = -0.045f;
    }

    public void AddLandingDip(float impactSpeed)
    {
        float norm = Mathf.Clamp01((impactSpeed - 3.5f) / 10f);
        landingDipOffset = Mathf.Min(landingDipOffset, -Mathf.Lerp(0.05f, 0.22f, norm));
        if (norm > 0.25f)
        {
            AddShake(norm * 0.14f);
        }
    }

    public void SetAimFOVOverride(float fovOrNegativeToClear)
    {
        customFOVOverride = fovOrNegativeToClear;
    }

    private void HandleHeadbobAndPolish()
    {
        float speed = 0f;
        bool isSprinting = false;
        bool isCrouching = false;

        if (playerController != null)
        {
            speed = playerController.CurrentSpeed;
            isSprinting = playerController.isSprinting;
            isCrouching = playerController.isCrouching;
        }

        float targetHeight = isCrouching ? eyeHeightCrouching : eyeHeightStanding;
        currentEyeHeight = Mathf.Lerp(currentEyeHeight, targetHeight, Time.deltaTime * 8f);
        landingDipOffset = Mathf.Lerp(landingDipOffset, 0f, Time.deltaTime * 6f);

        if (speed > 0.4f)
        {
            float currentBobSpeed = isSprinting ? bobSpeed * 1.35f : bobSpeed;
            float currentBobAmount = isSprinting ? bobAmountSprint : (isCrouching ? bobAmountWalk * 0.5f : bobAmountWalk);

            timer += Time.deltaTime * currentBobSpeed;
            float newY = currentEyeHeight + Mathf.Sin(timer) * currentBobAmount + landingDipOffset;
            float newX = Mathf.Cos(timer * 0.5f) * (currentBobAmount * 0.6f);
            transform.localPosition = new Vector3(newX, newY, 0.15f);
        }
        else
        {
            timer = 0;
            float curY = Mathf.Lerp(transform.localPosition.y, currentEyeHeight + landingDipOffset, Time.deltaTime * 8f);
            float curX = Mathf.Lerp(transform.localPosition.x, 0f, Time.deltaTime * 8f);
            transform.localPosition = new Vector3(curX, curY, 0.15f);
        }

        if (cam != null)
        {
            float targetFOV = customFOVOverride > 0f ? customFOVOverride : (isSprinting ? sprintFOV : baseFOV);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 6f);
        }

        if (shakeIntensity > 0)
        {
            shakeIntensity -= Time.deltaTime * 0.7f;
            if (shakeIntensity < 0) shakeIntensity = 0;
        }

        float horizontalInput = FragaInputRouter.Instance != null
            ? FragaInputRouter.Instance.MoveInput.x
            : Input.GetAxisRaw("Horizontal");
        float strafeTilt = -horizontalInput * strafeTiltMax;
        tiltAngle = Mathf.Lerp(tiltAngle, targetTiltAngle + strafeTilt, Time.deltaTime * 5f);
    }

    private void LateUpdate()
    {
        if (playerBody == null) return;

        bool uiBlocking = SurvivalMapSystem.IsMapOpen ||
                          (SurvivalInventoryCraftUI.Instance != null && SurvivalInventoryCraftUI.Instance.IsOpen) ||
                          (RadialQuickSelectUI.Instance != null && RadialQuickSelectUI.Instance.IsOpen) ||
                          (FragaOptionsMenu.Instance != null && FragaOptionsMenu.Instance.IsOpen) ||
                          StorageChestInteractable.IsAnyChestOpen ||
                          UIManager.IsEndgameModalOpen;

        if (uiBlocking)
        {
            if (IsDesktopPlatform() && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        if (Input.GetMouseButtonDown(0) && Input.touchCount == 0 && IsDesktopPlatform())
        {
            if (!FragaInputRouter.IsAnyPointerOverUI())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && IsDesktopPlatform())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Vector2 lookDelta = Vector2.zero;
        bool isTouch = false;

        if (touchController != null)
        {
            Vector2 tDelta = touchController.GetTouchDelta();
            if (tDelta.sqrMagnitude > 0.001f)
            {
                lookDelta = tDelta * touchSensitivity;
                isTouch = true;
            }
        }

        if (!isTouch && Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(t.fingerId))
                    continue;

                if (t.position.x > Screen.width * 0.48f && t.phase == TouchPhase.Moved)
                {
                    lookDelta = t.deltaPosition * touchSensitivity;
                    isTouch = true;
                    break;
                }
            }
        }

        if (!isTouch)
        {
            if (Cursor.lockState == CursorLockMode.Locked || Input.GetMouseButton(1))
            {
                float rawMouseX = Input.GetAxisRaw("Mouse X");
                float rawMouseY = Input.GetAxisRaw("Mouse Y");
                lookDelta = new Vector2(rawMouseX, rawMouseY) * mouseSensitivity * 1.5f;
            }
        }

        smoothedLookDelta = Vector2.Lerp(smoothedLookDelta, lookDelta, Time.deltaTime * 30f);

        xRotation -= smoothedLookDelta.y;
        xRotation = Mathf.Clamp(xRotation, topClamp, bottomClamp);

        float shakeX = Random.Range(-1f, 1f) * shakeIntensity;
        float shakeY = Random.Range(-1f, 1f) * shakeIntensity;

        transform.localRotation = Quaternion.Euler(xRotation + shakeY, 0f, tiltAngle + shakeX);
        playerBody.Rotate(Vector3.up * smoothedLookDelta.x);

        HandleHeadbobAndPolish();
    }
}
