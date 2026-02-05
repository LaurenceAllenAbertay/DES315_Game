using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    [Header("Equipped Items")]
    [SerializeField] private List<ItemDefinition> equippedItems = new List<ItemDefinition>();

    private StatsManager statsManager;
    private bool modifiersApplied;

    public IReadOnlyList<ItemDefinition> EquippedItems => equippedItems;

    private void Awake()
    {
        statsManager = StatsManager.Instance;
    }

    private void Start()
    {
        ApplyEquippedModifiers();
    }

    public bool Equip(ItemDefinition item)
    {
        if (item == null || equippedItems.Contains(item))
        {
            return false;
        }

        equippedItems.Add(item);
        ApplyEquippedModifiers();
        return true;
    }

    public bool Unequip(ItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        bool removed = equippedItems.Remove(item);
        if (removed)
        {
            ApplyEquippedModifiers();
        }

        return removed;
    }

    public void ClearEquipped()
    {
        if (equippedItems.Count == 0)
        {
            return;
        }

        equippedItems.Clear();
        ApplyEquippedModifiers();
    }

    private void ApplyEquippedModifiers()
    {
        if (statsManager == null)
        {
            statsManager = StatsManager.Instance;
        }

        if (statsManager == null)
        {
            return;
        }

        StatsManager.StatModifiers total = default;

        foreach (ItemDefinition item in equippedItems)
        {
            if (item == null)
            {
                continue;
            }

            total = StatsManager.AddModifiers(total, item.modifiers);
        }

        statsManager.SetItemModifiers(total);
        modifiersApplied = true;
    }

    private void OnEnable()
    {
        if (modifiersApplied)
        {
            ApplyEquippedModifiers();
        }
    }
}
