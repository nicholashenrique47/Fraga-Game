using UnityEngine;

/// <summary>
/// WeaponMotionSway: Adiciona animações procedurais AAA de arma em primeira pessoa estilo The Forest:
/// - Weapon Sway (Inércia ao olhar para os lados)
/// - Weapon Bobbing (Balanço rítmico ao caminhar e correr)
/// - Attack Swing Animation (Golpe físico fluído e recuo com impacto)
/// </summary>
public class WeaponMotionSway : MonoBehaviour
{
    [Header("Weapon Sway Settings")]
    public float swayAmount = 1.8f;
    public float maxSwayAmount = 4.5f;
    public float swaySmooth = 7.0f;

    [Header("Weapon Bobbing Settings")]
    public float walkBobSpeed = 9.0f;
    public float walkBobAmount = 0.035f;
    public float sprintBobSpeed = 14.0f;
    public float sprintBobAmount = 0.07f;

    [Header("Attack Motion")]
    public float attackDuration = 0.28f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private float bobTimer = 0f;
    private bool isAttacking = false;
    private float attackTimer = 0f;

    private PlayerController playerController;
    private PlayerStatus playerStatus;

    private void Start()
    {
        UpgradeChildWeaponToAAA();
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
        playerController = GetComponentInParent<PlayerController>();
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        playerStatus = FindAnyObjectByType<PlayerStatus>();
    }

    private void UpgradeChildWeaponToAAA()
    {
        // Substitui o machado de cilindros primitivos pelo modelo forjado AAA
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.Contains("Axe") || child.name.Contains("Machado") || child.name.Contains("Cylinder"))
            {
                Destroy(child.gameObject);
            }
        }

        Material wood = Resources.Load<Material>("Materials/Wood_Bark");
        if (wood == null) wood = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        Material steel = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        Material leather = Resources.Load<Material>("Materials/Celtic_Stone");

        GameObject newAxe = ProceduralSurvivalAxeGenerator.BuildAxeGameObject(wood, steel, leather);
        newAxe.transform.SetParent(transform, false);
        newAxe.transform.localScale = Vector3.one * 0.72f;
        newAxe.transform.localPosition = new Vector3(0.20f, -0.22f, 0.38f);
        newAxe.transform.localRotation = Quaternion.Euler(-25f, 65f, -15f);
    }

    private void Update()
    {
        HandleSway();
        HandleBobbing();
        HandleAttackMotion();
    }

    private void HandleSway()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        // Rotação oposta ao movimento do olhar (inércia física)
        float targetRotX = Mathf.Clamp(-mouseY * swayAmount, -maxSwayAmount, maxSwayAmount);
        float targetRotY = Mathf.Clamp(mouseX * swayAmount, -maxSwayAmount, maxSwayAmount);
        float targetRotZ = Mathf.Clamp(mouseX * (swayAmount * 0.7f), -maxSwayAmount, maxSwayAmount);

        Quaternion targetRotation = initialRotation * Quaternion.Euler(targetRotX, targetRotY, targetRotZ);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * swaySmooth);
    }

    private void HandleBobbing()
    {
        if (isAttacking) return;

        bool isMoving = false;
        bool isSprinting = false;

        if (playerController != null)
        {
            Vector3 vel = playerController.GetComponent<Rigidbody>() != null 
                ? playerController.GetComponent<Rigidbody>().linearVelocity 
                : Vector3.zero;
            vel.y = 0;
            isMoving = vel.magnitude > 0.4f;
        }

        if (playerStatus != null)
        {
            isSprinting = playerStatus.isSprinting && isMoving;
        }

        if (isMoving)
        {
            float speed = isSprinting ? sprintBobSpeed : walkBobSpeed;
            float amount = isSprinting ? sprintBobAmount : walkBobAmount;

            bobTimer += Time.deltaTime * speed;

            // Movimento em 8 (infinito) característico de The Forest
            float bobX = Mathf.Cos(bobTimer * 0.5f) * amount * 0.7f;
            float bobY = Mathf.Sin(bobTimer) * amount;

            Vector3 targetPos = initialPosition + new Vector3(bobX, bobY, 0);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * 10f);
        }
        else
        {
            // Respiração ociosa suave
            bobTimer += Time.deltaTime * 1.5f;
            float idleBobY = Mathf.Sin(bobTimer) * 0.006f;
            Vector3 targetPos = initialPosition + new Vector3(0, idleBobY, 0);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * 4f);
        }
    }

    public void TriggerAttackMotion()
    {
        isAttacking = true;
        attackTimer = 0f;
    }

    private void HandleAttackMotion()
    {
        if (!isAttacking) return;

        attackTimer += Time.deltaTime;
        float progress = attackTimer / attackDuration;

        if (progress <= 0.35f)
        {
            // Fase de elevação / Preparação do golpe
            float t = progress / 0.35f;
            transform.localPosition = Vector3.Lerp(initialPosition, initialPosition + new Vector3(0.1f, 0.15f, -0.1f), t);
            transform.localRotation = Quaternion.Slerp(initialRotation, initialRotation * Quaternion.Euler(-25f, 20f, 15f), t);
        }
        else if (progress <= 0.7f)
        {
            // Fase de corte (Golpe descendente rápido e violento)
            float t = (progress - 0.35f) / 0.35f;
            Vector3 strikePos = initialPosition + new Vector3(-0.15f, -0.2f, 0.25f);
            Quaternion strikeRot = initialRotation * Quaternion.Euler(55f, -40f, -25f);
            transform.localPosition = Vector3.Lerp(initialPosition + new Vector3(0.1f, 0.15f, -0.1f), strikePos, t * t);
            transform.localRotation = Quaternion.Slerp(initialRotation * Quaternion.Euler(-25f, 20f, 15f), strikeRot, t * t);
        }
        else if (progress < 1.0f)
        {
            // Retorno à posição de guarda
            float t = (progress - 0.7f) / 0.3f;
            Vector3 strikePos = initialPosition + new Vector3(-0.15f, -0.2f, 0.25f);
            Quaternion strikeRot = initialRotation * Quaternion.Euler(55f, -40f, -25f);
            transform.localPosition = Vector3.Lerp(strikePos, initialPosition, t);
            transform.localRotation = Quaternion.Slerp(strikeRot, initialRotation, t);
        }
        else
        {
            isAttacking = false;
            transform.localPosition = initialPosition;
            transform.localRotation = initialRotation;
        }
    }
}
