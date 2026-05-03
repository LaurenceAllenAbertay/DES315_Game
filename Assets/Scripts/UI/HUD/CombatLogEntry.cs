using System.Collections;
using TMPro;
using UnityEngine;

public class CombatLogEntry : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI label;

    [Header("Timing")]
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float fadeDuration = 1f;
    
    public void Initialize(string message, float overrideDisplayDuration = -1f, float overrideFadeDuration = -1f)
    {
        if (label != null)
            label.text = message;

        if (overrideDisplayDuration >= 0f)
            displayDuration = overrideDisplayDuration;

        if (overrideFadeDuration >= 0f)
            fadeDuration = overrideFadeDuration;

        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, displayDuration));
        
        if (label != null)
        {
            Color startColor = label.color;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, fadeDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                label.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }
        }

        Destroy(gameObject);
    }
}