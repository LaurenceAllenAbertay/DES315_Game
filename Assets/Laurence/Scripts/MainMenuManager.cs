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
    private AudioSource startGameAudioSource;
    [SerializeField] private AudioClip startGameClip;

    private bool hasTriggeredAnimation = false;
    private bool isStartingGame = false;

    private void Start()
    {
        startGameAudioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (!hasTriggeredAnimation)
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                TriggerTitleAnimation();
            }
        }
    }

    private void TriggerTitleAnimation()
    {
        hasTriggeredAnimation = true;
        
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

        StartCoroutine(PlayStartAudioThenLoad());
    }

    private IEnumerator PlayStartAudioThenLoad()
    {
        if (startGameAudioSource != null && startGameClip != null)
        {
            startGameAudioSource.PlayOneShot(startGameClip);
            yield return new WaitForSeconds(2);
        }

        SceneManager.LoadScene(1);
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
