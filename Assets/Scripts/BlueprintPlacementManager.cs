using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// BlueprintPlacementManager: Construcao Holografica Livre com seletor de projetos (Tecla B / Botao Obra)
/// e insercao Tora por Tora via InteractionDetector (Zero-GC, Zero-Find no Update).
/// Cada estrutura concluida ganha funcao real estilo The Forest: Abrigo (Dormir + Salvar),
/// Secador de Alimentos (Charque nao-perecivel), Coletor de Chuva (reabastece Cantil) e Palicada defensiva.
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
    public bool IsPlacingGhost => isPlacingGhost;
    public bool snapEnabled = true;

    private GameObject activeGhostObj;
    private float ghostRotationY = 0f;
    private GameObject selectorStripObj;
    private GameObject placementBarObj;
    private Text snapBtnLabel;

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
            new StructureProject { structureName = "Refugio de Fentos", requiredLogs = 4, requiredStones = 2 },
            new StructureProject { structureName = "Cabana de Toras", requiredLogs = 10, requiredStones = 6 },
            new StructureProject { structureName = "Palicada Pontiaguda", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Secador de Alimentos", requiredLogs = 2, requiredStones = 1 },
            new StructureProject { structureName = "Coletor de Chuva", requiredLogs = 2, requiredStones = 3 },
            new StructureProject { structureName = "Piso de Madeira", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Parede de Toras", requiredLogs = 4, requiredStones = 0 },
            new StructureProject { structureName = "Parede com Porta", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Teto de Madeira", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Escada de Toras", requiredLogs = 3, requiredStones = 0 },
            new StructureProject { structureName = "Cesto de Toras", requiredLogs = 2, requiredStones = 1 },
            new StructureProject { structureName = "Bau de Base", requiredLogs = 2, requiredStones = 1 }
        };
    }

    public void ToggleBuildMenuOrPlacement()
    {
        if (isPlacingGhost)
        {
            CancelPlacement();
            return;
        }

        if (selectorStripObj != null && selectorStripObj.activeSelf)
        {
            selectorStripObj.SetActive(false);
            return;
        }

        ShowBuildSelectorStrip();
    }

    private void ShowBuildSelectorStrip()
    {
        if (selectorStripObj == null) BuildSelectorUI();
        if (selectorStripObj != null) selectorStripObj.SetActive(true);
    }

    private void BuildSelectorUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        selectorStripObj = new GameObject("BlueprintSelectorStrip");
        selectorStripObj.transform.SetParent(canvas.transform, false);
        RectTransform rt = selectorStripObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 112f);
        rt.sizeDelta = new Vector2(920f, 134f);

        Image bg = selectorStripObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.08f, 0.12f, 0.90f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        int cols = 6;
        float cardW = 142f;
        float cardH = 54f;
        float gapX = 8f;
        float gapY = 8f;
        float totalW = cols * cardW + (cols - 1) * gapX;
        float startX = -totalW * 0.5f + cardW * 0.5f;

        for (int i = 0; i < availableProjects.Length; i++)
        {
            int projIdx = i;
            StructureProject p = availableProjects[i];
            int col = i % cols;
            int row = i / cols;

            GameObject card = new GameObject($"Proj_{i}");
            card.transform.SetParent(selectorStripObj.transform, false);
            RectTransform cRT = card.AddComponent<RectTransform>();
            float yPos = (row == 0) ? (cardH * 0.5f + gapY * 0.5f) : -(cardH * 0.5f + gapY * 0.5f);
            cRT.anchoredPosition = new Vector2(startX + col * (cardW + gapX), yPos);
            cRT.sizeDelta = new Vector2(cardW, cardH);

            Image cImg = card.AddComponent<Image>();
            cImg.sprite = FragaProIconLibrary.GetSlotFrameSprite();
            cImg.type = Image.Type.Sliced;
            cImg.color = new Color(0.12f, 0.20f, 0.28f, 0.92f);

            card.AddComponent<Button>().onClick.AddListener(() =>
            {
                selectorStripObj.SetActive(false);
                StartPlacement(projIdx);
            });

            GameObject tObj = new GameObject("Label");
            tObj.transform.SetParent(card.transform, false);
            RectTransform tRT = tObj.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(4f, 4f);
            tRT.offsetMax = new Vector2(-4f, -4f);
            Text txt = tObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 10;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = $"{p.structureName}\n({p.requiredLogs} Mad / {p.requiredStones} Ped)";
            txt.raycastTarget = false;
        }
    }

    private void ShowPlacementBar(bool show)
    {
        if (show && placementBarObj == null) BuildPlacementBarUI();
        if (placementBarObj != null) placementBarObj.SetActive(show);
    }

    private void BuildPlacementBarUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        placementBarObj = new GameObject("BlueprintPlacementBar");
        placementBarObj.transform.SetParent(canvas.transform, false);
        RectTransform rt = placementBarObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 118f);
        rt.sizeDelta = new Vector2(664f, 56f);

        Image bg = placementBarObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.08f, 0.12f, 0.90f);

        CreatePlacementBtn(placementBarObj.transform, "Btn_RotL", "GIRAR -", FragaProIconLibrary.ControlIcon.RotateLeft,
            new Vector2(-258f, 0f), 112f, new Color(0.16f, 0.28f, 0.40f, 0.94f), () => RotateGhost(-22.5f));
        CreatePlacementBtn(placementBarObj.transform, "Btn_RotR", "GIRAR +", FragaProIconLibrary.ControlIcon.RotateRight,
            new Vector2(-138f, 0f), 112f, new Color(0.16f, 0.28f, 0.40f, 0.94f), () => RotateGhost(22.5f));
        snapBtnLabel = CreatePlacementBtn(placementBarObj.transform, "Btn_Snap", snapEnabled ? "SNAP: ON" : "SNAP: OFF", FragaProIconLibrary.ControlIcon.BuildHammer,
            new Vector2(-16f, 0f), 118f, new Color(0.20f, 0.36f, 0.46f, 0.94f), ToggleSnap);
        CreatePlacementBtn(placementBarObj.transform, "Btn_Confirm", "CONFIRMAR", FragaProIconLibrary.ControlIcon.ConfirmCheck,
            new Vector2(120f, 0f), 136f, new Color(0.16f, 0.56f, 0.36f, 0.96f), ConfirmPlacement);
        CreatePlacementBtn(placementBarObj.transform, "Btn_Cancel", "CANCELAR", FragaProIconLibrary.ControlIcon.CancelCross,
            new Vector2(256f, 0f), 114f, new Color(0.58f, 0.18f, 0.18f, 0.94f), CancelPlacement);
    }

    private Text CreatePlacementBtn(Transform parent, string name, string label, FragaProIconLibrary.ControlIcon icon,
        Vector2 pos, float width, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 40f);

        Image img = obj.AddComponent<Image>();
        img.sprite = FragaProIconLibrary.GetPillSprite();
        img.type = Image.Type.Sliced;
        img.color = bgColor;
        obj.AddComponent<Button>().onClick.AddListener(onClick);

        GameObject icoObj = new GameObject("Icon");
        icoObj.transform.SetParent(obj.transform, false);
        RectTransform iRT = icoObj.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(10f, 0f);
        iRT.sizeDelta = new Vector2(20f, 20f);
        Image icoImg = icoObj.AddComponent<Image>();
        icoImg.sprite = FragaProIconLibrary.GetControlIcon(icon);
        icoImg.preserveAspect = true;
        icoImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(obj.transform, false);
        RectTransform tRT = txtObj.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(32f, 0f);
        tRT.offsetMax = new Vector2(-6f, 0f);
        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 11;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;
        return txt;
    }

    public void ToggleSnap()
    {
        snapEnabled = !snapEnabled;
        if (snapBtnLabel != null)
            snapBtnLabel.text = snapEnabled ? "SNAP: ON" : "SNAP: OFF";
        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip(snapEnabled ? "Encaixe automatico (Snap) ATIVADO" : "Encaixe automatico (Snap) DESATIVADO");
    }

    public void RotateGhost(float deltaDegrees)
    {
        ghostRotationY += deltaDegrees;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B) && (SurvivalInventoryCraftUI.Instance == null || !SurvivalInventoryCraftUI.Instance.IsOpen))
        {
            ToggleBuildMenuOrPlacement();
        }

        if (isPlacingGhost && activeGhostObj != null)
        {
            if (Input.GetKeyDown(KeyCode.G)) ToggleSnap();
            UpdateGhostPosition();

            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.Q)) ghostRotationY -= Time.deltaTime * 90f;
            if (Input.GetKey(KeyCode.E)) ghostRotationY += Time.deltaTime * 90f;

            bool overUI = FragaInputRouter.IsAnyPointerOverUI();
            if (!overUI && Input.touchCount == 0 && Input.GetMouseButtonDown(0))
            {
                ConfirmPlacement();
            }
            if (Input.GetKeyDown(KeyCode.Escape) || (Input.touchCount == 0 && Input.GetMouseButtonDown(1)))
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
        ShowPlacementBar(true);

        if (HUDSurvivalOverlay.Instance != null)
        {
            bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
            string hint = touchActive
                ? $"Construcao: {availableProjects[selectedProjectIndex].structureName} — Mire e toque em CONFIRMAR"
                : $"Construcao: {availableProjects[selectedProjectIndex].structureName} [Q/E Girar | Clique Fixar | Dir Cancelar]";
            HUDSurvivalOverlay.Instance.ShowLootTooltip(hint);
        }
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

        float finalYaw = ghostRotationY;
        if (snapEnabled)
        {
            finalYaw = Mathf.Round(finalYaw / 15f) * 15f;
            string projName = availableProjects[selectedProjectIndex].structureName;
            if (projName.Contains("Palicada"))
            {
                Transform bestNeighbor = FindClosestBuildingMatch(targetPos, 12.25f, "Palicada", null);

                if (bestNeighbor != null)
                {
                    Vector3 sideDir = bestNeighbor.right;
                    float dot = Vector3.Dot(targetPos - bestNeighbor.position, sideDir);
                    targetPos = bestNeighbor.position + sideDir * (dot >= 0f ? 2.15f : -2.15f);
                    targetPos.y = TerrainMeshGenerator.GetTerrainHeight(targetPos.x, targetPos.z);
                    finalYaw = bestNeighbor.eulerAngles.y;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else if (projName.Contains("Piso"))
            {
                Transform neighbor = FindClosestBuildingMatch(targetPos, 16f, "Piso", null);
                if (neighbor != null)
                {
                    Vector3 localDelta = neighbor.InverseTransformPoint(targetPos);
                    Vector3 offset = Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z)
                        ? neighbor.right * (localDelta.x >= 0f ? 3.0f : -3.0f)
                        : neighbor.forward * (localDelta.z >= 0f ? 3.0f : -3.0f);
                    targetPos = neighbor.position + offset;
                    finalYaw = neighbor.eulerAngles.y;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else if (projName.Contains("Parede") || projName.Contains("Porta"))
            {
                Transform floorSnap = FindClosestBuildingMatch(targetPos, 11.5f, "Piso", null);
                Transform wallSnap = FindClosestBuildingMatch(targetPos, 12.25f, "Parede", "Porta");
                if (floorSnap != null)
                {
                    Vector3 localDelta = floorSnap.InverseTransformPoint(targetPos);
                    if (Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z))
                    {
                        targetPos = floorSnap.position + floorSnap.right * (localDelta.x >= 0f ? 1.48f : -1.48f);
                        finalYaw = floorSnap.eulerAngles.y + 90f;
                    }
                    else
                    {
                        targetPos = floorSnap.position + floorSnap.forward * (localDelta.z >= 0f ? 1.48f : -1.48f);
                        finalYaw = floorSnap.eulerAngles.y;
                    }
                    targetPos.y += 0.14f;
                }
                else if (wallSnap != null)
                {
                    float dot = Vector3.Dot(targetPos - wallSnap.position, wallSnap.right);
                    targetPos = wallSnap.position + wallSnap.right * (dot >= 0f ? 3.0f : -3.0f);
                    finalYaw = wallSnap.eulerAngles.y;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else if (projName.Contains("Teto"))
            {
                Transform roofSnap = FindClosestBuildingMatch(targetPos, 14f, "Teto", null);
                Transform floorSnap = FindClosestBuildingMatch(targetPos, 11.5f, "Piso", null);
                if (roofSnap != null)
                {
                    Vector3 localDelta = roofSnap.InverseTransformPoint(targetPos);
                    Vector3 offset = Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z)
                        ? roofSnap.right * (localDelta.x >= 0f ? 3.0f : -3.0f)
                        : roofSnap.forward * (localDelta.z >= 0f ? 3.0f : -3.0f);
                    targetPos = roofSnap.position + offset;
                    finalYaw = roofSnap.eulerAngles.y;
                }
                else if (floorSnap != null)
                {
                    targetPos = floorSnap.position + Vector3.up * 2.52f;
                    finalYaw = floorSnap.eulerAngles.y;
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else if (projName.Contains("Escada"))
            {
                Transform floorSnap = FindClosestBuildingMatch(targetPos, 12.25f, "Piso", null);
                if (floorSnap != null)
                {
                    Vector3 localDelta = floorSnap.InverseTransformPoint(targetPos);
                    if (Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z))
                    {
                        float s = localDelta.x >= 0f ? 1f : -1f;
                        targetPos = floorSnap.position + floorSnap.right * (s * 2.1f);
                        finalYaw = floorSnap.eulerAngles.y + (s > 0f ? -90f : 90f);
                    }
                    else
                    {
                        float s = localDelta.z >= 0f ? 1f : -1f;
                        targetPos = floorSnap.position + floorSnap.forward * (s * 2.1f);
                        finalYaw = floorSnap.eulerAngles.y + (s > 0f ? 180f : 0f);
                    }
                    targetPos.y = TerrainMeshGenerator.GetTerrainHeight(targetPos.x, targetPos.z);
                }
                else
                {
                    SnapToGridXZ(ref targetPos);
                }
            }
            else
            {
                SnapToGridXZ(ref targetPos);
            }
        }

        activeGhostObj.transform.position = targetPos;
        activeGhostObj.transform.rotation = Quaternion.Euler(0, finalYaw, 0);
    }

    private static void SnapToGridXZ(ref Vector3 pos)
    {
        pos.x = Mathf.Round(pos.x * 2f) * 0.5f;
        pos.z = Mathf.Round(pos.z * 2f) * 0.5f;
        pos.y = TerrainMeshGenerator.GetTerrainHeight(pos.x, pos.z);
    }

    private static Transform FindClosestBuildingMatch(Vector3 targetPos, float maxDistSq, string filterA, string filterB)
    {
        Transform best = null;
        float bestSq = maxDistSq;
        var structs = StructureHealth.AllStructures;
        for (int i = 0; i < structs.Count; i++)
        {
            var s = structs[i];
            if (s == null) continue;
            if (!s.structureName.Contains(filterA) && (filterB == null || !s.structureName.Contains(filterB))) continue;
            float dSq = (s.transform.position - targetPos).sqrMagnitude;
            if (dSq < bestSq) { bestSq = dSq; best = s.transform; }
        }
        var sites = WorldBlueprintSite.AllSites;
        for (int i = 0; i < sites.Count; i++)
        {
            var st = sites[i];
            if (st == null) continue;
            if (!st.structureName.Contains(filterA) && (filterB == null || !st.structureName.Contains(filterB))) continue;
            float dSq = (st.transform.position - targetPos).sqrMagnitude;
            if (dSq < bestSq) { bestSq = dSq; best = st.transform; }
        }
        return best;
    }

    public void ConfirmPlacement()
    {
        if (activeGhostObj == null) return;

        StructureProject proj = availableProjects[selectedProjectIndex];
        Vector3 pos = activeGhostObj.transform.position;
        Quaternion rot = activeGhostObj.transform.rotation;

        Destroy(activeGhostObj);
        isPlacingGhost = false;
        ShowPlacementBar(false);

        GameObject blueprintObj = CreateGhostVisual(proj.structureName);
        blueprintObj.name = $"Blueprint_{proj.structureName}";
        blueprintObj.transform.position = pos;
        blueprintObj.transform.rotation = rot;

        BoxCollider siteCol = blueprintObj.AddComponent<BoxCollider>();
        siteCol.center = new Vector3(0f, 1.05f, 0f);
        siteCol.size = new Vector3(2.8f, 2.3f, 2.5f);
        siteCol.isTrigger = true;

        WorldBlueprintSite site = blueprintObj.AddComponent<WorldBlueprintSite>();
        site.structureName = proj.structureName;
        site.neededLogs = proj.requiredLogs;
        site.neededStones = proj.requiredStones;

        if (audioSource != null && placeGhostClip != null)
            audioSource.PlayOneShot(placeGhostClip, 0.75f);

        if (HUDSurvivalOverlay.Instance != null)
        {
            bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
            HUDSurvivalOverlay.Instance.ShowLootTooltip(touchActive
                ? "Projeto fixado! Aproxime-se e toque em USAR para alocar Toras/Pedras."
                : "Projeto fixado! Aproxime-se e use [E] para alocar Toras/Pedras.");
        }
    }

    public void CancelPlacement()
    {
        isPlacingGhost = false;
        if (activeGhostObj != null) Destroy(activeGhostObj);
        if (selectorStripObj != null) selectorStripObj.SetActive(false);
        ShowPlacementBar(false);
    }

    private GameObject CreateGhostVisual(string structureName)
    {
        GameObject ghost = new GameObject($"Ghost_{structureName}");
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material ghostMat = new Material(s);
        ghostMat.color = new Color(0.20f, 0.75f, 1.0f, 0.48f);

        if (structureName.Contains("Palicada"))
        {
            for (int i = -2; i <= 2; i++)
            {
                AddGhostPart(ghost.transform, $"Log_{i}", PrimitiveType.Cylinder,
                    new Vector3(i * 0.44f, 1.15f, 0f), new Vector3(0.26f, 1.15f, 0.26f), Vector3.zero, ghostMat);
                AddGhostPart(ghost.transform, $"Spike_{i}", PrimitiveType.Cylinder,
                    new Vector3(i * 0.44f, 1.45f, 0.36f), new Vector3(0.08f, 0.55f, 0.08f), new Vector3(42f, 0f, 0f), ghostMat);
            }
            AddGhostPart(ghost.transform, "CrossBrace_Low", PrimitiveType.Cube,
                new Vector3(0f, 0.65f, -0.12f), new Vector3(2.15f, 0.14f, 0.14f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "CrossBrace_High", PrimitiveType.Cube,
                new Vector3(0f, 1.65f, -0.12f), new Vector3(2.15f, 0.14f, 0.14f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Secador"))
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side * 0.85f;
                AddGhostPart(ghost.transform, $"AFrame_F_{side}", PrimitiveType.Cylinder,
                    new Vector3(sx, 0.92f, 0.35f), new Vector3(0.11f, 0.96f, 0.11f), new Vector3(-20f, 0f, 0f), ghostMat);
                AddGhostPart(ghost.transform, $"AFrame_B_{side}", PrimitiveType.Cylinder,
                    new Vector3(sx, 0.92f, -0.35f), new Vector3(0.11f, 0.96f, 0.11f), new Vector3(20f, 0f, 0f), ghostMat);
            }
            AddGhostPart(ghost.transform, "DryingBar_Top", PrimitiveType.Cylinder,
                new Vector3(0f, 1.72f, 0f), new Vector3(0.08f, 1.0f, 0.08f), new Vector3(0f, 0f, 90f), ghostMat);
            AddGhostPart(ghost.transform, "DryingBar_Mid", PrimitiveType.Cylinder,
                new Vector3(0f, 1.18f, 0f), new Vector3(0.07f, 0.95f, 0.07f), new Vector3(0f, 0f, 90f), ghostMat);
        }
        else if (structureName.Contains("Coletor"))
        {
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 120f;
                Vector3 off = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0.62f, 0.55f);
                AddGhostPart(ghost.transform, $"TripodLeg_{i}", PrimitiveType.Cylinder,
                    off, new Vector3(0.09f, 0.70f, 0.09f), new Vector3(22f, ang, 0f), ghostMat);
            }
            AddGhostPart(ghost.transform, "Basin_Shell", PrimitiveType.Cylinder,
                new Vector3(0f, 0.98f, 0f), new Vector3(1.12f, 0.18f, 1.12f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Collector_Foliage", PrimitiveType.Cube,
                new Vector3(0f, 1.25f, 0.38f), new Vector3(1.05f, 0.08f, 0.85f), new Vector3(-28f, 0f, 0f), ghostMat);
        }
        else if (structureName.Contains("Piso"))
        {
            AddGhostPart(ghost.transform, "Deck_Planks", PrimitiveType.Cube,
                new Vector3(0f, 0.12f, 0f), new Vector3(3.0f, 0.16f, 3.0f), Vector3.zero, ghostMat);
            for (int c = 0; c < 4; c++)
            {
                float px = (c % 2 == 0) ? -1.36f : 1.36f;
                float pz = (c < 2) ? -1.36f : 1.36f;
                AddGhostPart(ghost.transform, $"FloorStilt_{c}", PrimitiveType.Cylinder,
                    new Vector3(px, -0.15f, pz), new Vector3(0.20f, 0.32f, 0.20f), Vector3.zero, ghostMat);
            }
        }
        else if (structureName.Contains("Porta"))
        {
            AddGhostPart(ghost.transform, "Wall_LeftFrame", PrimitiveType.Cube,
                new Vector3(-1.02f, 1.20f, 0f), new Vector3(0.96f, 2.40f, 0.22f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Wall_RightFrame", PrimitiveType.Cube,
                new Vector3(1.02f, 1.20f, 0f), new Vector3(0.96f, 2.40f, 0.22f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Wall_TopLintel", PrimitiveType.Cube,
                new Vector3(0f, 2.15f, 0f), new Vector3(1.12f, 0.50f, 0.22f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Parede"))
        {
            for (int r = 0; r < 6; r++)
            {
                AddGhostPart(ghost.transform, $"WallLog_{r}", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.22f + r * 0.40f, 0f), new Vector3(0.22f, 1.50f, 0.22f), new Vector3(0f, 0f, 90f), ghostMat);
            }
            AddGhostPart(ghost.transform, "WallPost_L", PrimitiveType.Cylinder,
                new Vector3(-1.42f, 1.20f, 0f), new Vector3(0.18f, 1.20f, 0.18f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "WallPost_R", PrimitiveType.Cylinder,
                new Vector3(1.42f, 1.20f, 0f), new Vector3(0.18f, 1.20f, 0.18f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Teto"))
        {
            AddGhostPart(ghost.transform, "Roof_Deck", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, 0f), new Vector3(3.15f, 0.16f, 3.15f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Roof_BeamL", PrimitiveType.Cylinder,
                new Vector3(-1.35f, -0.02f, 0f), new Vector3(0.18f, 1.50f, 0.18f), new Vector3(90f, 0f, 0f), ghostMat);
            AddGhostPart(ghost.transform, "Roof_BeamR", PrimitiveType.Cylinder,
                new Vector3(1.35f, -0.02f, 0f), new Vector3(0.18f, 1.50f, 0.18f), new Vector3(90f, 0f, 0f), ghostMat);
        }
        else if (structureName.Contains("Escada"))
        {
            for (int sIdx = 0; sIdx < 7; sIdx++)
            {
                float sy = 0.18f + sIdx * 0.34f;
                float sz = -0.95f + sIdx * 0.32f;
                AddGhostPart(ghost.transform, $"Step_{sIdx}", PrimitiveType.Cube,
                    new Vector3(0f, sy, sz), new Vector3(1.40f, 0.14f, 0.42f), Vector3.zero, ghostMat);
            }
        }
        else if (structureName.Contains("Cesto"))
        {
            AddGhostPart(ghost.transform, "Holder_FrameL", PrimitiveType.Cube,
                new Vector3(-0.78f, 0.55f, 0f), new Vector3(0.14f, 1.10f, 0.95f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Holder_FrameR", PrimitiveType.Cube,
                new Vector3(0.78f, 0.55f, 0f), new Vector3(0.14f, 1.10f, 0.95f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Holder_Base", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, 0f), new Vector3(1.70f, 0.12f, 0.92f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Bau"))
        {
            AddGhostPart(ghost.transform, "Chest_Body", PrimitiveType.Cube,
                new Vector3(0f, 0.32f, 0f), new Vector3(1.15f, 0.58f, 0.72f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Chest_Lid", PrimitiveType.Cube,
                new Vector3(0f, 0.66f, 0f), new Vector3(1.20f, 0.14f, 0.76f), Vector3.zero, ghostMat);
        }
        else if (structureName.Contains("Cabana"))
        {
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1.35f : 1.35f;
                float z = (i < 2) ? -1.15f : 1.15f;
                AddGhostPart(ghost.transform, $"Post_{i}", PrimitiveType.Cylinder,
                    new Vector3(x, 1.15f, z), new Vector3(0.20f, 1.15f, 0.20f), Vector3.zero, ghostMat);
            }
            AddGhostPart(ghost.transform, "Wall_Back", PrimitiveType.Cube,
                new Vector3(0f, 1.05f, -1.15f), new Vector3(2.75f, 2.0f, 0.18f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Wall_Left", PrimitiveType.Cube,
                new Vector3(-1.35f, 1.05f, 0f), new Vector3(0.18f, 2.0f, 2.35f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Wall_Right", PrimitiveType.Cube,
                new Vector3(1.35f, 1.05f, 0f), new Vector3(0.18f, 2.0f, 2.35f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Roof_Left", PrimitiveType.Cube,
                new Vector3(-0.76f, 2.48f, 0f), new Vector3(1.78f, 0.14f, 2.65f), new Vector3(0f, 0f, 26f), ghostMat);
            AddGhostPart(ghost.transform, "Roof_Right", PrimitiveType.Cube,
                new Vector3(0.76f, 2.48f, 0f), new Vector3(1.78f, 0.14f, 2.65f), new Vector3(0f, 0f, -26f), ghostMat);
            AddGhostPart(ghost.transform, "Bed_Mat", PrimitiveType.Cube,
                new Vector3(-0.4f, 0.10f, -0.25f), new Vector3(1.35f, 0.14f, 1.75f), Vector3.zero, ghostMat);
        }
        else
        {
            AddGhostPart(ghost.transform, "Post_FL", PrimitiveType.Cylinder,
                new Vector3(-1.1f, 0.85f, 0.85f), new Vector3(0.15f, 0.85f, 0.15f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Post_FR", PrimitiveType.Cylinder,
                new Vector3(1.1f, 0.85f, 0.85f), new Vector3(0.15f, 0.85f, 0.15f), Vector3.zero, ghostMat);
            AddGhostPart(ghost.transform, "Ridge_Log", PrimitiveType.Cylinder,
                new Vector3(0f, 1.65f, 0.85f), new Vector3(0.14f, 1.2f, 0.14f), new Vector3(0f, 0f, 90f), ghostMat);
            AddGhostPart(ghost.transform, "Roof_Fern", PrimitiveType.Cube,
                new Vector3(0f, 0.90f, -0.05f), new Vector3(2.45f, 0.12f, 2.35f), new Vector3(38f, 0f, 0f), ghostMat);
            AddGhostPart(ghost.transform, "Bed_FernMat", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, 0.0f), new Vector3(1.75f, 0.12f, 1.50f), Vector3.zero, ghostMat);
        }

        return ghost;
    }

    private void AddGhostPart(Transform parent, string partName, PrimitiveType type, Vector3 localPos, Vector3 localScale, Vector3 localEuler, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;
        part.transform.localRotation = Quaternion.Euler(localEuler);
        part.GetComponent<MeshRenderer>().sharedMaterial = mat;
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

    private AudioClip GenerateHammerHitSound()
    {
        int sr = 44100; int count = (int)(sr * 0.22f); float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 140f * ((float)i / sr)) * Mathf.Exp(-16f * t) * 0.75f + Mathf.Sin(2f * Mathf.PI * 950f * ((float)i / sr)) * Mathf.Exp(-32f * t) * 0.45f;
        }
        AudioClip clip = AudioClip.Create("HammerHit_Procedural", count, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GeneratePlaceGhostSound()
    {
        int sr = 44100; int count = (int)(sr * 0.25f); float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 520f * ((float)i / sr)) * Mathf.Exp(-12f * t) * 0.6f;
        }
        AudioClip clip = AudioClip.Create("PlaceGhost_Procedural", count, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateCompleteBuildSound()
    {
        int sr = 44100; int count = (int)(sr * 0.45f); float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 220f * ((float)i / sr)) * Mathf.Exp(-5f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 440f * ((float)i / sr)) * Mathf.Exp(-6f * t) * 0.4f;
        }
        AudioClip clip = AudioClip.Create("CompleteBuild_Procedural", count, 1, sr, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// WorldBlueprintSite: Sitio de construcao fisica no mundo (interacao via InteractionDetector, Zero-Find no Update).
/// </summary>
public class WorldBlueprintSite : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<WorldBlueprintSite> AllSites = new System.Collections.Generic.List<WorldBlueprintSite>();
    public string structureName;
    public int neededLogs;
    public int neededStones;

    public int currentLogs = 0;
    public int currentStones = 0;

    private void OnEnable() { if (!AllSites.Contains(this)) AllSites.Add(this); }
    private void OnDisable() { AllSites.Remove(this); }

    public bool TryDepositResource(GameObject player)
    {
        LogCarryingSystem logCarry = LogCarryingSystem.Instance;
        if (logCarry != null && currentLogs < neededLogs && logCarry.ConsumeOneLog())
        {
            currentLogs++;
            OnResourceAdded("Tora");
            return true;
        }

        InventoryAdvanced inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null && currentStones < neededStones && inv.HasItem(101))
        {
            inv.RemoveItem(101, 1);
            currentStones++;
            OnResourceAdded("Pedra");
            return true;
        }

        if (inv != null && currentLogs < neededLogs && inv.HasItem(100))
        {
            inv.RemoveItem(100, 1);
            currentLogs++;
            OnResourceAdded("Madeira");
            return true;
        }

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Faltam Toras ({currentLogs}/{neededLogs}) ou Pedras ({currentStones}/{neededStones})");
        return false;
    }

    private void OnResourceAdded(string resourceName)
    {
        if (BlueprintPlacementManager.Instance != null)
            BlueprintPlacementManager.Instance.PlayHammerSound(transform.position);

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"+1 {resourceName} em {structureName} ({currentLogs}/{neededLogs} Mad | {currentStones}/{neededStones} Ped)");

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(0.06f);

        if (currentLogs >= neededLogs && currentStones >= neededStones)
        {
            CompleteStructure();
        }
    }

    private void CompleteStructure()
    {
        if (BlueprintPlacementManager.Instance != null)
            BlueprintPlacementManager.Instance.PlayCompleteSound(transform.position);

        if (VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position + Vector3.up * 0.6f);

        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark")
            ?? Resources.Load<Material>("Materials/Wood_Bark");
        Material fernMat = Resources.Load<Material>("Materials/Mat_Forest_Foliage")
            ?? Resources.Load<Material>("Materials/Pine_Foliage");
        Material stoneMat = Resources.Load<Material>("Materials/Ruin_Stones");

        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            string n = r.gameObject.name;
            if (n.Contains("Fern") || n.Contains("Foliage") || n.Contains("Bed"))
            {
                if (fernMat != null) r.sharedMaterial = fernMat;
                else if (barkMat != null) r.sharedMaterial = barkMat;
            }
            else if (n.Contains("Basin") && stoneMat != null)
            {
                r.sharedMaterial = stoneMat;
            }
            else if (barkMat != null)
            {
                r.sharedMaterial = barkMat;
            }

            if (!n.Contains("Bed") && r.GetComponent<Collider>() == null)
            {
                r.gameObject.AddComponent<BoxCollider>();
            }
        }

        BoxCollider rootTrig = GetComponent<BoxCollider>();
        StructureHealth hp = gameObject.AddComponent<StructureHealth>();
        hp.structureName = structureName;
        hp.maxHealth = structureName.Contains("Cabana") ? 240f
            : (structureName.Contains("Parede") || structureName.Contains("Porta") || structureName.Contains("Piso") || structureName.Contains("Teto") || structureName.Contains("Escada") ? 200f
            : (structureName.Contains("Palicada") ? 180f : 145f));
        hp.currentHealth = hp.maxHealth;

        if (structureName.Contains("Palicada"))
        {
            if (rootTrig != null) Destroy(rootTrig);
            var spike = gameObject.AddComponent<PalisadeSpikeHazard>();
            spike.structureHealth = hp;
        }
        else if (structureName.Contains("Secador"))
        {
            var rack = gameObject.AddComponent<DryingRackInteractable>();
            rack.BuildHangingSlots();
        }
        else if (structureName.Contains("Coletor"))
        {
            var collector = gameObject.AddComponent<RainCollectorInteractable>();
            collector.BuildWaterVisual();
        }
        else if (structureName.Contains("Cesto"))
        {
            gameObject.AddComponent<LogHolderInteractable>();
        }
        else if (structureName.Contains("Bau"))
        {
            gameObject.AddComponent<StorageChestInteractable>();
        }
        else if (structureName.Contains("Piso") || structureName.Contains("Parede") || structureName.Contains("Porta") || structureName.Contains("Teto") || structureName.Contains("Escada"))
        {
            // Peca modular estrutural (somente colliders fisicos solidos)
        }
        else
        {
            var shelter = gameObject.AddComponent<ShelterInteractable>();
            shelter.isCabin = structureName.Contains("Cabana");
        }

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#55FF99>CONSTRUCAO COMPLETA: {structureName}!</color>");

        Destroy(this);
    }
}

/// <summary>
/// ShelterInteractable: Permite dormir ate o amanhecer (avanca a noite e limpa Vultos proximos) e salvar o jogo.
/// </summary>
public class ShelterInteractable : MonoBehaviour
{
    public bool isCabin = false;

    public string GetPromptLabel()
    {
        DayNightCycle dnc = DayNightCycle.Instance;
        bool canSleep = dnc != null && (dnc.currentTime >= 18.0f || dnc.currentTime < 5.5f);
        return canSleep
            ? (isCabin ? "Dormir ate Amanhecer e Salvar (Cabana)" : "Dormir e Salvar (Refugio)")
            : (isCabin ? "Salvar Jogo na Cabana" : "Salvar Jogo no Refugio");
    }

    public void Interact()
    {
        DayNightCycle dnc = DayNightCycle.Instance ?? FindAnyObjectByType<DayNightCycle>();
        PlayerStatus status = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
        SanitySystem sanity = FindAnyObjectByType<SanitySystem>();
        SaveSystem save = FindAnyObjectByType<SaveSystem>();

        bool sleptThroughNight = false;
        if (dnc != null && (dnc.currentTime >= 18.0f || dnc.currentTime < 5.5f))
        {
            if (dnc.currentTime >= 18.0f) dnc.currentDay++;
            dnc.currentTime = 6.5f;
            sleptThroughNight = true;
        }

        if (status != null)
        {
            status.health = Mathf.Min(100f, status.health + (sleptThroughNight ? (isCabin ? 55f : 35f) : 15f));
            status.stamina = 100f;
            status.isBleeding = false;
        }
        if (sanity != null)
        {
            sanity.sanity = Mathf.Min(100f, sanity.sanity + (sleptThroughNight ? (isCabin ? 60f : 40f) : 15f));
        }
        if (sleptThroughNight && BodyTemperatureSystem.Instance != null)
        {
            BodyTemperatureSystem.Instance.ResetTemperature();
        }
        if (sleptThroughNight && VultoManager.Instance != null)
        {
            VultoManager.Instance.ClearNearbyVultos(transform.position, 35f);
        }

        if (save != null) save.SaveGame();

        if (FirstPersonCamera.Instance != null)
            FirstPersonCamera.Instance.AddShake(0.05f);

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(sleptThroughNight
                ? $"<color=#88FFAA>Voce descansou ate 06:30 (Dia {(dnc != null ? dnc.currentDay : 1)}) e salvou!</color>"
                : "<color=#88FFAA>Progresso salvo! (Durma apos as 18:00 para passar a noite)</color>");
        }
    }
}

/// <summary>
/// DryingRackInteractable: Seca carne crua (104/113 -> 212 Charque Salgado nao-perecivel) ou peixe cru (111 -> 207) estilo The Forest.
/// </summary>
public class DryingRackInteractable : MonoBehaviour
{
    public const int MaxSlots = 3;
    public float dryDuration = 20f;

    private readonly int[] rawItemIds = new int[MaxSlots];
    private readonly float[] dryTimers = new float[MaxSlots];
    private readonly bool[] isDried = new bool[MaxSlots];
    private readonly MeshRenderer[] slotRenderers = new MeshRenderer[MaxSlots];
    private Material rawMeatMat;
    private Material driedMeatMat;

    public void BuildHangingSlots()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        rawMeatMat = new Material(lit);
        rawMeatMat.color = new Color(0.68f, 0.16f, 0.14f);
        driedMeatMat = new Material(lit);
        driedMeatMat.color = new Color(0.45f, 0.22f, 0.12f);

        for (int i = 0; i < MaxSlots; i++)
        {
            GameObject meatObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meatObj.name = $"HangingMeat_{i}";
            Destroy(meatObj.GetComponent<Collider>());
            meatObj.transform.SetParent(transform, false);
            meatObj.transform.localPosition = new Vector3((i - 1) * 0.42f, 1.44f, 0f);
            meatObj.transform.localScale = new Vector3(0.14f, 0.34f, 0.08f);
            MeshRenderer mr = meatObj.GetComponent<MeshRenderer>();
            mr.sharedMaterial = rawMeatMat;
            meatObj.SetActive(false);
            slotRenderers[i] = mr;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        bool raining = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining;
        float speedMult = raining ? 0.5f : 1.0f;

        for (int i = 0; i < MaxSlots; i++)
        {
            if (rawItemIds[i] <= 0 || isDried[i]) continue;
            dryTimers[i] += dt * speedMult;
            if (dryTimers[i] >= dryDuration)
            {
                isDried[i] = true;
                if (slotRenderers[i] != null && driedMeatMat != null)
                    slotRenderers[i].sharedMaterial = driedMeatMat;
            }
        }
    }

    public string GetPromptLabel(InventoryAdvanced inv)
    {
        int readyCount = 0;
        int dryingCount = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (rawItemIds[i] > 0)
            {
                if (isDried[i]) readyCount++;
                else dryingCount++;
            }
        }

        if (readyCount > 0) return $"Coletar Charque / Peixe Seco ({readyCount} pronto)";
        bool hasRaw = inv != null && (inv.HasItem(104) || inv.HasItem(113) || inv.HasItem(111));
        if (dryingCount < MaxSlots && hasRaw) return $"Pendurar Carne/Peixe p/ Secar ({dryingCount}/{MaxSlots})";
        return dryingCount > 0 ? $"Secador: {dryingCount}/{MaxSlots} curando..." : "Secador Vazio (Requer Carne ou Peixe)";
    }

    public void Interact(InventoryAdvanced inv)
    {
        if (inv == null) inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (inv == null) return;

        int collected = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (rawItemIds[i] > 0 && isDried[i])
            {
                int outId = (rawItemIds[i] == 111) ? 207 : 212; // 212 = Charque Salgado (nao apodrece!)
                int leftover = inv.AddItem(outId, 1);
                if (leftover > 0 && LootManager.Instance != null)
                {
                    LootManager.Instance.DropItem(outId, leftover, transform.position + Vector3.up * 0.5f);
                }
                rawItemIds[i] = 0;
                dryTimers[i] = 0f;
                isDried[i] = false;
                if (slotRenderers[i] != null) slotRenderers[i].gameObject.SetActive(false);
                collected++;
            }
        }

        if (collected > 0)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"+{collected} Charque / Peixe Seco coletado!");
            return;
        }

        int emptySlot = -1;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (rawItemIds[i] <= 0) { emptySlot = i; break; }
        }

        if (emptySlot < 0)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Secador cheio — aguarde curar!");
            return;
        }

        int rawId = inv.HasItem(104) ? 104 : (inv.HasItem(111) ? 111 : (inv.HasItem(113) ? 113 : -1));
        if (rawId < 0)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Sem carne crua ou peixe para pendurar!");
            return;
        }

        inv.RemoveItem(rawId, 1);
        rawItemIds[emptySlot] = rawId;
        dryTimers[emptySlot] = 0f;
        isDried[emptySlot] = false;
        if (slotRenderers[emptySlot] != null)
        {
            slotRenderers[emptySlot].sharedMaterial = rawMeatMat;
            slotRenderers[emptySlot].gameObject.SetActive(true);
        }

        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip("Pendurado para secar! (~20s)");
    }
}

/// <summary>
/// RainCollectorInteractable: Acumula agua potavel da chuva/orvalho, entrega Agua Limpa (109) + reabastece Cantil (210).
/// </summary>
public class RainCollectorInteractable : MonoBehaviour
{
    public int maxCharges = 3;
    public int waterCharges = 1;
    private float fillTimer = 0f;
    private GameObject waterSurfaceObj;

    public void BuildWaterVisual()
    {
        waterSurfaceObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waterSurfaceObj.name = "CollectedWaterSurface";
        Destroy(waterSurfaceObj.GetComponent<Collider>());
        waterSurfaceObj.transform.SetParent(transform, false);
        waterSurfaceObj.transform.localPosition = new Vector3(0f, 1.06f, 0f);
        waterSurfaceObj.transform.localScale = new Vector3(0.92f, 0.03f, 0.92f);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.18f, 0.62f, 0.88f, 0.90f);
        mat.SetFloat("_Smoothness", 0.9f);
        waterSurfaceObj.GetComponent<MeshRenderer>().sharedMaterial = mat;
        RefreshVisual();
    }

    private void Update()
    {
        if (waterCharges >= maxCharges) return;
        bool raining = GalicianWeatherEngine.Instance != null && GalicianWeatherEngine.Instance.isRaining;
        fillTimer += Time.deltaTime * (raining ? 3.0f : 1.0f);
        if (fillTimer >= 34f)
        {
            fillTimer = 0f;
            waterCharges = Mathf.Min(maxCharges, waterCharges + 1);
            RefreshVisual();
        }
    }

    private void RefreshVisual()
    {
        if (waterSurfaceObj != null)
            waterSurfaceObj.SetActive(waterCharges > 0);
    }

    public string GetPromptLabel()
    {
        return waterCharges > 0
            ? $"Coletar Agua Limpa ({waterCharges}/{maxCharges})"
            : "Coletor Vazio (Aguardando Chuva/Orvalho)";
    }

    public void Interact(InventoryAdvanced inv, PlayerStatus status)
    {
        if (waterCharges <= 0)
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Coletor ainda sem agua acumulada.");
            return;
        }

        waterCharges--;
        RefreshVisual();

        bool refilledCanteen = false;
        if (inv != null)
        {
            int leftover = inv.AddItem(109, 1);
            if (leftover > 0 && LootManager.Instance != null)
                LootManager.Instance.DropItem(109, leftover, transform.position + Vector3.up * 0.5f);

            refilledCanteen = inv.TryRefillCanteen(out _);
        }

        if (status != null) status.thirst = Mathf.Min(100f, status.thirst + 20f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();

        if (HUDSurvivalOverlay.Instance != null)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip(refilledCanteen
                ? "+1 Agua Limpa, +20% Sede & Cantil 3/3!"
                : "+1 Agua Limpa e +20% Sede!");
        }
    }
}

/// <summary>
/// PalisadeSpikeHazard: Espinhos frontais da palicada ferem e repelem Vultos que se aproximam.
/// </summary>
public class PalisadeSpikeHazard : MonoBehaviour
{
    public StructureHealth structureHealth;
    private float nextTickTime = 0f;

    private void Update()
    {
        if (Time.time < nextTickTime) return;
        nextTickTime = Time.time + 0.85f;

        if (VultoManager.Instance != null)
        {
            if (VultoManager.Instance.TryDamageVultoAt(transform.position, 28f) && structureHealth != null)
            {
                structureHealth.ApplyDamage(3f);
            }
        }
    }
}

/// <summary>
/// StructureHealth: Integridade e reparo de estruturas construidas (Palicadas, Cabanas, Refugios, Secadores).
/// </summary>
public class StructureHealth : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<StructureHealth> AllStructures = new System.Collections.Generic.List<StructureHealth>();

    public string structureName = "Estrutura";
    public float maxHealth = 160f;
    public float currentHealth = 160f;
    private bool alertedDamaged = false;

    public bool IsDamaged => currentHealth < maxHealth - 1f;
    public float HealthRatio => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 1f;

    private void OnEnable() { if (!AllStructures.Contains(this)) AllStructures.Add(this); }
    private void OnDisable() { AllStructures.Remove(this); }

    public void ApplyDamage(float dmg)
    {
        if (dmg <= 0f || currentHealth <= 0f) return;
        currentHealth -= dmg;

        if (!alertedDamaged && currentHealth < maxHealth * 0.65f)
        {
            alertedDamaged = true;
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF9944>{structureName} sob ataque! Conserte com Madeira.</color>");
        }

        if (currentHealth <= 0f)
        {
            if (VFXPoolManager.Instance != null)
                VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position + Vector3.up * 0.5f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#FF4444>{structureName} foi destruida!</color>");
            Destroy(gameObject);
        }
    }

    public bool TryRepair(InventoryAdvanced inv)
    {
        if (!IsDamaged) return false;
        if (inv == null || !inv.HasItem(100))
        {
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Precisa de 1x Madeira para consertar {structureName}.");
            return false;
        }

        inv.RemoveItem(100, 1);
        currentHealth = Mathf.Min(maxHealth, currentHealth + 65f);
        if (currentHealth >= maxHealth * 0.7f) alertedDamaged = false;

        if (BlueprintPlacementManager.Instance != null)
            BlueprintPlacementManager.Instance.PlayHammerSound(transform.position);
        if (VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.PlayBuildPlaceVFX(transform.position + Vector3.up * 0.5f);
        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"Consertado: {structureName} ({Mathf.RoundToInt(HealthRatio * 100f)}%)");
        return true;
    }
}

/// <summary>
/// LogHolderInteractable: Cesto de Toras da base (armazena ate 12 toras com visual empilhado).
/// </summary>
public class LogHolderInteractable : MonoBehaviour
{
    public const int MaxLogs = 12;
    public int storedLogs = 0;
    private readonly GameObject[] logVisuals = new GameObject[MaxLogs];

    private void Awake()
    {
        Material barkMat = Resources.Load<Material>("Materials/Mat_DarkForest_Bark")
            ?? Resources.Load<Material>("Materials/Wood_Bark");
        for (int i = 0; i < MaxLogs; i++)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = $"StoredLog_{i}";
            Destroy(log.GetComponent<Collider>());
            log.transform.SetParent(transform, false);
            int row = i / 4;
            int col = i % 4;
            log.transform.localPosition = new Vector3(0f, 0.28f + row * 0.24f, -0.30f + col * 0.20f);
            log.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
            log.transform.localScale = new Vector3(0.18f, 0.68f, 0.18f);
            if (barkMat != null) log.GetComponent<MeshRenderer>().sharedMaterial = barkMat;
            log.SetActive(false);
            logVisuals[i] = log;
        }
    }

    public string GetPromptLabel()
    {
        bool carrying = LogCarryingSystem.Instance != null && LogCarryingSystem.Instance.currentLogs > 0;
        if (carrying && storedLogs < MaxLogs)
            return $"Guardar Tora no Cesto ({storedLogs}/{MaxLogs})";
        if (storedLogs > 0)
            return $"Pegar Tora do Cesto ({storedLogs}/{MaxLogs})";
        return $"Cesto de Toras Vazio (0/{MaxLogs})";
    }

    public void Interact(InventoryAdvanced inv)
    {
        var carry = LogCarryingSystem.Instance;
        if (carry != null && carry.currentLogs > 0 && storedLogs < MaxLogs && carry.ConsumeOneLog())
        {
            storedLogs++;
            RefreshVisuals();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Tora guardada no Cesto ({storedLogs}/{MaxLogs})");
            return;
        }

        if (storedLogs > 0 && carry != null && carry.CanPickupLog)
        {
            storedLogs--;
            carry.PickupLog();
            RefreshVisuals();
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Retirou Tora do Cesto ({storedLogs}/{MaxLogs})");
            return;
        }

        if (inv != null && storedLogs < MaxLogs && inv.HasItem(100))
        {
            inv.RemoveItem(100, 1);
            storedLogs++;
            RefreshVisuals();
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"Madeira armazenada como Tora ({storedLogs}/{MaxLogs})");
        }
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < MaxLogs; i++)
        {
            if (logVisuals[i] != null) logVisuals[i].SetActive(i < storedLogs);
        }
    }
}

/// <summary>
/// StorageChestInteractable: Bau de Base com deposito/retirada de itens via modal ou interacao rapida.
/// </summary>
public class StorageChestInteractable : MonoBehaviour
{
    public static bool IsAnyChestOpen { get; private set; }

    [System.Serializable]
    public struct StoredStack
    {
        public int itemId;
        public int amount;
    }

    public const int MaxStacks = 16;
    public readonly System.Collections.Generic.List<StoredStack> stacks = new System.Collections.Generic.List<StoredStack>(MaxStacks);

    private static GameObject chestModalObj;
    private static Transform chestGridParent;
    private static Transform bagGridParent;
    private static StorageChestInteractable activeChest;

    public string GetPromptLabel()
    {
        return $"Abrir Bau de Base ({stacks.Count}/{MaxStacks} Pilhas)";
    }

    public void Interact()
    {
        activeChest = this;
        OpenModal();
    }

    private static void OpenModal()
    {
        EnsureModalUI();
        IsAnyChestOpen = true;
        if (chestModalObj != null)
        {
            chestModalObj.SetActive(true);
            chestModalObj.transform.SetAsLastSibling();
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshModalRows();
    }

    public static void CloseModal()
    {
        IsAnyChestOpen = false;
        if (chestModalObj != null) chestModalObj.SetActive(false);
        bool touchActive = FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
        if (!touchActive)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (IsAnyChestOpen && activeChest == this && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseModal();
        }
    }

    private static void EnsureModalUI()
    {
        if (chestModalObj != null) return;
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        chestModalObj = new GameObject("BaseStorageChestModal");
        chestModalObj.transform.SetParent(canvas.transform, false);
        RectTransform rootRT = chestModalObj.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image dim = chestModalObj.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.04f, 0.06f, 0.72f);

        GameObject win = new GameObject("Window");
        win.transform.SetParent(chestModalObj.transform, false);
        RectTransform wRT = win.AddComponent<RectTransform>();
        wRT.anchorMin = new Vector2(0.5f, 0.5f);
        wRT.anchorMax = new Vector2(0.5f, 0.5f);
        wRT.sizeDelta = new Vector2(720f, 440f);
        Image wImg = win.AddComponent<Image>();
        wImg.sprite = FragaProIconLibrary.GetPillSprite();
        wImg.type = Image.Type.Sliced;
        wImg.color = new Color(0.08f, 0.11f, 0.15f, 0.96f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateLabel(win.transform, "Title", "BAU DE BASE (TOQUE PARA GUARDAR / RETIRAR)", font, 16,
            new Vector2(0f, 192f), new Vector2(680f, 30f), new Color(0.95f, 0.85f, 0.45f));
        CreateLabel(win.transform, "BagHdr", "MOCHILA (DEPOSITAR 1x)", font, 13,
            new Vector2(-170f, 152f), new Vector2(300f, 24f), Color.white);
        CreateLabel(win.transform, "ChestHdr", "NO BAU (RETIRAR 1x)", font, 13,
            new Vector2(170f, 152f), new Vector2(300f, 24f), Color.white);

        bagGridParent = CreateScrollColumn(win.transform, "BagCol", new Vector2(-170f, -15f), new Vector2(310f, 300f));
        chestGridParent = CreateScrollColumn(win.transform, "ChestCol", new Vector2(170f, -15f), new Vector2(310f, 300f));

        GameObject closeBtn = new GameObject("Btn_Close");
        closeBtn.transform.SetParent(win.transform, false);
        RectTransform cRT = closeBtn.AddComponent<RectTransform>();
        cRT.anchoredPosition = new Vector2(0f, -190f);
        cRT.sizeDelta = new Vector2(180f, 34f);
        Image cImg = closeBtn.AddComponent<Image>();
        cImg.sprite = FragaProIconLibrary.GetPillSprite();
        cImg.type = Image.Type.Sliced;
        cImg.color = new Color(0.56f, 0.18f, 0.18f, 0.95f);
        closeBtn.AddComponent<Button>().onClick.AddListener(CloseModal);
        CreateLabel(closeBtn.transform, "Lbl", "FECHAR [ESC]", font, 13, Vector2.zero, new Vector2(160f, 28f), Color.white);
    }

    private static Transform CreateScrollColumn(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject col = new GameObject(name);
        col.transform.SetParent(parent, false);
        RectTransform rt = col.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image bg = col.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.09f, 0.85f);
        VerticalLayoutGroup vlg = col.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        return col.transform;
    }

    private static void CreateLabel(Transform parent, string name, string text, Font font, int size, Vector2 pos, Vector2 box, Color col)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        Text t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = col;
        t.text = text;
        t.raycastTarget = false;
    }

    private static void RefreshModalRows()
    {
        if (activeChest == null || bagGridParent == null || chestGridParent == null) return;
        for (int i = bagGridParent.childCount - 1; i >= 0; i--) Destroy(bagGridParent.GetChild(i).gameObject);
        for (int i = chestGridParent.childCount - 1; i >= 0; i--) Destroy(chestGridParent.GetChild(i).gameObject);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        InventoryAdvanced inv = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (inv != null && inv.slots != null)
        {
            int shown = 0;
            for (int i = 0; i < inv.slots.Length && shown < 9; i++)
            {
                var sl = inv.slots[i];
                if (sl.IsEmpty()) continue;
                ItemData d = ItemDatabase.GetItem(sl.itemId);
                if (d == null) continue;
                int itemId = sl.itemId;
                int slotIdx = i;
                CreateRowButton(bagGridParent, $"{d.itemName} (x{sl.quantity})", font, new Color(0.14f, 0.25f, 0.35f, 0.92f), () =>
                {
                    activeChest.DepositOneFromSlot(inv, slotIdx, itemId);
                });
                shown++;
            }
        }

        for (int i = 0; i < activeChest.stacks.Count && i < 9; i++)
        {
            int idx = i;
            var st = activeChest.stacks[i];
            ItemData d = ItemDatabase.GetItem(st.itemId);
            string label = d != null ? $"{d.itemName} (x{st.amount})" : $"Item {st.itemId} (x{st.amount})";
            CreateRowButton(chestGridParent, label, font, new Color(0.18f, 0.38f, 0.26f, 0.92f), () =>
            {
                if (inv != null) activeChest.WithdrawOneToBag(inv, idx);
            });
        }
    }

    private static void CreateRowButton(Transform parent, string label, Font font, Color bgCol, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject("RowBtn");
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(290f, 28f);
        Image img = btnObj.AddComponent<Image>();
        img.color = bgCol;
        btnObj.AddComponent<Button>().onClick.AddListener(onClick);
        CreateLabel(btnObj.transform, "Txt", label, font, 12, Vector2.zero, new Vector2(280f, 24f), Color.white);
    }

    private void DepositOneFromSlot(InventoryAdvanced inv, int slotIdx, int itemId)
    {
        if (stacks.Count >= MaxStacks)
        {
            bool hasExisting = false;
            for (int i = 0; i < stacks.Count; i++) if (stacks[i].itemId == itemId) { hasExisting = true; break; }
            if (!hasExisting) return;
        }
        if (!inv.RemoveItem(itemId, 1)) return;

        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].itemId == itemId)
            {
                var st = stacks[i];
                st.amount++;
                stacks[i] = st;
                RefreshModalRows();
                return;
            }
        }
        stacks.Add(new StoredStack { itemId = itemId, amount = 1 });
        RefreshModalRows();
    }

    private void WithdrawOneToBag(InventoryAdvanced inv, int stackIdx)
    {
        if (stackIdx < 0 || stackIdx >= stacks.Count) return;
        var st = stacks[stackIdx];
        if (inv.AddItem(st.itemId, 1) > 0) return;
        st.amount--;
        if (st.amount <= 0) stacks.RemoveAt(stackIdx);
        else stacks[stackIdx] = st;
        RefreshModalRows();
    }
}
