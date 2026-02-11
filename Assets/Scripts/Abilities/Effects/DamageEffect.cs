using UnityEngine;

/// <summary>
/// Deals damage to the target
/// Damage is multiplied by any accumulated modifiers in the context
/// </summary>
[CreateAssetMenu(fileName = "DamageEffect", menuName = "Abilities/Effects/Damage")]
public class DamageEffect : AbilityEffect
{
    [Header("Damage Settings")]
    [Tooltip("Base damage before modifiers")]
    public float baseDamage = 10f;

    public override void Execute(AbilityExecutionContext context)
    {
        float modifiedBase = baseDamage;
        if (StatsManager.Instance != null)
        {
            modifiedBase = StatsManager.Instance.ApplyDamage(baseDamage);
        }

        float finalDamage = modifiedBase * context.AccumulatedMultiplier;

        if (targetSelf)
        {
            // Damage self
            if (context.Caster != null)
            {
                context.Caster.TakeDamage(finalDamage);
            }
        }
        else
        {
            // Damage the target
            if (context.CurrentTarget != null)
            {
                context.CurrentTarget.TakeDamage(finalDamage);
                if (context.CurrentTarget is Enemy)
                {
                    context.EnemyWasHit = true;
                }
                if (context.Caster is Player && context.CurrentTarget is Enemy enemyTarget)
                {
                    string abilityLabel = string.IsNullOrWhiteSpace(context.AbilityName) ? "ability" : context.AbilityName;
                    MessageUI.Instance?.EnqueueMessage(
                        $"You cast {abilityLabel} for {finalDamage:0.#} damage to {enemyTarget.name}.");
                }
                // Debug.Log($"[DamageEffect] Dealt {finalDamage} damage to {context.CurrentTarget.name} (base: {baseDamage}, multiplier: {context.AccumulatedMultiplier:F2})");
            }
        }
    }
}
