using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attaches UI buttons to the player's ability slots.
/// Assign one Button per slot in the Inspector; each click calls
/// PlayerAbilityManager.ActivateAbilitySlot(slotIndex).
/// </summary>
public class AbilitySlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAbilityManager abilityManager;

    [Header("Ability Buttons (slots 0, 1, 2)")]
    [SerializeField] private Button slot1Button;
    [SerializeField] private Button slot2Button;
    [SerializeField] private Button slot3Button;

    [Header("Optional Labels (show ability name)")]
    [SerializeField] private TextMeshProUGUI slot1Label;
    [SerializeField] private TextMeshProUGUI slot2Label;
    [SerializeField] private TextMeshProUGUI slot3Label;

    private void Awake()
    {
        if (abilityManager == null)
            abilityManager = FindFirstObjectByType<PlayerAbilityManager>();
    }

    private void OnEnable()
    {
        if (slot1Button != null) slot1Button.onClick.AddListener(() => abilityManager?.ActivateAbilitySlot(0));
        if (slot2Button != null) slot2Button.onClick.AddListener(() => abilityManager?.ActivateAbilitySlot(1));
        if (slot3Button != null) slot3Button.onClick.AddListener(() => abilityManager?.ActivateAbilitySlot(2));

        RefreshLabels();
    }

    private void OnDisable()
    {
        if (slot1Button != null) slot1Button.onClick.RemoveAllListeners();
        if (slot2Button != null) slot2Button.onClick.RemoveAllListeners();
        if (slot3Button != null) slot3Button.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Updates each button label to show the equipped ability name (if a label is assigned).
    /// Call this whenever abilities are re-equipped.
    /// </summary>
    public void RefreshLabels()
    {
        if (abilityManager == null) return;

        SetLabel(slot1Label, abilityManager.equippedAbilities.Length > 0 ? abilityManager.equippedAbilities[0] : null, "Slot 1");
        SetLabel(slot2Label, abilityManager.equippedAbilities.Length > 1 ? abilityManager.equippedAbilities[1] : null, "Slot 2");
        SetLabel(slot3Label, abilityManager.equippedAbilities.Length > 2 ? abilityManager.equippedAbilities[2] : null, "Slot 3");
    }

    private static void SetLabel(TextMeshProUGUI label, Ability ability, string fallback)
    {
        if (label == null) return;
        label.text = ability != null ? ability.abilityName : fallback;
    }
}