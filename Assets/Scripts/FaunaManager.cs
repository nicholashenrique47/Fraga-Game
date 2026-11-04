using UnityEngine;
using System.Runtime.InteropServices;

/// FaunaManager: Gere a fauna (cervos, lobos, ursos, javalis e coelhos) via IA nativa e simulação procedural.
/// Renderiza animais vivos sobre o relevo do terreno procedural, com animações e quedas de presas.
public class FaunaManager : MonoBehaviour
{
    // P/Invoke para IA nativa
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
    public int maxDeer = 14;
    public int maxWolves = 6;
    public float deerWanderSpeed = 1.5f;
    public float deerFleeSpeed = 6f;
    public float deerDetectionRadius = 9f;
    public float wolfHuntSpeed = 4.2f;
    public float wolfAttackRange = 1.6f;
    public float wolfDamage = 6f; // Dano moderado na sanidade/vida

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
    private float[] deerPosX, deerPosY, deerVelX, deerVelY;
    private int[] deerStates;
    private GCHandle deerPxH, deerPyH, deerVxH, deerVyH, deerSH;

    // Wolf SoA
    private float[] wolfPosX, wolfPosY, wolfVelX, wolfVelY;
    private int[] wolfStates;
    private GCHandle wolfPxH, wolfPyH, wolfVxH, wolfVyH, wolfSH;

    private GameObject[] deerInstances;
    private GameObject[] wolfInstances;
    private GameObject[] bearInstances;
    private GameObject[] boarInstances;
    private GameObject[] rabbitInstances;

    // Cooldown de ataque dos lobos
    private float[] wolfAttackCooldowns;
    
    // Ursos
    public int maxBears = 3;
    private float[] bearPosX, bearPosY;
    private float[] bearHP;

    // Javalis (Wild Boars)
    public int maxBoars = 5;
    private float[] boarPosX, boarPosY;
    private float[] boarHP;
    private float[] boarVelX, boarVelY;
    
    // Coelhos
    public int maxRabbits = 14;
    private float[] rabbitPosX, rabbitPosY;

    // Caches
    private TrapSystem trapSystemCache;
    private HapticManager hapticManagerCache;

    private void Awake()
    {
        trapSystemCache = FindAnyObjectByType<TrapSystem>();
        hapticManagerCache = FindAnyObjectByType<HapticManager>();

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

        // Alocar e fixar arrays de cervos
        deerPosX = new float[maxDeer];
        deerPosY = new float[maxDeer];
        deerVelX = new float[maxDeer];
        deerVelY = new float[maxDeer];
        deerStates = new int[maxDeer];

        deerPxH = GCHandle.Alloc(deerPosX, GCHandleType.Pinned);
        deerPyH = GCHandle.Alloc(deerPosY, GCHandleType.Pinned);
        deerVxH = GCHandle.Alloc(deerVelX, GCHandleType.Pinned);
        deerVyH = GCHandle.Alloc(deerVelY, GCHandleType.Pinned);
        deerSH = GCHandle.Alloc(deerStates, GCHandleType.Pinned);

        // Alocar e fixar arrays de lobos
        wolfPosX = new float[maxWolves];
        wolfPosY = new float[maxWolves];
        wolfVelX = new float[maxWolves];
        wolfVelY = new float[maxWolves];
        wolfStates = new int[maxWolves];

        wolfPxH = GCHandle.Alloc(wolfPosX, GCHandleType.Pinned);
        wolfPyH = GCHandle.Alloc(wolfPosY, GCHandleType.Pinned);
        wolfVxH = GCHandle.Alloc(wolfVelX, GCHandleType.Pinned);
        wolfVyH = GCHandle.Alloc(wolfVelY, GCHandleType.Pinned);
        wolfSH = GCHandle.Alloc(wolfStates, GCHandleType.Pinned);

        // Instancias Visuais
        deerInstances = new GameObject[maxDeer];
        wolfInstances = new GameObject[maxWolves];
        wolfAttackCooldowns = new float[maxWolves];
        
        bearInstances = new GameObject[maxBears];
        bearPosX = new float[maxBears];
        bearPosY = new float[maxBears];
        bearHP = new float[maxBears];

        boarInstances = new GameObject[maxBoars];
        boarPosX = new float[maxBoars];
        boarPosY = new float[maxBoars];
        boarHP = new float[maxBoars];
        boarVelX = new float[maxBoars];
        boarVelY = new float[maxBoars];
        
        rabbitInstances = new GameObject[maxRabbits];
        rabbitPosX = new float[maxRabbits];
        rabbitPosY = new float[maxRabbits];

        SpawnInitialFauna();
    }

    private void SpawnInitialFauna()
    {
        GameObject faunaParent = new GameObject("Fauna_Instances");

        float pX = (playerTransform != null) ? playerTransform.position.x : 50f;
        float pZ = (playerTransform != null) ? playerTransform.position.z : 50f;

        // 1. Spawn cervos (Garante que os 3 primeiros fiquem em raio visivel inicial de 18m a 35m)
        for (int i = 0; i < maxDeer; i++)
        {
            if (i < 3)
            {
                float angle = i * 2.1f;
                float r = Random.Range(18f, 32f);
                deerPosX[i] = Mathf.Clamp(pX + Mathf.Cos(angle) * r, 5f, 95f);
                deerPosY[i] = Mathf.Clamp(pZ + Mathf.Sin(angle) * r, 5f, 95f);
            }
            else
            {
                deerPosX[i] = Random.Range(10f, 90f);
                deerPosY[i] = Random.Range(10f, 90f);
            }

            if (deerPrefab != null) 
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(deerPosX[i], deerPosY[i]);
                deerInstances[i] = Instantiate(deerPrefab, new Vector3(deerPosX[i], gy, deerPosY[i]), Quaternion.identity, faunaParent.transform);
                deerInstances[i].SetActive(true);
            }
        }

        // 2. Spawn lobos nos arredores
        for (int i = 0; i < maxWolves; i++)
        {
            float angle = (float)i / maxWolves * Mathf.PI * 2f;
            wolfPosX[i] = Mathf.Clamp(pX + Mathf.Cos(angle) * 42f, 5f, 95f);
            wolfPosY[i] = Mathf.Clamp(pZ + Mathf.Sin(angle) * 42f, 5f, 95f);
            if (wolfPrefab != null) 
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(wolfPosX[i], wolfPosY[i]);
                wolfInstances[i] = Instantiate(wolfPrefab, new Vector3(wolfPosX[i], gy, wolfPosY[i]), Quaternion.identity, faunaParent.transform);
                wolfInstances[i].SetActive(true);
            }
        }
        
        // 3. Spawn Ursos
        for (int i = 0; i < maxBears; i++)
        {
            bearPosX[i] = Random.Range(20f, 80f);
            bearPosY[i] = Random.Range(20f, 80f);
            bearHP[i] = 100f;
            
            if (bearPrefab != null)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(bearPosX[i], bearPosY[i]);
                bearInstances[i] = Instantiate(bearPrefab, new Vector3(bearPosX[i], gy, bearPosY[i]), Quaternion.identity, faunaParent.transform);
                bearInstances[i].transform.localScale = Vector3.one * 1.4f;
                bearInstances[i].SetActive(true);
            }
        }

        // 4. Spawn Javalis (Wild Boars)
        for (int i = 0; i < maxBoars; i++)
        {
            if (i < 2)
            {
                float angle = i * 3.4f;
                float r = Random.Range(22f, 38f);
                boarPosX[i] = Mathf.Clamp(pX + Mathf.Cos(angle) * r, 5f, 95f);
                boarPosY[i] = Mathf.Clamp(pZ + Mathf.Sin(angle) * r, 5f, 95f);
            }
            else
            {
                boarPosX[i] = Random.Range(15f, 85f);
                boarPosY[i] = Random.Range(15f, 85f);
            }
            boarHP[i] = 60f;

            if (boarPrefab != null)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(boarPosX[i], boarPosY[i]);
                boarInstances[i] = Instantiate(boarPrefab, new Vector3(boarPosX[i], gy, boarPosY[i]), Quaternion.identity, faunaParent.transform);
                boarInstances[i].SetActive(true);
            }
        }
        
        // 5. Spawn Coelhos (Presença constante nas imediações)
        for (int i = 0; i < maxRabbits; i++)
        {
            if (i < 4)
            {
                float angle = i * 1.57f;
                float r = Random.Range(12f, 26f);
                rabbitPosX[i] = Mathf.Clamp(pX + Mathf.Cos(angle) * r, 5f, 95f);
                rabbitPosY[i] = Mathf.Clamp(pZ + Mathf.Sin(angle) * r, 5f, 95f);
            }
            else
            {
                rabbitPosX[i] = Random.Range(10f, 90f);
                rabbitPosY[i] = Random.Range(10f, 90f);
            }

            if (rabbitPrefab != null)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(rabbitPosX[i], rabbitPosY[i]);
                rabbitInstances[i] = Instantiate(rabbitPrefab, new Vector3(rabbitPosX[i], gy, rabbitPosY[i]), Quaternion.identity, faunaParent.transform);
                rabbitInstances[i].SetActive(true);
            }
        }
    }

    private void Update()
    {
        if (playerTransform == null || resourceManager == null) return;

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

        // Chamar IA nativa para cervos
        UpdateDeer(
            deerPxH.AddrOfPinnedObject(), deerPyH.AddrOfPinnedObject(),
            deerVxH.AddrOfPinnedObject(), deerVyH.AddrOfPinnedObject(),
            deerSH.AddrOfPinnedObject(), maxDeer,
            px, pz,
            wolfPxH.AddrOfPinnedObject(), wolfPyH.AddrOfPinnedObject(), maxWolves,
            dt, deerWanderSpeed, deerFleeSpeed, deerDetectionRadius,
            mapData, mw, mh);

        // Chamar IA nativa para lobos
        UpdateWolves(
            wolfPxH.AddrOfPinnedObject(), wolfPyH.AddrOfPinnedObject(),
            wolfVxH.AddrOfPinnedObject(), wolfVyH.AddrOfPinnedObject(),
            wolfSH.AddrOfPinnedObject(), maxWolves,
            px, pz, lightR,
            deerPxH.AddrOfPinnedObject(), deerPyH.AddrOfPinnedObject(), maxDeer,
            dt, wolfHuntSpeed, isNight,
            mapData, mw, mh);

        // Verificar ataques de lobos ao jogador
        CheckWolfAttacks(px, pz, dt);
        
        if (trapSystemCache != null)
        {
            trapSystemCache.CheckTrapsForWolves(wolfPosX, wolfPosY, maxWolves);
        }

        // Atualizar Ursos e Javalis
        UpdateBears(px, pz, dt);
        UpdateBoars(px, pz, dt);
        UpdateRabbits(px, pz, dt);

        // Atualizar posições e animações visuais com snap ao relevo do terreno
        UpdateRenderTransforms();
    }

    private void CheckWolfAttacks(float px, float pz, float dt)
    {
        float attackRangeSq = wolfAttackRange * wolfAttackRange;

        for (int i = 0; i < maxWolves; i++)
        {
            wolfAttackCooldowns[i] -= dt;

            if (wolfStates[i] == 1) // Hunting
            {
                float dx = wolfPosX[i] - px;
                float dy = wolfPosY[i] - pz;
                float distSq = dx * dx + dy * dy;

                if (distSq < attackRangeSq && wolfAttackCooldowns[i] <= 0f)
                {
                    if (playerSanity != null)
                    {
                        playerSanity.sanity -= wolfDamage;
                        Debug.Log("[Fauna] Lobo atacou o jogador!");

                        if (hapticManagerCache != null) hapticManagerCache.TriggerHeavyVibration();
                    }
                    wolfAttackCooldowns[i] = 3.5f;
                }
            }
        }
    }
    
    private void UpdateBears(float px, float pz, float dt)
    {
        for (int i = 0; i < maxBears; i++)
        {
            if (bearHP[i] <= 0) continue;
            
            float dx = px - bearPosX[i];
            float dz = pz - bearPosY[i];
            float distSq = dx * dx + dz * dz;
            
            if (distSq < 15f * 15f) // Aggro
            {
                float dist = Mathf.Sqrt(distSq);
                bearPosX[i] += (dx / dist) * 2.5f * dt;
                bearPosY[i] += (dz / dist) * 2.5f * dt;
                
                if (dist < 2.0f)
                {
                    if (playerSanity != null)
                    {
                        PlayerStatus ps = playerTransform.GetComponent<PlayerStatus>();
                        if (ps != null) ps.TakeDamagePassive(10f * dt);
                        
                        if (hapticManagerCache != null && Time.frameCount % 30 == 0) hapticManagerCache.TriggerHeavyVibration();
                    }
                }
            }
            else
            {
                // Wander sutil
                bearPosX[i] += Mathf.Sin(Time.time * 0.3f + i) * 0.4f * dt;
                bearPosY[i] += Mathf.Cos(Time.time * 0.3f + i) * 0.4f * dt;
            }
        }
    }

    private void UpdateBoars(float px, float pz, float dt)
    {
        for (int i = 0; i < maxBoars; i++)
        {
            if (boarHP[i] <= 0) continue;

            float dx = px - boarPosX[i];
            float dz = pz - boarPosY[i];
            float distSq = dx * dx + dz * dz;

            if (distSq < 8f * 8f) // Foge se o jogador aproximar a menos de 8m
            {
                float dist = Mathf.Sqrt(distSq);
                boarVelX[i] = -(dx / dist) * 4.5f;
                boarVelY[i] = -(dz / dist) * 4.5f;
                boarPosX[i] += boarVelX[i] * dt;
                boarPosY[i] += boarVelY[i] * dt;
            }
            else
            {
                // Pastejo tranquilo
                boarVelX[i] = Mathf.Sin(Time.time * 0.4f + i * 2f) * 1.2f;
                boarVelY[i] = Mathf.Cos(Time.time * 0.4f + i * 2f) * 1.2f;
                boarPosX[i] += boarVelX[i] * dt;
                boarPosY[i] += boarVelY[i] * dt;
            }

            boarPosX[i] = Mathf.Clamp(boarPosX[i], 2f, resourceManager.MapWidth - 3f);
            boarPosY[i] = Mathf.Clamp(boarPosY[i], 2f, resourceManager.MapHeight - 3f);
        }
    }
    
    private void UpdateRabbits(float px, float pz, float dt)
    {
        for (int i = 0; i < maxRabbits; i++)
        {
            if (rabbitPosX[i] < 0) continue;
            
            float dx = rabbitPosX[i] - px;
            float dz = rabbitPosY[i] - pz;
            float distSq = dx * dx + dz * dz;
            
            if (distSq < 10f * 10f) // Foge em ziguezague
            {
                float dist = Mathf.Sqrt(distSq);
                rabbitPosX[i] += (dx / dist) * 4.2f * dt;
                rabbitPosY[i] += (dz / dist) * 4.2f * dt;
            }
            else
            {
                rabbitPosX[i] += Mathf.Sin(Time.time * 0.7f + i) * 0.8f * dt;
                rabbitPosY[i] += Mathf.Cos(Time.time * 0.7f + i) * 0.8f * dt;
            }
            
            rabbitPosX[i] = Mathf.Clamp(rabbitPosX[i], 2f, resourceManager.MapWidth - 3f);
            rabbitPosY[i] = Mathf.Clamp(rabbitPosY[i], 2f, resourceManager.MapHeight - 3f);
        }
    }

    public void HuntDeer(int deerIndex)
    {
        if (deerIndex < 0 || deerIndex >= maxDeer) return;

        float gy = TerrainMeshGenerator.GetTerrainHeight(deerPosX[deerIndex], deerPosY[deerIndex]);
        Vector3 dropPos = new Vector3(deerPosX[deerIndex], gy + 0.2f, deerPosY[deerIndex]);
        
        if (LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(104, 2, dropPos); // 2x Carne Crua
            LootManager.Instance.DropItem(105, 1, dropPos); // 1x Pele de Animal
        }

        Debug.Log("[Fauna] Cervo caçado. Loot liberado no solo.");

        deerPosX[deerIndex] = Random.Range(10f, 90f);
        deerPosY[deerIndex] = Random.Range(10f, 90f);
        deerVelX[deerIndex] = 0f;
        deerVelY[deerIndex] = 0f;
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
        Debug.Log("[Fauna] Nenhum cervo ao alcance.");
    }

    public bool TryDamageFaunaAt(Vector3 hitPoint, float damage)
    {
        // 1. Cervos
        for (int i = 0; i < maxDeer; i++)
        {
            float dx = deerPosX[i] - hitPoint.x;
            float dy = deerPosY[i] - hitPoint.z;
            
            if (dx * dx + dy * dy < 4.5f)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(deerPosX[i], deerPosY[i]);
                HuntDeer(i);
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(deerPosX[i], gy + 0.3f, deerPosY[i]));
                return true;
            }
        }
        
        // 2. Lobos
        for (int i = 0; i < maxWolves; i++)
        {
            float dx = wolfPosX[i] - hitPoint.x;
            float dy = wolfPosY[i] - hitPoint.z;
            
            if (dx * dx + dy * dy < 4.5f)
            {
                float gy = TerrainMeshGenerator.GetTerrainHeight(wolfPosX[i], wolfPosY[i]);
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(wolfPosX[i], gy + 0.3f, wolfPosY[i]));
                if (LootManager.Instance != null)
                {
                    LootManager.Instance.DropItem(105, 1, new Vector3(wolfPosX[i], gy + 0.2f, wolfPosY[i])); // Pele de Lobo
                }
                wolfPosX[i] = Random.Range(10f, 90f);
                wolfPosY[i] = Random.Range(10f, 90f);
                Debug.LogWarning("[Fauna] Lobo abatido!");
                return true;
            }
        }
        
        // 3. Ursos
        for (int i = 0; i < maxBears; i++)
        {
            if (bearHP[i] <= 0) continue;
            
            float dx = bearPosX[i] - hitPoint.x;
            float dy = bearPosY[i] - hitPoint.z;
            
            if (dx * dx + dy * dy < 6.5f)
            {
                bearHP[i] -= damage;
                float gy = TerrainMeshGenerator.GetTerrainHeight(bearPosX[i], bearPosY[i]);
                Debug.Log($"[Fauna] Urso sofreu {damage} dano. HP: {bearHP[i]}");
                
                if (bearHP[i] <= 0)
                {
                    if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(bearPosX[i], gy + 0.5f, bearPosY[i]));
                    if (LootManager.Instance != null)
                    {
                        LootManager.Instance.DropItem(104, 5, new Vector3(bearPosX[i], gy + 0.2f, bearPosY[i])); // 5x Carne
                        LootManager.Instance.DropItem(105, 3, new Vector3(bearPosX[i], gy + 0.2f, bearPosY[i])); // 3x Pele
                    }
                    Debug.LogWarning("[Fauna] URSO ABATIDO!");
                    bearPosX[i] = -1000f; 
                }
                return true;
            }
        }

        // 4. Javalis
        for (int i = 0; i < maxBoars; i++)
        {
            if (boarHP[i] <= 0) continue;

            float dx = boarPosX[i] - hitPoint.x;
            float dy = boarPosY[i] - hitPoint.z;

            if (dx * dx + dy * dy < 5.0f)
            {
                boarHP[i] -= damage;
                float gy = TerrainMeshGenerator.GetTerrainHeight(boarPosX[i], boarPosY[i]);

                if (boarHP[i] <= 0)
                {
                    if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(boarPosX[i], gy + 0.3f, boarPosY[i]));
                    if (LootManager.Instance != null)
                    {
                        LootManager.Instance.DropItem(104, 3, new Vector3(boarPosX[i], gy + 0.2f, boarPosY[i])); // 3x Carne
                        LootManager.Instance.DropItem(105, 2, new Vector3(boarPosX[i], gy + 0.2f, boarPosY[i])); // 2x Pele
                    }
                    Debug.LogWarning("[Fauna] Javali abatido!");
                    boarPosX[i] = Random.Range(10f, 90f);
                    boarPosY[i] = Random.Range(10f, 90f);
                    boarHP[i] = 60f;
                }
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
                if (LootManager.Instance != null)
                {
                    LootManager.Instance.DropItem(113, 1, new Vector3(rabbitPosX[i], gy + 0.15f, rabbitPosY[i])); // Carne Leve
                }
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(hitPoint.x, gy + 0.2f, hitPoint.z));
                rabbitPosX[i] = Random.Range(10f, 90f);
                rabbitPosY[i] = Random.Range(10f, 90f);
                return true;
            }
        }
        
        return false;
    }

    private void UpdateRenderTransforms()
    {
        float px = playerTransform.position.x;
        float pz = playerTransform.position.z;
        float cullDistSq = 4900f; // 70 * 70 m

        // 1. Cervos com snap ao terreno
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
            }
        }

        // 2. Lobos com snap ao terreno
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
            }
        }
        
        // 3. Ursos com snap ao terreno
        for (int i = 0; i < maxBears; i++)
        {
            if (bearInstances[i] != null)
            {
                float distSq = (px - bearPosX[i]) * (px - bearPosX[i]) + (pz - bearPosY[i]) * (pz - bearPosY[i]);
                bool isAlive = bearHP[i] > 0 && bearPosX[i] > 0;
                bearInstances[i].SetActive(isAlive && distSq <= cullDistSq);
                if (isAlive)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(bearPosX[i], bearPosY[i]);
                    bearInstances[i].transform.position = new Vector3(bearPosX[i], gy, bearPosY[i]);
                }
            }
        }

        // 4. Javalis com snap ao terreno
        for (int i = 0; i < maxBoars; i++)
        {
            if (boarInstances[i] != null)
            {
                float distSq = (px - boarPosX[i]) * (px - boarPosX[i]) + (pz - boarPosY[i]) * (pz - boarPosY[i]);
                bool isAlive = boarHP[i] > 0;
                boarInstances[i].SetActive(isAlive && distSq <= cullDistSq);
                if (isAlive)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(boarPosX[i], boarPosY[i]);
                    boarInstances[i].transform.position = new Vector3(boarPosX[i], gy, boarPosY[i]);

                    Vector3 moveDir = new Vector3(boarVelX[i], 0, boarVelY[i]);
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                        boarInstances[i].transform.rotation = Quaternion.Slerp(boarInstances[i].transform.rotation, targetRot, Time.deltaTime * 6f);
                    }
                }
            }
        }
        
        // 5. Coelhos com snap ao terreno
        for (int i = 0; i < maxRabbits; i++)
        {
            if (rabbitInstances[i] != null)
            {
                float distSq = (px - rabbitPosX[i]) * (px - rabbitPosX[i]) + (pz - rabbitPosY[i]) * (pz - rabbitPosY[i]);
                bool isAlive = rabbitPosX[i] >= 0;
                rabbitInstances[i].SetActive(isAlive && distSq <= cullDistSq);
                if (isAlive)
                {
                    float gy = TerrainMeshGenerator.GetTerrainHeight(rabbitPosX[i], rabbitPosY[i]);
                    rabbitInstances[i].transform.position = new Vector3(rabbitPosX[i], gy, rabbitPosY[i]);
                }
            }
        }
    }
    
    // API para armadilhas
    public void CatchRabbit(int index)
    {
        if (index >= 0 && index < maxRabbits)
        {
            float gy = TerrainMeshGenerator.GetTerrainHeight(rabbitPosX[index], rabbitPosY[index]);
            if (LootManager.Instance != null)
            {
                LootManager.Instance.DropItem(113, 1, new Vector3(rabbitPosX[index], gy + 0.2f, rabbitPosY[index]));
            }
            rabbitPosX[index] = Random.Range(10f, 90f);
            rabbitPosY[index] = Random.Range(10f, 90f);
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