using UnityEngine;

/// <summary>
/// Player-specific functionality built on top of Unit base class
/// Handles combat state and player-specific mechanics
/// </summary>
public class Player : Unit
{
    [Header("Combat State")]
    [SerializeField] private bool isInCombat = false;

    [Header("Action Points")]
    [SerializeField] private int startingActionPoints = 10;
    [SerializeField] private int currentActionPoints = 10;

    // Events
    public delegate void CombatStateChanged(bool inCombat);
    public event CombatStateChanged OnCombatStateChanged;

    public delegate void ActionPointsChanged(int current, int max);
    public event ActionPointsChanged OnActionPointsChanged;

    // Properties
    public bool IsInCombat => isInCombat;
    public int StartingActionPoints => startingActionPoints;
    public int CurrentActionPoints => currentActionPoints;

    protected override void Awake()
    {
        base.Awake();
        currentActionPoints = Mathf.Max(0, startingActionPoints);
    }

    /// <summary>
    /// Override AddBlock to only work during combat
    /// </summary>
    public override void AddBlock(int amount)
    {
        if (amount <= 0) return;

        if (!isInCombat)
        {
            return;
        }

        base.AddBlock(amount);
    }

    /// <summary>
    /// Enter combat mode
    /// </summary>
    public void EnterCombat()
    {
        if (!isInCombat)
        {
            isInCombat = true;
            Debug.Log("[Player] Entered combat!");
            OnCombatStateChanged?.Invoke(true);
        }
    }

    /// <summary>
    /// Exit combat mode
    /// </summary>
    public void ExitCombat()
    {
        if (isInCombat)
        {
            isInCombat = false;
            ClearBlock();
            Debug.Log("[Player] Exited combat");
            OnCombatStateChanged?.Invoke(false);
        }
    }

    public void ResetActionPoints()
    {
        currentActionPoints = Mathf.Max(0, startingActionPoints);
        OnActionPointsChanged?.Invoke(currentActionPoints, startingActionPoints);
    }

    public bool CanSpendActionPoints(int amount)
    {
        if (amount <= 0) return true;
        return currentActionPoints >= amount;
    }

    public bool SpendActionPoints(int amount)
    {
        if (amount <= 0) return true;
        if (currentActionPoints < amount) return false;

        currentActionPoints -= amount;
        OnActionPointsChanged?.Invoke(currentActionPoints, startingActionPoints);
        return true;
    }

    protected override void Die()
    {
        base.Die();
        // TODO: Handle player death 
    }
}
