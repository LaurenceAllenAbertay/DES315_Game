using UnityEngine;

/// <summary>
/// Heals the target
/// Heal amount is multiplied by any accumulated modifiers in the context
/// </summary>
[CreateAssetMenu(fileName = "HealEffect", menuName = "Abilities/Effects/Heal")]
public class HealEffect : AbilityEffect
{
    [Header("Heal Settings")]
    [Tooltip("Base heal amount before modifiers")]
    public float baseHealAmount = 20f;

    public override void Execute(AbilityExecutionContext context)
    {
        float modifiedBase = baseHealAmount;
        if (StatsManager.Instance != null)
        {
            modifiedBase = StatsManager.Instance.ApplyHeal(baseHealAmount);
        }

        float finalHeal = modifiedBase * context.AccumulatedMultiplier;

        if (targetSelf)
        {
            // Healing self
            if (context.Caster != null)
            {
                context.Caster.Heal(finalHeal);
            }
        }
        else
        {
            // Healing another target
            if (context.CurrentTarget != null)
            {
                context.CurrentTarget.Heal(finalHeal);
            }
        }
    }
}
