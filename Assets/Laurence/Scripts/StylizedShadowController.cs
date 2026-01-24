using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks all lights in the scene and provides their data to the shadow renderer.
/// This is automatically created when the first StylizedLight registers.
/// </summary>
public class StylizedShadowController : MonoBehaviour
{
    public static StylizedShadowController Instance { get; private set; }
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private int trackedLightCount = 0; 
    
    public struct LightData
    {
        public Vector3 position;
        public float range;
    }
    
    private static HashSet<StylizedLight> registeredLights = new HashSet<StylizedLight>();
    private List<LightData> lightDataCache = new List<LightData>();
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("StylizedShadowController: Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        if (debugMode)
            Debug.Log("StylizedShadowController: Initialized");
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
    
    public static void RegisterLight(StylizedLight light)
    {
        if (light == null) return;
        
        registeredLights.Add(light);
        
        if (Instance != null && Instance.debugMode)
            Debug.Log($"StylizedShadowController: Registered light '{light.name}' (total: {registeredLights.Count})");
    }
    
    public static void UnregisterLight(StylizedLight light)
    {
        if (light == null) return;
        
        registeredLights.Remove(light);
        
        if (Instance != null && Instance.debugMode)
            Debug.Log($"StylizedShadowController: Unregistered light '{light.name}' (total: {registeredLights.Count})");
    }
    
    public List<LightData> GetTrackedLights()
    {
        lightDataCache.Clear();
        
        registeredLights.RemoveWhere(l => l == null);
        
        foreach (var light in registeredLights)
        {
            if (!light.isActiveAndEnabled)
                continue;
            
            float range = light.GetRange();
            
            lightDataCache.Add(new LightData
            {
                position = light.transform.position,
                range = range
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
