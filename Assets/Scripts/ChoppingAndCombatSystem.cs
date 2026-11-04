using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ChoppingAndCombatSystem: Sistema AAA de Combate e Coleta de Madeira (Padrão The Forest / Sons of the Forest).
/// Suporta:
/// - Golpe de machado com animação de swing e recoil no PC (Botão Esquerdo / F) e Celular (Botão 🪓)
/// - Corte de árvores com impacto sonoro, partículas de lascas de madeira e tremor de câmera
/// - Sistema de durabilidade de corte da árvore (4 golpes para derrubar)
/// - Coleta automática de madeira por golpe (+1 ou +2 com bônus de machado)
/// - Felling da árvore no 4º golpe: animação de tombamento com som de estalo e queda,
///   gerando toras de madeira no chão para coleta adicional
/// - Impacto em rochas (produz pedra/ferro) e ataque a vultos/animais
/// </summary>
public class ChoppingAndCombatSystem : MonoBehaviour
{
    public static ChoppingAndCombatSystem Instance { get; private set; }

    [Header("Combat Settings")]
    public float attackRange = 2.8f;
    public float attackRadius = 0.30f;
    public float attackCooldown = 0.45f;
    public int baseDamage = 35;

    [Header("Dependencies")]
    public Camera playerCamera;
    public WeaponMotionSway weaponSway;
    public InventoryAdvanced inventory;
    public PlayerController playerController;

    private float lastAttackTime = -10f;
    private AudioSource audioSource;
    private AudioClip chopWoodClip;
    private AudioClip treeFallClip;
    private AudioClip hitRockClip;
    private AudioClip swingWhooshClip;

    // Dicionário de saúde de árvores golpeadas (identificador único por árvore -> golpes restantes)
    private Dictionary<int, int> treeHitsRemaining = new Dictionary<int, int>();

    private void Awake()
    {
        Instance = this;

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
        if (weaponSway == null) weaponSway = FindAnyObjectByType<WeaponMotionSway>();

        SetupAudio();
    }

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
        if (weaponSway == null) weaponSway = FindAnyObjectByType<WeaponMotionSway>();
    }

    private void Update()
    {
        // Entrada no PC: Botão Esquerdo do Mouse ou tecla F
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F))
        {
            // Ignora se o cursor estiver sobre a interface de usuário (como o inventário aberto)
            if (UnityEngine.EventSystems.EventSystem.current == null || 
                !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                TriggerAttack();
            }
        }
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.35f;
        audioSource.volume = 0.75f;

        // Gerar clipes de áudio sintetizados realistas para garantir resposta mesmo sem assets externos
        chopWoodClip = GenerateChopWoodSound();
        treeFallClip = GenerateTreeFallSound();
        hitRockClip = GenerateRockHitSound();
        swingWhooshClip = GenerateSwingWhooshSound();
    }

    /// <summary>
    /// Disparado pelo botão mobile "🪓 GOLPEAR" ou pelo clique no PC.
    /// </summary>
    public void TriggerAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown) return;

        // Se estiver carregando tora no ombro, o ataque arremessa a tora para a frente
        if (LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs > 0)
        {
            LogCarryingSystem.Instance.DropLog(throwForward: true);
            lastAttackTime = Time.time;
            return;
        }

        lastAttackTime = Time.time;

        // 1. Aciona animação de swing do machado
        if (weaponSway == null) weaponSway = FindAnyObjectByType<WeaponMotionSway>();
        if (weaponSway != null)
        {
            weaponSway.TriggerAttackMotion();
        }

        // 2. Toca som do golpe no ar (whoosh)
        if (audioSource != null && swingWhooshClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(swingWhooshClip, 0.40f);
        }

        // 3. Executa a varredura física do golpe à frente da câmera
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.SphereCast(ray, attackRadius, out hit, attackRange, ~0, QueryTriggerInteraction.Ignore))
        {
            ProcessHit(hit);
        }
        else if (Physics.Raycast(ray, out hit, attackRange + 0.3f, ~0, QueryTriggerInteraction.Ignore))
        {
            ProcessHit(hit);
        }
    }

    private void ProcessHit(RaycastHit hit)
    {
        Collider col = hit.collider;
        if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) return;

        string colName = col.gameObject.name.ToLower();
        Transform rootObj = col.transform.root;
        string rootName = rootObj.gameObject.name.ToLower();

        bool isTree = colName.Contains("tree") || colName.Contains("trunk") || colName.Contains("foliage") ||
                      rootName.Contains("tree") || rootName.Contains("pine") || rootName.Contains("arvore") ||
                      col.GetComponentInParent<GalicianProceduralTreeGenerator>() != null;

        bool isRock = colName.Contains("rock") || colName.Contains("stone") || colName.Contains("ruin") ||
                      rootName.Contains("rock") || rootName.Contains("stone") || rootName.Contains("ruin");

        // === 1. CORTE DE ÁRVORE (THE FOREST TIMBER SYSTEM) ===
        if (isTree)
        {
            GameObject singleTree = FindSpecificTreeObject(col);
            if (singleTree != null)
            {
                HandleTreeChop(hit, singleTree);
            }
        }
        // === 2. MINERAÇÃO DE ROCHAS / RUÍNAS ===
        else if (isRock)
        {
            HandleRockStrike(hit);
        }
        // === 3. DANO A ENTIDADES E MONSTROS ===
        else
        {
            HandleEntityStrike(hit);
        }
    }

    /// <summary>
    /// Localiza o GameObject individual da árvore específica atingida pelo machado,
    /// garantindo NUNCA selecionar o MapRenderer ou o container raiz da floresta inteira.
    /// </summary>
    private GameObject FindSpecificTreeObject(Collider col)
    {
        if (col == null) return null;

        // 1. Se possuir o gerador procedural de árvore, retorna a raiz da árvore individual
        GalicianProceduralTreeGenerator gen = col.GetComponentInParent<GalicianProceduralTreeGenerator>();
        if (gen != null) return gen.gameObject;

        // 2. Busca para cima na hierarquia até achar o nó filho do container global (MapRenderer)
        Transform cur = col.transform;
        MapRenderer mr = FindAnyObjectByType<MapRenderer>();
        Transform mapRoot = (mr != null) ? mr.transform : null;

        while (cur != null && cur.parent != null)
        {
            if (cur.parent == mapRoot)
            {
                return cur.gameObject; // Filho direto do MapRenderer = árvore individual
            }

            string pName = cur.parent.name.ToLower();
            if (pName.Contains("map") || pName.Contains("environment") || pName.Contains("scene") || cur.parent == cur.root)
            {
                return cur.gameObject;
            }

            cur = cur.parent;
        }

        return col.gameObject;
    }

    private void HandleTreeChop(RaycastHit hit, GameObject treeRoot)
    {
        // 1. Som de machado na madeira viva
        if (audioSource != null && chopWoodClip != null)
        {
            audioSource.pitch = Random.Range(0.88f, 1.12f);
            audioSource.PlayOneShot(chopWoodClip, 0.85f);
        }

        // 2. Tremor na câmera (Screen Shake)
        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.09f);
        }

        // 3. Feedback tátil no mobile
        HapticManager haptic = FindAnyObjectByType<HapticManager>();
        if (haptic != null) haptic.TriggerLightVibration();

        // 4. Efeito de lascas de madeira voando
        SpawnWoodChipsVFX(hit.point, hit.normal);

        // 5. Gestão de durabilidade e queda da árvore
        int treeId = treeRoot.GetHashCode();
        if (!treeHitsRemaining.ContainsKey(treeId))
        {
            treeHitsRemaining[treeId] = 4; // 4 cortes para derrubar a árvore
        }

        treeHitsRemaining[treeId]--;
        int remaining = treeHitsRemaining[treeId];

        // Adiciona madeira ao inventário
        int woodGained = 1;
        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
        if (inventory != null)
        {
            if (inventory.HasItem(300)) woodGained = 2; // Bônus de machado avançado
            inventory.AddItem(100, woodGained); // 100 = Madeira
        }

        HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
        UIManager ui = FindAnyObjectByType<UIManager>();

        if (remaining <= 0)
        {
            // === ÁRVORE DERRUBADA (FALLING TREE) ===
            treeHitsRemaining.Remove(treeId);

            if (audioSource != null && treeFallClip != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(treeFallClip, 1.0f);
            }

            if (FirstPersonCamera.Instance != null)
            {
                FirstPersonCamera.Instance.AddShake(0.24f);
            }

            // Notificação na UI
            string logMsg = $"+{woodGained} Madeira! [ÁRVORE DERRUBADA]";
            if (hud != null) hud.ShowLootTooltip(logMsg);
            else if (ui != null) ui.SetLootTooltip(logMsg);

            // Gera toras e gravetos extras no chão ao redor do tronco caído
            SpawnFelledTreeLogs(treeRoot.transform.position);

            // Atualiza o mapa nativo e o MapRenderer se for uma árvore do grid
            int gridX = Mathf.RoundToInt(treeRoot.transform.position.x);
            int gridZ = Mathf.RoundToInt(treeRoot.transform.position.z);
            MapRenderer mr = FindAnyObjectByType<MapRenderer>();
            if (mr != null) mr.RemoveInstanceAt(gridX, gridZ, 1);

            // Animação de tombo físico e destruição
            StartCoroutine(AnimateTreeFallAndDestroy(treeRoot));
        }
        else
        {
            // Corte intermediário
            string msg = $"+{woodGained} Madeira! [Golpe {4 - remaining}/4]";
            if (hud != null) hud.ShowLootTooltip(msg);
            else if (ui != null) ui.SetLootTooltip(msg);

            // Leve estremecimento do tronco
            StartCoroutine(ShakeTreeTrunk(treeRoot.transform));
        }
    }

    private void HandleRockStrike(RaycastHit hit)
    {
        if (audioSource != null && hitRockClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.15f);
            audioSource.PlayOneShot(hitRockClip, 0.75f);
        }

        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.06f);
        }

        if (inventory == null) inventory = FindAnyObjectByType<InventoryAdvanced>();
        if (inventory != null)
        {
            inventory.AddItem(101, 1); // 101 = Pedra
            if (Random.value < 0.25f) inventory.AddItem(107, 1); // 25% ferro velho
        }

        HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
        if (hud != null) hud.ShowLootTooltip("+1 Pedra coletada");
    }

    private void HandleEntityStrike(RaycastHit hit)
    {
        float actualDamage = baseDamage;

        // Bônus de Contra-Ataque Crítico após Parry Perfeito
        if (PlayerMeleeCombatDefense.Instance != null && PlayerMeleeCombatDefense.Instance.hasParryBonus)
        {
            actualDamage *= PlayerMeleeCombatDefense.Instance.counterMultiplier;
            PlayerMeleeCombatDefense.Instance.ConsumeParryBonus();

            if (FirstPersonCamera.Instance != null)
            {
                FirstPersonCamera.Instance.AddShake(0.24f);
            }
        }
        else
        {
            if (FirstPersonCamera.Instance != null)
            {
                FirstPersonCamera.Instance.AddShake(0.12f);
            }
        }

        // Mensagem de golpe para entidades
        hit.collider.SendMessageUpwards("TakeDamage", actualDamage, SendMessageOptions.DontRequireReceiver);

        HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
        if (hud != null)
        {
            if (actualDamage > baseDamage)
            {
                hud.ShowLootTooltip($"💥 CRÍTICO DE PARRY! ({actualDamage:F0} Dano)");
            }
            else
            {
                hud.ShowLootTooltip($"Golpe desferido! ({actualDamage:F0} Dano)");
            }
        }
    }

    private System.Collections.IEnumerator ShakeTreeTrunk(Transform treeTrunk)
    {
        if (treeTrunk == null) yield break;
        Vector3 initialPos = treeTrunk.position;
        float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (treeTrunk == null) yield break;
            elapsed += Time.deltaTime;
            float strength = (1f - (elapsed / duration)) * 0.04f;
            Vector3 offset = new Vector3(Random.Range(-strength, strength), 0, Random.Range(-strength, strength));
            treeTrunk.position = initialPos + offset;
            yield return null;
        }

        if (treeTrunk != null) treeTrunk.position = initialPos;
    }

    private System.Collections.IEnumerator AnimateTreeFallAndDestroy(GameObject treeObj)
    {
        if (treeObj == null) yield break;

        // Desativa o colisor para que o jogador possa passar por onde a árvore estava
        Collider col = treeObj.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Vector3 fallAxis = Vector3.Cross(Vector3.up, (treeObj.transform.position - transform.position).normalized);
        if (fallAxis.sqrMagnitude < 0.01f) fallAxis = Vector3.right;

        float elapsed = 0f;
        float duration = 1.4f;
        Quaternion startRot = treeObj.transform.rotation;

        while (elapsed < duration)
        {
            if (treeObj == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float angle = Mathf.Lerp(0f, 85f, t * t); // Aceleração de queda gravitacional

            treeObj.transform.rotation = startRot * Quaternion.AngleAxis(angle, fallAxis);
            yield return null;
        }

        // Aguarda 1 segundo e remove o objeto da árvore tombada
        yield return new WaitForSeconds(1.0f);
        if (treeObj != null) Destroy(treeObj);
    }

    private void SpawnFelledTreeLogs(Vector3 treePos)
    {
        // Spawna 3 toras/galhos colecionáveis ao redor do toco
        int logCount = 3;
        for (int i = 0; i < logCount; i++)
        {
            float ang = i * (Mathf.PI * 2f / logCount) + Random.Range(-0.3f, 0.3f);
            float dist = Random.Range(1.2f, 2.5f);
            Vector3 spawnPos = treePos + new Vector3(Mathf.Cos(ang) * dist, 0.2f, Mathf.Sin(ang) * dist);
            spawnPos.y = TerrainMeshGenerator.GetTerrainHeight(spawnPos.x, spawnPos.z) + 0.15f;

            GameObject logObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            logObj.name = "Felled_Wood_Log";
            logObj.transform.position = spawnPos;
            logObj.transform.localScale = new Vector3(0.22f, 0.55f, 0.22f);
            logObj.transform.rotation = Quaternion.Euler(Random.Range(70f, 90f), Random.Range(0f, 360f), 0);

            Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
            if (barkMat != null) logObj.GetComponent<MeshRenderer>().sharedMaterial = barkMat;

            // Adiciona colisor trigger e componente de coleta
            Collider c = logObj.GetComponent<Collider>();
            if (c != null) c.isTrigger = true;

            ResourcePickup pickup = logObj.AddComponent<ResourcePickup>();
            pickup.itemID = 100; // 100 = Madeira
            pickup.amount = 2;
        }
    }

    private void SpawnWoodChipsVFX(Vector3 pos, Vector3 normal)
    {
        // Se existir VFXPoolManager, utiliza o pool; caso contrário gera partículas dinâmicas
        if (VFXPoolManager.Instance != null)
        {
            VFXPoolManager.Instance.PlayBuildPlaceVFX(pos);
            return;
        }

        GameObject vfxObj = new GameObject("WoodChipsBurst");
        vfxObj.transform.position = pos;
        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.4f;
        main.startLifetime = 0.5f;
        main.startSpeed = 4.5f;
        main.startSize = 0.08f;
        main.startColor = new Color(0.65f, 0.48f, 0.25f);
        main.loop = false;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.rotation = Quaternion.LookRotation(normal).eulerAngles;

        Destroy(vfxObj, 0.8f);
    }

    // =================== SÍNTESE PROCEDURAL DE ÁUDIO REALISTA ===================

    private AudioClip GenerateChopWoodSound()
    {
        int sampleRate = 44100;
        float duration = 0.24f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float env = Mathf.Exp(-18f * t);
            float crackle = Random.Range(-1f, 1f) * Mathf.Exp(-8f * t) * 0.45f;
            float thud = Mathf.Sin(2f * Mathf.PI * 110f * ((float)i / sampleRate)) * env * 0.65f;
            samples[i] = (thud + crackle) * 0.95f;
        }

        AudioClip clip = AudioClip.Create("AxeChopWood_Procedural", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateTreeFallSound()
    {
        int sampleRate = 44100;
        float duration = 1.25f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            // Estalo de madeira rachando no início + estrondo no final
            float crack = (t < 0.35f) ? Random.Range(-0.8f, 0.8f) * Mathf.Sin(t * 120f) : 0f;
            float crash = (t >= 0.35f) ? Mathf.Sin(2f * Mathf.PI * 55f * ((float)i / sampleRate)) * Mathf.Exp(-4f * (t - 0.35f)) : 0f;
            samples[i] = Mathf.Clamp(crack * 0.7f + crash * 0.85f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("TreeFall_Procedural", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateRockHitSound()
    {
        int sampleRate = 44100;
        float duration = 0.18f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float env = Mathf.Exp(-24f * t);
            float clink = Mathf.Sin(2f * Mathf.PI * 720f * ((float)i / sampleRate)) * env * 0.7f;
            float grit = Random.Range(-0.3f, 0.3f) * env;
            samples[i] = clink + grit;
        }

        AudioClip clip = AudioClip.Create("RockHit_Procedural", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateSwingWhooshSound()
    {
        int sampleRate = 44100;
        float duration = 0.16f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float env = Mathf.Sin(t * Mathf.PI);
            float noise = Random.Range(-1f, 1f) * env * 0.35f;
            samples[i] = noise;
        }

        AudioClip clip = AudioClip.Create("AxeWhoosh_Procedural", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}