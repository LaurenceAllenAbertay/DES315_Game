using System;
using UnityEngine;

public class StatsManager : MonoBehaviour
{
    [System.Serializable]
    public struct StatModifiers
    {
        [Header("Flat Mods")]
        public float maxHealthFlat;
        public float blockFlat;
        public float healFlat;
        public int baseCoinsFlat;
        public int carryoverCoinsFlat;

        [Header("Percent Mods")]
        public float abilityDamagePercent;
        public float abilityRangePercent;
        public float aoeSizePercent;
        public float maxCombatMovePercent;
    }

    public static StatsManager Instance { get; private set; }

    [Header("Base Player Stats")]
    public float maxHealth = 100f;
    public int baseCoins = 5;
    public int carryoverCoins = 1;
    public float maxCombatMoveDistance = 5f;

    [Header("Run Modifiers")]
    [SerializeField] private StatModifiers modifiers;
    [SerializeField] private StatModifiers itemModifiers;

    public StatModifiers Modifiers => AddModifiers(modifiers, itemModifiers);
    public StatModifiers RunModifiers => modifiers;
    public StatModifiers ItemModifiers => itemModifiers;

    public event Action OnModifiersChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ResetRunModifiers()
    {
        modifiers = default;
        OnModifiersChanged?.Invoke();
    }

    public void SetItemModifiers(StatModifiers newModifiers)
    {
        itemModifiers = newModifiers;
        OnModifiersChanged?.Invoke();
    }

    public float GetMaxHealth()
    {
        float baseValue = maxHealth;
        StatModifiers total = Modifiers;
        return Mathf.Max(1f, baseValue + total.maxHealthFlat);
    }

    public int GetBaseCoins()
    {
        int baseValue = baseCoins;
        StatModifiers total = Modifiers;
        return Mathf.Max(0, baseValue + total.baseCoinsFlat);
    }

    public int GetCarryoverCoins()
    {
        int baseValue = carryoverCoins;
        StatModifiers total = Modifiers;
        return Mathf.Max(0, baseValue + total.carryoverCoinsFlat);
    }

    public float GetMaxCombatMoveDistance()
    {
        float baseValue = maxCombatMoveDistance;
        StatModifiers total = Modifiers;
        return Mathf.Max(0f, ApplyPercent(baseValue, total.maxCombatMovePercent));
    }

    public float ApplyDamage(float baseDamage)
    {
        StatModifiers total = Modifiers;
        float scaled = ApplyPercent(baseDamage, total.abilityDamagePercent);
        return Mathf.Max(0f, scaled);
    }

    public float ApplyBlock(float baseBlock)
    {
        StatModifiers total = Modifiers;
        return Mathf.Max(0f, baseBlock + total.blockFlat);
    }

    public float ApplyHeal(float baseHeal)
    {
        StatModifiers total = Modifiers;
        return Mathf.Max(0f, baseHeal + total.healFlat);
    }

    public float ApplyAbilityRange(float baseRange)
    {
        StatModifiers total = Modifiers;
        return Mathf.Max(0f, ApplyPercent(baseRange, total.abilityRangePercent));
    }

    public float ApplyAoeSize(float baseValue)
    {
        StatModifiers total = Modifiers;
        return Mathf.Max(0f, ApplyPercent(baseValue, total.aoeSizePercent));
    }

    public static StatModifiers AddModifiers(StatModifiers a, StatModifiers b)
    {
        return new StatModifiers
        {
            maxHealthFlat = a.maxHealthFlat + b.maxHealthFlat,
            blockFlat = a.blockFlat + b.blockFlat,
            healFlat = a.healFlat + b.healFlat,
            baseCoinsFlat = a.baseCoinsFlat + b.baseCoinsFlat,
            carryoverCoinsFlat = a.carryoverCoinsFlat + b.carryoverCoinsFlat,
            abilityDamagePercent = a.abilityDamagePercent + b.abilityDamagePercent,
            abilityRangePercent = a.abilityRangePercent + b.abilityRangePercent,
            aoeSizePercent = a.aoeSizePercent + b.aoeSizePercent,
            maxCombatMovePercent = a.maxCombatMovePercent + b.maxCombatMovePercent
        };
    }

    private static float ApplyPercent(float baseValue, float percent)
    {
        return baseValue * (1f + (percent * 0.01f));
    }
}
