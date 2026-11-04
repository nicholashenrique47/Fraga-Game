using UnityEngine;

/// BossSystem: IA avançada para o Vulto Anciao (Boss Final).
/// Comporta-se de forma agressiva com varios padroes de ataque (Summon, AoE, Melee).
public class BossSystem : MonoBehaviour
{
    public enum BossState { Dormant, Chasing, Summoning, AreaAttack, Dead }
    
    [Header("Dependencies")]
    public Transform playerTransform;
    public VultoManager vultoManager;
    public DayNightCycle dayNightCycle;
    public SanitySystem sanitySystem;
    
    [Header("Boss Stats")]
    public float maxHealth = 500f;
    public float currentHealth;
    public float moveSpeed = 4f;
    
    [Header("Attacks")]
    public float meleeRange = 3f;
    public float meleeDamage = 25f;
    public float summonCooldown = 20f;
    public float aoeCooldown = 15f;
    public float aoeRadius = 8f;
    public float aoeDamage = 40f;
    
    private BossState state = BossState.Dormant;
    private GameObject bossInstance;
    
    private float summonTimer = 0f;
    private float aoeTimer = 0f;
    private float attackTimer = 0f;
    private float stateTimer = 0f;
    
    private Vector3 aoeTarget;
    private GameObject aoeIndicator;
    
    public bool IsActive => state != BossState.Dormant && state != BossState.Dead;
    public float HealthPercentage => currentHealth / maxHealth;
    
    private void Start()
    {
        currentHealth = maxHealth;
    }
    
    public void SpawnBoss(Vector3 position)
    {
        if (IsActive) return;
        
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Boss_Prefab");
        if (prefab != null)
        {
            bossInstance = Instantiate(prefab, position, Quaternion.identity, transform);
            state = BossState.Chasing;
            currentHealth = maxHealth;
            
            summonTimer = 5f;
            aoeTimer = 10f;
            
            Debug.LogWarning("[BossSystem] O VULTO ANCIÃO DESPERTOU!");
            
            // UI alert
            UIManager ui = FindAnyObjectByType<UIManager>();
            if (ui != null) ui.SetLootTooltip("O Ancião Despertou...");
            
            // Haptic
            HapticManager haptic = FindAnyObjectByType<HapticManager>();
            if (haptic != null) haptic.TriggerHeavyVibration();
            
            if (audioManager != null) audioManager.PlayMusic(bossMusicClip);
        }
    }
    
    public AudioManager audioManager;
    public AudioClip bossMusicClip;
    
    private void Update()
    {
        // Spawns automatically on Day 5 night
        if (state == BossState.Dormant && dayNightCycle != null)
        {
            if (dayNightCycle.currentDay >= 5 && dayNightCycle.currentTime >= 20f)
            {
                // Spawn near player
                Vector3 spawnPos = playerTransform.position + new Vector3(Random.Range(-15f, 15f), 0, Random.Range(-15f, 15f));
                SpawnBoss(spawnPos);
            }
        }
        
        if (!IsActive || bossInstance == null || playerTransform == null) return;
        
        // Face player
        Vector3 dirToPlayer = (playerTransform.position - bossInstance.transform.position);
        dirToPlayer.y = 0;
        float distToPlayer = dirToPlayer.magnitude;
        if (distToPlayer > 0.01f)
        {
            bossInstance.transform.rotation = Quaternion.Slerp(bossInstance.transform.rotation, Quaternion.LookRotation(dirToPlayer), Time.deltaTime * 3f);
        }
        
        switch (state)
        {
            case BossState.Chasing:
                UpdateChasing(dirToPlayer, distToPlayer);
                break;
            case BossState.Summoning:
                UpdateSummoning();
                break;
            case BossState.AreaAttack:
                UpdateAreaAttack(distToPlayer);
                break;
        }
        
        // Aura passiva: drena sanidade se estiver perto
        if (distToPlayer < 15f && sanitySystem != null)
        {
            sanitySystem.sanity -= 2f * Time.deltaTime;
        }
    }
    
    private void UpdateChasing(Vector3 dirToPlayer, float distToPlayer)
    {
        // Move towards player
        if (distToPlayer > meleeRange)
        {
            bossInstance.transform.position += dirToPlayer.normalized * moveSpeed * Time.deltaTime;
        }
        else
        {
            // Melee Attack
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                PlayerStatus pStatus = playerTransform.GetComponent<PlayerStatus>();
                if (pStatus != null) pStatus.TakeDamage(meleeDamage);
                
                PlayerController pController = playerTransform.GetComponent<PlayerController>();
                if (pController != null) pController.ApplyKnockback(dirToPlayer.normalized * 8f);
                
                attackTimer = 2f;
                HapticManager haptic = FindAnyObjectByType<HapticManager>();
                if (haptic != null) haptic.TriggerHeavyVibration();

                // Screen Shake no impacto melee
                if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.ShakeMedium();
            }
        }
        
        // Check state transitions
        summonTimer -= Time.deltaTime;
        aoeTimer -= Time.deltaTime;
        
        if (aoeTimer <= 0)
        {
            state = BossState.AreaAttack;
            stateTimer = 3f; // 3 seconds charge time
            aoeTimer = aoeCooldown;
            aoeTarget = playerTransform.position;
            
            // Create red indicator
            CreateAoEIndicator(aoeTarget);
        }
        else if (summonTimer <= 0 && vultoManager != null)
        {
            state = BossState.Summoning;
            stateTimer = 2f; // 2 seconds channel
            summonTimer = summonCooldown;
        }
    }
    
    private void UpdateSummoning()
    {
        stateTimer -= Time.deltaTime;
        
        // VFX pulsing
        float scale = 1f + Mathf.Sin(Time.time * 20f) * 0.1f;
        bossInstance.transform.localScale = new Vector3(scale, scale, scale);
        
        // Screen Shake contínuo durante channeling
        if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.ShakeContinuous(0.15f);
        
        if (stateTimer <= 0)
        {
            // Spawn 3 vultos
            Vector3 pos = bossInstance.transform.position;
            vultoManager.SpawnVulto(pos.x + 2f, pos.z);
            vultoManager.SpawnVulto(pos.x - 2f, pos.z);
            vultoManager.SpawnVulto(pos.x, pos.z + 2f);
            
            bossInstance.transform.localScale = Vector3.one;
            state = BossState.Chasing;
        }
    }
    
    private void UpdateAreaAttack(float distToPlayer)
    {
        stateTimer -= Time.deltaTime;
        
        if (aoeIndicator != null)
        {
            // Flash indicator
            float alpha = Mathf.PingPong(Time.time * 5f, 0.5f) + 0.2f;
            aoeIndicator.GetComponent<Renderer>().material.color = new Color(1f, 0f, 0f, alpha);
        }
        
        if (stateTimer <= 0)
        {
            // BOOM!
            float distToTarget = Vector3.Distance(playerTransform.position, aoeTarget);
            if (distToTarget <= aoeRadius)
            {
                PlayerStatus pStatus = playerTransform.GetComponent<PlayerStatus>();
                if (pStatus != null) pStatus.TakeDamage(aoeDamage);
                
                PlayerController pController = playerTransform.GetComponent<PlayerController>();
                if (pController != null) pController.ApplyKnockback((playerTransform.position - aoeTarget).normalized * 12f);
            }
            
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayDeathVFX(aoeTarget); // Mega smoke
            HapticManager haptic = FindAnyObjectByType<HapticManager>();
            if (haptic != null) haptic.TriggerHeavyVibration();

            // Screen Shake massivo na explosão AoE
            if (ScreenShakeSystem.Instance != null) ScreenShakeSystem.Instance.ShakeHeavy();
            
            if (aoeIndicator != null) Destroy(aoeIndicator);
            state = BossState.Chasing;
        }
    }
    
    private void CreateAoEIndicator(Vector3 pos)
    {
        aoeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(aoeIndicator.GetComponent<Collider>());
        aoeIndicator.transform.position = pos + new Vector3(0, 0.1f, 0);
        aoeIndicator.transform.localScale = new Vector3(aoeRadius * 2f, 0.05f, aoeRadius * 2f);
        
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (mat.shader == null) mat = new Material(Shader.Find("Unlit/Color"));
        
        mat.color = new Color(1f, 0f, 0f, 0.4f);
        // Make it transparent
        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        
        aoeIndicator.GetComponent<Renderer>().material = mat;
    }
    
    public void TakeDamage(float amount)
    {
        if (!IsActive) return;
        
        currentHealth -= amount;
        
        // Blink red
        Renderer[] renderers = bossInstance.GetComponentsInChildren<Renderer>();
        foreach(var r in renderers)
        {
            r.material.color = Color.Lerp(r.material.color, Color.red, 0.5f);
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        state = BossState.Dead;
        
        if (aoeIndicator != null) Destroy(aoeIndicator);
        
        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayDeathVFX(bossInstance.transform.position);
            VFXPoolManager.Instance.PlayDeathVFX(bossInstance.transform.position + Vector3.up);
        }
        
        // Drop Coração Sombrio (Item 115)
        if (LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(115, 1, bossInstance.transform.position);
        }
        
        UIManager ui = FindAnyObjectByType<UIManager>();
        if (ui != null) ui.SetLootTooltip("O VULTO ANCIÃO FOI DERROTADO!");
        
        Destroy(bossInstance);
        
        // Restore music
        if (audioManager != null) audioManager.StopMusic(); // Vai voltar ao normal
    }
}
