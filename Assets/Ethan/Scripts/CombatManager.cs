using System.Collections.Generic;
using UnityEngine;

//Manages turn based combat flow, initiative order, and combat state -EM//
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("Combat State")]
    [SerializeField] private bool inCombat = false;
    [SerializeField] private CombatPhase currentPhase = CombatPhase.NotInCombat;

    [Header("Turn Order")]
    [SerializeField] private List<Unit> turnOrder = new List<Unit>();
    [SerializeField] private int currentTurnIndex = 0;
    [SerializeField] private Unit currentUnit;

    [Header("References")]
    [SerializeField] private Player player;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private readonly List<Unit> subscribedUnits = new List<Unit>();

    //Events//
    public delegate void CombatStarted(List<Enemy> enemies);
    public event CombatStarted OnCombatStarted;

    public delegate void CombatEnded(CombatOutcome outcome);
    public event CombatEnded OnCombatEnded;

    public delegate void TurnStarted(Unit unit);
    public event TurnStarted OnTurnStarted;

    public delegate void TurnEnded(Unit unit);
    public event TurnEnded OnTurnEnded;

    public delegate void PhaseChanged(CombatPhase newPhase);
    public event PhaseChanged OnPhaseChanged;

    //Properties//
    public bool InCombat => inCombat;
    public CombatPhase CurrentPhase => currentPhase;
    public Unit CurrentUnit => currentUnit;
    public List<Unit> TurnOrder => new List<Unit>(turnOrder);
    public bool IsPlayerTurn => currentUnit != null && currentUnit is Player;

    public enum CombatPhase
    {
        NotInCombat,
        CombatStarting,
        TurnStart,
        PlayerAction,
        EnemyAction,
        TurnEnd,
        CombatEnding
    }

    public enum CombatOutcome
    {
        PlayerWon,
        EnemiesWon,
        Draw
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }
    }

    //Start combat with a list of enemies -EM//
    public void StartCombat(List<Enemy> enemies, Enemy initiatingEnemy = null)
    {
        if (inCombat)
        {
            return;
        }

        if (enemies == null || enemies.Count == 0)
        {
            if (debugMode) Debug.LogWarning("[CombatManager] Cannot start combat with no enemies!");
            return;
        }

        if (player == null)
        {
            if (debugMode) Debug.LogError("[CombatManager] No Player found!");
            return;
        }

        inCombat = true;
        SetPhase(CombatPhase.CombatStarting);

        if (debugMode) Debug.Log($"[CombatManager] Starting combat with {enemies.Count} enemies!");

        //Build turn order//
        BuildTurnOrder(enemies, initiatingEnemy);
        RegisterDeathCallbacks(enemies);

        //Notify player they're in combat//
        player.EnterCombat();

        MessageUI.Instance?.EnqueueMessage("Combat Start!");

        //Fire event//
        OnCombatStarted?.Invoke(enemies);

        //Start first turn//
        currentTurnIndex = 0;
        StartNextTurn();
    }


    //Start combat from a single initiating enemy (pulls in nearby enemies)-EM//
    public void StartCombatFromEnemy(Enemy initiatingEnemy)
    {
        if (initiatingEnemy == null) return;

        List<Enemy> enemies = GetEnemiesInCurrentRoom(initiatingEnemy);
        StartCombat(enemies, initiatingEnemy);
    }


    //Build the turn order based on initiative, For now player goes last, then enemies in order -EM//
    private void BuildTurnOrder(List<Enemy> enemies, Enemy initiatingEnemy)
    {
        turnOrder.Clear();

        //Initiating enemy always goes first//
        if (initiatingEnemy != null && !initiatingEnemy.IsDead)
        {
            turnOrder.Add(initiatingEnemy);
        }

        //Randomize the rest (other enemies + player)//
        List<Unit> remainingUnits = new List<Unit>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            if (enemy == initiatingEnemy) continue;
            remainingUnits.Add(enemy);
        }

        if (player != null && !player.IsDead)
        {
            remainingUnits.Add(player);
        }

        ShuffleUnits(remainingUnits);
        turnOrder.AddRange(remainingUnits);

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Turn order: {string.Join(" -> ", turnOrder.ConvertAll(u => u.name))}");
        }
    }


    //Start next unit's turn -EM//
    private void StartNextTurn()
    {
        if (!inCombat) return;

        //Check for combat end conditions//
        if (CheckCombatEnd())
        {
            return;
        }

        //Get next unit in turn order//
        currentUnit = turnOrder[currentTurnIndex];

        //Skip dead units//
        int safetyCounter = turnOrder.Count;
        while (currentUnit.IsDead && safetyCounter > 0)
        {
            currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
            currentUnit = turnOrder[currentTurnIndex];
            safetyCounter--;

            //Safety check if we've looped through everyone//
            if (CheckCombatEnd())
            {
                return;
            }
        }

        //Start turn//
        SetPhase(CombatPhase.TurnStart);

        //Clear block at start of turn//
        currentUnit.ClearBlock();

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Starting turn: {currentUnit.name}");
        }

        MessageUI.Instance?.EnqueueMessage($"{currentUnit.name}'s turn.");

        OnTurnStarted?.Invoke(currentUnit);

        // Determine action phase
        if (currentUnit is Player playerUnit)
        {
            // Initialize player's coins for this turn
            playerUnit.StartTurn();
            SetPhase(CombatPhase.PlayerAction);
            // Player will act via input/UI
        }
        else if (currentUnit is Enemy enemy)
        {
            SetPhase(CombatPhase.EnemyAction);
            // Start enemy AI turn
            StartEnemyTurn(enemy);
        }
    }


    //Handle control to the enemy's EnemyCombatAI component, EnemyCombatAI calls EndCurrentTurn() when its coroutine finishes -EM//
    //Falls back to ending the turn if component is missing -EM//
    private void StartEnemyTurn(Enemy enemy)
    { 
        if (debugMode)
        {
            Debug.Log($"[CombatManager] {enemy.name}'s turn starting...");
        }

        EnemyCombatAI combatAI = enemy.GetComponent<EnemyCombatAI>();

        if (combatAI != null && combatAI.TakeTurn(player))
        {
            //EnemyCombatAI coroutine is running, it calls EndCurrentTurn() itself//
            return;
        }

        //Safely fallback no AI component or it declined to act//
        if (debugMode)
        {
            Debug.LogWarning($"[CombatManager] {enemy.name} has no EnemyCombatAI or declined to act, ending turn in 0.5s");
        }
        //Simulate enemy action delay//
        Invoke(nameof(EndCurrentTurn), 0.5f);
    }


    //End the current unit's turn -EM//
    public void EndCurrentTurn()
    {
        if (!inCombat) return;
        if (currentUnit == null) return;

        SetPhase(CombatPhase.TurnEnd);

        //If it's the player's turn ending, handle bonus coin logic//
        if (currentUnit is Player playerUnit)
        {
            playerUnit.EndTurn();
        }

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Ending turn: {currentUnit.name}");
        }

        OnTurnEnded?.Invoke(currentUnit);

        //Move to next turn//
        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;

        //If we've completed a full round, log it//
        if (currentTurnIndex == 0 && debugMode)
        {
            Debug.Log("[CombatManager] ==== Round Completed ====");
        }

        StartNextTurn();
    }


    //Check if combat should end (all enemies are dead or player is dead) -EM//
    private bool CheckCombatEnd()
    {
        if (!inCombat) return true;

        //Check if player is dead//
        if (player.IsDead)
        {
            EndCombat(CombatOutcome.EnemiesWon);
            return true;
        }

        //Check if all enemies are dead//
        bool allEnemiesDead = true;
        foreach (Unit unit in turnOrder)
        {
            if (unit is Enemy && !unit.IsDead)
            {
                allEnemiesDead = false;
                break;
            }
        }

        if (allEnemiesDead)
        {
            EndCombat(CombatOutcome.PlayerWon);
            return true;
        }

        return false;
    }

    
    //End combat -EM//
    private void EndCombat(CombatOutcome outcome)
    {
        if (!inCombat) return;

        SetPhase(CombatPhase.CombatEnding);

        inCombat = false;

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Combat ended! Outcome: {outcome}");
        }

        if (outcome == CombatOutcome.EnemiesWon && player != null && !player.IsDead)
        {
            float lethalDamage = player.CurrentHealth + player.CurrentBlock + 1f;
            player.TakeDamage(lethalDamage);
        }

        MessageUI.Instance?.EnqueueMessage("Combat Ended!");

        //Clean up//
        UnregisterDeathCallbacks();
        turnOrder.Clear();
        currentUnit = null;
        currentTurnIndex = 0;

        //Notify player//
        if (player != null)
        {
            player.ExitCombat();
        }

        OnCombatEnded?.Invoke(outcome);

        SetPhase(CombatPhase.NotInCombat);
    }


    //Force end combat -EM//
    [ContextMenu("Force End Combat")]
    public void ForceEndCombat(CombatOutcome outcome = CombatOutcome.Draw)
    {
        if (inCombat)
        {
            if (debugMode) Debug.Log("[CombatManager] Force ending combat");
            EndCombat(outcome);
        }
    }

    private void SetPhase(CombatPhase newPhase)
    {
        if (currentPhase == newPhase) return;

        CombatPhase oldPhase = currentPhase;
        currentPhase = newPhase;

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Phase: {oldPhase} -> {newPhase}");
        }

        OnPhaseChanged?.Invoke(newPhase);
    }


    //Get all living enemies in combat -EM//
    public List<Enemy> GetLivingEnemies()
    {
        List<Enemy> enemies = new List<Enemy>();

        foreach (Unit unit in turnOrder)
        {
            if (unit is Enemy enemy && !enemy.IsDead)
            {
                enemies.Add(enemy);
            }
        }

        return enemies;
    }


    //Get all living units in combat -EM//
    public List<Unit> GetLivingUnits()
    {
        List<Unit> units = new List<Unit>();

        foreach (Unit unit in turnOrder)
        {
            if (!unit.IsDead)
            {
                units.Add(unit);
            }
        }

        return units;
    }

 
    //Get all living enemies in the player's current room -EM//
    private List<Enemy> GetEnemiesInCurrentRoom(Enemy initiatingEnemy)
    {
        List<Enemy> enemies = new List<Enemy>();

        RoomManager roomManager = FindFirstObjectByType<RoomManager>();
        RoomLA currentRoom = roomManager != null ? roomManager.CurrentRoom : null;

        if (currentRoom == null)
        {
            if (debugMode) Debug.LogWarning("[CombatManager] No current room found, starting combat with initiating enemy only.");
            if (initiatingEnemy != null && !initiatingEnemy.IsDead)
            {
                enemies.Add(initiatingEnemy);
            }
            return enemies;
        }

        Enemy[] allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in allEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            if (!currentRoom.Contains(enemy.transform.position)) continue;
            enemies.Add(enemy);
        }

        return enemies;
    }

    private void ShuffleUnits(List<Unit> units)
    {
        for (int i = 0; i < units.Count - 1; i++)
        {
            int swapIndex = Random.Range(i, units.Count);
            Unit temp = units[i];
            units[i] = units[swapIndex];
            units[swapIndex] = temp;
        }
    }

    private void RegisterDeathCallbacks(List<Enemy> enemies)
    {
        UnregisterDeathCallbacks();

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null) continue;
            enemy.OnDied += HandleUnitDied;
            subscribedUnits.Add(enemy);
        }

        if (player != null)
        {
            player.OnDied += HandleUnitDied;
            subscribedUnits.Add(player);
        }
    }

    private void UnregisterDeathCallbacks()
    {
        foreach (Unit unit in subscribedUnits)
        {
            if (unit == null) continue;
            unit.OnDied -= HandleUnitDied;
        }

        subscribedUnits.Clear();
    }

    private void HandleUnitDied(Unit unit)
    {
        if (!inCombat) return;
        CheckCombatEnd();
    }
}
