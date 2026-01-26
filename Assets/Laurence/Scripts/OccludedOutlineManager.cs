using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Toggles URP Render Objects features for occluded outline/silhouette effect.
/// Works with ScriptableRendererFeatures in the Universal Renderer Data.
/// </summary>
public class OccludedOutlineManager : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("Renderer Features")]
    [Tooltip("The Universal Renderer Data asset containing the silhouette features")]
    public UniversalRendererData rendererData;

    [Tooltip("Names of the Render Objects features to toggle (e.g., 'RenderObjects' for Player and Enemy)")]
    public string[] featureNames = new string[] { "RenderObjects" };

    [Header("Debug")]
    [SerializeField] private int foundFeatureCount = 0;
    [SerializeField] private bool outlinesVisible = false;

    private InputAction showOutlinesAction;
    private ScriptableRendererFeature[] targetFeatures;

    private void Awake()
    {
        // Find the target features
        FindTargetFeatures();

        // Setup input
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            showOutlinesAction = playerMap.FindAction("ShowVisionCones");
        }
    }

    private void OnEnable()
    {
        if (showOutlinesAction != null)
        {
            showOutlinesAction.Enable();
        }

        // Start with outlines hidden
        SetFeaturesActive(false);
    }

    private void OnDisable()
    {
        if (showOutlinesAction != null)
        {
            showOutlinesAction.Disable();
        }

        // Restore features when disabled
        SetFeaturesActive(false);
    }

    private void Update()
    {
        if (showOutlinesAction != null)
        {
            bool isPressed = showOutlinesAction.IsPressed();

            if (isPressed != outlinesVisible)
            {
                SetFeaturesActive(isPressed);
                outlinesVisible = isPressed;
            }
        }
    }

    private void FindTargetFeatures()
    {
        if (rendererData == null)
        {
            Debug.LogError("OccludedOutlineManager: No UniversalRendererData assigned!");
            return;
        }

        var featureList = new System.Collections.Generic.List<ScriptableRendererFeature>();

        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature == null) continue;

            // Check if this feature's name matches any of our target names
            foreach (string targetName in featureNames)
            {
                if (feature.name.Contains(targetName))
                {
                    featureList.Add(feature);
                    break;
                }
            }
        }

        targetFeatures = featureList.ToArray();
        foundFeatureCount = targetFeatures.Length;

        if (foundFeatureCount == 0)
        {
            Debug.LogWarning($"OccludedOutlineManager: No features found matching names: {string.Join(", ", featureNames)}");
        }
    }

    private void SetFeaturesActive(bool active)
    {
        if (targetFeatures == null) return;

        foreach (var feature in targetFeatures)
        {
            if (feature != null)
            {
                feature.SetActive(active);
            }
        }
    }

    /// <summary>
    /// Call this if you change the renderer data at runtime
    /// </summary>
    public void RefreshFeatures()
    {
        FindTargetFeatures();
    }
}