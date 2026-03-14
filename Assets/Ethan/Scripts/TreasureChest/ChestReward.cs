using UnityEngine;

//Reward type for treasure chest cards -EM//
public enum ChestRewardType
{
    Item,
    Ability
}

//Wrapper holding either an ItemDefinition or an Ability -EM//
[System.Serializable]
public struct ChestReward
{
    public ChestRewardType type;
    public ItemDefinition item; //Populated when type == Item//
    public Ability ability; //Populated when tpye == Ability//
}
