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

    [Header("Description")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Active Slot Highlight")]
    [SerializeField] private Color activeColor = new Color(1f, 0.8f, 0f, 1f);
    [SerializeField] private Color normalColor = Color.white;

    [SerializeField] private GameObject background;
    
    private Button[] buttons;

    private void Awake()
    {
        if (abilityManager == null)
            abilityManager = FindFirstObjectByType<PlayerAbilityManager>();

        buttons = new Button[] { slot1Button, slot2Button, slot3Button };
    }

    private void OnEnable()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            int index = i;
            buttons[i].onClick.AddListener(() => abilityManager?.ActivateAbilitySlot(index));
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
            buttons[i].image.sprite = ability != null ? ability.icon : null;
        }
    }

    private void ShowDescription(int slotIndex)
    {
        if (descriptionText == null || abilityManager == null) return;
        Ability ability = slotIndex < abilityManager.equippedAbilities.Length ? abilityManager.equippedAbilities[slotIndex] : null;
        descriptionText.text = ability != null ? ability.description : string.Empty;
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
            ColorBlock colors = buttons[i].colors;
            colors.normalColor = activeSlot.HasValue && activeSlot.Value == i ? activeColor : normalColor;
            buttons[i].colors = colors;
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