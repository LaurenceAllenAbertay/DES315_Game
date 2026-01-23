using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LightStatusUI : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;
    public TextMeshProUGUI statusText;
    public Image statusIndicator;
    public TextMeshProUGUI lightLevelText;

    [Header("Colors")]
    public Color lightColor = new Color(1f, 0.9f, 0.5f); 
    public Color shadowColor = new Color(0.2f, 0.2f, 0.4f); 

    [Header("Animation")]
    public float colorTransitionSpeed = 5f;

    private Color targetColor;

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
    }

    private void HandleLightStateChanged(bool inLight, float lightLevel)
    {
        // Debug.Log($"Light state changed: {(inLight ? "LIGHT" : "SHADOW")}");
    }

    private void UpdateUI()
    {
        if (player == null) return;

        bool inLight = player.IsInLight;
        float lightLevel = player.CurrentLightLevel;

        if (statusText != null)
        {
            statusText.text = inLight ? "IN LIGHT" : "IN SHADOW";
            statusText.color = inLight ? lightColor : shadowColor;
        }

        if (lightLevelText != null)
        {
            lightLevelText.text = $"Light Level: {(lightLevel * 100f):F1}";
        }

        targetColor = inLight ? lightColor : shadowColor;
    }
}