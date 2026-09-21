using UnityEngine;
using System.Runtime.InteropServices;

/// FaunaManager: Gere a fauna (cervos e lobos) via IA nativa C++.
/// Renderiza com GPU Instancing (DrawMeshInstanced). Zero GameObjects no mapa.
/// Os cervos fornecem carne e pele quando mortos. Os lobos sao predadores noturnos.
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
    public int maxDeer = 15;
    public int maxWolves = 6;
    public float deerWanderSpeed = 1.5f;
    public float deerFleeSpeed = 6f;
    public float deerDetectionRadius = 8f;
    public float wolfHuntSpeed = 4f;
    public float wolfAttackRange = 1.5f;
    public float wolfDamage = 8f; // Dano na sanidade por ataque

    [Header("Rendering (Asset Store Prefabs)")]
    public GameObject deerPrefab;
    public GameObject wolfPrefab;
    public GameObject bearPrefab;
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

    // Wolf attack cooldown
    private float[] wolfAttackCooldowns;
    
    // Bears (C# Logic for simplicity since C++ handles wolves/deer)
    public int maxBears = 2;
    private float[] bearPosX, bearPosY;
    private float[] bearHP;
    
    // Rabbits
    public int maxRabbits = 10;
    private float[] rabbitPosX, rabbitPosY;
    private GameObject[] rabbitInstances;

    // Caches
    private TrapSystem trapSystemCache;
    private HapticManager hapticManagerCache;

    private void Awake()
    {
        trapSystemCache = FindAnyObjectByType<TrapSystem>();
        hapticManagerCache = FindAnyObjectByType<HapticManager>();

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
        
        rabbitInstances = new GameObject[maxRabbits];
        rabbitPosX = new float[maxRabbits];
        rabbitPosY = new float[maxRabbits];

        SpawnInitialFauna();
    }

    private void SpawnInitialFauna()
    {
        GameObject faunaParent = new GameObject("Fauna_Instances");

        // Spawn cervos
        for (int i = 0; i < maxDeer; i++)
        {
            deerPosX[i] = Random.Range(10f, 90f);
            deerPosY[i] = Random.Range(10f, 90f);
            if (deerPrefab != null) 
            {
                deerInstances[i] = Instantiate(deerPrefab, faunaParent.transform);
                deerInstances[i].SetActive(true);
            }
        }

        // Spawn lobos nos cantos
        for (int i = 0; i < maxWolves; i++)
        {
            float angle = (float)i / maxWolves * Mathf.PI * 2f;
            wolfPosX[i] = 50f + Mathf.Cos(angle) * 40f;
            wolfPosY[i] = 50f + Mathf.Sin(angle) * 40f;
            if (wolfPrefab != null) 
            {
                wolfInstances[i] = Instantiate(wolfPrefab, faunaParent.transform);
                wolfInstances[i].SetActive(true);
            }
        }
        
        // Spawn Bears
        for (int i = 0; i < maxBears; i++)
        {
            bearPosX[i] = Random.Range(20f, 80f);
            bearPosY[i] = Random.Range(20f, 80f);
            bearHP[i] = 100f; // Urso precisa de Dano letal do arco
            
            if (bearPrefab != null)
            {
                bearInstances[i] = Instantiate(bearPrefab, faunaParent.transform);
                bearInstances[i].transform.localScale = Vector3.one * 1.5f; // Bigger
                bearInstances[i].SetActive(true);
            }
        }
        
        // Spawn Rabbits
        for(int i=0; i<maxRabbits; i++)
        {
            rabbitPosX[i] = Random.Range(10f, 90f);
            rabbitPosY[i] = Random.Range(10f, 90f);
            if (rabbitPrefab != null)
            {
                rabbitInstances[i] = Instantiate(rabbitPrefab, faunaParent.transform);
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

        // Atualizar Ursos (Logica simples C#)
        UpdateBears(px, pz, dt);
        
        UpdateRabbits(px, pz, dt);

        // Atualizar posições visuais
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
                    // Ataque do lobo
                    if (playerSanity != null)
                    {
                        playerSanity.sanity -= wolfDamage;
                        Debug.Log("[Fauna] Lobo atacou o jogador!");

                        if (hapticManagerCache != null) hapticManagerCache.TriggerHeavyVibration();
                    }
                    wolfAttackCooldowns[i] = 3f; // Cooldown entre ataques
                }
            }
        }
    }
    
    private void UpdateBears(float px, float pz, float dt)
    {
        for(int i=0; i<maxBears; i++)
        {
            if (bearHP[i] <= 0) continue;
            
            float dx = px - bearPosX[i];
            float dz = pz - bearPosY[i];
            float distSq = dx * dx + dz * dz;
            
            if (distSq < 15f * 15f) // Aggro range
            {
                // Chase
                float dist = Mathf.Sqrt(distSq);
                bearPosX[i] += (dx / dist) * 2.5f * dt;
                bearPosY[i] += (dz / dist) * 2.5f * dt;
                
                if (dist < 2.0f) // Attack
                {
                    if (playerSanity != null)
                    {
                        // Urso ataca direto o HP do player Status se der
                        PlayerStatus ps = playerTransform.GetComponent<PlayerStatus>();
                        if (ps != null) ps.TakeDamagePassive(10f * dt);
                        
                        if (hapticManagerCache != null && Time.frameCount % 30 == 0) hapticManagerCache.TriggerHeavyVibration();
                    }
                }
            }
        }
    }
    
    private void UpdateRabbits(float px, float pz, float dt)
    {
        for(int i=0; i<maxRabbits; i++)
        {
            if (rabbitPosX[i] < 0) continue; // Captured
            
            float dx = rabbitPosX[i] - px;
            float dz = rabbitPosY[i] - pz;
            float distSq = dx * dx + dz * dz;
            
            if (distSq < 10f * 10f) // Flee
            {
                float dist = Mathf.Sqrt(distSq);
                rabbitPosX[i] += (dx / dist) * 4f * dt;
                rabbitPosY[i] += (dz / dist) * 4f * dt;
            }
            else
            {
                // Wander
                rabbitPosX[i] += Mathf.Sin(Time.time * 0.5f + i) * 0.5f * dt;
                rabbitPosY[i] += Mathf.Cos(Time.time * 0.5f + i) * 0.5f * dt;
            }
            
            // Keep in bounds
            rabbitPosX[i] = Mathf.Clamp(rabbitPosX[i], 1, resourceManager.MapWidth - 2);
            rabbitPosY[i] = Mathf.Clamp(rabbitPosY[i], 1, resourceManager.MapHeight - 2);
        }
    }

    /// Chamado quando o jogador mata um cervo (se estiver perto e com faca).
    public void HuntDeer(int deerIndex)
    {
        if (deerIndex < 0 || deerIndex >= maxDeer) return;

        // Verificar se o jogador tem faca
        if (inventory != null && inventory.HasItem(302))
        {
            Vector3 dropPos = new Vector3(deerPosX[deerIndex], 0f, deerPosY[deerIndex]);
            
            if (LootManager.Instance != null)
            {
                LootManager.Instance.DropItem(104, 2, dropPos); // 2x Carne Crua
                LootManager.Instance.DropItem(105, 1, dropPos); // 1x Pele de Animal
            }

            Debug.Log("[Fauna] Cervo cacado. Loot largado no chao.");

            // Respawn o cervo longe
            deerPosX[deerIndex] = Random.Range(10f, 90f);
            deerPosY[deerIndex] = Random.Range(10f, 90f);
            deerVelX[deerIndex] = 0f;
            deerVelY[deerIndex] = 0f;
        }
        else
        {
            Debug.LogWarning("[Fauna] Precisa de uma Faca de Caca para esfolar o animal.");
        }
    }

    /// Verifica se ha um cervo proximo e o caca.
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
        Debug.Log("[Fauna] Nenhum cervo por perto.");
    }

    public bool TryDamageFaunaAt(Vector3 hitPoint, float damage)
    {
        // Tentar acertar cervo
        for (int i = 0; i < maxDeer; i++)
        {
            float dx = deerPosX[i] - hitPoint.x;
            float dy = deerPosY[i] - hitPoint.z;
            
            if (dx * dx + dy * dy < 4f)
            {
                HuntDeer(i);
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(deerPosX[i], 0f, deerPosY[i]));
                return true;
            }
        }
        
        // Tentar acertar lobo
        for (int i = 0; i < maxWolves; i++)
        {
            float dx = wolfPosX[i] - hitPoint.x;
            float dy = wolfPosY[i] - hitPoint.z;
            
            if (dx * dx + dy * dy < 4f)
            {
                // Respawn lobo longe
                wolfPosX[i] = Random.Range(10f, 90f);
                wolfPosY[i] = Random.Range(10f, 90f);
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(wolfPosX[i], 0f, wolfPosY[i]));
                Debug.LogWarning("[Fauna] Lobo abatido!");
                return true;
            }
        }
        
        // Tentar acertar urso
        for (int i = 0; i < maxBears; i++)
        {
            if (bearHP[i] <= 0) continue;
            
            float dx = bearPosX[i] - hitPoint.x;
            float dy = bearPosY[i] - hitPoint.z;
            
            if (dx * dx + dy * dy < 6f) // Urso tem hit radius maior
            {
                bearHP[i] -= damage;
                Debug.Log($"[Fauna] Urso sofreu {damage} dano. HP: {bearHP[i]}");
                
                if (bearHP[i] <= 0)
                {
                    if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(bearPosX[i], 0f, bearPosY[i]));
                    if (LootManager.Instance != null)
                    {
                        LootManager.Instance.DropItem(104, 5, new Vector3(bearPosX[i], 0f, bearPosY[i])); // 5 Carne
                        LootManager.Instance.DropItem(105, 3, new Vector3(bearPosX[i], 0f, bearPosY[i])); // 3 Pele
                    }
                    Debug.LogWarning("[Fauna] URSO ABATIDO!");
                    // Reposicionar e reviver daqui a muito tempo (hacky hide)
                    bearPosX[i] = -1000f; 
                }
                return true;
            }
        }
        
        // Tentar acertar coelho
        for(int i=0; i<maxRabbits; i++)
        {
            if (rabbitPosX[i] < 0) continue;
            
            float dx = rabbitPosX[i] - hitPoint.x;
            float dy = rabbitPosY[i] - hitPoint.z;
            if (dx*dx + dy*dy < 2.5f)
            {
                if (LootManager.Instance != null)
                {
                    LootManager.Instance.DropItem(113, 1, new Vector3(rabbitPosX[i], 0f, rabbitPosY[i])); // Carne Leve
                }
                rabbitPosX[i] = Random.Range(10f, 90f);
                rabbitPosY[i] = Random.Range(10f, 90f);
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(new Vector3(hitPoint.x, 0, hitPoint.z));
                return true;
            }
        }
        
        return false;
    }

    private void UpdateRenderTransforms()
    {
        float px = playerTransform.position.x;
        float pz = playerTransform.position.z;
        float cullDistSq = 3600f; // 60 * 60

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
                deerInstances[i].transform.position = new Vector3(deerPosX[i], 0f, deerPosY[i]);
                Vector3 moveDir = new Vector3(deerVelX[i], 0, deerVelY[i]);
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    deerInstances[i].transform.rotation = Quaternion.LookRotation(moveDir.normalized);
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
                wolfInstances[i].transform.position = new Vector3(wolfPosX[i], 0f, wolfPosY[i]);
                Vector3 moveDir = new Vector3(wolfVelX[i], 0, wolfVelY[i]);
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    wolfInstances[i].transform.rotation = Quaternion.LookRotation(moveDir.normalized);
                }
            }
        }
        
        for (int i = 0; i < maxBears; i++)
        {
            if (bearInstances[i] != null)
            {
                float distSq = (px - bearPosX[i]) * (px - bearPosX[i]) + (pz - bearPosY[i]) * (pz - bearPosY[i]);
                bearInstances[i].SetActive(bearHP[i] > 0 && distSq <= cullDistSq);
                bearInstances[i].transform.position = new Vector3(bearPosX[i], 0f, bearPosY[i]);
            }
        }
        
        for (int i = 0; i < maxRabbits; i++)
        {
            if (rabbitInstances[i] != null)
            {
                float distSq = (px - rabbitPosX[i]) * (px - rabbitPosX[i]) + (pz - rabbitPosY[i]) * (pz - rabbitPosY[i]);
                rabbitInstances[i].SetActive(rabbitPosX[i] >= 0 && distSq <= cullDistSq);
                rabbitInstances[i].transform.position = new Vector3(rabbitPosX[i], 0f, rabbitPosY[i]);
            }
        }
    }
    
    // API para armadilhas
    public void CatchRabbit(int index)
    {
        if (index >= 0 && index < maxRabbits)
        {
            if (LootManager.Instance != null)
            {
                LootManager.Instance.DropItem(113, 1, new Vector3(rabbitPosX[index], 0f, rabbitPosY[index]));
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
}
