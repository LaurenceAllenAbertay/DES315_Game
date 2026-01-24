using UnityEngine;

/// <summary>
/// Attach this to any Point Light to make it part of the stylized shadow system.
/// The range of the hard shadow circle is determined by the Unity Light's Range value.
/// </summary>
public class StylizedLight : MonoBehaviour
{
    [Header("Range Settings")]
    [Tooltip("If enabled, use custom range instead of the Unity Light's range")]
    public bool useCustomRange = false;
    
    [Tooltip("Custom range for the shadow circle (only used if useCustomRange is true)")]
    public float customRange = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool showRangeGizmo = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 1f, 0f, 0.5f);
    [SerializeField] private float currentRange = 0f; // Shows in inspector
    
    private Light unityLight;
    private bool isRegistered = false;
    
    private void Awake()
    {
        unityLight = GetComponent<Light>();      
    }
    
    private void OnEnable()
    {
        EnsureControllerExists();
        
        StylizedShadowController.RegisterLight(this);
        isRegistered = true;
    }
    
    private void OnDisable()
    {
        if (isRegistered)
        {
            StylizedShadowController.UnregisterLight(this);
            isRegistered = false;
        }
    }
    
    private void Update()
    {
        // Update inspector display
        currentRange = GetRange();
    }
    
    private void EnsureControllerExists()
    {
        if (StylizedShadowController.Instance == null)
        {
            // Try to find existing controller first
            var existing = FindFirstObjectByType<StylizedShadowController>();
            
            if (existing == null)
            {
                Debug.Log("StylizedLight: Creating StylizedShadowController");
                var controllerGO = new GameObject("StylizedShadowController");
                controllerGO.AddComponent<StylizedShadowController>();
            }
        }
    }
    
    /// <summary>
    /// Get the range for this light's shadow circle
    /// </summary>
    public float GetRange()
    {
        if (useCustomRange)
        {
            return customRange;
        }
        
        if (unityLight != null)
        {
            return unityLight.range;
        }
        
        return customRange;
    }
    
    private void OnDrawGizmos()
    {
        if (!showRangeGizmo) return;
        
        // Always draw in editor so we can see the range
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, GetRange());
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw filled sphere when selected
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.15f);
        Gizmos.DrawSphere(transform.position, GetRange());
        
        // Draw solid wire sphere
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, GetRange());
    }
}
