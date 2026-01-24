using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EnemyVisionCone : MonoBehaviour
{
    [Header("Vision Settings")]
    public float visionRange = 10f;
    public float visionAngle = 45f;
    public int visionResolution = 20;
    public float groundOffset = 0.1f;
    public LayerMask playerLayer;
    public LayerMask obstacleMask;

    [Header("Visual Settings")]
    public Color visionColor = new Color(1f, 0f, 0f, 0.3f);

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh visionMesh;
    private bool playerDetected = false;
    private Material instancedMaterial;

    public delegate void PlayerDetected();
    public event PlayerDetected OnPlayerDetected;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        visionMesh = new Mesh();
        visionMesh.name = "Vision Cone";
        meshFilter.mesh = visionMesh;

        SetupMaterial();
        meshRenderer.enabled = false;
    }

    private void SetupMaterial()
    {
        instancedMaterial = CreateTransparentMaterial();
        meshRenderer.material = instancedMaterial;
        SetColor(visionColor);
    }

    private Material CreateTransparentMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(shader);

        // URP Unlit transparency settings
        mat.SetFloat("_Surface", 1); // 1 = Transparent
        mat.SetFloat("_Blend", 0);   // 0 = Alpha blend
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return mat;
    }

    public void SetColor(Color color)
    {
        visionColor = color;

        if (instancedMaterial != null)
        {
            instancedMaterial.SetColor("_BaseColor", color);
        }
    }

    public void SetAlpha(float alpha)
    {
        visionColor.a = Mathf.Clamp01(alpha);
        SetColor(visionColor);
    }

    private void Update()
    {
        UpdateVisionMesh();
        CheckForPlayer();
    }

    private void UpdateVisionMesh()
    {
        Vector3[] vertices = new Vector3[visionResolution + 2];
        int[] triangles = new int[visionResolution * 3];

        vertices[0] = new Vector3(0, groundOffset, 0);

        float angleStep = visionAngle / visionResolution;
        float currentAngle = -visionAngle / 2f;

        for (int i = 0; i <= visionResolution; i++)
        {
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));

            float distance = visionRange;
            Vector3 worldDirection = transform.TransformDirection(direction);

            if (Physics.Raycast(transform.position, worldDirection, out RaycastHit hit, visionRange, obstacleMask))
            {
                distance = hit.distance;
            }

            vertices[i + 1] = direction * distance + new Vector3(0, groundOffset, 0);
            currentAngle += angleStep;
        }

        for (int i = 0; i < visionResolution; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        visionMesh.Clear();
        visionMesh.vertices = vertices;
        visionMesh.triangles = triangles;
        visionMesh.RecalculateNormals();
    }

    private void CheckForPlayer()
    {
        float angleStep = visionAngle / visionResolution;
        float currentAngle = -visionAngle / 2f;

        for (int i = 0; i <= visionResolution; i++)
        {
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 direction = transform.TransformDirection(new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)));

            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, visionRange))
            {
                if (((1 << hit.collider.gameObject.layer) & playerLayer) != 0)
                {
                    if (!playerDetected)
                    {
                        playerDetected = true;
                        Debug.Log("Combat starts!");
                        OnPlayerDetected?.Invoke();
                    }
                    return;
                }
            }

            currentAngle += angleStep;
        }

        playerDetected = false;
    }

    public void SetVisible(bool visible)
    {
        meshRenderer.enabled = visible;
    }

    public bool IsVisible()
    {
        return meshRenderer.enabled;
    }

    private void OnDestroy()
    {
        if (instancedMaterial != null)
        {
            Destroy(instancedMaterial);
        }

        if (visionMesh != null)
        {
            Destroy(visionMesh);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        float angleStep = visionAngle / 10;
        float currentAngle = -visionAngle / 2f;

        for (int i = 0; i <= 10; i++)
        {
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 direction = transform.TransformDirection(new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)));
            Gizmos.DrawLine(transform.position, transform.position + direction * visionRange);
            currentAngle += angleStep;
        }
    }
}