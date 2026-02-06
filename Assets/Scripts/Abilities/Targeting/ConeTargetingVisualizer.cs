using UnityEngine;

/// <summary>
/// Visualizes a cone-shaped targeting area emanating from the player
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ConeTargetingVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color coneColor = new Color(0.2f, 0.6f, 1f, 0.3f);
    public int resolution = 20;
    public float groundOffset = 0.1f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh coneMesh;
    private Material instancedMaterial;

    private float currentRange;
    private float currentAngle;
    private Vector3 currentDirection = Vector3.forward;

    public Vector3 CurrentDirection => currentDirection;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        coneMesh = new Mesh();
        coneMesh.name = "Targeting Cone";
        meshFilter.mesh = coneMesh;

        SetupMaterial();
        meshRenderer.enabled = false;
    }

    private void SetupMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        instancedMaterial = new Material(shader);
        
        instancedMaterial.SetFloat("_Surface", 1);
        instancedMaterial.SetFloat("_Blend", 0);
        instancedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        instancedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        instancedMaterial.SetInt("_ZWrite", 0);
        instancedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        instancedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        instancedMaterial.SetColor("_BaseColor", coneColor);
        meshRenderer.material = instancedMaterial;
    }

    /// <summary>
    /// Show the cone visualizer with given parameters
    /// </summary>
    public void Show(float range, float angle)
    {
        currentRange = range;
        currentAngle = angle;
        meshRenderer.enabled = true;
        UpdateMesh();
    }

    /// <summary>
    /// Hide the cone visualizer
    /// </summary>
    public void Hide()
    {
        meshRenderer.enabled = false;
    }

    /// <summary>
    /// Update the cone direction
    /// </summary>
    public void UpdateDirection(Vector3 origin, Vector3 direction)
    {
        transform.position = origin;
        currentDirection = direction.normalized;
        
        if (currentDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(currentDirection, Vector3.up);
        }

        UpdateMesh();
    }

    private void UpdateMesh()
    {
        // Make cone...
        Vector3[] vertices = new Vector3[resolution + 2];
        int[] triangles = new int[resolution * 3];
        
        vertices[0] = new Vector3(0, groundOffset, 0);

        float angleStep = currentAngle / resolution;
        float startAngle = -currentAngle * 0.5f;

        for (int i = 0; i <= resolution; i++)
        {
            float angle = startAngle + (angleStep * i);
            float rad = angle * Mathf.Deg2Rad;
            
            Vector3 localDir = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
            vertices[i + 1] = localDir * currentRange + new Vector3(0, groundOffset, 0);
        }
        
        for (int i = 0; i < resolution; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        coneMesh.Clear();
        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateNormals();
    }

    public void SetColor(Color color)
    {
        coneColor = color;
        if (instancedMaterial != null)
        {
            instancedMaterial.SetColor("_BaseColor", color);
        }
    }

    private void OnDestroy()
    {
        if (instancedMaterial != null)
        {
            Destroy(instancedMaterial);
        }

        if (coneMesh != null)
        {
            Destroy(coneMesh);
        }
    }
}