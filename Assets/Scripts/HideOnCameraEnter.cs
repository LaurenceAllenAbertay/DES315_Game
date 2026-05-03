using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshRenderer))]
public class HideOnCameraEnter : MonoBehaviour
{
    [SerializeField] private float entryMargin = 2f;

    private Collider[] colliders;
    private MeshRenderer meshRenderer;
    private Material instanceMaterial;
    private Color originalColor;
    private bool isTransparent;
    private Transform playerTransform;

    public bool IsTransparent => isTransparent;

    private void Awake()
    {
        colliders = GetComponents<Collider>();
        meshRenderer = GetComponent<MeshRenderer>();
        instanceMaterial = meshRenderer.material;
        originalColor = instanceMaterial.GetColor("_BaseColor");

        Player player = FindFirstObjectByType<Player>();
        if (player != null) playerTransform = player.transform;
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

        float activeMargin = entryMargin;
        if (playerTransform != null && IsBlockingPlayer(cam.transform.position, playerTransform.position))
            activeMargin = entryMargin * 2f;

        float distance = DistanceToSurface(cam.transform.position);

        if (distance >= activeMargin)
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

        float alpha = Mathf.Clamp01(distance / activeMargin);
        Color c = originalColor;
        c.a = alpha;
        instanceMaterial.SetColor("_BaseColor", c);
    }
    
    private bool IsBlockingPlayer(Vector3 cameraPos, Vector3 playerPos)
    {
        Vector3 direction = playerPos - cameraPos;
        float distance = direction.magnitude;
        Ray ray = new Ray(cameraPos, direction / distance);

        RaycastHit[] hits = Physics.RaycastAll(ray, distance);
        foreach (RaycastHit hit in hits)
        {
            foreach (Collider col in colliders)
            {
                if (hit.collider == col)
                    return true;
            }
        }
        return false;
    }
    
    private float DistanceToSurface(Vector3 point)
    {
        float minDistance = float.MaxValue;
        foreach (Collider col in colliders)
        {
            Vector3 closest = col.ClosestPoint(point);
            float dist = (closest - point).magnitude;
            if (dist < minDistance)
                minDistance = dist;
        }
        return minDistance;
    }
    
    public static bool RaycastIgnoreTransparent(Ray ray, out RaycastHit hit, float maxDistance, LayerMask mask)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, mask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            HideOnCameraEnter hider = h.collider.GetComponentInParent<HideOnCameraEnter>();
            if (hider != null && hider.isTransparent)
                continue;
            hit = h;
            return true;
        }

        hit = default;
        return false;
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