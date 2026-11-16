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

    private Vector3 gameCameraPos = new Vector3(384f, 18f, 374f); // Posicao de jogo isométrica dinâmica
    private Vector3 menuCameraPos = new Vector3(384f, 11f, 384f); // Posicao cinematográfica rasteira dinâmica

    private void Start()
    {
        Time.timeScale = 0f;
        FragaWorldBlueprint.EnsureInitialized();
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Fraga_MasterVolume", 1.0f));

        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        float cx = gameManager != null ? gameManager.mapWidth * 0.5f : FragaWorldBlueprint.CrashCampPos.x;
        float cz = gameManager != null ? gameManager.mapHeight * 0.5f : FragaWorldBlueprint.CrashCampPos.y;
        float groundY = TerrainMeshGenerator.GetTerrainHeight(cx, cz);
        menuCameraPos = new Vector3(cx, groundY + 5f, cz);
        gameCameraPos = new Vector3(cx, groundY + 15f, cz - 10f);

        if (mainCamera == null) mainCamera = Camera.main;

        FirstPersonCamera fpCam = mainCamera != null ? mainCamera.GetComponent<FirstPersonCamera>() : null;
        if (fpCam != null || (mainCamera != null && mainCamera.transform.parent != null))
        {
            mainCamera.orthographic = false;
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.transform.localPosition = new Vector3(0f, 1.75f, 0.15f);
            mainCamera.transform.localRotation = Quaternion.identity;
        }
        else if (mainCamera != null)
        {
            mainCamera.orthographic = false;
            mainCamera.clearFlags = CameraClearFlags.Skybox;
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
                playButton.GetComponentInChildren<Text>().text = "INICIAR JORNADA";
                playButton.interactable = true; // Permite iniciar jogo novo diretamente
            }
        }

        if (btnOpenNewGame != null) btnOpenNewGame.onClick.AddListener(() => SwitchPanel(newGamePanel));
        if (btnOpenSettings != null) btnOpenSettings.onClick.AddListener(() => SwitchPanel(settingsPanel));

        // === NEW GAME PANEL ===
        if (btnCreateWorld != null) btnCreateWorld.onClick.AddListener(OnCreateWorldPressed);
        if (btnBackFromNewGame != null) btnBackFromNewGame.onClick.AddListener(() => SwitchPanel(mainPanel));

        // === SETTINGS PANEL ===
        if (btnBackFromSettings != null) btnBackFromSettings.onClick.AddListener(() => { PlayerPrefs.Save(); SwitchPanel(mainPanel); });
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = AudioListener.volume;
            masterVolumeSlider.onValueChanged.AddListener((val) =>
            {
                float clamped = Mathf.Clamp01(val);
                AudioListener.volume = clamped;
                PlayerPrefs.SetFloat("Fraga_MasterVolume", clamped);
            });
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
        if (System.IO.File.Exists(Application.persistentDataPath + "/fraga_save_v2.dat"))
        {
            if (gameManager != null) gameManager.ContinueGame();
        }
        else
        {
            if (gameManager != null) gameManager.StartNewGame(1913);
        }
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
                parsedSeed = seedInput.text.GetHashCode();
            }
        }

        if (gameManager != null) gameManager.StartNewGame(parsedSeed);

        StartCoroutine(TransitionToGame());
    }

    private IEnumerator TransitionToGame()
    {
        SwitchPanel(null);

        if (audioManager != null) audioManager.PlayHorrorSting();

        FirstPersonCamera fpCam = mainCamera != null ? mainCamera.GetComponent<FirstPersonCamera>() : null;
        bool isFirstPerson = (fpCam != null) || (mainCamera != null && mainCamera.transform.parent != null);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 2.0f;
            if (mainMenuCanvasGroup != null) mainMenuCanvasGroup.alpha = 1f - t;

            if (mainCamera != null && !isFirstPerson)
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

        if (mainCamera != null && isFirstPerson)
        {
            mainCamera.transform.localPosition = new Vector3(0f, 1.75f, 0.15f);
            mainCamera.transform.localRotation = Quaternion.identity;
        }

        if (uiManager != null) uiManager.gameObject.SetActive(true);

        Time.timeScale = 1f;

        // Garante enquadramento imediato no jogador pelo IsometricCamera se estiver em modo isométrico
        var isoCam = mainCamera != null ? mainCamera.GetComponent<IsometricCamera>() : FindAnyObjectByType<IsometricCamera>();
        if (isoCam != null && !isFirstPerson)
        {
            isoCam.SnapToTarget();
        }
    }
}
