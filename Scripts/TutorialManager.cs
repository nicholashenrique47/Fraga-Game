using UnityEngine;

/// TutorialManager: Guia o jogador nos primeiros minutos de jogo.
/// Dá dicas através da UI e avança ao concluir objetivos.
public class TutorialManager : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform playerTransform;
    public InventoryAdvanced inventory;
    public UIManager uiManager;

    private int tutorialState = 0;
    private Vector3 startPos;
    private float stateTimer = 0f;

    private void Start()
    {
        // Apenas corre se for um novo jogo (Dia 1, de manhã)
        DayNightCycle dnc = FindAnyObjectByType<DayNightCycle>();
        if (dnc != null && (dnc.currentDay > 1 || dnc.currentTime > 10f))
        {
            tutorialState = 99; // Skipped
            return;
        }

        if (playerTransform != null) startPos = playerTransform.position;
        ShowMessage("Usa o Joystick Esquerdo para te moveres.");
    }

    private void Update()
    {
        if (tutorialState == 99 || playerTransform == null || inventory == null) return;

        stateTimer += Time.deltaTime;
        
        switch (tutorialState)
        {
            case 0: // Mover
                if (Vector3.Distance(playerTransform.position, startPos) > 3f)
                {
                    tutorialState = 1;
                    ShowMessage("Fica perto de Troncos Caídos e prime o botão Acão para recolher Madeira.");
                }
                break;
                
            case 1: // Apanhar Madeira
                if (inventory.HasItem(100)) // 100 = Madeira
                {
                    tutorialState = 2;
                    ShowMessage("Abre o menu CRAFT à direita. Cria um Machado e depois um Kit de Fogueira.");
                }
                break;
                
            case 2: // Craft Fogueira
                if (inventory.HasItem(400)) // 400 = Kit de Fogueira
                {
                    tutorialState = 3;
                    stateTimer = 0f;
                    ShowMessage("Muito bem. Abre o inventário em baixo, clica no Kit de Fogueira e usa-o.");
                }
                break;
                
            case 3: // Construir e fim
                if (stateTimer > 15f)
                {
                    tutorialState = 99;
                    ShowMessage("Sobrevive à noite. Mantém a sanidade perto do fogo.");
                }
                break;
        }
    }

    private void ShowMessage(string msg)
    {
        stateTimer = 0f;
        if (uiManager != null) uiManager.SetLootTooltip($"[TUTORIAL]\n{msg}");
        Debug.Log($"[Tutorial] {msg}");
    }
}
