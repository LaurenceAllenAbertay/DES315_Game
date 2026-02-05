using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
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

    [Header("Debug")]
    public bool debugMode = true;

    [Header("Detection Settings")]
    [Tooltip("How often to update the collider mesh (seconds). Lower = more accurate but more expensive")]
    public float colliderUpdateInterval = 0.2f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh visionMesh;
    private Mesh colliderMesh;
    private bool playerDetected = false;
    private Material instancedMaterial;
    private float lastColliderUpdateTime;

    // Cache for player detection
    private Transform detectedPlayerTransform;

    public delegate void PlayerDetected();
    public event PlayerDetected OnPlayerDetected;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        // Visual mesh (updated every frame for obstacle occlusion visuals)
        visionMesh = new Mesh();
        visionMesh.name = "Vision Cone Visual";
        meshFilter.mesh = visionMesh;

        // Collider mesh (updated less frequently, uses full range without obstacles)
        colliderMesh = new Mesh();
        colliderMesh.name = "Vision Cone Collider";

        // Setup the mesh collider as a trigger
        meshCollider.sharedMesh = colliderMesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = true;

        SetupMaterial();
        meshRenderer.enabled = false;

        // Build initial collider
        UpdateColliderMesh();
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

        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
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
        // Update visual mesh every frame (shows obstacle occlusion)
        UpdateVisualMesh();

        // Update collider mesh less frequently
        if (Time.time - lastColliderUpdateTime > colliderUpdateInterval)
        {
            UpdateColliderMesh();
            lastColliderUpdateTime = Time.time;
        }
    }

    /// <summary>
    /// Visual mesh that shows obstacle occlusion (updated every frame)
    /// </summary>
    private void UpdateVisualMesh()
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

    /// <summary>
    /// Collider mesh uses full range (no obstacle checking) - updated infrequently
    /// </summary>
    private void UpdateColliderMesh()
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
            vertices[i + 1] = direction * visionRange + new Vector3(0, groundOffset, 0);
            currentAngle += angleStep;
        }

        for (int i = 0; i < visionResolution; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        colliderMesh.Clear();
        colliderMesh.vertices = vertices;
        colliderMesh.triangles = triangles;
        colliderMesh.RecalculateNormals();
        colliderMesh.RecalculateBounds();

        // Reassign to trigger collider update
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = colliderMesh;
    }

    /// <summary>
    /// Called when something enters the vision cone trigger
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerLayer(other.gameObject)) return;

        detectedPlayerTransform = other.transform;
        CheckLineOfSight();
    }

    /// <summary>
    /// Called while something stays in the vision cone trigger
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!IsPlayerLayer(other.gameObject)) return;

        detectedPlayerTransform = other.transform;
        CheckLineOfSight();
    }

    /// <summary>
    /// Called when something exits the vision cone trigger
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerLayer(other.gameObject)) return;

        detectedPlayerTransform = null;
        playerDetected = false;
    }

    /// <summary>
    /// Performs a single raycast to check if we have line of sight to the player
    /// </summary>
    private void CheckLineOfSight()
    {
        if (detectedPlayerTransform == null) return;

        Vector3 directionToPlayer = detectedPlayerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        // Single raycast to check for obstacles between enemy and player
        if (Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, distanceToPlayer, obstacleMask | playerLayer))
        {
            // Check if what we hit is the player (not an obstacle)
            if (IsPlayerLayer(hit.collider.gameObject))
            {
                if (!playerDetected)
                {
                    playerDetected = true;
                    if (debugMode) Debug.Log($"[EnemyVisionCone] Player detected by {transform.parent?.name ?? name}!");
                    OnPlayerDetected?.Invoke();
                }
                return;
            }
        }

        // Obstacle is blocking line of sight
        playerDetected = false;
    }

    /// <summary>
    /// Check if a GameObject is on the player layer
    /// </summary>
    private bool IsPlayerLayer(GameObject obj)
    {
        return ((1 << obj.layer) & playerLayer) != 0;
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

        if (colliderMesh != null)
        {
            Destroy(colliderMesh);
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

        // Draw line to detected player
        if (detectedPlayerTransform != null)
        {
            Gizmos.color = playerDetected ? Color.green : Color.yellow;
            Gizmos.DrawLine(transform.position, detectedPlayerTransform.position);
        }
    }
}