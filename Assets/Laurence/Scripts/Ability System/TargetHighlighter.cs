using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the outline highlight effect on targetable enemies
/// </summary>
public class TargetHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);
    public float outlineThickness = 2.0f;
    public float outlineIntensity = 3.0f;

    [Header("Shader")]
    [Tooltip("Leave null to auto-find 'Custom/OccludedOutline'")]
    public Shader outlineShader;

    private GameObject currentTarget;
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private List<Material> outlineMaterials = new List<Material>();

    private void Awake()
    {
        if (outlineShader == null)
        {
            outlineShader = Shader.Find("Custom/OccludedOutline");
        }

        if (outlineShader == null)
        {
            Debug.LogWarning("[TargetHighlighter] Could not find outline shader. Using fallback.");
        }
    }

    /// <summary>
    /// Set the target to highlight
    /// </summary>
    public void SetTarget(GameObject target)
    {
        if (target == currentTarget) return;
        
        ClearTarget();

        if (target == null) return;

        currentTarget = target;
        ApplyHighlight(target);
    }

    /// <summary>
    /// Clear any current highlight
    /// </summary>
    public void ClearTarget()
    {
        if (currentTarget != null)
        {
            RemoveHighlight();
        }

        currentTarget = null;
    }

    private void ApplyHighlight(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            // Skip non-mesh renderers
            if (renderer is ParticleSystemRenderer) continue;

            // Store original materials
            originalMaterials[renderer] = renderer.materials;

            // Create a new material array with outline materials added
            Material[] newMaterials = new Material[renderer.materials.Length + 1];

            // Copy original materials
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                newMaterials[i] = renderer.materials[i];
            }

            // Add outline material
            Material outlineMat = CreateOutlineMaterial();
            outlineMaterials.Add(outlineMat);
            newMaterials[newMaterials.Length - 1] = outlineMat;

            renderer.materials = newMaterials;
        }
    }

    private void RemoveHighlight()
    {
        // Restore original materials
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }

        originalMaterials.Clear();

        // Destroy outline materials
        foreach (Material mat in outlineMaterials)
        {
            if (mat != null)
            {
                Destroy(mat);
            }
        }

        outlineMaterials.Clear();
    }

    private Material CreateOutlineMaterial()
    {
        Material mat;

        if (outlineShader != null)
        {
            mat = new Material(outlineShader);
            mat.SetColor("_OutlineColor", highlightColor);
            mat.SetFloat("_OutlinePower", outlineThickness);
            mat.SetFloat("_OutlineIntensity", outlineIntensity);
        }
        else
        {
            // Use a simple unlit transparent material
            Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(fallbackShader);

            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            mat.SetColor("_BaseColor", new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.3f));
        }

        return mat;
    }

    /// <summary>
    /// Update highlight color
    /// </summary>
    public void SetHighlightColor(Color color)
    {
        highlightColor = color;

        foreach (Material mat in outlineMaterials)
        {
            if (mat != null)
            {
                if (outlineShader != null)
                {
                    mat.SetColor("_OutlineColor", color);
                }
                else
                {
                    mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.3f));
                }
            }
        }
    }

    private void OnDestroy()
    {
        ClearTarget();
    }
}