using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f; // Usado pelo meta-upgrade system
    public float moveSpeed { get { return speed; } set { speed = value; } }

    private Rigidbody rb;
    private Vector3 movement;
    private Camera mainCamera;

    private Vector3 knockbackVelocity;
    private float knockbackTime;

    [Header("Stealth Mechanics")]
    public bool isCrouching = false;
    private float normalSpeed;

        [Header("Lighter Settings")]
    public Light lighterLight;
    private bool isLighterOn = false;

    private void StartLighter()
    {
        if (lighterLight == null)
        {
            GameObject lightObj = new GameObject("PlayerLighter");
            lightObj.transform.SetParent(this.transform);
            lightObj.transform.localPosition = new Vector3(0.3f, 1.5f, 0.5f);
            lighterLight = lightObj.AddComponent<Light>();
            lighterLight.type = LightType.Point;
            lighterLight.range = 8f;
            lighterLight.intensity = 0f;
            lighterLight.color = new Color(1f, 0.6f, 0.2f);
            lighterLight.shadows = LightShadows.Soft;
        }
    }

    private void UpdateLighter()
    {
        // Toggle Isqueiro (L on PC or generic Mobile Button placeholder)
                // Toggle Isqueiro Mobile (Tap on top left corner)
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            Vector2 touchPos = Input.GetTouch(0).position;
            if (touchPos.x < Screen.width * 0.2f && touchPos.y > Screen.height * 0.8f)
            {
                isLighterOn = !isLighterOn;
            }
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            isLighterOn = !isLighterOn;
        }

        if (lighterLight != null)
        {
            float targetIntensity = isLighterOn ? 1.5f + Random.Range(-0.2f, 0.2f) : 0f;
            lighterLight.intensity = Mathf.Lerp(lighterLight.intensity, targetIntensity, Time.deltaTime * 10f);
        }
    }

    private void Awake()
    {
        StartLighter();
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
        mainCamera = Camera.main;
        normalSpeed = speed;
    }

    public void ToggleCrouch()
    {
        isCrouching = !isCrouching;
        speed = isCrouching ? normalSpeed * 0.5f : normalSpeed;
        
        // Efeito visual de agachar
        transform.localScale = isCrouching ? new Vector3(1f, 0.6f, 1f) : new Vector3(1f, 1f, 1f);
    }

    private void Update()
    {
        UpdateLighter();
        ProcessInput();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    public MobileInputController mobileInput;

    private void ProcessInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        // Se o Joystick Mobile existir, priorizÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡-lo. Caso contrÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡rio, usar teclado (para debug no PC).
        if (mobileInput != null)
        {
            Vector2 mInput = mobileInput.GetInputVector();
            horizontal = mInput.x;
            vertical = mInput.y;
        }
        else
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");
        }

        Vector3 inputVector = new Vector3(horizontal, 0f, vertical);

        if (mainCamera != null)
        {
            // Transforma o input para o espaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§o isomÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â©trico da cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢mera
            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            movement = (camForward * inputVector.z + camRight * inputVector.x).normalized;
        }
        else
        {
            // Fallback caso a cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢mera nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o seja encontrada
            movement = inputVector;
        }

        if (knockbackTime > 0f)
        {
            knockbackTime -= Time.deltaTime;
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        knockbackVelocity = force;
        knockbackTime = 0.2f; // duracao do knockback
    }

    private void MovePlayer()
    {
        Vector3 finalMovement = movement * moveSpeed;

        if (knockbackTime > 0f)
        {
            finalMovement = knockbackVelocity;
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);
        }

        if (finalMovement == Vector3.zero && knockbackTime <= 0f) return;

        if (rb != null)
        {
            Vector3 nextPos = rb.position + finalMovement * Time.fixedDeltaTime;
            nextPos.y = Mathf.PerlinNoise(nextPos.x * 0.15f, nextPos.z * 0.15f) * 1.5f + Mathf.PerlinNoise(nextPos.x * 0.03f, nextPos.z * 0.03f) * 4f;
            rb.MovePosition(nextPos);
        }
        else
        {
            transform.Translate(finalMovement * Time.deltaTime, Space.World);
            Vector3 pos = transform.position;
            pos.y = Mathf.PerlinNoise(pos.x * 0.15f, pos.z * 0.15f) * 1.5f + Mathf.PerlinNoise(pos.x * 0.03f, pos.z * 0.03f) * 4f;
            transform.position = pos;
        }

        // RotaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o suave em direÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o ao movimento
        Quaternion targetRotation = Quaternion.LookRotation(movement);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
    }
}
