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

    [Header("Destination Indicator")]
    [Tooltip("Prefab to spawn at the destination point")]
    public GameObject destinationIndicatorPrefab;
    [Tooltip("Height offset above the clicked point")]
    public float indicatorHeight = 0.05f;

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

    private GameObject destinationIndicatorInstance;

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

        if (destinationIndicatorPrefab != null)
        {
            destinationIndicatorInstance = Instantiate(destinationIndicatorPrefab);
            destinationIndicatorInstance.SetActive(false);
        }
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
        UpdateDestinationIndicator();
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

            ShowDestinationIndicator(hit.point);
        }
    }

    //Input System On stop movement - EM//
    private void OnStopMovement(InputAction.CallbackContext context)
    {
        agent.isStopped = true;
        HideDestinationIndicator();
    }

    private void ShowDestinationIndicator(Vector3 position)
    {
        if (destinationIndicatorInstance == null) return;

        destinationIndicatorInstance.transform.position = position + Vector3.up * indicatorHeight;
        destinationIndicatorInstance.SetActive(true);
    }

    private void HideDestinationIndicator()
    {
        if (destinationIndicatorInstance == null) return;

        destinationIndicatorInstance.SetActive(false);
    }

    private void UpdateDestinationIndicator()
    {
        if (destinationIndicatorInstance == null || !destinationIndicatorInstance.activeSelf) return;

        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance)
        {
            HideDestinationIndicator();
        }
    }

    private void UpdateMovingState()
    {
        bool wasMoving = isMoving;

        isMoving = agent.hasPath && agent.remainingDistance > agent.stoppingDistance;

        if (wasMoving != isMoving)
        {
            OnMovementStateChanged?.Invoke(isMoving);

            if (!isMoving)
            {
                HideDestinationIndicator();
            }
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

    public Vector3 GetLightCheckPoint()
    {
        return transform.position + lightCheckOffset;
    }

    private void OnDestroy()
    {
        if (destinationIndicatorInstance != null)
        {
            Destroy(destinationIndicatorInstance);
        }
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