using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class VultoManager : MonoBehaviour
{
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
    public float baseSpeed = 3f;
    public float separationRadius = 1.5f;
    public float separationWeight = 2f;

    [Header("Combat")]
    public float attackRadius = 1.2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    private float nextAttackTime = 0f;
    
    [Header("Advanced AI")]
    public float teleportCooldown = 15f;
    public float teleportRange = 5f;
    public float ambushDaySpeedMult = 0.1f; // Quase parados de dia
    private float lastTeleportTime = -20f;

    [Header("Player Tracking")]
    public Transform playerTransform;
    public SanitySystem playerSanity;

    private float[] posXArray;
    private float[] posYArray;
    private float[] velXArray;
    private float[] velYArray;

    private GCHandle posXHandle;
    private GCHandle posYHandle;
    private GCHandle velXHandle;
    private GCHandle velYHandle;

    private int currentActiveVultos = 0;

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

    // GPU Instancing Buffers
    private Matrix4x4[] instanceMatrices;

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

        posXHandle = GCHandle.Alloc(posXArray, GCHandleType.Pinned);
        posYHandle = GCHandle.Alloc(posYArray, GCHandleType.Pinned);
        velXHandle = GCHandle.Alloc(velXArray, GCHandleType.Pinned);
        velYHandle = GCHandle.Alloc(velYArray, GCHandleType.Pinned);

        // Pre-alocar as matrizes
        instanceMatrices = new Matrix4x4[maxVultos];

        dnc = FindAnyObjectByType<DayNightCycle>();
        trapSystem = FindAnyObjectByType<TrapSystem>();
        ui = FindAnyObjectByType<UIManager>();
        haptic = FindAnyObjectByType<HapticManager>();
        if (playerTransform != null)
        {
            pStatus = playerTransform.GetComponent<PlayerStatus>();
            pController = playerTransform.GetComponent<PlayerController>();
        }
    }

    public void SpawnVulto(float x, float y)
    {
        if (currentActiveVultos >= maxVultos) return;

        int i = currentActiveVultos;
        posXArray[i] = x;
        posYArray[i] = y;
        velXArray[i] = 0f;
        velYArray[i] = 0f;

        currentActiveVultos++;
    }

    private void Update()
    {
        if (currentActiveVultos == 0 || flowFieldXPtr == System.IntPtr.Zero || playerTransform == null || playerSanity == null) return;

        float pX = playerTransform.position.x;
        float pY = playerTransform.position.z;
        float lightRad = playerSanity.GetLightRadius();
        
        if (pController != null && pController.isCrouching)
        {
            // O jogador agachado emite menos ruÃ­do e Ã© mais difÃ­cil de ver pelos vultos
            lightRad *= 0.3f; 
        }

        float effectiveSpeed = baseSpeed;
        
        // Comportamento de Emboscada: de dia os vultos mal se movem
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
            Time.deltaTime, effectiveSpeed, separationRadius, separationWeight,
            pX, pY, lightRad
        );
        
                // === STALKER AI OVERRIDE (THE FOREST STYLE) ===
        bool sanityLow = playerSanity != null && playerSanity.sanity < 30f;
        Vector3 playerForward = playerTransform.forward;
        playerForward.y = 0; playerForward.Normalize();

        for (int i = 0; i < currentActiveVultos; i++) {
            float dx = posXArray[i] - pX;
            float dz = posYArray[i] - pY;
            float sqrDist = dx*dx + dz*dz;
            
            if (!sanityLow && sqrDist > 100f && sqrDist < 600f) {
                Vector3 dirToVulto = new Vector3(dx, 0, dz).normalized;
                float dot = Vector3.Dot(playerForward, dirToVulto);
                
                // Se o jogador estiver a olhar para eles, eles param (Stalking nas sombras)
                if (dot > 0.3f) {
                    velXArray[i] = 0;
                    velYArray[i] = 0;
                } 
                // Se o jogador virar as costas, eles correm agressivamente (Ambush)
                else if (dot < -0.3f) {
                    velXArray[i] *= 2.5f;
                    velYArray[i] *= 2.5f;
                    posXArray[i] += velXArray[i] * Time.deltaTime;
                    posYArray[i] += velYArray[i] * Time.deltaTime;
                }
            }
        }

        // === TELEPORTE SOMBRIO ===
        if (isNight && Time.time - lastTeleportTime > teleportCooldown && currentActiveVultos > 0)
        {
            // Escolher um vulto aleatorio que esteja longe do jogador
            int chosen = Random.Range(0, currentActiveVultos);
            float dx = posXArray[chosen] - pX;
            float dz = posYArray[chosen] - pY;
            float dist = Mathf.Sqrt(dx*dx + dz*dz);
            
            if (dist > 15f) // So teleporta se estiver longe
            {
                // Teleportar atras do jogador
                Vector3 behind = playerTransform.position - playerTransform.forward * teleportRange;
                posXArray[chosen] = behind.x;
                posYArray[chosen] = behind.z;
                velXArray[chosen] = 0;
                velYArray[chosen] = 0;
                lastTeleportTime = Time.time;
                
                if (VFXPoolManager.Instance != null)
                {
                    VFXPoolManager.Instance.PlayDeathVFX(behind); // Smoke effect
                }
                Debug.Log("[Vulto] TELEPORTE SOMBRIO! Vulto apareceu atras do jogador!");
            }
        }

        if (trapSystem != null)
        {
            trapSystem.CheckTrapsForVultos(posXArray, posYArray, currentActiveVultos);
        }

        // AtualizaÃ§Ã£o contÃ­gua de matrizes e Dano FÃ­sico
        Vector3 scale = Vector3.one;
        bool playerHitThisFrame = false;

        for (int i = 0; i < currentActiveVultos; i++)
        {
            Vector3 pos = new Vector3(posXArray[i], 0f, posYArray[i]);
            Quaternion rot = Quaternion.identity;

            // Distance Culling: Despawn se > 60m do jogador
            float sqrDistToPlayer = (pX - posXArray[i]) * (pX - posXArray[i]) + (pY - posYArray[i]) * (pY - posYArray[i]);
            if (sqrDistToPlayer > 3600f) // 60 * 60
            {
                DespawnVultoAt(i);
                i--;
                continue;
            }

            if (Mathf.Abs(velXArray[i]) > 0.01f || Mathf.Abs(velYArray[i]) > 0.01f)
            {
                Vector3 moveDir = new Vector3(velXArray[i], 0f, velYArray[i]);
                rot = Quaternion.LookRotation(moveDir);
            }

            instanceMatrices[i].SetTRS(pos, rot, scale);

            // VerificaÃ§Ã£o de Combate (O(N) rapido)
            if (!playerHitThisFrame && Time.time >= nextAttackTime)
            {
                float sqrDist = (pX - posXArray[i]) * (pX - posXArray[i]) + (pY - posYArray[i]) * (pY - posYArray[i]);
                if (sqrDist <= attackRadius * attackRadius)
                {
                    if (pStatus != null)
                    {
                        pStatus.TakeDamage(attackDamage);
                    }
                    if (pController != null)
                    {
                        Vector3 knockbackDir = (playerTransform.position - pos).normalized;
                        knockbackDir.y = 0;
                        pController.ApplyKnockback(knockbackDir * 5f);
                    }
                    
                    // Indicador de dano direcional
                    if (ui != null) ui.ShowDamageFromDirection(pos);
                    
                    // Haptic feedback
                    if (haptic != null) haptic.TriggerHeavyVibration();
                    
                    nextAttackTime = Time.time + attackCooldown;
                    playerHitThisFrame = true;
                }
            }
        }

        // Desenhar na GPU
        if (vultoMesh != null && vultoMaterial != null)
        {
            // O Unity suporta chamadas de DrawMeshInstanced em batches de 1023.
            // Para maxVultos <= 1000 num mobile, podemos fazer apenas 1 draw call call.
            if (currentActiveVultos <= 1023)
            {
                Graphics.DrawMeshInstanced(vultoMesh, 0, vultoMaterial, instanceMatrices, currentActiveVultos);
            }
            else
            {
                // TODO: Particionar se o limite for ultrapassado
                int rendered = 0;
                while(rendered < currentActiveVultos)
                {
                    int count = Mathf.Min(1023, currentActiveVultos - rendered);
                    Matrix4x4[] batch = new Matrix4x4[count];
                    System.Array.Copy(instanceMatrices, rendered, batch, 0, count);
                    Graphics.DrawMeshInstanced(vultoMesh, 0, vultoMaterial, batch, count);
                    rendered += count;
                }
            }
        }
    }

    public bool TryDamageVultoAt(Vector3 hitPoint, float damage)
    {
        for (int i = 0; i < currentActiveVultos; i++)
        {
            float dx = posXArray[i] - hitPoint.x;
            float dy = posYArray[i] - hitPoint.z;
            
            if (dx * dx + dy * dy < 4f) // Raio de hit
            {
                // Vulto morre (Desintegra)
                if (VFXPoolManager.Instance != null)
                {
                    VFXPoolManager.Instance.PlayDeathVFX(new Vector3(posXArray[i], 0f, posYArray[i]));
                }
                
                // Swap-and-pop para O(1) removal
                int last = currentActiveVultos - 1;
                posXArray[i] = posXArray[last];
                posYArray[i] = posYArray[last];
                velXArray[i] = velXArray[last];
                velYArray[i] = velYArray[last];
                
                currentActiveVultos--;
                return true;
            }
        }
        return false;
    }

    private void DespawnVultoAt(int index)
    {
        int last = currentActiveVultos - 1;
        posXArray[index] = posXArray[last];
        posYArray[index] = posYArray[last];
        velXArray[index] = velXArray[last];
        velYArray[index] = velYArray[last];
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
