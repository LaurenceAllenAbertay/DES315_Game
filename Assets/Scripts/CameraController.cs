﻿using UnityEngine;
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

    [Header("Collision")]
    [Tooltip("Radius used for camera collision checks")]
    public float collisionRadius = 0.5f;
    [Tooltip("Extra distance kept from walls to prevent jitter")]
    public float collisionSkin = 0.05f;
    [Tooltip("Layers considered for camera collision")]
    public LayerMask collisionMask = ~0;

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
    private Rigidbody rb;

    private Vector3 desiredMoveDirection;
    private float desiredMoveSpeed;
    private float pendingYaw;

    //Input actions awake -EM//
    private void Awake()
    {
        //Setup input actions//
        if(inputActions != null)
        {
            var cameraMap = inputActions.FindActionMap("Camera");
            movementAction = cameraMap.FindAction("Movement");
            fastMoveAction = cameraMap.FindAction("FastMove");
            rotateAction = cameraMap.FindAction("Rotate");
            rotateDeltaAction = cameraMap.FindAction("RotateDelta");
        }

        rb = GetComponent<Rigidbody>();
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
        
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
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
        if (rb == null || desiredMoveDirection == Vector3.zero || desiredMoveSpeed <= 0f) return;

        Vector3 delta = desiredMoveDirection * desiredMoveSpeed * Time.fixedDeltaTime;
        Vector3 newPosition = rb.position + delta;
        newPosition = ResolveCameraCollision(rb.position, newPosition);

        if (player != null)
        {
            Vector3 offset = newPosition - player.position;
            offset.y = 0f;

            if (offset.magnitude > maxDistanceFromPlayer)
            {
                offset = offset.normalized * maxDistanceFromPlayer;
                newPosition = player.position + offset;
                newPosition.y = rb.position.y;
            }
        }

        rb.MovePosition(newPosition);
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
        if (rb == null || pendingYaw == 0f) return;

        Quaternion rotation = Quaternion.Euler(0f, pendingYaw, 0f) * rb.rotation;
        rb.MoveRotation(rotation);
        pendingYaw = 0f;
    }

    private Vector3 ResolveCameraCollision(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= Mathf.Epsilon) return to;

        Vector3 direction = delta / distance;
        if (Physics.SphereCast(from, collisionRadius, direction, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - collisionSkin);
            return from + direction * safeDistance;
        }

        return to;
    }
}
