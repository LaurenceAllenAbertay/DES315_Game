using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{
    [Header("Click Settings")]
    public LayerMask walkableMask = ~0;
    [Tooltip("Objects on these layers block movement clicks — clicks landing on them are ignored")]
    public LayerMask occluderMask;

    [Header("Movement Range")]
    public float minMoveDistance = 0.3f;
    [Tooltip("Extra distance added in the cursor direction while hold-moving, so the player walks through the cursor rather than stopping at it")]
    public float holdMoveLookAhead = 1.5f;
    [Tooltip("Cursor distance at which hold-move speed reaches minimum")]
    public float holdMoveMinSpeedDistance = 1f;
    [Tooltip("Cursor distance at which hold-move speed reaches full agent speed")]
    public float holdMoveMaxSpeedDistance = 6f;
    [Tooltip("Minimum speed fraction when cursor is very close (0–1)")]
    [Range(0f, 1f)] public float holdMoveMinSpeedFraction = 0.15f;

    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("Startup Movement Lock")]
    [Tooltip("Seconds to block player movement input at the start of the game")]
    [SerializeField] private float movementLockSeconds = 2f;

    [Header("Ability Targeting")]
    [SerializeField] private AbilityTargeting targetingSystem;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private LightDetectable lightDetectable;

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

    [Header("Debug")]
    public bool debugMode = true;

    [Header("Current State (Read Only)")]
    [SerializeField] private bool isMoving;

    private NavMeshAgent agent;
    private Camera mainCamera;

    private InputAction moveAction;
    private InputAction holdMoveAction;
    private InputAction stopMovementAction;
    private InputAction pointerPositionAction;
    private InputAction endTurnAction;

    private bool isHoldMoving;
    private float agentBaseSpeed;

    private GameObject destinationIndicatorInstance;

    private float lowVelocityTimer = 0f;
    private Vector3 lastFramePosition;
    private bool isPointerOverUI;
    private float movementUnlockTime;

    public delegate void LightStateChanged(bool inLight);
    public event LightStateChanged OnLightStateChanged;

    public delegate void MovementStateChanged(bool moving);
    public event MovementStateChanged OnMovementStateChanged;

    public bool IsInLight => lightDetectable != null && lightDetectable.IsInLight;
    public float CurrentLightLevel => lightDetectable != null ? lightDetectable.LightLevel : 0f;
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

        if (lightDetectable == null)
        {
            lightDetectable = GetComponent<LightDetectable>();
        }

        if (lightDetectable != null)
        {
            lightDetectable.OnLightStateChanged += OnLightDetectableStateChanged;
        }

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            moveAction = playerMap.FindAction("Move");
            holdMoveAction = playerMap.FindAction("HoldMove");
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

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.performed += OnMovePerformed;
            moveAction.Enable();
        }

        if (holdMoveAction != null)
        {
            holdMoveAction.performed += OnHoldMoveStarted;
            holdMoveAction.canceled += OnHoldMoveCanceled;
            holdMoveAction.Enable();
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

        if (targetingSystem != null)
        {
            targetingSystem.OnTargetConfirmed += OnAbilityTargetConfirmed;
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.Disable();
        }

        if (holdMoveAction != null)
        {
            holdMoveAction.performed -= OnHoldMoveStarted;
            holdMoveAction.canceled -= OnHoldMoveCanceled;
            holdMoveAction.Disable();
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

        if (targetingSystem != null)
        {
            targetingSystem.OnTargetConfirmed -= OnAbilityTargetConfirmed;
        }
    }

    private void Start()
    {
        lastFramePosition = transform.position;
        movementUnlockTime = Time.unscaledTime + movementLockSeconds;
        agentBaseSpeed = agent.speed;
    }

    private void Update()
    {
        UpdatePointerOverUI();

        if (IsTargetingActive())
        {
            HideDestinationIndicator();
            if (agent.hasPath) StopMovement();
        }

        if (!CanPlayerAct() && agent.hasPath)
        {
            StopMovement();
        }

        if (isHoldMoving)
        {
            if (IsTargetingActive() || !CanPlayerAct() || Time.unscaledTime < movementUnlockTime)
                isHoldMoving = false;
            else
                UpdateHoldMovement();
        }
        
        if (CombatManager.Instance != null && CombatManager.Instance.InCombat && isMoving)
        {
            TrackMovementDistance();
        }
        
        UpdateMovingState();
        UpdateDestinationIndicator();
        
        lastFramePosition = transform.position;
    }

    private void OnLightDetectableStateChanged(bool inLight)
    {
        OnLightStateChanged?.Invoke(inLight);
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        if (isPointerOverUI) return;
        if (Time.unscaledTime < movementUnlockTime) return;
        if (!CanPlayerAct()) return;
        if (IsTargetingActive()) return;
        if (mainCamera == null) return;
        if (pointerPositionAction == null) return;

        Vector2 pointerPos = pointerPositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        if (occluderMask.value != 0 && Physics.Raycast(ray, out RaycastHit occluderHit, 100f, occluderMask))
        {
            bool hasWalkableHit = Physics.Raycast(ray, out RaycastHit walkablePrecheck, 100f, walkableMask);
            if (!hasWalkableHit || occluderHit.distance <= walkablePrecheck.distance)
                return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, walkableMask))
        {
            Vector3 targetPoint = hit.point;
            Vector3 toTarget = targetPoint - transform.position;
            float requestedDistance = toTarget.magnitude;

            if (requestedDistance < minMoveDistance) return;

            bool inCombat = CombatManager.Instance != null && CombatManager.Instance.InCombat;

            if (inCombat)
            {
                if (player == null) return;

                if (!player.CanMove())
                {
                    if (debugMode) Debug.Log("[PlayerController] Cannot move - no coins or distance exhausted");
                    return;
                }

                float remainingDistance = player.RemainingMoveDistance;
                
                if (remainingDistance <= 0.01f)
                {
                    if (debugMode) Debug.Log("[PlayerController] No movement distance remaining this turn");
                    return;
                }

                if (requestedDistance > remainingDistance)
                {
                    if (debugMode) Debug.Log($"[PlayerController] Click too far! Requested: {requestedDistance:F2}, Remaining: {remainingDistance:F2}");
                    return;
                }
            }

            if (NavMesh.SamplePosition(targetPoint, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(navHit.position);
                lowVelocityTimer = 0f;
                ShowDestinationIndicator(navHit.position);
            }
        }
    }

    /// <summary>
    /// Continuously moves the player toward the cursor position each frame while the button is held.
    /// Speed and look-ahead both scale with cursor distance so the player decelerates as the cursor gets close.
    /// Occluders are intentionally ignored — hold-move reads the ground directly beneath the cursor.
    /// </summary>
    private void UpdateHoldMovement()
    {
        if (isPointerOverUI) return;
        if (mainCamera == null) return;
        if (pointerPositionAction == null) return;

        Vector2 pointerPos = pointerPositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, walkableMask))
        {
            agent.speed = agentBaseSpeed;
            return;
        }

        Vector3 targetPoint = hit.point;

        Vector3 toTarget = targetPoint - transform.position;
        toTarget.y = 0f;
        float cursorDistance = toTarget.magnitude;

        float t = Mathf.InverseLerp(holdMoveMinSpeedDistance, holdMoveMaxSpeedDistance, cursorDistance);
        agent.speed = Mathf.Lerp(agentBaseSpeed * holdMoveMinSpeedFraction, agentBaseSpeed, t);

        if (cursorDistance > 0.001f)
            targetPoint += (toTarget / cursorDistance) * (holdMoveLookAhead * t);

        bool inCombat = CombatManager.Instance != null && CombatManager.Instance.InCombat;
        if (inCombat)
        {
            if (player == null || !player.CanMove()) return;
            float remainingDistance = player.RemainingMoveDistance;
            if (remainingDistance <= 0.01f) return;
            float requestedDistance = Vector3.Distance(transform.position, targetPoint);
            if (requestedDistance > remainingDistance) return;
        }

        if (NavMesh.SamplePosition(targetPoint, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(navHit.position);
            lowVelocityTimer = 0f;
        }
    }

    private void OnHoldMoveStarted(InputAction.CallbackContext context)
    {
        if (isPointerOverUI) return;
        if (Time.unscaledTime < movementUnlockTime) return;
        if (!CanPlayerAct()) return;
        if (IsTargetingActive()) return;

        isHoldMoving = true;
        agent.speed = agentBaseSpeed;
        HideDestinationIndicator();
        agent.isStopped = true;
        agent.ResetPath();
    }

    private void OnHoldMoveCanceled(InputAction.CallbackContext context)
    {
        if (!isHoldMoving) return;
        isHoldMoving = false;
        agent.speed = agentBaseSpeed;
        StopMovement();
    }

    private void TrackMovementDistance()
    {
        if (player == null) return;
        if (CombatManager.Instance == null || !CombatManager.Instance.InCombat) return;

        Vector3 currentPosition = transform.position;
        float deltaDistance = Vector3.Distance(currentPosition, lastFramePosition);

        if (deltaDistance > 0.001f)
        {
            float newTotal = player.DistanceMovedThisTurn + deltaDistance;
            
            if (newTotal > player.MaxCombatMoveDistance)
            {
                Debug.Log($"[PlayerController] Movement limit reached! Stopping at {player.DistanceMovedThisTurn:F2} units");
                StopMovement();
                return;
            }

            player.AddMovementDistance(deltaDistance);
        }
    }

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
    }

    public void ForceStopMovement() => StopMovement();

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

    // Immediately cancels any pending movement when a targeting confirm fires,
    // covering the case where the Move and ConfirmTarget actions share the same button.
    private void OnAbilityTargetConfirmed(TargetingResult result)
    {
        StopMovement();
    }

    // Returns true while targeting is active OR on the frame a target was just confirmed,
    // preventing a same-frame movement click from slipping through.
    private bool IsTargetingActive()
    {
        return targetingSystem != null && (targetingSystem.IsTargeting || targetingSystem.TargetJustConfirmed);
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
        if (!agent.hasPath) return true;
        if (agent.remainingDistance <= arrivalThreshold) return true;

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

    private void UpdatePointerOverUI()
    {
        if (EventSystem.current == null)
        {
            isPointerOverUI = false;
            return;
        }

        if (Mouse.current != null)
        {
            isPointerOverUI = EventSystem.current.IsPointerOverGameObject();
            return;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            isPointerOverUI = EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue());
            return;
        }

        isPointerOverUI = false;
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
                TutorialManager.Instance?.Trigger("first_move");
            }
        }
    }

    private void OnDestroy()
    {
        if (lightDetectable != null)
            lightDetectable.OnLightStateChanged -= OnLightDetectableStateChanged;

        if (destinationIndicatorInstance != null)
        {
            Destroy(destinationIndicatorInstance);
        }
    }

    private void OnDrawGizmosSelected()
    {
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

        if (Application.isPlaying && CombatManager.Instance != null && CombatManager.Instance.InCombat && player != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, player.RemainingMoveDistance);
        }
    }
}