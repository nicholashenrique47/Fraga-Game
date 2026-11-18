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
    private bool lastTouchLayout = false;

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

    private bool IsTouchActive()
    {
        return FragaInputRouter.Instance != null && FragaInputRouter.Instance.IsTouchControlsActive;
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

        bool touchNow = IsTouchActive();
        if (touchNow != lastTouchLayout)
        {
            lastDisplayedState = -1;
            PushObjectiveUI(forceToast: false);
        }

        if (Time.time < nextPollTime) return;
        nextPollTime = Time.time + 0.35f;

        ResolveDependencies();
        if (inventory == null) return;

        int stones = inventory.GetItemCount(101);
        int wood = inventory.GetItemCount(100);
        int herbs = inventory.GetItemCount(103);
        int rope = inventory.GetItemCount(118);
        bool hasAxeOrHigher = inventory.HasItem(300) || inventory.HasItem(301) || inventory.HasItem(312);

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

            case 3: // Cortar Arvore/Rocha ou Aquecer-se na Fogueira
                if (inventory.HasItem(400) || (playerStatus != null && playerStatus.isNearBonfire && stateTimer >= 2.5f))
                {
                    AdvanceTo(4);
                }
                break;

            case 4: // Construir Refugio ou Cabana
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

    public void RefreshUIForCurrentLayout()
    {
        lastDisplayedState = -1;
        PushObjectiveUI(forceToast: false);
    }

    private void PushObjectiveUI(bool forceToast)
    {
        if (HUDSurvivalOverlay.Instance == null)
        {
            if (tutorialState < 99) lastDisplayedState = -1;
            return;
        }

        bool touch = IsTouchActive();
        lastTouchLayout = touch;
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
                title = touch ? "OBJETIVO 1/5: COLETAR DO CHAO (TOQUE PEGAR)" : "OBJETIVO 1/5: COLETAR DO CHAO COM [E]";
                detail = touch
                    ? $"Mire no chao e toque em PEGAR: Pedras {Mathf.Min(s, 2)}/2 | Galhos {Mathf.Min(w, 3)}/3 | Ervas {Mathf.Min(h, 2)}/2"
                    : $"Olhe para o chao e aperte [E]: Pedras {Mathf.Min(s, 2)}/2 | Galhos {Mathf.Min(w, 3)}/3 | Ervas {Mathf.Min(h, 2)}/2";
                break;
            case 1:
                title = touch ? "OBJETIVO 2/5: FAZER CORDA (MOCHILA)" : "OBJETIVO 2/5: FAZER CORDA DE FIBRA [I]";
                detail = touch
                    ? "Toque em MOCHILA (Fabricacao) e crie 'Corda de Fibra (x2)' (usa 2 Ervas)."
                    : "Aperte [I] (Fabricacao) e clique em 'Corda de Fibra (x2)' (usa 2 Ervas).";
                break;
            case 2:
                title = touch ? "OBJETIVO 3/5: FAZER MACHADO (MOCHILA)" : "OBJETIVO 3/5: FAZER MACHADO DE PEDRA [I]";
                detail = touch
                    ? "Toque em MOCHILA e crie 'Machado de Pedra' (3 Madeira + 2 Pedra + 1 Corda)."
                    : "Aperte [I] e crie o 'Machado de Pedra' (3 Madeira + 2 Pedra + 1 Corda).";
                break;
            case 3:
                title = "OBJETIVO 4/5: CORTAR COM MACHADO & FOGUEIRA";
                detail = touch
                    ? "Com o Machado, toque em CORTAR perto de Arvores/Rochas e se aqueca na Fogueira."
                    : "Use Machado no CLIQUE DO MOUSE em Arvores/Rochas e se aqueca na Fogueira [E].";
                break;
            case 4:
                title = touch ? "OBJETIVO 5/5: LIVRO DE CONSTRUCAO (OBRA)" : "OBJETIVO 5/5: LIVRO DE CONSTRUCAO [B]";
                detail = touch
                    ? "Toque em OBRA para abrir o Livro de Sobrevivencia e posicionar um Refugio."
                    : "Pressione [B] para abrir o Livro de Sobrevivencia e posicionar um Refugio.";
                break;
            default:
                title = "SOBREVIVA A 1a NOITE";
                detail = "Mantenha a fogueira acesa, sanidade e descanse no abrigo apos 18h.";
                break;
        }

        HUDSurvivalOverlay.Instance.SetObjectiveDisplay(true, title, detail);
        if (forceToast)
        {
            HUDSurvivalOverlay.Instance.ShowLootTooltip($"<color=#88EEFF>{title}</color>");
        }
    }
}
