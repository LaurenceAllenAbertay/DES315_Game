using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private Animator menuAnimator;
    [SerializeField] private string playTriggerName = "PlayGame";
    [SerializeField] private string animaticSceneName = "Animatic";
    [SerializeField] private AudioClip startGameClip;
    [SerializeField] private AudioSource backgroundMusicAudioSource;
    [SerializeField] private float backgroundMusicFadeDuration = 1f;

    private AudioSource gameAudioSource;
    private bool isStartingGame = false;

    private void Start()
    {
        gameAudioSource = GetComponent<AudioSource>();
    }

    public void StartGame()
    {
        if (isStartingGame)
        {
            return;
        }

        isStartingGame = true;

        if (menuAnimator != null)
        {
            menuAnimator.SetTrigger(playTriggerName);
        }

        if (backgroundMusicAudioSource != null && backgroundMusicAudioSource.isPlaying)
        {
            StartCoroutine(FadeOutBackgroundMusic());
        }

        StartCoroutine(PlayStartAudioThenLoad());
    }

    private IEnumerator PlayStartAudioThenLoad()
    {
        if (gameAudioSource != null && startGameClip != null)
        {
            gameAudioSource.PlayOneShot(startGameClip);
            yield return new WaitForSeconds(3.5f);
        }

        SceneManager.LoadScene(animaticSceneName);
    }

    private IEnumerator FadeOutBackgroundMusic()
    {
        if (backgroundMusicFadeDuration <= 0f)
        {
            backgroundMusicAudioSource.Stop();
            backgroundMusicAudioSource.volume = 0f;
            yield break;
        }

        float startVolume = backgroundMusicAudioSource.volume;
        float elapsed = 0f;

        while (elapsed < backgroundMusicFadeDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / backgroundMusicFadeDuration);
            backgroundMusicAudioSource.volume = Mathf.Lerp(startVolume, 0f, normalizedTime);
            yield return null;
        }

        backgroundMusicAudioSource.Stop();
        backgroundMusicAudioSource.volume = 0f;
    }

    public void OpenSettings()
    {

    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}