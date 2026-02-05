using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Items/Item")]
public class ItemDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName = "New Item";
    [TextArea(2, 4)]
    public string description = "";
    public Sprite icon;

    [Header("Stat Modifiers")]
    public StatsManager.StatModifiers modifiers;
}
