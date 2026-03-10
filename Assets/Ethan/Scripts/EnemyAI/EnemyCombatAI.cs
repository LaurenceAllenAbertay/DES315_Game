using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("Attack range for a Grunt")]
    public float gruntAttackRange = 2f;
    [Tooltip("Starting coin-flip chancefor a Grunt (0-100)")]
    [Range(0f, 100f)]
    public float gruntFlipChance = 65f;

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

    //Lifecycle -EM//

    private void Awake()
    {
       enemy = GetComponent<Enemy>();
       agent = GetComponent<NavMeshAgent>();
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

        //Get the stats for this type//
        float damage = GetAttackDamage();
        float range = GetAttackRange();
        string attackName = enemyType == EnemyType.Grunt ? "Strike" : "Slam";

        //Move into range if needed//
        if(!IsInRange(player, range))
        {
            yield return MoveIntoRange(player, range);
        }

        //Check range once more after movement//
        if(!IsInRange(player, range))
        {
            if (debugMode) Debug.LogWarning($"[EnemyCombatAI] {gameObject.name} could not reach the player");
            MessageUI.Instance?.EnqueueMessage($"{gameObject.name} couldn't reach you!");
        }
        else
        {
            //Coin flip//
            bool success = PerformFlip();
            lastFlipResult = success;

            if(success)
            {
                player.TakeDamage(damage);
                MessageUI.Instance?.EnqueueMessage($"{gameObject.name} used {attackName} and dealt {damage:0} damage!");
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} HIT for {damage}");
            }
            else
            {
                MessageUI.Instance?.EnqueueMessage($"{gameObject.name} used {attackName} but missed!");
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} Missed");

            }
        }

        yield return new WaitForSeconds(postActionDelay);

        isTakingTurn = false;
        CombatManager.Instance?.EndCurrentTurn();
    }


    //Coin flip (mirrors player coin flip) -EM//

    private bool PerformFlip()
    {
   
        float roll = Random.Range(0f, 100f);
        float chanceUsed = currentFlipChance;
        bool success = roll < currentFlipChance;

        if(success)
        {
            //Coming off a fail streak: rest; otherwise decrease;
            float startingChance = GetStartingFlipChance();
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
            float startingChance = GetStartingFlipChance();
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
    private float GetAttackDamage()
    {
        return enemyType == EnemyType.Grunt ? gruntAttackDamage : bruteAttackDamage;
    }

    private float GetAttackRange()
    {
        return enemyType == EnemyType.Grunt ? gruntAttackRange : bruteAttackRange;
    }

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
}
