using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

//Attach to the win screen panel GameObject -EM//
//Populates stats automatically when the panel is activated//
public class WinScreenUI : MonoBehaviour
{
    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Stats Text")]
    public TextMeshProUGUI statsText;

    private void OnEnable()
    {
        PopulateStats();
    }

    private void PopulateStats()
    {
        if (statsText == null) return;

        RunScoreManager rsm = RunScoreManager.Instance;

        if (rsm == null)
        {
            Debug.LogWarning("[WinScreenUI] No RunScoreManager found - stats will be empty.");
            statsText.text = "Stats unavailable.";
            return;
        }

        int minutes = Mathf.FloorToInt(rsm.TimeTaken / 60f);
        int seconds = Mathf.FloorToInt(rsm.TimeTaken % 60f);

        statsText.text =
            $"Score: {rsm.CalculateScore():N0}\n" +
            $"Time: {minutes:00}:{seconds:00}\n" +
            $"Enemies Killed: {rsm.EnemiesKilled}\n" +
            $"Damage Taken: {Mathf.RoundToInt(rsm.DamageTaken)}\n" +
            $"Rooms Explored: {rsm.RoomsExplored}\n" +
            $"Chests Opened: {rsm.ChestsOpened}";
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