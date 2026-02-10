using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private List<ItemDefinition> inventoryItems = new List<ItemDefinition>();
    [SerializeField] private int maxEquipped = 5;

    [Header("Equipped Items")]
    [SerializeField] private List<ItemDefinition> equippedItems = new List<ItemDefinition>();

    private StatsManager statsManager;
    private bool modifiersApplied;

    public IReadOnlyList<ItemDefinition> InventoryItems => inventoryItems;
    public IReadOnlyList<ItemDefinition> EquippedItems => equippedItems;
    public int MaxEquipped => maxEquipped;

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
        if (item == null)
        {
            return false;
        }

        if (equippedItems.Count >= maxEquipped)
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

    public bool AddItem(ItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        inventoryItems.Add(item);
        return true;
    }

    public bool RemoveItem(ItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        return inventoryItems.Remove(item);
    }

    private void ApplyEquippedModifiers()
    {
        if (statsManager == null)
        {
            statsManager = StatsManager.Instance;
            if (statsManager == null)
            {
                statsManager = FindFirstObjectByType<StatsManager>();
            }
        }

        if (statsManager == null)
        {
            Debug.LogWarning("[ItemManager] No StatsManager found; item modifiers not applied.");
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
        if (modifiersApplied || equippedItems.Count > 0)
        {
            ApplyEquippedModifiers();
        }
    }
}
