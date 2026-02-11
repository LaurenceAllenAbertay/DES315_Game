using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for light detection, tracks all active light sources and provides methods to check if points are in light or shadow
/// </summary>
public class LightDetectionManager : MonoBehaviour
{
    public static LightDetectionManager Instance { get; private set; }

    [Header("Detection Settings")]
    [Tooltip("Minimum total light contribution to be considered 'in light'")]
    [Range(0f, 1f)]
    public float lightThreshold = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool logLightChecks = false;

    private static HashSet<LightSource> activeLights = new HashSet<LightSource>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public static void RegisterLight(LightSource light)
    {
        activeLights.Add(light);
    }

    public static void UnregisterLight(LightSource light)
    {
        activeLights.Remove(light);
    }

    /// <summary>
    /// Check if a world point is in light or shadow
    /// </summary>
    public LightCheckResult CheckLightAtPoint(Vector3 point)
    {
        LightCheckResult result = LightCheckResult.Create();

        foreach (LightSource light in activeLights)
        {
            if (light == null) continue;

            if (light.CanReachPoint(point, out float contribution))
            {
                result.totalLightContribution += contribution;
                result.contributingLights.Add(light);
            }
        }

        result.isInLight = result.totalLightContribution >= lightThreshold;

        if (logLightChecks)
        {
            Debug.Log($"Light check at {point}: {(result.isInLight ? "IN LIGHT" : "IN SHADOW")} " +
                      $"(contribution: {result.totalLightContribution:F2}, lights: {result.contributingLights.Count})");
        }

        return result;
    }

    /// <summary>
    /// Check if a world point is in light, sampling around a radius to avoid tiny false negatives.
    /// </summary>
    public LightCheckResult CheckLightAtPoint(Vector3 point, float radius)
    {
        if (radius <= 0f)
        {
            return CheckLightAtPoint(point);
        }

        LightCheckResult bestResult = CheckLightAtPoint(point);
        if (bestResult.isInLight)
        {
            return bestResult;
        }

        Vector3[] offsets =
        {
            Vector3.right,
            Vector3.left,
            Vector3.forward,
            Vector3.back,
            Vector3.up,
            Vector3.down
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            LightCheckResult sampleResult = CheckLightAtPoint(point + offsets[i] * radius);
            if (sampleResult.totalLightContribution > bestResult.totalLightContribution)
            {
                bestResult = sampleResult;
            }
            if (sampleResult.isInLight)
            {
                return sampleResult;
            }
        }

        return bestResult;
    }

    /// <summary>
    /// A simple check, is this point in light?
    /// </summary>
    public bool IsPointInLight(Vector3 point)
    {
        return CheckLightAtPoint(point).isInLight;
    }

    /// <summary>
    /// Get the number of active lights being tracked
    /// </summary>
    public int GetActiveLightCount()
    {
        return activeLights.Count;
    }
}

/// <summary>
/// Result of a light detection check
/// </summary>
public struct LightCheckResult
{
    public bool isInLight;
    public float totalLightContribution;
    public List<LightSource> contributingLights;

    public static LightCheckResult Create()
    {
        return new LightCheckResult
        {
            isInLight = false,
            totalLightContribution = 0f,
            contributingLights = new List<LightSource>()
        };
    }
}
