using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

[RequireComponent(typeof(RoomLA))]
public class RoomShadowRenderer : MonoBehaviour
{
    [Header("Shadow Settings")]
    [SerializeField] private Material shadowMaterial;
    [Tooltip("Size of each shadow grid cell. Smaller = smoother edges but more triangles. 0.25 is a good starting point.")]
    [SerializeField] [Range(0.05f, 1f)] private float gridResolution = 0.25f;
    [Tooltip("Y height above the room floor. Must sit above the floor and below vision cone Y offsets.")]
    [SerializeField] private float yOffset = 0.02f;

    [Header("Light Detection")]
    [Tooltip("Number of rays cast per light to build its visibility polygon.")]
    [SerializeField] [Range(32, 360)] private int rayCount = 128;
    [SerializeField] private LayerMask occluderMask = ~0;

    private RoomLA roomLA;
    private LightSource[] roomLights;
    private Mesh shadowMesh;
    private GameObject meshObject;

    private struct LightVisibility
    {
        public Vector3 position;
        public float range;
        public float[] rayDistances;
    }

    private void Awake()
    {
        roomLA = GetComponent<RoomLA>();
        roomLights = GetComponentsInChildren<LightSource>(true);

        meshObject = new GameObject("RoomShadow");
        meshObject.transform.SetParent(transform);
        meshObject.transform.localPosition = Vector3.zero;
        meshObject.transform.localRotation = Quaternion.identity;
        meshObject.transform.localScale = Vector3.one;

        NavMeshModifier navModifier = meshObject.AddComponent<NavMeshModifier>();
        navModifier.ignoreFromBuild = true;

        MeshFilter mf = meshObject.AddComponent<MeshFilter>();
        MeshRenderer mr = meshObject.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (shadowMaterial != null)
            mr.material = shadowMaterial;

        shadowMesh = new Mesh();
        shadowMesh.name = "RoomShadowMesh";
        mf.mesh = shadowMesh;
    }

    private void Start()
    {
        BuildShadowMesh();
    }

    /// <summary>
    /// Rebuilds the shadow mesh. Call this if lights are toggled or their range changes.
    /// </summary>
    public void RebuildShadowMesh()
    {
        BuildShadowMesh();
    }

    private void BuildShadowMesh()
    {
        if (shadowMaterial == null)
        {
            Debug.LogWarning("[RoomShadowRenderer] No shadow material assigned.", this);
            return;
        }

        LightVisibility[] visibilities = ComputeLightVisibilities();
        Bounds bounds = GetRoomBounds();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float step = gridResolution;
        float halfStep = step * 0.5f;

        for (float x = bounds.min.x; x < bounds.max.x; x += step)
        {
            for (float z = bounds.min.z; z < bounds.max.z; z += step)
            {
                Vector3 cellCenter = new Vector3(x + halfStep, bounds.min.y + yOffset, z + halfStep);

                if (IsPointLit(cellCenter, visibilities))
                    continue;

                int baseIndex = vertices.Count;

                vertices.Add(new Vector3(x,        bounds.min.y + yOffset, z));
                vertices.Add(new Vector3(x + step, bounds.min.y + yOffset, z));
                vertices.Add(new Vector3(x + step, bounds.min.y + yOffset, z + step));
                vertices.Add(new Vector3(x,        bounds.min.y + yOffset, z + step));

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }
        }

        shadowMesh.Clear();
        shadowMesh.vertices = vertices.ToArray();
        shadowMesh.triangles = triangles.ToArray();
        shadowMesh.RecalculateNormals();
    }

    private LightVisibility[] ComputeLightVisibilities()
    {
        roomLights = GetComponentsInChildren<LightSource>(true);
        LightVisibility[] result = new LightVisibility[roomLights.Length];

        for (int i = 0; i < roomLights.Length; i++)
        {
            LightSource light = roomLights[i];
            float range = light.GetRange();
            float[] distances = new float[rayCount];
            float angleStep = 360f / rayCount;
            Vector3 origin = light.transform.position;

            for (int r = 0; r < rayCount; r++)
            {
                float angle = r * angleStep * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                if (Physics.Raycast(origin, dir, out RaycastHit hit, range, occluderMask))
                    distances[r] = hit.distance;
                else
                    distances[r] = range;
            }

            result[i] = new LightVisibility
            {
                position = origin,
                range = range,
                rayDistances = distances
            };
        }

        return result;
    }

    private bool IsPointLit(Vector3 point, LightVisibility[] visibilities)
    {
        foreach (LightVisibility vis in visibilities)
        {
            if (IsPointInVisibility(point, vis))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Tests whether a world point falls inside a light's precomputed visibility polygon
    /// by finding the ray sector the point falls into and comparing its distance.
    /// </summary>
    private bool IsPointInVisibility(Vector3 point, LightVisibility vis)
    {
        Vector3 toPoint = point - vis.position;
        toPoint.y = 0f;
        float distance = toPoint.magnitude;

        if (distance > vis.range)
            return false;

        float angle = Mathf.Atan2(toPoint.x, toPoint.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        float angleStep = 360f / rayCount;
        int sector = Mathf.Clamp((int)(angle / angleStep), 0, rayCount - 1);
        float t = (angle - sector * angleStep) / angleStep;

        float d1 = vis.rayDistances[sector];
        float d2 = vis.rayDistances[(sector + 1) % rayCount];
        float visibleDistance = Mathf.Lerp(d1, d2, t);

        return distance <= visibleDistance;
    }

    private Bounds GetRoomBounds()
    {
        Collider[] cols = roomLA.BoundaryColliders;

        if (cols == null || cols.Length == 0)
            return new Bounds(transform.position, new Vector3(10f, 1f, 10f));

        Bounds b = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++)
            if (cols[i] != null) b.Encapsulate(cols[i].bounds);
        return b;
    }
}