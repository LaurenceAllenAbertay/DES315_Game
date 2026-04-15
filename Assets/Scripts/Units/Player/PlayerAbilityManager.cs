using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the player's equipped abilities, handles input for casting, and coordinates with the targeting system.
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
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CoinUI coinUI;

    [Header("Audio")]
    [SerializeField] private AudioSource abilityAudioSource;

    private const float ABILITY_COOLDOWN = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private float[] cooldownTimers = new float[3];

    public event System.Action<int> OnAbilityCast;
    public event System.Action<int?> OnActiveSlotChanged;

    // Input actions
    private InputAction ability1Action;
    private InputAction ability2Action;
    private InputAction ability3Action;
    private InputAction cancelAction;
    private InputAction flipAction;

    private System.Action<InputAction.CallbackContext> onAbility1;
    private System.Action<InputAction.CallbackContext> onAbility2;
    private System.Action<InputAction.CallbackContext> onAbility3;
    private System.Action<InputAction.CallbackContext> onCancelAbility;

    private int? _activeAbilitySlot = null;
    private int? activeAbilitySlot
    {
        get => _activeAbilitySlot;
        set { _activeAbilitySlot = value; OnActiveSlotChanged?.Invoke(value); }
    }
    private bool flipSelected = false;

    public bool IsTargeting => activeAbilitySlot.HasValue;
    public void ActivateAbilitySlot(int slotIndex) => TryStartAbility(slotIndex);

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();

        if (targetingSystem == null)
            targetingSystem = GetComponent<AbilityTargeting>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        
        if (coinUI == null)
            coinUI = FindFirstObjectByType<CoinUI>();
        
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            ability1Action = playerMap.FindAction("Ability1");
            ability2Action = playerMap.FindAction("Ability2");
            ability3Action = playerMap.FindAction("Ability3");
            cancelAction = playerMap.FindAction("CancelAbility");
            flipAction = playerMap.FindAction("FlipCoinForAction");
        }

        for (int i = 0; i < cooldownTimers.Length; i++)
        {
            cooldownTimers[i] = 0f;
        }
    }

    private void OnEnable()
    {
        onAbility1 = ctx => TryStartAbility(0);
        onAbility2 = ctx => TryStartAbility(1);
        onAbility3 = ctx => TryStartAbility(2);
        onCancelAbility = ctx => CancelTargeting();

        if (ability1Action != null)
        {
            ability1Action.performed += onAbility1;
            ability1Action.Enable();
        }

        if (ability2Action != null)
        {
            ability2Action.performed += onAbility2;
            ability2Action.Enable();
        }

        if (ability3Action != null)
        {
            ability3Action.performed += onAbility3;
            ability3Action.Enable();
        }

        if (cancelAction != null)
        {
            cancelAction.performed += onCancelAbility;
            cancelAction.Enable();
        }

        if (flipAction != null)
        {
            flipAction.performed += OnFlipPerformed;
            flipAction.Enable();
        }
        
        if (targetingSystem != null)
        {
            targetingSystem.OnTargetConfirmed += OnTargetConfirmed;
            targetingSystem.OnTargetingCancelled += OnTargetingCancelled;
        }

    }

    private void Start()
    {
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnTurnEnded += OnTurnEnded;
    }

    private void OnDisable()
    {
        if (ability1Action != null) { ability1Action.performed -= onAbility1; ability1Action.Disable(); }
        if (ability2Action != null) { ability2Action.performed -= onAbility2; ability2Action.Disable(); }
        if (ability3Action != null) { ability3Action.performed -= onAbility3; ability3Action.Disable(); }
        if (cancelAction != null) { cancelAction.performed -= onCancelAbility; cancelAction.Disable(); }
        if (flipAction != null)
        {
            flipAction.performed -= OnFlipPerformed;
            flipAction.Disable();
        }

        if (targetingSystem != null)
        {
            targetingSystem.OnTargetConfirmed -= OnTargetConfirmed;
            targetingSystem.OnTargetingCancelled -= OnTargetingCancelled;
            
            if (activeAbilitySlot.HasValue)
            {
                targetingSystem.CancelTargeting();
            }
        }

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnTurnEnded -= OnTurnEnded;
        }
        
        activeAbilitySlot = null;
        SetCoinSpendingCount(0);
        coinUI?.SetIsFlipping(false);
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
                if (activeAbilitySlot.Value == slotIndex)
                {
                    CancelTargeting();
                    return;
                }

                CancelTargeting();
            }
        }

        // Valid slot?
        if (slotIndex < 0 || slotIndex >= equippedAbilities.Length)
            return;

        Ability ability = equippedAbilities[slotIndex];

        // Ability equipped?
        if (ability == null)
        {
            if (debugMode)
            {
                Debug.Log($"[AbilityManager] No ability in slot {slotIndex + 1}");
            }
            return;
        }

        // On cooldown?
        if (cooldownTimers[slotIndex] > 0)
        {
            if (debugMode)
            {
                Debug.Log($"[AbilityManager] Ability '{ability.abilityName}' on cooldown ({cooldownTimers[slotIndex]:F1}s remaining)");
            }
            return;
        }

        int abilityCoinCost = Mathf.Max(0, ability.coinCost);

        // Check combat restrictions
        if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
        {
            if (!CombatManager.Instance.IsPlayerTurn)
            {
                if (debugMode)
                {
                    Debug.Log("[AbilityManager] Not player's turn!");
                }
                return;
            }

            if (player == null)
            {
                return;
            }

            // Check if player has enough coins
            if (!player.CanSpendCoins(abilityCoinCost))
            {
                if (debugMode)
                {
                    Debug.Log($"[AbilityManager] Not enough coins for '{ability.abilityName}' (need {abilityCoinCost}, have {player.CurrentCoins})");
                }
                return;
            }
        }

        // Start targeting
        activeAbilitySlot = slotIndex;
        flipSelected = false;
        if (targetingSystem != null)
        {
            targetingSystem.SetFlipVisuals(false);
        }
        if (playerController != null)
            playerController.ForceStopMovement();
        targetingSystem.StartTargeting(ability, player);
        if (ability.IsOnlySelfTargeting())
        {
            TargetingResult selfResult = new TargetingResult
            {
                type = TargetingResultType.Point,
                targetPoint = player.transform.position,
            };
            targetingSystem.CancelTargeting();
            activeAbilitySlot = slotIndex;
            OnTargetConfirmed(selfResult);
            return;
        }
        if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
        {
            SetCoinSpendingCount(abilityCoinCost);
        }
        if (debugMode)
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
        flipSelected = false;
        targetingSystem.SetFlipVisuals(false);
        coinUI?.SetIsFlipping(false);
        SetCoinSpendingCount(0);
        if (debugMode)
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
            activeAbilitySlot = null;
            return;
        }

        // Execute the ability with coin flip
        ExecuteAbility(ability, result);

        // Start cooldown regardless of hit/miss
        cooldownTimers[slotIndex] = ABILITY_COOLDOWN;

        //matty addition - invoke the event so the animation controller can react to the cast
        OnAbilityCast?.Invoke(slotIndex);

        // Clear active slot
        activeAbilitySlot = null;
        flipSelected = false;
        targetingSystem.SetFlipVisuals(false);
        coinUI?.SetIsFlipping(false);
        SetCoinSpendingCount(0);
    }

    /// <summary>
    /// Called when targeting is cancelled
    /// </summary>
    private void OnTargetingCancelled()
    {
        activeAbilitySlot = null;
        flipSelected = false;
        if (targetingSystem != null)
        {
            targetingSystem.SetFlipVisuals(false);
        }
        coinUI?.SetIsFlipping(false);
        SetCoinSpendingCount(0);
    }

    /// <summary>
    /// Execute an ability with the given targeting result
    /// Spends ability coin cost in combat and optionally performs a coin flip to scale the outcome
    /// </summary>
    private void ExecuteAbility(Ability ability, TargetingResult result)
    {
        bool inCombat = CombatManager.Instance != null && CombatManager.Instance.InCombat;
        float flipMultiplier = 1f;

        if (inCombat)
        {
            if (player == null) return;

            int abilityCoinCost = Mathf.Max(0, ability.coinCost);

            // Spend coins for ability
            if (!player.SpendCoins(abilityCoinCost))
            {
                if (debugMode)
                {
                    Debug.Log($"[AbilityManager] Failed to spend {abilityCoinCost} coin(s) for '{ability.abilityName}'");
                }
                return;
            }

            if (flipSelected)
            {
                bool flipSuccess = player.PerformCoinFlip();
                flipMultiplier = flipSuccess ? 1.5f : 0.5f;
            }
        }

        TryEnqueueAbilityCastMessage(ability, result);

        PlayAbilityCastSound(ability);

        // Spawn visual effect if any
        SpawnAbilityVFX(ability, result);

        ExecuteAbilityWithDelay(ability, result, flipMultiplier);
    }

    private void ExecuteAbilityWithDelay(Ability ability, TargetingResult result, float baseMultiplier)
    {
        float delay = ability != null ? Mathf.Max(0f, ability.effectDelay) : 0f;
        if (delay <= 0f)
        {
            ExecuteAbilityEffects(ability, result, baseMultiplier);
            return;
        }

        StartCoroutine(ExecuteAbilityAfterDelay(ability, result, delay, baseMultiplier));
    }

    private IEnumerator ExecuteAbilityAfterDelay(Ability ability, TargetingResult result, float delay, float baseMultiplier)
    {
        yield return new WaitForSeconds(delay);
        ExecuteAbilityEffects(ability, result, baseMultiplier);
    }

    private void ExecuteAbilityEffects(Ability ability, TargetingResult result, float baseMultiplier)
    {
        if (ability == null) return;

        switch (result.type)
        {
            case TargetingResultType.SingleTarget:
                ability.Execute(player, result.singleTarget, baseMultiplier);
                break;

            case TargetingResultType.MultipleTargets:
                ability.Execute(player, result.multipleTargets, baseMultiplier);
                break;

            case TargetingResultType.Point:
                ability.Execute(player, result.targetPoint, baseMultiplier);
                break;
        }
    }

    private void OnTurnEnded(Unit unit)
    {
        if (activeAbilitySlot.HasValue)
            CancelTargeting();
    }

    private void OnFlipPerformed(InputAction.CallbackContext context)
    {
        if (!activeAbilitySlot.HasValue || targetingSystem == null || !targetingSystem.IsTargeting)
        {
            return;
        }

        if (CombatManager.Instance == null || !CombatManager.Instance.InCombat || !CombatManager.Instance.IsPlayerTurn)
        {
            return;
        }

        flipSelected = !flipSelected;
        targetingSystem.SetFlipVisuals(flipSelected);
        coinUI?.SetIsFlipping(flipSelected);
    }

    private void PlayAbilityCastSound(Ability ability)
    {
        if (ability == null || ability.castSound == null) return;

        if (abilityAudioSource == null)
        {
            abilityAudioSource = GetComponent<AudioSource>();
            if (abilityAudioSource == null)
            {
                abilityAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        abilityAudioSource.PlayOneShot(ability.castSound);
    }

    /// <summary>
    /// Spawns the visual effect for an ability at the appropriate location
    /// </summary>
    private void SpawnAbilityVFX(Ability ability, TargetingResult result)
    {
        if (ability.visualEffectPrefab == null) return;

        Vector3 spawnPosition = Vector3.zero;

        // Determine spawn position based on targeting type
        switch (ability.targetingType)
        {
            case TargetingType.PointAndClick:
                // Spawn on the enemy clicked on
                spawnPosition = result.targetPoint; 
                break;
                
            case TargetingType.Cone:
                // Spawn on the center of the player
                if (player != null)
                    spawnPosition = player.transform.position;
                break;
                
            case TargetingType.RangedAOE:
                // Spawn at the centre of the aoe area
                spawnPosition = result.targetPoint;
                break;
        }

        GameObject vfx = Instantiate(ability.visualEffectPrefab, spawnPosition, Quaternion.identity);
        
        // Auto-destroy after some time (particle effects usually last a few seconds)
        // This handles the user's question about needing code to destroy them.
        float vfxDuration = ability != null ? Mathf.Max(0.1f, ability.visualEffectDuration) : 5f;
        Destroy(vfx, vfxDuration);
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

        if (debugMode)
            Debug.Log($"[AbilityManager] Equipped '{(ability != null ? ability.abilityName : "null")}' to slot {slotIndex + 1}");
    }

    private static bool HasFeedbackEffects(Ability ability)
    {
        if (ability == null || ability.effects == null)
        {
            return false;
        }

        foreach (AbilityEffect effect in ability.effects)
        {
            if (effect == null) continue;
            if (effect is DamageEffect || effect is HealEffect || effect is BlockEffect)
            {
                return true;
            }
        }

        return false;
    }

    private void TryEnqueueAbilityCastMessage(Ability ability, TargetingResult result)
    {
        if (ability == null)
        {
            return;
        }

        string message = null;

        if (result.type == TargetingResultType.SingleTarget
            && ability.targetingType == TargetingType.PointAndClick
            && IsTorchTarget(result.singleTarget))
        {
            message = $"You casted {ability.abilityName} on a torch.";
        }
        else if (result.type == TargetingResultType.MultipleTargets
                 && (ability.targetingType == TargetingType.RangedAOE || ability.targetingType == TargetingType.Cone)
                 && (result.multipleTargets == null || result.multipleTargets.Count == 0))
        {
            message = $"You casted {ability.abilityName}.";
        }
        else if (!HasFeedbackEffects(ability))
        {
            message = $"You cast {ability.abilityName}.";
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            MessageUI.Instance?.EnqueueMessage(message);
        }
    }

    private void SetCoinSpendingCount(int count)
    {
        if (coinUI != null)
        {
            coinUI.SetCoinSpendingCount(count);
        }
    }

    private static bool IsTorchTarget(Unit target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.GetComponentInParent<LightSource>() != null)
        {
            return true;
        }
        
        return target.gameObject.name.IndexOf("torch", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    [Header("Ability Inventory")]
    public List<Ability> inventoryAbilities = new List<Ability>();

    public void AddAbilityToInventory(Ability ability)
    {
        if (ability == null) return;
        inventoryAbilities.Add(ability);
        if (debugMode) Debug.Log($"[AbilityManager] Added '{ability.abilityName}' to ability inventory");
    }

    public bool RemoveAbilityFromInventory(Ability ability)
    {
        return inventoryAbilities.Remove(ability);
    }

    //Test the Abilities -EM//
    [ContextMenu("Test Ability")]
    private void DebugTestAbility()
    {
        equippedAbilities[3].Execute(player, player.transform.position);
    }

}