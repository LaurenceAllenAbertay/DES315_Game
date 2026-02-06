using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

/// <summary>
/// Attach this to any GameObject to make it a light source
/// Handles both gameplay light detection and stylized shadow visuals
/// </summary>
public class LightSource : MonoBehaviour
{
    public enum AdditionalShadowResolutionTier
    {
        Low,
        Medium,
        High
    }

    [Header("Light Properties")]
    [Tooltip("How far this light reaches (used for both gameplay and visuals)")]
    public float strength = 10f;

    [Tooltip("Layer mask for objects that block light")]
    public LayerMask occluderMask = ~0;
    
    [Tooltip("Layer mask for enemies")]
    public LayerMask enemyMask = 0 << 8;

    [Tooltip("Multiplier applied to the Unity Light range for shadow casting only")]
    [Range(1.0f, 3.0f)]
    public float shadowRangeMultiplier = 1.2f;

    [Header("Unity Light Shadow Budget")]
    [Tooltip("Point lights use 6 shadow maps; spot lights use 1.")]
    public LightType shadowLightType = LightType.Point;

    [Tooltip("URP additional light shadow resolution tier (pipeline-level tiers)")]
    public AdditionalShadowResolutionTier shadowResolutionTier = AdditionalShadowResolutionTier.Low;

    [Tooltip("Spot angle used when Shadow Light Type is Spot")]
    [Range(1f, 179f)]
    public float shadowSpotAngle = 170f;

    [Tooltip("Inner spot angle used when Shadow Light Type is Spot")]
    [Range(0f, 179f)]
    public float shadowInnerSpotAngle = 120f;

    [Header("Shadow Properties")]
    [Tooltip("Should this light cast real-time shadows?")]
    public bool castShadows = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRays = true;

    private Light unityLight;
    private static FieldInfo additionalShadowResolutionTierField;

    private void Awake()
    {
        unityLight = GetComponent<Light>();
        if (unityLight == null)
        {
            unityLight = gameObject.AddComponent<Light>();
            unityLight.type = LightType.Point;
            unityLight.intensity = 1.0f;
            unityLight.color = new Color(0.01f, 0.01f, 0.01f); 
            unityLight.shadowStrength = 1.0f;
            unityLight.bounceIntensity = 0.0f;
            unityLight.shadowResolution = LightShadowResolution.Low;
        }

        ApplyShadowResolutionSettings();
    }

    private void Update()
    {
        if (unityLight != null)
        {
            unityLight.range = strength * Mathf.Max(1.0f, shadowRangeMultiplier);
            unityLight.type = shadowLightType;
            ApplyShadowResolutionSettings();
            if (shadowLightType == LightType.Spot)
            {
                unityLight.spotAngle = shadowSpotAngle;
                unityLight.innerSpotAngle = Mathf.Min(shadowInnerSpotAngle, shadowSpotAngle);
            }
            unityLight.shadows = castShadows ? LightShadows.Hard : LightShadows.None;
        }
    }

    public Light GetLight() => unityLight;

    private void ApplyShadowResolutionSettings()
    {
        if (unityLight == null) return;

        unityLight.shadowResolution = shadowResolutionTier switch
        {
            AdditionalShadowResolutionTier.Medium => LightShadowResolution.Medium,
            AdditionalShadowResolutionTier.High => LightShadowResolution.High,
            _ => LightShadowResolution.Low
        };

        UniversalAdditionalLightData additionalData = unityLight.GetUniversalAdditionalLightData();
        if (additionalShadowResolutionTierField == null)
        {
            additionalShadowResolutionTierField = typeof(UniversalAdditionalLightData)
                .GetField("m_AdditionalLightsShadowResolutionTier", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        if (additionalShadowResolutionTierField != null)
        {
            int tierValue = shadowResolutionTier switch
            {
                AdditionalShadowResolutionTier.Medium => UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium,
                AdditionalShadowResolutionTier.High => UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh,
                _ => UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow
            };

            additionalShadowResolutionTierField.SetValue(additionalData, tierValue);
        }
    }

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
        bool isBlocked = Physics.Raycast(ray, distance, occluderMask | enemyMask);

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
