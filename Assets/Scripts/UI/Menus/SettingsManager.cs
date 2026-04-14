using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;

    [Header("Display")]
    [SerializeField] private Toggle fullscreenToggle;

    private void Awake()
    {
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(isFullscreen => Screen.fullScreen = isFullscreen);
        }
    }

    private void OnDisable()
    {
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveAllListeners();
    }
}