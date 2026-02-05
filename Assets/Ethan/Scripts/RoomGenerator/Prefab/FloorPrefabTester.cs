using UnityEngine;

/// <summary>
/// Quick test script to verify floor prefabs scale correctly
/// Attach to any GameObject and use context menu to test individual prefabs
/// </summary>
public class FloorPrefabTester : MonoBehaviour
{
    [Header("Test Settings")]
    public GameObject floorPrefabToTest;
    public float testCellSize = 5f;

    [Header("Test Sizes")]
    public bool test1x1 = true;
    public bool test2x1 = true;
    public bool test1x2 = true;
    public bool test2x2 = true;

    [ContextMenu("Test Floor Prefab Scaling")]
    private void TestFloorPrefab()
    {
        if (floorPrefabToTest == null)
        {
            Debug.LogError("[FloorPrefabTester] No prefab assigned to test!");
            return;
        }

        ClearTestFloors();

        Vector3 startPos = transform.position;
        float spacing = testCellSize * 3f; // Space between test instances

        int testIndex = 0;

        if (test1x1)
        {
            CreateTestFloor(startPos + new Vector3(spacing * testIndex, 0, 0), 1, 1);
            testIndex++;
        }

        if (test2x1)
        {
            CreateTestFloor(startPos + new Vector3(spacing * testIndex, 0, 0), 2, 1);
            testIndex++;
        }

        if (test1x2)
        {
            CreateTestFloor(startPos + new Vector3(spacing * testIndex, 0, 0), 1, 2);
            testIndex++;
        }

        if (test2x2)
        {
            CreateTestFloor(startPos + new Vector3(spacing * testIndex, 0, 0), 2, 2);
            testIndex++;
        }

        Debug.Log($"[FloorPrefabTester] Created {testIndex} test instances");
    }

    private void CreateTestFloor(Vector3 position, int widthCells, int depthCells)
    {
        GameObject container = new GameObject($"TestFloor_{widthCells}x{depthCells}");
        container.transform.SetParent(transform);
        container.transform.position = position;

        // Calculate size
        float width = widthCells * testCellSize;
        float depth = depthCells * testCellSize;

        // Instantiate prefab
        GameObject floor = Instantiate(floorPrefabToTest, container.transform);
        floor.name = $"Floor_{widthCells}x{depthCells}";
        floor.transform.localPosition = new Vector3(width * 0.5f, 0f, depth * 0.5f);
        floor.transform.localScale = new Vector3(width, 1f, depth);

        // Add a wireframe cube to show the bounds
        CreateBoundsVisualizer(container, new Vector3(width * 0.5f, 0f, depth * 0.5f), width, depth);

        Debug.Log($"[FloorPrefabTester] Created {widthCells}x{depthCells} floor: {width}x{depth} units");
    }

    private void CreateBoundsVisualizer(GameObject parent, Vector3 center, float width, float depth)
    {
        GameObject bounds = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bounds.name = "Bounds_Visualizer";
        bounds.transform.SetParent(parent.transform);
        bounds.transform.localPosition = center;
        bounds.transform.localScale = new Vector3(width, 0.1f, depth);

        // Make it wireframe-like
        Renderer renderer = bounds.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0f, 1f, 0f, 0.3f);

        // Set up transparency
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0); // Alpha blend
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        renderer.material = mat;

        // Remove collider so it doesn't interfere
        DestroyImmediate(bounds.GetComponent<Collider>());
    }

    [ContextMenu("Clear Test Floors")]
    private void ClearTestFloors()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        Debug.Log("[FloorPrefabTester] Cleared all test floors");
    }

    private void OnDrawGizmos()
    {
        // Draw test area
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position + new Vector3(testCellSize * 6f, 0f, 0f);
        Gizmos.DrawWireCube(center, new Vector3(testCellSize * 12f, 0.1f, testCellSize * 3f));
    }
}