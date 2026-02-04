using UnityEngine;

/// <summary>
/// Base class for all units
/// Contains health, block, and common combat functionality
/// </summary>
public abstract class Unit : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] protected int maxHealth;
    [SerializeField] protected int currentHealth;

    [Header("Block")]
    [SerializeField] protected int currentBlock = 0;

    // Events
    public delegate void HealthChanged(int current, int max);
    public event HealthChanged OnHealthChanged;

    public delegate void BlockChanged(int current);
    public event BlockChanged OnBlockChanged;

    public delegate void UnitDied(Unit unit);
    public event UnitDied OnDied;

    // Properties
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int CurrentBlock => currentBlock;
    public bool IsDead => currentHealth <= 0;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Deal damage to the unit
    /// Block absorbs damage first
    /// </summary>
    public virtual void TakeDamage(int amount)
    {
        if (amount <= 0) return;

        int remainingDamage = amount;

        // Block absorbs damage first
        if (currentBlock > 0)
        {
            int blockedDamage = Mathf.Min(currentBlock, remainingDamage);
            currentBlock -= blockedDamage;
            remainingDamage -= blockedDamage;

            Debug.Log($"[{GetType().Name}] {gameObject.name} blocked {blockedDamage} damage. Block remaining: {currentBlock}");
            OnBlockChanged?.Invoke(currentBlock);
        }

        // Apply remaining damage to health
        if (remainingDamage > 0)
        {
            currentHealth -= remainingDamage;
            currentHealth = Mathf.Max(0, currentHealth);

            Debug.Log($"[{GetType().Name}] {gameObject.name} took {remainingDamage} damage. Health: {currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                Die();
            }
        }
    }

    /// <summary>
    /// Heal the unit
    /// </summary>
    public virtual void Heal(int amount)
    {
        if (amount <= 0) return;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        int actualHeal = currentHealth - previousHealth;

        if (actualHeal > 0)
        {
            Debug.Log($"[{GetType().Name}] {gameObject.name} healed for {actualHeal}. Health: {currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    /// <summary>
    /// Add block to the unit
    /// </summary>
    public virtual void AddBlock(int amount)
    {
        if (amount <= 0) return;

        currentBlock += amount;
        Debug.Log($"[{GetType().Name}] {gameObject.name} added {amount} block. Total block: {currentBlock}");
        OnBlockChanged?.Invoke(currentBlock);
    }

    /// <summary>
    /// Clear all block
    /// </summary>
    public virtual void ClearBlock()
    {
        if (currentBlock > 0)
        {
            Debug.Log($"[{GetType().Name}] {gameObject.name} block cleared (was {currentBlock})");
            currentBlock = 0;
            OnBlockChanged?.Invoke(currentBlock);
        }
    }

    /// <summary>
    /// Set max health and optionally adjust current health
    /// </summary>
    public virtual void SetMaxHealth(int newMax, bool adjustCurrentHealth = true)
    {
        maxHealth = Mathf.Max(1, newMax);

        if (adjustCurrentHealth)
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Called when the unit dies
    /// </summary>
    protected virtual void Die()
    {
        Debug.Log($"[{GetType().Name}] {gameObject.name} died!");
        OnDied?.Invoke(this);
    }

    protected virtual void OnValidate()
    {
        // Keep values within valid ranges in editor
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        currentBlock = Mathf.Max(0, currentBlock);
    }
}