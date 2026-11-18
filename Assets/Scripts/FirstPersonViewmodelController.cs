using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// FirstPersonViewmodelController: Rig procedural dual-arm em primeira pessoa (Padrao AAA) com:
/// - Respiracao idle, calafrios de hipotermia, caminhada/corrida/agachado, nado e inercia amortecida de camera
/// - Transicao de saque de armas (Equip Draw), Inspecao de Arma [T], Coleta manual e Escalada (Mantle)
/// - Combo alternado (Slash 1 / Slash 2), Golpe Pesado, Estocada de Lanca e Impacto de Aterrissagem
/// - Consumiveis com props 3D na mao esquerda (Comida, Cantil, Bandagem, Tala, Pergaminho) + Isqueiro de Latao
/// - Dedos articulados dinamicos (empunhadura fechada vs palma aberta) e 100% Zero-GC em Update.
/// </summary>
public class FirstPersonViewmodelController : MonoBehaviour
{
    public static FirstPersonViewmodelController Instance { get; private set; }

    public enum ConsumeAnimKind
    {
        Generic = 0,
        Eat = 1,
        Drink = 2,
        Bandage = 3,
        Splint = 4,
        ReadNote = 5
    }

    [Header("Optional External Animator Rig (Mixamo / Asset Store)")]
    public Animator externalArmsAnimator;

    private Camera cam;
    private Transform viewmodelRoot;
    private Transform rightArmPivot;
    private Transform leftArmPivot;
    private Transform rightHandSocket;
    private Transform leftHandSocket;
    private Transform rightFingerGroup;
    private Transform leftFingerGroup;

    private readonly Dictionary<int, GameObject> cachedRightModels = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> cachedLeftModels = new Dictionary<int, GameObject>();
    private readonly Dictionary<ConsumeAnimKind, GameObject> consumeProps = new Dictionary<ConsumeAnimKind, GameObject>();

    private GameObject bowNockedArrow;
    private Transform bowStringTop;
    private Transform bowStringBottom;
    private Light heldTorchLight;
    private float heldTorchBaseIntensity = 2.6f;

    private GameObject leftLighterVisual;
    private GameObject activeConsumeProp;

    private int currentMainHandId = -1;
    private int currentOffHandId = -1;
    private bool hasInitializedEquip = false;
    private bool isCarryingLogs = false;

    // Estados de animacao
    private float bobTimer = 0f;
    private float swimPhase = 0f;
    private float shiverPhase = 0f;
    private Vector3 smoothedSway = Vector3.zero;

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

    private bool isSlingWinding = false;
    private float slingWind01 = 0f;
    private float slingSpinPhase = 0f;

    private float consumeTimer = 0f;
    private float consumeDuration = 0.55f;
    private ConsumeAnimKind activeConsumeKind = ConsumeAnimKind.Generic;

    private float equipDrawTimer = 0f;
    private float equipDrawDuration = 0.26f;

    private float inspectTimer = 0f;
    private float inspectDuration = 1.35f;

    private float gatherTimer = 0f;
    private float gatherDuration = 0.34f;

    private float climbTimer = 0f;
    private float climbDuration = 0.36f;

    private float blockImpactTimer = 0f;
    private float recoilOffset = 0f;
    private float verticalImpulseOffset = 0f;
    private float impactBounceTimer = 0f;
    private float impactBounceStrength = 0f;
    private bool isHoldInteracting = false;
    private float interactionHoldProgress = 0f;

    public float RecoilOffset => recoilOffset;
    public float ImpactBounceRemaining => impactBounceTimer;
    public bool IsHoldInteracting => isHoldInteracting;
    public float InteractionHoldProgress => interactionHoldProgress;
    public bool IsEquipDrawing => equipDrawTimer > 0f;
    public bool IsInspecting => inspectTimer > 0f;
    public bool IsGathering => gatherTimer > 0f;
    public bool IsClimbing => climbTimer > 0f;
    public bool IsConsuming => consumeTimer > 0f;
    public bool IsSwimmingPose => playerCache != null && playerCache.isSwimming;
    public ConsumeAnimKind ActiveConsumeKind => activeConsumeKind;
    public GameObject ActiveConsumeProp => activeConsumeProp;
    public bool IsLighterVisualVisible => leftLighterVisual != null && leftLighterVisual.activeSelf;

    private static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
    private static readonly int AnimSprintHash = Animator.StringToHash("IsSprinting");
    private static readonly int AnimCrouchHash = Animator.StringToHash("IsCrouching");
    private static readonly int AnimGuardHash = Animator.StringToHash("IsGuarding");
    private static readonly int AnimAttackHash = Animator.StringToHash("Attack");
    private static readonly int AnimHeavyHash = Animator.StringToHash("AttackHeavy");
    private static readonly int AnimConsumeHash = Animator.StringToHash("Consume");
    private static readonly int AnimBowDrawHash = Animator.StringToHash("BowDraw");
    private static readonly int AnimEquipHash = Animator.StringToHash("Equip");
    private static readonly int AnimGatherHash = Animator.StringToHash("Gather");
    private static readonly int AnimInspectHash = Animator.StringToHash("Inspect");
    private static readonly int AnimClimbHash = Animator.StringToHash("Climb");
    private static readonly int AnimSwimmingHash = Animator.StringToHash("IsSwimming");

    private readonly Vector3 rightRestPos = new Vector3(0.27f, -0.25f, 0.42f);
    private readonly Vector3 leftRestPos = new Vector3(-0.29f, -0.27f, 0.38f);

    private Material skinMat;
    private Material sleeveMat;
    private Material woodMat;
    private Material steelMat;
    private Material leatherMat;
    private Material boneMat;
    private Material brassMat;
    private Material clothMat;

    private InventoryAdvanced inventoryCache;
    private PlayerController playerCache;
    private BodyTemperatureSystem bodyTempCache;

    private static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { SafeDestroy(this); return; }
    }

    private void Start()
    {
        cam = Camera.main ?? FindAnyObjectByType<Camera>();
        playerCache = PlayerController.Instance ?? GetComponent<PlayerController>();
        bodyTempCache = BodyTemperatureSystem.Instance ?? GetComponent<BodyTemperatureSystem>();
        inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();

        BuildDualArmRig();
        RemoveLegacyWeaponHolders();

        if (inventoryCache != null)
        {
            inventoryCache.OnEquipmentChanged -= HandleEquipmentChanged;
            inventoryCache.OnEquipmentChanged += HandleEquipmentChanged;
            OnEquipChanged(inventoryCache.equippedMainHandId, inventoryCache.equippedOffHandId, suppressDrawFoley: true);
        }
        else
        {
            OnEquipChanged(-1, -1, suppressDrawFoley: true);
        }
    }

    private void OnDestroy()
    {
        if (inventoryCache != null)
        {
            inventoryCache.OnEquipmentChanged -= HandleEquipmentChanged;
        }
        if (Instance == this) Instance = null;
    }

    private void HandleEquipmentChanged()
    {
        if (inventoryCache == null) return;
        OnEquipChanged(inventoryCache.equippedMainHandId, inventoryCache.equippedOffHandId, suppressDrawFoley: false);
    }

    private void LoadMaterials()
    {
        woodMat = Resources.Load<Material>("Materials/Mat_Wood_Carved")
            ?? Resources.Load<Material>("Materials/Wood_Bark")
            ?? Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        steelMat = Resources.Load<Material>("Materials/Mat_Ruins_Iron");
        leatherMat = Resources.Load<Material>("Materials/Mat_Leather_Wrap") ?? woodMat;

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        if (woodMat == null)
        {
            woodMat = new Material(urpLit) { color = new Color(0.36f, 0.22f, 0.12f) };
        }
        if (steelMat == null)
        {
            steelMat = new Material(urpLit) { color = new Color(0.56f, 0.59f, 0.62f) };
            if (steelMat.HasProperty("_Metallic")) steelMat.SetFloat("_Metallic", 0.78f);
            if (steelMat.HasProperty("_Smoothness")) steelMat.SetFloat("_Smoothness", 0.55f);
        }
        if (leatherMat == null)
        {
            leatherMat = new Material(urpLit) { color = new Color(0.26f, 0.16f, 0.10f) };
        }

        skinMat = new Material(urpLit) { color = new Color(0.76f, 0.58f, 0.46f) };
        if (skinMat.HasProperty("_Smoothness")) skinMat.SetFloat("_Smoothness", 0.28f);

        sleeveMat = new Material(urpLit) { color = new Color(0.22f, 0.19f, 0.15f) };
        if (sleeveMat.HasProperty("_Smoothness")) sleeveMat.SetFloat("_Smoothness", 0.15f);

        boneMat = new Material(urpLit) { color = new Color(0.90f, 0.85f, 0.72f) };
        if (boneMat.HasProperty("_Smoothness")) boneMat.SetFloat("_Smoothness", 0.35f);

        brassMat = new Material(urpLit) { color = new Color(0.78f, 0.60f, 0.22f) };
        if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.85f);
        if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.65f);

        clothMat = new Material(urpLit) { color = new Color(0.86f, 0.83f, 0.75f) };
        if (clothMat.HasProperty("_Smoothness")) clothMat.SetFloat("_Smoothness", 0.12f);
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

    public void BuildDualArmRig()
    {
        if (cam == null) cam = Camera.main ?? FindAnyObjectByType<Camera>();
        if (cam == null) return;

        if (viewmodelRoot != null)
        {
            SafeDestroy(viewmodelRoot.gameObject);
            viewmodelRoot = null;
        }
        cachedRightModels.Clear();
        cachedLeftModels.Clear();
        consumeProps.Clear();
        leftLighterVisual = null;
        activeConsumeProp = null;

        LoadMaterials();

        GameObject rootObj = new GameObject("Fraga_Viewmodel_Root");
        viewmodelRoot = rootObj.transform;
        viewmodelRoot.SetParent(cam.transform, false);

        // Braco Direito
        GameObject rPivot = new GameObject("RightArmPivot");
        rightArmPivot = rPivot.transform;
        rightArmPivot.SetParent(viewmodelRoot, false);
        rightArmPivot.localPosition = rightRestPos;

        rightFingerGroup = CreateArmLimb(rightArmPivot, isLeft: false);

        GameObject rSock = new GameObject("RightHandSocket");
        rightHandSocket = rSock.transform;
        rightHandSocket.SetParent(rightArmPivot, false);
        rightHandSocket.localPosition = new Vector3(-0.02f, 0.03f, 0.10f);

        // Braco Esquerdo
        GameObject lPivot = new GameObject("LeftArmPivot");
        leftArmPivot = lPivot.transform;
        leftArmPivot.SetParent(viewmodelRoot, false);
        leftArmPivot.localPosition = leftRestPos;

        leftFingerGroup = CreateArmLimb(leftArmPivot, isLeft: true);

        GameObject lSock = new GameObject("LeftHandSocket");
        leftHandSocket = lSock.transform;
        leftHandSocket.SetParent(leftArmPivot, false);
        leftHandSocket.localPosition = new Vector3(0.02f, 0.03f, 0.10f);

        PrebuildToolVisuals();
        PrebuildConsumeProps();
    }

    private Transform CreateArmLimb(Transform pivot, bool isLeft)
    {
        float sideSign = isLeft ? -1f : 1f;

        GameObject upperArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        SafeDestroy(upperArm.GetComponent<Collider>());
        upperArm.name = isLeft ? "Left_UpperArm" : "Right_UpperArm";
        upperArm.transform.SetParent(pivot, false);
        upperArm.transform.localPosition = new Vector3(sideSign * 0.09f, -0.19f, -0.32f);
        upperArm.transform.localRotation = Quaternion.Euler(46f, sideSign * -22f, sideSign * 8f);
        upperArm.transform.localScale = new Vector3(0.086f, 0.14f, 0.086f);
        upperArm.GetComponent<MeshRenderer>().sharedMaterial = sleeveMat;

        GameObject sleeve = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        SafeDestroy(sleeve.GetComponent<Collider>());
        sleeve.name = isLeft ? "Left_Sleeve" : "Right_Sleeve";
        sleeve.transform.SetParent(pivot, false);
        sleeve.transform.localPosition = new Vector3(sideSign * 0.04f, -0.10f, -0.18f);
        sleeve.transform.localRotation = Quaternion.Euler(78f, sideSign * -12f, 0f);
        sleeve.transform.localScale = new Vector3(0.075f, 0.14f, 0.075f);
        sleeve.GetComponent<MeshRenderer>().sharedMaterial = sleeveMat;

        GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        SafeDestroy(forearm.GetComponent<Collider>());
        forearm.name = isLeft ? "Left_Forearm" : "Right_Forearm";
        forearm.transform.SetParent(pivot, false);
        forearm.transform.localPosition = new Vector3(sideSign * 0.01f, -0.04f, -0.04f);
        forearm.transform.localRotation = Quaternion.Euler(82f, sideSign * -6f, 0f);
        forearm.transform.localScale = new Vector3(0.056f, 0.10f, 0.056f);
        forearm.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        SafeDestroy(hand.GetComponent<Collider>());
        hand.name = isLeft ? "Left_Hand" : "Right_Hand";
        hand.transform.SetParent(pivot, false);
        hand.transform.localPosition = new Vector3(0f, 0.0f, 0.07f);
        hand.transform.localScale = new Vector3(0.068f, 0.058f, 0.085f);
        hand.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        // Bracelete de couro com fivela metalica no antebraco
        GameObject bracer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        SafeDestroy(bracer.GetComponent<Collider>());
        bracer.name = isLeft ? "Left_Bracer" : "Right_Bracer";
        bracer.transform.SetParent(pivot, false);
        bracer.transform.localPosition = new Vector3(sideSign * 0.015f, -0.055f, -0.065f);
        bracer.transform.localRotation = Quaternion.Euler(82f, sideSign * -6f, 0f);
        bracer.transform.localScale = new Vector3(0.118f, 0.062f, 0.118f);
        bracer.GetComponent<MeshRenderer>().sharedMaterial = leatherMat;

        GameObject buckle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SafeDestroy(buckle.GetComponent<Collider>());
        buckle.name = isLeft ? "Left_BracerBuckle" : "Right_BracerBuckle";
        buckle.transform.SetParent(bracer.transform, false);
        buckle.transform.localPosition = new Vector3(0f, 0f, 0.52f);
        buckle.transform.localScale = new Vector3(0.28f, 0.36f, 0.14f);
        buckle.GetComponent<MeshRenderer>().sharedMaterial = steelMat;

        // Dedos articulados em capsula + polegar opositor
        GameObject fingers = new GameObject(isLeft ? "Left_Fingers" : "Right_Fingers");
        fingers.transform.SetParent(hand.transform, false);
        fingers.transform.localPosition = new Vector3(sideSign * -0.15f, -0.12f, 0.42f);
        fingers.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);

        for (int f = 0; f < 4; f++)
        {
            float t = (f - 1.5f) * 0.22f;
            float lenScale = (f == 1 || f == 2) ? 0.38f : 0.33f;
            GameObject digit = CreatePart(PrimitiveType.Capsule, fingers.transform,
                new Vector3(t, -Mathf.Abs(t) * 0.12f, 0.08f),
                Quaternion.Euler(68f, 0f, t * -14f),
                new Vector3(0.20f, lenScale, 0.20f), skinMat);
            digit.name = $"Finger_{f}";
        }

        GameObject thumb = CreatePart(PrimitiveType.Capsule, hand.transform,
            new Vector3(sideSign * -0.46f, 0.10f, 0.24f),
            Quaternion.Euler(52f, sideSign * 34f, sideSign * -20f),
            new Vector3(0.23f, 0.34f, 0.23f), skinMat);
        thumb.name = isLeft ? "Left_Thumb" : "Right_Thumb";

        return fingers.transform;
    }

    private static GameObject TryLoadPrefabVisual(string resourcePath, Transform parent, string rootName,
        Vector3 localPos, Vector3 localEuler, Vector3 localScale)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null) return null;

        GameObject root = new GameObject(rootName);
        if (parent != null) root.transform.SetParent(parent, false);

        GameObject instance = Instantiate(prefab, root.transform, false);
        instance.name = prefab.name + "_Mesh";
        instance.transform.localPosition = localPos;
        instance.transform.localRotation = Quaternion.Euler(localEuler);
        instance.transform.localScale = localScale;

        foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) SafeDestroy(c);
        foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>(true)) SafeDestroy(rb);
        foreach (MonoBehaviour mb in instance.GetComponentsInChildren<MonoBehaviour>(true)) SafeDestroy(mb);

        return root;
    }

    private void PrebuildToolVisuals()
    {
        // 300: Machado Rudimentar
        GameObject axe = ProceduralSurvivalAxeGenerator.BuildAxeGameObject(woodMat, steelMat, leatherMat);
        foreach (Collider c in axe.GetComponentsInChildren<Collider>(true)) SafeDestroy(c);
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

        // 314: Funda Celta
        GameObject sling = BuildSlingVisual();
        sling.transform.SetParent(rightHandSocket, false);
        sling.SetActive(false);
        cachedRightModels[314] = sling;

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

        // Isqueiro de Latao na mao esquerda (quando Lampiao/Isqueiro [L] ligado sem offhand equipado)
        leftLighterVisual = BuildLighterVisual();
        leftLighterVisual.transform.SetParent(leftHandSocket, false);
        leftLighterVisual.SetActive(false);
    }

    private void PrebuildConsumeProps()
    {
        // Alimento / Racao / Frutos
        GameObject foodProp = new GameObject("VM_Prop_Food");
        foodProp.transform.SetParent(leftHandSocket, false);
        CreatePart(PrimitiveType.Sphere, foodProp.transform, Vector3.zero, Quaternion.identity, new Vector3(0.068f, 0.048f, 0.052f), leatherMat);
        CreatePart(PrimitiveType.Sphere, foodProp.transform, new Vector3(0.015f, 0.012f, 0.015f), Quaternion.identity, new Vector3(0.042f, 0.036f, 0.042f), boneMat);
        foodProp.SetActive(false);
        consumeProps[ConsumeAnimKind.Eat] = foodProp;
        consumeProps[ConsumeAnimKind.Generic] = foodProp;

        // Cantil / Garrafa de Agua
        GameObject drinkProp = new GameObject("VM_Prop_Canteen");
        drinkProp.transform.SetParent(leftHandSocket, false);
        CreatePart(PrimitiveType.Cylinder, drinkProp.transform, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(0.072f, 0.055f, 0.048f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, drinkProp.transform, new Vector3(0f, 0.088f, 0f), Quaternion.identity, new Vector3(0.024f, 0.022f, 0.024f), brassMat);
        CreatePart(PrimitiveType.Cylinder, drinkProp.transform, new Vector3(0f, 0.112f, 0f), Quaternion.identity, new Vector3(0.020f, 0.014f, 0.020f), woodMat);
        drinkProp.SetActive(false);
        consumeProps[ConsumeAnimKind.Drink] = drinkProp;

        // Bandagem (Prefab externo prioritario ou rolo procedural de linho)
        GameObject bandageProp = TryLoadPrefabVisual("Prefabs/Bandage_Prefab", leftHandSocket, "VM_Prop_Bandage",
            new Vector3(0f, 0.02f, 0.02f), new Vector3(14f, 0f, 0f), Vector3.one * 0.55f);
        if (bandageProp == null)
        {
            bandageProp = new GameObject("VM_Prop_Bandage");
            bandageProp.transform.SetParent(leftHandSocket, false);
            CreatePart(PrimitiveType.Cylinder, bandageProp.transform, new Vector3(0f, 0.015f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.052f, 0.038f, 0.052f), clothMat);
            CreatePart(PrimitiveType.Cube, bandageProp.transform, new Vector3(0f, 0.042f, 0.015f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.068f, 0.008f, 0.055f), clothMat);
        }
        bandageProp.SetActive(false);
        consumeProps[ConsumeAnimKind.Bandage] = bandageProp;

        // Tala Ortopedica
        GameObject splintProp = new GameObject("VM_Prop_Splint");
        splintProp.transform.SetParent(leftHandSocket, false);
        CreatePart(PrimitiveType.Cube, splintProp.transform, new Vector3(-0.016f, 0.03f, 0f), Quaternion.identity, new Vector3(0.018f, 0.14f, 0.012f), woodMat);
        CreatePart(PrimitiveType.Cube, splintProp.transform, new Vector3(0.016f, 0.03f, 0f), Quaternion.identity, new Vector3(0.018f, 0.14f, 0.012f), woodMat);
        CreatePart(PrimitiveType.Cylinder, splintProp.transform, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.042f, 0.022f, 0.024f), clothMat);
        splintProp.SetActive(false);
        consumeProps[ConsumeAnimKind.Splint] = splintProp;

        // Pergaminho / Pagina
        GameObject noteProp = new GameObject("VM_Prop_Note");
        noteProp.transform.SetParent(leftHandSocket, false);
        CreatePart(PrimitiveType.Cube, noteProp.transform, new Vector3(0f, 0.03f, 0.02f), Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.11f, 0.14f, 0.006f), boneMat);
        noteProp.SetActive(false);
        consumeProps[ConsumeAnimKind.ReadNote] = noteProp;
    }

    private GameObject BuildLighterVisual()
    {
        GameObject root = new GameObject("VM_Lighter");
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.022f, 0f), Quaternion.identity, new Vector3(0.032f, 0.046f, 0.018f), brassMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.052f, -0.018f), Quaternion.Euler(-70f, 0f, 0f), new Vector3(0.032f, 0.020f, 0.018f), brassMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.052f, 0.002f), Quaternion.identity, new Vector3(0.010f, 0.014f, 0.010f), steelMat);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material flameMat = Resources.Load<Material>("Materials/Mat_Emissive_Campfire");
        if (flameMat == null && lit != null)
        {
            flameMat = new Material(lit) { color = new Color(1f, 0.65f, 0.22f) };
            if (flameMat.HasProperty("_EmissionColor"))
            {
                flameMat.EnableKeyword("_EMISSION");
                flameMat.SetColor("_EmissionColor", new Color(2.6f, 1.15f, 0.25f));
            }
        }
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.072f, 0.002f), Quaternion.identity, new Vector3(0.018f, 0.034f, 0.018f), flameMat);
        root.transform.localRotation = Quaternion.Euler(10f, 12f, -8f);
        return root;
    }

    private GameObject BuildPickaxeVisual()
    {
        GameObject prefabRoot = TryLoadPrefabVisual("Prefabs/Pickaxe_Prefab", null, "VM_Pickaxe",
            new Vector3(0f, -0.02f, 0.02f), new Vector3(12f, -15f, 4f), Vector3.one * 0.55f);
        if (prefabRoot != null) return prefabRoot;

        GameObject root = new GameObject("VM_Pickaxe");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.14f, 0f), Quaternion.identity, new Vector3(0.028f, 0.24f, 0.028f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(0.035f, 0.09f, 0.035f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.35f, 0f), Quaternion.identity, new Vector3(0.046f, 0.038f, 0.046f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.35f, 0.09f), Quaternion.Euler(14f, 0f, 0f), new Vector3(0.032f, 0.036f, 0.18f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.35f, -0.09f), Quaternion.Euler(-14f, 0f, 0f), new Vector3(0.028f, 0.032f, 0.16f), steelMat);
        root.transform.localRotation = Quaternion.Euler(12f, -15f, 4f);
        return root;
    }

    private GameObject BuildKnifeVisual()
    {
        GameObject root = new GameObject("VM_Knife");
        // Pomo de osso + pino de latao
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, -0.026f, 0f), Quaternion.identity, new Vector3(0.032f, 0.028f, 0.032f), boneMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, -0.040f, 0f), Quaternion.identity, new Vector3(0.012f, 0.008f, 0.012f), brassMat);
        // Empunhadura envolta em couro + rebites
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.038f, 0f), Quaternion.identity, new Vector3(0.025f, 0.065f, 0.028f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.092f, 0f), Quaternion.identity, new Vector3(0.030f, 0.010f, 0.034f), brassMat);
        // Guarda cruzada forjada
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.106f, 0f), Quaternion.identity, new Vector3(0.056f, 0.014f, 0.028f), steelMat);
        // Lamina com chanfro duplo e goteira
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.195f, 0.006f), Quaternion.Euler(-3f, 0f, 0f), new Vector3(0.009f, 0.17f, 0.030f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.195f, -0.005f), Quaternion.Euler(-3f, 0f, 0f), new Vector3(0.011f, 0.15f, 0.008f), brassMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.295f, 0.014f), Quaternion.Euler(-16f, 0f, 0f), new Vector3(0.006f, 0.068f, 0.018f), steelMat);
        root.transform.localRotation = Quaternion.Euler(24f, -10f, 8f);
        return root;
    }

    private GameObject BuildBowVisual()
    {
        GameObject root = new GameObject("VM_Bow");
        GameObject bowPrefab = Resources.Load<GameObject>("Prefabs/Bow_Prefab");
        if (bowPrefab != null)
        {
            GameObject bowMesh = Instantiate(bowPrefab, root.transform, false);
            bowMesh.name = "Bow_Prefab_Mesh";
            bowMesh.transform.localPosition = Vector3.zero;
            bowMesh.transform.localRotation = Quaternion.identity;
            bowMesh.transform.localScale = Vector3.one * 0.72f;
            foreach (Collider c in bowMesh.GetComponentsInChildren<Collider>(true)) SafeDestroy(c);
            foreach (Rigidbody rb in bowMesh.GetComponentsInChildren<Rigidbody>(true)) SafeDestroy(rb);
            foreach (MonoBehaviour mb in bowMesh.GetComponentsInChildren<MonoBehaviour>(true)) SafeDestroy(mb);
        }
        else
        {
            CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.16f, -0.02f), Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.022f, 0.18f, 0.022f), woodMat);
            CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, -0.16f, -0.02f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.022f, 0.18f, 0.022f), woodMat);
            CreatePart(PrimitiveType.Cylinder, root.transform, Vector3.zero, Quaternion.identity, new Vector3(0.028f, 0.07f, 0.028f), leatherMat);
        }

        GameObject strTop = CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.15f, -0.09f), Quaternion.identity, new Vector3(0.004f, 0.16f, 0.004f), leatherMat);
        bowStringTop = strTop.transform;
        GameObject strBot = CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, -0.15f, -0.09f), Quaternion.identity, new Vector3(0.004f, 0.16f, 0.004f), leatherMat);
        bowStringBottom = strBot.transform;

        bowNockedArrow = new GameObject("VM_NockedArrow");
        bowNockedArrow.transform.SetParent(root.transform, false);
        bowNockedArrow.transform.localPosition = new Vector3(0f, 0.02f, 0.08f);
        GameObject arrowPrefab = Resources.Load<GameObject>("Prefabs/Arrow_Prefab");
        if (arrowPrefab != null)
        {
            GameObject arrowMesh = Instantiate(arrowPrefab, bowNockedArrow.transform, false);
            arrowMesh.name = "Arrow_Prefab_Mesh";
            arrowMesh.transform.localScale = Vector3.one * 0.75f;
            foreach (Collider c in arrowMesh.GetComponentsInChildren<Collider>(true)) SafeDestroy(c);
            foreach (Rigidbody rb in arrowMesh.GetComponentsInChildren<Rigidbody>(true)) SafeDestroy(rb);
            foreach (MonoBehaviour mb in arrowMesh.GetComponentsInChildren<MonoBehaviour>(true)) SafeDestroy(mb);
        }
        else
        {
            CreatePart(PrimitiveType.Cylinder, bowNockedArrow.transform, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.011f, 0.24f, 0.011f), woodMat);
            CreatePart(PrimitiveType.Cube, bowNockedArrow.transform, new Vector3(0f, 0f, 0.24f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.024f, 0.008f, 0.04f), steelMat);
            CreatePart(PrimitiveType.Cube, bowNockedArrow.transform, new Vector3(0f, 0f, -0.22f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.024f, 0.024f, 0.055f), boneMat);
        }
        bowNockedArrow.SetActive(false);

        root.transform.localPosition = new Vector3(-0.04f, 0.02f, 0.06f);
        root.transform.localRotation = Quaternion.Euler(8f, -12f, 10f);
        return root;
    }

    private GameObject BuildFishingRodVisual()
    {
        GameObject prefabRoot = TryLoadPrefabVisual("Prefabs/FishingRod_Prefab", null, "VM_FishingRod",
            Vector3.zero, new Vector3(14f, -8f, 0f), Vector3.one * 0.65f);
        if (prefabRoot != null) return prefabRoot;

        GameObject root = new GameObject("VM_FishingRod");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.026f, 0.08f, 0.026f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0.024f, 0.11f, 0.02f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.024f, 0.018f, 0.024f), steelMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.36f, 0.22f), Quaternion.Euler(34f, 0f, 0f), new Vector3(0.014f, 0.42f, 0.014f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.56f, 0.44f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.003f, 0.20f, 0.003f), steelMat);
        root.transform.localRotation = Quaternion.Euler(14f, -8f, 0f);
        return root;
    }

    private GameObject BuildHoeVisual()
    {
        GameObject prefabRoot = TryLoadPrefabVisual("Prefabs/Hoe_Prefab", null, "VM_Hoe",
            Vector3.zero, new Vector3(12f, -12f, 4f), Vector3.one * 0.60f);
        if (prefabRoot != null) return prefabRoot;

        GameObject root = new GameObject("VM_Hoe");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.16f, 0f), Quaternion.identity, new Vector3(0.026f, 0.28f, 0.026f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(0.032f, 0.07f, 0.032f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.41f, 0f), Quaternion.identity, new Vector3(0.038f, 0.026f, 0.038f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.42f, 0.06f), Quaternion.Euler(20f, 0f, 0f), new Vector3(0.11f, 0.018f, 0.13f), steelMat);
        root.transform.localRotation = Quaternion.Euler(12f, -12f, 4f);
        return root;
    }

    private GameObject BuildSpearVisual()
    {
        GameObject root = new GameObject("VM_Spear");
        // Haste longa de freixo + anel de couro na empunhadura
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.05f, 0.12f), Quaternion.Euler(78f, 0f, 0f), new Vector3(0.028f, 0.54f, 0.028f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.02f, -0.02f), Quaternion.Euler(78f, 0f, 0f), new Vector3(0.033f, 0.11f, 0.033f), leatherMat);
        // Colar de amarracao + soquete de ferro + folha celta com nervura central
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.12f, 0.48f), Quaternion.Euler(78f, 0f, 0f), new Vector3(0.036f, 0.06f, 0.036f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.14f, 0.55f), Quaternion.Euler(78f, 0f, 0f), new Vector3(0.038f, 0.040f, 0.038f), steelMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.138f, 0.53f), Quaternion.Euler(78f, 0f, 0f), new Vector3(0.042f, 0.012f, 0.042f), brassMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.16f, 0.67f), Quaternion.Euler(-12f, 45f, 0f), new Vector3(0.048f, 0.014f, 0.22f), steelMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.16f, 0.66f), Quaternion.Euler(78f, 0f, 0f), new Vector3(0.014f, 0.10f, 0.014f), steelMat);
        root.transform.localPosition = new Vector3(0.02f, -0.02f, 0.02f);
        return root;
    }

    private GameObject BuildBoneClubVisual()
    {
        GameObject root = new GameObject("VM_BoneClub");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.034f, 0.10f, 0.034f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.22f, 0f), Quaternion.Euler(4f, 0f, 0f), new Vector3(0.038f, 0.18f, 0.038f), boneMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.30f, 0.005f), Quaternion.Euler(4f, 0f, 0f), new Vector3(0.046f, 0.022f, 0.046f), steelMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.37f, 0.01f), Quaternion.identity, new Vector3(0.092f, 0.110f, 0.092f), boneMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0.040f, 0.38f, 0.03f), Quaternion.Euler(15f, 25f, 0f), new Vector3(0.026f, 0.026f, 0.055f), boneMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(-0.038f, 0.35f, 0.03f), Quaternion.Euler(-15f, -25f, 0f), new Vector3(0.026f, 0.026f, 0.055f), boneMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.40f, 0.052f), Quaternion.Euler(22f, 0f, 45f), new Vector3(0.024f, 0.024f, 0.060f), steelMat);
        root.transform.localRotation = Quaternion.Euler(14f, -14f, 4f);
        return root;
    }

    private GameObject BuildIronAxeVisual()
    {
        GameObject root = new GameObject("VM_IronAxe");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.15f, 0f), Quaternion.identity, new Vector3(0.028f, 0.24f, 0.028f), woodMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, -0.07f, 0f), Quaternion.identity, new Vector3(0.042f, 0.036f, 0.042f), woodMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(0.034f, 0.08f, 0.034f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.28f, 0f), Quaternion.identity, new Vector3(0.034f, 0.022f, 0.034f), leatherMat);
        // Olho forjado + martelo posterior + lamina barbada chanfrada
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.34f, 0f), Quaternion.identity, new Vector3(0.044f, 0.048f, 0.044f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.34f, -0.034f), Quaternion.identity, new Vector3(0.036f, 0.042f, 0.032f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.335f, 0.052f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.024f, 0.115f, 0.145f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.305f, 0.128f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.011f, 0.148f, 0.046f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.305f, 0.148f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.004f, 0.150f, 0.016f), boneMat);
        root.transform.localRotation = Quaternion.Euler(10f, -16f, 5f);
        return root;
    }

    private GameObject BuildSlingVisual()
    {
        GameObject root = new GameObject("VM_Sling");
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.02f, 0.04f), Quaternion.Euler(22f, 0f, 0f), new Vector3(0.016f, 0.14f, 0.016f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0.025f, 0.09f, 0.08f), Quaternion.Euler(40f, 12f, -18f), new Vector3(0.008f, 0.13f, 0.008f), leatherMat);
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(-0.025f, 0.09f, 0.08f), Quaternion.Euler(40f, -12f, 18f), new Vector3(0.008f, 0.13f, 0.008f), leatherMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.16f, 0.14f), Quaternion.Euler(20f, 0f, 0f), new Vector3(0.068f, 0.024f, 0.088f), leatherMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.182f, 0.14f), Quaternion.identity, new Vector3(0.050f, 0.044f, 0.050f), steelMat);
        root.transform.localRotation = Quaternion.Euler(12f, -10f, 6f);
        return root;
    }

    private GameObject BuildTorchVisual()
    {
        GameObject root = TryLoadPrefabVisual("Prefabs/Torch_Prefab", null, "VM_Torch",
            Vector3.zero, Vector3.zero, Vector3.one * 0.68f);
        Vector3 lightLocalPos = new Vector3(0f, 0.36f, 0f);
        Transform lightParent = null;

        if (root == null)
        {
            root = new GameObject("VM_Torch");
            CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.12f, 0f), Quaternion.identity, new Vector3(0.028f, 0.18f, 0.028f), woodMat);
            CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.30f, 0f), Quaternion.identity, new Vector3(0.065f, 0.08f, 0.065f), leatherMat);
            Material flameMat = Resources.Load<Material>("Materials/Mat_Emissive_Campfire");
            if (flameMat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                flameMat = new Material(lit) { color = new Color(1f, 0.55f, 0.15f) };
                if (flameMat.HasProperty("_EmissionColor"))
                {
                    flameMat.EnableKeyword("_EMISSION");
                    flameMat.SetColor("_EmissionColor", new Color(2.5f, 1.0f, 0.25f));
                }
            }
            GameObject flame = CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.35f, 0f), Quaternion.identity, new Vector3(0.055f, 0.09f, 0.055f), flameMat);
            lightParent = flame.transform;
            lightLocalPos = Vector3.zero;
        }

        GameObject lObj = new GameObject("Torch_PointLight");
        lObj.transform.SetParent(lightParent != null ? lightParent : root.transform, false);
        lObj.transform.localPosition = lightLocalPos;
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
        GameObject root = TryLoadPrefabVisual("Prefabs/Lantern_Prefab", null, "VM_Lantern",
            Vector3.zero, Vector3.zero, Vector3.one * 0.65f);
        if (root == null)
        {
            root = new GameObject("VM_Lantern");
            CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.08f, 0f), Quaternion.identity, new Vector3(0.08f, 0.02f, 0.08f), steelMat);
            CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.22f, 0f), Quaternion.identity, new Vector3(0.085f, 0.02f, 0.085f), steelMat);

            Material glowMat = Resources.Load<Material>("Materials/Mat_Emissive_BeaconGlow");
            if (glowMat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                glowMat = new Material(lit) { color = new Color(1f, 0.85f, 0.52f) };
                if (glowMat.HasProperty("_EmissionColor"))
                {
                    glowMat.EnableKeyword("_EMISSION");
                    glowMat.SetColor("_EmissionColor", new Color(2.2f, 1.65f, 0.75f));
                }
            }
            CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.15f, 0f), Quaternion.identity, new Vector3(0.06f, 0.06f, 0.06f), glowMat);
        }

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
        CreatePart(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.06f, 0.036f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.385f, 0.011f, 0.385f), leatherMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.06f, 0.065f), Quaternion.identity, new Vector3(0.115f, 0.115f, 0.055f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.06f, 0.056f), Quaternion.identity, new Vector3(0.32f, 0.028f, 0.012f), steelMat);
        CreatePart(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.06f, 0.056f), Quaternion.identity, new Vector3(0.028f, 0.32f, 0.012f), steelMat);
        // Rebites de latao na borda
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0.14f, 0.06f, 0.060f), Quaternion.identity, new Vector3(0.018f, 0.018f, 0.012f), brassMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(-0.14f, 0.06f, 0.060f), Quaternion.identity, new Vector3(0.018f, 0.018f, 0.012f), brassMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.20f, 0.060f), Quaternion.identity, new Vector3(0.018f, 0.018f, 0.012f), brassMat);
        CreatePart(PrimitiveType.Sphere, root.transform, new Vector3(0f, -0.08f, 0.060f), Quaternion.identity, new Vector3(0.018f, 0.018f, 0.012f), brassMat);
        root.transform.localPosition = new Vector3(0.03f, -0.02f, 0.06f);
        root.transform.localRotation = Quaternion.Euler(4f, 24f, 0f);
        return root;
    }

    private static GameObject CreatePart(PrimitiveType type, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        SafeDestroy(obj.GetComponent<Collider>());
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
        OnEquipChanged(mainHandId, offHandId, suppressDrawFoley: false);
    }

    public void OnEquipChanged(int mainHandId, int offHandId, bool suppressDrawFoley)
    {
        bool changed = hasInitializedEquip && (currentMainHandId != mainHandId || currentOffHandId != offHandId);
        hasInitializedEquip = true;

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
        isSlingWinding = false;
        slingWind01 = 0f;
        inspectTimer = 0f;
        if (bowNockedArrow != null) bowNockedArrow.SetActive(false);

        if (changed && !suppressDrawFoley)
        {
            equipDrawTimer = equipDrawDuration;
            if (externalArmsAnimator != null) externalArmsAnimator.SetTrigger(AnimEquipHash);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.EquipWeapon, null, 0.56f, 0.92f, 1.08f);
            }
        }

        RefreshLeftLighterState();
        RefreshVisibility();
    }

    public void RefreshLeftLighterState()
    {
        if (leftLighterVisual == null) return;
        bool show = playerCache != null && playerCache.IsLighterOn && currentOffHandId <= 0 && consumeTimer <= 0f;
        if (leftLighterVisual.activeSelf != show)
        {
            leftLighterVisual.SetActive(show);
        }
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
        inspectTimer = 0f;
        attackComboIndex = Mathf.Abs(comboIndex) % 3;
        attackIsHeavy = isHeavy;
        attackWeaponId = currentMainHandId;
        attackIsThrust = currentMainHandId == 308 || (currentMainHandId == 302 && attackComboIndex == 1);
        float finisherBonus = attackComboIndex == 2 ? 1.12f : 1.0f;
        attackDuration = (isHeavy ? 0.42f : (currentMainHandId == 302 ? 0.21f : (currentMainHandId == 301 || currentMainHandId == 311 ? 0.35f : 0.30f))) * finisherBonus;
        recoilOffset = isHeavy ? 0.045f : (attackComboIndex == 2 ? 0.032f : 0.02f);

        if (externalArmsAnimator != null)
        {
            externalArmsAnimator.SetTrigger(isHeavy ? AnimHeavyHash : AnimAttackHash);
        }
    }

    public void TriggerVerticalImpulse(float amount)
    {
        verticalImpulseOffset = Mathf.Clamp(verticalImpulseOffset + amount, -0.07f, 0.07f);
    }

    public void TriggerLandingDip(float fallSpeed)
    {
        float dip = Mathf.Clamp(fallSpeed * 0.0055f, 0.02f, 0.068f);
        verticalImpulseOffset = Mathf.Clamp(verticalImpulseOffset + dip, -0.07f, 0.07f);
        recoilOffset = Mathf.Max(recoilOffset, dip * 0.4f);
    }

    public void TriggerImpactBounce(float strength = 1.0f)
    {
        float s = Mathf.Clamp(strength, 0.2f, 2.0f);
        impactBounceTimer = 0.14f;
        impactBounceStrength = s;
        recoilOffset = Mathf.Max(recoilOffset, 0.036f * s);
        verticalImpulseOffset = Mathf.Clamp(verticalImpulseOffset - 0.024f * s, -0.07f, 0.07f);
    }

    public void SetInteractionHoldPose(bool active, float progress01)
    {
        isHoldInteracting = active;
        interactionHoldProgress = active ? Mathf.Clamp01(progress01) : 0f;
    }

    public void SetGuardPose(bool active)
    {
        isGuarding = active;
        if (active) inspectTimer = 0f;
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

    public void TriggerGatherAnimation()
    {
        gatherTimer = gatherDuration;
        if (externalArmsAnimator != null) externalArmsAnimator.SetTrigger(AnimGatherHash);
    }

    public void TriggerInspectAnimation()
    {
        if (isAttacking || isBowDrawing || isSpearAiming || isSlingWinding || isGuarding) return;
        inspectTimer = inspectDuration;
        if (externalArmsAnimator != null) externalArmsAnimator.SetTrigger(AnimInspectHash);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.WeaponInspect, null, 0.48f, 0.96f, 1.04f);
        }
    }

    public void TriggerClimbAnimation(float duration = 0.36f)
    {
        climbDuration = Mathf.Max(0.20f, duration);
        climbTimer = climbDuration;
        inspectTimer = 0f;
        if (externalArmsAnimator != null) externalArmsAnimator.SetTrigger(AnimClimbHash);
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

    public void SetSlingWindup(bool active, float wind01)
    {
        isSlingWinding = active;
        slingWind01 = Mathf.Clamp01(wind01);
    }

    public void TriggerConsumeAnimation()
    {
        TriggerConsumeAnimation(ConsumeAnimKind.Generic, -1);
    }

    public void TriggerConsumeAnimation(ConsumeAnimKind kind, int itemId = -1)
    {
        activeConsumeKind = kind;
        switch (kind)
        {
            case ConsumeAnimKind.Bandage:
            case ConsumeAnimKind.Splint:
                consumeDuration = 0.68f;
                break;
            case ConsumeAnimKind.ReadNote:
                consumeDuration = 0.72f;
                break;
            default:
                consumeDuration = 0.52f;
                break;
        }
        consumeTimer = consumeDuration;

        HideAllConsumeProps();
        if (consumeProps.TryGetValue(kind, out GameObject prop) && prop != null)
        {
            prop.SetActive(true);
            activeConsumeProp = prop;
        }
        RefreshLeftLighterState();

        if (externalArmsAnimator != null) externalArmsAnimator.SetTrigger(AnimConsumeHash);
    }

    private void HideAllConsumeProps()
    {
        foreach (var kv in consumeProps)
        {
            if (kv.Value != null && kv.Value.activeSelf) kv.Value.SetActive(false);
        }
        activeConsumeProp = null;
    }

    private void Update()
    {
        if (viewmodelRoot == null || isCarryingLogs) return;
        if (Time.timeScale <= 0.001f) return;

        bool gameplayAllowed = FragaInputRouter.Instance == null || FragaInputRouter.Instance.IsGameplayAllowed;
        if (gameplayAllowed && Input.GetKeyDown(KeyCode.T))
        {
            TriggerInspectAnimation();
        }

        RefreshLeftLighterState();

        float dt = Time.deltaTime;
        float speed = playerCache != null ? playerCache.CurrentSpeed : 0f;
        bool sprinting = playerCache != null && playerCache.isSprinting && speed > 0.3f;
        bool crouching = playerCache != null && playerCache.isCrouching;
        bool swimming = playerCache != null && playerCache.isSwimming;
        bool moving = speed > 0.35f;

        if (externalArmsAnimator != null)
        {
            externalArmsAnimator.SetFloat(AnimSpeedHash, speed);
            externalArmsAnimator.SetBool(AnimSprintHash, sprinting);
            externalArmsAnimator.SetBool(AnimCrouchHash, crouching);
            externalArmsAnimator.SetBool(AnimSwimmingHash, swimming);
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

        // 2. Damped Look Sway (Zero-GC e suave sem tremor)
        bool lookAllowed = FragaInputRouter.Instance == null || FragaInputRouter.Instance.IsCameraLookAllowed;
        float mx = lookAllowed ? Input.GetAxisRaw("Mouse X") : 0f;
        float my = lookAllowed ? Input.GetAxisRaw("Mouse Y") : 0f;
        Vector3 rawSway = new Vector3(Mathf.Clamp(-mx * 0.006f, -0.025f, 0.025f), Mathf.Clamp(-my * 0.006f, -0.025f, 0.025f), 0f);
        smoothedSway = Vector3.Lerp(smoothedSway, rawSway, dt * 12f);

        // 3. Torch flicker leve
        if (heldTorchLight != null && currentOffHandId == 303)
        {
            heldTorchLight.intensity = heldTorchBaseIntensity + Mathf.Sin(Time.time * 13f) * 0.25f + Mathf.Cos(Time.time * 23f) * 0.15f;
        }

        recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, dt * 0.22f);
        verticalImpulseOffset = Mathf.Lerp(verticalImpulseOffset, 0f, dt * 10f);

        if (equipDrawTimer > 0f) equipDrawTimer = Mathf.Max(0f, equipDrawTimer - dt);
        if (inspectTimer > 0f) inspectTimer = Mathf.Max(0f, inspectTimer - dt);
        if (gatherTimer > 0f) gatherTimer = Mathf.Max(0f, gatherTimer - dt);
        if (climbTimer > 0f) climbTimer = Mathf.Max(0f, climbTimer - dt);

        // 4. Right Arm Pose
        Vector3 targetRPos = rightRestPos + bobOffset + smoothedSway - Vector3.forward * recoilOffset - Vector3.up * verticalImpulseOffset;
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
        else if (climbTimer > 0f)
        {
            float p = 1f - Mathf.Clamp01(climbTimer / climbDuration);
            float grab = Mathf.Sin(p * Mathf.PI);
            targetRPos = new Vector3(0.22f, -0.06f + (0.5f - p) * 0.20f, 0.48f + grab * 0.06f);
            targetRRot = Quaternion.Euler(-28f, -14f, 6f);
        }
        else if (swimming)
        {
            swimPhase += dt * (moving ? 5.6f : 2.2f);
            float stroke = Mathf.Sin(swimPhase);
            targetRPos = new Vector3(0.24f + stroke * 0.08f, -0.22f + Mathf.Cos(swimPhase) * 0.03f, 0.36f + stroke * 0.12f) + smoothedSway * 0.5f;
            targetRRot = Quaternion.Euler(22f + stroke * 18f, -16f, 12f);
        }
        else if (isBowDrawing)
        {
            targetRPos = new Vector3(0.11f, -0.11f, 0.36f) + smoothedSway * 0.4f;
            targetRRot = Quaternion.Euler(0f, -14f, 4f);
        }
        else if (isSpearAiming)
        {
            targetRPos = new Vector3(0.29f, -0.08f, 0.26f - spearCharge01 * 0.09f) + smoothedSway * 0.5f;
            targetRRot = Quaternion.Euler(-28f - spearCharge01 * 12f, -4f, 6f);
        }
        else if (isSlingWinding)
        {
            slingSpinPhase += dt * Mathf.Lerp(10f, 26f, slingWind01);
            float orbit = 0.045f * (0.4f + slingWind01 * 0.6f);
            targetRPos = new Vector3(0.22f + Mathf.Cos(slingSpinPhase) * orbit, -0.10f + Mathf.Sin(slingSpinPhase) * orbit, 0.34f) + smoothedSway * 0.5f;
            targetRRot = Quaternion.Euler(-16f + Mathf.Sin(slingSpinPhase) * 18f, -18f, Mathf.Cos(slingSpinPhase) * 32f);
        }
        else if (isGuarding && currentOffHandId != 309)
        {
            targetRPos = new Vector3(0.12f, -0.16f, 0.36f) + smoothedSway * 0.5f;
            targetRRot = Quaternion.Euler(14f, -48f, 38f);
        }
        else if (inspectTimer > 0f)
        {
            float p = 1f - Mathf.Clamp01(inspectTimer / inspectDuration);
            float arch = Mathf.Sin(p * Mathf.PI);
            float roll = Mathf.Sin(p * Mathf.PI * 2f);
            targetRPos = Vector3.Lerp(targetRPos, new Vector3(0.14f, -0.15f + arch * 0.04f, 0.37f), arch);
            targetRRot = Quaternion.Euler(-18f * arch, -34f * arch + roll * 22f, 26f * roll);
        }
        else if (sprinting && !isBowDrawing && !isSpearAiming && !isSlingWinding)
        {
            // Postura de corrida estilo The Forest (arma cruzada abaixo da linha de visao)
            targetRPos += new Vector3(-0.07f, -0.05f, -0.04f);
            targetRRot = Quaternion.Euler(18f, -34f, 24f);
        }

        if (equipDrawTimer > 0f)
        {
            float drawT = Mathf.Clamp01(equipDrawTimer / equipDrawDuration);
            targetRPos += new Vector3(0.05f * drawT, -0.22f * drawT, -0.08f * drawT);
            targetRRot *= Quaternion.Euler(34f * drawT, 12f * drawT, -10f * drawT);
        }

        if (isHoldInteracting && !isAttacking && !isBowDrawing && !isSpearAiming && !isSlingWinding && !isGuarding)
        {
            float workWave = Mathf.Sin(interactionHoldProgress * Mathf.PI * 6f);
            targetRPos = Vector3.Lerp(targetRPos, new Vector3(0.18f, -0.17f + workWave * 0.015f, 0.50f + workWave * 0.022f), 0.78f);
            targetRRot = Quaternion.Slerp(targetRRot, Quaternion.Euler(14f + workWave * 8f, -22f, 12f), 0.78f);
        }

        if (impactBounceTimer > 0f)
        {
            impactBounceTimer -= dt;
            float bi = Mathf.Sin(Mathf.Clamp01(impactBounceTimer / 0.14f) * Mathf.PI) * impactBounceStrength;
            targetRPos += new Vector3(0.016f * bi, 0.018f * bi, -0.048f * bi);
            targetRRot *= Quaternion.Euler(-14f * bi, 5f * bi, -6f * bi);
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
        Vector3 targetLPos = leftRestPos + new Vector3(-bobOffset.x, bobOffset.y * 0.8f - verticalImpulseOffset, 0f) + smoothedSway * 0.8f;
        Quaternion targetLRot = Quaternion.identity;

        if (currentOffHandId == 309 && consumeTimer <= 0f) // Escudo
        {
            Vector3 shieldIdle = new Vector3(-0.24f, -0.22f, 0.36f);
            Vector3 shieldBlock = new Vector3(-0.07f, -0.13f, 0.34f);
            targetLPos = Vector3.Lerp(shieldIdle, shieldBlock, guardBlend) + smoothedSway * 0.5f;
            targetLRot = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(4f, 18f, -6f), guardBlend);
        }
        else if (climbTimer > 0f)
        {
            float p = 1f - Mathf.Clamp01(climbTimer / climbDuration);
            float grab = Mathf.Sin(p * Mathf.PI);
            targetLPos = new Vector3(-0.22f, -0.04f + (0.5f - p) * 0.22f, 0.46f + grab * 0.08f);
            targetLRot = Quaternion.Euler(-26f, 16f, -8f);
        }
        else if (isBowDrawing)
        {
            targetLPos = new Vector3(0.06f, -0.10f, 0.33f - bowDraw01 * 0.15f);
            targetLRot = Quaternion.Euler(-6f, 18f, 0f);
        }
        else if (consumeTimer > 0f)
        {
            consumeTimer -= dt;
            float norm = Mathf.Clamp01(consumeTimer / Mathf.Max(0.1f, consumeDuration));
            float t = Mathf.Sin(norm * Mathf.PI);
            if (activeConsumeKind == ConsumeAnimKind.Bandage || activeConsumeKind == ConsumeAnimKind.Splint)
            {
                float wrapOsc = Mathf.Sin((1f - norm) * Mathf.PI * 3f);
                targetLPos = Vector3.Lerp(targetLPos, new Vector3(-0.02f + wrapOsc * 0.03f, -0.14f, 0.32f), t);
                targetLRot = Quaternion.Euler(-18f * t, 36f * t, 20f * wrapOsc);
            }
            else if (activeConsumeKind == ConsumeAnimKind.Drink)
            {
                targetLPos = Vector3.Lerp(targetLPos, new Vector3(-0.05f, -0.04f, 0.24f), t);
                targetLRot = Quaternion.Euler(-48f * t, 24f * t, -18f * t);
            }
            else if (activeConsumeKind == ConsumeAnimKind.ReadNote)
            {
                targetLPos = Vector3.Lerp(targetLPos, new Vector3(-0.02f, -0.10f, 0.32f), t);
                targetLRot = Quaternion.Euler(-16f * t, 14f * t, 0f);
            }
            else
            {
                targetLPos = Vector3.Lerp(targetLPos, new Vector3(-0.06f, -0.07f, 0.25f), t);
                targetLRot = Quaternion.Euler(-32f * t, 20f * t, 0f);
            }

            if (consumeTimer <= 0f)
            {
                consumeTimer = 0f;
                HideAllConsumeProps();
                RefreshLeftLighterState();
            }
        }
        else if (gatherTimer > 0f)
        {
            float p = 1f - Mathf.Clamp01(gatherTimer / gatherDuration);
            float reach = Mathf.Sin(p * Mathf.PI);
            targetLPos = Vector3.Lerp(targetLPos, new Vector3(-0.08f, -0.16f - reach * 0.05f, 0.56f), reach);
            targetLRot = Quaternion.Euler(24f * reach, 18f * reach, -12f * reach);
        }
        else if (swimming)
        {
            float strokeL = Mathf.Sin(swimPhase + Mathf.PI);
            targetLPos = new Vector3(-0.24f - strokeL * 0.08f, -0.22f + Mathf.Cos(swimPhase) * 0.03f, 0.35f + strokeL * 0.12f) + smoothedSway * 0.5f;
            targetLRot = Quaternion.Euler(22f + strokeL * 18f, 16f, -12f);
        }
        else if (isHoldInteracting && !isBowDrawing && !isGuarding)
        {
            float workWave = Mathf.Sin(interactionHoldProgress * Mathf.PI * 6f);
            targetLPos = Vector3.Lerp(targetLPos, new Vector3(-0.16f, -0.18f - workWave * 0.012f, 0.46f), 0.65f);
            targetLRot = Quaternion.Slerp(targetLRot, Quaternion.Euler(-14f, 18f, -8f), 0.65f);
        }
        else if (leftLighterVisual != null && leftLighterVisual.activeSelf)
        {
            targetLPos = new Vector3(-0.20f, -0.16f, 0.42f) + bobOffset * 0.6f + smoothedSway * 0.7f;
            targetLRot = Quaternion.Euler(-8f, 14f, -4f);
        }

        if (equipDrawTimer > 0f && currentOffHandId > 0)
        {
            float drawT = Mathf.Clamp01(equipDrawTimer / equipDrawDuration);
            targetLPos += new Vector3(-0.04f * drawT, -0.20f * drawT, -0.06f * drawT);
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

        // 6. Articulacao dinamica dos dedos (empunhadura fechada vs palma aberta)
        if (rightFingerGroup != null)
        {
            bool rightGripping = currentMainHandId > 0 || isAttacking || isGuarding;
            float targetCurl = rightGripping ? 48f : (gatherTimer > 0f || climbTimer > 0f ? 10f : 22f);
            rightFingerGroup.localRotation = Quaternion.Slerp(rightFingerGroup.localRotation, Quaternion.Euler(targetCurl, 0f, 0f), dt * 16f);
        }
        if (leftFingerGroup != null)
        {
            bool leftGripping = currentOffHandId > 0 || consumeTimer > 0f || (leftLighterVisual != null && leftLighterVisual.activeSelf);
            float targetCurlL = leftGripping ? 46f : (gatherTimer > 0f || climbTimer > 0f ? 8f : 22f);
            leftFingerGroup.localRotation = Quaternion.Slerp(leftFingerGroup.localRotation, Quaternion.Euler(targetCurlL, 0f, 0f), dt * 16f);
        }
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

        // Punho alternado quando desarmado
        if (attackWeaponId <= 0)
        {
            float punchArc = Mathf.Sin(p * Mathf.PI);
            posOffset = new Vector3(-0.09f * punchArc, 0.06f * punchArc, 0.24f * punchArc);
            rotOffset = Quaternion.Euler(-16f * punchArc, 18f * punchArc, -10f * punchArc);
            return;
        }

        float dir = attackComboIndex == 0 ? 1f : (attackComboIndex == 1 ? -1f : 1.16f);
        float heavyScale = (attackIsHeavy ? 1.35f : 1f) * (attackComboIndex == 2 ? 1.20f : 1f);

        Vector3 windPos;
        Quaternion windRot;
        Vector3 strikePos;
        Quaternion strikeRot;

        if (attackWeaponId == 301 || attackWeaponId == 307 || attackWeaponId == 311)
        {
            windPos = new Vector3(0.03f * dir, 0.20f * heavyScale, -0.11f);
            windRot = Quaternion.Euler(-48f * heavyScale, 8f * dir, 6f * dir);
            strikePos = new Vector3(-0.06f * dir, -0.19f, 0.20f);
            strikeRot = Quaternion.Euler(68f * heavyScale, -12f * dir, -8f * dir);
        }
        else if (attackWeaponId == 302)
        {
            windPos = new Vector3(0.10f * dir, 0.04f, -0.04f);
            windRot = Quaternion.Euler(-10f, 34f * dir, 32f * dir);
            strikePos = new Vector3(-0.20f * dir, -0.05f, 0.18f);
            strikeRot = Quaternion.Euler(16f, -52f * dir, -44f * dir);
        }
        else
        {
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
