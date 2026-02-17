﻿﻿using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float moveSpeed = 10f;
    public float fastMoveMultiplier = 2f;

    [Header("Rotation Settings")]
    public float rotateSpeed = 0.3f;

    [Header("Player Tether")]
    [Tooltip("The player transform to stay near")]
    public Transform player;
    [Tooltip("Maximum distance camera can be from player")]
    public float maxDistanceFromPlayer = 25f;

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

    private bool isRotating;
    private bool isFastMoving;
    private Vector3 desiredMoveDirection;
    private float desiredMoveSpeed;
    private float pendingYaw;

    //Input actions awake -EM//
    private void Awake()
    {
        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
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
    private void Update()
    {
        CachePanInput();
        CacheRotationInput();
        ApplyPan();
        ApplyRotation();
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
        Vector3 newPosition = transform.position + delta;

        if (player != null)
        {
            Vector3 offset = newPosition - player.position;
            offset.y = 0f;

            if (offset.magnitude > maxDistanceFromPlayer)
            {
                offset = offset.normalized * maxDistanceFromPlayer;
                newPosition = player.position + offset;
                newPosition.y = transform.position.y;
            }
        }

        newPosition = ClampToCurrentRoom(newPosition);
        transform.position = newPosition;
    }

    //Handle rotation update -EM//
    private void CacheRotationInput()
    {
       if(!isRotating || rotateAction == null) return;

       Vector2 mouseDelta = rotateDeltaAction.ReadValue<Vector2>();
        float angle = mouseDelta.x * rotateSpeed;

        pendingYaw += angle;
    }

    private void ApplyRotation()
    {
        if (pendingYaw == 0f) return;

        Quaternion rotation = Quaternion.Euler(0f, pendingYaw, 0f) * transform.rotation;
        transform.rotation = rotation;
        pendingYaw = 0f;
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
}
