using UnityEngine;

/// <summary>
/// Attach this to any GameObject to make it a light source
/// Handles both gameplay light detection and stylized shadow visuals
/// </summary>
public class LightSource : MonoBehaviour
{
    [Header("Light Properties")]
    [Tooltip("How far this light reaches (used for both gameplay and visuals)")]
    public float strength = 10f;

    [Tooltip("Layer mask for objects that block light")]
    public LayerMask occluderMask = ~0;

    [Tooltip("Multiplier applied to the Unity Light range for shadow casting only")]
    [Range(1.0f, 3.0f)]
    public float shadowRangeMultiplier = 1.2f;

    [Header("Shadow Properties")]
    [Tooltip("Should this light cast real-time shadows?")]
    public bool castShadows = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRays = true;

    private Light unityLight;

    private void Awake()
    {
        unityLight = GetComponent<Light>();
        if (unityLight == null)
        {
            unityLight = gameObject.AddComponent<Light>();
            unityLight.type = LightType.Point;
            unityLight.intensity = 1.0f;
            unityLight.color = new Color(0.01f, 0.01f, 0.01f); // Dark enough to be almost invisible
            unityLight.shadowStrength = 1.0f;
        }
    }

    private void Update()
    {
        if (unityLight != null)
        {
            unityLight.range = strength * Mathf.Max(1.0f, shadowRangeMultiplier);
            unityLight.shadows = castShadows ? LightShadows.Hard : LightShadows.None;
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

        Vector3 toTarget = targetPoint - transform.position;
        float distance = toTarget.magnitude;

        if (distance > strength)
        {
            return false;
        }

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
    /// Get the range for this light
    /// </summary>
    public float GetRange()
    {
        return strength;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, strength);
    }
}
