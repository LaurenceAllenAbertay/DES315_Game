using UnityEngine;

/// <summary>
/// Visualizes a circular AOE targeting area on the ground
/// Follows the mouse position, clamped to ability range
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AOECircleVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color circleColor = new Color(0.2f, 0.6f, 1f, 0.3f);
    public int resolution = 32;
    public float groundOffset = 0.1f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh circleMesh;
    private Material instancedMaterial;

    private float currentRadius;
    private float maxRange;
    private Vector3 currentPosition;

    public Vector3 CurrentPosition => currentPosition;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        circleMesh = new Mesh();
        circleMesh.name = "AOE Circle";
        meshFilter.mesh = circleMesh;

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

        instancedMaterial.SetColor("_BaseColor", circleColor);
        meshRenderer.material = instancedMaterial;
    }

    /// <summary>
    /// Show the AOE circle visualizer
    /// </summary>
    public void Show(float radius, float range)
    {
        currentRadius = radius;
        maxRange = range;
        meshRenderer.enabled = true;
        UpdateMesh();
    }

    /// <summary>
    /// Hide the AOE circle visualizer
    /// </summary>
    public void Hide()
    {
        meshRenderer.enabled = false;
    }

    /// <summary>
    /// Update the circle position 
    /// </summary>
    public void UpdatePosition(Vector3 position)
    {
        currentPosition = position;
        transform.position = position;
    }

    private void UpdateMesh()
    {
        Vector3[] vertices = new Vector3[resolution + 1];
        int[] triangles = new int[resolution * 3];

        vertices[0] = new Vector3(0f, groundOffset, 0f);

        for (int i = 0; i < resolution; i++)
        {
            float angle = (i / (float)resolution) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * currentRadius;
            float z = Mathf.Sin(angle) * currentRadius;
            vertices[i + 1] = new Vector3(x, groundOffset, z);
        }

        for (int i = 0; i < resolution; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = ((i + 1) % resolution) + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        circleMesh.Clear();
        circleMesh.vertices = vertices;
        circleMesh.triangles = triangles;
        circleMesh.RecalculateNormals();
    }

    public void SetColor(Color color)
    {
        circleColor = color;

        if (instancedMaterial != null)
        {
            instancedMaterial.SetColor("_BaseColor", color);
        }

        UpdateMesh();
    }

    private void OnDestroy()
    {
        if (instancedMaterial != null)
        {
            Destroy(instancedMaterial);
        }

        if (circleMesh != null)
        {
            Destroy(circleMesh);
        }
    }
}
