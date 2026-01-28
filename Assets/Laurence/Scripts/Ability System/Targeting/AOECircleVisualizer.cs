using UnityEngine;

/// <summary>
/// Visualizes a circular AOE targeting area on the ground
/// Follows the mouse position, clamped to ability range
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AOECircleVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color circleColor = new Color(1f, 0.4f, 0.2f, 0.3f);
    public Color edgeColor = new Color(1f, 0.6f, 0.3f, 0.8f);
    public int resolution = 32;
    public float groundOffset = 0.1f;
    public float edgeWidth = 0.15f;

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
        transform.position = new Vector3(position.x, position.y + groundOffset, position.z);
    }

    private void UpdateMesh()
    {
        // Make this like ring thing I guess
        int totalVerts = 1 + resolution + resolution; 
        Vector3[] vertices = new Vector3[totalVerts];
        Color[] colors = new Color[totalVerts];
        
        vertices[0] = Vector3.zero;
        colors[0] = circleColor;

        float innerRadius = currentRadius - edgeWidth;
        if (innerRadius < 0) innerRadius = currentRadius * 0.8f;
        
        for (int i = 0; i < resolution; i++)
        {
            float angle = (i / (float)resolution) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * innerRadius;
            float z = Mathf.Sin(angle) * innerRadius;

            vertices[1 + i] = new Vector3(x, 0, z);
            colors[1 + i] = circleColor;
        }
        
        for (int i = 0; i < resolution; i++)
        {
            float angle = (i / (float)resolution) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * currentRadius;
            float z = Mathf.Sin(angle) * currentRadius;

            vertices[1 + resolution + i] = new Vector3(x, 0, z);
            colors[1 + resolution + i] = edgeColor;
        }
        
        int[] triangles = new int[resolution * 3 + resolution * 6];
        int triIndex = 0;
        
        for (int i = 0; i < resolution; i++)
        {
            triangles[triIndex++] = 0;
            triangles[triIndex++] = 1 + i;
            triangles[triIndex++] = 1 + ((i + 1) % resolution);
        }
        
        for (int i = 0; i < resolution; i++)
        {
            int innerCurrent = 1 + i;
            int innerNext = 1 + ((i + 1) % resolution);
            int outerCurrent = 1 + resolution + i;
            int outerNext = 1 + resolution + ((i + 1) % resolution);
            
            triangles[triIndex++] = innerCurrent;
            triangles[triIndex++] = outerCurrent;
            triangles[triIndex++] = outerNext;

            triangles[triIndex++] = innerCurrent;
            triangles[triIndex++] = outerNext;
            triangles[triIndex++] = innerNext;
        }

        circleMesh.Clear();
        circleMesh.vertices = vertices;
        circleMesh.triangles = triangles;
        circleMesh.colors = colors;
        circleMesh.RecalculateNormals();
    }

    public void SetColor(Color fill, Color edge)
    {
        circleColor = fill;
        edgeColor = edge;

        if (instancedMaterial != null)
        {
            instancedMaterial.SetColor("_BaseColor", fill);
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