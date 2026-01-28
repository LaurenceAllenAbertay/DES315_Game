using UnityEngine;

/// <summary>
/// Conditions for the modifier to activate
/// </summary>
public enum LightCondition
{
    CasterInLight,
    CasterInShadow,
    TargetInLight,
    TargetInShadow
}

/// <summary>
/// Conditionally modifies subsequent effects in the ability based on light/shadow state
/// </summary>
[CreateAssetMenu(fileName = "ConditionalModifier", menuName = "Abilities/Effects/Conditional Modifier")]
public class ConditionalModifierEffect : AbilityEffect
{
    [Header("Condition")]
    [Tooltip("What condition must be true for this modifier to apply")]
    public LightCondition condition = LightCondition.CasterInShadow;

    [Header("Modifier")]
    [Tooltip("Multiplier to apply to subsequent effects if condition is met")]
    public float multiplier = 1.5f;

    public override void Execute(AbilityExecutionContext context)
    {
        bool conditionMet = EvaluateCondition(context);

        if (conditionMet)
        {
            context.AccumulatedMultiplier *= multiplier;
            Debug.Log($"[ConditionalModifier] Condition '{condition}' met! Multiplier now: {context.AccumulatedMultiplier:F2}");
        }
        else
        {
            Debug.Log($"[ConditionalModifier] Condition '{condition}' not met. Multiplier unchanged: {context.AccumulatedMultiplier:F2}");
        }
    }

    private bool EvaluateCondition(AbilityExecutionContext context)
    {
        switch (condition)
        {
            case LightCondition.CasterInLight:
                return context.IsCasterInLight();

            case LightCondition.CasterInShadow:
                return !context.IsCasterInLight();

            case LightCondition.TargetInLight:
                return context.IsTargetInLight();

            case LightCondition.TargetInShadow:
                return !context.IsTargetInLight();

            default:
                return false;
        }
    }
}