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
    
    public abstract void Execute(AbilityExecutionContext context);
}

/// <summary>
/// Runtime context for ability execution
/// Tracks the caster, current target, and any accumulated modifiers
/// </summary>
public class AbilityExecutionContext
{
    public Player Caster { get; private set; }
    public string AbilityName { get; private set; }
    public Unit CurrentTarget { get; set; }
    public Vector3 TargetPoint { get; set; }
    public bool FlipUsed { get; private set; }
    public bool FlipSuccess { get; private set; }

    /// <summary>
    /// Accumulated damage/heal multiplier from conditional modifiers
    /// Persists through the entire ability execution
    /// </summary>
    public float AccumulatedMultiplier { get; set; } = 1f;

    /// <summary>
    /// Track if any enemy was hit
    /// </summary>
    public bool EnemyWasHit { get; set; } = false;

    public AbilityExecutionContext(Player caster, string abilityName, float baseMultiplier = 1f)
    {
        Caster = caster;
        AbilityName = abilityName;
        AccumulatedMultiplier = baseMultiplier;
        if (Mathf.Approximately(baseMultiplier, 1.5f))
        {
            FlipUsed = true;
            FlipSuccess = true;
        }
        else if (Mathf.Approximately(baseMultiplier, 0.5f))
        {
            FlipUsed = true;
            FlipSuccess = false;
        }
        else
        {
            FlipUsed = false;
            FlipSuccess = false;
        }
    }

    public bool IsCasterInLight()
    {
        LightDetectable detectable = Caster.GetComponent<LightDetectable>();
        return detectable != null && detectable.IsInLight;
    }

    public bool IsTargetInLight()
    {
        if (CurrentTarget == null) return false;
        LightDetectable detectable = CurrentTarget.GetComponent<LightDetectable>();
        return detectable != null && detectable.IsInLight;
    }
}