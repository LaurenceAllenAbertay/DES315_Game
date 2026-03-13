using UnityEngine;
using UnityEngine.UI;

public class LightStatusUI : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;
    public Image shadowOverlay;
    public Image shadowBorder;

    [Header("Overlay")]
    public float overlayTransitionSpeed = 3f;
    public float maxOverlayAlpha = 0.7f;
    [Tooltip("Multiplier on light contribution before clamping — increase to suppress the overlay at lower light levels")]
    public float overlayLightScale = 2f;

    [Header("Border")]
    public float borderTransitionSpeed = 3f;
    public float maxBorderAlpha = 1f;

    private void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (shadowOverlay != null)
        {
            Color c = shadowOverlay.color;
            shadowOverlay.color = new Color(c.r, c.g, c.b, 0f);
        }

        if (shadowBorder != null)
        {
            Color c = shadowBorder.color;
            shadowBorder.color = new Color(c.r, c.g, c.b, 0f);
        }
    }

    private void Update()
    {
        if (player == null) return;

        float lightLevel = Mathf.Clamp01(player.CurrentLightLevel * overlayLightScale);
        float shadowAmount = 1f - lightLevel;

        if (shadowOverlay != null)
        {
            float targetAlpha = shadowAmount * maxOverlayAlpha;
            Color c = shadowOverlay.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, overlayTransitionSpeed * Time.deltaTime);
            shadowOverlay.color = c;
        }

        if (shadowBorder != null)
        {
            float targetAlpha = player.IsInLight ? 0f : maxBorderAlpha;
            Color c = shadowBorder.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, borderTransitionSpeed * Time.deltaTime);
            shadowBorder.color = c;
        }
    }
}