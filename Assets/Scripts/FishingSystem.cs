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
            ShowTooltip("Precisas de uma Vara de Pesca!");
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
            state = FishingState.WaitingBite;
            stateTimer = Random.Range(2.0f, 3.6f);
            if (audioSource != null && splashClip != null) audioSource.PlayOneShot(splashClip);
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
            inventory.AddItem(111, 1);

            float bonusRoll = Random.value;
            string extraMsg = "";
            if (bonusRoll < 0.18f)
            {
                inventory.AddItem(107, 1);
                extraMsg = " + 1 Ferro Velho";
            }

            if (FirstPersonViewmodelController.Instance != null)
            {
                FirstPersonViewmodelController.Instance.TriggerPrimaryAttack(1, true);
            }
            if (audioSource != null && splashClip != null) audioSource.PlayOneShot(splashClip);
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayBuildPlaceVFX(playerTransform.position + playerTransform.forward * 2f);
            }

            ShowTooltip($"Pescaste +1 Peixe Cru{extraMsg}!");
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

    private static void ShowTooltip(string msg)
    {
        if (HUDSurvivalOverlay.Instance != null) HUDSurvivalOverlay.Instance.ShowLootTooltip(msg);
    }

    private static AudioClip BuildSplashClip(float freq, float dur)
    {
        int sr = 44100;
        int len = (int)(sr * dur);
        float[] data = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float env = Mathf.Exp(-10f * t);
            data[i] = (Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sr)) * 0.5f + Random.Range(-0.4f, 0.4f)) * env * 0.6f;
        }
        AudioClip clip = AudioClip.Create("Fishing_SFX", len, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
