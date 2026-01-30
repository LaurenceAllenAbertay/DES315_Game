using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How this ability selects its targets
/// </summary>
public enum TargetingType
{
    // Click on a specific enemy to target them
    PointAndClick,
    
    // Cone emanating from the player toward the mouse cursor
    Cone,
    
    // Circle placed on the ground at the mouse position
    RangedAOE
}

/// <summary>
/// Defines an ability that can be equipped and used by the player
/// </summary>
[CreateAssetMenu(fileName = "NewAbility", menuName = "Abilities/Ability")]
public class Ability : ScriptableObject
{
    [Header("Basic Info")]
    public string abilityName = "New Ability";
    [TextArea(2, 4)]
    public string description = "";
    public Sprite icon;

    [Header("Targeting")]
    public TargetingType targetingType = TargetingType.PointAndClick;
    
    [Tooltip("Maximum range for this ability")]
    public float range = 5f;
    
    [Tooltip("Angle of the cone in degrees (only for Cone targeting)")]
    [Range(10f, 180f)]
    public float coneAngle = 60f;
    
    [Tooltip("Radius of the AOE circle (only for RangedAOE targeting)")]
    public float aoeRadius = 3f;

    [Header("Effects")]
    [Tooltip("Effects are executed in order. Modifiers affect subsequent effects.")]
    public List<AbilityEffect> effects = new List<AbilityEffect>();

    [Header("Cost")]
    [Tooltip("Action point cost during turn-based combat")]
    public int actionPointCost = 1;

    /// <summary>
    /// Execute this ability on a single target
    /// </summary>
    public void Execute(Player caster, Enemy target)
    {
        AbilityExecutionContext context = new AbilityExecutionContext(caster);
        context.CurrentTarget = target;

        ExecuteEffects(context);

        if (context.EnemyWasHit)
        {
            TriggerCombat(target);
        }
    }

    /// <summary>
    /// Execute this ability on multiple targets
    /// </summary>
    public void Execute(Player caster, List<Enemy> targets)
    {
        if (targets == null || targets.Count == 0)
        {
            // No targets, might be a self-only ability
            AbilityExecutionContext context = new AbilityExecutionContext(caster);
            ExecuteEffects(context);
            return;
        }

        bool anyEnemyHit = false;

        foreach (Enemy target in targets)
        {
            AbilityExecutionContext context = new AbilityExecutionContext(caster);
            context.CurrentTarget = target;
            
            ExecuteEffects(context);

            if (context.EnemyWasHit)
            {
                anyEnemyHit = true;
            }
        }

        if (anyEnemyHit)
        {
            // Trigger combat with targets
            // Later on trigger combat with all enemies in range of the targeted enemy
            foreach (var target in targets)
            {
                TriggerCombat(target);   
            }
        }
    }

    /// <summary>
    /// Execute this ability at a point
    /// </summary>
    public void Execute(Player caster, Vector3 targetPoint)
    {
        AbilityExecutionContext context = new AbilityExecutionContext(caster);
        context.TargetPoint = targetPoint;

        ExecuteEffects(context);
    }

    private void ExecuteEffects(AbilityExecutionContext context)
    {
        Debug.Log($"[Ability] Executing '{abilityName}' with {effects.Count} effects");

        foreach (AbilityEffect effect in effects)
        {
            if (effect == null)
            {
                Debug.LogWarning($"[Ability] Null effect in ability '{abilityName}'");
                continue;
            }

            effect.Execute(context);
        }
    }

    private void TriggerCombat(Enemy enemy)
    {
        Debug.Log($"[Ability] Combat triggered! Enemy hit: {enemy.name}");
        
        CombatManager.Instance?.StartCombatFromEnemy(enemy);
    }

    /// <summary>
    /// Check if this ability has any self-targeting effects
    /// </summary>
    public bool HasSelfTargetingEffects()
    {
        foreach (var effect in effects)
        {
            if (effect != null && effect.targetSelf)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Check if this ability ONLY has self-targeting effects
    /// </summary>
    public bool IsOnlySelfTargeting()
    {
        if (effects.Count == 0) return false;

        foreach (var effect in effects)
        {
            if (effect == null) continue;
            
            if (effect is ConditionalModifierEffect) continue;
            
            if (!effect.targetSelf) return false;
        }
        return true;
    }
}
