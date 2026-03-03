using UnityEngine;

public class ShadowParticleRoom : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomLA room;

    [Header("Particle Settings")]
    [SerializeField] private int particleCount = 64;
    [SerializeField] private float particleSize = 0.12f;
    [SerializeField] private Color particleColor = new Color(0.15f, 0.1f, 0.35f, 0.55f);
    [SerializeField] private float spawnHeightMin = 0.1f;
    [SerializeField] private float spawnHeightMax = 2f;

    [Header("Drift")]
    [SerializeField] private float driftAmplitudeXZ = 0.25f;
    [SerializeField] private float driftAmplitudeY = 0.15f;
    [SerializeField] private float driftSpeed = 0.4f;

    [Header("Performance")]
    [SerializeField] private int lightCheckBatchPerFrame = 8;
    [SerializeField] private Material overrideMaterial;

    private Vector3[] basePositions;
    private float[] phaseOffsets;
    private bool[] inShadow;

    private Matrix4x4[] shadowMatrices;
    private int shadowCount;

    private Mesh quadMesh;
    private Material particleMaterial;

    private int checkIndex;
    private bool isActiveRoom;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        if (room == null)
            room = GetComponentInParent<RoomLA>();

        quadMesh = CreateQuadMesh();
        particleMaterial = overrideMaterial != null ? new Material(overrideMaterial) : CreateMaterial();

        if (particleMaterial != null)
            particleMaterial.SetColor(BaseColorId, particleColor);

        basePositions = new Vector3[particleCount];
        phaseOffsets = new float[particleCount];
        inShadow = new bool[particleCount];
        shadowMatrices = new Matrix4x4[particleCount];
    }

    private void Start()
    {
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.RoomChanged += OnRoomChanged;
            isActiveRoom = RoomManager.Instance.CurrentRoom == room;
        }

        if (room != null)
            GeneratePositions();

        if (isActiveRoom)
            RunFullLightCheck();
    }

    private void OnDestroy()
    {
        if (RoomManager.Instance != null)
            RoomManager.Instance.RoomChanged -= OnRoomChanged;

        if (particleMaterial != null)
            Destroy(particleMaterial);

        if (quadMesh != null)
            Destroy(quadMesh);
    }

    private void Update()
    {
        if (!isActiveRoom || quadMesh == null || particleMaterial == null)
            return;

        StepLightChecks();
        BuildAndDrawInstances();
    }

    private void OnRoomChanged(RoomLA previous, RoomLA current)
    {
        isActiveRoom = current == room;

        if (isActiveRoom)
            RunFullLightCheck();
    }

    /// <summary>
    /// Checks a small batch of particle positions per frame against the light system to avoid per-frame full scans.
    /// </summary>
    private void StepLightChecks()
    {
        if (LightDetectionManager.Instance == null)
            return;

        int count = Mathf.Min(lightCheckBatchPerFrame, particleCount);
        for (int i = 0; i < count; i++)
        {
            int idx = checkIndex % particleCount;
            inShadow[idx] = !LightDetectionManager.Instance.IsPointInLight(basePositions[idx]);
            checkIndex++;
        }
    }

    private void BuildAndDrawInstances()
    {
        float time = Time.time;
        shadowCount = 0;

        for (int i = 0; i < particleCount; i++)
        {
            if (!inShadow[i])
                continue;

            float phase = phaseOffsets[i];
            Vector3 drift = new Vector3(
                Mathf.Sin(time * driftSpeed + phase) * driftAmplitudeXZ,
                Mathf.Sin(time * driftSpeed * 0.7f + phase + 1f) * driftAmplitudeY,
                Mathf.Cos(time * driftSpeed + phase + 0.5f) * driftAmplitudeXZ
            );

            Vector3 worldPos = basePositions[i] + drift;
            shadowMatrices[shadowCount] = Matrix4x4.TRS(worldPos, Quaternion.identity, Vector3.one * particleSize);
            shadowCount++;
        }

        if (shadowCount > 0)
            Graphics.DrawMeshInstanced(quadMesh, 0, particleMaterial, shadowMatrices, shadowCount);
    }

    private void RunFullLightCheck()
    {
        if (LightDetectionManager.Instance == null)
            return;

        for (int i = 0; i < particleCount; i++)
            inShadow[i] = !LightDetectionManager.Instance.IsPointInLight(basePositions[i]);

        checkIndex = 0;
    }

    /// <summary>
    /// Rejection-samples random positions within the room's collider AABB, discarding any outside the room's actual bounds.
    /// </summary>
    private void GeneratePositions()
    {
        Bounds roomBounds = ComputeRoomBounds();

        int placed = 0;
        int attempts = 0;
        int maxAttempts = particleCount * 20;

        while (placed < particleCount && attempts < maxAttempts)
        {
            attempts++;

            Vector3 candidate = new Vector3(
                Random.Range(roomBounds.min.x, roomBounds.max.x),
                Random.Range(spawnHeightMin, spawnHeightMax),
                Random.Range(roomBounds.min.z, roomBounds.max.z)
            );

            Vector3 floorCandidate = new Vector3(candidate.x, roomBounds.center.y, candidate.z);

            if (!room.Contains(floorCandidate))
                continue;

            basePositions[placed] = candidate;
            phaseOffsets[placed] = Random.Range(0f, Mathf.PI * 2f);
            placed++;
        }

        if (placed < particleCount)
        {
            for (int i = placed; i < particleCount; i++)
            {
                basePositions[i] = basePositions[placed > 0 ? placed - 1 : 0];
                phaseOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
            }
        }
    }

    private Bounds ComputeRoomBounds()
    {
        Collider[] colliders = room.BoundaryColliders;
        if (colliders == null || colliders.Length == 0)
            return new Bounds(transform.position, Vector3.one * 10f);

        Bounds combined = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                combined.Encapsulate(colliders[i].bounds);
        }

        return combined;
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ShadowParticleQuad";

        float h = 0.5f;
        mesh.vertices = new Vector3[]
        {
            new Vector3(-h, 0f, -h),
            new Vector3( h, 0f, -h),
            new Vector3( h, 0f,  h),
            new Vector3(-h, 0f,  h),
        };

        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
        };

        mesh.RecalculateNormals();
        return mesh;
    }

    private static Material CreateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[ShadowParticleRoom] URP/Unlit shader not found. Assign a material via overrideMaterial.");
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
        mat.enableInstancing = true;

        return mat;
    }
}