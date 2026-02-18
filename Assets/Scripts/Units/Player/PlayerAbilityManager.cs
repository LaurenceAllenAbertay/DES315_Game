using System.Collections;
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
    [SerializeField] private CoinUI coinUI;

    [Header("Audio")]
    [SerializeField] private AudioSource abilityAudioSource;

    private const float ABILITY_COOLDOWN = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private float[] cooldownTimers = new float[3];

    // Input actions
    private InputAction ability1Action;
    private InputAction ability2Action;
    private InputAction ability3Action;
    private InputAction cancelAction;
    private InputAction flipAction;

    private int? activeAbilitySlot = null;
    private bool flipSelected = false;

    public bool IsTargeting => activeAbilitySlot.HasValue;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();

        if (targetingSystem == null)
            targetingSystem = GetComponent<AbilityTargeting>();
        
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

    private void OnDisable()
    {
        if (ability1Action != null) ability1Action.Disable();
        if (ability2Action != null) ability2Action.Disable();
        if (ability3Action != null) ability3Action.Disable();
        if (cancelAction != null) cancelAction.Disable();
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
        
        activeAbilitySlot = null;
        SetCoinSpendingCount(0);
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

            // Check if player has at least 1 coin
            if (!player.CanSpendCoin())
            {
                if (debugMode)
                {
                    Debug.Log($"[AbilityManager] Not enough coins for '{ability.abilityName}' (need 1, have {player.CurrentCoins})");
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
        targetingSystem.StartTargeting(ability, player);
        if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
        {
            SetCoinSpendingCount(1);
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
        
        // Clear active slot
        activeAbilitySlot = null;
        flipSelected = false;
        targetingSystem.SetFlipVisuals(false);
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
        SetCoinSpendingCount(0);
    }

    /// <summary>
    /// Execute an ability with the given targeting result
    /// Spends 1 coin in combat and optionally performs a coin flip to scale the outcome
    /// </summary>
    private void ExecuteAbility(Ability ability, TargetingResult result)
    {
        bool inCombat = CombatManager.Instance != null && CombatManager.Instance.InCombat;
        float flipMultiplier = 1f;

        if (inCombat)
        {
            if (player == null) return;

            // Spend 1 coin
            if (!player.SpendCoin())
            {
                if (debugMode)
                {
                    Debug.Log($"[AbilityManager] Failed to spend coin for '{ability.abilityName}'");
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
}
