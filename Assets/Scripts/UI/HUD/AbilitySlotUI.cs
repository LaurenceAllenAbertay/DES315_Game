using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class AbilitySlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAbilityManager abilityManager;

    [Header("Ability Buttons (slots 0, 1, 2)")]
    [SerializeField] private Button slot1Button;
    [SerializeField] private Button slot2Button;
    [SerializeField] private Button slot3Button;

    [Header("Slot Icon Images")]
    [SerializeField] private Image slot1Icon;
    [SerializeField] private Image slot2Icon;
    [SerializeField] private Image slot3Icon;

    [Header("Description")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Active Slot Highlight")]
    [SerializeField] private Color activeColor = new Color(1f, 0.8f, 0f, 1f);
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color dimmedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [SerializeField] private GameObject background;
    
    private Button[] buttons;
    private Image[] slotIcons;
    private bool inSwapMode = false;
    private System.Action<int> swapCallback = null;

    private void Awake()
    {
        if (abilityManager == null)
            abilityManager = FindFirstObjectByType<PlayerAbilityManager>();

        buttons = new Button[] { slot1Button, slot2Button, slot3Button };
        slotIcons = new Image[] { slot1Icon, slot2Icon, slot3Icon };
    }

    private void OnEnable()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            int index = i;
            buttons[i].onClick.AddListener(() => HandleSlotButtonClicked(index));
            AddHoverEvents(buttons[i], index);
        }

        if (abilityManager != null)
            abilityManager.OnActiveSlotChanged += HandleActiveSlotChanged;

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCombatStarted += HandleCombatStarted;
            CombatManager.Instance.OnCombatEnded += HandleCombatEnded;
            CombatManager.Instance.OnTurnStarted += HandleTurnStarted;
        }

        RefreshIcons();
        UpdateVisibility();
    }

    private void OnDisable()
    {
        foreach (Button b in buttons)
        {
            if (b != null) b.onClick.RemoveAllListeners();
        }

        if (abilityManager != null)
            abilityManager.OnActiveSlotChanged -= HandleActiveSlotChanged;

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCombatStarted -= HandleCombatStarted;
            CombatManager.Instance.OnCombatEnded -= HandleCombatEnded;
            CombatManager.Instance.OnTurnStarted -= HandleTurnStarted;
        }

        if (descriptionText != null)
            descriptionText.text = string.Empty;
    }

    /// <summary>
    /// Adds pointer enter/exit EventTrigger entries to a button for hover description display.
    /// Clears existing triggers first to avoid duplicates on re-enable.
    /// </summary>
    private void AddHoverEvents(Button button, int slotIndex)
    {
        EventTrigger trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => ShowDescription(slotIndex));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => ClearDescription());
        trigger.triggers.Add(exitEntry);
    }

    public void RefreshIcons()
    {
        if (abilityManager == null) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            Ability ability = i < abilityManager.equippedAbilities.Length ? abilityManager.equippedAbilities[i] : null;
            Sprite icon = ability != null ? ability.icon : null;

            if (slotIcons[i] != null)
                slotIcons[i].sprite = icon;
            else
                buttons[i].image.sprite = icon;
        }
    }

    private void ShowDescription(int slotIndex)
    {
        if (inSwapMode) return;
        if (descriptionText == null || abilityManager == null) return;
        Ability ability = slotIndex < abilityManager.equippedAbilities.Length ? abilityManager.equippedAbilities[slotIndex] : null;
        descriptionText.text = ability != null ? ability.description : string.Empty;
    }

    private void HandleSlotButtonClicked(int index)
    {
        if (inSwapMode)
        {
            swapCallback?.Invoke(index);
            ExitSwapMode();
        }
        else
        {
            abilityManager?.ActivateAbilitySlot(index);
        }
    }

    public void EnterSwapMode(Ability incomingAbility, System.Action<int> onSlotSelected)
    {
        inSwapMode = true;
        swapCallback = onSlotSelected;
        if (descriptionText != null)
            descriptionText.text = "Select an ability to swap out...";
    }

    private void ExitSwapMode()
    {
        inSwapMode = false;
        swapCallback = null;
        ClearDescription();
    }

    private void ClearDescription()
    {
        if (descriptionText != null)
            descriptionText.text = string.Empty;
    }

    private void HandleActiveSlotChanged(int? activeSlot)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            bool isSelected = activeSlot.HasValue && activeSlot.Value == i;
            bool isInactive = activeSlot.HasValue && !isSelected;

            ColorBlock colors = buttons[i].colors;
            colors.normalColor = isSelected ? activeColor : normalColor;
            buttons[i].colors = colors;

            if (slotIcons[i] != null)
                slotIcons[i].color = isInactive ? dimmedColor : Color.white;
        }
    }

    private void UpdateVisibility()
    {
        if (CombatManager.Instance == null || !CombatManager.Instance.InCombat)
        {
            SetButtonsVisible(true);
            return;
        }

        background.SetActive(CombatManager.Instance.IsPlayerTurn);
        SetButtonsVisible(CombatManager.Instance.IsPlayerTurn);
    }

    private void SetButtonsVisible(bool visible)
    {
        foreach (Button b in buttons)
        {
            if (b != null) b.gameObject.SetActive(visible);
        }
        if (background != null) background.SetActive(visible);
    }

    private void HandleCombatStarted(List<Enemy> enemies) => SetButtonsVisible(false);

    private void HandleCombatEnded(CombatManager.CombatOutcome outcome) => SetButtonsVisible(true);

    private void HandleTurnStarted(Unit unit) => SetButtonsVisible(unit is Player);
}