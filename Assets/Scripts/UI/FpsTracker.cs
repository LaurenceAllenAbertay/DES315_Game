using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class FpsTracker : MonoBehaviour
{
    [Tooltip("Update interval in seconds for the FPS readout.")]
    [SerializeField] private float updateInterval = 0.1f;

    private TextMeshProUGUI fpsText;
    private float timeRemaining;
    private int frameCount;
    private float timeAccumulator;

    private void Awake()
    {
        fpsText = GetComponent<TextMeshProUGUI>();
        timeRemaining = updateInterval;
    }

    private void Update()
    {
        float unscaledDelta = Time.unscaledDeltaTime;
        timeRemaining -= unscaledDelta;
        frameCount++;
        timeAccumulator += unscaledDelta;

        if (timeRemaining > 0f)
        {
            return;
        }

        float fps = frameCount > 0 ? frameCount / Mathf.Max(0.0001f, timeAccumulator) : 0f;
        fpsText.text = $"{Mathf.RoundToInt(fps)} fps";

        timeRemaining = updateInterval;
        frameCount = 0;
        timeAccumulator = 0f;
    }
}
