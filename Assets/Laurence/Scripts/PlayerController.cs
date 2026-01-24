using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{
    [Header("Click Settings")]
    public LayerMask walkableMask = ~0;

    [Header("Light Detection")]
    public Vector3 lightCheckOffset = new Vector3(0f, 1f, 0f);

    //Input system updated - EM//
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("Path Visualization")]
    private LineRenderer pathLineRenderer;
    public float pathLineHeight = 0.1f;
    public Color pathLineColor = Color.green;
    public float pathLineWidth = 0.1f;

    [Header("Current State (Read Only)")]
    [SerializeField] private bool isInLight;
    [SerializeField] private float currentLightLevel;
    [SerializeField] private bool isMoving;

    private NavMeshAgent agent;
    private Camera mainCamera;

    //Input Actions - EM//
    private InputAction moveAction;
    private InputAction stopMovementAction;
    private InputAction pointerPositionAction;


    public delegate void LightStateChanged(bool inLight, float lightLevel);
    public event LightStateChanged OnLightStateChanged;

    public delegate void MovementStateChanged(bool moving);
    public event MovementStateChanged OnMovementStateChanged;

    public bool IsInLight => isInLight;
    public float CurrentLightLevel => currentLightLevel;
    public bool IsMoving => isMoving;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        mainCamera = Camera.main;

        //Setup input action - EM//
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            moveAction = playerMap.FindAction("Move");
            stopMovementAction = playerMap.FindAction("StopMovement");
            pointerPositionAction = playerMap.FindAction("PointerPosition");
        }

        // Setup LineRenderer
        if (pathLineRenderer == null)
        {
            pathLineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // Configure LineRenderer
        pathLineRenderer.startWidth = pathLineWidth;
        pathLineRenderer.endWidth = pathLineWidth;
        pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        pathLineRenderer.startColor = pathLineColor;
        pathLineRenderer.endColor = pathLineColor;
        pathLineRenderer.positionCount = 0;
    }

    //Movement System on Enable - EM//
    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.performed += OnMovePerformed;
            moveAction.Enable();
        }

        if (stopMovementAction != null)
        {
            stopMovementAction.performed += OnStopMovement;
            stopMovementAction.Enable();
        }

        if (pointerPositionAction != null)
        {
            pointerPositionAction.Enable();
        }
    }

    //Movement System on Disable- EM//
    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.Disable();
        }

        if (stopMovementAction != null)
        {
            stopMovementAction.performed -= OnStopMovement;
            stopMovementAction.Disable();
        }

        if (pointerPositionAction != null)
        {
            pointerPositionAction.Disable();
        }
    }

    private void Start()
    {
        UpdateLightState();
    }

    private void Update()
    {
        UpdateMovingState();
        UpdateLightState();
        UpdatePathLine();
    }

    //Input system on move performed- EM//
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        if (mainCamera == null) return;


        if (pointerPositionAction == null) return;

        Vector2 pointerPos = pointerPositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, walkableMask))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.point);
        }

    }

    //Input System On stop movement - EM//
    private void OnStopMovement(InputAction.CallbackContext context)
    {
        agent.isStopped = true;
    }

    private void UpdateMovingState()
    {
        bool wasMoving = isMoving;

        isMoving = agent.hasPath && agent.remainingDistance > agent.stoppingDistance;

        if (wasMoving != isMoving)
        {
            OnMovementStateChanged?.Invoke(isMoving);
        }
    }

    private void UpdateLightState()
    {
        if (LightDetectionManager.Instance == null) return;

        Vector3 checkPoint = transform.position + lightCheckOffset;
        LightCheckResult result = LightDetectionManager.Instance.CheckLightAtPoint(checkPoint);

        bool previousState = isInLight;

        isInLight = result.isInLight;
        currentLightLevel = result.totalLightContribution;

        if (previousState != isInLight)
        {
            OnLightStateChanged?.Invoke(isInLight, currentLightLevel);
        }
    }

    private void UpdatePathLine()
    {
        if (pathLineRenderer == null) return;

        if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance)
        {
            // Get the path corners
            Vector3[] corners = agent.path.corners;

            // Set the number of positions in the line renderer
            pathLineRenderer.positionCount = corners.Length;

            // Set each position
            for (int i = 0; i < corners.Length; i++)
            {
                pathLineRenderer.SetPosition(i, corners[i] + Vector3.up * pathLineHeight);
            }
        }
        else
        {
            // Clear the line when there's no path
            pathLineRenderer.positionCount = 0;
        }
    }

    public Vector3 GetLightCheckPoint()
    {
        return transform.position + lightCheckOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPoint = transform.position + lightCheckOffset;
        Gizmos.color = isInLight ? Color.yellow : Color.blue;
        Gizmos.DrawWireSphere(checkPoint, 0.2f);

        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.green;
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }
    }
}