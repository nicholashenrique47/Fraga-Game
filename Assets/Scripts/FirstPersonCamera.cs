using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerBody;
    public CameraTouchController touchController;
    private Camera cam;

    [Header("Settings")]
    public float sensitivity = 0.2f;
    public float topClamp = -90f;
    public float bottomClamp = 90f;

    private float xRotation = 0f;

    [Header("Headbob Settings")]
    public float bobSpeed = 10f;
    public float bobAmount = 0.05f;
    private float defaultYPos = 1.8f;
    private float timer = 0f;

    [Header("AAA Camera Polish")]
    public float baseFOV = 60f;
    public float sprintFOV = 75f;
    private float shakeIntensity = 0f;
    private float tiltAngle = 0f;
    private float targetTiltAngle = 0f;

    public static FirstPersonCamera Instance;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = baseFOV;
    }

    private void Start()
    {
        transform.localPosition = new Vector3(0, defaultYPos, 0);
    }

    public void AddShake(float intensity)
    {
        shakeIntensity = Mathf.Min(shakeIntensity + intensity, 0.5f);
    }

    public void SetTilt(float angle)
    {
        targetTiltAngle = angle;
    }

    private void HandleHeadbobAndPolish()
    {
        float speed = 0f;
        Rigidbody rb = playerBody.GetComponent<Rigidbody>();
        if (rb != null)
        {
            speed = rb.velocity.magnitude;
        }

        // Headbob
        if (speed > 0.1f)
        {
            timer += Time.deltaTime * bobSpeed;
            float newY = defaultYPos + Mathf.Sin(timer) * bobAmount;
            transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
        }
        else
        {
            timer = 0;
            transform.localPosition = new Vector3(transform.localPosition.x, Mathf.Lerp(transform.localPosition.y, defaultYPos, Time.deltaTime * bobSpeed), transform.localPosition.z);
        }

        // Dynamic FOV
        if (cam != null)
        {
            float targetFOV = speed > 3f ? sprintFOV : baseFOV;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 5f);
        }

        // Camera Shake Decay
        if (shakeIntensity > 0)
        {
            shakeIntensity -= Time.deltaTime * 0.5f;
            if (shakeIntensity < 0) shakeIntensity = 0;
        }

        // Tilt Interpolation
        tiltAngle = Mathf.Lerp(tiltAngle, targetTiltAngle, Time.deltaTime * 2f);
    }

    private void LateUpdate()
    {
        if (touchController == null || playerBody == null) return;

        Vector2 input = touchController.GetTouchDelta();
        
        float mouseX = input.x * sensitivity;
        float mouseY = input.y * sensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, topClamp, bottomClamp);

        // Shake offset
        float shakeX = Random.Range(-1f, 1f) * shakeIntensity;
        float shakeY = Random.Range(-1f, 1f) * shakeIntensity;

        // Rodar camara cima/baixo + Tilt + Shake
        transform.localRotation = Quaternion.Euler(xRotation + shakeY, 0f, tiltAngle + shakeX);
        
        // Rodar jogador esquerda/direita
        playerBody.Rotate(Vector3.up * mouseX);
        
        HandleHeadbobAndPolish();
    }
}