using UnityEngine;

public class EquipmentSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerHandTransform; // Onde a arma é instanciada
    public PlayerStatus playerStatus;
    public InventoryAdvanced inventory;
    
    [Header("Weapon Settings")]
    public float attackRange = 2f;
    public float attackCooldown = 0.8f;
    public LayerMask enemyLayer; // Layer "Enemy" (Fauna / Vultos)
    
    private GameObject currentWeaponInstance;
    private int currentWeaponID = -1; // -1 = Desarmado (Punhos)
    
    private float lastAttackTime = 0f;

    private VultoManager vultosCache;
    private FaunaManager faunaCache;
    private MapRenderer mapRendererCache;
    private HapticManager hapticCache;
    private AudioManager audioCache;

    // Áudio Procedural
    private AudioSource weaponAudio;
    private AudioClip clipSwing;
    private AudioClip clipHitWood;
    private AudioClip clipHitRock;
    private AudioClip clipHitFlesh;

    private void Start()
    {
        vultosCache = FindAnyObjectByType<VultoManager>();
        faunaCache = FindAnyObjectByType<FaunaManager>();
        mapRendererCache = FindAnyObjectByType<MapRenderer>();
        hapticCache = FindAnyObjectByType<HapticManager>();
        audioCache = FindAnyObjectByType<AudioManager>();
        
        weaponAudio = gameObject.AddComponent<AudioSource>();
        weaponAudio.spatialBlend = 0.5f;
        
        clipSwing = GenerateActionClip("swing");
        clipHitWood = GenerateActionClip("wood");
        clipHitRock = GenerateActionClip("rock");
        clipHitFlesh = GenerateActionClip("flesh");
    }

    private AudioClip GenerateActionClip(string type)
    {
        int sampleRate = 44100;
        float duration = type == "swing" ? 0.2f : 0.15f;
        float[] samples = new float[(int)(sampleRate * duration)];
        
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / samples.Length;
            float timeSec = (float)i / sampleRate;
            float sample = 0f;
            
            if (type == "swing")
            {
                float freq = 200f - t * 150f;
                sample = Mathf.Sin(2f * Mathf.PI * freq * timeSec) * Mathf.Exp(-5f * t);
            }
            else if (type == "wood")
            {
                float noise = Random.Range(-1f, 1f);
                float env = Mathf.Exp(-20f * t);
                sample = noise * env * 0.7f + Mathf.Sin(2f * Mathf.PI * 300f * timeSec) * env * 0.3f;
            }
            else if (type == "rock")
            {
                float noise = Random.Range(-1f, 1f);
                float env = Mathf.Exp(-40f * t);
                sample = noise * env * 0.9f + Mathf.Sin(2f * Mathf.PI * 800f * timeSec) * env * 0.1f;
            }
            else if (type == "flesh")
            {
                float noise = Random.Range(-1f, 1f);
                float env = Mathf.Exp(-15f * t);
                sample = noise * env * 0.4f + Mathf.Sin(2f * Mathf.PI * 150f * timeSec) * env * 0.6f;
            }
            
            samples[i] = sample * 0.6f;
        }

        AudioClip clip = AudioClip.Create($"Synth_{type}", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void Update()
    {
        // Se quisermos atalho de teclado para PC
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Attack();
        }
    }

    public void EquipWeapon(int itemID, GameObject weaponPrefab)
    {
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
        }
        
        currentWeaponID = itemID;
        
        if (weaponPrefab != null && playerHandTransform != null)
        {
            currentWeaponInstance = Instantiate(weaponPrefab, playerHandTransform);
            currentWeaponInstance.transform.localPosition = Vector3.zero;
            currentWeaponInstance.transform.localRotation = Quaternion.identity;
        }
        
        Debug.Log($"[Equipment] Arma equipada: ID {itemID}");
    }

    public void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        
        // Custo de Stamina
        if (playerStatus != null)
        {
            if (playerStatus.stamina < 15f)
            {
                Debug.LogWarning("[Equipment] Sem stamina para atacar!");
                return;
            }
            playerStatus.stamina -= 15f;
        }
        
        lastAttackTime = Time.time;
        
        // Efeito Sonoro / Animação Básica (Placeholder via Juice)
        if (weaponAudio != null) weaponAudio.PlayOneShot(clipSwing);
        
        if (currentWeaponInstance != null)
        {
            currentWeaponInstance.transform.localRotation = Quaternion.Euler(60f, 0, 0); // Fake Swing Animation
            Invoke("ResetWeaponRotation", 0.2f);
        }
        
        // Cálculo de Dano e Range (Arma Branca, Punhos ou Arco)
        float damage = 5f; // Punhos
        float currentAttackRange = attackRange;
        bool isRanged = false;
        
        if (currentWeaponID == 302) damage = 25f; // Faca de caça
        else if (currentWeaponID == 300) damage = 10f; // Machado
        else if (currentWeaponID == 305) 
        {
            // Arco de Sobrevivencia
            if (inventory == null || !inventory.HasItem(110)) // 110 = Flechas
            {
                Debug.LogWarning("[Equipment] Sem flechas!");
                UIManager ui = FindAnyObjectByType<UIManager>();
                if (ui != null) ui.SetLootTooltip("Sem Flechas!");
                return;
            }
            
            inventory.RemoveItem(110, 1);
            damage = 100f; // Dano letal
            currentAttackRange = 25f; // Long Range
            isRanged = true;
            Debug.Log("[Equipment] Flecha disparada!");
        }
        
        // Logica de HitDetection baseada no tipo de arma
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = transform.forward;
        bool hitSomething = false;

        if (isRanged)
        {
            // Tiro de flecha (Raycast fino e longo)
            if (Physics.Raycast(origin, dir, out RaycastHit hit, currentAttackRange))
            {
                string hitType = CheckHit(hit.point, damage, currentWeaponID);
                if (hitType != "none")
                {
                    hitSomething = true;
                    if (weaponAudio != null)
                    {
                        if (hitType == "flesh") weaponAudio.PlayOneShot(clipHitFlesh);
                        else if (hitType == "wood") weaponAudio.PlayOneShot(clipHitWood);
                        else if (hitType == "rock") weaponAudio.PlayOneShot(clipHitRock);
                    }
                }
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(hit.point); // Hit feedback
            }
        }
        else
        {
            // Melee (SphereCast / Cone)
            RaycastHit[] hits = Physics.SphereCastAll(origin, 1.5f, dir, currentAttackRange);
            foreach(var hit in hits)
            {
                string hitType = CheckHit(hit.point, damage, currentWeaponID);
                if (hitType != "none")
                {
                    hitSomething = true;
                    if (weaponAudio != null)
                    {
                        if (hitType == "flesh") weaponAudio.PlayOneShot(clipHitFlesh);
                        else if (hitType == "wood") weaponAudio.PlayOneShot(clipHitWood);
                        else if (hitType == "rock") weaponAudio.PlayOneShot(clipHitRock);
                    }
                    break;
                }
            }
        }
        
        if (hitSomething)
        {
            if (hapticCache != null) hapticCache.TriggerHeavyVibration();
            Debug.Log("[Equipment] Acertou num alvo!");
        }
    }
    
    private string CheckHit(Vector3 hitPoint, float damage, int weaponID)
    {
        if (vultosCache != null && vultosCache.TryDamageVultoAt(hitPoint, damage)) return "flesh";
        
        if (faunaCache != null && faunaCache.TryDamageFaunaAt(hitPoint, damage)) return "flesh";
        
        if (mapRendererCache != null)
        {
            int gridX = Mathf.RoundToInt(hitPoint.x);
            int gridY = Mathf.RoundToInt(hitPoint.z);
            
            if (weaponID == 300 && mapRendererCache.TryHarvestTreeAt(gridX, gridY))
            {
                if (inventory != null)
                {
                    inventory.AddItem(100, 2);
                    Debug.Log("[Equipment] Árvore derrubada! +2 Madeira.");
                }
                return "wood";
            }
            else if (weaponID == 301 && mapRendererCache.TryHarvestRockAt(gridX, gridY))
            {
                if (inventory != null)
                {
                    inventory.AddItem(101, 3);
                    Debug.Log("[Equipment] Rocha destruida! +3 Pedra.");
                }
                return "rock";
            }
            else
            {
                int plantID = mapRendererCache.TryHarvestPlantAt(gridX, gridY);
                if (plantID == 9) // Berry
                {
                    if (inventory != null)
                    {
                        inventory.AddItem(202, 2); // 2x Bagas Silvestres
                        Debug.Log("[Equipment] Colheu +2 Bagas Silvestres.");
                    }
                    return "wood";
                }
                else if (plantID == 10) // Mushroom
                {
                    if (inventory != null)
                    {
                        // 50% de chance de ser Cogumelo Estranho (203) ou Erva (103)
                        int item = UnityEngine.Random.value > 0.5f ? 203 : 103;
                        inventory.AddItem(item, 1);
                        Debug.Log($"[Equipment] Colheu Cogumelo/Erva (ID {item}).");
                    }
                    return "flesh"; // mushy sound
                }
            }
        }
        
        return "none";
    }
    
    private void ResetWeaponRotation()
    {
        if (currentWeaponInstance != null)
        {
            currentWeaponInstance.transform.localRotation = Quaternion.identity;
        }
    }
}
