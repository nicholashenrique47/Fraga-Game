using UnityEngine;

/// TutorialManager: Guia de objetivos iniciais (Fase 1).
/// Atualiza em tempo real no HUD (HUDSurvivalOverlay) e avanca ao concluir cada marco de sobrevivencia.
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Dependencies")]
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public UIManager uiManager;

    private PlayerStatus playerStatus;
    private DayNightCycle dayNightCycle;

    private int tutorialState = 0;
    private float stateTimer = 0f;
    private float nextPollTime = 0f;
    private int lastStoneCount = -1;
    private int lastWoodCount = -1;
    private int lastFiberCount = -1;
    private int lastDisplayedState = -1;

    public int CurrentStep => tutorialState;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        ResolveDependencies();
        if (dayNightCycle != null && (dayNightCycle.currentDay > 1 && tutorialState < 5))
        {
            tutorialState = 99;
        }
        PushObjectiveUI(forceToast: tutorialState == 0);
    }

    public void SetStep(int step)
    {
        tutorialState = step >= 99 ? 99 : Mathf.Clamp(step, 0, 5);
        stateTimer = 0f;
        lastDisplayedState = -1;
        ResolveDependencies();
        PushObjectiveUI(forceToast: false);
    }

    private void ResolveDependencies()
    {
        if (playerTransform == null && PlayerController.Instance != null) playerTransform = PlayerController.Instance.transform;
        if (inventory == null) inventory = InventoryAdvanced.Instance ?? FindAnyObjectByType<InventoryAdvanced>();
        if (uiManager == null) uiManager = FindAnyObjectByType<UIManager>();
        if (playerStatus == null) playerStatus = PlayerStatus.Instance ?? FindAnyObjectByType<PlayerStatus>();
        if (dayNightCycle == null) dayNightCycle = DayNightCycle.Instance ?? FindAnyObjectByType<DayNightCycle>();
    }

    private void Update()
    {
        if (tutorialState >= 99) return;
        stateTimer += Time.deltaTime;

        if (Time.time < nextPollTime) return;
        nextPollTime = Time.time + 0.35f;

        ResolveDependencies();
        if (inventory == null) return;

        int stones = inventory.GetItemCount(101);
        int wood = inventory.GetItemCount(100);
        int herbs = inventory.GetItemCount(103);
        int rope = inventory.GetItemCount(118);
        bool hasAxeOrHigher = inventory.HasItem(300) || inventory.HasItem(301) || inventory.HasItem(302) || inventory.HasItem(305) || inventory.HasItem(306);

        switch (tutorialState)
        {
            case 0: // Coletar galhos, pedras soltas e ervas ao redor do acampamento
                if (hasAxeOrHigher)
                {
                    AdvanceTo(3);
                    return;
                }
                if (stones >= 2 && wood >= 3 && (herbs >= 2 || rope >= 1))
                {
                    AdvanceTo(rope >= 1 ? 2 : 1);
                    return;
                }
                if (stones != lastStoneCount || wood != lastWoodCount || herbs != lastFiberCount || lastDisplayedState != 0)
                {
                    lastStoneCount = stones;
                    lastWoodCount = wood;
                    lastFiberCount = herbs;
                    PushObjectiveUI(forceToast: false);
                }
                break;

            case 1: // Fabricar Corda de Fibra
                if (hasAxeOrHigher)
                {
                    AdvanceTo(3);
                    return;
                }
                if (rope >= 1)
                {
                    AdvanceTo(2);
                }
                break;

            case 2: // Fabricar Machado de Pedra
                if (hasAxeOrHigher)
                {
                    AdvanceTo(3);
                }
                break;

            case 3: // Criar ou aquecer-se na Fogueira
                if (inventory.HasItem(400) || (playerStatus != null && playerStatus.isNearBonfire && stateTimer >= 2.5f))
                {
                    AdvanceTo(4);
                }
                break;

            case 4: // Construir Refugio ou Cabana (B / OBRA)
                if (FindAnyObjectByType<ShelterInteractable>() != null || (dayNightCycle != null && dayNightCycle.currentDay >= 2))
                {
                    AdvanceTo(5);
                }
                break;

            case 5: // Sobreviver a primeira noite
                if (dayNightCycle != null && dayNightCycle.currentDay >= 2 && !dayNightCycle.isNight)
                {
                    AdvanceTo(99);
                }
                break;
        }
    }

    private void AdvanceTo(int nextStep)
    {
        tutorialState = nextStep;
        stateTimer = 0f;
        lastDisplayedState = -1;
        PushObjectiveUI(forceToast: true);
    }

    private void PushObjectiveUI(bool forceToast)
    {
        if (HUDSurvivalOverlay.Instance == null)
        {
            if (tutorialState < 99) lastDisplayedState = -1;
            return;
        }

        lastDisplayedState = tutorialState;

        if (tutorialState >= 99)
        {
            HUDSurvivalOverlay.Instance.SetObjectiveDisplay(false, string.Empty, string.Empty);
            if (forceToast)
            {
                HUDSurvivalOverlay.Instance.ShowLootTooltip("<color=#66FFAA>Guia inicial concluido! Explore a ilha e o Farol Celta.</color>");
            }
            return;
        }

        string title;
        string detail;
        switch (tutorialState)
        {
            case 0:
                int s = inventory != null ? inventory.GetItemCount(101) : 0;
                int w = inventory != null ? inventory.GetItemCount(100) : 0;
                int h = inventory != null ? inventory.GetItemCount(103) : 0;
                title = "OBJETIVO 1/5: COLETAR RECURSOS";
                detail = $"Pedras {Mathf.Min(s, 2)}/2 | Galhos {Mathf.Min(w, 3)}/3 | Ervas {Mathf.Min(h, 2)}/2 (chao/arbustos)";
                break;
            case 1:
                title = "OBJETIVO 2/5: CORDA DE FIBRA";
                detail = "Abra CRAFT [I] e crie Corda de Fibra (2 Ervas).";
                break;
            case 2:
                title = "OBJETIVO 3/5: MACHADO DE PEDRA";
                detail = "Abra CRAFT [I] e crie o Machado de Pedra (3 Madeira, 2 Pedra, 1 Corda).";
                break;
            case 3:
                title = "OBJETIVO 4/5: FOGUEIRA & CALOR";
                detail = "Aproxime-se da Fogueira do acampamento ou crie um Kit de Fogueira.";
                break;
            case 4:
                title = "OBJETIVO 5/5: CONSTRUIR ABRIGO";
                detail = "Pressione [B] (ou OBRA) para posicionar um Refugio de Fentos.";
                break;
            default:
                title = "SOBREVIVA A 1a NOITE";
                detail = "Mantenha a fogueira acesa, sanidade e descanse no abrigo apos 18h.";
                break;
        }

        HUDSurvivalOverlay.Instance.SetObjectiveDisplay(true, title, detail);

        if (forceToast)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#55DDFF>{title}</color>");
        }
        else if (uiManager != null && tutorialState == 0)
        {
            uiManager.SetLootTooltip($"{title}\n{detail}");
        }
    }
}
