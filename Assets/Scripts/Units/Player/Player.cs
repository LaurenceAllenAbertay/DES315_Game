using UnityEngine;

public class Player : Unit
{
    [Header("UI")]
    [SerializeField] private Sprite icon;

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
    public Sprite Icon => icon;
    public int BaseCoins => GetBaseCoins();
    public int CurrentCoins => currentCoins;
    public float CurrentFlipChance => currentFlipChance;
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
    
    public override void AddBlock(float amount)
    {
        if (amount <= 0f) return;

        if (!isInCombat)
        {
            return;
        }

        base.AddBlock(amount);
    }
    
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
    
    public void ExitCombat()
    {
        if (isInCombat)
        {
            isInCombat = false;
            ClearBlock();
            currentFlipChance = BASE_FLIP_CHANCE;
            bonusCoinsNextTurn = 0;
            distanceMovedThisTurn = 0f;
            if (debugMode) Debug.Log("[Player] Exited combat");
            OnCombatStateChanged?.Invoke(false);
        }
    }
    
    public void StartTurn()
    {
        int coinsThisTurn = GetBaseCoins() + bonusCoinsNextTurn;
        currentCoins = coinsThisTurn;
        
        if (debugMode)
        {
            Debug.Log($"[Player] Turn started with {currentCoins} coins (base: {GetBaseCoins()}, bonus: {bonusCoinsNextTurn})");
        }

        bonusCoinsNextTurn = 0;
        
        distanceMovedThisTurn = 0f;

        OnCoinsChanged?.Invoke(currentCoins, GetBaseCoins());
    }
    
    public void EndTurn()
    {

    }
    
    public bool CanSpendCoin()
    {
        return CanSpendCoins(1);
    }
    
    public bool SpendCoin()
    {
        return SpendCoins(1);
    }
    
    public bool CanSpendCoins(int cost)
    {
        if (cost <= 0) return true;
        return currentCoins >= cost;
    }

    public bool SpendCoins(int cost)
    {
        if (cost <= 0) return true;
        if (CheatManager.Instance != null && CheatManager.Instance.InfiniteCoins)
        {
            if (debugMode) Debug.Log($"[Player] Coin spend of {cost} bypassed by Infinite Coins cheat.");
            return true;
        }
        if (currentCoins < cost) return false;

        currentCoins -= cost;
        if (debugMode)
        {
            Debug.Log($"[Player] Spent {cost} coin(s). Remaining: {currentCoins}");
        }
        OnCoinsChanged?.Invoke(currentCoins, GetBaseCoins());
        return true;
    }
    
    public bool CanMove()
    {
        if (!isInCombat) return true;

        return distanceMovedThisTurn < MaxCombatMoveDistance;
    }
    
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
    
    public float GetAllowedMoveDistance(float requestedDistance)
    {
        if (!isInCombat) return requestedDistance;
        
        float remaining = MaxCombatMoveDistance - distanceMovedThisTurn;
        return Mathf.Min(requestedDistance, remaining);
    }
    
    public bool PerformCoinFlip()
    {
        float roll = Random.Range(0f, 100f);
        bool success = roll < currentFlipChance;

        if (debugMode)
        {
            Debug.Log($"[Player] Coin Flip! Chance: {currentFlipChance:F0}%, Roll: {roll:F1} -> {(success ? "SUCCESS!" : "MISS!")}");
        }

        OnCoinFlipResult?.Invoke(success, currentFlipChance);
        
        if (success)
        {
            if (currentFlipChance > BASE_FLIP_CHANCE)
            {
                currentFlipChance = BASE_FLIP_CHANCE;
                if (debugMode)
                {
                    Debug.Log($"[Player] Success after fail streak! Flip chance reset to {currentFlipChance:F0}%");
                }
            }
            else
            {
                currentFlipChance = Mathf.Max(MIN_FLIP_CHANCE, currentFlipChance - SUCCESS_PENALTY);
                if (debugMode)
                {
                    Debug.Log($"[Player] Success streak continues. Next flip chance: {currentFlipChance:F0}%");
                }
            }
        }
        else
        {
            if (currentFlipChance < BASE_FLIP_CHANCE)
            {
                currentFlipChance = BASE_FLIP_CHANCE;
                if (debugMode)
                {
                    Debug.Log($"[Player] Fail after success streak! Flip chance reset to {currentFlipChance:F0}%");
                }
            }
            else
            {
                currentFlipChance = Mathf.Min(MAX_FLIP_CHANCE, currentFlipChance + FAIL_BONUS);
                if (debugMode)
                {
                    Debug.Log($"[Player] Fail streak continues. Next flip chance: {currentFlipChance:F0}%");
                }
            }
        }

        return success;
    }
    
    public override void TakeDamage(float amount)
    {
        if (CheatManager.Instance != null && CheatManager.Instance.InfiniteHealth)
        {
            if (debugMode) Debug.Log("[Player] TakeDamage blocked by Infinite Health cheat.");
            return;
        }
        float healthBefore = currentHealth;
        base.TakeDamage(amount);
        if (currentHealth < healthBefore)
        {
            TutorialManager.Instance?.Trigger("first_damage_taken");
        }
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

    private float GetMaxCombatMoveDistance()
    {
        if (StatsManager.Instance == null)
        {
            return baseCombatMoveDistance;
        }

        return StatsManager.Instance.GetMaxCombatMoveDistance();
    }
}