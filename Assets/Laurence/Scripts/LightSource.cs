using UnityEngine;

/// <summary>
/// Attach this to any GameObject to make it a light source
/// Defines gameplay relevant light properties for shadow detection
/// </summary>
public class LightSource : MonoBehaviour
{
    [Header("Light Properties")]
    [Tooltip("How far this light reaches for gameplay purposes")]
    public float strength = 10f;

    [Tooltip("Layer mask for objects that block light")]
    public LayerMask occluderMask = ~0; // Default: everything blocks light

    [Header("Debug")]
    [SerializeField] private bool drawDebugRays = true;

    private Light unityLight;

    private void Awake()
    {
        unityLight = GetComponent<Light>();
    }

    private void OnEnable()
    {
        LightDetectionManager.RegisterLight(this);
    }

    private void OnDisable()
    {
        LightDetectionManager.UnregisterLight(this);
    }

    /// <summary>
    /// Check if this light can see the target point
    /// </summary>
    /// <param name="targetPoint">World position to check</param>
    /// <param name="lightContribution">Output: 0-1 value based on distance (1 = close, 0 = at max range)</param>
    /// <returns>True if light reaches the target unobstructed</returns>
    public bool CanReachPoint(Vector3 targetPoint, out float lightContribution)
    {
        lightContribution = 0f;

        Vector3 toTarget = targetPoint - transform.position;
        float distance = toTarget.magnitude;

        if (distance > strength)
        {
            return false;
        }

        // Check for obstacles
        Ray ray = new Ray(transform.position, toTarget.normalized);
        bool isBlocked = Physics.Raycast(ray, distance, occluderMask);

        if (drawDebugRays)
        {
            Color rayColor = isBlocked ? Color.red : Color.yellow;
            Debug.DrawLine(transform.position, targetPoint, rayColor);
        }

        if (isBlocked)
        {
            return false;
        }

        // Calculate contribution based on distance (inverse falloff)
        lightContribution = 1f - (distance / strength);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize light range in editor
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, strength);
    }
}