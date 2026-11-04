using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// BlueprintPlacementManager: Sistema de Construção Holográfica Livre com Alocação Tora por Tora (Padrão The Forest).
/// </summary>
public class BlueprintPlacementManager : MonoBehaviour
{
    public static BlueprintPlacementManager Instance { get; private set; }

    [System.Serializable]
    public class StructureProject
    {
        public string structureName;
        public int requiredLogs;
        public int requiredStones;
        public GameObject completedPrefab;
    }

    [Header("Building Blueprints")]
    public StructureProject[] availableProjects;

    private int selectedProjectIndex = 0;
    private bool isPlacingGhost = false;
    private GameObject activeGhostObj;
    private float ghostRotationY = 0f;

    private AudioSource audioSource;
    private AudioClip hammerHitClip;
    private AudioClip placeGhostClip;
    private AudioClip completeBuildClip;

    private void Awake()
    {
        Instance = this;
        SetupDefaultProjects();
        SetupAudio();
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.35f;
        audioSource.volume = 0.85f;

        hammerHitClip = GenerateHammerHitSound();
        placeGhostClip = GeneratePlaceGhostSound();
        completeBuildClip = GenerateCompleteBuildSound();
    }

    private void SetupDefaultProjects()
    {
        availableProjects = new StructureProject[]
        {
            new StructureProject { structureName = "Refúgio de Fentos (Lean-To)", requiredLogs = 4, requiredStones = 2 },
            new StructureProject { structureName = "Cabana de Toras (Log Cabin)", requiredLogs = 10, requiredStones = 6 },
            new StructureProject { structureName = "Paliçada Pontiaguda (Defesa)", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Secador de Alimentos", requiredLogs = 2, requiredStones = 1 },
            new StructureProject { structureName = "Coletor de Água da Chuva", requiredLogs = 2, requiredStones = 3 }
        };
    }

    private void Update()
    {
        // Tecla B no PC para alternar modo de construção de blueprint
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (isPlacingGhost) CancelPlacement();
            else StartPlacement(selectedProjectIndex);
        }

        if (isPlacingGhost && activeGhostObj != null)
        {
            UpdateGhostPosition();

            // Rotação com teclas R ou Q/E
            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.Q)) ghostRotationY -= Time.deltaTime * 90f;
            if (Input.GetKey(KeyCode.E)) ghostRotationY += Time.deltaTime * 90f;

            // Botão Esquerdo ou toque para fixar no chão
            if (Input.GetMouseButtonDown(0))
            {
                ConfirmPlacement();
            }
            // Tecla ESC ou Botão Direito para cancelar
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
            }
        }
    }

    public void StartPlacement(int projectIndex)
    {
        selectedProjectIndex = Mathf.Clamp(projectIndex, 0, availableProjects.Length - 1);
        isPlacingGhost = true;
        ghostRotationY = transform.eulerAngles.y;

        if (activeGhostObj != null) Destroy(activeGhostObj);

        activeGhostObj = CreateGhostVisual(availableProjects[selectedProjectIndex].structureName);

        HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
        if (hud != null) hud.ShowLootTooltip($"Modo Construção: {availableProjects[selectedProjectIndex].structureName} [Clique p/ Fixar]");
    }

    private void UpdateGhostPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 targetPos;

        if (Physics.Raycast(ray, out RaycastHit hit, 7.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            targetPos = hit.point;
        }
        else
        {
            targetPos = cam.transform.position + cam.transform.forward * 4.5f;
            targetPos.y = TerrainMeshGenerator.GetTerrainHeight(targetPos.x, targetPos.z);
        }

        activeGhostObj.transform.position = targetPos;
        activeGhostObj.transform.rotation = Quaternion.Euler(0, ghostRotationY, 0);
    }

    public void ConfirmPlacement()
    {
        if (activeGhostObj == null) return;

        StructureProject proj = availableProjects[selectedProjectIndex];
        Vector3 pos = activeGhostObj.transform.position;
        Quaternion rot = activeGhostObj.transform.rotation;

        Destroy(activeGhostObj);
        isPlacingGhost = false;

        // Cria a blueprint física colocada no mundo
        GameObject blueprintObj = CreateGhostVisual(proj.structureName);
        blueprintObj.name = $"Blueprint_{proj.structureName}";
        blueprintObj.transform.position = pos;
        blueprintObj.transform.rotation = rot;

        WorldBlueprintSite site = blueprintObj.AddComponent<WorldBlueprintSite>();
        site.structureName = proj.structureName;
        site.neededLogs = proj.requiredLogs;
        site.neededStones = proj.requiredStones;

        if (audioSource != null && placeGhostClip != null)
        {
            audioSource.PlayOneShot(placeGhostClip, 0.75f);
        }

        HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
        if (hud != null) hud.ShowLootTooltip($"Projeto fixado! Adicione Toras ({proj.requiredLogs}) e Pedras ({proj.requiredStones})");
    }

    public void CancelPlacement()
    {
        isPlacingGhost = false;
        if (activeGhostObj != null) Destroy(activeGhostObj);
    }

    private GameObject CreateGhostVisual(string structureName)
    {
        GameObject ghost = new GameObject($"Ghost_{structureName}");
        Material ghostMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ghostMat.color = new Color(0.20f, 0.75f, 1.0f, 0.45f);

        // Geometria procedural de moldura (postes e vigas rústicas)
        int posts = 4;
        for (int i = 0; i < posts; i++)
        {
            float x = (i % 2 == 0) ? -1.2f : 1.2f;
            float z = (i < 2) ? -1.0f : 1.0f;

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(post.GetComponent<Collider>());
            post.transform.SetParent(ghost.transform, false);
            post.transform.localPosition = new Vector3(x, 1.1f, z);
            post.transform.localScale = new Vector3(0.18f, 1.1f, 0.18f);
            post.GetComponent<MeshRenderer>().sharedMaterial = ghostMat;
        }

        // Teto de ramos
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(roof.GetComponent<Collider>());
        roof.transform.SetParent(ghost.transform, false);
        roof.transform.localPosition = new Vector3(0, 2.2f, 0);
        roof.transform.localScale = new Vector3(2.6f, 0.12f, 2.2f);
        roof.transform.localRotation = Quaternion.Euler(14f, 0, 0);
        roof.GetComponent<MeshRenderer>().sharedMaterial = ghostMat;

        return ghost;
    }

    public void PlayHammerSound(Vector3 pos)
    {
        if (audioSource != null && hammerHitClip != null)
        {
            audioSource.pitch = Random.Range(0.88f, 1.12f);
            audioSource.PlayOneShot(hammerHitClip, 0.85f);
        }
    }

    public void PlayCompleteSound(Vector3 pos)
    {
        if (audioSource != null && completeBuildClip != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(completeBuildClip, 1.0f);
        }
    }

    // =================== SÍNTESE PROCEDURAL DE ÁUDIO ===================

    private AudioClip GenerateHammerHitSound()
    {
        int sampleRate = 44100;
        float dur = 0.22f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float woodThump = Mathf.Sin(2f * Mathf.PI * 140f * ((float)i / sampleRate)) * Mathf.Exp(-16f * t) * 0.75f;
            float nailClink = Mathf.Sin(2f * Mathf.PI * 950f * ((float)i / sampleRate)) * Mathf.Exp(-32f * t) * 0.45f;
            samples[i] = woodThump + nailClink;
        }

        AudioClip clip = AudioClip.Create("HammerHit_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GeneratePlaceGhostSound()
    {
        int sampleRate = 44100;
        float dur = 0.25f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float chime = Mathf.Sin(2f * Mathf.PI * 520f * ((float)i / sampleRate)) * Mathf.Exp(-12f * t) * 0.6f;
            samples[i] = chime;
        }

        AudioClip clip = AudioClip.Create("PlaceGhost_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateCompleteBuildSound()
    {
        int sampleRate = 44100;
        float dur = 0.45f;
        int count = (int)(sampleRate * dur);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float horn = Mathf.Sin(2f * Mathf.PI * 220f * ((float)i / sampleRate)) * Mathf.Exp(-5f * t) * 0.5f;
            float triumph = Mathf.Sin(2f * Mathf.PI * 440f * ((float)i / sampleRate)) * Mathf.Exp(-6f * t) * 0.4f;
            samples[i] = horn + triumph;
        }

        AudioClip clip = AudioClip.Create("CompleteBuild_Procedural", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// WorldBlueprintSite: Sítio de construção no mundo onde o jogador insere recursos tora por tora.
/// </summary>
public class WorldBlueprintSite : MonoBehaviour
{
    public string structureName;
    public int neededLogs;
    public int neededStones;

    public int currentLogs = 0;
    public int currentStones = 0;

    private float interactionRadius = 3.2f;

    private void Update()
    {
        // Se o jogador estiver próximo e pressionar E ou botão de interagir
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) player = GameObject.Find("Player_CameraParent");
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= interactionRadius)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                TryDepositResource(player);
            }
        }
    }

    public bool TryDepositResource(GameObject player)
    {
        // 1. Tenta depositar tora carregada no ombro
        LogCarryingSystem logCarry = LogCarryingSystem.Instance;
        if (logCarry != null && logCarry.currentLogs > 0 && currentLogs < neededLogs)
        {
            logCarry.currentLogs--;
            currentLogs++;
            OnResourceAdded("Madeira");
            return true;
        }

        // 2. Tenta depositar pedra do inventário
        InventoryAdvanced inv = FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null && currentStones < neededStones && inv.HasItem(101))
        {
            inv.RemoveItem(101, 1);
            currentStones++;
            OnResourceAdded("Pedra");
            return true;
        }

        // 3. Tenta depositar madeira do inventário se não tiver no ombro
        if (inv != null && currentLogs < neededLogs && inv.HasItem(100))
        {
            inv.RemoveItem(100, 1);
            currentLogs++;
            OnResourceAdded("Madeira");
            return true;
        }

        return false;
    }

    private void OnResourceAdded(string resourceName)
    {
        if (BlueprintPlacementManager.Instance != null)
        {
            BlueprintPlacementManager.Instance.PlayHammerSound(transform.position);
        }

        HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
        if (hud != null)
        {
            hud.ShowLootTooltip($"+1 {resourceName} alocado em {structureName}! (Toras: {currentLogs}/{neededLogs} | Pedras: {currentStones}/{neededStones})");
        }

        if (FirstPersonCamera.Instance != null)
        {
            FirstPersonCamera.Instance.AddShake(0.06f);
        }

        if (currentLogs >= neededLogs && currentStones >= neededStones)
        {
            CompleteStructure();
        }
    }

    private void CompleteStructure()
    {
        if (BlueprintPlacementManager.Instance != null)
        {
            BlueprintPlacementManager.Instance.PlayCompleteSound(transform.position);
        }

        // Torna a estrutura sólida e permanente
        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark");
        if (barkMat == null) barkMat = Resources.Load<Material>("Materials/Wood_Bark");

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
        {
            if (barkMat != null) r.sharedMaterial = barkMat;
            // Adiciona colisor sólido para proteção física
            if (r.GetComponent<Collider>() == null)
            {
                r.gameObject.AddComponent<BoxCollider>();
            }
        }

        HUDSurvivalOverlay hud = FindAnyObjectByType<HUDSurvivalOverlay>();
        if (hud != null)
        {
            hud.ShowLootTooltip($"<color=#00FF88>★ CONSTRUÇÃO COMPLETA: {structureName}!</color>");
        }

        Destroy(this); // Remove o componente do sítio, tornando o objeto um abrigo estático permanente
    }
}