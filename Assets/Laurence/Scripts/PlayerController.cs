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

    [Header("Current State (Read Only)")]
    [SerializeField] private bool isInLight;
    [SerializeField] private float currentLightLevel;
    [SerializeField] private bool isMoving;

    private NavMeshAgent agent;
    private Camera mainCamera;

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
    }

    private void Start()
    {
        UpdateLightState();
    }

    private void Update()
    {
        HandleClickInput();
        UpdateMovingState();
        UpdateLightState();
    }

    private void HandleClickInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, walkableMask))
            {
                agent.SetDestination(hit.point);
            }
        }
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