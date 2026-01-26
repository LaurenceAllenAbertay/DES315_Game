using UnityEngine;
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
        HandlePan();
        HandleRotation();
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
    private void HandlePan()
    {
        if (movementAction == null) return;

        Vector2 input = movementAction.ReadValue<Vector2>();

        if (input == Vector2.zero) return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDirection = (forward * input.y + right * input.x).normalized;

        float speed = moveSpeed;
        if (isFastMoving)
        {
            speed *= fastMoveMultiplier;
        }

        Vector3 newPosition = transform.position + moveDirection * speed * Time.deltaTime;

        // Hard clamp to player
        if (player != null)
        {
            Vector3 offset = newPosition - player.position;
            offset.y = 0f;

            if (offset.magnitude > maxDistanceFromPlayer)
            {
                offset = offset.normalized * maxDistanceFromPlayer;
                newPosition = player.position + offset;
                newPosition.y = transform.position.y; // Preserve camera height
            }
        }

        transform.position = newPosition;
    }

    //Handle rotation update -EM//
    private void HandleRotation()
    {
       if(!isRotating || rotateAction == null) return;

       Vector2 mouseDelta = rotateDeltaAction.ReadValue<Vector2>();
        float angle = mouseDelta.x * rotateSpeed;

        transform.Rotate(0f, angle, 0f, Space.World);
    }
}