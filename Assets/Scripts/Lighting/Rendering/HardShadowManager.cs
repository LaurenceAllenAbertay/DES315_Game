using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton that tracks all active LightSources for the HardShadowFeature.
/// </summary>
public class HardShadowManager : MonoBehaviour
{
    public static HardShadowManager Instance { get; private set; }

    private static readonly HashSet<LightSource> registeredLights = new HashSet<LightSource>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void RegisterLight(LightSource light)
    {
        if (light != null)
            registeredLights.Add(light);
    }

    public static void UnregisterLight(LightSource light)
    {
        if (light != null)
            registeredLights.Remove(light);
    }

    public static IReadOnlyCollection<LightSource> GetLights()
    {
        registeredLights.RemoveWhere(l => l == null);
        return registeredLights;
    }
}