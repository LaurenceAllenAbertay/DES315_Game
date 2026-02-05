using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages turn based combat flow, initiative order, and combat state
/// </summary>
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

    [Header("Combat Join Range")]
    [SerializeField] private float enemyJoinRadius = 8f;
    [SerializeField] private LayerMask enemyLayer = 1 << 8;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Events
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

    // Properties
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

    /// <summary>
    /// Start combat with a list of enemies
    /// </summary>
    public void StartCombat(List<Enemy> enemies)
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

        // Build turn order
        BuildTurnOrder(enemies);

        // Notify player they're in combat
        player.EnterCombat();

        // Fire event
        OnCombatStarted?.Invoke(enemies);

        // Start first turn
        currentTurnIndex = 0;
        StartNextTurn();
    }

    /// <summary>
    /// Start combat from a single initiating enemy (pulls in nearby enemies)
    /// </summary>
    public void StartCombatFromEnemy(Enemy initiatingEnemy)
    {
        if (initiatingEnemy == null) return;

        List<Enemy> enemies = GetEnemiesInJoinRange(initiatingEnemy);
        StartCombat(enemies);
    }

    /// <summary>
    /// Build the turn order based on initiative
    /// For now player goes last, then enemies in order
    /// </summary>
    private void BuildTurnOrder(List<Enemy> enemies)
    {
        turnOrder.Clear();

        // Add all enemies first
        foreach (Enemy enemy in enemies)
        {
            if (enemy != null)
            {
                turnOrder.Add(enemy);
            }
        }

        // Player goes last
        turnOrder.Add(player);

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Turn order: {string.Join(" -> ", turnOrder.ConvertAll(u => u.name))}");
        }
    }

    /// <summary>
    /// Start next unit's turn
    /// </summary>
    private void StartNextTurn()
    {
        if (!inCombat) return;

        // Check for combat end conditions
        if (CheckCombatEnd())
        {
            return;
        }

        // Get next unit in turn order
        currentUnit = turnOrder[currentTurnIndex];

        // Skip dead units
        int safetyCounter = turnOrder.Count;
        while (currentUnit.IsDead && safetyCounter > 0)
        {
            currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
            currentUnit = turnOrder[currentTurnIndex];
            safetyCounter--;

            // Safety check if we've looped through everyone
            if (CheckCombatEnd())
            {
                return;
            }
        }

        // Start turn
        SetPhase(CombatPhase.TurnStart);

        // Clear block at start of turn
        currentUnit.ClearBlock();

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Starting turn: {currentUnit.name}");
        }

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

    /// <summary>
    /// Handle enemy turn (Enemy AI needs expanding)
    /// </summary>
    private void StartEnemyTurn(Enemy enemy)
    {
        // TODO: Implement enemy AI decision making

        if (debugMode)
        {
            Debug.Log($"[CombatManager] {enemy.name} is thinking...");
        }

        // Simulate enemy action delay
        Invoke(nameof(EndCurrentTurn), 1f);
    }

    /// <summary>
    /// End the current unit's turn
    /// </summary>
    public void EndCurrentTurn()
    {
        if (!inCombat) return;
        if (currentUnit == null) return;

        SetPhase(CombatPhase.TurnEnd);

        // If it's the player's turn ending, handle bonus coin logic
        if (currentUnit is Player playerUnit)
        {
            playerUnit.EndTurn();
        }

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Ending turn: {currentUnit.name}");
        }

        OnTurnEnded?.Invoke(currentUnit);

        // Move to next turn
        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;

        // If we've completed a full round, log it
        if (currentTurnIndex == 0 && debugMode)
        {
            Debug.Log("[CombatManager] ==== Round Completed ====");
        }

        StartNextTurn();
    }

    /// <summary>
    /// Check if combat should end (all enemies are dead or player is dead)
    /// </summary>
    private bool CheckCombatEnd()
    {
        if (!inCombat) return true;

        // Check if player is dead
        if (player.IsDead)
        {
            EndCombat(false);
            return true;
        }

        // Check if all enemies are dead
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
            EndCombat(true);
            return true;
        }

        return false;
    }

    /// <summary>
    /// End combat
    /// </summary>
    private void EndCombat(bool playerWon)
    {
        if (!inCombat) return;

        SetPhase(CombatPhase.CombatEnding);

        inCombat = false;

        if (debugMode)
        {
            Debug.Log($"[CombatManager] Combat ended! Player won: {playerWon}");
        }

        // Clean up
        turnOrder.Clear();
        currentUnit = null;
        currentTurnIndex = 0;

        // Notify player
        if (player != null)
        {
            player.ExitCombat();
        }

        OnCombatEnded?.Invoke(playerWon);

        SetPhase(CombatPhase.NotInCombat);
    }

    /// <summary>
    /// Force end combat 
    /// </summary>
    [ContextMenu("Force End Combat")]
    public void ForceEndCombat()
    {
        if (inCombat)
        {
            if (debugMode) Debug.Log("[CombatManager] Force ending combat");
            EndCombat(false);
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

    /// <summary>
    /// Get all living enemies in combat
    /// </summary>
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

    /// <summary>
    /// Get all living units in combat
    /// </summary>
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

    /// <summary>
    /// Get enemies within join range of the initiating enemy
    /// </summary>
    private List<Enemy> GetEnemiesInJoinRange(Enemy initiatingEnemy)
    {
        List<Enemy> enemies = new List<Enemy>();

        if (!initiatingEnemy.IsDead)
        {
            enemies.Add(initiatingEnemy);
        }

        if (enemyJoinRadius <= 0f)
        {
            return enemies;
        }

        if (enemyLayer.value != 0)
        {
            Collider[] colliders = Physics.OverlapSphere(initiatingEnemy.transform.position, enemyJoinRadius, enemyLayer);
            foreach (Collider col in colliders)
            {
                Enemy enemy = col.GetComponentInParent<Enemy>();
                if (enemy != null && !enemy.IsDead && !enemies.Contains(enemy))
                {
                    enemies.Add(enemy);
                }
            }
        }
        else
        {
            Enemy[] allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (Enemy enemy in allEnemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                if (enemies.Contains(enemy)) continue;

                float distance = Vector3.Distance(initiatingEnemy.transform.position, enemy.transform.position);
                if (distance <= enemyJoinRadius)
                {
                    enemies.Add(enemy);
                }
            }
        }

        return enemies;
    }
}