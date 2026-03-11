using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private InventoryItemButton itemPrefab;
    [SerializeField] private Button openInventoryButton;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Data")]
    [SerializeField] private ItemManager itemManager;

    private InputAction inventoryAction;
    private readonly List<InventoryItemButton> spawnedButtons = new List<InventoryItemButton>();

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (itemManager == null)
        {
            itemManager = FindFirstObjectByType<ItemManager>();
        }

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

    private void OnCombatStarted(System.Collections.Generic.List<Enemy> enemies)
    {
        SetInventoryInputEnabled(false);
        SetUIActive(false);
    }

    private void OnCombatEnded(CombatManager.CombatOutcome outcome)
    {
        SetInventoryInputEnabled(true);
    }

    private void SetInventoryInputEnabled(bool enabled)
    {
        if (inventoryAction != null)
        {
            if (enabled) inventoryAction.Enable();
            else inventoryAction.Disable();
        }

        if (openInventoryButton != null)
        {
            openInventoryButton.gameObject.SetActive(enabled);
        }
    }

    private void OnInventory(InputAction.CallbackContext context)
    {
        Toggle();
    }

    public void Toggle()
    {
        if (root == null)
        {
            return;
        }
        
        if (openInventoryButton != null)
        {
            openInventoryButton.gameObject.SetActive(!openInventoryButton.gameObject.activeSelf);
        }
        
        bool show = !root.activeSelf;
        SetUIActive(show);

        if (show)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (contentRoot == null || itemPrefab == null)
        {
            return;
        }

        if (itemManager == null)
        {
            itemManager = FindFirstObjectByType<ItemManager>();
            if (itemManager == null)
            {
                return;
            }
        }

        ClearButtons();

        Dictionary<ItemDefinition, int> equippedCounts = new Dictionary<ItemDefinition, int>();
        foreach (ItemDefinition equippedItem in itemManager.EquippedItems)
        {
            if (equippedItem == null)
            {
                continue;
            }

            if (!equippedCounts.ContainsKey(equippedItem))
            {
                equippedCounts[equippedItem] = 0;
            }

            equippedCounts[equippedItem]++;
        }

        Dictionary<ItemDefinition, int> usedEquippedCounts = new Dictionary<ItemDefinition, int>();

        foreach (ItemDefinition item in itemManager.InventoryItems)
        {
            if (item == null)
            {
                continue;
            }

            bool equipped = false;
            if (equippedCounts.TryGetValue(item, out int totalEquipped))
            {
                int usedCount = 0;
                if (usedEquippedCounts.TryGetValue(item, out int currentUsed))
                {
                    usedCount = currentUsed;
                }

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
    }

    private void HandleItemClicked(InventoryItemButton button)
    {
        if (button == null || itemManager == null)
        {
            return;
        }

        ItemDefinition item = button.Item;
        if (item == null)
        {
            return;
        }

        if (button.IsEquipped)
        {
            if (itemManager.Unequip(item))
            {
                button.SetEquipped(false);
            }

            return;
        }

        if (!itemManager.Equip(item))
        {
            return;
        }

        button.SetEquipped(true);
    }

    private void ClearButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
            {
                Destroy(spawnedButtons[i].gameObject);
            }
        }

        spawnedButtons.Clear();
    }

    private void SetUIActive(bool isActive)
    {
        if (root != null)
        {
            root.SetActive(isActive);
        }
    }
}