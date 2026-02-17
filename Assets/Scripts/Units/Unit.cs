using UnityEngine;

/// <summary>
/// Base class for all units
/// Contains health, block, and common combat functionality
/// </summary>
public abstract class Unit : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] protected float maxHealth;
    [SerializeField] protected float currentHealth;

    [Header("Block")]
    [SerializeField] protected float currentBlock = 0f;

    [Header("Debug")]
    [SerializeField] protected bool debugMode = true;

    // Events
    public delegate void HealthChanged(float current, float max);
    public event HealthChanged OnHealthChanged;

    public delegate void BlockChanged(float current);
    public event BlockChanged OnBlockChanged;

    public delegate void UnitDied(Unit unit);
    public event UnitDied OnDied;

    // Properties
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float CurrentBlock => currentBlock;
    public bool IsDead => currentHealth <= 0f;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Deal damage to the unit
    /// Block absorbs damage first
    /// </summary>
    public virtual void TakeDamage(float amount)
    {
        float roundedAmount = Mathf.Ceil(amount);
        if (roundedAmount <= 0f) return;

        float remainingDamage = roundedAmount;

        // Block absorbs damage first
        if (currentBlock > 0f)
        {
            float blockedDamage = Mathf.Min(currentBlock, remainingDamage);
            currentBlock -= blockedDamage;
            remainingDamage -= blockedDamage;

            if (debugMode) Debug.Log($"[{GetType().Name}] {gameObject.name} blocked {blockedDamage} damage. Block remaining: {currentBlock}");
            OnBlockChanged?.Invoke(currentBlock);
        }

        // Apply remaining damage to health
        if (remainingDamage > 0f)
        {
            currentHealth -= remainingDamage;
            currentHealth = Mathf.Max(0f, currentHealth);

            if (debugMode) Debug.Log($"[{GetType().Name}] {gameObject.name} took {remainingDamage} damage. Health: {currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }
    }

    /// <summary>
    /// Heal the unit
    /// </summary>
    public virtual void Heal(float amount)
    {
        float roundedAmount = Mathf.Ceil(amount);
        if (roundedAmount <= 0f) return;

        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(currentHealth + roundedAmount, maxHealth);

        float actualHeal = currentHealth - previousHealth;

        if (actualHeal > 0f)
        {
            if (debugMode) Debug.Log($"[{GetType().Name}] {gameObject.name} healed for {actualHeal}. Health: {currentHealth}/{maxHealth}");
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    /// <summary>
    /// Add block to the unit
    /// </summary>
    public virtual void AddBlock(float amount)
    {
        float roundedAmount = Mathf.Ceil(amount);
        if (roundedAmount <= 0f) return;

        currentBlock += roundedAmount;
        if (debugMode) Debug.Log($"[{GetType().Name}] {gameObject.name} added {roundedAmount} block. Total block: {currentBlock}");
        OnBlockChanged?.Invoke(currentBlock);
    }

    /// <summary>
    /// Clear all block
    /// </summary>
    public virtual void ClearBlock()
    {
        if (currentBlock > 0f)
        {
            if (debugMode) Debug.Log($"[{GetType().Name}] {gameObject.name} block cleared (was {currentBlock})");
            currentBlock = 0f;
            OnBlockChanged?.Invoke(currentBlock);
        }
    }

    /// <summary>
    /// Set max health and optionally adjust current health
    /// </summary>
    public virtual void SetMaxHealth(float newMax, bool adjustCurrentHealth = true)
    {
        maxHealth = Mathf.Max(1f, newMax);

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
        if (debugMode) Debug.Log($"[{GetType().Name}] {gameObject.name} died!");
        OnDied?.Invoke(this);
    }

    protected virtual void OnValidate()
    {
        // Keep values within valid ranges in editor
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentBlock = Mathf.Max(0f, currentBlock);
    }
}
