using UnityEngine;

/// <summary>
/// Player-specific functionality built on top of Unit base class
/// Handles combat state and player-specific mechanics
/// </summary>
public class Player : Unit
{
    [Header("Combat State")]
    [SerializeField] private bool isInCombat = false;

    // Events
    public delegate void CombatStateChanged(bool inCombat);
    public event CombatStateChanged OnCombatStateChanged;

    // Properties
    public bool IsInCombat => isInCombat;

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

    protected override void Die()
    {
        base.Die();
        // TODO: Handle player death 
    }
}