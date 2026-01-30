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

    [Header("Movement Range")]
    [Tooltip("Maximum distance the player can move in a single click")]
    public float maxMoveDistance = 20f;
    [Tooltip("Minimum distance the player can move in a single click")]
    public float minMoveDistance = 0.3f;

    //Input system updated - EM//
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("Ability Targeting")]
    [SerializeField] private AbilityTargeting targetingSystem;

    [Header("References")]
    [SerializeField] private Player player;

    [Header("Destination Indicator")]
    [Tooltip("Prefab to spawn at the destination point")]
    public GameObject destinationIndicatorPrefab;
    [Tooltip("Height offset above the clicked point")]
    public float indicatorHeight = 0.05f;

    [Header("NavMesh Validation")]
    [Tooltip("Maximum distance to search for valid NavMesh position - keep small to avoid clicking through walls")]
    public float navMeshSampleDistance = 0.3f;

    [Header("Arrival Settings")]
    [Tooltip("Distance threshold to consider the player has arrived (should be >= agent stopping distance)")]
    public float arrivalThreshold = 0.5f;
    [Tooltip("If velocity is below this for stuckTime seconds, consider arrived/stuck")]
    public float stuckVelocityThreshold = 0.1f;
    [Tooltip("Time in seconds of low velocity before considering stuck")]
    public float stuckTime = 0.5f;

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
    private InputAction endTurnAction;

    private GameObject destinationIndicatorInstance;

    private float lowVelocityTimer = 0f;
    private Vector3 lastMovePosition;
    private float moveDistanceAccumulator = 0f;
    private bool trackingMovement = false;
    private int movementPointsSpent = 0;

    public delegate void LightStateChanged(bool inLight);
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
        if (targetingSystem == null)
        {
            targetingSystem = GetComponent<AbilityTargeting>();
        }
        if (player == null)
        {
            player = GetComponent<Player>();
        }

        //Setup input action - EM//
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            moveAction = playerMap.FindAction("Move");
            stopMovementAction = playerMap.FindAction("StopMovement");
            pointerPositionAction = playerMap.FindAction("PointerPosition");
            endTurnAction = playerMap.FindAction("EndTurn");
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

        if (endTurnAction != null)
        {
            endTurnAction.performed += OnEndTurn;
            endTurnAction.Enable();
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

        if (endTurnAction != null)
        {
            endTurnAction.performed -= OnEndTurn;
            endTurnAction.Disable();
        }
    }

    private void Start()
    {
        UpdateLightState();
    }

    private void Update()
    {
        if (IsTargetingActive() && agent.hasPath)
        {
            StopMovement();
        }
        if (!CanPlayerAct() && agent.hasPath)
        {
            StopMovement();
        }
        UpdateMovingState();
        if (CombatManager.Instance != null && CombatManager.Instance.InCombat && isMoving)
        {
            UpdateMovementCost();
        }
        UpdateLightState();
        UpdateDestinationIndicator();
    }

    //Input system on move performed- EM//
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        if (!CanPlayerAct()) return;
        if (IsTargetingActive()) return;
        if (mainCamera == null) return;

        if (pointerPositionAction == null) return;

        if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
        {
            if (player == null) return;
            if (!player.CanSpendActionPoints(1)) return;
        }

        Vector2 pointerPos = pointerPositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, walkableMask))
        {
            Vector3 targetPoint = hit.point;

            Vector3 toTarget = targetPoint - transform.position;
            if (toTarget.magnitude > maxMoveDistance)
            {
                return;
            }
            if (toTarget.magnitude < minMoveDistance)
            {
                return;
            }

            if (NavMesh.SamplePosition(targetPoint, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(navHit.position);
                lowVelocityTimer = 0f;
                ShowDestinationIndicator(navHit.position);
                StartMovementTracking();
            }
        }
    }

    //Input System On stop movement - EM//
    private void OnStopMovement(InputAction.CallbackContext context)
    {
        StopMovement();
    }

    private void StopMovement()
    {
        agent.isStopped = true;
        agent.ResetPath();
        HideDestinationIndicator();
        lowVelocityTimer = 0f;
        StopMovementTracking();
    }

    private void OnEndTurn(InputAction.CallbackContext context)
    {
        if (CombatManager.Instance == null) return;
        if (!CombatManager.Instance.InCombat) return;
        if (!CombatManager.Instance.IsPlayerTurn) return;

        StopMovement();
        CombatManager.Instance.EndCurrentTurn();
    }

    private bool CanPlayerAct()
    {
        if (CombatManager.Instance == null) return true;
        if (!CombatManager.Instance.InCombat) return true;
        return CombatManager.Instance.IsPlayerTurn;
    }

    private void StartMovementTracking()
    {
        trackingMovement = true;
        lastMovePosition = transform.position;
        moveDistanceAccumulator = 0f;
        movementPointsSpent = 0;

        if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
        {
            if (player == null)
            {
                StopMovement();
                return;
            }

            if (!player.SpendActionPoints(1))
            {
                StopMovement();
                return;
            }

            movementPointsSpent = 1;
        }
    }

    private void StopMovementTracking()
    {
        trackingMovement = false;
        moveDistanceAccumulator = 0f;
        movementPointsSpent = 0;
    }

    private void UpdateMovementCost()
    {
        if (!trackingMovement) return;
        if (player == null) return;
        if (CombatManager.Instance == null || !CombatManager.Instance.InCombat) return;

        Vector3 currentPosition = transform.position;
        float deltaDistance = Vector3.Distance(currentPosition, lastMovePosition);
        lastMovePosition = currentPosition;

        if (deltaDistance <= 0f) return;

        moveDistanceAccumulator += deltaDistance;

        int requiredPoints = Mathf.Max(1, Mathf.CeilToInt(moveDistanceAccumulator));
        while (movementPointsSpent < requiredPoints)
        {
            if (!player.SpendActionPoints(1))
            {
                StopMovement();
                return;
            }
            movementPointsSpent += 1;
        }
    }

    private bool IsTargetingActive()
    {
        return targetingSystem != null && targetingSystem.IsTargeting;
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

        if (HasArrivedAtDestination())
        {
            StopMovement();
        }
    }

    private bool HasArrivedAtDestination()
    {
        if (!agent.hasPath)
            return true;

        if (agent.remainingDistance <= arrivalThreshold)
            return true;

        if (agent.velocity.magnitude < stuckVelocityThreshold)
        {
            lowVelocityTimer += Time.deltaTime;
            if (lowVelocityTimer >= stuckTime)
            {
                Debug.Log("PlayerController: Detected stuck state, stopping movement");
                return true;
            }
        }
        else
        {
            lowVelocityTimer = 0f;
        }

        return false;
    }

    private void UpdateMovingState()
    {
        bool wasMoving = isMoving;

        isMoving = agent.hasPath && !HasArrivedAtDestination();

        if (wasMoving != isMoving)
        {
            OnMovementStateChanged?.Invoke(isMoving);

            if (!isMoving)
            {
                HideDestinationIndicator();
                StopMovementTracking();
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
            OnLightStateChanged?.Invoke(isInLight);
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

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(agent.destination, arrivalThreshold);
        }
    }
}
