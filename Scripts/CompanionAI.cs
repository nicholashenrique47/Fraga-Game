using UnityEngine;

public class CompanionAI : MonoBehaviour
{
    [Header("Settings")]
    public float followSpeed = 3f;
    public float stoppingDistance = 2f;
    public float attackRange = 3f;
    public float attackCooldown = 2f;
    public float attackDamage = 50f;
    
    private Transform playerTransform;
    private float currentCooldown = 0f;
    private bool isHired = false; // Tem de interagir e dar carne primeiro

    private FaunaManager faunaCache;
    private VultoManager vultosCache;
    private UIManager uiCache;
    private AudioManager audioCache;
    
    private void Start()
    {
        // Encontra o jogador
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) playerTransform = p.transform;

        faunaCache = FindAnyObjectByType<FaunaManager>();
        vultosCache = FindAnyObjectByType<VultoManager>();
        uiCache = FindAnyObjectByType<UIManager>();
        audioCache = FindAnyObjectByType<AudioManager>();
    }
    
    public void Interact(InventoryAdvanced playerInventory)
    {
        if (isHired)
        {
            Debug.Log("[Companion] Já estou a seguir-te. Fica perto!");
            return;
        }
        
        // Pede 1 lata de comida (ID 102 - Comida Enlatada)
        if (playerInventory != null && playerInventory.HasItem(102))
        {
            playerInventory.RemoveItem(102, 1);
            isHired = true;
            Debug.Log("[Companion] Obrigado pela comida! Vou ajudar-te a sobreviver.");
            
            // UI Feedback
            if (uiCache != null) uiCache.SetLootTooltip("Sobrevivente Recrutado!");
        }
        else
        {
            Debug.Log("[Companion] Estou com tanta fome... Tens alguma Comida Enlatada?");
            if (uiCache != null) uiCache.SetLootTooltip("Precisa de 1 Comida Enlatada");
        }
    }

    private void Update()
    {
        if (!isHired || playerTransform == null) return;
        
        Vector3 playerPos = playerTransform.position;
        float distance = Vector3.Distance(transform.position, playerPos);
        
        // Follow Player
        if (distance > stoppingDistance)
        {
            Vector3 dir = (playerPos - transform.position).normalized;
            transform.position += dir * followSpeed * Time.deltaTime;
            
            // Look at dir
            dir.y = 0;
            if (dir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            }
        }
        
        // Auto-Attack enemies
        if (currentCooldown > 0f)
        {
            currentCooldown -= Time.deltaTime;
        }
        else
        {
            TryAttackNearbyEnemies();
        }
    }
    
    private void TryAttackNearbyEnemies()
    {
        bool attacked = false;
        
        // Tenta atacar Fauna (Lobos)
        if (faunaCache != null)
        {
            if (faunaCache.TryDamageFaunaAt(transform.position, attackDamage))
            {
                attacked = true;
                Debug.Log("[Companion] Ataquei um Lobo!");
            }
        }
        
        // Tenta atacar Vultos
        if (!attacked)
        {
            if (vultosCache != null)
            {
                if (vultosCache.TryDamageVultoAt(transform.position, attackDamage))
                {
                    attacked = true;
                    Debug.Log("[Companion] Ataquei um Vulto!");
                }
            }
        }
        
        if (attacked)
        {
            currentCooldown = attackCooldown;
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position);
            
            if (audioCache != null) audioCache.PlayPickupSound(); // Som placeholder de hit
        }
    }
}
