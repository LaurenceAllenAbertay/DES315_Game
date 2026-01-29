using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.Android;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Enemy))]

//State machine like Ai for basic enemy movement to be scaled when combat side is ready - EM//
public class EnemyAI : MonoBehaviour
{
    [Header("Roaming Settings")]
    [Tooltip("How far from current position the enemy will roam")]
    public float roamRadius = 10f;

    [Tooltip("Minimum time to wait at each desitination")]
    public float minIdleTime = 2f;

    [Tooltip("Maxiumum time to wait at each destination")]
    public float maxIdleTime = 5f;

    [Header("Detection Settings")]
    [Tooltip("How far from current position the enemy will roam")]
    public float detectionRange = 15f;

    [Tooltip("Field of view angle in degress (360 = See all around)")]
    [Range(0f, 360f)]
    public float fieldOfViewAngle = 120f;

    [Tooltip("Layer mask for line of sight obstacles")]
    public LayerMask obstacleMask = ~0;

    [Tooltip("How often to check for the player (in seconds)")]
    public float detectionInterval = 0.5f;

    [Header("Chase Settings")]
    [Tooltip("How close to get to the player when chasing")]
    public float chaseStoppingDistance = 2f;

    [Tooltip("if player gets this far away, stop chasing")]
    public float maxChaseDistance = 25f;


    [Header("Debug")]
    [SerializeField] private AIState currentState;
    [SerializeField] private Vector3 currentDestination;
    [SerializeField] private float idleTimer;
    [SerializeField] private bool playerDetected;
    [SerializeField] private bool isInitialized = false;

    private NavMeshAgent agent;
    private Enemy enemy;
    private Transform playerTransform;
    private float detectionTimer;
    private float originalStoppingDistance;

    //Can add more when combat is defined//
    private enum AIState
    {
        Idle,
        Roaming,
        Chasing
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemy = GetComponent<Enemy>();
    }

    
    private void Start()
    {
        //Wait for NavMeshAgent to be properly initialised//
        if (!ValidateAgent())
        {
            Debug.LogWarning($"{gameObject.name}: NavMeshAgent not on NavMesh! Retrying initialization...");
            Invoke(nameof(InitializeAI), 0.1f);
            return;
        }

        InitializeAI();
    }

    private void InitializeAI()
    {
        if(!ValidateAgent())
        {
            Debug.LogError($"{gameObject.name}: Cannot initialize AI - NavMehsAgent is not on a valid NavMesh!");
            enabled = false;
            return;
        }

        //Find the player//
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No PlayerController found in Scene!");
        }

        originalStoppingDistance = agent.stoppingDistance;
        currentState = AIState.Idle;
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
        detectionTimer = detectionInterval;
        isInitialized = true;

        Debug.Log($"{gameObject.name}: AI Initialized successfully");
    }

    private bool ValidateAgent()
    {
        if (agent == null) return false;
        if (!agent.isOnNavMesh) return false;
        if (!agent.enabled) return false;
        return true;
    }


private void Update()
    {
        if (!isInitialized) return;

        //Periodically check for player detection//
        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0)
        {
            CheckForPlayer();
            detectionTimer = detectionInterval;
        }
        switch (currentState)
        {
            case AIState.Idle:
                HandleIdleState();
                break;
            case AIState.Roaming:
                HandleRoamingState();
                break;
            case AIState.Chasing:
                HandleChasingState();
                break;
        }
    }

    private void HandleIdleState()
    {
        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
        {
            StartRoaming();
        }
    }

    private void HandleRoamingState()
    {
        if (!ValidateAgent()) return;

        //Check if we've reached our destination//
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if(!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                StartIdling();
            }
        }
    }

    private void HandleChasingState()
    {
        if (!ValidateAgent()) return;

        if (playerTransform == null)
        {
            StartIdling();
            return;
        }

        //Update destination to player's current position//
        agent.SetDestination(playerTransform.position);

        //Check if player is too far away//
        float distanceToPlayer =  Vector3.Distance(transform.position, playerTransform.position);
        if(distanceToPlayer > maxChaseDistance)
        {
            Debug.Log($"{gameObject.name} lost player - too far away");
            StartIdling();
        }
    }

    private void StartRoaming()
    {
        if (!ValidateAgent()) return;

        Vector3 randomDestination = GetRandomNavMeshPoint();

        if(randomDestination != Vector3.zero)
        {
            agent.SetDestination(randomDestination);
            currentDestination = randomDestination;
            currentState = AIState.Roaming;

            Debug.Log($"{gameObject.name} roaming to {randomDestination}");
        }
        else
        {
            //If we couldn't find a valid point try again after a short wait//
            idleTimer = 1f;
        }
    }

    private void StartIdling()
    {
        if (!ValidateAgent()) return;

        currentState = AIState.Idle;
        idleTimer = Random.Range(minIdleTime,maxIdleTime);

        Debug.Log($"{gameObject.name} idling for {idleTimer:F1} seconds");
    }

    private void CheckForPlayer()
    {
        //If already chasing, we're already tracking the player//
        if(currentState == AIState.Chasing)
        {
            return;
        }

        if(playerTransform == null)
        {
            playerDetected = false;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        //Is player in range//
        if(distanceToPlayer > detectionRange)
        {
            playerDetected = false;
            return;
        }

        //is player in field of view//
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if(angleToPlayer > fieldOfViewAngle / 2f)
        {
            playerDetected = false;
            return;
        }

        //Can we see the player 9line of sight)//
        Ray ray = new Ray(transform.position + Vector3.up, directionToPlayer);
        if(Physics.Raycast(ray, distanceToPlayer, obstacleMask))
        {
            playerDetected = false;
            return;
        }

        //Player detected//
        playerDetected = true;
        StartChasing();
    }

    private void StartChasing()
    {
        if (!ValidateAgent()) return;
        if (currentState == AIState.Chasing) return;

        currentState = AIState.Chasing;
        agent.stoppingDistance = chaseStoppingDistance;
        Debug.Log($"{gameObject.name} detected player - starting chase!");
    }

    [ContextMenu("Force Stop Chasing")]
    public void ForceStopChasing()
    {
        if(currentState == AIState.Chasing)
        {
            Debug.Log($"{gameObject.name} forced to stop chasing - returning to idle");
            playerDetected = false;
            StartIdling();
        }
        else
        {
            Debug.Log($"{gameObject.name} is not currently chasing");
        }
    }

    private Vector3 GetRandomNavMeshPoint()
    {
        //Try to find a random point on the NavMesh within roam radius//
        for(int i = 0; i < 30; i++) //Try up to 30x//
        {
            Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            if(NavMesh.SamplePosition(randomDirection, out hit, roamRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        Debug.LogWarning($"{gameObject.name} couldn't find valid NavMesh point within {roamRadius} units");
        return Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        //Visualise roam radius//
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, roamRadius);

        //Visualise detection range//
        Gizmos.color = playerDetected ? Color.red : new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        //Visualise field of view//
        if(fieldOfViewAngle < 360f)
        {
            Vector3 forward = transform.forward * detectionRange;
            Vector3 rightBoundary = Quaternion.Euler(0, fieldOfViewAngle / 2f, 0) * forward;
            Vector3 leftBoundary = Quaternion.Euler(0, -fieldOfViewAngle / 2f, 0) * forward;

            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
            Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        }

        //Draw line to player if detected//
        if(playerDetected && playerTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up, playerTransform.position = Vector3.up);
        }

        //Draw current destination//
        if (currentState == AIState.Roaming && currentDestination != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentDestination);
            Gizmos.DrawWireSphere(currentDestination, 0.5f);
        }

        //Draw Path if available//
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if(agent != null && agent.hasPath)
        {
            Gizmos.color = Color.yellow;
            Vector3[] corners = agent.path.corners;
            for(int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }
    }
}
