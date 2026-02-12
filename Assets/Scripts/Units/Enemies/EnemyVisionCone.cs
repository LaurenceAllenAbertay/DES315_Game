using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(SphereCollider))]
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
    [Tooltip("Optional material asset to use for the vision cone (prevents shader stripping in builds).")]
    [SerializeField] private Material visionMaterial;

    [Header("Debug")]
    public bool debugMode = true;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private SphereCollider visionTrigger;
    private Mesh visionMesh;
    private bool playerDetected = false;
    private Material instancedMaterial;

    // Cache for player detection
    private Transform detectedPlayerTransform;

    public delegate void PlayerDetected();
    public event PlayerDetected OnPlayerDetected;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        visionTrigger = GetComponent<SphereCollider>();

        // Visual mesh (updated every frame for obstacle occlusion visuals)
        visionMesh = new Mesh();
        visionMesh.name = "Vision Cone Visual";
        meshFilter.mesh = visionMesh;

        // Setup the trigger used for nearby detection only
        visionTrigger.isTrigger = true;
        visionTrigger.center = Vector3.zero;
        visionTrigger.radius = visionRange;

        SetupMaterial();
        meshRenderer.enabled = false;

    }

    private void SetupMaterial()
    {
        if (visionMaterial != null)
        {
            instancedMaterial = new Material(visionMaterial);
        }
        else
        {
            instancedMaterial = CreateTransparentMaterial();
        }

        if (instancedMaterial != null)
        {
            meshRenderer.material = instancedMaterial;
            SetColor(visionColor);
        }
    }

    private Material CreateTransparentMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            if (debugMode)
            {
                Debug.LogWarning("[EnemyVisionCone] URP/Unlit shader not found. Assign a material in the inspector.");
            }
            return null;
        }
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
        UpdateTriggerRadius();
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

    private void UpdateTriggerRadius()
    {
        if (visionTrigger != null)
        {
            visionTrigger.radius = visionRange;
        }
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
        Vector3 directionFlat = new Vector3(directionToPlayer.x, 0f, directionToPlayer.z);

        if (directionFlat.sqrMagnitude < 0.0001f)
        {
            playerDetected = false;
            return;
        }

        float angleToPlayer = Vector3.Angle(transform.forward, directionFlat.normalized);
        if (angleToPlayer > visionAngle * 0.5f)
        {
            playerDetected = false;
            return;
        }

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
