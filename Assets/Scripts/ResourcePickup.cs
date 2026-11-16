using UnityEngine;

public class ResourcePickup : MonoBehaviour
{
    public int itemID;
    public int amount = 1;
    public float maxLifetime = 180f;
    public bool launchOnStart = true;
    public bool rotateInPlace = true;
    public float magnetRadius = 3.0f;
    public bool markMapHarvestedOnPickup = false;
    public Vector2Int mapGridKey;

    public System.Action<GameObject> onRecycle;

    private bool canPickup = false;
    private bool harvestNotified = false;
    private float spawnTime;
    private float nextFullWarnTime = 0f;

    private Rigidbody rbCache;
    private Transform playerTransformCache;
    private InventoryAdvanced inventoryCache;
    private UIManager uiCache;
    private AudioManager audioCache;
    private MapRenderer mapRendererCache;

    private void OnEnable()
    {
        spawnTime = Time.time;
        harvestNotified = false;
        nextFullWarnTime = 0f;
        canPickup = !launchOnStart;
    }

    private void Start()
    {
        spawnTime = Time.time;
        rbCache = GetComponent<Rigidbody>();
        if (launchOnStart && rbCache != null)
        {
            rbCache.linearVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 5f), Random.Range(-3f, 3f));
            rbCache.angularVelocity = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));
        }

        if (!launchOnStart)
        {
            canPickup = true;
        }

        if (PlayerController.Instance != null) playerTransformCache = PlayerController.Instance.transform;
        inventoryCache = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        uiCache = FindAnyObjectByType<UIManager>();
        audioCache = AudioManager.Instance ?? FindAnyObjectByType<AudioManager>();
        if (markMapHarvestedOnPickup) mapRendererCache = FindAnyObjectByType<MapRenderer>();
    }

    public void ResetPickupState(
        int newItemId,
        int newAmount,
        bool markHarvested,
        Vector2Int gridKey,
        bool launch = false,
        bool rotate = false,
        float lifetime = 0f,
        float magnet = 1.35f)
    {
        itemID = newItemId;
        amount = Mathf.Max(1, newAmount);
        markMapHarvestedOnPickup = markHarvested;
        mapGridKey = gridKey;
        launchOnStart = launch;
        rotateInPlace = rotate;
        maxLifetime = lifetime;
        magnetRadius = magnet;
        spawnTime = Time.time;
        harvestNotified = false;
        nextFullWarnTime = 0f;
        canPickup = !launchOnStart;

        if (markHarvested && mapRendererCache == null)
        {
            mapRendererCache = FindAnyObjectByType<MapRenderer>();
        }
    }

    public void NotifyCollected()
    {
        if (harvestNotified || !markMapHarvestedOnPickup) return;
        harvestNotified = true;
        if (mapRendererCache == null) mapRendererCache = FindAnyObjectByType<MapRenderer>();
        if (mapRendererCache != null) mapRendererCache.MarkPropHarvested(mapGridKey);
    }

    public void CompleteManualCollect()
    {
        NotifyCollected();
        ConsumeOrDestroy();
    }

    private void ConsumeOrDestroy()
    {
        if (onRecycle != null)
        {
            onRecycle.Invoke(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        float age = Time.time - spawnTime;
        if (maxLifetime > 0f && age >= maxLifetime)
        {
            ConsumeOrDestroy();
            return;
        }

        if (!canPickup && age > 1.25f)
        {
            canPickup = true;
        }

        if (!canPickup) return;

        // Rotacao sutil para destacar pickups largados no chao
        if (rotateInPlace && (rbCache == null || rbCache.IsSleeping()))
        {
            transform.Rotate(Vector3.up, 42f * Time.deltaTime, Space.World);
        }

        if (playerTransformCache == null && PlayerController.Instance != null)
            playerTransformCache = PlayerController.Instance.transform;
        if (playerTransformCache == null) return;

        Vector3 targetPos = playerTransformCache.position + Vector3.up * 0.55f;
        float distSq = (transform.position - targetPos).sqrMagnitude;
        float magnetSq = magnetRadius * magnetRadius;

        // Magnetismo suave quando proximo
        if (distSq < magnetSq)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 6.5f);
        }

        // Coleta automatica com verificacao de espaco na mochila
        if (distSq < 1.25f)
        {
            if (inventoryCache == null) inventoryCache = InventoryAdvanced.Instance;
            if (inventoryCache == null) return;

            int leftover = inventoryCache.AddItem(itemID, amount);
            int collected = amount - leftover;

            if (collected > 0)
            {
                ItemData d = ItemDatabase.GetItem(itemID);
                string msg = $"+{collected} {(d != null ? d.itemName : "Item")}";
                if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
                else if (uiCache != null) uiCache.SetLootTooltip(msg);

                if (audioCache == null) audioCache = AudioManager.Instance;
                if (audioCache != null) audioCache.PlayPickupSound();
            }

            if (leftover > 0)
            {
                amount = leftover;
                if (Time.time >= nextFullWarnTime)
                {
                    nextFullWarnTime = Time.time + 2.0f;
                    if (HUDSurvivalOverlay.Instance != null)
                        HUDSurvivalOverlay.Instance.ShowLootTooltip("Mochila cheia!");
                    else if (uiCache != null)
                        uiCache.SetLootTooltip("Mochila cheia!");
                }
            }
            else
            {
                NotifyCollected();
                ConsumeOrDestroy();
            }
        }
    }
}
