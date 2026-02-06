using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks all lights in the scene and provides their data to the hard shadow renderer.
/// </summary>
public class HardShadowManager : MonoBehaviour
{
    public static HardShadowManager Instance { get; private set; }
    
    [Header("Debug")]
    public bool debugMode = true;

    public struct LightData
    {
        public Vector3 position;
        public float range;
        public Light lightComponent;
    }

    private static HashSet<LightSource> registeredLights = new HashSet<LightSource>();
    private List<LightData> lightDataCache = new List<LightData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (debugMode) Debug.LogWarning("HardShadowManager: Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void RegisterLight(LightSource light)
    {
        if (light == null) return;

        registeredLights.Add(light);
    }

    public static void UnregisterLight(LightSource light)
    {
        if (light == null) return;

        registeredLights.Remove(light);
    }

    public List<LightData> GetTrackedLights()
    {
        lightDataCache.Clear();

        registeredLights.RemoveWhere(l => l == null);

        foreach (var light in registeredLights)
        {
            if (!light.isActiveAndEnabled)
                continue;

            lightDataCache.Add(new LightData
            {
                position = light.transform.position,
                range = light.GetRange(),
                lightComponent = light.GetLight()
            });
        }

        return lightDataCache;
    }

    public int GetLightCount()
    {
        return registeredLights.Count;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);

        foreach (var light in registeredLights)
        {
            if (light == null || !light.isActiveAndEnabled) continue;

            Gizmos.DrawWireSphere(light.transform.position, light.GetRange());
        }
    }
}