using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Title Animation")]
    [SerializeField] private Animator menuAnimator;
    [SerializeField] private string animationTriggerName = "Start";

    [Header("Start Game")]
    [SerializeField] private string playTriggerName = "Play";
    private AudioSource gameAudioSource;
    [SerializeField] private AudioClip titleClip;
    [SerializeField] private AudioClip startGameClip;
    [SerializeField] private AudioSource backgroundMusicAudioSource;
    [SerializeField] private float backgroundMusicFadeDuration = 1f;

    private bool hasTriggeredAnimation = false;
    private bool isStartingGame = false;

    private void Start()
    {
        gameAudioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (!hasTriggeredAnimation)
        {
            if (IsAnyTitleAdvanceInputPressed())
            {
                TriggerTitleAnimation();
            }
        }
    }

    private bool IsAnyTitleAdvanceInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current == null)
        {
            return false;
        }

        return Mouse.current.leftButton.wasPressedThisFrame
            || Mouse.current.rightButton.wasPressedThisFrame
            || Mouse.current.middleButton.wasPressedThisFrame
            || Mouse.current.forwardButton.wasPressedThisFrame
            || Mouse.current.backButton.wasPressedThisFrame;
    }

    private void TriggerTitleAnimation()
    {
        hasTriggeredAnimation = true;

        if (gameAudioSource != null && titleClip != null)
        {
            gameAudioSource.PlayOneShot(titleClip);
        }

        if (menuAnimator != null)
        {
            menuAnimator.SetTrigger(animationTriggerName);
        }
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

        SceneManager.LoadScene(1);
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
