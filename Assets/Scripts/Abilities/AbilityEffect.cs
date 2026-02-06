using UnityEngine;

/// <summary>
/// Abstract base class for all ability effects
/// Effects are executed in order and can modify subsequent effects.
/// </summary>
public abstract class AbilityEffect : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("If true, this effect targets the caster. Otherwise targets units.")]
    public bool targetSelf = false;
    
    // Execute this effect within the given context
    public abstract void Execute(AbilityExecutionContext context);
}

/// <summary>
/// Runtime context for ability execution
/// Tracks the caster, current target, and any accumulated modifiers
/// </summary>
public class AbilityExecutionContext
{
    public Player Caster { get; private set; }
    public Unit CurrentTarget { get; set; }
    public Vector3 TargetPoint { get; set; }
    
    /// <summary>
    /// Accumulated damage/heal multiplier from conditional modifiers
    /// Persists through the entire ability execution
    /// </summary>
    public float AccumulatedMultiplier { get; set; } = 1f;

    /// <summary>
    /// Track if any enemy was hit
    /// </summary>
    public bool EnemyWasHit { get; set; } = false;

    public AbilityExecutionContext(Player caster)
    {
        Caster = caster;
        AccumulatedMultiplier = 1f;
    }

    /// <summary>
    /// Check if the caster is in light
    /// </summary>
    public bool IsCasterInLight()
    {
        if (LightDetectionManager.Instance == null) return false;
        
        PlayerController playerController = Caster.GetComponent<PlayerController>();
        Vector3 checkPoint = playerController != null 
            ? playerController.GetLightCheckPoint() 
            : Caster.transform.position;
            
        return LightDetectionManager.Instance.IsPointInLight(checkPoint);
    }

    /// <summary>
    /// Check if the current target is in light
    /// </summary>
    public bool IsTargetInLight()
    {
        if (LightDetectionManager.Instance == null) return false;
        if (CurrentTarget == null) return false;
        
        return LightDetectionManager.Instance.IsPointInLight(CurrentTarget.transform.position);
    }
}
