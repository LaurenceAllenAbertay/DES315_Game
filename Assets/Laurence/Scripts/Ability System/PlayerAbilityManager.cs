using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Manages the player's equipped abilities, handles input for casting,
/// and coordinates with the targeting system
/// </summary>
public class PlayerAbilityManager : MonoBehaviour
{
    [Header("Ability Slots")]
    [Tooltip("Abilities equipped in slots 1, 2, 3")]
    public Ability[] equippedAbilities = new Ability[3];

    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private AbilityTargeting targetingSystem;

    private const float ABILITY_COOLDOWN = 3.0f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private float[] cooldownTimers = new float[3];

    // Input actions
    private InputAction ability1Action;
    private InputAction ability2Action;
    private InputAction ability3Action;
    private InputAction cancelAction;

    private int? activeAbilitySlot = null;

    public bool IsTargeting => activeAbilitySlot.HasValue;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();

        if (targetingSystem == null)
            targetingSystem = GetComponent<AbilityTargeting>();
        
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            ability1Action = playerMap.FindAction("Ability1");
            ability2Action = playerMap.FindAction("Ability2");
            ability3Action = playerMap.FindAction("Ability3");
            cancelAction = playerMap.FindAction("CancelAbility");
        }
    }

    private void OnEnable()
    {
        if (ability1Action != null)
        {
            ability1Action.performed += ctx => TryStartAbility(0);
            ability1Action.Enable();
        }

        if (ability2Action != null)
        {
            ability2Action.performed += ctx => TryStartAbility(1);
            ability2Action.Enable();
        }

        if (ability3Action != null)
        {
            ability3Action.performed += ctx => TryStartAbility(2);
            ability3Action.Enable();
        }

        if (cancelAction != null)
        {
            cancelAction.performed += ctx => CancelTargeting();
            cancelAction.Enable();
        }
        
        if (targetingSystem != null)
        {
            targetingSystem.OnTargetConfirmed += OnTargetConfirmed;
            targetingSystem.OnTargetingCancelled += OnTargetingCancelled;
        }
    }

    private void OnDisable()
    {
        if (ability1Action != null) ability1Action.Disable();
        if (ability2Action != null) ability2Action.Disable();
        if (ability3Action != null) ability3Action.Disable();
        if (cancelAction != null) cancelAction.Disable();

        if (targetingSystem != null)
        {
            targetingSystem.OnTargetConfirmed -= OnTargetConfirmed;
            targetingSystem.OnTargetingCancelled -= OnTargetingCancelled;
            
            if (activeAbilitySlot.HasValue)
            {
                targetingSystem.CancelTargeting();
            }
        }
        
        activeAbilitySlot = null;
    }

    private void Update()
    {
        UpdateCooldowns();
    }

    private void UpdateCooldowns()
    {
        for (int i = 0; i < cooldownTimers.Length; i++)
        {
            if (cooldownTimers[i] > 0)
            {
                cooldownTimers[i] -= Time.deltaTime;
            }
        }
    }

    /// <summary>
    /// Try to start targeting for an ability
    /// </summary>
    private void TryStartAbility(int slotIndex)
    {
        // Already targeting?
        if (activeAbilitySlot.HasValue)
        {
            if (targetingSystem != null && !targetingSystem.IsTargeting)
            {
                activeAbilitySlot = null;
            }
            else
            {
                return;
            }
        }

        // Valid slot?
        if (slotIndex < 0 || slotIndex >= equippedAbilities.Length)
            return;

        Ability ability = equippedAbilities[slotIndex];

        // Ability equipped?
        if (ability == null && debugMode)
        {
            Debug.Log($"[AbilityManager] No ability in slot {slotIndex + 1}");
            return;
        }

        // On cooldown?
        if (cooldownTimers[slotIndex] > 0 && debugMode)
        {
            Debug.Log($"[AbilityManager] Ability '{ability.abilityName}' on cooldown ({cooldownTimers[slotIndex]:F1}s remaining)");
            return;
        }

        // Start targeting
        activeAbilitySlot = slotIndex;
        targetingSystem.StartTargeting(ability, player);
        if(debugMode)
            Debug.Log($"[AbilityManager] Started targeting for '{ability.abilityName}'");
    }

    /// <summary>
    /// Cancel current targeting
    /// </summary>
    public void CancelTargeting()
    {
        if (!activeAbilitySlot.HasValue) return;

        targetingSystem.CancelTargeting();
        activeAbilitySlot = null;
        if(debugMode)
            Debug.Log("[AbilityManager] Targeting cancelled");
    }

    /// <summary>
    /// Called when targeting system confirms a target
    /// </summary>
    private void OnTargetConfirmed(TargetingResult result)
    {
        if (!activeAbilitySlot.HasValue) return;

        int slotIndex = activeAbilitySlot.Value;
        Ability ability = equippedAbilities[slotIndex];

        if (ability == null)
        {
            return;
        }

        // Execute the ability
        ExecuteAbility(ability, result);

        // Start cooldown
        cooldownTimers[slotIndex] = ABILITY_COOLDOWN;
    }

    /// <summary>
    /// Called when targeting is cancelled
    /// </summary>
    private void OnTargetingCancelled()
    {
        activeAbilitySlot = null;
    }

    /// <summary>
    /// Execute an ability with the given targeting result
    /// </summary>
    private void ExecuteAbility(Ability ability, TargetingResult result)
    {
        switch (result.type)
        {
            case TargetingResultType.SingleTarget:
                ability.Execute(player, result.singleTarget);
                break;

            case TargetingResultType.MultipleTargets:
                ability.Execute(player, result.multipleTargets);
                break;

            case TargetingResultType.Point:
                ability.Execute(player, result.targetPoint);
                break;
        }
    }

    /// <summary>
    /// Get the cooldown remaining for a slot (0 = ready)
    /// </summary>
    public float GetCooldownRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= cooldownTimers.Length)
            return 0;

        return Mathf.Max(0, cooldownTimers[slotIndex]);
    }

    /// <summary>
    /// Get cooldown as a 0-1 ratio (for UI)
    /// </summary>
    public float GetCooldownRatio(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= equippedAbilities.Length)
            return 0;

        Ability ability = equippedAbilities[slotIndex];
        if (ability == null)
            return 0;

        return Mathf.Clamp01(cooldownTimers[slotIndex] / ABILITY_COOLDOWN);
    }

    /// <summary>
    /// Check if an ability slot is ready to use
    /// </summary>
    public bool IsAbilityReady(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= equippedAbilities.Length)
            return false;

        return equippedAbilities[slotIndex] != null && cooldownTimers[slotIndex] <= 0;
    }

    /// <summary>
    /// Equip an ability to a slot
    /// </summary>
    public void EquipAbility(int slotIndex, Ability ability)
    {
        if (slotIndex < 0 || slotIndex >= equippedAbilities.Length)
            return;

        equippedAbilities[slotIndex] = ability;
        cooldownTimers[slotIndex] = 0;

        if(debugMode)
            Debug.Log($"[AbilityManager] Equipped '{(ability != null ? ability.abilityName : "null")}' to slot {slotIndex + 1}");
    }
}