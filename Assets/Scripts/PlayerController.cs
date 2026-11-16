using UnityEngine;

/// <summary>
/// Controlador Fisico de Movimentacao do Jogador (Padrao AAA The Forest).
/// Integrado ao FragaInputRouter para entrada hibrida PC + Mobile sem conflitos de tecla.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement Settings")]
    public float speed = 5f;
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
    public float stepOffset = 0.48f;

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
    private Light spotCookieLight;
    private static Texture2D s_lanternCookieTex;

    [Header("Input Dependencies")]
    public MobileInputController mobileInput;

    // Estado fisico interno
    private Rigidbody rb;
    private CapsuleCollider playerCol;
    private Camera mainCamera;
    private PlayerStatus playerStatus;
    private SanitySystem sanityCache;

    private Vector3 currentVelocity;
    private float verticalVelocity = 0f;
    private bool isGrounded = true;
    private Vector3 groundNormal = Vector3.up;
    public bool isSwimming { get; private set; }
    private float coyoteTimer = 0f;
    private float jumpBufferTimer = 0f;
    private float peakFallSpeed = 0f;

    private Vector3 knockbackVelocity;
    private float knockbackTime;
    private bool isMantling = false;
    private static readonly float[] ObstacleCheckHeights = { 0.68f, 1.15f };

    public bool isSprinting { get; private set; }
    public bool IsGrounded => isGrounded;
    public bool IsMoving => currentVelocity.sqrMagnitude > 0.05f;
    public float CurrentSpeed => new Vector2(currentVelocity.x, currentVelocity.z).magnitude;
    public bool IsLighterOn => isLighterOn;

    private void Awake()
    {
        Instance = this;
        StartLighter();

        playerCol = GetComponent<CapsuleCollider>();
        if (playerCol == null)
        {
            playerCol = gameObject.AddComponent<CapsuleCollider>();
        }
        playerCol.center = new Vector3(0, standingHeight * 0.5f, 0);
        playerCol.radius = 0.35f;
        playerCol.height = standingHeight;

        foreach (var c in GetComponentsInChildren<Collider>())
        {
            if (c != playerCol)
            {
                Destroy(c);
            }
        }

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
        sanityCache = GetComponent<SanitySystem>();

        // Auto-anexa subsistemas essenciais + novos sistemas centrais
        if (GetComponent<FragaInputRouter>() == null) gameObject.AddComponent<FragaInputRouter>();
        if (GetComponent<InteractionDetector>() == null) gameObject.AddComponent<InteractionDetector>();
        if (GetComponent<FirstPersonViewmodelController>() == null) gameObject.AddComponent<FirstPersonViewmodelController>();
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
        if (GetComponent<FragaStealthSystem>() == null) gameObject.AddComponent<FragaStealthSystem>();

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
        if (sanityCache == null) sanityCache = GetComponent<SanitySystem>() ?? FindAnyObjectByType<SanitySystem>();
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
            lighterLight.shadows = LightShadows.None;

            GameObject spotObj = new GameObject("PlayerLanternBeam");
            spotObj.transform.SetParent(Camera.main != null ? Camera.main.transform : this.transform, false);
            spotObj.transform.localPosition = new Vector3(0.16f, -0.06f, 0.25f);
            spotObj.transform.localRotation = Quaternion.identity;
            spotCookieLight = spotObj.AddComponent<Light>();
            spotCookieLight.type = LightType.Spot;
            spotCookieLight.range = 24f;
            spotCookieLight.spotAngle = 54f;
            spotCookieLight.intensity = 0f;
            spotCookieLight.color = new Color(1f, 0.84f, 0.62f);
            spotCookieLight.shadows = LightShadows.None;
            spotCookieLight.cookie = GetOrCreateLanternCookie();
        }
    }

    private static Texture2D GetOrCreateLanternCookie()
    {
        if (s_lanternCookieTex != null) return s_lanternCookieTex;
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Clamp01(1f - r);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.58f) * 3.2f) * 0.22f;
                float v = Mathf.Pow(core, 1.55f) + ring * (1f - Mathf.SmoothStep(0.7f, 1.0f, r));
                px[y * size + x] = new Color(v, v, v, Mathf.Clamp01(v));
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        s_lanternCookieTex = tex;
        return s_lanternCookieTex;
    }

    public void ToggleLighter()
    {
        isLighterOn = !isLighterOn;
    }

    private void UpdateLighter()
    {
        bool pressedLight = FragaInputRouter.Instance != null
            ? FragaInputRouter.Instance.LightTogglePressedThisFrame
            : Input.GetKeyDown(KeyCode.L);

        if (pressedLight)
        {
            isLighterOn = !isLighterOn;
            if (sanityCache != null) sanityCache.ToggleLamp();
        }

        if (lighterLight != null)
        {
            bool hasOffHandLight = InventoryAdvanced.Instance != null && (InventoryAdvanced.Instance.equippedOffHandId == 303 || InventoryAdvanced.Instance.equippedOffHandId == 304);
            float targetIntensity = isLighterOn ? (hasOffHandLight ? 0.9f : 2.1f) + Random.Range(-0.12f, 0.12f) : 0f;
            lighterLight.intensity = Mathf.Lerp(lighterLight.intensity, targetIntensity, Time.deltaTime * 10f);
        }

        if (spotCookieLight != null)
        {
            bool hasLanternOffHand = InventoryAdvanced.Instance != null && InventoryAdvanced.Instance.equippedOffHandId == 304;
            bool beamOn = isLighterOn || hasLanternOffHand;
            float targetBeam = beamOn ? (hasLanternOffHand ? 2.4f : 1.35f) + Random.Range(-0.06f, 0.06f) : 0f;
            spotCookieLight.intensity = Mathf.Lerp(spotCookieLight.intensity, targetBeam, Time.deltaTime * 10f);
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
        var router = FragaInputRouter.Instance;

        float horizontal = 0f;
        float vertical = 0f;
        bool wantsSprint = false;
        bool crouchPressed = false;
        bool jumpPressed = false;

        if (router != null)
        {
            horizontal = router.MoveInput.x;
            vertical = router.MoveInput.y;
            wantsSprint = router.WantsSprint;
            crouchPressed = router.CrouchPressedThisFrame;
            jumpPressed = router.JumpPressedThisFrame;
        }
        else
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");
            if (mobileInput == null) mobileInput = FindAnyObjectByType<MobileInputController>();
            if (mobileInput != null)
            {
                Vector2 mInput = mobileInput.GetInputVector();
                if (mInput.sqrMagnitude > 0.0001f)
                {
                    horizontal = mInput.x;
                    vertical = mInput.y;
                }
                wantsSprint = mobileInput.IsSprinting;
            }
            wantsSprint = wantsSprint || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            crouchPressed = Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
            jumpPressed = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);
        }

        if (crouchPressed)
        {
            ToggleCrouch();
        }

        bool isMovingInput = (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f);
        if (playerStatus != null)
        {
            isSprinting = wantsSprint && playerStatus.stamina > 5f && !isCrouching && !playerStatus.isFractured && isMovingInput;
        }
        else
        {
            isSprinting = wantsSprint && !isCrouching && isMovingInput;
        }

        if (jumpPressed)
        {
            jumpBufferTimer = 0.13f;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        }

        if (jumpBufferTimer > 0f)
        {
            TryJump();
        }

        Vector3 inputVector = new Vector3(horizontal, 0f, vertical);
        if (inputVector.sqrMagnitude > 1f)
        {
            inputVector.Normalize();
        }

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
        if (isMantling || isSwimming) return;
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 forward = transform.forward;

        if (Physics.Raycast(origin, forward, out RaycastHit wallHit, 0.75f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (wallHit.collider == null || wallHit.collider.transform == transform || wallHit.collider.transform.IsChildOf(transform)) return;
            string hitName = wallHit.collider.gameObject.name.ToLower();
            if (hitName.Contains("tree") || hitName.Contains("foliage") || hitName.Contains("bush") || hitName.Contains("pine")) return;

            Vector3 topOrigin = wallHit.point + forward * 0.35f + Vector3.up * 1.0f;
            if (Physics.Raycast(topOrigin, Vector3.down, out RaycastHit ledgeHit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                // So escala bordas quase planas e nao copas de arvores
                if (ledgeHit.normal.y > 0.85f && ledgeHit.collider != null && !ledgeHit.collider.gameObject.name.ToLower().Contains("tree"))
                {
                    float heightDiff = ledgeHit.point.y - transform.position.y;
                    if (heightDiff >= 0.4f && heightDiff <= 1.55f)
                    {
                        StartCoroutine(PerformMantling(ledgeHit.point));
                    }
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
        float duration = 0.36f;

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

    public void TryJump()
    {
        if (LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs >= 2)
        {
            return;
        }

        if (isSwimming)
        {
            verticalVelocity = 3.2f;
            return;
        }

        if ((isGrounded || coyoteTimer > 0f) && !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * gravity);
            isGrounded = false;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            peakFallSpeed = 0f;

            if (FirstPersonCamera.Instance != null)
            {
                FirstPersonCamera.Instance.AddJumpImpulse();
            }
            if (FirstPersonViewmodelController.Instance != null)
            {
                FirstPersonViewmodelController.Instance.TriggerVerticalImpulse(-0.025f);
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

                if (FragaStealthSystem.Instance != null)
                {
                    FragaStealthSystem.Instance.EmitNoisePulse(isSprinting ? 0.72f : (isCrouching ? 0.18f : 0.40f));
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
        float baseSpd = speed > 0.5f ? speed : (normalSpeed > 0.5f ? normalSpeed : 5.0f);
        float targetSpeed = baseSpd;
        if (isSprinting) targetSpeed = baseSpd * sprintMultiplier;
        else if (isCrouching) targetSpeed = baseSpd * crouchMultiplier;

        float curWaterY = FragaWorldBlueprint.GetWaterSurfaceY(transform.position.x, transform.position.z);
        float curFloorY = TerrainMeshGenerator.GetTerrainHeight(transform.position.x, transform.position.z);
        float curWaterDepth = curWaterY - Mathf.Max(curFloorY, transform.position.y - 0.1f);
        if (isSwimming)
        {
            targetSpeed *= 0.64f;
        }
        else if (curWaterDepth > 0.25f)
        {
            targetSpeed *= 0.78f;
        }

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
            moveDir = Vector3.ProjectOnPlane(rawInputDirection, groundNormal).normalized * inputMag;
        }
        Vector3 targetVelocity = moveDir * targetSpeed;

        float accelRate = (rawInputDirection.sqrMagnitude > 0.01f) ? acceleration : deceleration;
        if (Vector3.Dot(currentVelocity, targetVelocity) < 0f) accelRate *= 1.8f;
        if (!isGrounded) accelRate *= airControl;

        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, accelRate * Time.fixedDeltaTime);

        if (knockbackTime > 0f)
        {
            currentVelocity = knockbackVelocity;
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.fixedDeltaTime * 8f);
        }

        Vector3 horizontalMove = currentVelocity * Time.fixedDeltaTime;
        Vector3 finalHorizontalMove = SolveObstacleCollisions(horizontalMove);

        if (!isGrounded)
        {
            float gravScale = verticalVelocity < 0f ? 1.42f : 1.0f;
            verticalVelocity -= gravity * gravScale * Time.fixedDeltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -28f);
            if (verticalVelocity < 0f) peakFallSpeed = Mathf.Max(peakFallSpeed, -verticalVelocity);
        }
        Vector3 verticalMove = Vector3.up * (verticalVelocity * Time.fixedDeltaTime);

        Vector3 proposedPosition = transform.position + finalHorizontalMove + verticalMove;

        float worldMaxX = Mathf.Max(64f, FragaWorldBlueprint.Width - 22f);
        float worldMaxZ = Mathf.Max(64f, FragaWorldBlueprint.Height - 22f);
        proposedPosition.x = Mathf.Clamp(proposedPosition.x, 22f, worldMaxX);
        proposedPosition.z = Mathf.Clamp(proposedPosition.z, 22f, worldMaxZ);

        float terrainFloorY = TerrainMeshGenerator.GetTerrainHeight(proposedPosition.x, proposedPosition.z);

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

        float waterSurfaceY = FragaWorldBlueprint.GetWaterSurfaceY(proposedPosition.x, proposedPosition.z);
        float submergeDepth = waterSurfaceY - terrainFloorY;
        float swimSurfaceTargetY = waterSurfaceY - 1.05f;
        isSwimming = (submergeDepth > 1.05f) && (proposedPosition.y <= swimSurfaceTargetY + 0.12f);

        bool wasGrounded = isGrounded;
        float currentY = transform.position.y;
        float heightDiff = terrainFloorY - currentY;

        if (isSwimming)
        {
            proposedPosition.y = Mathf.Max(terrainFloorY, Mathf.Lerp(proposedPosition.y, swimSurfaceTargetY, Time.fixedDeltaTime * 7f));
            verticalVelocity = Mathf.Lerp(verticalVelocity, 0f, Time.fixedDeltaTime * 5f);
            isGrounded = false;
            groundNormal = Vector3.up;
        }
        else if (isGrounded)
        {
            if (heightDiff > 0f && heightDiff <= stepOffset)
            {
                proposedPosition.y = Mathf.MoveTowards(currentY, terrainFloorY, (targetSpeed + 12f) * Time.fixedDeltaTime);
                verticalVelocity = 0f;
                isGrounded = true;
            }
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
                isGrounded = false;
            }
        }
        else
        {
            if (proposedPosition.y <= terrainFloorY + 0.05f)
            {
                proposedPosition.y = terrainFloorY;
                verticalVelocity = 0f;
                isGrounded = true;
            }
        }

        if (isGrounded)
        {
            coyoteTimer = 0.12f;
            if (!wasGrounded && peakFallSpeed > 4.0f)
            {
                if (peakFallSpeed > 12.5f && playerStatus != null)
                {
                    float fallDmg = (peakFallSpeed - 12.5f) * 6.5f;
                    playerStatus.TakeDamage(fallDmg);
                    if (peakFallSpeed > 15.2f)
                    {
                        playerStatus.isFractured = true;
                    }
                }
                if (FirstPersonCamera.Instance != null)
                    FirstPersonCamera.Instance.AddLandingDip(peakFallSpeed);
                if (FirstPersonViewmodelController.Instance != null)
                    FirstPersonViewmodelController.Instance.TriggerVerticalImpulse(Mathf.Clamp(peakFallSpeed * 0.005f, 0.02f, 0.06f));
                if (GalicianFootstepSurfaceDetector.Instance != null)
                {
                    GalicianFootstepSurfaceDetector.Instance.PlayFootstep(proposedPosition, true, false);
                }
                else if (footstepAudio != null && footstepClips != null && footstepClips.Length > 0)
                {
                    footstepAudio.pitch = Random.Range(0.76f, 0.90f);
                    footstepAudio.PlayOneShot(footstepClips[0], Mathf.Clamp01(peakFallSpeed / 14f) * 0.55f);
                }
            }
            peakFallSpeed = 0f;
            if (jumpBufferTimer > 0f) TryJump();
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
        }

        transform.position = proposedPosition;

        if (rb != null)
        {
            rb.position = proposedPosition;
        }
    }

    private Vector3 SolveObstacleCollisions(Vector3 moveDelta)
    {
        if (moveDelta.sqrMagnitude < 0.00001f) return Vector3.zero;

        Vector3 moveDir = moveDelta.normalized;
        float moveDist = moveDelta.magnitude;

        for (int h = 0; h < ObstacleCheckHeights.Length; h++)
        {
            Vector3 origin = transform.position + Vector3.up * ObstacleCheckHeights[h];
            if (Physics.SphereCast(origin, 0.25f, moveDir, out RaycastHit hit, moveDist + 0.08f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform))
                {
                    bool isTerrain = hit.collider.GetComponent<TerrainMeshGenerator>() != null ||
                                     hit.collider.gameObject.name.ToLower().Contains("terrain");

                    if ((!isTerrain && hit.normal.y < 0.56f) || (isTerrain && hit.normal.y < 0.42f))
                    {
                        Vector3 normal = hit.normal;
                        normal.y = 0f;
                        if (normal.sqrMagnitude > 0.01f)
                        {
                            normal.Normalize();
                            Vector3 slideMove = Vector3.ProjectOnPlane(moveDelta, normal);
                            slideMove.y = 0f;

                            if (slideMove.sqrMagnitude > 0.0001f)
                            {
                                if (Physics.SphereCast(origin + normal * 0.02f, 0.25f, slideMove.normalized, out RaycastHit secondHit, slideMove.magnitude + 0.04f, ~0, QueryTriggerInteraction.Ignore))
                                {
                                    if (secondHit.collider != null && secondHit.collider.transform != transform && !secondHit.collider.transform.IsChildOf(transform))
                                    {
                                        bool isSecTerrain = secondHit.collider.GetComponent<TerrainMeshGenerator>() != null ||
                                                            secondHit.collider.gameObject.name.ToLower().Contains("terrain");
                                        if ((!isSecTerrain && secondHit.normal.y < 0.56f) || (isSecTerrain && secondHit.normal.y < 0.42f))
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
