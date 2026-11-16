using UnityEngine;

/// <summary>
/// WeaponMotionSway: Balanco e inercia de armas legadas (corrigido para ler PlayerController.CurrentSpeed).
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
        playerController = PlayerController.Instance ?? GetComponentInParent<PlayerController>() ?? FindAnyObjectByType<PlayerController>();
        playerStatus = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
    }

    private void UpgradeChildWeaponToAAA()
    {
        bool isAxeHolder = gameObject.name.Contains("Weapon_Holder") || gameObject.name.Contains("Axe") || transform.Find("Held_Survival_Axe") != null;
        if (!isAxeHolder) return;

        Transform existingAxe = transform.Find("Held_Survival_Axe");
        if (existingAxe != null && existingAxe.Find("Leather_Grip") != null)
        {
            return;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.Contains("Axe") || child.name.Contains("Machado") || child.name.Contains("Cylinder"))
            {
                Destroy(child.gameObject);
            }
        }

        Material wood = Resources.Load<Material>("Materials/Mat_Wood_Carved")
            ?? Resources.Load<Material>("Materials/Wood_Bark")
            ?? Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        Material steel = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        Material leather = Resources.Load<Material>("Materials/Mat_Leather_Wrap") ?? wood;

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
        if (FragaInputRouter.Instance != null && !FragaInputRouter.Instance.IsCameraLookAllowed)
            return;

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        float targetRotX = Mathf.Clamp(-mouseY * swayAmount, -maxSwayAmount, maxSwayAmount);
        float targetRotY = Mathf.Clamp(mouseX * swayAmount, -maxSwayAmount, maxSwayAmount);
        float targetRotZ = Mathf.Clamp(mouseX * (swayAmount * 0.7f), -maxSwayAmount, maxSwayAmount);

        Quaternion targetRotation = initialRotation * Quaternion.Euler(targetRotX, targetRotY, targetRotZ);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * swaySmooth);
    }

    private void HandleBobbing()
    {
        if (isAttacking) return;

        if (playerController == null) playerController = PlayerController.Instance;
        if (playerStatus == null) playerStatus = PlayerStatus.Instance;

        float curSpeed = playerController != null ? playerController.CurrentSpeed : 0f;
        bool isMoving = curSpeed > 0.35f;
        bool isSprinting = isMoving && ((playerController != null && playerController.isSprinting) || (playerStatus != null && playerStatus.isSprinting));

        if (isMoving)
        {
            float speed = isSprinting ? sprintBobSpeed : walkBobSpeed;
            float amount = isSprinting ? sprintBobAmount : walkBobAmount;

            bobTimer += Time.deltaTime * speed;

            float bobX = Mathf.Cos(bobTimer * 0.5f) * amount * 0.7f;
            float bobY = Mathf.Sin(bobTimer) * amount;

            Vector3 targetPos = initialPosition + new Vector3(bobX, bobY, 0);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * 10f);
        }
        else
        {
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
            float t = progress / 0.35f;
            transform.localPosition = Vector3.Lerp(initialPosition, initialPosition + new Vector3(0.1f, 0.15f, -0.1f), t);
            transform.localRotation = Quaternion.Slerp(initialRotation, initialRotation * Quaternion.Euler(-25f, 20f, 15f), t);
        }
        else if (progress <= 0.7f)
        {
            float t = (progress - 0.35f) / 0.35f;
            Vector3 strikePos = initialPosition + new Vector3(-0.15f, -0.2f, 0.25f);
            Quaternion strikeRot = initialRotation * Quaternion.Euler(55f, -40f, -25f);
            transform.localPosition = Vector3.Lerp(initialPosition + new Vector3(0.1f, 0.15f, -0.1f), strikePos, t * t);
            transform.localRotation = Quaternion.Slerp(initialRotation * Quaternion.Euler(-25f, 20f, 15f), strikeRot, t * t);
        }
        else if (progress < 1.0f)
        {
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
