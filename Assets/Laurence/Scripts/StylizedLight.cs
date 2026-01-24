using UnityEngine;

/// <summary>
/// Attach this to any Light Source to make it part of the stylized shadow system.
/// </summary>
[RequireComponent(typeof(LightSource))]
public class StylizedLight : MonoBehaviour
{
    private LightSource lightSource;
    private bool isRegistered = false;

    private void Awake()
    {
        lightSource = GetComponent<LightSource>();
    }

    private void OnEnable()
    {
        EnsureControllerExists();

        StylizedShadowController.RegisterLight(this);
        isRegistered = true;
    }

    private void OnDisable()
    {
        if (isRegistered)
        {
            StylizedShadowController.UnregisterLight(this);
            isRegistered = false;
        }
    }

    private void EnsureControllerExists()
    {
        if (StylizedShadowController.Instance == null)
        {
            var existing = FindFirstObjectByType<StylizedShadowController>();

            if (existing == null)
            {
                Debug.Log("StylizedLight: Creating StylizedShadowController");
                var controllerGO = new GameObject("StylizedShadowController");
                controllerGO.AddComponent<StylizedShadowController>();
            }
        }
    }

    /// <summary>
    /// Get the range for this light's shadow circle
    /// </summary>
    public float GetRange()
    {
        if (lightSource != null)
        {
            return lightSource.strength;
        }

        return 0f;
    }
}