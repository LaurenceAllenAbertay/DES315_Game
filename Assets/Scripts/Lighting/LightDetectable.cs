using System;
using UnityEngine;

public class LightDetectable : MonoBehaviour
{
    public Vector3 checkOffset = Vector3.zero;

    public bool IsInLight { get; private set; }
    public float LightLevel { get; private set; }

    public event Action<bool> OnLightStateChanged;

    private void Update()
    {
        if (LightDetectionManager.Instance == null) return;

        Vector3 point = transform.position + checkOffset;
        LightCheckResult result = LightDetectionManager.Instance.CheckLightAtPoint(point);

        bool prev = IsInLight;
        IsInLight = result.isInLight;
        LightLevel = result.totalLightContribution;

        if (prev != IsInLight)
            OnLightStateChanged?.Invoke(IsInLight);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsInLight ? Color.yellow : Color.blue;
        Gizmos.DrawWireSphere(transform.position + checkOffset, 0.1f);
    }
}