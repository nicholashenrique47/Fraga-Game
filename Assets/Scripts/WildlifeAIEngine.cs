using UnityEngine;

/// <summary>
/// WildlifeAIEngine: Inteligência Artificial sensorial orgânica para animais selvagens (Lobos, Javalis, Cervos).
/// Suporta:
/// - Cone de visão frontal de 110 graus e audição de passos.
/// - Pavor de fogo e tochas (recuo automático a mais de 5.5m).
/// - Estado de frenesi de javalis quando feridos (investida a 8.5 m/s).
/// - Sistema de carcaça e esfolamento (drop de carne crua 104, pele 114, ossos 113 e tendões 115).
/// </summary>
public class WildlifeAIEngine : MonoBehaviour
{
    public enum CreatureType { IberianWolf, WildBoar, Deer }
    public enum AIState { Idle, Grazing, Stalking, Orbiting, Charging, FleeingFire, Dead }

    [Header("Creature Configuration")]
    public CreatureType creatureType = CreatureType.IberianWolf;
    public AIState currentState = AIState.Idle;
    public int maxHealth = 75;
    public int currentHealth;
    public float walkSpeed = 2.4f;
    public float runSpeed = 6.5f;
    public float chargeSpeed = 8.5f;

    [Header("Sensory Parameters")]
    public float visionAngle = 110f;
    public float visionDistance = 28f;
    public float fearOfFireDistance = 5.5f;
    public float attackReach = 1.6f;
    public int attackDamage = 22;

    [Header("Dependencies")]
    public Transform playerTransform;

    private float stateTimer = 0f;
    private float orbitAngle = 0f;
    private float nextAttackTime = 0f;
    private AudioSource audioSource;
    private AudioClip growlClip;
    private AudioClip attackClip;
    private AudioClip deathClip;

    private void Awake()
    {
        currentHealth = maxHealth;
        SetupAudio();
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player_CameraParent");
            if (player != null) playerTransform = player.transform;
        }

        orbitAngle = Random.Range(0f, 360f);
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.85f;
        audioSource.minDistance = 3f;
        audioSource.maxDistance = 30f;

        growlClip = GenerateGrowlSound();
        attackClip = GenerateAttackSound();
        deathClip = GenerateDeathSound();
    }

    private void Update()
    {
        if (currentState == AIState.Dead) return;
        if (playerTransform == null) return;

        stateTimer += Time.deltaTime;

        // 1. Checa proximidade com fogo ou tocha
        if (CheckForFireNear())
        {
            currentState = AIState.FleeingFire;
        }

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case AIState.Idle:
                float stealthMult = FragaStealthSystem.Instance != null ? FragaStealthSystem.Instance.GetDetectionMultiplier() : 1f;
                float effectiveVision = visionDistance * stealthMult;
                bool heardPlayer = FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.Noise01 > 0.68f && distToPlayer < 14f;
                if ((distToPlayer < effectiveVision && CanSeePlayer(distToPlayer)) || heardPlayer)
                {
                    currentState = (creatureType == CreatureType.Deer) ? AIState.FleeingFire : AIState.Orbiting;
                    if (audioSource != null && growlClip != null) audioSource.PlayOneShot(growlClip, 0.7f);
                }
                break;

            case AIState.Orbiting:
                // Orbita o jogador preparando o bote
                orbitAngle += Time.deltaTime * 35f;
                float rad = orbitAngle * Mathf.Deg2Rad;
                Vector3 targetOrbit = playerTransform.position + new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * 6.5f;
                transform.position = Vector3.MoveTowards(transform.position, targetOrbit, Time.deltaTime * walkSpeed);
                transform.LookAt(playerTransform.position);

                if (stateTimer > 4.5f && Time.time > nextAttackTime)
                {
                    currentState = AIState.Charging;
                    stateTimer = 0f;
                    if (audioSource != null && attackClip != null) audioSource.PlayOneShot(attackClip, 0.85f);
                }
                break;

            case AIState.Charging:
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, Time.deltaTime * chargeSpeed);
                transform.LookAt(playerTransform.position);

                if (distToPlayer <= attackReach)
                {
                    playerTransform.SendMessageUpwards("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
                    nextAttackTime = Time.time + 3.0f;
                    currentState = AIState.Orbiting;
                    stateTimer = 0f;
                }
                else if (stateTimer > 3.0f)
                {
                    currentState = AIState.Orbiting;
                    stateTimer = 0f;
                }
                break;

            case AIState.FleeingFire:
                Vector3 fleeDir = (transform.position - playerTransform.position).normalized;
                transform.position += fleeDir * Time.deltaTime * runSpeed;
                transform.rotation = Quaternion.LookRotation(fleeDir);

                if (distToPlayer > 18f)
                {
                    currentState = AIState.Idle;
                }
                break;
        }

        // Mantém preso ao relevo do terreno
        float terrY = TerrainMeshGenerator.GetTerrainHeight(transform.position.x, transform.position.z);
        transform.position = new Vector3(transform.position.x, terrY, transform.position.z);
    }

    public void TakeDamage(int damage)
    {
        if (currentState == AIState.Dead) return;

        currentHealth -= damage;

        if (audioSource != null && attackClip != null)
        {
            audioSource.PlayOneShot(attackClip, 0.8f);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Javali entra em estado de frenesi ao levar dano
            if (creatureType == CreatureType.WildBoar)
            {
                currentState = AIState.Charging;
                stateTimer = 0f;
            }
            else
            {
                // Lobo ou cervo tenta correr temporariamente
                currentState = AIState.FleeingFire;
            }
        }
    }

    private void Die()
    {
        currentState = AIState.Dead;

        if (audioSource != null && deathClip != null)
        {
            audioSource.PlayOneShot(deathClip, 1.0f);
        }

        // Tombamento visual do corpo
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 85f);

        // Gera carcaça com recursos (Carne 104, Pele 114, Ossos 113)
        SpawnCarcassDrops();

        HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
        if (hud != null) hud.ShowLootTooltip($"<color=#55FF88>Presa abatida: {creatureType}!</color>");

        Destroy(gameObject, 45f);
    }

    private void SpawnCarcassDrops()
    {
        Vector3 pos = transform.position + Vector3.up * 0.2f;
        int meatQty = (creatureType == CreatureType.WildBoar) ? 3 : 2;

        if (LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(104, meatQty, pos + Vector3.right * 0.25f);
            LootManager.Instance.DropItem(114, 1, pos - Vector3.right * 0.25f);
            return;
        }

        // Drop 1: Carne Crua (Item 104)
        GameObject meat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        meat.name = "Harvest_RawMeat";
        meat.transform.position = pos + Random.insideUnitSphere * 0.4f;
        meat.transform.localScale = new Vector3(0.2f, 0.15f, 0.2f);
        ResourcePickup p1 = meat.AddComponent<ResourcePickup>();
        p1.itemID = 104;
        p1.amount = meatQty;

        // Drop 2: Pele Animal (Item 114)
        GameObject pelt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pelt.name = "Harvest_AnimalPelt";
        pelt.transform.position = pos + Random.insideUnitSphere * 0.4f;
        pelt.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);
        ResourcePickup p2 = pelt.AddComponent<ResourcePickup>();
        p2.itemID = 114;
        p2.amount = 1;
    }

    private bool CanSeePlayer(float distToPlayer)
    {
        if (distToPlayer < 4.5f) return true;
        Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, toPlayer);
        float fovHalf = visionAngle * 0.5f;
        if (FragaStealthSystem.Instance != null && FragaStealthSystem.Instance.IsHidden) fovHalf *= 0.65f;
        return angle < fovHalf;
    }

    private bool CheckForFireNear()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, fearOfFireDistance);
        foreach (var c in hits)
        {
            string n = c.gameObject.name.ToLower();
            if (n.Contains("torch") || n.Contains("fire") || n.Contains("bonfire") || n.Contains("campfire"))
            {
                return true;
            }
        }
        return false;
    }

    // =================== SÍNTESE PROCEDURAL DE ÁUDIO ===================

    private AudioClip GenerateGrowlSound()
    {
        int sampleRate = 44100;
        float dur = 0.55f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = (creatureType == CreatureType.WildBoar) ? 75f : 120f;
            float growl = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * (Random.Range(-0.4f, 0.4f) + 0.6f) * Mathf.Sin(t * Mathf.PI) * 0.6f;
            samples[i] = growl;
        }

        AudioClip clip = AudioClip.Create("CreatureGrowl_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateAttackSound()
    {
        int sampleRate = 44100;
        float dur = 0.35f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float snap = Mathf.Sin(2f * Mathf.PI * 220f * ((float)i / sampleRate)) * Mathf.Exp(-12f * t) * 0.75f;
            samples[i] = snap;
        }

        AudioClip clip = AudioClip.Create("CreatureAttack_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateDeathSound()
    {
        int sampleRate = 44100;
        float dur = 0.60f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float groan = Mathf.Sin(2f * Mathf.PI * (160f - t * 80f) * ((float)i / sampleRate)) * Mathf.Exp(-4f * t) * 0.7f;
            samples[i] = groan;
        }

        AudioClip clip = AudioClip.Create("CreatureDeath_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}