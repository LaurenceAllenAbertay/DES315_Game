using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Video;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Enemy))]

//State machine like Ai for basic enemy movement - EM//
//Disables itself when combat starts and re-enables when combat ends -EM//
public class EnemyAI : MonoBehaviour
{
    [Header("Roaming Settings")]
    [Tooltip("How far from current position the enemy will roam")]
    public float roamRadius = 10f;
    private float roamSettleTImer = 0f;
    private const float ROAM_SETTLE_TIME = 0.3f;

    [Tooltip("Minimum time to wait at each desitination")]
    public float minIdleTime = 2f;

    [Tooltip("Maxiumum time to wait at each destination")]
    public float maxIdleTime = 5f;

    [Header("Alert Settings")]
    [Tooltip("Roam radius while alert - smaller so enemy lingers nearby")]
    public float alertRoamRadius = 4f;

    [Tooltip("Max idle time while alert - shorter so enemy keeps moving")]
    public float alertMaxIdleTime = 1.5f;

    [Header("Chase Settings")]
    [Tooltip("How close the enemy needs to get to the player to trigger combat")]
    public float combatEngageRange = 1.5f;

    [Tooltip("How long the enemy chases before giving up if it loses player")]
    public float chaseTimeout = 6f;


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
    private EnemyVisionCone visionCone;
    private Transform playerTransform;
    private float chaseTimer;

    //Can add more when combat is defined//
    private enum AIState
    {
        Idle,
        Roaming,
        Returning,
        Alert,
        Chasing
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemy = GetComponent<Enemy>();
        visionCone = GetComponent<EnemyVisionCone>();
    }

    
    private void Start()
    {
        if(CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCombatStarted += HandleCombatStarted;
            CombatManager.Instance.OnCombatEnded += HandleCombatEnded;
        }

        if (visionCone != null)
            visionCone.OnPlayerDetected += HandlePlayerSpotted;
        else if (debugMode)
            Debug.LogWarning($"{gameObject.name}: No EnemyVisionCone found in children!");

        StartCoroutine(InitializeAfterNavMesh());
    }

    /// <summary>
    /// Waits one fixed update so the physics system can place the NavMeshAgent
    /// onto the freshly-baked mesh before we attempt initialization.
    /// </summary>
    private System.Collections.IEnumerator InitializeAfterNavMesh()
    {
        yield return new WaitForFixedUpdate();
        InitializeAI();
    }

    private void OnDestroy()
    {
        if(CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCombatStarted -= HandleCombatStarted;
            CombatManager.Instance.OnCombatEnded -= HandleCombatEnded;
        }
        if (visionCone != null)
        {
            visionCone.OnPlayerDetected -= HandlePlayerSpotted;
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

        //Un-stopped the agent so th enemy can move//
        if(ValidateAgent())
        {
            agent.isStopped = false;
        }

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
        if (!ValidateAgent())
        {
            if (debugMode) Debug.LogError($"{gameObject.name}: Cannot initialize - not on NavMesh!");
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                if (debugMode) Debug.Log($"{gameObject.name}: Warped to NavMesh at {hit.position}");
            }
            else
            {
                if (debugMode) Debug.LogWarning($"{gameObject.name}: No NavMesh found - disabling AI");
                enabled = false;
                return;
            }
        }
        else
        {
            if (debugMode) Debug.Log($"{gameObject.name}: Already on NavMesh at {transform.position}"); //-EM//
        }

        agent.isStopped = false;
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
            case AIState.Alert:
                HandleAlertState();
                break;
            case AIState.Chasing:
                HandleChasingState();
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

    //private void HandleRoamingState()
    //{
    //    if (!ValidateAgent()) return;

    //    //Give the agent a moment to start moving before checking arrival//
    //    roamSettleTImer += Time.deltaTime;
    //    if (roamSettleTImer < ROAM_SETTLE_TIME) return;


    //    //Check if we've reached our destination//
    //    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
    //    {
    //        if(!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
    //        {
    //            StartIdling();
    //        }
    //    }
    //}

    private void HandleRoamingState()
    {
        if (!ValidateAgent()) return;

        roamSettleTImer += Time.deltaTime;
        if (roamSettleTImer < ROAM_SETTLE_TIME) return;

        float distToDest = Vector3.Distance(transform.position, currentDestination);

        if (distToDest <= 1.0f)
        {
            StartIdling();
            return;
        }

        //Stuck detection -EM//
        if (agent.velocity.sqrMagnitude < 0.05f && roamSettleTImer > 2.0f)
        {
            if (debugMode) Debug.LogWarning($"{gameObject.name}: Stuck, warping to NavMesh and re-idling");

            //Warp back to nearest valid NavMesh point -EM//
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            agent.ResetPath();
            StartIdling();

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

    private void HandleAlertState()
    {
        //Behaves like roaming but with tighter radius and shorter idles//
        if (!ValidateAgent()) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                StartIdling();
            }
        }
    }
    private void HandleChasingState()
    {
        if (!ValidateAgent()) return;
        if (playerTransform == null) return;

        //Keep updating destination toward player//
        agent.SetDestination(playerTransform.position);

        //Close enough to engage combat//
        if (Vector3.Distance(transform.position, playerTransform.position) <= combatEngageRange)
        {
            if (CheatManager.Instance != null && CheatManager.Instance.IgnoreEnemies)
            {
                SetAlerted();
                return;
            }
            if (debugMode) Debug.Log($"{gameObject.name}: Engaging combat!");
            CombatManager.Instance?.StartCombatFromEnemy(enemy);
            return;
        }

        //Chase timeout - give up if taking too long//
        chaseTimer -= Time.deltaTime;
        if (chaseTimer <= 0f)
        {
            if (debugMode) Debug.Log($"{gameObject.name}: Lost the player, returning to alert.");
            SetAlerted(); //Drop back to alert rather than fully resetting//
        }
    }

    //State transitions -EM//
    private void StartRoaming()
    {
        if (!ValidateAgent()) return;

        roamSettleTImer = 0f;

        agent.isStopped = false;

        //use tighter radius when alert//
        float radius = (currentState == AIState.Alert) ? alertRoamRadius : roamRadius;
        Vector3 randomDestination = GetRandomNavMeshPoint();

        if(randomDestination != Vector3.zero)
        {
            agent.SetDestination(randomDestination);
            currentDestination = randomDestination;
            currentState = (currentState == AIState.Alert) ? AIState.Alert : AIState.Roaming;

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

        //Shorter idle when alert so enemy keeps looking around//
        float maxIdle = (currentState == AIState.Alert) ? alertMaxIdleTime : maxIdleTime;
        idleTimer = Random.Range(minIdleTime, maxIdle);
        currentState = (currentState == AIState.Chasing) ? AIState.Idle : currentState;

        //Preserve alert state through idle//
        if(currentState != AIState.Alert)
        {
            currentState = AIState.Idle;
        }

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

    //Called by RoomManager when the player enters this enemy's room//
    public void SetAlerted()
    {
        if (currentState == AIState.Chasing) return; //Don't downgrade a chasing enemy//
        if (!isInitialized || !ValidateAgent()) return;

        currentState = AIState.Alert;
        //Pick a nearby wander point iwth the tigher alert radius//
        Vector3 alertDestination = GetRandomNavMeshPointWithRadius(alertRoamRadius);
        if(alertDestination != Vector3.zero)
        {
            agent.SetDestination(alertDestination);
        }

        if (debugMode) Debug.Log($"{gameObject.name}: Alerted - player is in room.");
    }

    //Called by EnemyVisionCone when this specific enemy spots the player//
    private void HandlePlayerSpotted()
    {
        if(currentState == AIState.Chasing) return;
        if (CombatManager.Instance != null && CombatManager.Instance.InCombat) return;
        if (CheatManager.Instance != null && CheatManager.Instance.IgnoreEnemies) return;

        Player p = FindAnyObjectByType<Player>();
        if (p != null) playerTransform = p.transform;

        currentState = AIState.Chasing;
        chaseTimer = chaseTimeout;
        agent.isStopped = false;

        if (debugMode) Debug.Log($"{gameObject.name}: Spotted player - chasing!");
    }

    //Utility -EM//

    private Vector3 GetRandomNavMeshPoint()
    {
        return GetRandomNavMeshPointWithRadius(roamRadius);
    }
    private Vector3 GetRandomNavMeshPointWithRadius(float radius)
    {
        //Find which room this enemy is in -EM//
        RoomLA myRoom = null;
        foreach (var room in FindObjectsByType<RoomLA>(FindObjectsSortMode.None))
        {
            if (room.Contains(transform.position))
            {
                myRoom = room;
                break;
            }
        }

        if (myRoom == null)
        {
            if (debugMode) Debug.LogWarning($"{gameObject.name}: Not inside any RoomLA bounds!");
            return Vector3.zero;
        }

        //Get the room's collider bounds for XZ clamping -EM//
        Bounds roomBounds = new Bounds();
        foreach (Collider col in myRoom.BoundaryColliders)
        {
            if (col == null) continue;
            roomBounds.Encapsulate(col.bounds);
        }

        for (int i = 0; i < 30; i++)
        {
            //Pick random point within room bounds at floor height -EM//
            float randX = Random.Range(roomBounds.min.x, roomBounds.max.x);
            float randZ = Random.Range(roomBounds.min.z, roomBounds.max.z);
            Vector3 candidate = new Vector3(randX, transform.position.y, randZ);

            if (!myRoom.Contains(candidate)) continue;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        if (debugMode) Debug.LogWarning($"{gameObject.name}: Could not find valid point within room bounds");
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