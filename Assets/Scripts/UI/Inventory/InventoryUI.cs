using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private InventoryItemButton itemPrefab;
    [SerializeField] private GameObject inventoryButton;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Data")]
    [SerializeField] private ItemManager itemManager;
    [SerializeField] private PlayerAbilityManager abilityManager;
    [SerializeField] private AbilitySlotUI abilitySlotUI;

    private InputAction inventoryAction;
    private readonly List<InventoryItemButton> spawnedButtons = new List<InventoryItemButton>();

    private void Awake()
    {
        if (root == null) root = gameObject;

        if (itemManager == null) itemManager = FindFirstObjectByType<ItemManager>();
        if (abilityManager == null) abilityManager = FindFirstObjectByType<PlayerAbilityManager>();
        if (abilitySlotUI == null) abilitySlotUI = FindFirstObjectByType<AbilitySlotUI>();

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            inventoryAction = playerMap != null ? playerMap.FindAction("Inventory") : null;
        }

        SetUIActive(false);
    }

    private void OnEnable()
    {
        if (inventoryAction != null)
        {
            inventoryAction.performed += OnInventory;
            inventoryAction.Enable();
        }

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCombatStarted += OnCombatStarted;
            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
        }
    }

    private void OnDisable()
    {
        if (inventoryAction != null)
        {
            inventoryAction.performed -= OnInventory;
            inventoryAction.Disable();
        }

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCombatStarted -= OnCombatStarted;
            CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
        }
    }

    private void OnCombatStarted(List<Enemy> enemies)
    {
        SetUIActive(false);
        if (inventoryButton != null) inventoryButton.SetActive(false);
    }

    private void OnCombatEnded(CombatManager.CombatOutcome outcome)
    {
        if (inventoryButton != null) inventoryButton.SetActive(true);
    }

    private void OnInventory(InputAction.CallbackContext context) => Toggle();

    public void Toggle()
    {
        if (root == null) return;
        if (CombatManager.Instance != null && CombatManager.Instance.InCombat) return;

        bool show = !root.activeSelf;
        SetUIActive(show);

        if (show) Refresh();
    }

    public void Refresh()
    {
        if (contentRoot == null || itemPrefab == null) return;

        if (itemManager == null)
        {
            itemManager = FindFirstObjectByType<ItemManager>();
            if (itemManager == null) return;
        }

        ClearButtons();

        Dictionary<ItemDefinition, int> equippedCounts = new Dictionary<ItemDefinition, int>();
        foreach (ItemDefinition equippedItem in itemManager.EquippedItems)
        {
            if (equippedItem == null) continue;
            if (!equippedCounts.ContainsKey(equippedItem)) equippedCounts[equippedItem] = 0;
            equippedCounts[equippedItem]++;
        }

        Dictionary<ItemDefinition, int> usedEquippedCounts = new Dictionary<ItemDefinition, int>();

        foreach (ItemDefinition item in itemManager.InventoryItems)
        {
            if (item == null) continue;

            bool equipped = false;
            if (equippedCounts.TryGetValue(item, out int totalEquipped))
            {
                int usedCount = 0;
                if (usedEquippedCounts.TryGetValue(item, out int currentUsed)) usedCount = currentUsed;
                if (usedCount < totalEquipped)
                {
                    equipped = true;
                    usedEquippedCounts[item] = usedCount + 1;
                }
            }

            InventoryItemButton instance = Instantiate(itemPrefab, contentRoot);
            instance.Setup(item, equipped, HandleItemClicked);
            spawnedButtons.Add(instance);
        }

        if (abilityManager == null) abilityManager = FindFirstObjectByType<PlayerAbilityManager>();

        if (abilityManager != null)
        {
            foreach (Ability ab in abilityManager.inventoryAbilities)
            {
                if (ab == null) continue;
                InventoryItemButton instance = Instantiate(itemPrefab, contentRoot);
                instance.SetupAbility(ab, HandleAbilityClicked);
                spawnedButtons.Add(instance);
            }
        }
    }

    private void HandleAbilityClicked(InventoryItemButton button)
    {
        if (button?.AbilityItem == null || abilityManager == null) return;

        Ability ability = button.AbilityItem;

        if (abilitySlotUI == null) abilitySlotUI = FindFirstObjectByType<AbilitySlotUI>();
        if (abilitySlotUI == null) return;

        SetUIActive(false);

        abilitySlotUI.EnterSwapMode(ability, (slotIndex) =>
        {
            Ability old = slotIndex < abilityManager.equippedAbilities.Length
                ? abilityManager.equippedAbilities[slotIndex] : null;

            abilityManager.equippedAbilities[slotIndex] = ability;
            abilityManager.RemoveAbilityFromInventory(ability);
            if (old != null) abilityManager.AddAbilityToInventory(old);

            abilitySlotUI.RefreshIcons();
        });
    }

    private void HandleItemClicked(InventoryItemButton button)
    {
        if (button == null || itemManager == null) return;

        ItemDefinition item = button.Item;
        if (item == null) return;

        if (button.IsEquipped)
        {
            if (itemManager.Unequip(item)) button.SetEquipped(false);
            return;
        }

        if (!itemManager.Equip(item)) return;
        button.SetEquipped(true);
    }

    private void ClearButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null) Destroy(spawnedButtons[i].gameObject);
        }
        spawnedButtons.Clear();
    }

    private void SetUIActive(bool isActive)
    {
        if (root != null) root.SetActive(isActive);
        if (inventoryButton != null) inventoryButton.SetActive(!isActive);
        Debug.Log("Inventory UI active: " + inventoryButton.activeSelf);
    }
}