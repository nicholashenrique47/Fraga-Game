using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// MainMenuManager: Menu Inicial Nativo (Zero Load Times)
/// O menu sobrepoe-se ao mundo tridimensional da Fraga, criando um efeito imersivo.
public class MainMenuManager : MonoBehaviour
{
    [Header("Dependencies")]
    public CanvasGroup mainMenuCanvasGroup;
    public SaveSystem saveSystem;
    public UIManager uiManager;
    public AudioManager audioManager;
    public Camera mainCamera;
    
    [Header("UI Panels")]
    public GameObject mainPanel;
    public GameObject newGamePanel;
    public GameObject settingsPanel;

    [Header("Main Panel Elements")]
    public Button playButton;
    public Button btnOpenNewGame;
    public Button btnOpenSettings;
    public Text titleText;

    [Header("New Game Elements")]
    public InputField seedInput;
    public Button btnCreateWorld;
    public Button btnBackFromNewGame;

    [Header("Settings Elements")]
    public Slider masterVolumeSlider;
    public Button btnBackFromSettings;

    [Header("Game Reference")]
    public GameManager gameManager;

    private Vector3 gameCameraPos = new Vector3(50f, 15f, 40f); // Posicao de jogo isométrica
    private Vector3 menuCameraPos = new Vector3(50f, 5f, 50f);  // Posicao cinematográfica rasteira

    private void Start()
    {
        Time.timeScale = 0f;
        
        if (mainCamera != null)
        {
            mainCamera.transform.position = menuCameraPos;
            mainCamera.transform.rotation = Quaternion.Euler(15f, 45f, 0f); 
        }

        if (uiManager != null && uiManager.gameObject != this.gameObject)
        {
            uiManager.gameObject.SetActive(false); 
        }

        SetupPanels();
        SetupButtons();
    }

    private void SetupPanels()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (newGamePanel != null) newGamePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void SetupButtons()
    {
        // === MAIN PANEL ===
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayPressed);
            if (System.IO.File.Exists(Application.persistentDataPath + "/fraga_save_v2.dat"))
            {
                playButton.GetComponentInChildren<Text>().text = "CONTINUAR JORNADA";
                playButton.interactable = true;
            }
            else
            {
                playButton.GetComponentInChildren<Text>().text = "SEM SAVE GUARDADO";
                playButton.interactable = false; // Só pode criar mundo se não houver save
            }
        }

        if (btnOpenNewGame != null) btnOpenNewGame.onClick.AddListener(() => SwitchPanel(newGamePanel));
        if (btnOpenSettings != null) btnOpenSettings.onClick.AddListener(() => SwitchPanel(settingsPanel));

        // === NEW GAME PANEL ===
        if (btnCreateWorld != null) btnCreateWorld.onClick.AddListener(OnCreateWorldPressed);
        if (btnBackFromNewGame != null) btnBackFromNewGame.onClick.AddListener(() => SwitchPanel(mainPanel));

        // === SETTINGS PANEL ===
        if (btnBackFromSettings != null) btnBackFromSettings.onClick.AddListener(() => SwitchPanel(mainPanel));
        if (masterVolumeSlider != null) 
        {
            masterVolumeSlider.onValueChanged.AddListener((val) => { AudioListener.volume = val; });
        }
    }

    private void SwitchPanel(GameObject targetPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (newGamePanel != null) newGamePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (targetPanel != null) targetPanel.SetActive(true);
    }

    private void OnPlayPressed()
    {
        if (gameManager != null) gameManager.ContinueGame();
        StartCoroutine(TransitionToGame());
    }

    private void OnCreateWorldPressed()
    {
        // Apaga o save antigo e comeca de novo
        string savePath = Application.persistentDataPath + "/fraga_save_v2.dat";
        if (System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath);
        
        int parsedSeed = 1913; // Default
        if (seedInput != null && !string.IsNullOrEmpty(seedInput.text))
        {
            if (!int.TryParse(seedInput.text, out parsedSeed))
            {
                // Se o user escrever "morte", usamos a hashcode da string
                parsedSeed = seedInput.text.GetHashCode();
            }
        }

        if (gameManager != null) gameManager.StartNewGame(parsedSeed);

        StartCoroutine(TransitionToGame());
    }

    private IEnumerator TransitionToGame()
    {
        // Esconder todos os paineis instantaneamente
        SwitchPanel(null);

        if (audioManager != null) audioManager.PlayHorrorSting();

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 1.5f;
            if (mainMenuCanvasGroup != null) mainMenuCanvasGroup.alpha = 1f - t;
            
            if (mainCamera != null)
            {
                mainCamera.transform.position = Vector3.Lerp(menuCameraPos, gameCameraPos, t);
                mainCamera.transform.rotation = Quaternion.Lerp(Quaternion.Euler(15f, 45f, 0f), Quaternion.Euler(45f, 45f, 0f), t);
            }
            
            yield return null;
        }

        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha = 0f;
            mainMenuCanvasGroup.interactable = false;
            mainMenuCanvasGroup.blocksRaycasts = false;
            mainMenuCanvasGroup.gameObject.SetActive(false);
        }

        if (uiManager != null) uiManager.gameObject.SetActive(true); 

        Time.timeScale = 1f; 
    }
}
