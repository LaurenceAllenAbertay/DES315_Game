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

    private HashSet<GameObject> currentTargets = new HashSet<GameObject>();
    private Dictionary<GameObject, Renderer[]> targetRenderers = new Dictionary<GameObject, Renderer[]>();
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material> outlineMaterials = new Dictionary<Renderer, Material>();
    private Dictionary<Renderer, int> rendererRefCounts = new Dictionary<Renderer, int>();

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
        if (target == null)
        {
            ClearTargets();
            return;
        }

        SetTargets(new List<GameObject> { target });
    }

    /// <summary>
    /// Clear any current highlight
    /// </summary>
    public void ClearTargets()
    {
        if (currentTargets.Count == 0)
        {
            return;
        }

        var targetsToRemove = new List<GameObject>(currentTargets);
        for (int i = 0; i < targetsToRemove.Count; i++)
        {
            RemoveHighlight(targetsToRemove[i]);
        }
    }

    public void SetTargets(List<GameObject> targets)
    {
        if (targets == null || targets.Count == 0)
        {
            ClearTargets();
            return;
        }

        HashSet<GameObject> newTargets = new HashSet<GameObject>();
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
            {
                newTargets.Add(targets[i]);
            }
        }

        if (newTargets.Count == 0)
        {
            ClearTargets();
            return;
        }

        var toRemove = new List<GameObject>();
        foreach (GameObject existing in currentTargets)
        {
            if (!newTargets.Contains(existing))
            {
                toRemove.Add(existing);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            RemoveHighlight(toRemove[i]);
        }

        foreach (GameObject target in newTargets)
        {
            if (!currentTargets.Contains(target))
            {
                ApplyHighlight(target);
            }
        }
    }

    private void ApplyHighlight(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        List<Renderer> appliedRenderers = new List<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            // Skip non-mesh renderers
            if (renderer is ParticleSystemRenderer) continue;
            if (renderer.GetComponentInParent<EnemyVisionCone>() != null) continue;

            appliedRenderers.Add(renderer);

            if (!rendererRefCounts.TryGetValue(renderer, out int refCount))
            {
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
                outlineMaterials[renderer] = outlineMat;
                newMaterials[newMaterials.Length - 1] = outlineMat;

                renderer.materials = newMaterials;
                rendererRefCounts[renderer] = 1;
            }
            else
            {
                rendererRefCounts[renderer] = refCount + 1;
            }
        }

        targetRenderers[target] = appliedRenderers.ToArray();
        currentTargets.Add(target);
    }

    private void RemoveHighlight(GameObject target)
    {
        if (!targetRenderers.TryGetValue(target, out Renderer[] renderers))
        {
            currentTargets.Remove(target);
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            if (rendererRefCounts.TryGetValue(renderer, out int refCount))
            {
                refCount--;
                if (refCount <= 0)
                {
                    if (originalMaterials.TryGetValue(renderer, out Material[] original))
                    {
                        renderer.materials = original;
                    }

                    if (outlineMaterials.TryGetValue(renderer, out Material outline))
                    {
                        if (outline != null)
                        {
                            Destroy(outline);
                        }
                    }

                    originalMaterials.Remove(renderer);
                    outlineMaterials.Remove(renderer);
                    rendererRefCounts.Remove(renderer);
                }
                else
                {
                    rendererRefCounts[renderer] = refCount;
                }
            }
        }

        targetRenderers.Remove(target);
        currentTargets.Remove(target);
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

        foreach (Material mat in outlineMaterials.Values)
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
        ClearTargets();
    }
}
