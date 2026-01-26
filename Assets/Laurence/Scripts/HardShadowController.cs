using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks all lights in the scene and provides their data to the shadow renderer.
/// This is automatically created when the first LightSource registers.
/// </summary>
public class HardShadowManager : MonoBehaviour
{
    public static HardShadowManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private int trackedLightCount = 0;

    public struct LightData
    {
        public Vector3 position;
        public float range;
    }

    private static HashSet<LightSource> registeredLights = new HashSet<LightSource>();
    private List<LightData> lightDataCache = new List<LightData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("HardShadowManager: Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (debugMode)
            Debug.Log("HardShadowManager: Initialized");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        trackedLightCount = registeredLights.Count;
    }

    public static void RegisterLight(LightSource light)
    {
        if (light == null) return;

        registeredLights.Add(light);

        if (Instance != null && Instance.debugMode)
            Debug.Log($"HardShadowManager: Registered light '{light.name}' (total: {registeredLights.Count})");
    }

    public static void UnregisterLight(LightSource light)
    {
        if (light == null) return;

        registeredLights.Remove(light);

        if (Instance != null && Instance.debugMode)
            Debug.Log($"HardShadowManager: Unregistered light '{light.name}' (total: {registeredLights.Count})");
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
                range = light.GetRange()
            });
        }

        return lightDataCache;
    }

    public int GetLightCount()
    {
        return registeredLights.Count;
    }

    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);

        foreach (var light in registeredLights)
        {
            if (light == null || !light.isActiveAndEnabled) continue;

            Gizmos.DrawWireSphere(light.transform.position, light.GetRange());
        }
    }
}