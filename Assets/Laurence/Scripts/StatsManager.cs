using UnityEngine;

public class StatsManager : MonoBehaviour
{
    [System.Serializable]
    public struct StatModifiers
    {
        [Header("Flat Mods")]
        public int maxHealthFlat;
        public int blockFlat;
        public int healFlat;
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
    public int maxHealth = 100;
    public int baseCoins = 5;
    public int carryoverCoins = 1;
    public float maxCombatMoveDistance = 5f;

    [Header("Run Modifiers")]
    [SerializeField] private StatModifiers modifiers;

    public StatModifiers Modifiers => modifiers;

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
    }

    public int GetMaxHealth()
    {
        int baseValue = maxHealth;
        return Mathf.Max(1, baseValue + modifiers.maxHealthFlat);
    }

    public int GetBaseCoins()
    {
        int baseValue = baseCoins;
        return Mathf.Max(0, baseValue + modifiers.baseCoinsFlat);
    }

    public int GetCarryoverCoins()
    {
        int baseValue = carryoverCoins;
        return Mathf.Max(0, baseValue + modifiers.carryoverCoinsFlat);
    }

    public float GetMaxCombatMoveDistance()
    {
        float baseValue = maxCombatMoveDistance;
        return Mathf.Max(0f, ApplyPercent(baseValue, modifiers.maxCombatMovePercent));
    }

    public float ApplyDamage(float baseDamage)
    {
        float scaled = ApplyPercent(baseDamage, modifiers.abilityDamagePercent);
        return Mathf.Max(0f, scaled);
    }

    public float ApplyBlock(float baseBlock)
    {
        return Mathf.Max(0f, baseBlock + modifiers.blockFlat);
    }

    public float ApplyHeal(float baseHeal)
    {
        return Mathf.Max(0f, baseHeal + modifiers.healFlat);
    }

    public float ApplyAbilityRange(float baseRange)
    {
        return Mathf.Max(0f, ApplyPercent(baseRange, modifiers.abilityRangePercent));
    }

    public float ApplyAoeSize(float baseValue)
    {
        return Mathf.Max(0f, ApplyPercent(baseValue, modifiers.aoeSizePercent));
    }

    private static float ApplyPercent(float baseValue, float percent)
    {
        return baseValue * (1f + (percent * 0.01f));
    }
}
