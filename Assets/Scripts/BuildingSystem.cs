using UnityEngine;
using UnityEngine.UI;

public class BuildingSystem : MonoBehaviour
{
    public static BuildingSystem Instance { get; private set; }

    [Header("Dependencies")]
    public Transform playerTransform;
    public GameManager gameManager;
    public InventoryAdvanced inventory;

    [Header("Visuals")]
    public GameObject hologramPrefab;

    private GameObject currentHologram;
    private MeshRenderer hologramRenderer;
    private Material hologramMaterial;
    private GameObject buildBarObj;

    private bool isBuildModeActive = false;
    public bool IsBuildModeActive => isBuildModeActive;

    private int currentBuildingTileID = 0;

    private System.IntPtr mapDataPtr;
    private int mapWidth;
    private int mapHeight;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void Initialize(System.IntPtr ptr, int width, int height)
    {
        mapDataPtr = ptr;
        mapWidth = width;
        mapHeight = height;

        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
    }

    private void Start()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
    }

    private void Update()
    {
        if (!isBuildModeActive || playerTransform == null || mapDataPtr == System.IntPtr.Zero) return;

        UpdateHologramPosition();

        bool overUI = FragaInputRouter.IsAnyPointerOverUI();
        if (!overUI && Input.touchCount == 0 && Input.GetMouseButtonDown(0))
        {
            TryBuild();
        }

        if ((Input.touchCount == 0 && Input.GetMouseButtonDown(1)) || Input.GetKeyDown(KeyCode.Escape))
        {
            ExitBuildMode();
        }
    }

    public void EnterBuildMode(int tileID)
    {
        currentBuildingTileID = tileID;
        isBuildModeActive = true;

        if (currentHologram == null)
        {
            if (hologramPrefab != null)
            {
                currentHologram = Instantiate(hologramPrefab);
                hologramRenderer = currentHologram.GetComponentInChildren<MeshRenderer>();
                if (hologramRenderer != null && hologramRenderer.sharedMaterial != null)
                {
                    hologramMaterial = new Material(hologramRenderer.sharedMaterial);
                    hologramRenderer.material = hologramMaterial;
                }
            }
            else
            {
                currentHologram = GameObject.CreatePrimitive(PrimitiveType.Cube);
                currentHologram.name = "BuildHologram_Procedural";
                currentHologram.transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);

                Collider col = currentHologram.GetComponent<Collider>();
                if (col != null) Destroy(col);

                hologramRenderer = currentHologram.GetComponent<MeshRenderer>();
                if (hologramRenderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Standard");
                    hologramMaterial = new Material(shader);
                    if (hologramMaterial.HasProperty("_Surface"))
                    {
                        hologramMaterial.SetFloat("_Surface", 1f);
                        hologramMaterial.SetFloat("_Blend", 0f);
                        hologramMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }
                    hologramMaterial.SetInt("_ZWrite", 0);
                    hologramMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    hologramRenderer.material = hologramMaterial;
                    hologramRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        if (currentHologram != null) currentHologram.SetActive(true);
        ShowBuildBar(true);
    }

    public void ExitBuildMode()
    {
        isBuildModeActive = false;
        if (currentHologram != null) currentHologram.SetActive(false);
        ShowBuildBar(false);
    }

    private void ShowBuildBar(bool show)
    {
        if (show && buildBarObj == null) BuildBarUI();
        if (buildBarObj != null) buildBarObj.SetActive(show);
    }

    private void BuildBarUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        buildBarObj = new GameObject("BuildingSystemBar");
        buildBarObj.transform.SetParent(canvas.transform, false);
        RectTransform rt = buildBarObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 118f);
        rt.sizeDelta = new Vector2(320f, 54f);

        Image bg = buildBarObj.AddComponent<Image>();
        bg.sprite = FragaProIconLibrary.GetPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.08f, 0.12f, 0.90f);

        CreateBuildBarBtn(buildBarObj.transform, "Btn_Build", "CONSTRUIR", FragaProIconLibrary.ControlIcon.ConfirmCheck,
            new Vector2(-74f, 0f), 144f, new Color(0.16f, 0.56f, 0.36f, 0.96f), TryBuild);
        CreateBuildBarBtn(buildBarObj.transform, "Btn_Cancel", "CANCELAR", FragaProIconLibrary.ControlIcon.CancelCross,
            new Vector2(78f, 0f), 136f, new Color(0.58f, 0.18f, 0.18f, 0.94f), ExitBuildMode);
    }

    private static void CreateBuildBarBtn(Transform parent, string name, string label, FragaProIconLibrary.ControlIcon icon,
        Vector2 pos, float width, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 38f);

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
        tRT.offsetMin = new Vector2(34f, 0f);
        tRT.offsetMax = new Vector2(-8f, 0f);
        Text txt = txtObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 11;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;
    }

    private Vector2Int GetTargetGridPosition()
    {
        Camera cam = Camera.main;
        Vector3 targetPos;
        if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 5.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            targetPos = hit.point;
        }
        else
        {
            Vector3 forward = cam != null ? cam.transform.forward : playerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            targetPos = playerTransform.position + forward * 2.2f;
        }

        return new Vector2Int(Mathf.RoundToInt(targetPos.x), Mathf.RoundToInt(targetPos.z));
    }

    private void UpdateHologramPosition()
    {
        if (currentHologram == null) return;

        Vector2Int gridPos = GetTargetGridPosition();
        float groundY = TerrainMeshGenerator.GetTerrainHeight(gridPos.x, gridPos.y);
        currentHologram.transform.position = new Vector3(gridPos.x, groundY + 0.5f, gridPos.y);

        bool canBuild = CanBuildAt(gridPos.x, gridPos.y);

        if (hologramMaterial != null)
        {
            Color col = canBuild ? new Color(0.15f, 1f, 0.3f, 0.45f) : new Color(1f, 0.15f, 0.15f, 0.45f);
            hologramMaterial.color = col;
            if (hologramMaterial.HasProperty("_BaseColor"))
            {
                hologramMaterial.SetColor("_BaseColor", col);
            }
        }
    }

    private bool CanBuildAt(int x, int y)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return false;
        int currentTile = ResourceManager.GetMapCell(mapDataPtr, mapWidth, mapHeight, x, y);
        return currentTile == 0 || currentTile == 2;
    }

    private void TryBuild()
    {
        Vector2Int gridPos = GetTargetGridPosition();
        if (CanBuildAt(gridPos.x, gridPos.y))
        {
            if (currentBuildingTileID == 3)
            {
                if (inventory != null && inventory.GetItemCount(100) >= 2) inventory.RemoveItem(100, 2);
                else { Notify("Sem Madeira suficiente (2)"); return; }
            }
            else if (currentBuildingTileID == 4)
            {
                if (inventory != null && inventory.GetItemCount(101) >= 2 && inventory.GetItemCount(100) >= 1)
                {
                    inventory.RemoveItem(101, 2);
                    inventory.RemoveItem(100, 1);
                }
                else { Notify("Faltam recursos (2 Pedra, 1 Madeira)"); return; }
            }
            else if (currentBuildingTileID == 5)
            {
                if (inventory != null && inventory.GetItemCount(101) >= 1 && inventory.GetItemCount(100) >= 2)
                {
                    inventory.RemoveItem(101, 1);
                    inventory.RemoveItem(100, 2);
                }
                else { Notify("Faltam recursos (1 Pedra, 2 Madeira)"); return; }
            }

            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager != null) gameManager.UpdateMapCellNative(gridPos.x, gridPos.y, currentBuildingTileID);

            if (VFXPoolManager.Instance != null)
            {
                float vfxY = TerrainMeshGenerator.GetTerrainHeight(gridPos.x, gridPos.y) + 0.2f;
                VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(gridPos.x, vfxY, gridPos.y));
            }

            ExitBuildMode();
        }
        else
        {
            Notify("Local invalido para construir.");
        }
    }

    private static void Notify(string msg)
    {
        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
    }

    public bool ForceBuild(int tileID)
    {
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (playerTransform == null) return false;
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

        Vector3 forward = Camera.main != null ? Camera.main.transform.forward : playerTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 aim = playerTransform.position + forward * 1.8f;

        Vector2Int pos = new Vector2Int(Mathf.RoundToInt(aim.x), Mathf.RoundToInt(aim.z));
        if (gameManager != null)
        {
            gameManager.UpdateMapCellNative(pos.x, pos.y, tileID);
            if (VFXPoolManager.Instance != null)
            {
                float vfxY = TerrainMeshGenerator.GetTerrainHeight(pos.x, pos.y) + 0.2f;
                VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(pos.x, vfxY, pos.y));
            }
            return true;
        }
        return false;
    }
}
