using UnityEngine;


/// <summary>
/// Attach this to any GameObject to make it a light source
/// Handles gameplay light detection
/// </summary>
public class LightSource : MonoBehaviour
{
    [Header("Light Properties")]
    [Tooltip("How far this light reaches (used for both gameplay and visuals)")]
    public float strength = 10f;

    [Tooltip("Override how far this light detects units for gameplay. Set to 0 to use Strength.")]
    public float detectionRangeExtension = 0.6f;

    [Tooltip("Layer mask for objects that block light")]
    public LayerMask occluderMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRays = false;

    private Light unityLight;

    private void Awake()
    {
        unityLight = GetComponentInChildren<Light>();
        if (unityLight == null)
        {
            unityLight = gameObject.AddComponent<Light>();
            unityLight.type = LightType.Point;
            unityLight.intensity = 1.0f;
        }
    }

    public Light GetLight() => unityLight;

    private void OnEnable()
    {
        LightDetectionManager.RegisterLight(this);
        HardShadowManager.RegisterLight(this);
    }

    private void OnDisable()
    {
        LightDetectionManager.UnregisterLight(this);
        HardShadowManager.UnregisterLight(this);
    }

    /// <summary>
    /// Check if this light can see the target point
    /// </summary>
    public bool CanReachPoint(Vector3 targetPoint, out float lightContribution)
    {
        lightContribution = 0f;

        float effectiveRange = strength + detectionRangeExtension;

        Vector3 toTarget = targetPoint - transform.position;
        float distance = toTarget.magnitude;

        if (distance > effectiveRange)
        {
            return false;
        }

        Ray ray = new Ray(transform.position, toTarget.normalized);
        bool isBlocked = Physics.Raycast(ray, distance, occluderMask);

        if (isBlocked)
        {
            if (drawDebugRays) Debug.DrawLine(transform.position, targetPoint, Color.red);
            return false;
        }

        if (drawDebugRays) Debug.DrawLine(transform.position, targetPoint, Color.yellow);

        lightContribution = 1f - (distance / effectiveRange);
        return true;
    }

    /// <summary>
    /// Get the range for this light
    /// </summary>
    public float GetRange()
    {
        return strength;
    }

    private void OnDrawGizmosSelected()
    {
        float effectiveRange = strength + detectionRangeExtension;
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, strength);
        Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, effectiveRange);
    }
}