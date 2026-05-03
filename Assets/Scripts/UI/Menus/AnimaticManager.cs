using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class AnimaticManager : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private string gameSceneName = "Game";

    [Header("Skip Hold")]
    [SerializeField] private Image skipFillImage;
    [SerializeField] private float skipHoldDuration = 1.5f;

    [Header("Hint Text")]
    [SerializeField] private TMP_Text hintText;

    private float holdTimer = 0f;
    private bool hasLoaded = false;

    private void Start()
    {
        if (skipFillImage != null)
            skipFillImage.fillAmount = 0f;

        if (hintText != null)
            hintText.gameObject.SetActive(false);

        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    private void Update()
    {
        if (hasLoaded)
            return;

        HandleHintText();
        HandleSkipHold();
    }

    private void HandleHintText()
    {
        if (hintText == null || hintText.gameObject.activeSelf)
            return;

        bool anyKeyPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool anyMousePressed = Mouse.current != null && (
            Mouse.current.leftButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame ||
            Mouse.current.middleButton.wasPressedThisFrame);

        if (anyKeyPressed || anyMousePressed)
            hintText.gameObject.SetActive(true);
    }
    
    private void HandleSkipHold()
    {
        if (skipFillImage == null || Mouse.current == null)
            return;

        if (Mouse.current.leftButton.isPressed)
        {
            holdTimer += Time.deltaTime;
            skipFillImage.fillAmount = Mathf.Clamp01(holdTimer / skipHoldDuration);

            if (holdTimer >= skipHoldDuration)
                LoadGameScene();
        }
        else
        {
            holdTimer = 0f;
            skipFillImage.fillAmount = 0f;
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        LoadGameScene();
    }

    private void LoadGameScene()
    {
        if (hasLoaded)
            return;

        hasLoaded = true;
        SceneManager.LoadScene(gameSceneName);
    }
}