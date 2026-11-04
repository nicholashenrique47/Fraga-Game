using UnityEngine;

/// <summary>
/// Câmera em Primeira Pessoa AAA (The Forest Standard).
/// Suporta:
/// - Mouse Look suave no PC
/// - Touch Look nativo no Celular (arraste com o dedo no lado direito da tela)
/// - Headbobbing físico proporcional à velocidade real de caminhada/corrida
/// - Transição suave de altura dos olhos ao agachar (1.75m -> 1.05m)
/// - Tilt / Roll dinâmico ao esquivar para os lados (Strafing)
/// - Dynamic FOV entre caminhada (65) e corrida (78)
/// - Trava de cursor automática no PC com destrave via ESC
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

    // Propriedade mantida para retrocompatibilidade
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
    public float sprintFOV = 78f;
    public float strafeTiltMax = 1.5f;

    private float shakeIntensity = 0f;
    private float tiltAngle = 0f;
    private float targetTiltAngle = 0f;
    private float landingDipOffset = 0f;

    public static FirstPersonCamera Instance;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = baseFOV;

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
        transform.localPosition = new Vector3(0, currentEyeHeight, 0.15f);

        // Trava cursor no PC se for desktop sem touch
        if (autoLockCursorOnPC && Input.touchCount == 0 && (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxPlayer))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Oculta malha do proprio corpo para evitar clipping na lente, preservando sombras
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

    public void AddShake(float intensity)
    {
        shakeIntensity = Mathf.Min(shakeIntensity + intensity, 0.5f);
    }

    public void SetTilt(float angle)
    {
        targetTiltAngle = angle;
    }

    public void AddJumpImpulse()
    {
        landingDipOffset = -0.04f;
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

        // 1. Transicao suave de altura dos olhos ao agachar
        float targetHeight = isCrouching ? eyeHeightCrouching : eyeHeightStanding;
        currentEyeHeight = Mathf.Lerp(currentEyeHeight, targetHeight, Time.deltaTime * 8f);

        // 2. Amortecimento de aterrissagem / salto
        landingDipOffset = Mathf.Lerp(landingDipOffset, 0f, Time.deltaTime * 6f);

        // 3. Headbobbing fisico proporcional a velocidade real
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

        // 4. Dynamic FOV
        if (cam != null)
        {
            float targetFOV = isSprinting ? sprintFOV : baseFOV;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 5f);
        }

        // 5. Decaimento de Camera Shake
        if (shakeIntensity > 0)
        {
            shakeIntensity -= Time.deltaTime * 0.6f;
            if (shakeIntensity < 0) shakeIntensity = 0;
        }

        // 6. Strafe Tilt (Roll sutil ao andar de lado)
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float strafeTilt = -horizontalInput * strafeTiltMax;
        tiltAngle = Mathf.Lerp(tiltAngle, targetTiltAngle + strafeTilt, Time.deltaTime * 5f);
    }

    private void LateUpdate()
    {
        if (playerBody == null) return;

        // Suporte para travar cursor com clique e destravar com ESC no PC
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                if (Input.touchCount == 0)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Vector2 lookDelta = Vector2.zero;
        bool isTouch = false;

        // 1. Tenta pegar do painel dedicado CameraTouchController se houver
        if (touchController != null)
        {
            Vector2 tDelta = touchController.GetTouchDelta();
            if (tDelta.sqrMagnitude > 0.001f)
            {
                lookDelta = tDelta * touchSensitivity;
                isTouch = true;
            }
        }

        // 2. Toque Nativo: varre qualquer toque no lado direito da tela (fora da área do joystick)
        if (!isTouch && Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                // Ignora toques sobre botoes e elementos de UI da interface
                if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(t.fingerId))
                {
                    continue;
                }

                // Lado direito da tela (48% em diante)
                if (t.position.x > Screen.width * 0.48f)
                {
                    if (t.phase == TouchPhase.Moved)
                    {
                        lookDelta = t.deltaPosition * touchSensitivity;
                        isTouch = true;
                        break;
                    }
                }
            }
        }

        // 3. Fallback Mouse no PC
        if (!isTouch)
        {
            if (Cursor.lockState == CursorLockMode.Locked || Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                float rawMouseX = Input.GetAxisRaw("Mouse X");
                float rawMouseY = Input.GetAxisRaw("Mouse Y");
                lookDelta = new Vector2(rawMouseX, rawMouseY) * mouseSensitivity * 1.5f;
            }
        }

        // Suavizacao
        smoothedLookDelta = Vector2.Lerp(smoothedLookDelta, lookDelta, Time.deltaTime * 30f);

        float rotX = smoothedLookDelta.x;
        float rotY = smoothedLookDelta.y;

        xRotation -= rotY;
        xRotation = Mathf.Clamp(xRotation, topClamp, bottomClamp);

        // Shake offset
        float shakeX = Random.Range(-1f, 1f) * shakeIntensity;
        float shakeY = Random.Range(-1f, 1f) * shakeIntensity;

        // Rodar câmera cima/baixo + Tilt lateral + Shake
        transform.localRotation = Quaternion.Euler(xRotation + shakeY, 0f, tiltAngle + shakeX);

        // Rodar corpo do jogador esquerda/direita
        playerBody.Rotate(Vector3.up * rotX);

        HandleHeadbobAndPolish();
    }
}
