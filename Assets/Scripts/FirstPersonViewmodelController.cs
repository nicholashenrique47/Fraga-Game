using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// FirstPersonViewmodelController: Rig procedural dual-arm em primeira pessoa com animacoes fluidas de:
/// - Respiracao idle, calafrios de hipotermia, caminhada/corrida/agachado e inercia de camera
/// - Combo alternado (Slash 1 / Slash 2), Golpe Pesado, Estocada de Lanca e Consumo
/// - Guarda / Escudo, Aparo Perfeito (Parry Snap), Mira de Arco (corda + flecha dinamica) e Mira de Lanca
/// - Sincronizado com InventoryAdvanced.OnEquipmentChanged e LogCarryingSystem (Zero-GC em Update).
/// </summary>
public class FirstPersonViewmodelController : MonoBehaviour
{
    public static FirstPersonViewmodelController Instance { get; private set; }

    [Header("Optional External Animator Rig (Mixamo / Asset Store)")]
    public Animator externalArmsAnimator;

    private Camera cam;
    private Transform viewmodelRoot;
    private Transform rightArmPivot;
    private Transform leftArmPivot;
    private Transform rightHandSocket;
    private Transform leftHandSocket;

    private readonly Dictionary<int, GameObject> cachedRightModels = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> cachedLeftModels = new Dictionary<int, GameObject>();

    private GameObject bowNockedArrow;
    private Transform bowStringTop;
    private Transform bowStringBottom;
    private Light heldTorchLight;
    private float heldTorchBaseIntensity = 2.6f;

    private int currentMainHandId = -1;
    private int currentOffHandId = -1;
    private bool isCarryingLogs = false;

    // Estados de animacao
    private float bobTimer = 0f;
    private float shiverPhase = 0f;
    private bool isAttacking = false;
    private float attackTimer = 0f;
    private float attackDuration = 0.30f;
    private int attackComboIndex = 0;
    private bool attackIsHeavy = false;
    private bool attackIsThrust = false;
    private int attackWeaponId = -1;

    private bool isGuarding = false;
    private float guardBlend = 0f;
    private float parryTimer = 0f;

    private bool isBowDrawing = false;
    private float bowDraw01 = 0f;

    private bool isSpearAiming = false;
    private float spearCharge01 = 0f;

    private float consumeTimer = 0f;
    private float blockImpactTimer = 0f;
    private float recoilOffset = 0f;
    private float verticalImpulseOffset = 0f;

    private static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
    private static readonly int AnimSprintHash = Animator.StringToHash("IsSprinting");
    private static readonly int AnimCrouchHash = Animator.StringToHash("IsCrouching");
    private static readonly int AnimGuardHash = Animator.StringToHash("IsGuarding");
    private static readonly int AnimAttackHash = Animator.StringToHash("Attack");
    private static readonly int AnimHeavyHash = Animator.StringToHash("AttackHeavy");
    private static readonly int AnimConsumeHash = Animator.StringToHash("Consume");
    private static readonly int AnimBowDrawHash = Animator.StringToHash("BowDraw");

    private readonly Vector3 rightRestPos = new Vector3(0.27f, -0.25f, 0.42f);
    private readonly Vector3 leftRestPos = new Vector3(-0.29f, -0.27f, 0.38f);

    private Material skinMat;
    private Material sleeveMat;
    private Material woodMat;
    private Material steelMat;
    private Material leatherMat;
    private Material boneMat;

    private InventoryAdvanced inventoryCache;
    private PlayerController playerCache;
    private BodyTemperatureSystem bodyTempCache;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    private void Start()
    {
        cam = Camera.main;
        playerCache = PlayerController.Instance ?? GetComponent<PlayerController>();
        bodyTempCache = BodyTemperatureSystem.Instance ?? GetComponent<BodyTemperatureSystem>();
        inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();

        BuildDualArmRig();
        RemoveLegacyWeaponHolders();

        if (inventoryCache != null)
        {
            inventoryCache.OnEquipmentChanged += HandleEquipmentChanged;
            OnEquipChanged(inventoryCache.equippedMainHandId, inventoryCache.equippedOffHandId);
        }
        else
        {
            OnEquipChanged(-1, -1);
        }
    }

    private void OnDestroy()
    {
        if (inventoryCache != null)
        {
            inventoryCache.OnEquipmentChanged -= HandleEquipmentChanged;
        }
    }

    private void HandleEquipmentChanged()
    {
        if (inventoryCache == null) return;
        OnEquipChanged(inventoryCache.equippedMainHandId, inventoryCache.equippedOffHandId);
    }

    private void LoadMaterials()
    {
        woodMat = Resources.Load<Material>("Materials/Mat_Wood_Carved")
            ?? Resources.Load<Material>("Materials/Wood_Bark")
            ?? Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        steelMat = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        leatherMat = Resources.Load<Material>("Materials/Mat_Leather_Wrap") ?? woodMat;

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        skinMat = new Material(urpLit);
        skinMat.color = new Color(0.76f, 0.58f, 0.46f);
        if (skinMat.HasProperty("_Smoothness")) skinMat.SetFloat("_Smoothness", 0.28f);

        sleeveMat = new Material(urpLit);
        sleeveMat.color = new Color(0.22f, 0.19f, 0.15f);
        if (sleeveMat.HasProperty("_Smoothness")) sleeveMat.SetFloat("_Smoothness", 0.15f);

        boneMat = new Material(urpLit);
        boneMat.color = new Color(0.90f, 0.85f, 0.72f);
        if (boneMat.HasProperty("_Smoothness")) boneMat.SetFloat("_Smoothness", 0.35f);
    }

    private void RemoveLegacyWeaponHolders()
    {
        if (cam == null) return;
        for (int i = cam.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = cam.transform.GetChild(i);
            if (child == viewmodelRoot) continue;
            string n = child.name;
            if (n.Contains("Weapon_Holder") || n.Contains("Procedural_Bow"))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void BuildDualArmRig()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        LoadMaterials();

        GameObject rootObj = new GameObject("Fraga_Viewmodel_Root");
        viewmodelRoot = rootObj.transform;
        viewmodelRoot.SetParent(cam.transform, false);

        // Braco Direito
        GameObject rPivot = new GameObject("RightArmPivot");
        rightArmPivot = rPivot.transform;
        rightArmPivot.SetParent(viewmodelRoot, false);
        rightArmPivot.localPosition = rightRestPos;

        CreateArmLimb(rightArmPivot, isLeft: false);

        GameObject rSock = new GameObject("RightHandSocket");
        rightHandSocket = rSock.transform;
        rightHandSocket.SetParent(rightArmPivot, false);
        rightHandSocket.localPosition = new Vector3(-0.02f, 0.03f, 0.10f);

        // Braco Esquerdo
        GameObject lPivot = new GameObject("LeftArmPivot");
        leftArmPivot = lPivot.transform;
        leftArmPivot.SetParent(viewmodelRoot, false);
        leftArmPivot.localPosition = leftRestPos;

        CreateArmLimb(leftArmPivot, isLeft: true);

        GameObject lSock = new GameObject("LeftHandSocket");
        leftHandSocket = lSock.transform;
        leftHandSocket.SetParent(leftArmPivot, false);
        leftHandSocket.localPosition = new Vector3(0.02f, 0.03f, 0.10f);

        PrebuildToolVisuals();
    }

    private void CreateArmLimb(Transform pivot, bool isLeft)
    {
        float sideSign = isLeft ? -1f : 1f;

        GameObject upperArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(upperArm.GetComponent<Collider>());
        upperArm.name = isLeft ? "Left_UpperArm" : "Right_UpperArm";
        upperArm.transform.SetParent(pivot, false);
        upperArm.transform.localPosition = new Vector3(sideSign * 0.09f, -0.19f, -0.32f);
        upperArm.transform.localRotation = Quaternion.Euler(46f, sideSign * -22f, sideSign * 8f);
        upperArm.transform.localScale = new Vector3(0.086f, 0.14f, 0.086f);
        upperArm.GetComponent<MeshRenderer>().sharedMaterial = sleeveMat;

        GameObject sleeve = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(sleeve.GetComponent<Collider>());
        sleeve.name = isLeft ? "Left_Sleeve" : "Right_Sleeve";
        sleeve.transform.SetParent(pivot, false);
        sleeve.transform.localPosition = new Vector3(sideSign * 0.04f, -0.10f, -0.18f);
        sleeve.transform.localRotation = Quaternion.Euler(78f, sideSign * -12f, 0f);
        sleeve.transform.localScale = new Vector3(0.075f, 0.14f, 0.075f);
        sleeve.GetComponent<MeshRenderer>().sharedMaterial = sleeveMat;

        GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(forearm.GetComponent<Collider>());
        forearm.name = isLeft ? "Left_Forearm" : "Right_Forearm";
        forearm.transform.SetParent(pivot, false);
        forearm.transform.localPosition = new Vector3(sideSign * 0.01f, -0.04f, -0.04f);
        forearm.transform.localRotation = Quaternion.Euler(82f, sideSign * -6f, 0f);
        forearm.transform.localScale = new Vector3(0.056f, 0.10f, 0.056f);
        forearm.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(hand.GetComponent<Collider>());
        hand.name = isLeft ? "Left_Hand" : "Right_Hand";
        hand.transform.SetParent(pivot, false);
        hand.transform.localPosition = new Vector3(0f, 0.0f, 0.07f);
        hand.transform.localScale = new Vector3(0.068f, 0.058f, 0.085f);
        hand.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        GameObject fingers = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(fingers.GetComponent<Collider>());
        fingers.name = isLeft ? "Left_Fingers" : "Right_Fingers";
        fingers.transform.SetParent(hand.transform, false);
        fingers.transform.localPosition = new Vector3(sideSign * -0.15f, -0.12f, 0.42f);
        fingers.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
        fingers.transform.localScale = new Vector3(0.68f, 0.56f, 0.72f);
        fingers.GetComponent<MeshRenderer>().sharedMaterial = skinMat;
    }

    private void PrebuildToolVisuals()
    {
        // 300: Machado Rudimentar
        GameObject axe = ProceduralSurvivalAxeGenerator.BuildAxeGameObject(woodMat, steelMat, leatherMat);
        foreach (Collider c in axe.GetComponentsInChildren<Collider>(true)) Destroy(c);
        axe.transform.SetParent(rightHandSocket, false);
        axe.transform.localPosition = new Vector3(0f, -0.06f, 0.04f);
        axe.transform.localRotation = Quaternion.Euler(8f, -18f, 6f);
        axe.transform.localScale = Vector3.one * 0.65f;
        axe.SetActive(false);
        cachedRightModels[300] = axe;

        // 301: Picareta de Pedra
        GameObject pick = BuildPickaxeVisual();
        pick.transform.SetParent(rightHandSocket, false);
        pick.SetActive(false);
        cachedRightModels[301] = pick;

        // 302: Faca de Caca
        GameObject knife = BuildKnifeVisual();
        knife.transform.SetParent(rightHandSocket, false);
        knife.SetActive(false);
        cachedRightModels[302] = knife;

        // 305: Arco de Sobrevivencia
        GameObject bow = BuildBowVisual();
        bow.transform.SetParent(rightHandSocket, false);
        bow.SetActive(false);
        cachedRightModels[305] = bow;

        // 306: Vara de Pesca
        GameObject rod = BuildFishingRodVisual();
        rod.transform.SetParent(rightHandSocket, false);
        rod.SetActive(false);
        cachedRightModels[306] = rod;

        // 307: Enxada Rustica
        GameObject hoe = BuildHoeVisual();
        hoe.transform.SetParent(rightHandSocket, false);
        hoe.SetActive(false);
        cachedRightModels[307] = hoe;

        // 308: Lanca Celta
        GameObject spear = BuildSpearVisual();
        spear.transform.SetParent(rightHandSocket, false);
        spear.SetActive(false);
        cachedRightModels[308] = spear;

        // 311: Clava de Osso
        GameObject boneClub = BuildBoneClubVisual();
        boneClub.transform.SetParent(rightHandSocket, false);
        boneClub.SetActive(false);
        cachedRightModels[311] = boneClub;

        // 312: Machado de Ferro
        GameObject ironAxe = BuildIronAxeVisual();
        ironAxe.transform.SetParent(rightHandSocket, false);
        ironAxe.SetActive(false);
        cachedRightModels[312] = ironAxe;

        // MAO ESQUERDA
        // 303: Tocha
        GameObject torch = BuildTorchVisual();
        torch.transform.SetParent(leftHandSocket, false);
        torch.SetActive(false);
        cachedLeftModels[303] = torch;

        // 304: Lampiao Reforcado
        GameObject lantern = BuildLanternVisual();
        lantern.transform.SetParent(leftHandSocket, false);
        lantern.SetActive(false);
        cachedLeftModels[304] = lantern;

        // 309: Escudo de Madeira
        GameObject shield = BuildShieldVisual();
        shield.transform.SetParent(leftHandSocket, false);
        shield.SetActive(false);
        cachedLeftModels[309] = shield;
    }

    private GameObject BuildPickaxeVisual()
    {
        GameObject root = new GameObject("VM_Pickaxe");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.14f, 0f), Quaternion.identity, new Vector3(0.028f, 0.24f, 0.028f), woodMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.35f, 0.02f), Quaternion.Euler(10f, 0f, 0f), new Vector3(0.036f, 0.045f, 0.34f), steelMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(0.035f, 0.09f, 0.035f), leatherMat);
        root.transform.localRotation = Quaternion.Euler(12f, -15f, 4f);
        return root;
    }

    private GameObject BuildKnifeVisual()
    {
        GameObject root = new GameObject("VM_Knife");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.024f, 0.065f, 0.024f), leatherMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.105f, 0f), Quaternion.identity, new Vector3(0.042f, 0.012f, 0.022f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.20f, 0.008f), Quaternion.Euler(-4f, 0f, 0f), new Vector3(0.008f, 0.18f, 0.028f), steelMat);
        root.transform.localRotation = Quaternion.Euler(24f, -10f, 8f);
        return root;
    }

    private GameObject BuildBowVisual()
    {
        GameObject root = new GameObject("VM_Bow");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.16f, -0.02f), Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.022f, 0.18f, 0.022f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, -0.16f, -0.02f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.022f, 0.18f, 0.022f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, Vector3.zero, Quaternion.identity, new Vector3(0.028f, 0.07f, 0.028f), leatherMat);

        GameObject strTop = CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.15f, -0.09f), Quaternion.identity, new Vector3(0.004f, 0.16f, 0.004f), leatherMat);
        bowStringTop = strTop.transform;
        GameObject strBot = CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, -0.15f, -0.09f), Quaternion.identity, new Vector3(0.004f, 0.16f, 0.004f), leatherMat);
        bowStringBottom = strBot.transform;

        bowNockedArrow = new GameObject("VM_NockedArrow");
        bowNockedArrow.transform.SetParent(root.transform, false);
        bowNockedArrow.transform.localPosition = new Vector3(0f, 0.02f, 0.08f);
        CreatePart(PrimitiveType.Cylinder, bowNockedArrow.transform, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.011f, 0.24f, 0.011f), woodMat);
        CreatePart(PrimitiveType.Cube, bowNockedArrow.transform, new Vector3(0f, 0f, 0.24f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.024f, 0.008f, 0.04f), steelMat);
        bowNockedArrow.SetActive(false);

        root.transform.localPosition = new Vector3(-0.04f, 0.02f, 0.06f);
        root.transform.localRotation = Quaternion.Euler(8f, -12f, 10f);
        return root;
    }

    private GameObject BuildFishingRodVisual()
    {
        GameObject root = new GameObject("VM_FishingRod");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.026f, 0.08f, 0.026f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.36f, 0.22f), Quaternion.Euler(34f, 0f, 0f), new Vector3(0.014f, 0.42f, 0.014f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.56f, 0.44f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.003f, 0.20f, 0.003f), steelMat);
        root.transform.localRotation = Quaternion.Euler(14f, -8f, 0f);
        return root;
    }

    private GameObject BuildHoeVisual()
    {
        GameObject root = new GameObject("VM_Hoe");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.16f, 0f), Quaternion.identity, new Vector3(0.026f, 0.28f, 0.026f), woodMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.42f, 0.06f), Quaternion.Euler(20f, 0f, 0f), new Vector3(0.11f, 0.018f, 0.13f), steelMat);
        root.transform.localRotation = Quaternion.Euler(12f, -12f, 4f);
        return root;
    }

    private GameObject BuildSpearVisual()
    {
        GameObject root = new GameObject("VM_Spear");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.05f, 0.12f), Quaternion.Euler(78f, 0f, 0f), new Vector3(0.028f, 0.52f, 0.028f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.12f, 0.48f), Quaternion.Euler(78f, 0f, 0f), new Vector3(0.036f, 0.06f, 0.036f), leatherMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.16f, 0.65f), Quaternion.Euler(-12f, 45f, 0f), new Vector3(0.042f, 0.014f, 0.18f), steelMat);
        root.transform.localPosition = new Vector3(0.02f, -0.02f, 0.02f);
        return root;
    }

    private GameObject BuildBoneClubVisual()
    {
        GameObject root = new GameObject("VM_BoneClub");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.032f, 0.10f, 0.032f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.22f, 0f), Quaternion.Euler(4f, 0f, 0f), new Vector3(0.038f, 0.18f, 0.038f), boneMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.37f, 0.01f), Quaternion.identity, new Vector3(0.088f, 0.105f, 0.088f), boneMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0.038f, 0.38f, 0.03f), Quaternion.Euler(15f, 25f, 0f), new Vector3(0.025f, 0.025f, 0.05f), boneMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(-0.035f, 0.35f, 0.03f), Quaternion.Euler(-15f, -25f, 0f), new Vector3(0.025f, 0.025f, 0.05f), boneMat);
        root.transform.localRotation = Quaternion.Euler(14f, -14f, 4f);
        return root;
    }

    private GameObject BuildIronAxeVisual()
    {
        GameObject root = new GameObject("VM_IronAxe");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.15f, 0f), Quaternion.identity, new Vector3(0.028f, 0.24f, 0.028f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(0.034f, 0.08f, 0.034f), leatherMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.34f, 0.05f), Quaternion.Euler(6f, 0f, 0f), new Vector3(0.026f, 0.11f, 0.14f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.31f, 0.125f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.012f, 0.14f, 0.045f), steelMat);
        root.transform.localRotation = Quaternion.Euler(10f, -16f, 5f);
        return root;
    }

    private GameObject BuildTorchVisual()
    {
        GameObject root = new GameObject("VM_Torch");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.12f, 0f), Quaternion.identity, new Vector3(0.028f, 0.18f, 0.028f), woodMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.30f, 0f), Quaternion.identity, new Vector3(0.065f, 0.08f, 0.065f), leatherMat);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material flameMat = new Material(lit);
        flameMat.color = new Color(1f, 0.55f, 0.15f);
        if (flameMat.HasProperty("_EmissionColor"))
        {
            flameMat.EnableKeyword("_EMISSION");
            flameMat.SetColor("_EmissionColor", new Color(2.5f, 1.0f, 0.25f));
        }
        GameObject flame = CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.35f, 0f), Quaternion.identity, new Vector3(0.055f, 0.09f, 0.055f), flameMat);

        GameObject lObj = new GameObject("Torch_PointLight");
        lObj.transform.SetParent(flame.transform, false);
        heldTorchLight = lObj.AddComponent<Light>();
        heldTorchLight.type = LightType.Point;
        heldTorchLight.color = new Color(1f, 0.64f, 0.28f);
        heldTorchLight.range = 14f;
        heldTorchLight.intensity = heldTorchBaseIntensity;
        heldTorchLight.shadows = LightShadows.None;

        root.transform.localRotation = Quaternion.Euler(14f, 8f, -10f);
        return root;
    }

    private GameObject BuildLanternVisual()
    {
        GameObject root = new GameObject("VM_Lantern");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.08f, 0f), Quaternion.identity, new Vector3(0.08f, 0.02f, 0.08f), steelMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.22f, 0f), Quaternion.identity, new Vector3(0.085f, 0.02f, 0.085f), steelMat);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material glowMat = new Material(lit);
        glowMat.color = new Color(1f, 0.85f, 0.52f);
        if (glowMat.HasProperty("_EmissionColor"))
        {
            glowMat.EnableKeyword("_EMISSION");
            glowMat.SetColor("_EmissionColor", new Color(2.2f, 1.65f, 0.75f));
        }
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.15f, 0f), Quaternion.identity, new Vector3(0.06f, 0.06f, 0.06f), glowMat);

        GameObject lObj = new GameObject("Lantern_PointLight");
        lObj.transform.SetParent(root.transform, false);
        lObj.transform.localPosition = new Vector3(0f, 0.15f, 0.05f);
        Light lt = lObj.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = new Color(1f, 0.84f, 0.55f);
        lt.range = 18f;
        lt.intensity = 3.2f;
        lt.shadows = LightShadows.None;

        root.transform.localRotation = Quaternion.Euler(8f, 10f, -6f);
        return root;
    }

    private GameObject BuildShieldVisual()
    {
        GameObject root = new GameObject("VM_Shield");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.06f, 0.04f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.36f, 0.018f, 0.36f), woodMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.06f, 0.065f), Quaternion.identity, new Vector3(0.11f, 0.11f, 0.05f), steelMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.06f, 0.036f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.38f, 0.01f, 0.38f), leatherMat);
        root.transform.localPosition = new Vector3(0.03f, -0.02f, 0.06f);
        root.transform.localRotation = Quaternion.Euler(4f, 24f, 0f);
        return root;
    }

    private static GameObject CreatePart(PrimitiveType type, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        Destroy(obj.GetComponent<Collider>());
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localRotation = rot;
        obj.transform.localScale = scale;
        if (mat != null)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }
        return obj;
    }

    public void OnEquipChanged(int mainHandId, int offHandId)
    {
        currentMainHandId = mainHandId;
        currentOffHandId = offHandId;

        foreach (var kv in cachedRightModels)
        {
            if (kv.Value != null) kv.Value.SetActive(kv.Key == mainHandId);
        }

        foreach (var kv in cachedLeftModels)
        {
            if (kv.Value != null) kv.Value.SetActive(kv.Key == offHandId);
        }

        isBowDrawing = false;
        bowDraw01 = 0f;
        isSpearAiming = false;
        spearCharge01 = 0f;
        if (bowNockedArrow != null) bowNockedArrow.SetActive(false);

        RefreshVisibility();
    }

    public void SetCarryingLogs(bool carrying)
    {
        isCarryingLogs = carrying;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (viewmodelRoot == null) return;
        viewmodelRoot.gameObject.SetActive(!isCarryingLogs);
    }

    public void TriggerPrimaryAttack(int comboIndex, bool isHeavy)
    {
        isAttacking = true;
        attackTimer = 0f;
        attackComboIndex = comboIndex % 2;
        attackIsHeavy = isHeavy;
        attackWeaponId = currentMainHandId;
        attackIsThrust = currentMainHandId == 308 || (currentMainHandId == 302 && attackComboIndex == 1);
        attackDuration = isHeavy ? 0.42f : (currentMainHandId == 302 ? 0.21f : (currentMainHandId == 301 || currentMainHandId == 311 ? 0.35f : 0.30f));
        recoilOffset = isHeavy ? 0.045f : 0.02f;

        if (externalArmsAnimator != null)
        {
            externalArmsAnimator.SetTrigger(isHeavy ? AnimHeavyHash : AnimAttackHash);
        }
    }

    public void TriggerVerticalImpulse(float amount)
    {
        verticalImpulseOffset = Mathf.Clamp(verticalImpulseOffset + amount, -0.07f, 0.07f);
    }

    public void SetGuardPose(bool active)
    {
        isGuarding = active;
        if (externalArmsAnimator != null) externalArmsAnimator.SetBool(AnimGuardHash, active);
    }

    public void TriggerParrySnap()
    {
        parryTimer = 0.22f;
        recoilOffset = 0.04f;
    }

    public void TriggerBlockImpact()
    {
        blockImpactTimer = 0.16f;
        recoilOffset = 0.035f;
    }

    public void SetBowDraw(bool active, float draw01)
    {
        isBowDrawing = active;
        bowDraw01 = Mathf.Clamp01(draw01);
        if (bowNockedArrow != null)
        {
            bowNockedArrow.SetActive(active);
            if (active)
            {
                bowNockedArrow.transform.localPosition = new Vector3(0f, 0.02f, 0.08f - bowDraw01 * 0.14f);
            }
        }
        if (bowStringTop != null && bowStringBottom != null)
        {
            float zPull = -0.09f - bowDraw01 * 0.14f;
            bowStringTop.localPosition = new Vector3(0f, 0.15f, zPull);
            bowStringTop.localRotation = Quaternion.Euler(-bowDraw01 * 24f, 0f, 0f);
            bowStringBottom.localPosition = new Vector3(0f, -0.15f, zPull);
            bowStringBottom.localRotation = Quaternion.Euler(bowDraw01 * 24f, 0f, 0f);
        }
    }

    public void SetSpearAim(bool active, float charge01)
    {
        isSpearAiming = active;
        spearCharge01 = Mathf.Clamp01(charge01);
    }

    public void TriggerConsumeAnimation()
    {
        consumeTimer = 0.45f;
        if (externalArmsAnimator != null) externalArmsAnimator.SetTrigger(AnimConsumeHash);
    }

    private void Update()
    {
        if (viewmodelRoot == null || isCarryingLogs) return;
        if (Time.timeScale <= 0.001f) return;

        float dt = Time.deltaTime;
        float speed = playerCache != null ? playerCache.CurrentSpeed : 0f;
        bool sprinting = playerCache != null && playerCache.isSprinting && speed > 0.3f;
        bool crouching = playerCache != null && playerCache.isCrouching;
        bool moving = speed > 0.35f;

        if (externalArmsAnimator != null)
        {
            externalArmsAnimator.SetFloat(AnimSpeedHash, speed);
            externalArmsAnimator.SetBool(AnimSprintHash, sprinting);
            externalArmsAnimator.SetBool(AnimCrouchHash, crouching);
            externalArmsAnimator.SetFloat(AnimBowDrawHash, isBowDrawing ? bowDraw01 : 0f);
        }

        // 1. Walk/Idle Bobbing + Shiver
        float bobFreq = moving ? (sprinting ? 14f : (crouching ? 6.5f : 9.2f)) : 1.7f;
        float bobAmp = moving ? (sprinting ? 0.036f : (crouching ? 0.012f : 0.020f)) : 0.005f;
        bobTimer += dt * bobFreq;

        Vector3 bobOffset = new Vector3(
            Mathf.Cos(bobTimer * 0.5f) * bobAmp * 0.7f,
            Mathf.Sin(bobTimer) * bobAmp,
            0f);

        if (bodyTempCache != null && bodyTempCache.currentTemperature <= BodyTemperatureSystem.TIER1_SHIVER_TEMP)
        {
            shiverPhase += dt * 42f;
            float intensity = Mathf.Lerp(0.002f, 0.010f, (BodyTemperatureSystem.TIER1_SHIVER_TEMP - bodyTempCache.currentTemperature) / 4f);
            bobOffset.x += Mathf.Sin(shiverPhase) * intensity;
            bobOffset.y += Mathf.Cos(shiverPhase * 1.3f) * intensity * 0.6f;
        }

        // 2. Look Sway
        float mx = (FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsCameraLookAllowed) ? Input.GetAxisRaw("Mouse X") : 0f;
        float my = (FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsCameraLookAllowed) ? Input.GetAxisRaw("Mouse Y") : 0f;
        Vector3 swayOffset = new Vector3(Mathf.Clamp(-mx * 0.006f, -0.025f, 0.025f), Mathf.Clamp(-my * 0.006f, -0.025f, 0.025f), 0f);

        // 3. Torch flicker leve
        if (heldTorchLight != null && currentOffHandId == 303)
        {
            heldTorchLight.intensity = heldTorchBaseIntensity + Mathf.Sin(Time.time * 13f) * 0.25f + Mathf.Cos(Time.time * 23f) * 0.15f;
        }

        recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, dt * 0.22f);
        verticalImpulseOffset = Mathf.Lerp(verticalImpulseOffset, 0f, dt * 10f);

        // 4. Right Arm Pose
        Vector3 targetRPos = rightRestPos + bobOffset + swayOffset - Vector3.forward * recoilOffset - Vector3.up * verticalImpulseOffset;
        Quaternion targetRRot = Quaternion.identity;

        if (isAttacking)
        {
            attackTimer += dt;
            float p = Mathf.Clamp01(attackTimer / attackDuration);
            EvaluateAttackPose(p, out Vector3 aPos, out Quaternion aRot);
            targetRPos += aPos;
            targetRRot = aRot;
            if (p >= 1f) isAttacking = false;
        }
        else if (isBowDrawing)
        {
            targetRPos = new Vector3(0.11f, -0.11f, 0.36f) + swayOffset * 0.4f;
            targetRRot = Quaternion.Euler(0f, -14f, 4f);
        }
        else if (isSpearAiming)
        {
            targetRPos = new Vector3(0.29f, -0.08f, 0.26f - spearCharge01 * 0.09f) + swayOffset * 0.5f;
            targetRRot = Quaternion.Euler(-28f - spearCharge01 * 12f, -4f, 6f);
        }
        else if (isGuarding && currentOffHandId != 309)
        {
            targetRPos = new Vector3(0.12f, -0.16f, 0.36f) + swayOffset * 0.5f;
            targetRRot = Quaternion.Euler(14f, -48f, 38f);
        }
        else if (sprinting && !isBowDrawing && !isSpearAiming)
        {
            // Postura de corrida estilo The Forest (arma cruzada abaixo da linha de visao)
            targetRPos += new Vector3(-0.07f, -0.05f, -0.04f);
            targetRRot = Quaternion.Euler(18f, -34f, 24f);
        }

        if (blockImpactTimer > 0f)
        {
            blockImpactTimer -= dt;
            float bi = Mathf.Sin((blockImpactTimer / 0.16f) * Mathf.PI);
            targetRPos += new Vector3(0.03f * bi, -0.02f * bi, -0.05f * bi);
            targetRRot *= Quaternion.Euler(-10f * bi, 8f * bi, 0f);
        }

        // 5. Left Arm Pose
        guardBlend = Mathf.MoveTowards(guardBlend, isGuarding ? 1f : 0f, dt * 10f);
        Vector3 targetLPos = leftRestPos + new Vector3(-bobOffset.x, bobOffset.y * 0.8f - verticalImpulseOffset, 0f) + swayOffset * 0.8f;
        Quaternion targetLRot = Quaternion.identity;

        if (currentOffHandId == 309) // Escudo
        {
            Vector3 shieldIdle = new Vector3(-0.24f, -0.22f, 0.36f);
            Vector3 shieldBlock = new Vector3(-0.07f, -0.13f, 0.34f);
            targetLPos = Vector3.Lerp(shieldIdle, shieldBlock, guardBlend) + swayOffset * 0.5f;
            targetLRot = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(4f, 18f, -6f), guardBlend);
        }
        else if (isBowDrawing)
        {
            targetLPos = new Vector3(0.06f, -0.10f, 0.33f - bowDraw01 * 0.15f);
            targetLRot = Quaternion.Euler(-6f, 18f, 0f);
        }
        else if (consumeTimer > 0f)
        {
            consumeTimer -= dt;
            float t = Mathf.Sin((consumeTimer / 0.45f) * Mathf.PI);
            targetLPos = Vector3.Lerp(targetLPos, new Vector3(-0.06f, -0.08f, 0.26f), t);
            targetLRot = Quaternion.Euler(-30f * t, 20f * t, 0f);
        }

        if (parryTimer > 0f)
        {
            parryTimer -= dt;
            float p = Mathf.Sin((parryTimer / 0.22f) * Mathf.PI);
            targetLPos += new Vector3(0.06f * p, 0.04f * p, 0.11f * p);
            targetLRot *= Quaternion.Euler(-14f * p, 22f * p, -18f * p);
        }
        else if (blockImpactTimer > 0f)
        {
            float bi = Mathf.Sin((blockImpactTimer / 0.16f) * Mathf.PI);
            targetLPos += new Vector3(-0.03f * bi, -0.02f * bi, -0.07f * bi);
            targetLRot *= Quaternion.Euler(12f * bi, -10f * bi, 8f * bi);
        }

        float posSmooth = isAttacking ? 26f : 14f;
        rightArmPivot.localPosition = Vector3.Lerp(rightArmPivot.localPosition, targetRPos, dt * posSmooth);
        rightArmPivot.localRotation = Quaternion.Slerp(rightArmPivot.localRotation, targetRRot, dt * posSmooth);

        leftArmPivot.localPosition = Vector3.Lerp(leftArmPivot.localPosition, targetLPos, dt * 15f);
        leftArmPivot.localRotation = Quaternion.Slerp(leftArmPivot.localRotation, targetLRot, dt * 15f);
    }

    private void EvaluateAttackPose(float p, out Vector3 posOffset, out Quaternion rotOffset)
    {
        if (attackIsThrust)
        {
            if (p < 0.25f)
            {
                float t = p / 0.25f;
                posOffset = Vector3.Lerp(Vector3.zero, new Vector3(0.04f, -0.03f, -0.12f), t);
                rotOffset = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(8f, 0f, 0f), t);
            }
            else if (p < 0.55f)
            {
                float t = (p - 0.25f) / 0.30f;
                posOffset = Vector3.Lerp(new Vector3(0.04f, -0.03f, -0.12f), new Vector3(-0.08f, 0.04f, 0.24f), t * t);
                rotOffset = Quaternion.Slerp(Quaternion.Euler(8f, 0f, 0f), Quaternion.Euler(-10f, -12f, 0f), t);
            }
            else
            {
                float t = (p - 0.55f) / 0.45f;
                posOffset = Vector3.Lerp(new Vector3(-0.08f, 0.04f, 0.24f), Vector3.zero, t);
                rotOffset = Quaternion.Slerp(Quaternion.Euler(-10f, -12f, 0f), Quaternion.identity, t);
            }
            return;
        }

        // Punho esquerdo/direito alternado quando desarmado
        if (attackWeaponId <= 0)
        {
            float punchArc = Mathf.Sin(p * Mathf.PI);
            posOffset = new Vector3(-0.09f * punchArc, 0.06f * punchArc, 0.24f * punchArc);
            rotOffset = Quaternion.Euler(-16f * punchArc, 18f * punchArc, -10f * punchArc);
            return;
        }

        float dir = attackComboIndex == 0 ? 1f : -1f;
        float heavyScale = attackIsHeavy ? 1.35f : 1f;

        Vector3 windPos;
        Quaternion windRot;
        Vector3 strikePos;
        Quaternion strikeRot;

        if (attackWeaponId == 301 || attackWeaponId == 307 || attackWeaponId == 311)
        {
            // Picareta / Enxada / Clava: golpe vertical de cima para baixo
            windPos = new Vector3(0.03f * dir, 0.20f * heavyScale, -0.11f);
            windRot = Quaternion.Euler(-48f * heavyScale, 8f * dir, 6f * dir);
            strikePos = new Vector3(-0.06f * dir, -0.19f, 0.20f);
            strikeRot = Quaternion.Euler(68f * heavyScale, -12f * dir, -8f * dir);
        }
        else if (attackWeaponId == 302)
        {
            // Faca de caca: corte horizontal rapido
            windPos = new Vector3(0.10f * dir, 0.04f, -0.04f);
            windRot = Quaternion.Euler(-10f, 34f * dir, 32f * dir);
            strikePos = new Vector3(-0.20f * dir, -0.05f, 0.18f);
            strikeRot = Quaternion.Euler(16f, -52f * dir, -44f * dir);
        }
        else
        {
            // Machado de pedra / ferro: corte lenhador diagonal amplo
            windPos = new Vector3(0.12f * dir * heavyScale, 0.13f * heavyScale, -0.10f);
            windRot = Quaternion.Euler(-26f * heavyScale, 28f * dir, 22f * dir);
            strikePos = new Vector3(-0.24f * dir * heavyScale, -0.15f, 0.19f);
            strikeRot = Quaternion.Euler(50f * heavyScale, -50f * dir, -32f * dir);
        }

        if (p < 0.28f)
        {
            float t = p / 0.28f;
            posOffset = Vector3.Lerp(Vector3.zero, windPos, t);
            rotOffset = Quaternion.Slerp(Quaternion.identity, windRot, t);
        }
        else if (p < 0.62f)
        {
            float t = (p - 0.28f) / 0.34f;
            posOffset = Vector3.Lerp(windPos, strikePos, t * t);
            rotOffset = Quaternion.Slerp(windRot, strikeRot, t * t);
        }
        else
        {
            float t = (p - 0.62f) / 0.38f;
            posOffset = Vector3.Lerp(strikePos, Vector3.zero, t);
            rotOffset = Quaternion.Slerp(strikeRot, Quaternion.identity, t);
        }
    }
}
