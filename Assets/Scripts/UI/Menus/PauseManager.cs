using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartRunButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string pauseActionName = "OpenPauseMenu";

    [Header("Behavior")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    private bool isPaused;
    private float previousTimeScale = 1f;
    private InputAction pauseAction;

    public event System.Action OnPaused;

    private void Awake()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartRunButton != null) restartRunButton.onClick.AddListener(RestartRun);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        if (inputActions != null)
            pauseAction = inputActions.FindAction(pauseActionName, true);
    }

    private void OnEnable()
    {
        if (pauseAction == null && inputActions != null)
        {
            pauseAction = inputActions.FindAction(pauseActionName, true);
        }

        if (pauseAction != null)
        {
            pauseAction.performed += OnPauseActionPerformed;
            pauseAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPauseActionPerformed;
            pauseAction.Disable();
        }

        if (isPaused)
        {
            isPaused = false;
            PauseStack.Push();
        }
    }

    private void OnPauseActionPerformed(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        if (isPaused) return;

        isPaused = true;
        PauseStack.Push();
        SetPauseUI(true);
        OnPaused?.Invoke();
    }

    public void Resume()
    {
        if (!isPaused) return;

        isPaused = false;
        PauseStack.Pop();
        SetPauseUI(false);
    }

    public void RestartRun()
    {
        ResumeBeforeSceneChange();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        ResumeBeforeSceneChange();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        ResumeBeforeSceneChange();
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #else
                Application.Quit();
        #endif
    }

    public void OpenSettings()
    {
    }

    private void SetPauseUI(bool show)
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(show);
        }

    }

    private void ResumeBeforeSceneChange()
    {
        if (isPaused)
        {
            isPaused = false;
            PauseStack.Pop();
        }

        SetPauseUI(false);
    }

    
}