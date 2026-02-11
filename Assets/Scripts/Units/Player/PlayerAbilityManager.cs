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
        targetingSystem.StartTargeting(ability, player);
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
    /// Spends 1 coin and performs a coin flip to determine success
    /// </summary>
    private void ExecuteAbility(Ability ability, TargetingResult result)
    {
        bool inCombat = CombatManager.Instance != null && CombatManager.Instance.InCombat;

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

            // Perform coin flip
            bool flipSuccess = player.PerformCoinFlip();

            if (!flipSuccess)
            {
                // Ability missed, coin spent but nothing happens
                if (debugMode) Debug.Log($"[AbilityManager] '{ability.abilityName}' MISSED! Coin spent, no effect.");
                MessageUI.Instance?.EnqueueMessage($"You missed {ability.abilityName}.");
                return;
            }

            if (debugMode) Debug.Log($"[AbilityManager] '{ability.abilityName}' HIT! Executing effects...");
        }

        if (!HasFeedbackEffects(ability))
        {
            MessageUI.Instance?.EnqueueMessage($"You cast {ability.abilityName}.");
        }

        PlayAbilityCastSound(ability);

        // Spawn visual effect if any
        SpawnAbilityVFX(ability, result);

        ExecuteAbilityWithDelay(ability, result);
    }

    private void ExecuteAbilityWithDelay(Ability ability, TargetingResult result)
    {
        float delay = ability != null ? Mathf.Max(0f, ability.effectDelay) : 0f;
        if (delay <= 0f)
        {
            ExecuteAbilityEffects(ability, result);
            return;
        }

        StartCoroutine(ExecuteAbilityAfterDelay(ability, result, delay));
    }

    private IEnumerator ExecuteAbilityAfterDelay(Ability ability, TargetingResult result, float delay)
    {
        yield return new WaitForSeconds(delay);
        ExecuteAbilityEffects(ability, result);
    }

    private void ExecuteAbilityEffects(Ability ability, TargetingResult result)
    {
        if (ability == null) return;

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
}
