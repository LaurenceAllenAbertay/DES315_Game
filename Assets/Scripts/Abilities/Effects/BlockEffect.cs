using UnityEngine;

/// <summary>
/// Adds block to the target
/// Doesn't do anything outside of combat
/// </summary>
[CreateAssetMenu(fileName = "BlockEffect", menuName = "Abilities/Effects/Block")]
public class BlockEffect : AbilityEffect
{
    [Header("Block Settings")]
    [Tooltip("Base block amount before modifiers")]
    public float baseBlockAmount = 15f;

    public override void Execute(AbilityExecutionContext context)
    {
        float modifiedBase = baseBlockAmount;
        // Get the bonus block from the stats manager
        if (StatsManager.Instance != null)
        {
            modifiedBase = StatsManager.Instance.ApplyBlock(baseBlockAmount);
        }

        // Multiple effect of block by any multiplier
        float finalBlock = modifiedBase * context.AccumulatedMultiplier;
        float roundedBlock = Mathf.Ceil(finalBlock);
        
        // Apply block to the caster
        if (context.Caster != null)
        {
            context.Caster.AddBlock(roundedBlock);
            if (context.Caster is Player)
            {
                MessageUI.Instance?.EnqueueMessage($"You gained {roundedBlock:0} block.");
            }
            // Debug.Log($"[BlockEffect] Added {finalBlock} block to self (base: {baseBlockAmount}, multiplier: {context.AccumulatedMultiplier:F2})");
        }
    }
}
