using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string pauseActionName = "OpenPauseMenu";

    [Header("Behavior")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    private bool isPaused;
    private float previousTimeScale = 1f;
    private InputAction pauseAction;

    private void Awake()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (inputActions != null)
        {
            pauseAction = inputActions.FindAction(pauseActionName, true);
        }
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
            Time.timeScale = 1f;
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
        if (isPaused)
        {
            return;
        }

        isPaused = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        SetPauseUI(true);
    }

    public void Resume()
    {
        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        Time.timeScale = Mathf.Approximately(previousTimeScale, 0f) ? 1f : previousTimeScale;
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
            Time.timeScale = 1f;
        }

        SetPauseUI(false);
    }

    
}
