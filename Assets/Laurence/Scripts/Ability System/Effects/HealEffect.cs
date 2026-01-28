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
    public int baseHealAmount = 20;

    public override void Execute(AbilityExecutionContext context)
    {
        int finalHeal = Mathf.RoundToInt(baseHealAmount * context.AccumulatedMultiplier);

        if (targetSelf)
        {
            // Healing self
            if (context.Caster != null)
            {
                context.Caster.Heal(finalHeal);
                Debug.Log($"[HealEffect] Healed self for {finalHeal} (base: {baseHealAmount}, multiplier: {context.AccumulatedMultiplier:F2})");
            }
        }
        else
        {
            // Healing an enemy
            if (context.CurrentTarget != null)
            {
                context.CurrentTarget.Heal(finalHeal);
                Debug.Log($"[HealEffect] Healed {context.CurrentTarget.name} for {finalHeal}");
            }
            else
            {
                Debug.LogWarning("[HealEffect] No target to heal!");
            }
        }
    }
}