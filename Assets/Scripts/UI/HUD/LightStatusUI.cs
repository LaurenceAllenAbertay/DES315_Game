 using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LightStatusUI : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;
    public TextMeshProUGUI statusText;
    public Image statusIndicator;
    public Image shadowOverlay;

    [Header("Colors")]
    public Color lightColor = new Color(1f, 0.9f, 0.5f); 
    public Color shadowColor = new Color(0.2f, 0.2f, 0.4f); 

    [Header("Animation")]
    public float colorTransitionSpeed = 5f;
    public float overlayTransitionSpeed = 3f;
    public float maxShadowAlpha = 0.7f;

    private Color targetColor;
    private float targetOverlayAlpha;

    private void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        if (player != null)
        {
            player.OnLightStateChanged += HandleLightStateChanged;
        }

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.OnLightStateChanged -= HandleLightStateChanged;
        }
    }

    private void Update()
    {
        UpdateUI();

        if (statusIndicator != null)
        {
            statusIndicator.color = Color.Lerp(statusIndicator.color, targetColor,
                                                colorTransitionSpeed * Time.deltaTime);
        }

        if (shadowOverlay != null)
        {
            Color currentColor = shadowOverlay.color;
            float newAlpha = Mathf.Lerp(currentColor.a, targetOverlayAlpha, overlayTransitionSpeed * Time.deltaTime);
            shadowOverlay.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);
        }
    }

    private void HandleLightStateChanged(bool inLight)
    {
        // In future, stuff will go here probably
    }

    private void UpdateUI()
    {
        if (player == null) return;

        bool inLight = player.IsInLight;

        if (statusText != null)
        {
            statusText.text = inLight ? "IN LIGHT" : "IN SHADOW";
            statusText.color = inLight ? lightColor : shadowColor;
        }

        targetColor = inLight ? lightColor : shadowColor;
        targetOverlayAlpha = inLight ? 0f : maxShadowAlpha;
    }
}