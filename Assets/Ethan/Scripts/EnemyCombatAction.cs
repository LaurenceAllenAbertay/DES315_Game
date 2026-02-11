using UnityEngine;

//The type of action an enemy can take on its turn -EM//
public enum EnemyActionType
{
    Attack, //Deal damage to the palyer//
    Defend, //Add block to self//
    Heal, //Restore own health//
    SpecialAttack //Bonus damage hit, only used when the enemy is healthy//
}

//A single combat action available to an enemy -EM//
//Create via Assets > Create > Enemy > Combat Action and assign to EnemyCombatAI//
//Resuses coin flip mechanic//

[CreateAssetMenu(fileName = "NewEnemyAction", menuName = "Enemy/Combat Action")]
public class EnemyCombatAction : ScriptableObject
{
    [Header("Identity")]
    public string actionName = "New Action";
    [TextArea(1, 3)]
    public string description = "";
    public EnemyActionType actionType = EnemyActionType.Attack;

    [Header("Values")]
    [Tooltip("Base damage, heal, or block amount before any flip multiplier")]
    public float baseValue = 10f;

    [Tooltip("For SpecialAttack only: multiplier applied on top of baseValue on a successful flip")]
    public float specialMultiplier = 1.5f;

    [Header("Coin Flip")]
    [Tooltip("Starting flip chance (0-100). Adjusts each flip just like the player's system.")]
    [Range(0f, 100f)]
    public float startingFlipChance = 60f;

    [Tooltip("How much the flip chance increases after a miss")]
    [Range(0f, 30f)]
    public float failBonus = 10f;

    [Tooltip("How much the flip chance decreases after a hit")]
    [Range(0f, 30f)]
    public float successPenalty = 5f;

    [Header("Decision Tree Conditions")]
    [Tooltip("Only consider this action when health % is AT OR BELOW this. Set to 1 to always consider it.")]
    [Range(0f, 1f)]
    public float healthThresholdMax = 1f;

    [Tooltip("Only consider this action when health % is AT OR ABOVE this. Useful for actions only available when healthy.")]
    [Range(0f, 1f)]
    public float healthThresholdMin = 0f;

    [Tooltip("Weight when multiple actions of the same type are valid. Higher = chosen more often.")]
    [Range(1, 10)]
    public int selectionWeight = 1;
}
