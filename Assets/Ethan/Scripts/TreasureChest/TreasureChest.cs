using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//Treasure chest that can be opened by pressing E when nearby, shows 3 random item cards for the player to choose from -EM//
public class TreasureChest : MonoBehaviour
{

    [Header("Chest State")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private bool hasBeenOpened = false;

    [Header("Interaction")]
    [Tooltip("Distance player must be within to interact")]
    public float interactionRange = 3f;

    [Tooltip("Layer for the player")]
    public LayerMask playerMask = 1 << 7;

    [Header("Item Pool")]
    [Tooltip("All possible items that can appear in this chest")]
    public List<ItemDefinition> itemPool = new List<ItemDefinition>();

    [Header("Ability Pool")]
    [Tooltip("All possible abilites that can appear in this chest")]
    public List<Ability> abilityPool = new List<Ability>();

    [Tooltip("How many of the cards should be abilities (rest are items), Randomised each open.")]
    public int minAbilityCards = 1;
    public int maxAbilityCards = 2;

    [Tooltip("Number of item cards to show (default 3)")]
    public int itemChoiceCount = 3;

    [Header("UI")]
    [Tooltip("The card selection UI Manager")]
    public TreasureChestUI chestUI;

    [Header("Visual Feedback")]
    [Tooltip("GameObject to show when chest can be interacted with")]
    public GameObject interactionPrompt;

    [Tooltip("Drag the chest model root here so aritsts can swap it out")]
    public GameObject chestModel;

    [Tooltip("Optional: Animator for chest opening animation")]
    public Animator chestAnimator;

    [Header("Audio")]
    [Tooltip("Optional: AudioClip for chest opening sound")]
    public AudioClip openSound;
    [Tooltip("Optional: AudioSource to play chest sounds")]
    [SerializeField] private AudioSource audioSource;

    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("Debug")]
    public bool debugMode = true;

    private InputAction interactionAction;
    private bool playerInRange = false;

    private void Awake()
    {
        //Get Input Action//
        if(inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            interactionAction = playerMap?.FindAction("Interact");
        }

        //Get or create audio source//
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null && openSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        //Hide interaction prompt by default//
        if(interactionAction != null)
        {
            interactionPrompt.SetActive(false);
        }

        //Find UI if not assigned//
        if(chestUI == null)
        {
            chestUI = FindAnyObjectByType<TreasureChestUI>();
        }

        //If no animator assigned try to find one the chest model//
        if(chestAnimator == null && chestModel != null)
        {
            chestAnimator = chestModel.GetComponentInChildren<Animator>();
        }
    }

    private void OnEnable()
    {
        if (interactionAction != null)
        {
            interactionAction.performed += OnInteract;
            interactionAction.Enable();
        }
    }

    private void OnDisable()
    {
        if(interactionAction != null)
        {
            interactionAction.performed -= OnInteract;
            interactionAction.Disable();
        }
    }

    private void Update()
    {
        CheckPlayerProximity();
    }

    private void CheckPlayerProximity()
    {
        if (hasBeenOpened) return;

        //Check if player is in range//
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, playerMask);
        bool wasInRange = playerInRange;
        playerInRange = colliders.Length > 0;

        //Show/ hide interaction prompt//
        if(playerInRange != wasInRange && interactionPrompt !=  null)
        {
            interactionPrompt.SetActive(playerInRange);
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!playerInRange || hasBeenOpened || isOpen) return;

        OpenChest();
    }

    //Open the chest and show item selection UI -EM//
    public void OpenChest()
    {
        if(hasBeenOpened)
        {
            if (debugMode) Debug.Log("[TreasureChest] Chest already opened!");
            return;
        }

        isOpen = true;
        hasBeenOpened = true;

        if (debugMode) Debug.Log("[TreasureChest] Opening Chest!");
        
        TutorialManager.Instance?.Trigger("first_chest_open");

        //Hide interaction prompt//
        if(interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }

        //Play opening animation//
        if(chestAnimator != null)
        {
            chestAnimator.SetTrigger("Open");
        }

        //Play opening sound//
        if(audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        //Generatre random item choices//
        List <ChestReward> rewards = GenerateRewards();

        //Show UI with choices//
        if (chestUI != null)
        {
            chestUI.ShowRewards(rewards, OnRewardSelected);
        }
        else
        {
            Debug.LogError("[TreasureChest] No TreasureChestUI found!");
        }
    }

    //Generate random items from the item pool -EM//
    private List<ChestReward> GenerateRewards()
    {
        List<ChestReward> rewards = new List<ChestReward>();

        //Pick how many ability cards to show this open (random between min and max)//
        int abilityCount = UnityEngine.Random.Range(minAbilityCards, Mathf.Min(maxAbilityCards, abilityPool.Count) + 1);
        int itemCount = itemChoiceCount - abilityCount;

        //Pick abilities//
        List<Ability> availableAbilities = new List<Ability>(abilityPool);
        for (int i = 0; i < abilityCount && availableAbilities.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, availableAbilities.Count);
            rewards.Add(new ChestReward { type = ChestRewardType.Ability, ability = availableAbilities[idx] });
            availableAbilities.RemoveAt(idx);
        }

        //Pick items//
        List<ItemDefinition> availableItems = new List<ItemDefinition>(itemPool);
        for (int i = 0; i < itemCount && availableItems.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, availableItems.Count);
            rewards.Add(new ChestReward { type = ChestRewardType.Item, item = availableItems[idx] });
            availableItems.RemoveAt(idx);
        }

        //Shuffle so abilities aren't always first//
        for (int i = rewards.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (rewards[i], rewards[j]) = (rewards[j], rewards[i]);
        }

        if (debugMode) Debug.Log($"[TreasureChest] Generated {itemCount} item(s) and {abilityCount} ability card(s)");
        return rewards;

    }

    private Ability pendingAbility;

    //Called when player selects an item from the UI//
    private void OnRewardSelected(ChestReward reward)
    {
        if (reward.type == ChestRewardType.Item && reward.item != null)
        {
            if (debugMode) Debug.Log($"[TreasureChest] Player selected item: {reward.item.itemName}");

            ItemManager itemManager = FindAnyObjectByType<ItemManager>();
            if (itemManager != null)
            {
                itemManager.AddItem(reward.item);
            }
            else
            {
                Debug.LogError("[TreasureChest] No ItemManager found!");
            }

            isOpen = false;
        }
        else if (reward.type == ChestRewardType.Ability && reward.ability != null)
        {
            if (debugMode) Debug.Log($"[TreasureChest] Player selected ability: {reward.ability.abilityName}");

            PlayerAbilityManager abilityManager = FindAnyObjectByType<PlayerAbilityManager>();
            if (abilityManager == null)
            {
                Debug.LogError("[ReasureChest] No PlayerAbilityManager found!");
                isOpen = false;
                return;
            }
            
            //Try to find an empty slot first//
            for(int i = 0; i < abilityManager.equippedAbilities.Length; i++)
            {
                if (abilityManager.equippedAbilities[i] == null)
                {
                    abilityManager.equippedAbilities[i] = reward.ability;
                    if (debugMode) Debug.Log($"[TreasureChest] Ability '{reward.ability.abilityName}' equipped to slot {i}");
                    isOpen = false;
                    return;
                }
            }

            //All slots full - show replace prompt//
            if (debugMode) Debug.Log("[TreasureChest] All slots full, prompting replace choice");
            pendingAbility = reward.ability;
            chestUI.ShowReplacePrompt(abilityManager.equippedAbilities, OnReplaceSlotSelected);
            //IsOpen stays true until the replace choice is made//
        }
    }

    //Called when palyer pick which slot to replace -EM//
    private void OnReplaceSlotSelected(int slotIndex)
    {
        PlayerAbilityManager abilityManager = FindAnyObjectByType<PlayerAbilityManager>();
        if(abilityManager != null && pendingAbility != null)
        {
            if (debugMode) Debug.Log($"[TreasureChest] Replacing slot {slotIndex} with '{pendingAbility.abilityName}'");
            abilityManager.equippedAbilities[slotIndex] = pendingAbility;

            //Refresh the ability slot UI so the hotbar updates//
            AbilitySlotUI slotUI = FindAnyObjectByType<AbilitySlotUI>();
            if (slotUI != null) slotUI.RefreshIcons();
        }

        pendingAbility = null;
        isOpen = false;
    }

    //Force open the chest (for testing) -EM//
    [ContextMenu("Force Open Chest")]
    public void ForceOpen()
    {
        hasBeenOpened = false;
        OpenChest();
    }

    [ContextMenu("Reset Chest")]
    public void ResetChest()
    {
        isOpen = false;
        hasBeenOpened = false;

        if(chestAnimator != null)
        {
            chestAnimator.SetTrigger("Close");
        }

        if(debugMode)
        {
            Debug.Log("[TreasureChest] Chest reset!");
        }
            
    }

    private void OnDrawGizmosSelected()
    {
        //Draw interaction range//
        Gizmos.color = hasBeenOpened ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        //Draw state indicator//
        Gizmos.color = hasBeenOpened ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
    }
}
