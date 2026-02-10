using UnityEngine;

/// <summary>
/// Player-specific functionality built on top of Unit base class
/// </summary>
public class Player : Unit
{
    [Header("Combat State")]
    [SerializeField] private bool isInCombat = false;

    [Header("Coin System")]
    [SerializeField] private int baseCoins = 5;
    [SerializeField] private int currentCoins = 5;
    [SerializeField] private int bonusCoinsNextTurn = 0;
    [SerializeField] private int baseCarryoverCoins = 1;

    [Header("Coin Flip")]
    [Tooltip("Current chance to succeed on a coin flip (0-100)")]
    [SerializeField] private float currentFlipChance = 60f;
    [SerializeField] private const float BASE_FLIP_CHANCE = 60f;
    [SerializeField] private const float FAIL_BONUS = 10f;      // +10% per fail
    [SerializeField] private const float SUCCESS_PENALTY = 5f;  // -5% per success
    [SerializeField] private const float MIN_FLIP_CHANCE = 0f;
    [SerializeField] private const float MAX_FLIP_CHANCE = 100f;

    [Header("Movement")]
    [SerializeField] private bool hasSpentMovementCoin = false;
    [SerializeField] private float distanceMovedThisTurn = 0f;
    [SerializeField] private float baseCombatMoveDistance = 5f;

    // Events
    public delegate void CombatStateChanged(bool inCombat);
    public event CombatStateChanged OnCombatStateChanged;

    public delegate void CoinsChanged(int current, int max);
    public event CoinsChanged OnCoinsChanged;

    public delegate void CoinFlipResult(bool success, float chance);
    public event CoinFlipResult OnCoinFlipResult;

    public delegate void MovementCoinSpent();
    public event MovementCoinSpent OnMovementCoinSpent;

    // Properties
    public bool IsInCombat => isInCombat;
    public int BaseCoins => GetBaseCoins();
    public int CurrentCoins => currentCoins;
    public float CurrentFlipChance => currentFlipChance;
    public bool HasSpentMovementCoin => hasSpentMovementCoin;
    public float DistanceMovedThisTurn => distanceMovedThisTurn;
    public float RemainingMoveDistance => MaxCombatMoveDistance - distanceMovedThisTurn;
    public float MaxCombatMoveDistance => GetMaxCombatMoveDistance();

    protected override void Awake()
    {
        maxHealth = GetMaxHealth();
        base.Awake();
        currentCoins = GetBaseCoins();
        currentFlipChance = BASE_FLIP_CHANCE;
    }

    private void OnEnable()
    {
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.OnModifiersChanged += HandleModifiersChanged;
        }
    }

    private void OnDisable()
    {
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.OnModifiersChanged -= HandleModifiersChanged;
        }
    }

    /// <summary>
    /// Override AddBlock to only work during combat
    /// </summary>
    public override void AddBlock(float amount)
    {
        if (amount <= 0f) return;

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
            currentFlipChance = BASE_FLIP_CHANCE;
            bonusCoinsNextTurn = 0;
            currentCoins = 0;
            OnCoinsChanged?.Invoke(currentCoins, GetBaseCoins());
            if (debugMode) Debug.Log("[Player] Entered combat!");
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
            currentFlipChance = BASE_FLIP_CHANCE;
            bonusCoinsNextTurn = 0;
            hasSpentMovementCoin = false;
            distanceMovedThisTurn = 0f;
            if (debugMode) Debug.Log("[Player] Exited combat");
            OnCombatStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// Reset coins at the start of player's turn
    /// Called by CombatManager when player's turn starts
    /// </summary>
    public void StartTurn()
    {
        // Calculate coins for this turn (base + any bonus from last turn)
        int coinsThisTurn = GetBaseCoins() + bonusCoinsNextTurn;
        currentCoins = coinsThisTurn;
        
        if (debugMode)
        {
            Debug.Log($"[Player] Turn started with {currentCoins} coins (base: {GetBaseCoins()}, bonus: {bonusCoinsNextTurn})");
        }

        // Reset bonus for next turn calculation
        bonusCoinsNextTurn = 0;
        
        // Reset movement tracking for new turn
        hasSpentMovementCoin = false;
        distanceMovedThisTurn = 0f;

        OnCoinsChanged?.Invoke(currentCoins, GetBaseCoins());
    }

    /// <summary>
    /// Called when player's turn ends
    /// Checks if they have coins remaining for bonus next turn
    /// </summary>
    public void EndTurn()
    {
        int carryoverCap = GetCarryoverCoins();
        bonusCoinsNextTurn = Mathf.Clamp(currentCoins, 0, carryoverCap);
        if (debugMode)
        {
            if (bonusCoinsNextTurn > 0)
            {
                Debug.Log($"[Player] Turn ended with {currentCoins} coins remaining. +{bonusCoinsNextTurn} bonus coins next turn (cap: {carryoverCap}).");
            }
            else
            {
                Debug.Log("[Player] Turn ended with no coins remaining. No bonus next turn.");
            }
        }
    }

    /// <summary>
    /// Check if player can spend a coin
    /// </summary>
    public bool CanSpendCoin()
    {
        return currentCoins >= 1;
    }

    /// <summary>
    /// Spend a coin
    /// </summary>
    public bool SpendCoin()
    {
        if (currentCoins < 1) return false;

        currentCoins--;
        if (debugMode)
        {
            Debug.Log($"[Player] Spent 1 coin. Remaining: {currentCoins}");
        }
        OnCoinsChanged?.Invoke(currentCoins, GetBaseCoins());
        return true;
    }

    /// <summary>
    /// Spend the movement coin for this turn (only costs 1 coin for the entire turn's movement)
    /// </summary>
    public bool SpendMovementCoin()
    {
        if (hasSpentMovementCoin)
        {
            // Already spent movement coin this turn, movement is free
            return true;
        }

        if (currentCoins < 1)
        {
            if (debugMode)
            {
                Debug.Log("[Player] Cannot move - no coins available!");
            }
            return false;
        }

        currentCoins--;
        hasSpentMovementCoin = true;
        
        if (debugMode)
        {
            Debug.Log($"[Player] Spent movement coin. Remaining coins: {currentCoins}");
        }
        
        OnCoinsChanged?.Invoke(currentCoins, GetBaseCoins());
        OnMovementCoinSpent?.Invoke();
        return true;
    }

    /// <summary>
    /// Check if player can still move (has distance remaining)
    /// </summary>
    public bool CanMove()
    {
        if (!isInCombat) return true;
        
        // If haven't spent movement coin yet, need at least 1 coin
        if (!hasSpentMovementCoin && currentCoins < 1)
        {
            return false;
        }

        // Check if there's distance remaining
        return distanceMovedThisTurn < MaxCombatMoveDistance;
    }

    /// <summary>
    /// Add distance to the movement tracker
    /// Returns false if the movement would exceed the limit
    /// </summary>
    public bool AddMovementDistance(float distance)
    {
        if (!isInCombat) return true;

        float newTotal = distanceMovedThisTurn + distance;
        
        if (newTotal > MaxCombatMoveDistance + 0.01f) 
        {
            if (debugMode)
            {
                Debug.Log($"[Player] Movement would exceed limit! Current: {distanceMovedThisTurn:F2}, Adding: {distance:F2}, Max: {MaxCombatMoveDistance}");
            }
            return false;
        }

        distanceMovedThisTurn = newTotal;
        
        if (debugMode && distance > 0.1f)
        {
            Debug.Log($"[Player] Moved {distance:F2} units. Total this turn: {distanceMovedThisTurn:F2}/{MaxCombatMoveDistance}");
        }
        
        return true;
    }

    /// <summary>
    /// Get how much distance can still be moved to a target point
    /// Returns the clamped distance or 0 if can't move
    /// </summary>
    public float GetAllowedMoveDistance(float requestedDistance)
    {
        if (!isInCombat) return requestedDistance;
        
        float remaining = MaxCombatMoveDistance - distanceMovedThisTurn;
        return Mathf.Min(requestedDistance, remaining);
    }

    /// <summary>
    /// Perform a coin flip for ability success
    /// Returns true if the ability hits, false if it misses
    /// Also updates the flip chance based on the result
    /// </summary>
    public bool PerformCoinFlip()
    {
        float roll = Random.Range(0f, 100f);
        bool success = roll < currentFlipChance;

        if (debugMode)
        {
            Debug.Log($"[Player] Coin Flip! Chance: {currentFlipChance:F0}%, Roll: {roll:F1} -> {(success ? "SUCCESS!" : "MISS!")}");
        }

        OnCoinFlipResult?.Invoke(success, currentFlipChance);

        // Update flip chance based on result
        if (success)
        {
            // On success: decrease chance by 5%, down to 0%
            // But if we were on a fail streak (chance > 60), reset to 60%
            if (currentFlipChance > BASE_FLIP_CHANCE)
            {
                // Was on a fail streak, reset to base
                currentFlipChance = BASE_FLIP_CHANCE;
                if (debugMode)
                {
                    Debug.Log($"[Player] Success after fail streak! Flip chance reset to {currentFlipChance:F0}%");
                }
            }
            else
            {
                // Normal success, decrease chance
                currentFlipChance = Mathf.Max(MIN_FLIP_CHANCE, currentFlipChance - SUCCESS_PENALTY);
                if (debugMode)
                {
                    Debug.Log($"[Player] Success streak continues. Next flip chance: {currentFlipChance:F0}%");
                }
            }
        }
        else
        {
            // On fail: increase chance by 10%, up to 100%
            // But if we were on a success streak (chance < 60), reset to 60%
            if (currentFlipChance < BASE_FLIP_CHANCE)
            {
                // Was on a success streak, reset to base
                currentFlipChance = BASE_FLIP_CHANCE;
                if (debugMode)
                {
                    Debug.Log($"[Player] Fail after success streak! Flip chance reset to {currentFlipChance:F0}%");
                }
            }
            else
            {
                // Normal fail, increase chance
                currentFlipChance = Mathf.Min(MAX_FLIP_CHANCE, currentFlipChance + FAIL_BONUS);
                if (debugMode)
                {
                    Debug.Log($"[Player] Fail streak continues. Next flip chance: {currentFlipChance:F0}%");
                }
            }
        }

        return success;
    }

    protected override void Die()
    {
        base.Die();
        // TODO: Handle player death 
    }

    private void HandleModifiersChanged()
    {
        SetMaxHealth(GetMaxHealth(), true);
    }

    private float GetMaxHealth()
    {
        if (StatsManager.Instance == null)
        {
            return maxHealth;
        }

        return StatsManager.Instance.GetMaxHealth();
    }

    private int GetBaseCoins()
    {
        if (StatsManager.Instance == null)
        {
            return baseCoins;
        }

        return StatsManager.Instance.GetBaseCoins();
    }

    private int GetCarryoverCoins()
    {
        if (StatsManager.Instance == null)
        {
            return baseCarryoverCoins;
        }

        return StatsManager.Instance.GetCarryoverCoins();
    }

    private float GetMaxCombatMoveDistance()
    {
        if (StatsManager.Instance == null)
        {
            return baseCombatMoveDistance;
        }

        return StatsManager.Instance.GetMaxCombatMoveDistance();
    }
}
