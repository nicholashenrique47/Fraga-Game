using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// VultoManager: Sistema GPU-Instanced Zero-GC das entidades sombrias (Vultos da Fraga).
/// Inclui HP individual, knockback fisico, stagger ao receber golpes, deteccao por raio/esfera,
/// repulsao por fogueiras acesas e recompensas ao abater.
/// </summary>
public class VultoManager : MonoBehaviour
{
    public static VultoManager Instance { get; private set; }

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UpdateVultos(
        System.IntPtr posX, System.IntPtr posY,
        System.IntPtr velX, System.IntPtr velY,
        int count, System.IntPtr flowFieldX, System.IntPtr flowFieldY,
        int width, int height, float deltaTime,
        float speed, float separationRadius, float separationWeight,
        float playerX, float playerY, float lightRadius);

    [Header("Pool Settings (Mobile Instanced)")]
    public int maxVultos = 500;
    public Mesh vultoMesh;
    public Material vultoMaterial;

    [Header("Flocking Configs")]
    public float baseSpeed = 3.2f;
    public float separationRadius = 1.5f;
    public float separationWeight = 2f;

    [Header("Combat")]
    public float baseVultoHP = 55f;
    public float attackRadius = 1.25f;
    public float attackDamage = 8f;
    public float attackCooldown = 1.35f;
    private float nextAttackTime = 0f;

    [Header("Advanced AI")]
    public float teleportCooldown = 45f;
    public float teleportRange = 5f;
    public float ambushDaySpeedMult = 0.1f;
    public float nightPatrolSpawnInterval = 34f;
    public int maxAmbientNightVultos = 6;
    private float lastTeleportTime = -20f;
    private float nextAmbientPatrolTime = 18f;

    [Header("Player Tracking")]
    public Transform playerTransform;
    public SanitySystem playerSanity;

    private float[] posXArray;
    private float[] posYArray;
    private float[] velXArray;
    private float[] velYArray;
    private float[] hpArray;
    private float[] staggerTimers;
    private float[] scaleVariance;
    private byte[] aiStateArray; // 0=Stalk, 1=Flank, 2=Charge, 3=Retreat
    private float[] retreatTimers;
    private float clanFearTimer = 0f;

    private GCHandle posXHandle;
    private GCHandle posYHandle;
    private GCHandle velXHandle;
    private GCHandle velYHandle;

    private int currentActiveVultos = 0;
    public int ActiveVultoCount => currentActiveVultos;

    /// <summary>
    /// Retorna intensidade de ameaca normalizada (0..1) considerando Vultos ativos e Boss.
    /// </summary>
    public float GetThreatIntensity01()
    {
        float baseThreat = Mathf.Clamp01(currentActiveVultos / 8f);
        if (BossSystem.Instance != null && BossSystem.Instance.IsActive)
            baseThreat = Mathf.Max(baseThreat, BossSystem.Instance.IsEnraged ? 1f : 0.85f);
        return baseThreat;
    }

    /// <summary>
    /// Hook Multiplayer-Ready: escolhe a posicao do jogador vivo mais proximo registrado na sessao Co-op.
    /// Mantem fallback direto para playerTransform no modo Solo sem alterar o ABI nativo.
    /// </summary>
    public Vector3 GetNearestTargetPlayerPosition(Vector3 fromWorldPos)
    {
        Vector3 fallback = playerTransform != null ? playerTransform.position : fromWorldPos;
        if (FragaCoopSession.Instance == null || !FragaCoopSession.Instance.IsCoopSessionActive)
            return fallback;

        var members = FragaCoopSession.Instance.Members;
        float bestDistSq = float.MaxValue;
        Vector3 bestPos = fallback;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (!m.isConnected || m.isDowned) continue;
            float dSq = (m.worldPosition - fromWorldPos).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                bestPos = m.worldPosition;
            }
        }
        return bestPos;
    }

    private int mapWidth;
    private int mapHeight;
    private System.IntPtr flowFieldXPtr;
    private System.IntPtr flowFieldYPtr;

    // Caches de Componentes
    private DayNightCycle dnc;
    private TrapSystem trapSystem;
    private UIManager ui;
    private HapticManager haptic;
    private PlayerStatus pStatus;
    private PlayerController pController;
    private AudioManager audioManagerCache;
    private BonfireEntity[] bonfireCache;
    private float nextBonfireRefreshTime = 0f;
    private GameManager gmCache;
    private InventoryAdvanced inventoryCache;

    // GPU Instancing Buffers (Zero-GC)
    private Matrix4x4[] instanceMatrices;
    private readonly Matrix4x4[] batchBuffer = new Matrix4x4[1023];

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void InitializeSystem(int width, int height, System.IntPtr flowX, System.IntPtr flowY)
    {
        mapWidth = width;
        mapHeight = height;
        flowFieldXPtr = flowX;
        flowFieldYPtr = flowY;

        posXArray = new float[maxVultos];
        posYArray = new float[maxVultos];
        velXArray = new float[maxVultos];
        velYArray = new float[maxVultos];
        hpArray = new float[maxVultos];
        staggerTimers = new float[maxVultos];
        scaleVariance = new float[maxVultos];
        aiStateArray = new byte[maxVultos];
        retreatTimers = new float[maxVultos];

        posXHandle = GCHandle.Alloc(posXArray, GCHandleType.Pinned);
        posYHandle = GCHandle.Alloc(posYArray, GCHandleType.Pinned);
        velXHandle = GCHandle.Alloc(velXArray, GCHandleType.Pinned);
        velYHandle = GCHandle.Alloc(velYArray, GCHandleType.Pinned);

        instanceMatrices = new Matrix4x4[maxVultos];

        dnc = FindAnyObjectByType<DayNightCycle>();
        trapSystem = FindAnyObjectByType<TrapSystem>();
        ui = FindAnyObjectByType<UIManager>();
        haptic = FindAnyObjectByType<HapticManager>();
        audioManagerCache = FindAnyObjectByType<AudioManager>();
        gmCache = FindAnyObjectByType<GameManager>();
        inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (playerTransform != null)
        {
            pStatus = playerTransform.GetComponent<PlayerStatus>();
            pController = playerTransform.GetComponent<PlayerController>();
        }
    }

    public void ClearNearbyVultos(Vector3 center, float radius)
    {
        float sqrRad = radius * radius;
        for (int i = 0; i < currentActiveVultos; i++)
        {
            float dx = posXArray[i] - center.x;
            float dz = posYArray[i] - center.z;
            if (dx * dx + dz * dz <= sqrRad)
            {
                DespawnVultoAt(i);
                i--;
            }
        }
    }

    public void SpawnVulto(float x, float y)
    {
        if (currentActiveVultos >= maxVultos || posXArray == null) return;

        int i = currentActiveVultos;
        posXArray[i] = x;
        posYArray[i] = y;
        velXArray[i] = 0f;
        velYArray[i] = 0f;
        float dayBonus = dnc != null ? Mathf.Max(0, dnc.currentDay - 1) * 5f : 0f;
        hpArray[i] = baseVultoHP + dayBonus;
        staggerTimers[i] = 0f;
        scaleVariance[i] = Random.Range(0.90f, 1.18f);
        aiStateArray[i] = 0;
        retreatTimers[i] = 0f;

        currentActiveVultos++;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float now = Time.time;
        if (clanFearTimer > 0f) clanFearTimer = Mathf.Max(0f, clanFearTimer - dt);

        if (dnc == null) dnc = DayNightCycle.Instance ?? FindAnyObjectByType<DayNightCycle>();
        bool isNightNow = dnc == null || dnc.currentTime >= 19f || dnc.currentTime < 6f;

        // Patrulha noturna ambiental nas sombras da floresta
        if (isNightNow && playerTransform != null && now >= nextAmbientPatrolTime && currentActiveVultos < maxAmbientNightVultos)
        {
            nextAmbientPatrolTime = now + nightPatrolSpawnInterval;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(26f, 38f);
            float sx = Mathf.Clamp(playerTransform.position.x + Mathf.Cos(ang) * dist, 8f, Mathf.Max(16f, mapWidth - 8f));
            float sz = Mathf.Clamp(playerTransform.position.z + Mathf.Sin(ang) * dist, 8f, Mathf.Max(16f, mapHeight - 8f));
            SpawnVulto(sx, sz);
        }

        if (currentActiveVultos == 0 || flowFieldXPtr == System.IntPtr.Zero || playerTransform == null || playerSanity == null) return;

        if (now >= nextBonfireRefreshTime)
        {
            nextBonfireRefreshTime = now + 1.5f;
            bonfireCache = FindObjectsByType<BonfireEntity>();
            if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;
            if (gmCache == null) gmCache = FindAnyObjectByType<GameManager>();
        }

        float pX = playerTransform.position.x;
        float pY = playerTransform.position.z;
        float lightRad = playerSanity.GetLightRadius();
        float stealthMult = FragaStealthSystem.Instance != null ? FragaStealthSystem.Instance.GetDetectionMultiplier() : 1f;
        bool playerHidden = FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.IsHidden;

        if (pController != null && pController.isCrouching)
        {
            lightRad *= 0.35f;
        }
        lightRad *= Mathf.Lerp(0.65f, 1.15f, Mathf.Clamp01(stealthMult - 0.3f));

        float effectiveSpeed = baseSpeed;
        bool isNight = true;
        if (dnc != null)
        {
            isNight = dnc.currentTime >= 19f || dnc.currentTime < 6f;
            if (!isNight) effectiveSpeed *= ambushDaySpeedMult;
        }

        UpdateVultos(
            posXHandle.AddrOfPinnedObject(),
            posYHandle.AddrOfPinnedObject(),
            velXHandle.AddrOfPinnedObject(),
            velYHandle.AddrOfPinnedObject(),
            currentActiveVultos,
            flowFieldXPtr, flowFieldYPtr,
            mapWidth, mapHeight,
            dt, effectiveSpeed, separationRadius, separationWeight,
            pX, pY, lightRad
        );

        // === STALKER AI OVERRIDE + STAGGER + REPULSAO DE FOGUEIRA ===
        bool sanityLow = playerSanity != null && playerSanity.sanity < 30f;
        bool hasTorchOrLamp = (inventoryCache != null && (inventoryCache.equippedOffHandId == 303 || inventoryCache.equippedOffHandId == 304)) ||
                              (playerSanity != null && playerSanity.isLampOn && playerSanity.oil > 1f);
        bool holdingTorch = inventoryCache != null && inventoryCache.equippedOffHandId == 303;
        Vector3 playerForward = playerTransform.forward;
        playerForward.y = 0f;
        playerForward.Normalize();
        var structures = StructureHealth.AllStructures;

        for (int i = 0; i < currentActiveVultos; i++)
        {
            if (staggerTimers[i] > 0f)
            {
                staggerTimers[i] -= dt;
                velXArray[i] *= 0.15f;
                velYArray[i] *= 0.15f;
                continue;
            }

            float dx = posXArray[i] - pX;
            float dz = posYArray[i] - pY;
            float sqrDist = dx * dx + dz * dz;

            // === IA SOCIAL DE CLA + FURTIVIDADE (Stalk / Flank / Charge / Retreat) ===
            if (retreatTimers[i] > 0f) retreatTimers[i] -= dt;
            bool lowHp = hpArray[i] < baseVultoHP * 0.28f;
            if (lowHp || (clanFearTimer > 0f && sqrDist < 196f))
            {
                aiStateArray[i] = 3; // Retreat
            }
            else if (sqrDist <= 36f || sanityLow)
            {
                aiStateArray[i] = 2; // Charge
            }
            else if (sqrDist < 225f && (!playerHidden || stealthMult > 0.65f))
            {
                aiStateArray[i] = 1; // Flank
            }
            else
            {
                aiStateArray[i] = 0; // Stalk
            }

            if (aiStateArray[i] == 3 && sqrDist > 0.25f)
            {
                float invR = 1f / Mathf.Sqrt(sqrDist);
                posXArray[i] += dx * invR * 3.6f * dt;
                posYArray[i] += dz * invR * 3.6f * dt;
                velXArray[i] = dx * invR * 3.2f;
                velYArray[i] = dz * invR * 3.2f;
            }
            else if (aiStateArray[i] == 1 && sqrDist > 36f)
            {
                float invF = 1f / Mathf.Sqrt(sqrDist);
                float sideSign = (i & 1) == 0 ? 1f : -1f;
                float tanX = -dz * invF * sideSign;
                float tanZ = dx * invF * sideSign;
                posXArray[i] += tanX * 2.1f * dt;
                posYArray[i] += tanZ * 2.1f * dt;
            }
            else if (aiStateArray[i] == 0 && playerHidden && sqrDist > 120f)
            {
                velXArray[i] *= 0.35f;
                velYArray[i] *= 0.35f;
            }

            // Reacao a cone de luz da Tocha / Lampiao
            if (hasTorchOrLamp && !sanityLow && sqrDist < (holdingTorch ? 49f : 36f) && sqrDist > 0.25f)
            {
                float invD = 1f / Mathf.Sqrt(sqrDist);
                float lightDot = playerForward.x * (dx * invD) + playerForward.z * (dz * invD);
                if (lightDot > 0.52f)
                {
                    float push = holdingTorch ? 3.2f : 1.8f;
                    posXArray[i] += dx * invD * push * dt;
                    posYArray[i] += dz * invD * push * dt;
                    velXArray[i] *= 0.25f;
                    velYArray[i] *= 0.25f;
                }
            }

            // Reacao a Circulo de Sal (tile 25 no grid nativo)
            if (gmCache != null && gmCache.mapDataPtr != System.IntPtr.Zero && sqrDist < 484f)
            {
                int gx = Mathf.RoundToInt(posXArray[i]);
                int gz = Mathf.RoundToInt(posYArray[i]);
                if (gx >= 1 && gx < gmCache.mapWidth - 1 && gz >= 1 && gz < gmCache.mapHeight - 1)
                {
                    int cell = ResourceManager.GetMapCell(gmCache.mapDataPtr, gmCache.mapWidth, gmCache.mapHeight, gx, gz);
                    if (cell == 25)
                    {
                        float inv = sqrDist > 0.01f ? 1f / Mathf.Sqrt(sqrDist) : 1f;
                        posXArray[i] += dx * inv * 6.0f * dt;
                        posYArray[i] += dz * inv * 6.0f * dt;
                        hpArray[i] -= 10f * dt;
                    }
                }
            }

            if (!sanityLow && bonfireCache != null)
            {
                for (int b = 0; b < bonfireCache.Length; b++)
                {
                    BonfireEntity bf = bonfireCache[b];
                    if (bf == null || !bf.isLit) continue;
                    float bx = posXArray[i] - bf.transform.position.x;
                    float bz = posYArray[i] - bf.transform.position.z;
                    float bDistSq = bx * bx + bz * bz;
                    if (bDistSq < 30.25f && bDistSq > 0.01f)
                    {
                        float inv = 1f / Mathf.Sqrt(bDistSq);
                        posXArray[i] += bx * inv * 4.5f * dt;
                        posYArray[i] += bz * inv * 4.5f * dt;
                        velXArray[i] = bx * inv * 3.0f;
                        velYArray[i] = bz * inv * 3.0f;
                    }
                }
            }

            if (!sanityLow && sqrDist > 100f && sqrDist < 600f)
            {
                float invDist = 1f / Mathf.Sqrt(sqrDist);
                float dot = playerForward.x * (dx * invDist) + playerForward.z * (dz * invDist);

                if (dot > 0.35f)
                {
                    velXArray[i] = 0f;
                    velYArray[i] = 0f;
                }
                else if (dot < -0.35f)
                {
                    velXArray[i] *= 2.2f;
                    velYArray[i] *= 2.2f;
                    posXArray[i] += velXArray[i] * dt;
                    posYArray[i] += velYArray[i] * dt;
                }
            }

            // Cerco noturno contra estruturas construidas (Palicadas / Abrigos)
            if (isNight && structures.Count > 0 && sqrDist < 900f)
            {
                for (int sIdx = 0; sIdx < structures.Count; sIdx++)
                {
                    StructureHealth st = structures[sIdx];
                    if (st == null) continue;
                    float sx = posXArray[i] - st.transform.position.x;
                    float sz = posYArray[i] - st.transform.position.z;
                    float sDistSq = sx * sx + sz * sz;
                    if (sDistSq < 3.2f && sDistSq > 0.01f)
                    {
                        st.ApplyDamage(7.5f * dt);
                        float invS = 1f / Mathf.Sqrt(sDistSq);
                        posXArray[i] += sx * invS * 1.4f * dt;
                        posYArray[i] += sz * invS * 1.4f * dt;
                        break;
                    }
                }
            }
        }

        // === TELEPORTE SOMBRIO ===
        if (isNight && sanityLow && now - lastTeleportTime > teleportCooldown && currentActiveVultos > 0)
        {
            int chosen = Random.Range(0, currentActiveVultos);
            float dx = posXArray[chosen] - pX;
            float dz = posYArray[chosen] - pY;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist > 15f)
            {
                Vector3 behind = playerTransform.position - playerTransform.forward * teleportRange;
                behind.x = Mathf.Clamp(behind.x, 2f, Mathf.Max(4f, mapWidth - 3f));
                behind.z = Mathf.Clamp(behind.z, 2f, Mathf.Max(4f, mapHeight - 3f));
                behind.y = TerrainMeshGenerator.GetTerrainHeight(behind.x, behind.z);
                posXArray[chosen] = behind.x;
                posYArray[chosen] = behind.z;
                velXArray[chosen] = 0f;
                velYArray[chosen] = 0f;
                lastTeleportTime = now;

                if (VFXPoolManager.Instance != null)
                {
                    VFXPoolManager.Instance.PlayShadowHitVFX(behind + Vector3.up * 0.8f);
                }
            }
        }

        if (trapSystem != null)
        {
            trapSystem.CheckTrapsForVultos(posXArray, posYArray, currentActiveVultos);
        }

        bool playerHitThisFrame = false;

        for (int i = 0; i < currentActiveVultos; i++)
        {
            float sqrDistToPlayer = (pX - posXArray[i]) * (pX - posXArray[i]) + (pY - posYArray[i]) * (pY - posYArray[i]);
            if (sqrDistToPlayer > 3600f)
            {
                DespawnVultoAt(i);
                i--;
                continue;
            }

            float gy = TerrainMeshGenerator.GetTerrainHeight(posXArray[i], posYArray[i]);
            float floatBob = Mathf.Sin(now * 4.2f + i * 1.1f) * 0.045f;
            Vector3 pos = new Vector3(posXArray[i], gy + floatBob, posYArray[i]);
            Quaternion rot = Quaternion.identity;

            bool isMoving = Mathf.Abs(velXArray[i]) > 0.02f || Mathf.Abs(velYArray[i]) > 0.02f;
            if (isMoving)
            {
                Vector3 moveDir = new Vector3(velXArray[i], 0f, velYArray[i]);
                rot = Quaternion.LookRotation(moveDir);
            }
            else
            {
                Vector3 toPlayer = new Vector3(pX - posXArray[i], 0f, pY - posYArray[i]);
                if (toPlayer.sqrMagnitude > 0.01f) rot = Quaternion.LookRotation(toPlayer);
            }

            bool isStaggered = staggerTimers[i] > 0f;
            float shiverRoll = Mathf.Sin(now * (isStaggered ? 22f : 7.8f) + i * 1.7f) * (isStaggered ? 12f : (isMoving ? 4.5f : 2.2f));
            float leanPitch = isStaggered ? -18f : (isMoving ? 11f : Mathf.Sin(now * 3.1f + i) * 3.5f);
            rot *= Quaternion.Euler(leanPitch, 0f, shiverRoll);

            float s = scaleVariance[i] > 0f ? scaleVariance[i] : 1f;
            if (isStaggered) s *= 0.94f;
            instanceMatrices[i].SetTRS(pos, rot, new Vector3(s, s, s));

            if (!playerHitThisFrame && !isStaggered && now >= nextAttackTime)
            {
                if (sqrDistToPlayer <= attackRadius * attackRadius)
                {
                    if (pController != null && pController.IsInvulnerable)
                    {
                        if (HUDSurvivalOverlay.Instance != null)
                            HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#55FFDD>ESQUIVA SOMBRIA!</color>");
                        nextAttackTime = now + 0.85f;
                        playerHitThisFrame = true;
                    }
                    else
                    {
                        if (pStatus != null)
                        {
                            float hpBefore = pStatus.health;
                            pStatus.TakeDamage(attackDamage);
                            if (pStatus.health < hpBefore - 1f && Random.value < 0.20f)
                            {
                                pStatus.ApplyBleeding();
                            }
                        }
                        if (pController != null)
                        {
                            Vector3 knockbackDir = (playerTransform.position - pos).normalized;
                            knockbackDir.y = 0f;
                            pController.ApplyKnockback(knockbackDir * 5.2f);
                        }

                        if (audioManagerCache != null) audioManagerCache.PlayVultoAttackSound();
                        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowDamageFromDirection(pos);
                        else if (ui != null) ui.ShowDamageFromDirection(pos);
                        if (haptic != null) haptic.TriggerHeavyVibration();

                        nextAttackTime = now + attackCooldown;
                        playerHitThisFrame = true;
                    }
                }
            }
        }

        if (vultoMesh != null && vultoMaterial != null)
        {
            if (currentActiveVultos <= 1023)
            {
                Graphics.DrawMeshInstanced(vultoMesh, 0, vultoMaterial, instanceMatrices, currentActiveVultos);
            }
            else
            {
                int rendered = 0;
                while (rendered < currentActiveVultos)
                {
                    int count = Mathf.Min(1023, currentActiveVultos - rendered);
                    System.Array.Copy(instanceMatrices, rendered, batchBuffer, 0, count);
                    Graphics.DrawMeshInstanced(vultoMesh, 0, vultoMaterial, batchBuffer, count);
                    rendered += count;
                }
            }
        }
    }

    public bool TryDamageVultoInRay(Ray ray, float maxRange, float radius, float damage, Vector3 knockbackDir, out Vector3 hitPos, out bool killed)
    {
        hitPos = Vector3.zero;
        killed = false;
        if (currentActiveVultos == 0 || posXArray == null) return false;

        int bestIdx = -1;
        float bestProj = maxRange + 0.5f;
        float hitThreshold = radius + 0.65f;

        for (int i = 0; i < currentActiveVultos; i++)
        {
            float gy = TerrainMeshGenerator.GetTerrainHeight(posXArray[i], posYArray[i]);
            Vector3 center = new Vector3(posXArray[i], gy + 0.95f, posYArray[i]);
            Vector3 toCenter = center - ray.origin;
            float proj = Vector3.Dot(toCenter, ray.direction);

            if (proj < -0.2f || proj > maxRange + 0.5f) continue;

            Vector3 closestOnRay = ray.origin + ray.direction * Mathf.Max(0f, proj);
            float dx = center.x - closestOnRay.x;
            float dz = center.z - closestOnRay.z;
            float dy = Mathf.Abs(center.y - closestOnRay.y);

            if (dx * dx + dz * dz <= hitThreshold * hitThreshold && dy <= 1.35f)
            {
                if (proj < bestProj)
                {
                    bestProj = proj;
                    bestIdx = i;
                    hitPos = center;
                }
            }
        }

        if (bestIdx < 0) return false;

        ApplyDamageToVulto(bestIdx, damage, knockbackDir, out killed);
        return true;
    }

    public bool TryDamageVultoAt(Vector3 hitPoint, float damage)
    {
        if (currentActiveVultos == 0 || posXArray == null) return false;

        int bestIdx = -1;
        float bestSq = 4.0f;
        for (int i = 0; i < currentActiveVultos; i++)
        {
            float dx = posXArray[i] - hitPoint.x;
            float dy = posYArray[i] - hitPoint.z;
            float sq = dx * dx + dy * dy;
            if (sq < bestSq)
            {
                bestSq = sq;
                bestIdx = i;
            }
        }

        if (bestIdx < 0) return false;

        Vector3 kb = new Vector3(posXArray[bestIdx] - hitPoint.x, 0f, posYArray[bestIdx] - hitPoint.z);
        ApplyDamageToVulto(bestIdx, damage, kb, out _);
        return true;
    }

    private void ApplyDamageToVulto(int index, float damage, Vector3 knockbackDir, out bool killed)
    {
        killed = false;
        float gy = TerrainMeshGenerator.GetTerrainHeight(posXArray[index], posYArray[index]);
        Vector3 vultoPos = new Vector3(posXArray[index], gy + 0.85f, posYArray[index]);

        hpArray[index] -= damage;

        if (hpArray[index] <= 0f)
        {
            killed = true;
            clanFearTimer = 3.2f;
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayDeathVFX(vultoPos);
            }

            if (LootManager.Instance != null && Random.value < 0.60f)
            {
                int[] vultoLoot = { 106, 102, 118, 120, 121, 116 }; // Sal, Oleo, Corda, Osso, Tecido, Pagina Diario
                int dropId = vultoLoot[Random.Range(0, vultoLoot.Length)];
                LootManager.Instance.DropItem(dropId, 1, vultoPos);
            }

            if (audioManagerCache != null) audioManagerCache.PlayVultoAttackSound();
            DespawnVultoAt(index);
        }
        else
        {
            staggerTimers[index] = 0.42f;
            if (hpArray[index] < baseVultoHP * 0.30f) retreatTimers[index] = 2.6f;
            Vector3 dir = knockbackDir;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f && playerTransform != null)
            {
                dir = new Vector3(posXArray[index] - playerTransform.position.x, 0f, posYArray[index] - playerTransform.position.z);
            }
            if (dir.sqrMagnitude > 0.001f)
            {
                dir.Normalize();
                posXArray[index] = Mathf.Clamp(posXArray[index] + dir.x * 1.15f, 4f, Mathf.Max(8f, mapWidth - 4f));
                posYArray[index] = Mathf.Clamp(posYArray[index] + dir.z * 1.15f, 4f, Mathf.Max(8f, mapHeight - 4f));
            }

            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayShadowHitVFX(vultoPos);
            }
        }
    }

    private void DespawnVultoAt(int index)
    {
        int last = currentActiveVultos - 1;
        posXArray[index] = posXArray[last];
        posYArray[index] = posYArray[last];
        velXArray[index] = velXArray[last];
        velYArray[index] = velYArray[last];
        hpArray[index] = hpArray[last];
        staggerTimers[index] = staggerTimers[last];
        scaleVariance[index] = scaleVariance[last];
        aiStateArray[index] = aiStateArray[last];
        retreatTimers[index] = retreatTimers[last];
        currentActiveVultos--;
    }

    private void OnDestroy()
    {
        if (posXHandle.IsAllocated) posXHandle.Free();
        if (posYHandle.IsAllocated) posYHandle.Free();
        if (velXHandle.IsAllocated) velXHandle.Free();
        if (velYHandle.IsAllocated) velYHandle.Free();
    }
}
