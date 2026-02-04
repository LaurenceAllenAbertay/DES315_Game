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
    public int baseDamage = 10;

    public override void Execute(AbilityExecutionContext context)
    {
        float modifiedBase = baseDamage;
        if (StatsManager.Instance != null)
        {
            modifiedBase = StatsManager.Instance.ApplyDamage(baseDamage);
        }

        int finalDamage = Mathf.RoundToInt(modifiedBase * context.AccumulatedMultiplier);

        if (targetSelf)
        {
            // Damage self
            if (context.Caster != null)
            {
                context.Caster.TakeDamage(finalDamage);
                Debug.Log($"[DamageEffect] Self damage: {finalDamage} (base: {baseDamage}, multiplier: {context.AccumulatedMultiplier:F2})");
            }
        }
        else
        {
            // Damage the enemy target
            if (context.CurrentTarget != null)
            {
                context.CurrentTarget.TakeDamage(finalDamage);
                context.EnemyWasHit = true;
                Debug.Log($"[DamageEffect] Dealt {finalDamage} damage to {context.CurrentTarget.name} (base: {baseDamage}, multiplier: {context.AccumulatedMultiplier:F2})");
            }
            else
            {
                Debug.Log("[DamageEffect] No target to damage!");
            }
        }
    }
}
