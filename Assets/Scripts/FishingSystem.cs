using UnityEngine;

/// <summary>
/// FishingSystem: Minigame ativo de pesca com lance, espera da boia e janela de fisgada.
/// Acionado pelo InteractionDetector ([E]/Botao Interagir) ou Ataque com Vara de Pesca (306) equipada.
/// </summary>
public class FishingSystem : MonoBehaviour
{
    public static FishingSystem Instance { get; private set; }

    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public GameManager gameManager;

    private enum FishingState { Idle, WaitingBite, BiteReady }
    private FishingState state = FishingState.Idle;
    private float stateTimer = 0f;
    private AudioSource audioSource;
    private AudioClip splashClip;
    private AudioClip biteAlertClip;

    public bool IsFishing => state != FishingState.Idle;
    public bool HasBite => state == FishingState.BiteReady;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.25f;
        audioSource.volume = 0.7f;
        splashClip = BuildSplashClip(220f, 0.22f);
        biteAlertClip = BuildSplashClip(540f, 0.16f);
    }

    private void Start()
    {
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
    }

    private void Update()
    {
        if (state == FishingState.Idle) return;

        // Se o jogador se afastar demais da agua, cancela silenciosamente
        if (!IsNearWater())
        {
            state = FishingState.Idle;
            return;
        }

        stateTimer -= Time.deltaTime;
        if (state == FishingState.WaitingBite && stateTimer <= 0f)
        {
            state = FishingState.BiteReady;
            stateTimer = 2.1f; // Janela para puxar
            if (audioSource != null && biteAlertClip != null) audioSource.PlayOneShot(biteAlertClip);
            if (FirstPersonCamera.Instance != null) FirstPersonCamera.Instance.AddShake(0.04f);
            ShowTooltip("PEIXE FISGOU! Puxe agora!");
        }
        else if (state == FishingState.BiteReady && stateTimer <= 0f)
        {
            state = FishingState.Idle;
            ShowTooltip("O peixe escapou... Tente lancar novamente!");
        }
    }

    public void TryFish()
    {
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (inventory == null) inventory = InventoryAdvanced.Instance;
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero || inventory == null) return;

        if (!inventory.HasItem(306))
        {
            ShowTooltip("Voce precisa de uma Vara de Pesca!");
            return;
        }

        if (!IsNearWater())
        {
            state = FishingState.Idle;
            ShowTooltip("Aproxime-se do lago, rio ou mar para pescar.");
            return;
        }

        if (state == FishingState.Idle)
        {
            PlayerStatus status = PlayerStatus.Instance;
            if (status != null)
            {
                if (status.stamina < 4f)
                {
                    ShowTooltip("Sem folego para lancar a linha!");
                    return;
                }
                status.stamina = Mathf.Max(0f, status.stamina - 6f);
            }

            state = FishingState.WaitingBite;
            stateTimer = Random.Range(2.0f, 3.6f);
            if (AudioManager.Instance != null && playerTransform != null)
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.WaterSplashIn, playerTransform.position + playerTransform.forward * 2.2f, 0.70f, 0.94f, 1.08f);
            else if (audioSource != null && splashClip != null)
                audioSource.PlayOneShot(splashClip);
            if (FirstPersonViewmodelController.Instance != null)
            {
                FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(0, false);
            }
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayBuildPlaceVFX(playerTransform.position + playerTransform.forward * 2.2f);
            }
            ShowTooltip("Linha lancada... Aguarde a fisgada!");
            return;
        }

        if (state == FishingState.WaitingBite)
        {
            ShowTooltip("Ainda nao mordeu... Aguarde a fisgada!");
            return;
        }

        if (state == FishingState.BiteReady)
        {
            state = FishingState.Idle;
            AddOrDrop(111, 1);

            float bonusRoll = Random.value;
            string extraMsg = "";
            if (bonusRoll < 0.18f)
            {
                AddOrDrop(107, 1);
                extraMsg = " + 1 Ferro Velho";
            }

            if (inventory.equippedMainHandId == 306)
            {
                inventory.DamageEquippedTool(EquipSlotType.MainHand, 1f);
            }

            if (FirstPersonViewmodelController.Instance != null)
            {
                FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(1, true);
            }
            if (AudioManager.Instance != null && playerTransform != null)
                AudioManager.Instance.PlayFoley(AudioManager.FoleyEventType.WaterSplashIn, playerTransform.position + playerTransform.forward * 2.0f, 0.80f, 0.88f, 1.04f);
            else if (audioSource != null && splashClip != null)
                audioSource.PlayOneShot(splashClip);
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayBuildPlaceVFX(playerTransform.position + playerTransform.forward * 2f);
            }

            ShowTooltip($"Pescou +1 Peixe Cru{extraMsg}!");
        }
    }

    private bool IsNearWater()
    {
        if (playerTransform == null || gameManager == null || gameManager.mapDataPtr == System.IntPtr.Zero) return false;

        int pX = Mathf.RoundToInt(playerTransform.position.x);
        int pY = Mathf.RoundToInt(playerTransform.position.z);

        for (int x = pX - 4; x <= pX + 4; x++)
        {
            for (int y = pY - 4; y <= pY + 4; y++)
            {
                if (x >= 0 && x < gameManager.mapWidth && y >= 0 && y < gameManager.mapHeight)
                {
                    int tile = ResourceManager.GetMapCell(gameManager.mapDataPtr, gameManager.mapWidth, gameManager.mapHeight, x, y);
                    if (tile == 11 || tile == 7) return true;
                }
            }
        }

        for (int dz = -4; dz <= 4; dz += 2)
        {
            for (int dx = -4; dx <= 4; dx += 2)
            {
                if (FragaWorldBlueprint.GetWaterDepth(playerTransform.position.x + dx, playerTransform.position.z + dz) > 0.15f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void AddOrDrop(int itemId, int amount)
    {
        if (inventory == null || amount <= 0) return;
        int leftover = inventory.AddItem(itemId, amount);
        if (leftover > 0 && LootManager.Instance != null && playerTransform != null)
        {
            LootManager.Instance.DropItem(itemId, leftover, playerTransform.position + playerTransform.forward + Vector3.up * 0.4f);
        }
    }

    private static void ShowTooltip(string msg)
    {
        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
    }

    private static AudioClip BuildSplashClip(float freq, float dur)
    {
        int sr = 22050;
        int len = (int)(sr * dur);
        float[] data = new float[len];
        uint seed = 72013u;
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float env = Mathf.Exp(-10f * t);
            seed = seed * 1664525u + 1013904223u;
            float w = ((seed >> 16) / 65535f) * 2f - 1f;
            lp1 = Mathf.Lerp(lp1, w, 0.30f);
            lp2 = Mathf.Lerp(lp2, lp1, 0.09f);
            data[i] = Mathf.Clamp((Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sr)) * 0.42f + (lp1 - lp2) * 0.65f) * env * 0.65f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Fishing_SFX", len, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
