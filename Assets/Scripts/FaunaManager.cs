using UnityEngine;
using System.Runtime.InteropServices;

/// FaunaManager: Gere a fauna (cervos, lobos, ursos, javalis e coelhos) via IA nativa e simulação procedural.
/// Renderiza animais vivos sobre o relevo do terreno procedural, com animações LOD, flash de dano, HP e carcaças esfoláveis.
public class FaunaManager : MonoBehaviour
{
    public static FaunaManager Instance { get; private set; }

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    private static extern void UpdateDeer(
        System.IntPtr posX, System.IntPtr posY, System.IntPtr velX, System.IntPtr velY,
        System.IntPtr stateFlags, int count,
        float playerX, float playerY,
        System.IntPtr wolfPosX, System.IntPtr wolfPosY, int wolfCount,
        float deltaTime, float wanderSpeed, float fleeSpeed, float detectionRadius,
        System.IntPtr mapData, int mapWidth, int mapHeight);

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    private static extern void UpdateWolves(
        System.IntPtr posX, System.IntPtr posY, System.IntPtr velX, System.IntPtr velY,
        System.IntPtr stateFlags, int count,
        float playerX, float playerY, float lightRadius,
        System.IntPtr deerPosX, System.IntPtr deerPosY, int deerCount,
        float deltaTime, float huntSpeed, float isNight,
        System.IntPtr mapData, int mapWidth, int mapHeight);

    [Header("Config")]
    public int maxDeer = 22;
    public int maxWolves = 10;
    public float deerWanderSpeed = 1.5f;
    public float deerFleeSpeed = 6f;
    public float deerDetectionRadius = 9f;
    public float wolfHuntSpeed = 4.2f;
    public float wolfAttackRange = 1.6f;
    public float wolfDamage = 6f;

    [Header("Rendering (Prefabs da Fauna)")]
    public GameObject deerPrefab;
    public GameObject wolfPrefab;
    public GameObject bearPrefab;
    public GameObject boarPrefab;
    public GameObject rabbitPrefab;

    [Header("Dependencies")]
    public Transform playerTransform;
    public SanitySystem playerSanity;
    public DayNightCycle dayNightCycle;
    public InventoryAdvanced inventory;
    public ResourceManager resourceManager;

    // Deer SoA
    private float[] deerPosX, deerPosY, deerVelX, deerVelY, deerHP;
    private int[] deerStates;
    private GCHandle deerPxH, deerPyH, deerVxH, deerVyH, deerSH;

    // Wolf SoA
    private float[] wolfPosX, wolfPosY, wolfVelX, wolfVelY, wolfHP;
    private int[] wolfStates;
    private GCHandle wolfPxH, wolfPyH, wolfVxH, wolfVyH, wolfSH;

    private GameObject[] deerInstances;
    private GameObject[] wolfInstances;
    private GameObject[] bearInstances;
    private GameObject[] boarInstances;
    private GameObject[] rabbitInstances;

    private AnimalProceduralAnimator[] deerAnims;
    private AnimalProceduralAnimator[] wolfAnims;
    private AnimalProceduralAnimator[] bearAnims;
    private AnimalProceduralAnimator[] boarAnims;
    private AnimalProceduralAnimator[] rabbitAnims;

    private float[] wolfAttackCooldowns;

    // Ursos
    public int maxBears = 5;
    private float[] bearPosX, bearPosY;
    private float[] bearVelX, bearVelY;
    private float[] bearHP;
    private float[] bearAttackCooldowns;

    // Javalis (Wild Boars)
    public int maxBoars = 9;
    private float[] boarPosX, boarPosY;
    private float[] boarHP;
    private float[] boarVelX, boarVelY;
    private float[] boarAttackCooldowns;

    // Coelhos
    public int maxRabbits = 20;
    private float[] rabbitPosX, rabbitPosY;
    private float[] rabbitVelX, rabbitVelY;

    // Caches Zero-GC
    private TrapSystem trapSystemCache;
    private HapticManager hapticManagerCache;
    private PlayerStatus playerStatusCache;
    private PlayerController playerControllerCache;
    private Material cachedCarcassMat;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        trapSystemCache = FindAnyObjectByType<TrapSystem>();
        hapticManagerCache = FindAnyObjectByType<HapticManager>();
        if (playerTransform != null)
        {
            playerStatusCache = playerTransform.GetComponent<PlayerStatus>();
            playerControllerCache = playerTransform.GetComponent<PlayerController>();
        }

        if (deerPrefab == null) deerPrefab = Resources.Load<GameObject>("Prefabs/Deer_Prefab");
        if (wolfPrefab == null) wolfPrefab = Resources.Load<GameObject>("Prefabs/Wolf_Prefab");
        if (bearPrefab == null) bearPrefab = Resources.Load<GameObject>("Prefabs/Bear_Prefab");
        if (boarPrefab == null) boarPrefab = Resources.Load<GameObject>("Prefabs/Boar_Prefab");
        if (rabbitPrefab == null) rabbitPrefab = Resources.Load<GameObject>("Prefabs/Rabbit_Prefab");

        if (deerPrefab == null) deerPrefab = CreateProceduralAnimalFallback("Fallback_Deer", new Color(0.55f, 0.35f, 0.18f), new Vector3(0.7f, 1.3f, 1.6f), AnimalProceduralAnimator.LocomotionStyle.QuadrupedUngulate);
        if (wolfPrefab == null) wolfPrefab = CreateProceduralAnimalFallback("Fallback_Wolf", new Color(0.28f, 0.28f, 0.30f), new Vector3(0.6f, 0.85f, 1.4f), AnimalProceduralAnimator.LocomotionStyle.QuadrupedStalker);
        if (bearPrefab == null) bearPrefab = CreateProceduralAnimalFallback("Fallback_Bear", new Color(0.22f, 0.14f, 0.08f), new Vector3(1.1f, 1.3f, 2.0f), AnimalProceduralAnimator.LocomotionStyle.QuadrupedHeavy);
        if (boarPrefab == null) boarPrefab = CreateProceduralAnimalFallback("Fallback_Boar", new Color(0.25f, 0.20f, 0.15f), new Vector3(0.7f, 0.8f, 1.3f), AnimalProceduralAnimator.LocomotionStyle.QuadrupedStalker);
        if (rabbitPrefab == null) rabbitPrefab = CreateProceduralAnimalFallback("Fallback_Rabbit", new Color(0.65f, 0.58f, 0.50f), new Vector3(0.35f, 0.35f, 0.5f), AnimalProceduralAnimator.LocomotionStyle.Hopper);

        deerPosX = new float[maxDeer];
        deerPosY = new float[maxDeer];
        deerVelX = new float[maxDeer];
        deerVelY = new float[maxDeer];
        deerHP = new float[maxDeer];
        deerStates = new int[maxDeer];

        deerPxH = GCHandle.Alloc(deerPosX, GCHandleType.Pinned);
        deerPyH = GCHandle.Alloc(deerPosY, GCHandleType.Pinned);
        deerVxH = GCHandle.Alloc(deerVelX, GCHandleType.Pinned);
        deerVyH = GCHandle.Alloc(deerVelY, GCHandleType.Pinned);
        deerSH = GCHandle.Alloc(deerStates, GCHandleType.Pinned);

        wolfPosX = new float[maxWolves];
        wolfPosY = new float[maxWolves];
        wolfVelX = new float[maxWolves];
        wolfVelY = new float[maxWolves];
        wolfHP = new float[maxWolves];
        wolfStates = new int[maxWolves];

        wolfPxH = GCHandle.Alloc(wolfPosX, GCHandleType.Pinned);
        wolfPyH = GCHandle.Alloc(wolfPosY, GCHandleType.Pinned);
        wolfVxH = GCHandle.Alloc(wolfVelX, GCHandleType.Pinned);
        wolfVyH = GCHandle.Alloc(wolfVelY, GCHandleType.Pinned);
        wolfSH = GCHandle.Alloc(wolfStates, GCHandleType.Pinned);

        deerInstances = new GameObject[maxDeer];
        deerAnims = new AnimalProceduralAnimator[maxDeer];
        wolfInstances = new GameObject[maxWolves];
        wolfAnims = new AnimalProceduralAnimator[maxWolves];
        wolfAttackCooldowns = new float[maxWolves];

        bearInstances = new GameObject[maxBears];
        bearAnims = new AnimalProceduralAnimator[maxBears];
        bearPosX = new float[maxBears];
        bearPosY = new float[maxBears];
        bearVelX = new float[maxBears];
        bearVelY = new float[maxBears];
        bearHP = new float[maxBears];
        bearAttackCooldowns = new float[maxBears];

        boarInstances = new GameObject[maxBoars];
        boarAnims = new AnimalProceduralAnimator[maxBoars];
        boarPosX = new float[maxBoars];
        boarPosY = new float[maxBoars];
        boarHP = new float[maxBoars];
        boarVelX = new float[maxBoars];
        boarVelY = new float[maxBoars];
        boarAttackCooldowns = new float[maxBoars];

        rabbitInstances = new GameObject[maxRabbits];
        rabbitAnims = new AnimalProceduralAnimator[maxRabbits];
        rabbitPosX = new float[maxRabbits];
        rabbitPosY = new float[maxRabbits];
        rabbitVelX = new float[maxRabbits];
        rabbitVelY = new float[maxRabbits];

        SpawnInitialFauna();
    }

    private void GetMapBounds(out float minB, out float maxW, out float maxH)
    {
        float w = (resourceManager != null && resourceManager.MapWidth > 0) ? resourceManager.MapWidth : FragaWorldBlueprint.DefaultWidth;
        float h = (resourceManager != null && resourceManager.MapHeight > 0) ? resourceManager.MapHeight : FragaWorldBlueprint.DefaultHeight;
        minB = 36f;
        maxW = Mathf.Max(64f, w - 36f);
        maxH = Mathf.Max(64f, h - 36f);
    }

    private Vector2 FindDryLandNear(float centerX, float centerZ, float minR, float maxR, float minB, float maxW, float maxH)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(minR, maxR);
            float cx = Mathf.Clamp(centerX + Mathf.Cos(ang) * r, minB, maxW);
            float cz = Mathf.Clamp(centerZ + Mathf.Sin(ang) * r, minB, maxH);
            if (FragaWorldBlueprint.IsInsideIsland(cx, cz) && FragaWorldBlueprint.GetWaterDepth(cx, cz) < 0.12f)
            {
                return new Vector2(cx, cz);
            }
        }
        return new Vector2(Mathf.Clamp(centerX, minB, maxW), Mathf.Clamp(centerZ, minB, maxH));
    }

    private void SpawnInitialFauna()
    {
        GameObject faunaParent = new GameObject("Fauna_Instances");
        FragaWorldBlueprint.EnsureInitialized();

        GetMapBounds(out float minB, out float maxW, out float maxH);
        float pX = (playerTransform != null) ? playerTransform.position.x : FragaWorldBlueprint.CrashCampPos.x;
        float pZ = (playerTransform != null) ? playerTransform.position.z : FragaWorldBlueprint.CrashCampPos.y;

        // 1. Spawn cervos
        for (int i = 0; i < maxDeer; i++)
        {
            if (i < 4)
            {
                Vector2 dPos = FindDryLandNear(pX, pZ, 18f, 36f, minB, maxW, maxH);
                deerPosX[i] = dPos.x;
                deerPosY[i] = dPos.y;
            }
            else
            {
                Vector2 anchor = (i % 3 == 0) ? FragaWorldBlueprint.HighlandLakePos : FragaWorldBlueprint.CrashCampPos;
                Vector2 dPos = FindDryLandNear(anchor.x, anchor.y, 48f, 220f, minB, maxW, maxH);
                deerPosX[i] = dPos.x;
                deerPosY[i] = dPos.y;
            }
            deerHP[i] = 50f;

            if (deerPrefab != null)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(deerPosX[i], deerPosY[i]);
                deerInstances[i] = Instantiate(deerPrefab, new Vector3(deerPosX[i], gy, deerPosY[i]), Quaternion.identity, faunaParent.transform);
                deerInstances[i].SetActive(true);
                deerAnims[i] = deerInstances[i].GetComponent<AnimalProceduralAnimator>();
            }
        }

        // 2. Spawn lobos
        for (int i = 0; i < maxWolves; i++)
        {
            Vector2 anchor = (i < 4) ? new Vector2(pX, pZ) : FragaWorldBlueprint.CelticCastroPos;
            Vector2 wPos = FindDryLandNear(anchor.x, anchor.y, 44f, 120f, minB, maxW, maxH);
            wolfPosX[i] = wPos.x;
            wolfPosY[i] = wPos.y;
            wolfHP[i] = 65f;
            if (wolfPrefab != null)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(wolfPosX[i], wolfPosY[i]);
                wolfInstances[i] = Instantiate(wolfPrefab, new Vector3(wolfPosX[i], gy, wolfPosY[i]), Quaternion.identity, faunaParent.transform);
                wolfInstances[i].SetActive(true);
                wolfAnims[i] = wolfInstances[i].GetComponent<AnimalProceduralAnimator>();
            }
        }

        // 3. Spawn Ursos
        for (int i = 0; i < maxBears; i++)
        {
            Vector2 anchor = (i % 2 == 0) ? FragaWorldBlueprint.SacredCavePos : FragaWorldBlueprint.MountainSummitPos;
            Vector2 bPos = FindDryLandNear(anchor.x, anchor.y, 24f, 110f, minB, maxW, maxH);
            bearPosX[i] = bPos.x;
            bearPosY[i] = bPos.y;
            bearHP[i] = 110f;

            if (bearPrefab != null)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(bearPosX[i], bearPosY[i]);
                bearInstances[i] = Instantiate(bearPrefab, new Vector3(bearPosX[i], gy, bearPosY[i]), Quaternion.identity, faunaParent.transform);
                bearInstances[i].transform.localScale = Vector3.one * 1.18f;
                bearInstances[i].SetActive(true);
                bearAnims[i] = bearInstances[i].GetComponent<AnimalProceduralAnimator>();
            }
        }

        // 4. Spawn Javalis
        for (int i = 0; i < maxBoars; i++)
        {
            Vector2 anchor = (i < 2) ? new Vector2(pX, pZ) : ((i % 2 == 0) ? FragaWorldBlueprint.SwampBasinPos : FragaWorldBlueprint.CrashCampPos);
            Vector2 boPos = FindDryLandNear(anchor.x, anchor.y, i < 2 ? 22f : 55f, i < 2 ? 40f : 160f, minB, maxW, maxH);
            boarPosX[i] = boPos.x;
            boarPosY[i] = boPos.y;
            boarHP[i] = 60f;

            if (boarPrefab != null)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(boarPosX[i], boarPosY[i]);
                boarInstances[i] = Instantiate(boarPrefab, new Vector3(boarPosX[i], gy, boarPosY[i]), Quaternion.identity, faunaParent.transform);
                boarInstances[i].SetActive(true);
                boarAnims[i] = boarInstances[i].GetComponent<AnimalProceduralAnimator>();
            }
        }

        // 5. Spawn Coelhos
        for (int i = 0; i < maxRabbits; i++)
        {
            Vector2 rPos = FindDryLandNear(pX, pZ, i < 5 ? 12f : 40f, i < 5 ? 28f : 140f, minB, maxW, maxH);
            rabbitPosX[i] = rPos.x;
            rabbitPosY[i] = rPos.y;

            if (rabbitPrefab != null)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(rabbitPosX[i], rabbitPosY[i]);
                rabbitInstances[i] = Instantiate(rabbitPrefab, new Vector3(rabbitPosX[i], gy, rabbitPosY[i]), Quaternion.identity, faunaParent.transform);
                rabbitInstances[i].SetActive(true);
                rabbitAnims[i] = rabbitInstances[i].GetComponent<AnimalProceduralAnimator>();
            }
        }
    }

    private float nextAmbientRelocateTime = 0f;

    private void Update()
    {
        if (playerTransform == null || resourceManager == null) return;
        if (playerStatusCache == null) playerStatusCache = playerTransform.GetComponent<PlayerStatus>();
        if (playerControllerCache == null) playerControllerCache = playerTransform.GetComponent<PlayerController>() ?? PlayerController.Instance;

        float px = playerTransform.position.x;
        float pz = playerTransform.position.z;
        float dt = Time.deltaTime;
        float lightR = playerSanity != null ? playerSanity.GetLightRadius() : 5f;
        float isNight = 0f;

        if (dayNightCycle != null)
        {
            isNight = (dayNightCycle.currentTime >= 19f || dayNightCycle.currentTime < 6f) ? 1f : 0f;
        }

        System.IntPtr mapData = resourceManager.MapDataPtr;
        int mw = resourceManager.MapWidth;
        int mh = resourceManager.MapHeight;

        if (mapData == System.IntPtr.Zero) return;

        UpdateDeer(
            deerPxH.AddrOfPinnedObject(), deerPyH.AddrOfPinnedObject(),
            deerVxH.AddrOfPinnedObject(), deerVyH.AddrOfPinnedObject(),
            deerSH.AddrOfPinnedObject(), maxDeer,
            px, pz,
            wolfPxH.AddrOfPinnedObject(), wolfPyH.AddrOfPinnedObject(), maxWolves,
            dt, deerWanderSpeed, deerFleeSpeed, deerDetectionRadius,
            mapData, mw, mh);

        UpdateWolves(
            wolfPxH.AddrOfPinnedObject(), wolfPyH.AddrOfPinnedObject(),
            wolfVxH.AddrOfPinnedObject(), wolfVyH.AddrOfPinnedObject(),
            wolfSH.AddrOfPinnedObject(), maxWolves,
            px, pz, lightR,
            deerPxH.AddrOfPinnedObject(), deerPyH.AddrOfPinnedObject(), maxDeer,
            dt, wolfHuntSpeed, isNight,
            mapData, mw, mh);

        CheckWolfAttacks(px, pz, dt);

        if (trapSystemCache != null)
        {
            trapSystemCache.CheckTrapsForWolves(wolfPosX, wolfPosY, maxWolves);
        }

        UpdateBears(px, pz, dt);
        UpdateBoars(px, pz, dt);
        UpdateRabbits(px, pz, dt);

        if (Time.time >= nextAmbientRelocateTime)
        {
            nextAmbientRelocateTime = Time.time + 2.5f;
            RelocateFarFauna(px, pz);
        }

        UpdateRenderTransforms();
    }

    private void RelocateFarFauna(float px, float pz)
    {
        GetMapBounds(out float minB, out float maxW, out float maxH);
        const float maxRoamSq = 190f * 190f;

        void CheckArray(float[] arrX, float[] arrZ, int count, float minRing, float maxRing)
        {
            for (int i = 0; i < count; i++)
            {
                if (arrX[i] < 0f) continue;
                float dx = arrX[i] - px;
                float dz = arrZ[i] - pz;
                if (dx * dx + dz * dz > maxRoamSq)
                {
                    Vector2 np = FindDryLandNear(px, pz, minRing, maxRing, minB, maxW, maxH);
                    arrX[i] = np.x;
                    arrZ[i] = np.y;
                }
            }
        }

        CheckArray(deerPosX, deerPosY, maxDeer, 68f, 130f);
        CheckArray(wolfPosX, wolfPosY, maxWolves, 74f, 135f);
        CheckArray(boarPosX, boarPosY, maxBoars, 68f, 130f);
        CheckArray(rabbitPosX, rabbitPosY, maxRabbits, 60f, 115f);
    }

    private void CheckWolfAttacks(float px, float pz, float dt)
    {
        float attackRangeSq = wolfAttackRange * wolfAttackRange;

        for (int i = 0; i < maxWolves; i++)
        {
            wolfAttackCooldowns[i] -= dt;

            if (wolfStates[i] == 1)
            {
                float dx = wolfPosX[i] - px;
                float dy = wolfPosY[i] - pz;
                float distSq = dx * dx + dy * dy;

                if (distSq < attackRangeSq && wolfAttackCooldowns[i] <= 0f)
                {
                    Vector3 wolfPos = new Vector3(wolfPosX[i], TerrainMeshGenerator.GetTerrainHeight(wolfPosX[i], wolfPosY[i]), wolfPosY[i]);
                    if (playerControllerCache != null && playerControllerCache.IsInvulnerable)
                    {
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFDD>ESQUIVA PERFEITA!</color>");
                        wolfAttackCooldowns[i] = 1.8f;
                        continue;
                    }

                    if (playerSanity != null)
                    {
                        playerSanity.sanity -= wolfDamage;
                        if (hapticManagerCache != null) hapticManagerCache.TriggerHeavyVibration();
                    }
                    if (playerStatusCache != null)
                    {
                        float hpBefore = playerStatusCache.health;
                        playerStatusCache.TakeDamage(wolfDamage * 1.4f);
                        if (playerStatusCache.health < hpBefore - 1f && Random.value < 0.30f)
                        {
                            playerStatusCache.ApplyBleeding();
                        }
                    }
                    if (playerControllerCache != null)
                    {
                        Vector3 kb = (playerTransform.position - wolfPos);
                        kb.y = 0f;
                        if (kb.sqrMagnitude > 0.01f) playerControllerCache.ApplyKnockback(kb.normalized * 5.2f);
                    }
                    if (HUDSurvivalOverlay.Instance != null)
                    {
                        HUDSurvivalOverlay.Instance.ShowDamageFromDirection(wolfPos);
                    }
                    wolfAttackCooldowns[i] = 3.2f;
                }
            }
        }
    }

    private void UpdateBears(float px, float pz, float dt)
    {
        GetMapBounds(out float minB, out float maxW, out float maxH);
        for (int i = 0; i < maxBears; i++)
        {
            if (bearHP[i] <= 0) continue;
            if (bearAttackCooldowns != null && bearAttackCooldowns[i] > 0f)
                bearAttackCooldowns[i] -= dt;

            float dx = px - bearPosX[i];
            float dz = pz - bearPosY[i];
            float distSq = dx * dx + dz * dz;

            if (distSq < 15f * 15f)
            {
                float dist = Mathf.Sqrt(distSq);
                if (dist > 0.05f)
                {
                    bearVelX[i] = (dx / dist) * 2.5f;
                    bearVelY[i] = (dz / dist) * 2.5f;
                    bearPosX[i] += bearVelX[i] * dt;
                    bearPosY[i] += bearVelY[i] * dt;
                }

                if (dist < 2.25f && (bearAttackCooldowns == null || bearAttackCooldowns[i] <= 0f))
                {
                    Vector3 bearPos = new Vector3(bearPosX[i], TerrainMeshGenerator.GetTerrainHeight(bearPosX[i], bearPosY[i]), bearPosY[i]);
                    if (playerControllerCache != null && playerControllerCache.IsInvulnerable)
                    {
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFDD>ESQUIVA PERFEITA!</color>");
                        if (bearAttackCooldowns != null) bearAttackCooldowns[i] = 1.6f;
                        continue;
                    }

                    if (playerStatusCache != null)
                    {
                        float hpBefore = playerStatusCache.health;
                        playerStatusCache.TakeDamage(26f);
                        if (playerStatusCache.health < hpBefore - 1f)
                        {
                            if (Random.value < 0.35f) playerStatusCache.ApplyFracture();
                            if (Random.value < 0.25f) playerStatusCache.ApplyBleeding();
                        }
                    }
                    if (playerControllerCache != null)
                    {
                        Vector3 kb = (playerTransform.position - bearPos);
                        kb.y = 0f;
                        if (kb.sqrMagnitude > 0.01f) playerControllerCache.ApplyKnockback(kb.normalized * 9.0f);
                    }
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowDamageFromDirection(bearPos);
                    if (hapticManagerCache != null) hapticManagerCache.TriggerHeavyVibration();
                    if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.ShakeMedium();
                    if (bearAttackCooldowns != null) bearAttackCooldowns[i] = 2.4f;
                }
            }
            else
            {
                bearVelX[i] = Mathf.Sin(Time.time * 0.3f + i) * 0.65f;
                bearVelY[i] = Mathf.Cos(Time.time * 0.3f + i) * 0.65f;
                bearPosX[i] += bearVelX[i] * dt;
                bearPosY[i] += bearVelY[i] * dt;
            }

            bearPosX[i] = Mathf.Clamp(bearPosX[i], minB, maxW);
            bearPosY[i] = Mathf.Clamp(bearPosY[i], minB, maxH);
        }
    }

    private void UpdateBoars(float px, float pz, float dt)
    {
        GetMapBounds(out float minB, out float maxW, out float maxH);
        for (int i = 0; i < maxBoars; i++)
        {
            if (boarHP[i] <= 0) continue;
            if (boarAttackCooldowns != null && boarAttackCooldowns[i] > 0f)
                boarAttackCooldowns[i] -= dt;

            float dx = px - boarPosX[i];
            float dz = pz - boarPosY[i];
            float distSq = dx * dx + dz * dz;

            if (boarHP[i] < 28f && distSq < 12f * 12f)
            {
                float dist = Mathf.Max(0.05f, Mathf.Sqrt(distSq));
                boarVelX[i] = -(dx / dist) * 4.5f;
                boarVelY[i] = -(dz / dist) * 4.5f;
                boarPosX[i] += boarVelX[i] * dt;
                boarPosY[i] += boarVelY[i] * dt;
            }
            else if (distSq < 9f * 9f)
            {
                float dist = Mathf.Max(0.05f, Mathf.Sqrt(distSq));
                boarVelX[i] = (dx / dist) * 5.1f;
                boarVelY[i] = (dz / dist) * 5.1f;
                boarPosX[i] += boarVelX[i] * dt;
                boarPosY[i] += boarVelY[i] * dt;

                if (dist < 1.85f && (boarAttackCooldowns == null || boarAttackCooldowns[i] <= 0f))
                {
                    Vector3 boarPos = new Vector3(boarPosX[i], TerrainMeshGenerator.GetTerrainHeight(boarPosX[i], boarPosY[i]), boarPosY[i]);
                    if (playerControllerCache != null && playerControllerCache.IsInvulnerable)
                    {
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFDD>ESQUIVA PERFEITA!</color>");
                        if (boarAttackCooldowns != null) boarAttackCooldowns[i] = 1.5f;
                        continue;
                    }

                    if (playerStatusCache != null)
                    {
                        float hpBefore = playerStatusCache.health;
                        playerStatusCache.TakeDamage(14f);
                        if (playerStatusCache.health < hpBefore - 1f && Random.value < 0.22f)
                        {
                            playerStatusCache.ApplyFracture();
                        }
                    }
                    if (playerControllerCache != null)
                    {
                        Vector3 kb = (playerTransform.position - boarPos);
                        kb.y = 0f;
                        if (kb.sqrMagnitude > 0.01f) playerControllerCache.ApplyKnockback(kb.normalized * 6.2f);
                    }
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowDamageFromDirection(boarPos);
                    if (hapticManagerCache != null) hapticManagerCache.TriggerLightVibration();
                    if (boarAttackCooldowns != null) boarAttackCooldowns[i] = 2.6f;
                }
            }
            else
            {
                boarVelX[i] = Mathf.Sin(Time.time * 0.4f + i * 2f) * 1.2f;
                boarVelY[i] = Mathf.Cos(Time.time * 0.4f + i * 2f) * 1.2f;
                boarPosX[i] += boarVelX[i] * dt;
                boarPosY[i] += boarVelY[i] * dt;
            }

            boarPosX[i] = Mathf.Clamp(boarPosX[i], minB, maxW);
            boarPosY[i] = Mathf.Clamp(boarPosY[i], minB, maxH);
        }
    }

    private void UpdateRabbits(float px, float pz, float dt)
    {
        GetMapBounds(out float minB, out float maxW, out float maxH);
        for (int i = 0; i < maxRabbits; i++)
        {
            if (rabbitPosX[i] < 0) continue;

            float dx = rabbitPosX[i] - px;
            float dz = rabbitPosY[i] - pz;
            float distSq = dx * dx + dz * dz;

            if (distSq < 10f * 10f)
            {
                float dist = Mathf.Sqrt(distSq);
                if (dist > 0.01f)
                {
                    rabbitVelX[i] = (dx / dist) * 4.2f;
                    rabbitVelY[i] = (dz / dist) * 4.2f;
                    rabbitPosX[i] += rabbitVelX[i] * dt;
                    rabbitPosY[i] += rabbitVelY[i] * dt;
                }
            }
            else
            {
                rabbitVelX[i] = Mathf.Sin(Time.time * 0.7f + i) * 0.8f;
                rabbitVelY[i] = Mathf.Cos(Time.time * 0.7f + i) * 0.8f;
                rabbitPosX[i] += rabbitVelX[i] * dt;
                rabbitPosY[i] += rabbitVelY[i] * dt;
            }

            rabbitPosX[i] = Mathf.Clamp(rabbitPosX[i], minB, maxW);
            rabbitPosY[i] = Mathf.Clamp(rabbitPosY[i], minB, maxH);
        }
    }

    public void HuntDeer(int deerIndex)
    {
        if (deerIndex < 0 || deerIndex >= maxDeer) return;

        float gy = TerrainMeshGenerator.GetTerrainHeight(deerPosX[deerIndex], deerPosY[deerIndex]);
        Vector3 dropPos = new Vector3(deerPosX[deerIndex], gy, deerPosY[deerIndex]);

        SpawnCarcass("Cervo", dropPos, new Color(0.55f, 0.35f, 0.18f), new Vector3(0.75f, 0.50f, 1.35f), 104, 2, 105, 2, 2);

        GetMapBounds(out float minB, out float maxW, out float maxH);
        deerPosX[deerIndex] = Random.Range(minB, maxW);
        deerPosY[deerIndex] = Random.Range(minB, maxH);
        deerVelX[deerIndex] = 0f;
        deerVelY[deerIndex] = 0f;
        deerHP[deerIndex] = 50f;
    }

    public void TryHuntNearby()
    {
        if (playerTransform == null) return;

        float px = playerTransform.position.x;
        float pz = playerTransform.position.z;
        float huntRange = 2.5f;

        for (int i = 0; i < maxDeer; i++)
        {
            float dx = deerPosX[i] - px;
            float dy = deerPosY[i] - pz;

            if (dx * dx + dy * dy < huntRange * huntRange)
            {
                HuntDeer(i);
                return;
            }
        }
    }

    public bool TryDamageFaunaInRay(Ray ray, float maxRange, float radius, float damage, out Vector3 hitPos, out bool killed)
    {
        hitPos = Vector3.zero;
        killed = false;

        int bestKind = -1; // 0=deer, 1=wolf, 2=bear, 3=boar, 4=rabbit
        int bestIdx = -1;
        float bestProj = maxRange + 0.4f;
        Vector3 bestHitPos = Vector3.zero;

        void TestArray(float[] arrX, float[] arrZ, int count, int kind, float bodyRadius, float centerYOffset, float[] hpArr)
        {
            for (int i = 0; i < count; i++)
            {
                if (arrX[i] < 0f) continue;
                if (hpArr != null && hpArr[i] <= 0f) continue;

                float gy = TerrainMeshGenerator.GetTerrainHeight(arrX[i], arrZ[i]);
                Vector3 center = new Vector3(arrX[i], gy + centerYOffset, arrZ[i]);
                Vector3 toCenter = center - ray.origin;
                float proj = Vector3.Dot(toCenter, ray.direction);
                if (proj < -0.15f || proj > maxRange + 0.4f) continue;

                Vector3 closest = ray.origin + ray.direction * Mathf.Max(0f, proj);
                float dx = center.x - closest.x;
                float dz = center.z - closest.z;
                float dy = Mathf.Abs(center.y - closest.y);
                float thr = radius + bodyRadius;

                if (dx * dx + dz * dz <= thr * thr && dy <= bodyRadius + 0.65f)
                {
                    if (proj < bestProj)
                    {
                        bestProj = proj;
                        bestKind = kind;
                        bestIdx = i;
                        bestHitPos = center;
                    }
                }
            }
        }

        TestArray(deerPosX, deerPosY, maxDeer, 0, 0.72f, 0.68f, deerHP);
        TestArray(wolfPosX, wolfPosY, maxWolves, 1, 0.62f, 0.55f, wolfHP);
        TestArray(bearPosX, bearPosY, maxBears, 2, 0.95f, 0.85f, bearHP);
        TestArray(boarPosX, boarPosY, maxBoars, 3, 0.65f, 0.48f, boarHP);
        TestArray(rabbitPosX, rabbitPosY, maxRabbits, 4, 0.42f, 0.25f, null);

        if (bestIdx < 0) return false;
        hitPos = bestHitPos;

        killed = ApplyDamageToAnimal(bestKind, bestIdx, damage, hitPos);
        return true;
    }

    public bool TryDamageFaunaAt(Vector3 hitPoint, float damage)
    {
        return TryDamageFaunaAt(hitPoint, damage, out _);
    }

    public bool TryDamageFaunaAt(Vector3 hitPoint, float damage, out bool killed)
    {
        killed = false;

        // 1. Cervos
        for (int i = 0; i < maxDeer; i++)
        {
            float dx = deerPosX[i] - hitPoint.x;
            float dy = deerPosY[i] - hitPoint.z;
            if (dx * dx + dy * dy < 4.2f)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(deerPosX[i], deerPosY[i]);
                killed = ApplyDamageToAnimal(0, i, damage, new Vector3(deerPosX[i], gy + 0.6f, deerPosY[i]));
                return true;
            }
        }

        // 2. Lobos
        for (int i = 0; i < maxWolves; i++)
        {
            float dx = wolfPosX[i] - hitPoint.x;
            float dy = wolfPosY[i] - hitPoint.z;
            if (dx * dx + dy * dy < 4.2f)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(wolfPosX[i], wolfPosY[i]);
                killed = ApplyDamageToAnimal(1, i, damage, new Vector3(wolfPosX[i], gy + 0.5f, wolfPosY[i]));
                return true;
            }
        }

        // 3. Ursos
        for (int i = 0; i < maxBears; i++)
        {
            if (bearHP[i] <= 0) continue;
            float dx = bearPosX[i] - hitPoint.x;
            float dy = bearPosY[i] - hitPoint.z;
            if (dx * dx + dy * dy < 6.2f)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(bearPosX[i], bearPosY[i]);
                killed = ApplyDamageToAnimal(2, i, damage, new Vector3(bearPosX[i], gy + 0.7f, bearPosY[i]));
                return true;
            }
        }

        // 4. Javalis
        for (int i = 0; i < maxBoars; i++)
        {
            if (boarHP[i] <= 0) continue;
            float dx = boarPosX[i] - hitPoint.x;
            float dy = boarPosY[i] - hitPoint.z;
            if (dx * dx + dy * dy < 4.8f)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(boarPosX[i], boarPosY[i]);
                killed = ApplyDamageToAnimal(3, i, damage, new Vector3(boarPosX[i], gy + 0.45f, boarPosY[i]));
                return true;
            }
        }

        // 5. Coelhos
        for (int i = 0; i < maxRabbits; i++)
        {
            if (rabbitPosX[i] < 0) continue;
            float dx = rabbitPosX[i] - hitPoint.x;
            float dy = rabbitPosY[i] - hitPoint.z;
            if (dx * dx + dy * dy < 3.0f)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(rabbitPosX[i], rabbitPosY[i]);
                killed = ApplyDamageToAnimal(4, i, damage, new Vector3(rabbitPosX[i], gy + 0.25f, rabbitPosY[i]));
                return true;
            }
        }

        return false;
    }

    private bool ApplyDamageToAnimal(int kind, int i, float damage, Vector3 hitPos)
    {
        if (VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.PlayBloodSplashVFX(hitPos, Vector3.up);

        GetMapBounds(out float minB, out float maxW, out float maxH);

        switch (kind)
        {
            case 0: // Cervo
                deerHP[i] -= damage;
                if (deerAnims[i] != null) deerAnims[i].TriggerHitFlash();
                if (deerHP[i] <= 0f)
                {
                    HuntDeer(i);
                    return true;
                }
                deerStates[i] = 1; // Foge ao ser ferido
                return false;

            case 1: // Lobo
                wolfHP[i] -= damage;
                if (wolfAnims[i] != null) wolfAnims[i].TriggerHitFlash();
                if (wolfHP[i] <= 0f)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(wolfPosX[i], wolfPosY[i]);
                    Vector3 pos = new Vector3(wolfPosX[i], gy, wolfPosY[i]);
                    SpawnCarcass("Lobo", pos, new Color(0.28f, 0.28f, 0.30f), new Vector3(0.55f, 0.40f, 1.15f), 104, 1, 105, 2, 2);
                    wolfPosX[i] = Random.Range(minB, maxW);
                    wolfPosY[i] = Random.Range(minB, maxH);
                    wolfHP[i] = 65f;
                    return true;
                }
                return false;

            case 2: // Urso
                bearHP[i] -= damage;
                if (bearAnims[i] != null) bearAnims[i].TriggerHitFlash();
                if (bearHP[i] <= 0f)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(bearPosX[i], bearPosY[i]);
                    Vector3 pos = new Vector3(bearPosX[i], gy, bearPosY[i]);
                    SpawnCarcass("Urso Pardo", pos, new Color(0.22f, 0.14f, 0.08f), new Vector3(1.05f, 0.72f, 1.85f), 104, 5, 105, 3, 4, 115);
                    bearPosX[i] = -1000f;
                    return true;
                }
                return false;

            case 3: // Javali
                boarHP[i] -= damage;
                if (boarAnims[i] != null) boarAnims[i].TriggerHitFlash();
                if (boarHP[i] <= 0f)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(boarPosX[i], boarPosY[i]);
                    Vector3 pos = new Vector3(boarPosX[i], gy, boarPosY[i]);
                    SpawnCarcass("Javali", pos, new Color(0.25f, 0.20f, 0.15f), new Vector3(0.65f, 0.45f, 1.15f), 104, 3, 105, 2, 2);
                    boarPosX[i] = Random.Range(minB, maxW);
                    boarPosY[i] = Random.Range(minB, maxH);
                    boarHP[i] = 60f;
                    return true;
                }
                return false;

            case 4: // Coelho
                float rGy = TerrainMeshGenerator.GetTerrainHeight(rabbitPosX[i], rabbitPosY[i]);
                if (LootManager.Instance != null)
                {
                    Vector3 dropAt = new Vector3(rabbitPosX[i], rGy + 0.15f, rabbitPosY[i]);
                    LootManager.Instance.DropItem(113, 1, dropAt);
                    if (Random.value < 0.5f) LootManager.Instance.DropItem(120, 1, dropAt);
                }
                rabbitPosX[i] = Random.Range(minB, maxW);
                rabbitPosY[i] = Random.Range(minB, maxH);
                return true;
        }
        return false;
    }

    private void SpawnCarcass(string displayName, Vector3 worldPos, Color hideColor, Vector3 size, int meatId, int meatAmt, int hideId, int hideAmt, int boneAmt = 2, int bonusId = -1)
    {
        GameObject carcassGo = new GameObject($"Carcass_{displayName}");
        carcassGo.transform.position = worldPos + Vector3.up * (size.y * 0.22f);
        carcassGo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 82f);

        if (cachedCarcassMat == null)
        {
            cachedCarcassMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            cachedCarcassMat.SetFloat("_Smoothness", 0.18f);
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "CarcassBody";
        body.transform.SetParent(carcassGo.transform, false);
        body.transform.localScale = new Vector3(size.x, size.z * 0.5f, size.y);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Collider childCol = body.GetComponent<Collider>();
        if (childCol != null) Destroy(childCol);

        Renderer r = body.GetComponent<Renderer>();
        r.sharedMaterial = cachedCarcassMat;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", hideColor * 0.78f);
        mpb.SetColor("_Color", hideColor * 0.78f);
        r.SetPropertyBlock(mpb);

        BoxCollider box = carcassGo.AddComponent<BoxCollider>();
        box.size = new Vector3(Mathf.Max(1.0f, size.x * 1.4f), Mathf.Max(0.75f, size.y * 1.3f), Mathf.Max(1.2f, size.z * 1.15f));
        box.center = Vector3.zero;

        SkinningCarcass skin = carcassGo.AddComponent<SkinningCarcass>();
        skin.animalDisplayName = displayName;
        skin.meatItemId = meatId;
        skin.meatAmount = meatAmt;
        skin.hideItemId = hideId;
        skin.hideAmount = hideAmt;
        skin.boneItemId = 120;
        skin.boneAmount = boneAmt;
        skin.bonusDropId = bonusId;
    }

    private void UpdateRenderTransforms()
    {
        float px = playerTransform.position.x;
        float pz = playerTransform.position.z;
        float cullDist = GalicianOptimizationManager.Instance != null ? GalicianOptimizationManager.Instance.wildlifeCullDist : 70f;
        float cullDistSq = cullDist * cullDist;

        for (int i = 0; i < maxDeer; i++)
        {
            if (deerInstances[i] != null)
            {
                float distSq = (px - deerPosX[i]) * (px - deerPosX[i]) + (pz - deerPosY[i]) * (pz - deerPosY[i]);
                if (distSq > cullDistSq)
                {
                    deerInstances[i].SetActive(false);
                    continue;
                }
                deerInstances[i].SetActive(true);
                float gy = TerrainMeshGenerator.GetTerrainHeight(deerPosX[i], deerPosY[i]);
                deerInstances[i].transform.position = new Vector3(deerPosX[i], gy, deerPosY[i]);

                Vector3 moveDir = new Vector3(deerVelX[i], 0, deerVelY[i]);
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                    deerInstances[i].transform.rotation = Quaternion.Slerp(deerInstances[i].transform.rotation, targetRot, Time.deltaTime * 6f);
                }
                if (deerAnims[i] != null)
                {
                    deerAnims[i].SetAnimState(deerStates[i] == 1 ? AnimalProceduralAnimator.AnimState.Flee :
                        (moveDir.sqrMagnitude > 0.02f ? AnimalProceduralAnimator.AnimState.Move : AnimalProceduralAnimator.AnimState.Idle));
                }
            }
        }

        for (int i = 0; i < maxWolves; i++)
        {
            if (wolfInstances[i] != null)
            {
                float distSq = (px - wolfPosX[i]) * (px - wolfPosX[i]) + (pz - wolfPosY[i]) * (pz - wolfPosY[i]);
                if (distSq > cullDistSq)
                {
                    wolfInstances[i].SetActive(false);
                    continue;
                }
                wolfInstances[i].SetActive(true);
                float gy = TerrainMeshGenerator.GetTerrainHeight(wolfPosX[i], wolfPosY[i]);
                wolfInstances[i].transform.position = new Vector3(wolfPosX[i], gy, wolfPosY[i]);

                Vector3 moveDir = new Vector3(wolfVelX[i], 0, wolfVelY[i]);
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                    wolfInstances[i].transform.rotation = Quaternion.Slerp(wolfInstances[i].transform.rotation, targetRot, Time.deltaTime * 6f);
                }
                if (wolfAnims[i] != null)
                {
                    var targetState = (wolfStates[i] == 1 && distSq < 16f)
                        ? AnimalProceduralAnimator.AnimState.TelegraphAttack
                        : (moveDir.sqrMagnitude > 0.02f ? AnimalProceduralAnimator.AnimState.Move : AnimalProceduralAnimator.AnimState.Idle);
                    wolfAnims[i].SetAnimState(targetState);
                }
            }
        }

        for (int i = 0; i < maxBears; i++)
        {
            if (bearInstances[i] != null)
            {
                float distSq = (px - bearPosX[i]) * (px - bearPosX[i]) + (pz - bearPosY[i]) * (pz - bearPosY[i]);
                bool isAlive = bearHP[i] > 0 && bearPosX[i] > 0;
                bearInstances[i].SetActive(isAlive && distSq <= cullDistSq);
                if (isAlive && distSq <= cullDistSq)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(bearPosX[i], bearPosY[i]);
                    bearInstances[i].transform.position = new Vector3(bearPosX[i], gy, bearPosY[i]);

                    Vector3 moveDir = new Vector3(bearVelX[i], 0, bearVelY[i]);
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                        bearInstances[i].transform.rotation = Quaternion.Slerp(bearInstances[i].transform.rotation, targetRot, Time.deltaTime * 5f);
                    }
                    if (bearAnims[i] != null)
                    {
                        var targetState = distSq < 14f
                            ? AnimalProceduralAnimator.AnimState.TelegraphAttack
                            : (moveDir.sqrMagnitude > 0.02f ? AnimalProceduralAnimator.AnimState.Move : AnimalProceduralAnimator.AnimState.Idle);
                        bearAnims[i].SetAnimState(targetState);
                    }
                }
            }
        }

        for (int i = 0; i < maxBoars; i++)
        {
            if (boarInstances[i] != null)
            {
                float distSq = (px - boarPosX[i]) * (px - boarPosX[i]) + (pz - boarPosY[i]) * (pz - boarPosY[i]);
                bool isAlive = boarHP[i] > 0;
                boarInstances[i].SetActive(isAlive && distSq <= cullDistSq);
                if (isAlive && distSq <= cullDistSq)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(boarPosX[i], boarPosY[i]);
                    boarInstances[i].transform.position = new Vector3(boarPosX[i], gy, boarPosY[i]);

                    Vector3 moveDir = new Vector3(boarVelX[i], 0, boarVelY[i]);
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                        boarInstances[i].transform.rotation = Quaternion.Slerp(boarInstances[i].transform.rotation, targetRot, Time.deltaTime * 6f);
                    }
                    if (boarAnims[i] != null)
                    {
                        boarAnims[i].SetAnimState(distSq < 64f ? AnimalProceduralAnimator.AnimState.Flee :
                            (moveDir.sqrMagnitude > 0.02f ? AnimalProceduralAnimator.AnimState.Move : AnimalProceduralAnimator.AnimState.Idle));
                    }
                }
            }
        }

        for (int i = 0; i < maxRabbits; i++)
        {
            if (rabbitInstances[i] != null)
            {
                float distSq = (px - rabbitPosX[i]) * (px - rabbitPosX[i]) + (pz - rabbitPosY[i]) * (pz - rabbitPosY[i]);
                bool isAlive = rabbitPosX[i] >= 0;
                rabbitInstances[i].SetActive(isAlive && distSq <= cullDistSq);
                if (isAlive && distSq <= cullDistSq)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(rabbitPosX[i], rabbitPosY[i]);
                    rabbitInstances[i].transform.position = new Vector3(rabbitPosX[i], gy, rabbitPosY[i]);

                    Vector3 moveDir = new Vector3(rabbitVelX[i], 0, rabbitVelY[i]);
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                        rabbitInstances[i].transform.rotation = Quaternion.Slerp(rabbitInstances[i].transform.rotation, targetRot, Time.deltaTime * 8f);
                    }
                    if (rabbitAnims[i] != null)
                    {
                        rabbitAnims[i].SetAnimState(distSq < 100f ? AnimalProceduralAnimator.AnimState.Flee :
                            (moveDir.sqrMagnitude > 0.02f ? AnimalProceduralAnimator.AnimState.Move : AnimalProceduralAnimator.AnimState.Idle));
                    }
                }
            }
        }
    }

    public void CatchRabbit(int index)
    {
        if (index >= 0 && index < maxRabbits)
        {
            float gy = TerrainMeshGenerator.GetTerrainHeight(rabbitPosX[index], rabbitPosY[index]);
            if (LootManager.Instance != null)
            {
                Vector3 dropAt = new Vector3(rabbitPosX[index], gy + 0.2f, rabbitPosY[index]);
                LootManager.Instance.DropItem(113, 1, dropAt);
                if (Random.value < 0.5f) LootManager.Instance.DropItem(120, 1, dropAt);
            }
            GetMapBounds(out float minB, out float maxW, out float maxH);
            rabbitPosX[index] = Random.Range(minB, maxW);
            rabbitPosY[index] = Random.Range(minB, maxH);
        }
    }

    public float[] GetRabbitPosX() { return rabbitPosX; }
    public float[] GetRabbitPosY() { return rabbitPosY; }

    private void OnDestroy()
    {
        if (deerPxH.IsAllocated) deerPxH.Free();
        if (deerPyH.IsAllocated) deerPyH.Free();
        if (deerVxH.IsAllocated) deerVxH.Free();
        if (deerVyH.IsAllocated) deerVyH.Free();
        if (deerSH.IsAllocated) deerSH.Free();

        if (wolfPxH.IsAllocated) wolfPxH.Free();
        if (wolfPyH.IsAllocated) wolfPyH.Free();
        if (wolfVxH.IsAllocated) wolfVxH.Free();
        if (wolfVyH.IsAllocated) wolfVyH.Free();
        if (wolfSH.IsAllocated) wolfSH.Free();
    }

    private GameObject CreateProceduralAnimalFallback(string name, Color color, Vector3 size, AnimalProceduralAnimator.LocomotionStyle style)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = size;
        body.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = color;
        body.GetComponent<Renderer>().sharedMaterial = mat;

        AnimalProceduralAnimator anim = go.AddComponent<AnimalProceduralAnimator>();
        anim.locomotionStyle = style;
        anim.bodyTransform = body.transform;

        return go;
    }
}
