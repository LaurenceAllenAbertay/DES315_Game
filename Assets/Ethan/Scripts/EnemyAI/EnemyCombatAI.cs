using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;


//Enemy type definitions//
//Each type has fixed base stats set here; can be tweaked in the inspector per enemy//
public enum EnemyType
{
    Grunt, //Fast, low health, low damage - currently spames basic hits//
    Brute //Slow, high health, high damage - one heavy attack per turn?//
}

//Simplified enemy combat AI - Each enemy type has one attack and fixed stats//
//Replaces the old descion tree, Alert/Vision/Roam logic in EnemyAI is unchanged -EM//

[RequireComponent(typeof(Enemy))]
public class EnemyCombatAI : MonoBehaviour
{
    [Header("Enemy Type")]
    [Tooltip("Determine this enemy's stats and attack style")]
    public EnemyType enemyType = EnemyType.Grunt;

    //Per type stat overrides//
    [Header("Grunt Settings (only used if type = Grunt)")]
    [Tooltip("Max Health for a Grunt")]
    public float gruntMaxHealth = 40f;
    [Tooltip("Damage dealt per succesful Grunt Attack")]
    public float gruntAttackDamage = 8f;
    [Tooltip("Starting coin-flip chancefor a Grunt (0-100)")]
    [Range(0f, 100f)]
    public float gruntFlipChance = 65f;

    [Header("Grunt Range Settings")]
    [Tooltip("Minimum distance the Grunt tries to keep from the player")]
    public float gruntMinRange = 4f;
    [Tooltip("Maximum distance the Grunt can attack from")]
    public float gruntMaxRange = 7f;
    [Tooltip("How close the player must get before the Grunt actively retreats")]
    public float gruntRetreatRange = 3f;

    [Header("Grunt Shadow Settings")]
    [Tooltip("Flat reduction to flip chance when the Grunt attacks from light instad of shadow")]
    [Range(0f, 50f)]
    public float gruntLightPenalty = 20f;

    [Header("Brute Settings (only used if type = Brute")]
    [Tooltip("Max Health for a Brute")]
    public float bruteMaxHealth = 120f;
    [Tooltip("Damage dealt per succesful Brute Attack")]
    public float bruteAttackDamage = 22f;
    [Tooltip("Attack range for a Brute")]
    public float bruteAttackRange = 2.5f;
    [Tooltip("Starting coin-flip chancefor a Brute (0-100)")]
    [Range(0f, 100f)]
    public float bruteFlipChance = 45f;

    [Header("Timing")]
    [Tooltip("Pause before acting - gives the player a moment to read the situation")]
    public float thinkTime = 0.6f;
    [Tooltip("Short pause after acting before handing control back")]
    public float postActionDelay = 0.5f;

    [Header("Combat Movement")]
    [Tooltip("Max time spent moving into range before acting anyway")]
    public float maxMoveTime = 2.5f;
    [Tooltip("Tolerance added to range check so the enemy doesnt need to be pixel perfect")]
    public float rangeTolerance = 0.3f;

    [Header("Coin Flip Streak")]
    [Tooltip("How much the flip chance rises after a miss (same mechanic as the player)")]
    public float failBonus = 10f;
    [Tooltip("How much the flip chance falls after a hit")]
    public float successPenalty = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool lastFlipResult = false;
    [SerializeField] private float currentFlipChance = 60f;

    private const float MIN_FLIP = 5f;
    private const float MAX_FLIP = 95f;

    private Enemy enemy;
    private NavMeshAgent agent;
    private bool isTakingTurn = false;
    private float originalStoppingDistance;
    private Brute_AnimController animController;

    //Lifecycle -EM//

    private void Awake()
    {
       enemy = GetComponent<Enemy>();
       agent = GetComponent<NavMeshAgent>();
        animController = GetComponentInChildren<Brute_AnimController>();
        if (agent != null)
       {
           originalStoppingDistance = agent.stoppingDistance;
       }
    }

    private void Start()
    {
        ApplyTypeStats();
    }

    //Apply the fixed stats for this enemy's type to the enemy component//
    //SetMaxHealth(newMax, flase) so we can manually reset current health to full//
    private void ApplyTypeStats()
    {
        switch(enemyType)
        {
            case EnemyType.Grunt:
                enemy.SetMaxHealth(gruntMaxHealth, false);
                enemy.Heal(gruntMaxHealth);
                currentFlipChance = gruntFlipChance;
                break;

            case EnemyType.Brute:
                enemy.SetMaxHealth(bruteMaxHealth, false);
                enemy.Heal(bruteMaxHealth);
                currentFlipChance = bruteFlipChance;
                break;
        }

        if(debugMode)
        {
            Debug.Log($"[EnemyCombatAI] {gameObject.name} initialised as {enemyType} " + $"(hp:{enemy.MaxHealth}, flip:{currentFlipChance:F0}%");
        }
    }

    //Public API -EM//

    //Begins this enemy's combat turn//
    //Returns false if the component can't act (CombatManager will end the turn immediately in that case as a safety net)//
    public bool TakeTurn(Player player)
    {
        if(isTakingTurn)
        {
            if (debugMode) Debug.LogWarning($"[EnemyCombatAI] {gameObject.name}: TakeTurn called while already taking a turn!");
            return false;
        }

        if (player == null || enemy == null || enemy.IsDead) return false;

        StartCoroutine(ExecuteTurn(player));
        return true;
    }

    //Turn Execution -EM//
    private IEnumerator ExecuteTurn(Player player)
    {
        isTakingTurn = true;

        //Stop any wandering so the enemy stands still on its turn//
        StopMovement();

        yield return new WaitForSeconds(thinkTime);

        //Branch to the correct behaviour for this type//
        if (enemyType == EnemyType.Grunt) yield return ExecuteGruntTurn(player);
        else yield return ExecuteBruteTurn(player);

        yield return new WaitForSeconds(postActionDelay);

        isTakingTurn = false;
        CombatManager.Instance?.EndCurrentTurn();
    }

    private IEnumerator ExecuteGruntTurn(Player player)
    {
        //Step 1: reposition into preffered range band//
        yield return RepositionGrunt(player);

        //Step 2: Check we are within attack range after repositioning//
        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
        if(distToPlayer > gruntMaxRange + rangeTolerance)
        {
            if (debugMode) Debug.LogWarning($"[EnemyCombatAI] {gameObject.name} Grunt could not reach attack range");
            MessageUI.Instance?.EnqueueMessage($"{gameObject.name} couldn't get into position!");
            yield break;
        }

        //Step 3: Check light and calculate effective flip chance//
        bool isInLight = LightDetectionManager.Instance != null && LightDetectionManager.Instance.IsPointInLight(transform.position);

        float effectiveFlipChance = currentFlipChance;
        if(isInLight)
        {
            effectiveFlipChance = Mathf.Max(MIN_FLIP, currentFlipChance - gruntLightPenalty);
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} in LIGHT - flip {currentFlipChance:F0}% -> {effectiveFlipChance:F0}%");
            MessageUI.Instance?.EnqueueMessage($"{gameObject.name} is exposed in the light!");
        }
        else
        {
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} in SHADOW - no penalty");
        }

        //Step 4: Attack//
        bool success = PerformFlip(effectiveFlipChance);
        lastFlipResult = success;

        if(success)
        {
            player.TakeDamage(gruntAttackDamage);
            MessageUI.Instance?.EnqueueMessage($"{gameObject.name} used Strike and dealth {gruntAttackDamage:0} damage!");
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} Grunt HIT for {gruntAttackDamage}");
        }
        else
        {
            MessageUI.Instance?.EnqueueMessage($"{gameObject.name} used Strike but missed!");
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} Grunt Missed");
        }
    }

    //Reposition the Grunt into its prteffered range band -EM//
    //Too close: retreat away from player. Too far: advance toward player. in band: stay put//
    private IEnumerator RepositionGrunt(Player player)
    {
        if(agent == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled) yield break;

        float dist = Vector3.Distance(transform.position, player.transform.position);

        //Already in preffered badn - no movement needed//
        if(dist >= gruntMinRange && dist <= gruntMaxRange)
        {
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} Grunt already in range band ({dist:F1})");
            yield break;
        }

        Vector3 targetPos;

        if(dist < gruntRetreatRange)
        {
            //Too close - retreat directly away from the player//
            Vector3 awayDir = (transform.position - player.transform.position).normalized;
            targetPos = transform.position + awayDir * (gruntMinRange - dist);
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} Grunt retreating ({dist:F1})");
        }
        else
        {
            //Too far - retreat directly away from the player//
            Vector3 toPlayer = (player.transform.position - transform.position).normalized;
            float midRange = gruntMinRange + (gruntMaxRange - dist);
            targetPos = transform.position - toPlayer * midRange;
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} Grunt advancing to range band (dist {dist:F1})");
        }

        //Snap target to NavMesh//
        if(NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.stoppingDistance = 0.1f;
            agent.SetDestination(hit.position);

            float timer = 0f;
            while(timer < maxMoveTime)
            {
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + rangeTolerance) break;
                timer += Time.deltaTime;
                yield return null;
            }

            agent.isStopped = true;
            agent.ResetPath();
            agent.stoppingDistance = originalStoppingDistance;
        }
    }

    //Brute turn: close in and slam -EM//
    private IEnumerator ExecuteBruteTurn(Player player)
    {
        if(!IsInRange(player, bruteAttackRange)) yield return MoveIntoRange(player, bruteAttackRange);

        if(!IsInRange(player, bruteAttackRange))
        {
            if (debugMode) Debug.LogWarning($"[EnemyCombatAI] {gameObject.name} Brute could not reach the player.");
            MessageUI.Instance?.EnqueueMessage($"{gameObject.name} couldn't reach you!");
            yield break;
        }

        bool success = PerformFlip(currentFlipChance);
        lastFlipResult = success;
        animController?.TriggerAttack();

        if (success)
        {
            player.TakeDamage(bruteAttackDamage);
            MessageUI.Instance?.EnqueueMessage($"{gameObject.name} used Slam and dealt {bruteAttackDamage:0} damage!");
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} Brute HIT for {bruteAttackDamage}");
        }
        else
        {
            MessageUI.Instance?.EnqueueMessage($"{gameObject.name} used Slam but missed!");
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} Brute MISSED");
        }
    }

    //Coin flip (mirrors player coin flip) -EM//

    private bool PerformFlip(float chance)
    {
   
        float roll = Random.Range(0f, 100f);
        float chanceUsed = chance;
        bool success = roll < chance;

        float startingChance = GetStartingFlipChance();

        if (success)
        {
            //Coming off a fail streak: rest; otherwise decrease;
            
            if (currentFlipChance > startingChance)
            {
                currentFlipChance = startingChance;
            }
            else
            {
                currentFlipChance = Mathf.Max(MIN_FLIP, currentFlipChance - successPenalty);
            }
        }
        else
        {
            if (currentFlipChance < startingChance)
            {
                currentFlipChance = startingChance;
            }
            else
            {
                currentFlipChance = Mathf.Min(MAX_FLIP, currentFlipChance + failBonus);
            }
        }
        if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} flip: roll {roll:F1} vs {chanceUsed:F0}% -> {(success ? "HIT" : "MISS")}");
        return success;
    }

    //Per type helpers -EM//

    private float GetStartingFlipChance()
    {
        return enemyType == EnemyType.Grunt ? gruntFlipChance : bruteFlipChance;
    }

    //Movement helpers -EM//

    private bool IsInRange(Player player, float range)
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.transform.position) <= range + rangeTolerance;
    }

    private IEnumerator MoveIntoRange(Player player, float range)
    {
        if (agent == null || player == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled)
        {
            yield break;
        }

        agent.isStopped = false;
        agent.stoppingDistance = Mathf.Max(0.1f, range);
        agent.SetDestination(player.transform.position);

        float timer = 0f;
        while (timer < maxMoveTime)
        {
            if (IsInRange(player, range))
            {
                break;
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        agent.isStopped = true;
        agent.ResetPath();
        agent.stoppingDistance = originalStoppingDistance;
    }

    private void StopMovement()
    {
        if (agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void OnDisable()
    {
        isTakingTurn = false;
    }
}
