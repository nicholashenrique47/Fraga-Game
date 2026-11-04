using UnityEngine;

/// <summary>
/// Controlador Físico de Movimentação do Jogador (Padrão AAA The Forest).
/// Suporta:
/// - Aceleração e desaceleração física com inércia
/// - Deslize suave de paredes, troncos de árvores e rochas (sem travar nem atravessar)
/// - Step Offset (0.35m) para subir automaticamente raízes, seixos e degraus sem engasgar
/// - Aderência suave ao relevo e encostas sem quicar ou perder o contato com o chão
/// - Detecção robusta de solo em terrenos procedurais, pedras e pontes
/// - Gravidade física realista e pulo responsivo (Barra de Espaço ou Botão Mobile)
/// - Corrida com stamina e agachamento com encolhimento de cápsula
/// - Integração total com Joystick Analógico Mobile 360 graus e Teclado
/// - Sistema de Escalada de Bordas (Mantling) e transporte de toras (Log Carrying)
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement Settings")]
    public float speed = 5f; // Usado pelo meta-upgrade system
    public float moveSpeed { get { return speed; } set { speed = value; } }
    public float sprintMultiplier = 1.6f;
    public float crouchMultiplier = 0.55f;
    public float acceleration = 34f;
    public float deceleration = 38f;
    public float airControl = 0.4f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.25f;
    public float gravity = 22f;
    public float maxSlopeAngle = 48f;
    public float stepOffset = 0.35f;

    [Header("Stealth & Crouch")]
    public bool isCrouching = false;
    public float standingHeight = 1.9f;
    public float crouchHeight = 1.15f;
    private float normalSpeed;

    [Header("Footsteps")]
    public float footstepInterval = 0.45f;
    private float footstepTimer = 0f;
    private AudioSource footstepAudio;
    private AudioClip[] footstepClips;
    private int footstepIndex = 0;

    [Header("Lighter Settings")]
    public Light lighterLight;
    private bool isLighterOn = false;

    [Header("Input Dependencies")]
    public MobileInputController mobileInput;

    // Estado físico interno
    private Rigidbody rb;
    private CapsuleCollider playerCol;
    private Camera mainCamera;
    private PlayerStatus playerStatus;

    private Vector3 currentVelocity;
    private float verticalVelocity = 0f;
    private bool isGrounded = true;
    private Vector3 groundNormal = Vector3.up;

    private Vector3 knockbackVelocity;
    private float knockbackTime;
    private bool isMantling = false;

    // Propriedades Públicas para outros sistemas
    public bool isSprinting { get; private set; }
    public bool IsGrounded => isGrounded;
    public bool IsMoving => currentVelocity.sqrMagnitude > 0.05f;
    public float CurrentSpeed => new Vector2(currentVelocity.x, currentVelocity.z).magnitude;

    private void Awake()
    {
        Instance = this;
        StartLighter();

        // 1. Configura CapsuleCollider no jogador raiz
        playerCol = GetComponent<CapsuleCollider>();
        if (playerCol == null)
        {
            playerCol = gameObject.AddComponent<CapsuleCollider>();
        }
        playerCol.center = new Vector3(0, standingHeight * 0.5f, 0);
        playerCol.radius = 0.35f;
        playerCol.height = standingHeight;

        // Remove colisores secundarios em filhos visuais para evitar auto-colisoes
        foreach (var c in GetComponentsInChildren<Collider>())
        {
            if (c != playerCol)
            {
                Destroy(c);
            }
        }

        // 2. Configura Rigidbody cinematico estavel
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        mainCamera = Camera.main;
        if (speed <= 0.1f) speed = 5f;
        normalSpeed = speed;
        playerStatus = GetComponent<PlayerStatus>();

        // Auto-anexa subsistemas essenciais
        if (GetComponent<ChoppingAndCombatSystem>() == null) gameObject.AddComponent<ChoppingAndCombatSystem>();
        if (GetComponent<LogCarryingSystem>() == null) gameObject.AddComponent<LogCarryingSystem>();
        if (GetComponent<BowAndArrowSystem>() == null) gameObject.AddComponent<BowAndArrowSystem>();
        if (GetComponent<CelticSlingSystem>() == null) gameObject.AddComponent<CelticSlingSystem>();
        if (GetComponent<PlayerMeleeCombatDefense>() == null) gameObject.AddComponent<PlayerMeleeCombatDefense>();
        if (GetComponent<BodyTemperatureSystem>() == null) gameObject.AddComponent<BodyTemperatureSystem>();
        if (GetComponent<SanityHallucinationDriver>() == null) gameObject.AddComponent<SanityHallucinationDriver>();
        if (GetComponent<AcousticOcclusionZone>() == null) gameObject.AddComponent<AcousticOcclusionZone>();
        if (GetComponent<BlueprintPlacementManager>() == null) gameObject.AddComponent<BlueprintPlacementManager>();
        if (GetComponent<RadialQuickSelectUI>() == null) gameObject.AddComponent<RadialQuickSelectUI>();

        // 3. AudioSource de passos com gerador procedural fallback
        footstepAudio = GetComponent<AudioSource>();
        if (footstepAudio == null)
        {
            footstepAudio = gameObject.AddComponent<AudioSource>();
        }
        footstepAudio.spatialBlend = 0.25f;
        footstepAudio.volume = 0.35f;
        GenerateFootstepClips();
    }

    private void Start()
    {
        SnapToGround();
        if (mobileInput == null)
        {
            mobileInput = FindAnyObjectByType<MobileInputController>();
        }
    }

    private void StartLighter()
    {
        if (lighterLight == null)
        {
            GameObject lightObj = new GameObject("PlayerLighter");
            lightObj.transform.SetParent(this.transform);
            lightObj.transform.localPosition = new Vector3(0.3f, 1.5f, 0.5f);
            lighterLight = lightObj.AddComponent<Light>();
            lighterLight.type = LightType.Point;
            lighterLight.range = 14f;
            lighterLight.intensity = 0f;
            lighterLight.color = new Color(1f, 0.75f, 0.4f);
            lighterLight.shadows = LightShadows.Soft;
        }
    }

    public void ToggleLighter()
    {
        isLighterOn = !isLighterOn;
    }

    private void UpdateLighter()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            isLighterOn = !isLighterOn;
        }

        if (lighterLight != null)
        {
            float targetIntensity = isLighterOn ? 2.2f + Random.Range(-0.15f, 0.15f) : 0f;
            lighterLight.intensity = Mathf.Lerp(lighterLight.intensity, targetIntensity, Time.deltaTime * 10f);
        }
    }

    private void GenerateFootstepClips()
    {
        footstepClips = new AudioClip[4];
        int sampleRate = 44100;

        for (int c = 0; c < 4; c++)
        {
            float duration = Random.Range(0.08f, 0.12f);
            float[] samples = new float[(int)(sampleRate * duration)];

            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                float env = Mathf.Exp(-12f * t);
                float noise = Random.Range(-1f, 1f);
                float thump = Mathf.Sin(2f * Mathf.PI * (60f + c * 15f) * ((float)i / sampleRate)) * env;
                samples[i] = (noise * 0.3f + thump * 0.7f) * env * 0.5f;
            }

            footstepClips[c] = AudioClip.Create($"Footstep_{c}", samples.Length, 1, sampleRate, false);
            footstepClips[c].SetData(samples, 0);
        }
    }

    public void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 5.0f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 25.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
        }
        else
        {
            float targetY = TerrainMeshGenerator.GetTerrainHeight(transform.position.x, transform.position.z);
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
        }

        if (rb != null)
        {
            rb.position = transform.position;
        }
    }

    public void ToggleCrouch()
    {
        isCrouching = !isCrouching;
    }

    public void ApplyKnockback(Vector3 force)
    {
        knockbackVelocity = force;
        knockbackTime = 0.25f;
    }

    private void Update()
    {
        UpdateLighter();
        HandleCrouchHeight();
        ProcessInput();
        UpdateFootsteps();
    }

    private void HandleCrouchHeight()
    {
        if (playerCol == null) return;

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        playerCol.height = Mathf.Lerp(playerCol.height, targetHeight, Time.deltaTime * 10f);
        playerCol.center = new Vector3(0, playerCol.height * 0.5f, 0);
    }

    private Vector3 rawInputDirection = Vector3.zero;

    private void ProcessInput()
    {
        // 1. Input Teclado (WASD / Setas)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 2. Input Mobile Analógico 360° (Auto-localiza se a referência estiver nula)
        if (mobileInput == null)
        {
            mobileInput = FindAnyObjectByType<MobileInputController>();
        }

        if (mobileInput != null)
        {
            Vector2 mInput = mobileInput.GetInputVector();
            if (mInput.sqrMagnitude > 0.0001f)
            {
                horizontal = mInput.x;
                vertical = mInput.y;
            }
        }

        // Tecla C ou Control para agachar
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl))
        {
            ToggleCrouch();
        }

        // Tecla Shift para Correr (Sprint) no PC ou empurrão forte do joystick no Mobile
        bool wantsSprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (mobileInput != null && mobileInput.IsSprinting)
        {
            wantsSprint = true;
        }

        if (playerStatus != null)
        {
            isSprinting = wantsSprint && playerStatus.stamina > 5f && !isCrouching && (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f);
        }
        else
        {
            isSprinting = wantsSprint && !isCrouching && (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f);
        }

        // Tecla Espaco para Pular
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space))
        {
            TryJump();
        }

        Vector3 inputVector = new Vector3(horizontal, 0f, vertical);
        if (inputVector.sqrMagnitude > 1f)
        {
            inputVector.Normalize();
        }

        // Converte para a direcao da camera em primeira pessoa
        if (mainCamera == null) mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();
            rawInputDirection = (camForward * inputVector.z + camRight * inputVector.x);
        }
        else
        {
            rawInputDirection = inputVector;
        }

        // Escalada de Bordas (Mantling) quando no ar movendo para frente
        if (!isGrounded && rawInputDirection.sqrMagnitude > 0.1f)
        {
            CheckMantling();
        }

        if (knockbackTime > 0f)
        {
            knockbackTime -= Time.deltaTime;
        }
    }

    private void CheckMantling()
    {
        if (isMantling) return;
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 forward = transform.forward;

        if (Physics.Raycast(origin, forward, out RaycastHit wallHit, 0.75f, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 topOrigin = wallHit.point + forward * 0.35f + Vector3.up * 1.0f;
            if (Physics.Raycast(topOrigin, Vector3.down, out RaycastHit ledgeHit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                float heightDiff = ledgeHit.point.y - transform.position.y;
                if (heightDiff >= 0.4f && heightDiff <= 1.6f)
                {
                    StartCoroutine(PerformMantling(ledgeHit.point));
                }
            }
        }
    }

    private System.Collections.IEnumerator PerformMantling(Vector3 targetLedge)
    {
        isMantling = true;
        verticalVelocity = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = targetLedge + Vector3.up * 0.1f;
        float elapsed = 0f;
        float duration = 0.38f;

        if (FirstPersonCamera.Instance != null) FirstPersonCamera.Instance.AddShake(0.06f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        transform.position = endPos;
        isGrounded = true;
        isMantling = false;
    }

    /// <summary>
    /// Executa o pulo com impulso físico e efeito de câmera. Chamado pelo teclado ou botão mobile.
    /// </summary>
    public void TryJump()
    {
        // 2 toras no ombro impedem o salto
        if (LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs >= 2)
        {
            return;
        }

        if (isGrounded && !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * gravity);
            isGrounded = false;

            if (FirstPersonCamera.Instance != null)
            {
                FirstPersonCamera.Instance.AddJumpImpulse();
            }
        }
    }

    private void UpdateFootsteps()
    {
        if (footstepAudio == null) return;

        bool isMoving = CurrentSpeed > 0.4f && isGrounded && knockbackTime <= 0f;

        if (isMoving)
        {
            float currentInterval = footstepInterval;
            if (isSprinting) currentInterval *= 0.65f;
            else if (isCrouching) currentInterval *= 1.55f;

            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                footstepTimer = currentInterval;

                if (GalicianFootstepSurfaceDetector.Instance != null)
                {
                    GalicianFootstepSurfaceDetector.Instance.PlayFootstep(transform.position, isSprinting, isCrouching);
                }
                else if (footstepClips != null && footstepClips.Length > 0)
                {
                    float baseVol = isSprinting ? 0.45f : (isCrouching ? 0.15f : 0.28f);
                    footstepAudio.pitch = Random.Range(0.88f, 1.12f);
                    footstepAudio.PlayOneShot(footstepClips[footstepIndex % footstepClips.Length], baseVol);
                    footstepIndex++;
                }
            }
        }
        else
        {
            footstepTimer = 0.08f;
        }
    }

    private void FixedUpdate()
    {
        if (isMantling) return;
        MovePlayerPhysics();
    }

    private void MovePlayerPhysics()
    {
        // 1. Determina a velocidade alvo segura (mínimo 4.5f)
        float baseSpd = speed > 0.5f ? speed : (normalSpeed > 0.5f ? normalSpeed : 5.0f);
        float targetSpeed = baseSpd;
        if (isSprinting) targetSpeed = baseSpd * sprintMultiplier;
        else if (isCrouching) targetSpeed = baseSpd * crouchMultiplier;

        // Penalidade fisiológica por transporte de toras
        if (LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs > 0)
        {
            if (LogCarryingSystem.Instance.currentLogs >= 2)
            {
                isSprinting = false;
                targetSpeed = Mathf.Min(targetSpeed, 3.2f);
            }
            else
            {
                targetSpeed *= 0.85f;
            }
        }

        Vector3 moveDir = rawInputDirection;
        float inputMag = Mathf.Clamp01(rawInputDirection.magnitude);
        if (isGrounded && groundNormal != Vector3.up && groundNormal.y > 0.40f)
        {
            // Projeta o vetor de movimento ao longo do relevo preservando a magnitude analogica do joystick
            moveDir = Vector3.ProjectOnPlane(rawInputDirection, groundNormal).normalized * inputMag;
        }
        Vector3 targetVelocity = moveDir * targetSpeed;

        // 2. Aceleração / Desaceleração física responsiva com reversão rápida
        float accelRate = (rawInputDirection.sqrMagnitude > 0.01f) ? acceleration : deceleration;
        if (Vector3.Dot(currentVelocity, targetVelocity) < 0f) accelRate *= 1.8f;
        if (!isGrounded) accelRate *= airControl;

        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, accelRate * Time.fixedDeltaTime);

        // Aplica Knockback se houver
        if (knockbackTime > 0f)
        {
            currentVelocity = knockbackVelocity;
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.fixedDeltaTime * 8f);
        }

        // 3. Calculo do deslocamento horizontal com detecção segura de obstáculos e deslizamento tangencial
        Vector3 horizontalMove = currentVelocity * Time.fixedDeltaTime;
        Vector3 finalHorizontalMove = SolveObstacleCollisions(horizontalMove);

        // 4. Calculo da gravidade e pulo
        if (!isGrounded)
        {
            verticalVelocity -= gravity * Time.fixedDeltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -28f); // Terminal velocity
        }
        Vector3 verticalMove = Vector3.up * (verticalVelocity * Time.fixedDeltaTime);

        // 5. Nova posição proposta
        Vector3 proposedPosition = transform.position + finalHorizontalMove + verticalMove;

        // 6. Ground Snapping seguro e suave baseado na malha do terreno procedural e colisores de superfície
        float terrainFloorY = TerrainMeshGenerator.GetTerrainHeight(proposedPosition.x, proposedPosition.z);

        // Checagem de raio vertical para suportar pontes, pisos de madeira e pedras
        if (Physics.Raycast(proposedPosition + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 5.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform))
            {
                terrainFloorY = Mathf.Max(terrainFloorY, hit.point.y);
                groundNormal = hit.normal;
            }
        }
        else
        {
            groundNormal = Vector3.up;
        }

        // 7. Aderência suave ao relevo (Step-offset e descida de ladeira sem quicar)
        float currentY = transform.position.y;
        float heightDiff = terrainFloorY - currentY;

        if (isGrounded)
        {
            // Subida de degrau ou aclive moderado (Step-offset até 0.35m)
            if (heightDiff > 0f && heightDiff <= stepOffset)
            {
                proposedPosition.y = Mathf.MoveTowards(currentY, terrainFloorY, (targetSpeed + 12f) * Time.fixedDeltaTime);
                verticalVelocity = 0f;
                isGrounded = true;
            }
            // Descida suave colada ao solo (evita virar airborne e quicar)
            else if (heightDiff < 0f && heightDiff >= -0.75f)
            {
                proposedPosition.y = Mathf.MoveTowards(currentY, terrainFloorY, 24f * Time.fixedDeltaTime);
                verticalVelocity = 0f;
                isGrounded = true;
            }
            else if (proposedPosition.y <= terrainFloorY + 0.05f)
            {
                proposedPosition.y = terrainFloorY;
                verticalVelocity = 0f;
                isGrounded = true;
            }
            else
            {
                // Caiu de penhasco ou desnível grande
                isGrounded = false;
            }
        }
        else
        {
            // Aterrissagem suave
            if (proposedPosition.y <= terrainFloorY + 0.05f)
            {
                proposedPosition.y = terrainFloorY;
                verticalVelocity = 0f;
                isGrounded = true;
            }
        }

        // Aplica a posição calculada
        transform.position = proposedPosition;

        if (rb != null)
        {
            rb.position = proposedPosition;
        }
    }

    /// <summary>
    /// Resolve colisões contra obstáculos verticais (troncos de árvores, rochedos, ruínas e muros)
    /// com deslizamento tangencial fluído, ignorando pequenos desníveis de relevo menores que o step-offset.
    /// </summary>
    private Vector3 SolveObstacleCollisions(Vector3 moveDelta)
    {
        if (moveDelta.sqrMagnitude < 0.00001f) return Vector3.zero;

        Vector3 moveDir = moveDelta.normalized;
        float moveDist = moveDelta.magnitude;

        // Varre na altura da coxa (0.50m - acima do step offset) e no peito (1.10m - troncos/paredes)
        float[] checkHeights = new float[] { 0.50f, 1.10f };

        for (int h = 0; h < checkHeights.Length; h++)
        {
            Vector3 origin = transform.position + Vector3.up * checkHeights[h];
            if (Physics.SphereCast(origin, 0.28f, moveDir, out RaycastHit hit, moveDist + 0.08f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform))
                {
                    bool isTerrain = hit.collider.GetComponent<TerrainMeshGenerator>() != null || 
                                     hit.collider.gameObject.name.ToLower().Contains("terrain");

                    // Se for terreno, só bloqueia se for um penhasco quase vertical (> 65 graus)
                    // Se for árvore, rocha, ruína ou muro, bloqueia sempre com deslizamento limpo
                    if (!isTerrain || hit.normal.y < 0.42f)
                    {
                        Vector3 normal = hit.normal;
                        normal.y = 0f;
                        if (normal.sqrMagnitude > 0.01f)
                        {
                            normal.Normalize();
                            // Deslizamento tangencial na superfície do obstáculo
                            Vector3 slideMove = Vector3.ProjectOnPlane(moveDelta, normal);
                            slideMove.y = 0f;

                            // Checagem secundária contra quinas/cantos
                            if (slideMove.sqrMagnitude > 0.0001f)
                            {
                                if (Physics.SphereCast(origin + normal * 0.02f, 0.25f, slideMove.normalized, out RaycastHit secondHit, slideMove.magnitude + 0.04f, ~0, QueryTriggerInteraction.Ignore))
                                {
                                    if (secondHit.collider != null && secondHit.collider.transform != transform && !secondHit.collider.transform.IsChildOf(transform))
                                    {
                                        bool isSecTerrain = secondHit.collider.GetComponent<TerrainMeshGenerator>() != null || 
                                                            secondHit.collider.gameObject.name.ToLower().Contains("terrain");
                                        if (!isSecTerrain || secondHit.normal.y < 0.42f)
                                        {
                                            return Vector3.zero;
                                        }
                                    }
                                }
                            }
                            return slideMove;
                        }
                        return Vector3.zero;
                    }
                }
            }
        }

        return moveDelta;
    }
}