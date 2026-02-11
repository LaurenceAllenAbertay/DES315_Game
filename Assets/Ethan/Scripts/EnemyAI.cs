using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Enemy))]

//State machine like Ai for basic enemy movement - EM//
//Disables itself when combat starts and re-enables when combat ends -EM//
public class EnemyAI : MonoBehaviour
{
    [Header("Roaming Settings")]
    [Tooltip("How far from current position the enemy will roam")]
    public float roamRadius = 10f;

    [Tooltip("Minimum time to wait at each desitination")]
    public float minIdleTime = 2f;

    [Tooltip("Maxiumum time to wait at each destination")]
    public float maxIdleTime = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private AIState currentState;
    [SerializeField] private Vector3 currentDestination;
    [SerializeField] private float idleTimer;
    [SerializeField] private bool isInitialized = false;

    private NavMeshAgent agent;
    private Enemy enemy;
    private Vector3 combatStartPosition;
    private Quaternion combatStartRotation;
    private bool hasCombatStartTransform;

    //Can add more when combat is defined//
    private enum AIState
    {
        Idle,
        Roaming,
        Returning
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemy = GetComponent<Enemy>();
    }

    
    private void Start()
    {
        //Subsribe to combat events to pause/resume wandering//
        if(CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCombatStarted += HandleCombatStarted;
            CombatManager.Instance.OnCombatEnded += HandleCombatEnded;
        }

        //Wait for NavMeshAgent to be properly initialised//
        if (!ValidateAgent())
        {
            if (debugMode) Debug.LogWarning($"{gameObject.name}: NavMeshAgent not on NavMesh! Retrying initialization...");
            Invoke(nameof(InitializeAI), 0.1f);
            return;
        }

        InitializeAI();
    }

    private void OnDestroy()
    {
        if(CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCombatStarted -= HandleCombatStarted;
            CombatManager.Instance.OnCombatEnded -= HandleCombatEnded;
        }
    }

    //Combat callbacks disable enable this component -EM//

    private void HandleCombatStarted(System.Collections.Generic.List<Enemy> enemies)
    {
        //Disable wandering for all enemies in the scene when any combat begins//
        //scoped room based wandering can be added when a room detector is built//
        if(!enabled) return;

        if (enemies != null && enemies.Contains(enemy))
        {
            combatStartPosition = transform.position;
            combatStartRotation = transform.rotation;
            hasCombatStartTransform = true;
        }
        else
        {
            hasCombatStartTransform = false;
        }

        if (debugMode) Debug.Log($"[EnemyAI] {gameObject.name}: Combat started - disabling wandering AI");

        //Stop in place before disabling//
        if(ValidateAgent())
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        enabled = false;
    }

    private void HandleCombatEnded(CombatManager.CombatOutcome outcome)
    {
        //Re-enable wandering when combat is over (regardless of outcome)//
        if (enabled) return;

        if (debugMode) Debug.Log($"[EnemyAI] {gameObject.name}: combat ended - re-enabling wandering AI");

        enabled = true;

        if (hasCombatStartTransform)
        {
            if (ValidateAgent())
            {
                StartReturning();
                return;
            }

            SnapToCombatStartTransform();
        }

        //Resume from idle so the enemy doesn't immediately charge ooff//
        if (isInitialized && ValidateAgent())
        {
            StartIdling();
        }
    }

    //Initialisation -EM//

    private void InitializeAI()
    {
        if(!ValidateAgent())
        {
            if (debugMode) Debug.LogError($"{gameObject.name}: Cannot initialize AI - NavMehsAgent is not on a valid NavMesh!");
            enabled = false;
            return;
        }

        currentState = AIState.Idle;
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
        isInitialized = true;

        if (debugMode) Debug.Log($"{gameObject.name}: AI Initialized successfully");
    }

    private bool ValidateAgent()
    {
        if (agent == null) return false;
        if (!agent.isOnNavMesh) return false;
        if (!agent.enabled) return false;
        return true;
    }

    //Update Loop -EM//

    private void Update()
    {
        if (!isInitialized) return;

        switch (currentState)
        {
            case AIState.Idle:
                HandleIdleState();
                break;
            case AIState.Roaming:
                HandleRoamingState();
                break;
            case AIState.Returning:
                HandleReturningState();
                break;
        }
    }

    //State handlers -EM//

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

    private void HandleReturningState()
    {
        if (!ValidateAgent()) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                FinishReturning();
            }
        }
    }

    //State transitions -EM//
    private void StartRoaming()
    {
        if (!ValidateAgent()) return;

        Vector3 randomDestination = GetRandomNavMeshPoint();

        if(randomDestination != Vector3.zero)
        {
            agent.SetDestination(randomDestination);
            currentDestination = randomDestination;
            currentState = AIState.Roaming;

            if (debugMode) Debug.Log($"{gameObject.name} roaming to {randomDestination}");
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

        if (debugMode) Debug.Log($"{gameObject.name} idling for {idleTimer:F1} seconds");
    }

    private void StartReturning()
    {
        agent.isStopped = false;
        agent.ResetPath();
        agent.SetDestination(combatStartPosition);
        currentDestination = combatStartPosition;
        currentState = AIState.Returning;
    }

    private void SnapToCombatStartTransform()
    {
        transform.position = combatStartPosition;
        transform.rotation = combatStartRotation;
        currentDestination = Vector3.zero;
        hasCombatStartTransform = false;
    }

    private void FinishReturning()
    {
        transform.rotation = combatStartRotation;
        currentDestination = Vector3.zero;
        hasCombatStartTransform = false;
        StartIdling();
    }

    //Utility -EM//
    private Vector3 GetRandomNavMeshPoint()
    {
        //Try to find a random point on the NavMesh within roam radius//
        for (int i = 0; i < 30; i++) //Try up to 30x//
        {
            Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, roamRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        if (debugMode) Debug.LogWarning($"{gameObject.name} couldn't find valid NavMesh point within {roamRadius} units");
        return Vector3.zero;
    }

    //Gizmos -EM//

    private void OnDrawGizmosSelected()
    {
        //Visualise roam radius//
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, roamRadius);

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
