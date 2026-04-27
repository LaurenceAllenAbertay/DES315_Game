using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

//Attach to the win screen panel GameObject -EM//
//Populates stats automatically when the panel is activated//
public class WinScreenUI : MonoBehaviour
{
    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Stat Boxes")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI enemiesKilledText;
    public TextMeshProUGUI damageTakenText;
    public TextMeshProUGUI roomsExploredText;
    public TextMeshProUGUI chestsOpenedText;

    private void OnEnable()
    {
        PopulateStats();
    }

    private void PopulateStats()
    {
        RunScoreManager rsm = RunScoreManager.Instance;

        if (rsm == null)
        {
            Debug.LogWarning("[WinScreenUI] No RunScoreManager found - stats will be empty.");
            return;
        }

        int minutes = Mathf.FloorToInt(rsm.TimeTaken / 60f);
        int seconds = Mathf.FloorToInt(rsm.TimeTaken % 60f);

        if (scoreText != null)         scoreText.text         = $"Score\n{rsm.CalculateScore():N0}";
        if (timeText != null)          timeText.text          = $"Time\n{minutes:00}:{seconds:00}";
        if (enemiesKilledText != null) enemiesKilledText.text = $"Enemies\nKilled\n{rsm.EnemiesKilled}";
        if (damageTakenText != null)   damageTakenText.text   = $"Damage\nTaken\n{Mathf.RoundToInt(rsm.DamageTaken)}";
        if (roomsExploredText != null) roomsExploredText.text = $"Rooms\nExplored\n{rsm.RoomsExplored}";
        if (chestsOpenedText != null)  chestsOpenedText.text  = $"Chests\nOpened\n{rsm.ChestsOpened}";
    }

    //Wire this to the main menu button's OnClick event in the Inspector -EM//
    public void ReturnToMainMenu()
    {
        PauseStack.Pop();

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Debug.LogWarning("[WinScreenUI] mainMenuSceneName is not set.");
    }
}