using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


//Handles all enemy combat behaviour via a decision tree -EM//
//Add this and Enemy.cs on every enemy that needs combat AI//
//Descision tree priority each turn (highest first)//
//1. Heal if health % <= healThreshold AND a Heal action exists//
//2. Defend if health % <= defendThreshold AND a Defend action exists//
//3. SpecialAttack if health % >= specialHealthMin (enemy is confident)//
//4. Attack always available as a fallback//
//Each chosen action runs the same coin-flip streak as the player//
//Success reduces flip chance, failure increases it, streaks reset to base//

[RequireComponent(typeof(Enemy))]
public class EnemyCombatAI : MonoBehaviour
{
    [Header("Combat Actions")]
    [Tooltip("Assign EnemyCombatAction ScriptableObjects here. Include at least one Attack")]
    public List<EnemyCombatAction> availableActions = new List<EnemyCombatAction>();

    [Header("Decision Threshold")]
    [Tooltip("Use heal when health % is at or below this (0-1)")]
    [Range(0f, 1f)]
    public float healThreshold = 0.4f;

    [Tooltip("Use Defend when health % is at or below this (0-1)")]
    [Range(0f, 1f)]
    public float defendThreshold = 0.6f;

    [Tooltip("Use SpecialAttack only when health % is at or above this (0-1)")]
    [Range(0f, 1f)]
    public float specialHealthMin = 0.5f;

    [Header("Timing")]
    [Tooltip("Pause before acting - gives the player a moment to read the situation")]
    public float thinkTime = 0.6f;

    [Tooltip("Short pause after acting before handing control back")]
    public float postActionDelay = 0.5f;

    [Header("Combat Movement")]
    [Tooltip("Max time spent moving into range for an action (seconds)")]
    public float maxMoveTime = 2.5f;

    [Tooltip("How close to the action range we accept before acting")]
    public float rangeTolerance = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private string lastChosenAction = "None";
    [SerializeField] private bool lastFlipresult = false;
    [SerializeField] private float lastFlipChance = 0f;

    //Per-action flip chance, keyed by action name, persists across turns//
    private Dictionary<string, float> actionFlipChances = new Dictionary<string, float>();

    private Enemy enemy;
    private NavMeshAgent agent;
    private bool isTakingTurn = false;
    private float originalStoppingDistance = 0f;

    private const float MIN_FLIP_CHANCE = 0f;
    private const float MAX_FLIP_CHANCE = 100f;

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
        InitialiseFlipChances();
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

        //Run the descision tree//
        EnemyCombatAction chosen = ChooseAction();

        if(chosen != null)
        {
            lastChosenAction = chosen.actionName;
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} chose: {chosen.actionName}");

            if (RequiresTarget(chosen) && !IsTargetInRange(chosen, player))
            {
                yield return MoveIntoRange(chosen, player);
            }

            if (RequiresTarget(chosen) && !IsTargetInRange(chosen, player))
            {
                if (debugMode) Debug.LogWarning($"[EnemyCombatAI] {gameObject.name} could not reach range for {chosen.actionName}");
            }
            else
            {
            bool flipSuccess = PerformFlip(chosen);
            lastFlipresult = flipSuccess;

            if(flipSuccess)
            {
                if(debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} FLIP SUCCESS - executing {chosen.actionName}");
                ExecuteAction(chosen, player);
            }
            else
            {
                if(debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} FLIP MISS - {chosen.actionName} failed");
                MessageUI.Instance?.EnqueueMessage($"{gameObject.name} missed {chosen.actionName}.");
            }
            }
        }
        else
        {
            lastChosenAction = "No valid action";
            if (debugMode) Debug.LogWarning($"[EnemyCombatAI] {gameObject.name}: no valid action foind - ending turn");
        }

        yield return new WaitForSeconds(postActionDelay);

        isTakingTurn = false;

        //Hand control back to CombatManager//
        CombatManager.Instance?.EndCurrentTurn();
    }

    //Decision tree -EM//
    
    //Evaluates the decision tree and returns the best action for this turn//
    //Priority: Heal > defend > SpecialAttack > Attack//
    private EnemyCombatAction ChooseAction()
    {
        float healthPercent = (enemy.MaxHealth > 0f) ? enemy.CurrentHealth / enemy.MaxHealth : 1f;

        //1. Heal only when hurting//
        if(healthPercent <= healThreshold)
        {
            EnemyCombatAction heal = FindBestActionOfType(EnemyActionType.Heal, healthPercent);
            if(heal != null)
            {
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} descision: HEAL (hp {healthPercent:P0})");
                return heal;
            }
        }

        //2. Defend - When moderatley hurt//
        if(healthPercent <= defendThreshold)
        {
            EnemyCombatAction defend = FindBestActionOfType(EnemyActionType.Defend, healthPercent);
            if(defend != null)
            {
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} decision: DEFEND (hp {healthPercent:P0})");
                return defend;
            }
        }

        //3. Special attack - only when healthy enough to be aggressive//
        if(healthPercent >= specialHealthMin)
        {
            EnemyCombatAction special = FindBestActionOfType(EnemyActionType.SpecialAttack, healthPercent);
            if(special != null)
            {
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} decision: SPECIAL ATTACK");
                return special;
            }
        }

        //4. Fallback - basic attack//
        EnemyCombatAction attack = FindBestActionOfType(EnemyActionType.Attack, healthPercent);
        if(attack != null)
        {
            if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} decision: ATTACK");
            return attack;
        }

        return null;
    }

    //Returns the highest-weoght valid action of the requested type -EM//
    //If multiple actions tie on weight, one is chosen at random -EM//
    //Respect healthThresholdMin?Max set on each action -EM//
    private EnemyCombatAction FindBestActionOfType(EnemyActionType type, float healthPercent)
    {
        List<EnemyCombatAction> candidates = new List<EnemyCombatAction>();
        int bestWeight = 0;

        foreach(EnemyCombatAction action in availableActions)
        {
            if (action == null) continue;
            if (action.actionType != type) continue;
            if (healthPercent > action.healthThresholdMax) continue;
            if (healthPercent < action.healthThresholdMin) continue;

            if(action.selectionWeight > bestWeight)
            {
                candidates.Clear();
                bestWeight = action.selectionWeight;
                candidates.Add(action);
            }
            else if (action.selectionWeight == bestWeight)
            {
                candidates.Add(action);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    //Coin flip (mirrors player coin flip) -EM//

    private bool PerformFlip(EnemyCombatAction action)
    {
        //Initialise this action's chance if we haven't seen it yet//
        if (!actionFlipChances.ContainsKey(action.actionName)) actionFlipChances[action.actionName] = action.startingFlipChance;

        float currentChance = actionFlipChances[action.actionName];
        lastFlipChance = currentChance;
        float roll = Random.Range(0f, 100f);
        bool success = roll < currentChance;

        if (debugMode)
        {
            Debug.Log($"[EnemyCombatAI] {gameObject.name} - {action.actionName}: " + $"Chance {currentChance:F0}%, roll {roll:F1} -> {(success ? "HIT" : "MISS")}");
        }

        //Update streak//
        if(success)
        {
            if(currentChance > action.startingFlipChance)
            {
                //Coming off a fail streak: rest to base//
                actionFlipChances[action.actionName] = action.startingFlipChance;
            }
            else
            {
                //Normal success: decrease chance//
                actionFlipChances[action.actionName] = Mathf.Max(MIN_FLIP_CHANCE, currentChance - action.successPenalty);
            }
        }
        else
        {
            if(currentChance < action.startingFlipChance)
            {
                //Coming off a success streak: reset to base//
                actionFlipChances[action.actionName] = action.startingFlipChance;
            }
            else
            {
                //Normal failure: increase chance//
                actionFlipChances[action.actionName] = Mathf.Min(MAX_FLIP_CHANCE, currentChance + action.failBonus);
            }
        }

        return success;
    }

    //Action execution -EM//
    private void ExecuteAction(EnemyCombatAction action, Player player)
    {
        switch(action.actionType)
        {
            case EnemyActionType.Attack:
                float damage = action.baseValue;
                player.TakeDamage(damage);
                MessageUI.Instance?.EnqueueMessage(
                    $"{gameObject.name} cast {action.actionName} and dealt {damage:0.#} damage to you.");
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} attacked player for {damage}");
                break;

            case EnemyActionType.SpecialAttack:
                float specialDamage = action.baseValue * action.specialMultiplier;
                player.TakeDamage(specialDamage);
                MessageUI.Instance?.EnqueueMessage(
                    $"{gameObject.name} cast {action.actionName} and dealt {specialDamage:0.#} damage to you.");
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} used special attack on player for {specialDamage}");
                break;

            case EnemyActionType.Defend:
                enemy.AddBlock(action.baseValue);
                MessageUI.Instance?.EnqueueMessage(
                    $"{gameObject.name} cast {action.actionName} and gained {action.baseValue:0.#} block.");
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} added {action.baseValue} block");
                break;

            case EnemyActionType.Heal:
                enemy.Heal(action.baseValue);
                MessageUI.Instance?.EnqueueMessage(
                    $"{gameObject.name} cast {action.actionName} and healed for {action.baseValue:0.#}.");
                if (debugMode) Debug.Log($"[EnemyCombatAI] {gameObject.name} healed for {action.baseValue}");
                break;
        }
    }

    //Helpers -EM//

    private void StopMovement()
    {
        if(agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private bool RequiresTarget(EnemyCombatAction action)
    {
        return action.actionType == EnemyActionType.Attack || action.actionType == EnemyActionType.SpecialAttack;
    }

    private bool IsTargetInRange(EnemyCombatAction action, Player player)
    {
        if (action.range <= 0f || player == null) return true;
        float distance = Vector3.Distance(transform.position, player.transform.position);
        return distance <= action.range + rangeTolerance;
    }

    private IEnumerator MoveIntoRange(EnemyCombatAction action, Player player)
    {
        if (agent == null || player == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled)
        {
            yield break;
        }

        agent.isStopped = false;
        agent.stoppingDistance = Mathf.Max(0.1f, action.range);
        agent.SetDestination(player.transform.position);

        float timer = 0f;
        while (timer < maxMoveTime)
        {
            if (IsTargetInRange(action, player))
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

    //Seed the flip chance dictionary from the action list so the inspector reflects starting values on the first turn -EM//
    private void InitialiseFlipChances()
    {
        foreach(EnemyCombatAction action in availableActions)
        {
            if (action == null) continue;
            if (!actionFlipChances.ContainsKey(action.actionName)) actionFlipChances[action.actionName] = action.startingFlipChance;
        }
    }
}
