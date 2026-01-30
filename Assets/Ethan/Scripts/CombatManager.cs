using System.Collections.Generic;
using UnityEngine;

//Manages turn based combat flow, initiative order, and combat state - EM//
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

    [Header("Refernces")]
    [SerializeField] private Player player;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    //Events//
    public delegate void CombatStarted(List<Enemy> enemies);
    public event CombatStarted OnCombatStarted;

    public delegate void CombatEnded(bool playerWon);
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

    private void Awake()
    {
       if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if(player == null)
        {
            player = FindFirstObjectByType<Player>();
        }
    }

    //Start combat with a list of enemies - EM//
    public void StartCombat(List<Enemy> enemies)
    {
        if (inCombat)
        {
            Debug.LogWarning("[CombatManager] Already in combat!");
            return;
        }

        if (enemies == null || enemies.Count == 0)
        {
            Debug.LogWarning("[CombatManager] Cannot start combat with no enemies!");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[CombatManager] No Player found!");
            return;
        }

        inCombat = true;
        SetPhase(CombatPhase.CombatStarting);

        Debug.Log($"[CombatManager] Starting combat with {enemies.Count} enemies!");

        //Build turn order//
        BuildTurnOrder(enemies);

        //Notify player thy're in combat//
        player.EnterCombat();

        //Fire event//
        OnCombatStarted?.Invoke(enemies);

        //Start first turn//
        currentTurnIndex = 0;
        StartNextTurn();
    }

    //Start combat with a single enemy - EM//
    private void StartCombat(Enemy enemy)
    {
        if (enemy == null) return;
        StartCombat(new List<Enemy> { enemy });
    }

    //Build the turn order based on initiative -EM//
    //For now player goes firs then enemies in order - EM//
    private void BuildTurnOrder(List<Enemy> enemies)
    {
        turnOrder.Clear();

        //Player goes first//
        turnOrder.Add(player);

        //Add all enemies//
        foreach(Enemy enemy in enemies)
        {
            if(enemy != null)
            {
                turnOrder.Add(enemy);
            }
        }

        if(debugMode)
        {
            Debug.Log($"[CombatManager] Turn order: {string.Join(" -> ", turnOrder.ConvertAll(u => u.name))}");
        }
    }

    //Start next unit's turn -EM//
    private void StartNextTurn()
    {
        if (!inCombat) return;

        //Check for combat end conditions//
        if(CheckCombatEnd())
        {
            return;
        }

        //Get next unit in turn order//
        currentUnit = turnOrder[currentTurnIndex];

        //Skip dead units//
        while(currentUnit.IsDead)
        {
            if(debugMode)
            {
                Debug.Log($"[CombatManager] Skipping dead unit: {currentUnit.name}");
            }

            currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
            currentUnit =  turnOrder[currentTurnIndex];

            //Safely check if we've looped through everyone//
            if(CheckCombatEnd())
            {
                return;
            }
        }

        //Start turn//
        SetPhase(CombatPhase.TurnStart);

        //Clear block at start of turn//
        currentUnit.ClearBlock();

        if(debugMode)
        {
            Debug.Log($"[CombatManager] Starting turn: {currentUnit.name}");
        }

        OnTurnStarted?.Invoke(currentUnit);

        //Determine action phase//
        if(currentUnit is Player)
        {
            SetPhase(CombatPhase.PlayerAction);
            //Player will act via input/UI//
        }
        else if (currentUnit is Enemy)
        {
            SetPhase(CombatPhase.PlayerAction);
            //Start enemy AI turn//
            StartEnemyTurn(currentUnit as Enemy);
        }
    }

    //Handle enemy turn (Enemy AI Needs expanding) - EM//
    private void StartEnemyTurn(Enemy enemy)
    {
        //TODO: impliment enemy AI Descion making//
        //UNTIL THEN: just end turn immediatley//

        if(debugMode)
        {
            Debug.Log($"[CombatManager] {enemy.name} is thinking...");
        }

        //Simulate eenmy action delay//
        Invoke(nameof(EndCurrentTurn), 1f);
    }

    //End the current unit's turn -EM//
    public void EndCurrentTurn()
    {
        if (!inCombat) return;
        if (currentUnit == null) return;

        SetPhase(CombatPhase.TurnEnd);

        if(debugMode)
        {
            Debug.Log($"[CombatManager] Ending turn: {currentUnit.name}");
        }

        OnTurnEnded?.Invoke(currentUnit);

        //Move to next turn//
        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;

        //if we've completed a full round, log it//
        if(currentTurnIndex == 0 && debugMode)
        {
            Debug.Log("[CombatManager] ==== Round Completed ====");
        }

        StartNextTurn();
    }

    //Check if combat should end (all enemies are dead or player is dead) - EM//
    private bool CheckCombatEnd()
    {
        if (!inCombat) return true;

        //Check if player is dead//
        if(player.IsDead)
        {
            EndCombat(false);
            return true;
        }

        //Check if all enemies are dead//
        bool allEnemiesDead = true;
        foreach(Unit unit in turnOrder)
        {
            if(unit is Enemy && !unit.IsDead)
            {
                allEnemiesDead = false;
                break;
            }
        }

        if(allEnemiesDead)
        {
            EndCombat(true);
            return true;
        }

        return false;
    }

    //End Combat - EM//
    private void EndCombat(bool playerWon)
    {
        if (!inCombat) return;

        SetPhase(CombatPhase.CombatEnding);

        inCombat = false;

        if(debugMode)
        {
            Debug.Log($"[CombatManager] Combat ended! Player won: {playerWon}");
        }

        //Clean up//
        turnOrder.Clear();
        currentUnit = null;
        currentTurnIndex = 0;

        //Notify player//
        if(player != null)
        {
            player.ExitCombat();
        }

        OnCombatEnded?.Invoke(playerWon);

        SetPhase(CombatPhase.NotInCombat);
    }

    //Force end combat (for debugging and other cases) - EM//
    [ContextMenu("Force End Combat")]
    public void ForceEndCombat()
    {
        if(inCombat)
        {
            Debug.Log("[CombatManager} Force ending combat");
            EndCombat(false);
        }
    }

    private void SetPhase(CombatPhase newPhase)
    {
        if (CurrentPhase == newPhase) return;

        CombatPhase oldPhase = currentPhase;
        currentPhase = newPhase;

        if(debugMode)
        {
            Debug.Log($"[CombatManager] Phase: {oldPhase} -> {newPhase}");
        }

        OnPhaseChanged?.Invoke(newPhase);
    }

    //Get all living enemies in combat -EM//
    public List<Enemy> GetLivingEnemies()
    {
        List<Enemy> enemies = new List<Enemy>();

        foreach(Unit unit in turnOrder)
        {
            if(unit is Enemy enemy && !enemy.IsDead)
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

        foreach(Unit unit in turnOrder)
        {
            if(!unit.IsDead)
            {
                units.Add(unit);
            }
        }

        return units;
    }
}
