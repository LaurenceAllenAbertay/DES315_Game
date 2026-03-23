using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float moveSpeed = 10f;
    public float fastMoveMultiplier = 2f;

    [Header("Rotation Settings")]
    public float rotateSpeed = 0.3f;
    
    [Tooltip("Pivot offset in camera local space (forward = +Z, down = -Y)")]
    public Vector3 offsetVector = new Vector3(0f, -6f, 5f);

    [Header("Player Follow")]
    [Tooltip("The player transform to follow")]
    public Transform player;
    [Tooltip("PlayerController to subscribe to for movement events")]
    public PlayerController playerController;
    [Tooltip("How fast the camera lerps to the player when they start moving")]
    public float followLerpSpeed = 8f;

    [Header("Room Clamp")]
    [SerializeField] private RoomManager roomManager;
    [Tooltip("Extra inward padding from the room boundary (units).")]
    [SerializeField] private float roomClampPadding = 1f;

    //Input actions update -EM//
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    //Input actions -EM//
    private InputAction movementAction;
    private InputAction fastMoveAction;
    private InputAction rotateAction;
    private InputAction rotateDeltaAction;

    public Vector3 PivotPoint => pivotPoint;
    private bool isRotating;
    [Header("Carousel Pan")]
    [SerializeField] private float carouselPanSpeed = 8f;

    private bool isFollowingPlayer;
    private bool isFastMoving;
    private Vector3 desiredMoveDirection;
    private float desiredMoveSpeed;
    private float pendingYaw;
    private Vector3 pivotPoint;
    private float cameraPitch;
    private float currentYaw;
    private Vector3? lerpPanTarget;
    

    //Input actions awake -EM//
    private void Awake()
    {
        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        //Setup input actions//
        if(inputActions != null)
        {
            var cameraMap = inputActions.FindActionMap("Camera");
            movementAction = cameraMap.FindAction("Movement");
            fastMoveAction = cameraMap.FindAction("FastMove");
            rotateAction = cameraMap.FindAction("Rotate");
            rotateDeltaAction = cameraMap.FindAction("RotateDelta");
        }

    }

    //Input action on Enable -EM//
    private void OnEnable()
    {
        if (playerController != null)
        {
            playerController.OnMovementStateChanged += OnPlayerMovementStateChanged;
        }

        if(movementAction != null)
        {
            movementAction.Enable();
        }

        if (fastMoveAction != null)
        {
            fastMoveAction.performed += OnFastMovePerformed;
            fastMoveAction.canceled += OnFastMoveCanceled;
            fastMoveAction.Enable();
        }

        if(rotateAction != null)
        {
            rotateAction.performed += OnRotateStarted;
            rotateAction.canceled += OnRotateCanceled;
            rotateAction.Enable();
        }

        if(rotateDeltaAction != null)
        {
            rotateDeltaAction.Enable();
        }
    }

    //Input action on Disable -EM//
    private void OnDisable()
    {
        if (playerController != null)
        {
            playerController.OnMovementStateChanged -= OnPlayerMovementStateChanged;
        }

        if(movementAction != null)
        {
            movementAction.Disable();
        }

        if(fastMoveAction != null)
        {
            fastMoveAction.performed -= OnFastMovePerformed;
            fastMoveAction.canceled -= OnFastMoveCanceled;
            fastMoveAction.Disable();
        }

        if (rotateAction != null)
        {
            rotateAction.performed -= OnRotateStarted;
            rotateAction.canceled -= OnRotateCanceled;
            rotateAction.Disable();
        }

        if (rotateDeltaAction != null)
        {
            rotateDeltaAction.Disable();
        }
    }
    private void Start()
    {
        cameraPitch = transform.eulerAngles.x;
        currentYaw = transform.eulerAngles.y;
        pivotPoint = transform.position + transform.rotation * offsetVector;
    }

    private void Update()
    {
        CachePanInput();
        CacheRotationInput();
        ApplyPlayerFollow();
        ApplyLerpPan();
        ApplyPan();
        ApplyRotation();
        ApplyCameraTransform();
    }

    //On fast move perfromed -EM//
    private void OnFastMovePerformed(InputAction.CallbackContext context)
    {
        isFastMoving = true;
    }
    //on fast movment cancelled -EM//
    private void OnFastMoveCanceled(InputAction.CallbackContext context)
    {
        isFastMoving = false;
    }
    //On rotate started -EM/
    private void OnRotateStarted(InputAction.CallbackContext context)
    {
        isRotating = true;
    }

    //On rotate canceled -EM//
    private void OnRotateCanceled(InputAction.CallbackContext context)
    {
        isRotating= false;
    }

    //Update on Handle pan -EM//
    private void CachePanInput()
    {
        if (movementAction == null)
        {
            desiredMoveDirection = Vector3.zero;
            desiredMoveSpeed = 0f;
            return;
        }

        Vector2 input = movementAction.ReadValue<Vector2>();

        if (input == Vector2.zero)
        {
            desiredMoveDirection = Vector3.zero;
            desiredMoveSpeed = 0f;
            return;
        }

        lerpPanTarget = null;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        desiredMoveDirection = (forward * input.y + right * input.x).normalized;

        float speed = moveSpeed;
        if (isFastMoving)
        {
            speed *= fastMoveMultiplier;
        }

        desiredMoveSpeed = speed;
    }

    private void ApplyPan()
    {
        if (desiredMoveDirection == Vector3.zero || desiredMoveSpeed <= 0f) return;

        Vector3 delta = desiredMoveDirection * desiredMoveSpeed * Time.deltaTime;
        pivotPoint += delta;
    }

    //Handle rotation update -EM//
    private void CacheRotationInput()
    {
       if(!isRotating || rotateAction == null) return;

       Vector2 mouseDelta = rotateDeltaAction.ReadValue<Vector2>();
        float angle = mouseDelta.x * rotateSpeed;

        pendingYaw += angle;
    }

    /// <summary>
    /// Orbits the full camera arm (including Y) around the pivot, then faces the camera toward the pivot.
    /// </summary>
    private void ApplyRotation()
    {
        if (pendingYaw == 0f) return;
        currentYaw += pendingYaw;
        pendingYaw = 0f;
    }

    /// <summary>
    /// Single point that writes to transform. Derives position from pivotPoint + currentYaw,
    /// clamps camera XZ to the room, and back-computes pivot from the clamped position so they stay consistent.
    /// </summary>
    private void ApplyCameraTransform()
    {
        Quaternion rot = Quaternion.Euler(cameraPitch, currentYaw, 0f);
        Vector3 desiredPos = pivotPoint - rot * offsetVector;
        Vector3 clampedPos = ClampToCurrentRoom(desiredPos);
        transform.position = clampedPos;
        transform.rotation = rot;
        if (clampedPos != desiredPos)
            pivotPoint = clampedPos + rot * offsetVector;
    }

    private Vector3 ClampToCurrentRoom(Vector3 position)
    {
        RoomLA currentRoom = roomManager != null ? roomManager.CurrentRoom : null;
        if (currentRoom == null)
        {
            return position;
        }

        Collider[] colliders = currentRoom.BoundaryColliders;
        if (colliders == null || colliders.Length == 0)
        {
            return position;
        }

        if (IsInsideRoomXZ(position, colliders))
        {
            if (roomClampPadding <= 0f)
            {
                return position;
            }

            if (TryGetContainingCollider(position, colliders, out Collider containingCollider))
            {
                return ClampPositionXZToBounds(position, containingCollider.bounds, roomClampPadding);
            }

            return position;
        }

        Vector2 positionXZ = new Vector2(position.x, position.z);
        float bestSqrDistance = float.PositiveInfinity;
        Vector3 bestPosition = position;

        Collider bestCollider = null;

        foreach (var collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            Vector3 probe = position;
            probe.y = collider.bounds.center.y;
            Vector3 closest = collider.ClosestPoint(probe);
            Vector2 closestXZ = new Vector2(closest.x, closest.z);
            float sqrDistance = (closestXZ - positionXZ).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestPosition = new Vector3(closest.x, position.y, closest.z);
                bestCollider = collider;
            }
        }

        if (bestCollider != null && roomClampPadding > 0f)
        {
            return ClampPositionXZToBounds(bestPosition, bestCollider.bounds, roomClampPadding);
        }

        return bestPosition;
    }

    private static bool IsInsideRoomXZ(Vector3 position, Collider[] colliders)
    {
        Vector2 positionXZ = new Vector2(position.x, position.z);

        foreach (var collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            Vector3 probe = position;
            probe.y = collider.bounds.center.y;
            Vector3 closest = collider.ClosestPoint(probe);
            Vector2 closestXZ = new Vector2(closest.x, closest.z);

            if ((closestXZ - positionXZ).sqrMagnitude <= 0.0001f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetContainingCollider(Vector3 position, Collider[] colliders, out Collider containing)
    {
        foreach (var collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            Vector3 probe = position;
            probe.y = collider.bounds.center.y;
            Vector3 closest = collider.ClosestPoint(probe);
            Vector2 closestXZ = new Vector2(closest.x, closest.z);
            Vector2 positionXZ = new Vector2(position.x, position.z);

            if ((closestXZ - positionXZ).sqrMagnitude <= 0.0001f)
            {
                containing = collider;
                return true;
            }
        }

        containing = null;
        return false;
    }

    private static Vector3 ClampPositionXZToBounds(Vector3 position, Bounds bounds, float padding)
    {
        float minX = bounds.min.x + padding;
        float maxX = bounds.max.x - padding;
        float minZ = bounds.min.z + padding;
        float maxZ = bounds.max.z - padding;

        if (minX > maxX)
        {
            float centerX = bounds.center.x;
            minX = centerX;
            maxX = centerX;
        }

        if (minZ > maxZ)
        {
            float centerZ = bounds.center.z;
            minZ = centerZ;
            maxZ = centerZ;
        }

        float clampedX = Mathf.Clamp(position.x, minX, maxX);
        float clampedZ = Mathf.Clamp(position.z, minZ, maxZ);

        return new Vector3(clampedX, position.y, clampedZ);
    }

    private void OnPlayerMovementStateChanged(bool moving)
    {
        isFollowingPlayer = moving;
    }

    /// <summary>
    /// Lerps the orbit pivot toward the player XZ while following, moving the camera by the same delta so the offset is preserved.
    /// </summary>
    private void ApplyPlayerFollow()
    {
        if (!isFollowingPlayer || player == null) return;

        Vector3 targetPivot = new Vector3(player.position.x, pivotPoint.y, player.position.z);
        pivotPoint = Vector3.Lerp(pivotPoint, targetPivot, followLerpSpeed * Time.deltaTime);
    }

    //Instantly snap the camera to be centred over the player -EM//
    private void ApplyLerpPan()
    {
        if (lerpPanTarget == null) return;

        Vector3 target = new Vector3(lerpPanTarget.Value.x, pivotPoint.y, lerpPanTarget.Value.z);
        pivotPoint = Vector3.Lerp(pivotPoint, target, carouselPanSpeed * Time.deltaTime);

        if (Vector3.Distance(new Vector3(pivotPoint.x, 0f, pivotPoint.z), new Vector3(target.x, 0f, target.z)) < 0.01f)
        {
            pivotPoint = target;
            lerpPanTarget = null;
        }
    }

    public void SnapToPlayer()
    {
        if (player == null) return;

        pivotPoint = new Vector3(player.position.x, pivotPoint.y, player.position.z);
        ApplyCameraTransform();
    }

    public void PanToPosition(Vector3 worldPosition)
    {
        lerpPanTarget = worldPosition;
    }
}