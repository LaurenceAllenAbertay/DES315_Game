using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(MeshRenderer))]
public class HideOnCameraEnter : MonoBehaviour
{
    [SerializeField] private float entryMargin = 1.5f;

    private Collider col;
    private MeshRenderer meshRenderer;
    private Material instanceMaterial;
    private Color originalColor;
    private bool isTransparent;

    private void Awake()
    {
        col = GetComponent<Collider>();
        meshRenderer = GetComponent<MeshRenderer>();
        instanceMaterial = meshRenderer.material;
        originalColor = instanceMaterial.GetColor("_BaseColor");
    }

    private void OnDestroy()
    {
        if (instanceMaterial != null)
            Destroy(instanceMaterial);
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float distance = DistanceToSurface(cam.transform.position);

        if (distance >= entryMargin)
        {
            if (isTransparent)
            {
                SetOpaque(instanceMaterial);
                instanceMaterial.SetColor("_BaseColor", originalColor);
                isTransparent = false;
            }
            return;
        }

        if (!isTransparent)
        {
            SetTransparent(instanceMaterial);
            isTransparent = true;
        }

        float alpha = Mathf.Clamp01(distance / entryMargin);
        Color c = originalColor;
        c.a = alpha;
        instanceMaterial.SetColor("_BaseColor", c);
    }

    /// <summary>
    /// ClosestPoint returns the query point unchanged when inside the collider, giving distance 0.
    /// When outside it returns the nearest surface point, giving the true distance to the surface.
    /// Only reliable for convex colliders (Box, Sphere, Capsule, convex MeshCollider).
    /// </summary>
    private float DistanceToSurface(Vector3 point)
    {
        Vector3 closest = col.ClosestPoint(point);
        return (closest - point).magnitude;
    }

    private static void SetTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void SetOpaque(Material mat)
    {
        mat.SetFloat("_Surface", 0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.One);
        mat.SetInt("_DstBlend", (int)BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Geometry;
    }
}