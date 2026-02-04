using UnityEngine;

/// <summary>
/// Adds block to the target
/// Block reduces incoming damage and clears at the start of your turn
/// Only functional during combat mode
/// </summary>
[CreateAssetMenu(fileName = "BlockEffect", menuName = "Abilities/Effects/Block")]
public class BlockEffect : AbilityEffect
{
    [Header("Block Settings")]
    [Tooltip("Base block amount before modifiers")]
    public int baseBlockAmount = 15;

    public override void Execute(AbilityExecutionContext context)
    {
        // TODO: Check if in combat mode - block does nothing outside combat
        float modifiedBase = baseBlockAmount;
        if (StatsManager.Instance != null)
        {
            modifiedBase = StatsManager.Instance.ApplyBlock(baseBlockAmount);
        }

        int finalBlock = Mathf.RoundToInt(modifiedBase * context.AccumulatedMultiplier);
        
        if (context.Caster != null)
        {
            context.Caster.AddBlock(finalBlock);
            Debug.Log($"[BlockEffect] Added {finalBlock} block to self (base: {baseBlockAmount}, multiplier: {context.AccumulatedMultiplier:F2})");
        }
    }
}
