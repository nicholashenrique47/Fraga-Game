using UnityEngine;
using System.Runtime.InteropServices;

public class MineableRockNode : MonoBehaviour
{
    public Vector2Int gridKey;
    public int tileID = 8;
    public bool isPermanent = false;
    public int maxHitsBeforeCooldown = 4;
    public int remainingHits = 4;
    public float cooldownSeconds = 90f;
    private float cooldownUntil = 0f;

    public bool IsOnCooldown(out int waitSeconds)
    {
        if (!isPermanent || cooldownUntil <= 0f)
        {
            waitSeconds = 0;
            return false;
        }
        float rem = cooldownUntil - Time.time;
        if (rem <= 0f)
        {
            cooldownUntil = 0f;
            remainingHits = Mathf.Max(1, maxHitsBeforeCooldown);
            waitSeconds = 0;
            return false;
        }
        waitSeconds = Mathf.CeilToInt(rem);
        return true;
    }

    public bool TryConsumeStrike(out int hitsLeft, out int waitSeconds)
    {
        if (!isPermanent)
        {
            hitsLeft = 1;
            waitSeconds = 0;
            return true;
        }

        if (IsOnCooldown(out waitSeconds))
        {
            hitsLeft = 0;
            return false;
        }

        if (remainingHits <= 0) remainingHits = Mathf.Max(1, maxHitsBeforeCooldown);
        remainingHits--;
        hitsLeft = remainingHits;
        if (remainingHits <= 0)
        {
            cooldownUntil = Time.time + cooldownSeconds;
        }
        return true;
    }

    public void ResetNode(Vector2Int key, int tile, bool permanent)
    {
        gridKey = key;
        tileID = tile;
        isPermanent = permanent;
        remainingHits = Mathf.Max(1, maxHitsBeforeCooldown);
        cooldownUntil = 0f;
    }
}

public class ResourceManager : MonoBehaviour
{
    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetMapCell(System.IntPtr mapData, int width, int height, int x, int y);

    [DllImport("FragaNative", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UpdateMapCell(System.IntPtr mapData, int width, int height, int x, int y, int newTileID);

    [Header("Dependencies")]
    public float interactionRange = 1.6f;
    public Transform playerTransform;
    public InventoryAdvanced inventory;

    public System.IntPtr MapDataPtr { get; private set; }
    public int MapWidth { get; private set; } = FragaWorldBlueprint.DefaultWidth;
    public int MapHeight { get; private set; } = FragaWorldBlueprint.DefaultHeight;

    private readonly System.Collections.Generic.Dictionary<Vector2Int, int> resourceHealth = new System.Collections.Generic.Dictionary<Vector2Int, int>();
    private MapRenderer mapRendererCache;
    private UIManager uiCache;

    public void Initialize(System.IntPtr ptr, int width, int height)
    {
        MapDataPtr = ptr;
        MapWidth = width;
        MapHeight = height;
    }

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        mapRendererCache = FindAnyObjectByType<MapRenderer>();
        uiCache = FindAnyObjectByType<UIManager>();
    }

    public void TryHarvest()
    {
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (playerTransform == null || MapDataPtr == System.IntPtr.Zero) return;

        Vector3 interactPos = playerTransform.position + playerTransform.forward * interactionRange;
        TryHarvestAtGrid(Mathf.RoundToInt(interactPos.x), Mathf.RoundToInt(interactPos.z));
    }

    public void TryHarvestAtGrid(int gridX, int gridY)
    {
        if (MapDataPtr == System.IntPtr.Zero) return;
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (inventory == null) return;
        if (gridX < 0 || gridX >= MapWidth || gridY < 0 || gridY >= MapHeight) return;

        int tileID = GetMapCell(MapDataPtr, MapWidth, MapHeight, gridX, gridY);
        Vector2Int posKey = new Vector2Int(gridX, gridY);
        Vector3 vfxPos = new Vector3(gridX, TerrainMeshGenerator.GetTerrainHeight(gridX, gridY) + 0.8f, gridY);

        ItemData weapon = inventory.equippedMainHandId > 0 ? ItemDatabase.GetItem(inventory.equippedMainHandId) : null;

        if (tileID == 1)
        {
            if (!resourceHealth.ContainsKey(posKey)) resourceHealth[posKey] = 3;

            float treeMult = weapon != null && weapon.harvestTreeMultiplier > 1.01f
                ? weapon.harvestTreeMultiplier
                : (inventory.HasItem(312) ? 3.0f : (inventory.HasItem(300) ? 2.2f : 1.0f));

            int amount = treeMult >= 2.8f ? 3 : (treeMult >= 1.8f ? 2 : 1);
            int resinAmount = Random.value < (treeMult >= 1.8f ? 0.45f : 0.25f) ? 1 : 0;
            int herbAmount = Random.value < 0.25f ? 1 : 0;
            string bonusText = treeMult >= 2.8f ? " (Machado Ferro)" : (treeMult >= 1.8f ? " (Bonus Machado)" : "");

            AddOrDrop(100, amount, vfxPos);
            if (resinAmount > 0) AddOrDrop(119, resinAmount, vfxPos);
            if (herbAmount > 0) AddOrDrop(103, herbAmount, vfxPos);

            resourceHealth[posKey]--;

            string extra = "";
            if (resinAmount > 0) extra += ", +1 Resina";
            if (herbAmount > 0) extra += ", +1 Ervas";

            if (resourceHealth[posKey] <= 0)
            {
                UpdateMapCell(MapDataPtr, MapWidth, MapHeight, gridX, gridY, 0);
                resourceHealth.Remove(posKey);
                if (mapRendererCache == null) mapRendererCache = FindAnyObjectByType<MapRenderer>();
                if (mapRendererCache != null) mapRendererCache.RemoveInstanceAt(gridX, gridY, 1);

                ShowMsg($"+{amount} Madeira{extra}{bonusText} [Arvore Derrubada]");
            }
            else
            {
                ShowMsg($"+{amount} Madeira{extra}{bonusText} [Restam {resourceHealth[posKey]}]");
            }

            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlayWoodChipVFX(vfxPos, Vector3.up);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
        }
        else if (tileID == 2 || tileID == 8)
        {
            HarvestRockInternal(posKey, vfxPos, tileID, null, clearNativeTile: true, targetObj: null);
        }
    }

    public void TryHarvestWorldRock(Vector3 hitPos, MineableRockNode node, Collider col)
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (inventory == null) return;

        if (node != null && node.isPermanent)
        {
            if (!node.TryConsumeStrike(out _, out int waitSec))
            {
                ShowMsg($"Monumento esgotado temporariamente — aguarde {waitSec}s");
                return;
            }
        }
        else if (node != null && MapDataPtr != System.IntPtr.Zero)
        {
            int gx = node.gridKey.x;
            int gz = node.gridKey.y;
            if (gx >= 0 && gx < MapWidth && gz >= 0 && gz < MapHeight)
            {
                int nativeTile = GetMapCell(MapDataPtr, MapWidth, MapHeight, gx, gz);
                if (nativeTile == 2 || nativeTile == 8)
                {
                    TryHarvestAtGrid(gx, gz);
                    return;
                }
            }
        }

        Vector2Int key = node != null
            ? node.gridKey
            : new Vector2Int(Mathf.RoundToInt(hitPos.x), Mathf.RoundToInt(hitPos.z));
        int tileId = node != null ? node.tileID : 8;
        GameObject targetObj = node != null ? node.gameObject : (col != null ? col.gameObject : null);
        Vector3 vfxPos = hitPos + Vector3.up * 0.55f;

        HarvestRockInternal(key, vfxPos, tileId, node, clearNativeTile: false, targetObj: targetObj);
    }

    private void HarvestRockInternal(Vector2Int posKey, Vector3 vfxPos, int tileID, MineableRockNode node, bool clearNativeTile, GameObject targetObj)
    {
        bool isPermanent = node != null && node.isPermanent;
        if (!isPermanent && !resourceHealth.ContainsKey(posKey))
            resourceHealth[posKey] = 4;

        ItemData weapon = inventory.equippedMainHandId > 0 ? ItemDatabase.GetItem(inventory.equippedMainHandId) : null;
        float rockMult = weapon != null && weapon.harvestRockMultiplier > 1.01f
            ? weapon.harvestRockMultiplier
            : (inventory.HasItem(301) ? 2.5f : (inventory.HasItem(312) ? 1.6f : 1.0f));

        bool hasPick = rockMult >= 2.0f;
        int amount = rockMult >= 2.3f ? 3 : (rockMult >= 1.4f ? 2 : 1);
        int ironAmount = Random.Range(0, 100) < (hasPick ? 58 : 25) ? (hasPick ? 2 : 1) : 0;
        int saltAmount = Random.Range(0, 100) < (hasPick ? 45 : 22) ? 1 : 0;
        string bonusText = hasPick ? " (Bonus Picareta)" : (rockMult >= 1.4f ? " (Ferramenta)" : " (Maos)");

        AddOrDrop(101, amount, vfxPos);
        if (ironAmount > 0) AddOrDrop(107, ironAmount, vfxPos);
        if (saltAmount > 0) AddOrDrop(106, saltAmount, vfxPos);

        string extras = "";
        if (ironAmount > 0) extras += $", +{ironAmount} Ferro";
        if (saltAmount > 0) extras += $", +{saltAmount} Sal";

        if (!isPermanent)
        {
            resourceHealth[posKey]--;
            if (resourceHealth[posKey] <= 0)
            {
                if (clearNativeTile && MapDataPtr != System.IntPtr.Zero)
                {
                    UpdateMapCell(MapDataPtr, MapWidth, MapHeight, posKey.x, posKey.y, 0);
                }
                resourceHealth.Remove(posKey);

                if (mapRendererCache == null) mapRendererCache = FindAnyObjectByType<MapRenderer>();
                if (mapRendererCache != null)
                {
                    mapRendererCache.RemoveInstanceAt(posKey.x, posKey.y, tileID);
                }
                else if (targetObj != null)
                {
                    Destroy(targetObj);
                }

                string label = tileID == 2 ? "Ruina Minada" : "Rocha Minada";
                ShowMsg($"+{amount} Pedra{extras}{bonusText} [{label}]");
            }
            else
            {
                ShowMsg($"+{amount} Pedra{extras}{bonusText}");
            }
        }
        else
        {
            string remText = node != null && node.remainingHits > 0 ? $" [Restam {node.remainingHits}]" : " [Esgotado 90s]";
            ShowMsg($"+{amount} Pedra{extras}{bonusText}{remText}");
        }

        if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlaySparkVFX(vfxPos, Vector3.up);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSound();
    }

    private void AddOrDrop(int itemId, int amount, Vector3 dropPos)
    {
        if (inventory == null || amount <= 0) return;
        int leftover = inventory.AddItem(itemId, amount);
        if (leftover > 0 && LootManager.Instance != null)
        {
            LootManager.Instance.DropItem(itemId, leftover, dropPos);
        }
    }

    private void ShowMsg(string msg)
    {
        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
        else if (uiCache != null) uiCache.SetLootTooltip(msg);
    }
}
