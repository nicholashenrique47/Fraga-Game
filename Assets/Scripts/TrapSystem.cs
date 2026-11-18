using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TrapInteractable: Componente tatil para Armadilha Pesada (tile 5 / item 401) e Laco para Coelhos (tile 16 / item 406).
/// Suporta segurar [E]/Toque para Armar (quando disparada) ou Recolher/Desarmar (quando armada).
/// </summary>
public class TrapInteractable : MonoBehaviour
{
    public static readonly List<TrapInteractable> AllTraps = new List<TrapInteractable>();

    public int tileID = 5;
    public Vector2Int gridKey;
    public bool isArmed = true;
    public bool isSprung;

    public int ItemId => tileID == 16 ? 406 : 401;
    public string DisplayName => tileID == 16 ? "Laco para Coelhos" : "Armadilha Pesada";

    private void OnEnable()
    {
        if (!AllTraps.Contains(this)) AllTraps.Add(this);
    }

    private void OnDisable()
    {
        AllTraps.Remove(this);
    }

    private void OnDestroy()
    {
        AllTraps.Remove(this);
    }

    public void Configure(int trapTileId, Vector2Int key, bool armed = true)
    {
        tileID = trapTileId == 16 ? 16 : 5;
        gridKey = key;
        SetArmed(armed);
    }

    public void SetArmed(bool armed)
    {
        isArmed = armed;
        isSprung = !armed;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        Transform jawL = transform.Find("Trap_JawL");
        Transform jawR = transform.Find("Trap_JawR");
        if (jawL != null)
        {
            jawL.localPosition = isArmed ? new Vector3(-0.22f, 0.12f, 0f) : new Vector3(-0.07f, 0.14f, 0f);
            jawL.localEulerAngles = isArmed ? new Vector3(0f, 0f, 18f) : new Vector3(0f, 0f, -46f);
        }
        if (jawR != null)
        {
            jawR.localPosition = isArmed ? new Vector3(0.22f, 0.12f, 0f) : new Vector3(0.07f, 0.14f, 0f);
            jawR.localEulerAngles = isArmed ? new Vector3(0f, 0f, -18f) : new Vector3(0f, 0f, 46f);
        }
    }

    public string GetPromptLabel()
    {
        return isArmed
            ? $"Recolher {DisplayName} [E]"
            : $"Armar {DisplayName} [E]";
    }

    public bool Interact(InventoryAdvanced inv, GameManager gm = null, MapRenderer mr = null)
    {
        if (!isArmed)
        {
            SetArmed(true);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.TrapMetalSnap, transform.position, 0.72f);
            if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlaySparkVFX(transform.position + Vector3.up * 0.2f, Vector3.up);
            if (FirstPersonViewmodelController.Instance != null) FirstPersonViewmodelController.Instance.TriggerImpactBounce(0.65f);
            if (HUDSurvivalOverlay.Instance != null)
                HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#88FFAA>{DisplayName} armada e pronta!</color>");
            return true;
        }

        if (inv != null)
        {
            int leftover = inv.AddItem(ItemId, 1);
            if (leftover > 0 && LootManager.Instance != null)
                LootManager.Instance.DropItem(ItemId, leftover, transform.position + Vector3.up * 0.4f);
        }

        if (gm != null && gm.mapDataPtr != System.IntPtr.Zero &&
            gridKey.x >= 0 && gridKey.x < gm.mapWidth && gridKey.y >= 0 && gridKey.y < gm.mapHeight)
        {
            gm.UpdateMapCellNative(gridKey.x, gridKey.y, 0);
        }
        if (mr != null) mr.RemoveInstanceAt(gridKey.x, gridKey.y, tileID);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayPickupSoundForItem(ItemId, transform.position);
        if (HUDSurvivalOverlay.Instance != null)
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"+1 {DisplayName} recolhida na mochila.");

        if (Application.isPlaying) Destroy(gameObject);
        else gameObject.SetActive(false);
        return true;
    }

    public static TrapInteractable FindAtGrid(int gx, int gz)
    {
        for (int i = 0; i < AllTraps.Count; i++)
        {
            TrapInteractable t = AllTraps[i];
            if (t != null && t.gridKey.x == gx && t.gridKey.y == gz) return t;
        }
        return null;
    }
}

public class TrapSystem : MonoBehaviour
{
    private GameManager gameManager;
    private FaunaManager faunaManager;
    private VultoManager vultoManager;
    private UIManager uiCache;
    private AudioManager audioCache;
    
    // Lista de entidades presas (ID, tempo restante)
    // Para simplificar no contexto C#, vamos usar dicionarios 
    // Mapeando a posicao da grid presa ou usar o array de Vultos/Lobos.
    
    private void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        faunaManager = FindAnyObjectByType<FaunaManager>();
        vultoManager = FindAnyObjectByType<VultoManager>();
        uiCache = FindAnyObjectByType<UIManager>();
        audioCache = AudioManager.Instance ?? FindAnyObjectByType<AudioManager>();
    }
    
    private void Update()
    {
        if (faunaManager != null)
        {
            CheckTrapsForRabbits(faunaManager.GetRabbitPosX(), faunaManager.GetRabbitPosY(), faunaManager.maxRabbits);
        }
    }
    
    public void CheckTrapsForWolves(float[] wolfPosX, float[] wolfPosY, int maxWolves)
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;
        if (wolfPosX == null || wolfPosY == null) return;
        
        for (int i = 0; i < maxWolves; i++)
        {
            int gridX = Mathf.RoundToInt(wolfPosX[i]);
            int gridY = Mathf.RoundToInt(wolfPosY[i]);
            
            if (gridX >= 0 && gridX < gameManager.mapWidth && gridY >= 0 && gridY < gameManager.mapHeight)
            {
                int tileID = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, gridX, gridY);
                if (tileID == 5) // Armadilha!
                {
                    TrapInteractable trap = TrapInteractable.FindAtGrid(gridX, gridY);
                    if (trap != null && !trap.isArmed) continue;

                    Debug.Log($"[TrapSystem] Lobo preso na armadilha em {gridX}, {gridY}!");
                    
                    // Danificar o lobo massivamente
                    Vector3 pos = new Vector3(wolfPosX[i], TerrainMeshGenerator.GetTerrainHeight(wolfPosX[i], wolfPosY[i]), wolfPosY[i]);
                    if (faunaManager != null) faunaManager.TryDamageFaunaAt(pos, 100f);
                    PlayTrapTriggerFeedback(pos, "Armadilha Pesada abateu um Lobo!");
                    
                    // Desarmar para rearmar depois ou consumir se nao houver instancia
                    if (trap != null) trap.SetArmed(false);
                    else gameManager.UpdateMapCellNative(gridX, gridY, 0);
                }
            }
        }
    }
    
    public void CheckTrapsForVultos(float[] posXArray, float[] posYArray, int activeVultos)
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;
        if (posXArray == null || posYArray == null) return;
        
        for (int i = 0; i < activeVultos; i++)
        {
            int gridX = Mathf.RoundToInt(posXArray[i]);
            int gridY = Mathf.RoundToInt(posYArray[i]);
            
            if (gridX >= 0 && gridX < gameManager.mapWidth && gridY >= 0 && gridY < gameManager.mapHeight)
            {
                int tileID = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, gridX, gridY);
                if (tileID == 5) // Armadilha!
                {
                    TrapInteractable trap = TrapInteractable.FindAtGrid(gridX, gridY);
                    if (trap != null && !trap.isArmed) continue;

                    Debug.Log($"[TrapSystem] Vulto preso na armadilha em {gridX}, {gridY}!");
                    
                    // Danificar vulto
                    Vector3 pos = new Vector3(posXArray[i], TerrainMeshGenerator.GetTerrainHeight(posXArray[i], posYArray[i]), posYArray[i]);
                    if (vultoManager != null) vultoManager.TryDamageVultoAt(pos, 100f);
                    PlayTrapTriggerFeedback(pos, "Armadilha disparada contra um Vulto!");
                    
                    // Desarmar para rearmar depois ou consumir se nao houver instancia
                    if (trap != null) trap.SetArmed(false);
                    else gameManager.UpdateMapCellNative(gridX, gridY, 0);
                }
            }
        }
    }
    
    public void CheckTrapsForRabbits(float[] posXArray, float[] posYArray, int maxRabbits)
    {
        if (gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return;
        if (posXArray == null || posYArray == null) return;
        
        for (int i = 0; i < maxRabbits; i++)
        {
            int gridX = Mathf.RoundToInt(posXArray[i]);
            int gridY = Mathf.RoundToInt(posYArray[i]);
            
            if (gridX >= 0 && gridX < gameManager.mapWidth && gridY >= 0 && gridY < gameManager.mapHeight)
            {
                int tileID = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, gridX, gridY);
                if (tileID == 16) // Snare Trap!
                {
                    TrapInteractable trap = TrapInteractable.FindAtGrid(gridX, gridY);
                    if (trap != null && !trap.isArmed) continue;

                    Debug.Log($"[TrapSystem] Coelho apanhado no laco em {gridX}, {gridY}!");
                    
                    // Capturar coelho (largar carne)
                    if (faunaManager != null) faunaManager.CatchRabbit(i);
                    Vector3 pos = new Vector3(posXArray[i], TerrainMeshGenerator.GetTerrainHeight(posXArray[i], posYArray[i]), posYArray[i]);
                    PlayTrapTriggerFeedback(pos, "Uma armadilha de laco capturou um Coelho!");
                    
                    // Desarmar para rearmar depois ou consumir se nao houver instancia
                    if (trap != null) trap.SetArmed(false);
                    else gameManager.UpdateMapCellNative(gridX, gridY, 0);
                }
            }
        }
    }

    private void PlayTrapTriggerFeedback(Vector3 pos, string msg)
    {
        if (VFXPoolManager.Instance != null) VFXPoolManager.Instance.PlaySparkVFX(pos + Vector3.up * 0.25f, Vector3.up);
        if (audioCache == null) audioCache = AudioManager.Instance;
        if (audioCache != null) audioCache.PlayFoley(AudioManager.FoleyEventType.TrapMetalSnap, pos, 0.85f, 0.92f, 1.06f);

        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
        else if (uiCache != null) uiCache.SetLootTooltip(msg);
    }
}
