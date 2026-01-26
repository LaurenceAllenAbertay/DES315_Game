using UnityEngine;

/// <summary>
/// Attach this to any GameObject to make it a light source.
/// Handles both gameplay light detection and stylized shadow visuals.
/// </summary>
public class LightSource : MonoBehaviour
{
    [Header("Light Properties")]
    [Tooltip("How far this light reaches (used for both gameplay and visuals)")]
    public float strength = 10f;

    [Tooltip("Layer mask for objects that block light")]
    public LayerMask occluderMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRays = true;

    private void OnEnable()
    {
        // Register with gameplay system
        LightDetectionManager.RegisterLight(this);

        // Register with visual system
        HardShadowManager.RegisterLight(this);
    }

    private void OnDisable()
    {
        // Unregister from both systems
        LightDetectionManager.UnregisterLight(this);
        HardShadowManager.UnregisterLight(this);
    }

    /// <summary>
    /// Check if this light can see the target point (for gameplay)
    /// </summary>
    /// <param name="targetPoint">World position to check</param>
    /// <param name="lightContribution">Output: 1 if in light, 0 if in shadow</param>
    /// <returns>True if light reaches the target unobstructed</returns>
    public bool CanReachPoint(Vector3 targetPoint, out float lightContribution)
    {
        lightContribution = 0f;

        Vector3 toTarget = targetPoint - transform.position;
        float distance = toTarget.magnitude;

        // Outside range = in shadow
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

        // Blocked by obstacle = in shadow
        if (isBlocked)
        {
            return false;
        }

        lightContribution = 1f;
        return true;
    }

    /// <summary>
    /// Get the range for this light (used by stylized shadow system)
    /// </summary>
    public float GetRange()
    {
        return strength;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize light range in editor
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, strength);
    }
}