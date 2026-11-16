using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// FarmingSystem: Lavra solo a frente da mira da camera com Enxada (307) e planta Sementes (112).
/// Input unificado via InteractionDetector / EquipmentSystem (sem KeyCode.T solto).
/// </summary>
public class FarmingSystem : MonoBehaviour
{
    public static FarmingSystem Instance { get; private set; }

    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public GameManager gameManager;
    public MapRenderer mapRenderer;
    public DayNightCycle dayNightCycle;

    [System.Serializable]
    public class FarmPlot
    {
        public int x, y;
        public float plantTime;
        public bool isGrown;
    }

    private readonly List<FarmPlot> activePlots = new List<FarmPlot>();
    private float nextGrowthCheckTime = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (mapRenderer == null) mapRenderer = FindAnyObjectByType<MapRenderer>();
        if (dayNightCycle == null) dayNightCycle = FindAnyObjectByType<DayNightCycle>();
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
    }

    private void Update()
    {
        if (Time.time >= nextGrowthCheckTime)
        {
            nextGrowthCheckTime = Time.time + 2.0f;
            CheckGrowth();
        }
    }

    public bool TryTillOrPlant()
    {
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero || inventory == null) return false;

        Vector3 fwd = Camera.main != null ? Camera.main.transform.forward : playerTransform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = playerTransform.forward;
        fwd.Normalize();

        Vector3 targetPos = playerTransform.position + fwd * 1.6f;
        int pX = Mathf.RoundToInt(targetPos.x);
        int pY = Mathf.RoundToInt(targetPos.z);

        if (pX < 0 || pX >= gameManager.mapWidth || pY < 0 || pY >= gameManager.mapHeight) return false;

        int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, pX, pY);

        if (tile == 0)
        {
            if (inventory.HasItem(307))
            {
                gameManager.UpdateMapCellNative(pX, pY, 14);
                if (mapRenderer != null) mapRenderer.AddInstanceAt(pX, pY, 14);

                float gy = TerrainMeshGenerator.GetTerrainHeight(pX, pY);
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(pX, gy + 0.1f, pY));
                if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip("Terra Fertil lavrada!");
                return true;
            }
        }
        else if (tile == 14)
        {
            if (inventory.HasItem(112))
            {
                inventory.RemoveItem(112, 1);

                float currentHour = dayNightCycle != null ? dayNightCycle.currentTime : 8f;
                FarmPlot plot = new FarmPlot { x = pX, y = pY, plantTime = currentHour, isGrown = false };
                activePlots.Add(plot);

                float gy = TerrainMeshGenerator.GetTerrainHeight(pX, pY);
                if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayBuildPlaceVFX(new Vector3(pX, gy + 0.1f, pY));
                if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip("Sementes Silvestres plantadas!");
                return true;
            }
            else if (HUDSurvivalOverlay.Instance != null)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip("Sem Sementes Silvestres para plantar!");
            }
        }

        return false;
    }

    private void CheckGrowth()
    {
        if (dayNightCycle == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;

        for (int i = activePlots.Count - 1; i >= 0; i--)
        {
            FarmPlot p = activePlots[i];
            if (!p.isGrown)
            {
                float timePassed = dayNightCycle.currentTime - p.plantTime;
                if (timePassed < 0f) timePassed += 24f;

                if (timePassed > 12f)
                {
                    p.isGrown = true;
                    gameManager.UpdateMapCellNative(p.x, p.y, 9);
                    if (mapRenderer != null)
                    {
                        mapRenderer.RemoveInstanceAt(p.x, p.y, 14);
                        mapRenderer.AddInstanceAt(p.x, p.y, 9);
                    }
                    activePlots.RemoveAt(i);
                }
            }
        }
    }
}
