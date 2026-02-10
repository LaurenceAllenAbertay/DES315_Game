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

    [Tooltip("Number of item cards to show (default 3)")]
    public int itemChoiceCount = 3;

    [Header("UI")]
    [Tooltip("The card selection UI Manager")]
    public TreasureChestUI chestUI;

    [Header("Visual Feedback")]
    [Tooltip("GameObject to show when chest can be interacted with")]
    public GameObject interactionPrompt;

    [Tooltip("Optional: Animator for chest opening animation")]
    public Animator chestAnimator;

    [Tooltip("Optional: AudioClip for chest opening sound")]
    public AudioClip openSound;

    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("Debug")]
    public bool debugMode = true;

    private InputAction interactionAction;
    private AudioSource audioSource;
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
        audioSource = GetComponent<AudioSource>();
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
        List<ItemDefinition> itemChoices = GenerateItemChoices();

        //Show UI with choices//
        if (chestUI != null)
        {
            chestUI.ShowItemSelection(itemChoices, OnItemSelected);
        }
        else
        {
            Debug.LogError("[TreasureChest] No TreasureChestUI found!");
        }
    }

    //Generate random items from the item pool -EM//
    private List<ItemDefinition> GenerateItemChoices()
    {
        List<ItemDefinition> choices = new List<ItemDefinition>();

        if(itemPool.Count == 0)
        {
            Debug.LogWarning("[TreasureChest] Item pool is empty!");
            return choices;
        }

        //Create a copy of the pool to avoid picking duplicates//
        List<ItemDefinition> availableItems = new List<ItemDefinition>(itemPool);

        //Pick random items//
        int count = Mathf.Min(itemChoiceCount, availableItems.Count);
        for(int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, availableItems.Count);
            choices.Add(availableItems[randomIndex]);
            availableItems.RemoveAt(randomIndex);
        }

        if(debugMode)
        {
            Debug.Log($"[TreasureChest] Generated {choices.Count} item choices");
        }

        return choices;
    }

    //Called when player selects an item from the UI -EM//
    private void OnItemSelected(ItemDefinition selectedItem)
    {
        if (selectedItem == null) return;

        if(debugMode)
        {
            Debug.Log($"[TreasureChest] Player selected: {selectedItem.itemName}");
        }

        //Add item to player's inventory//
        ItemManager itemManager = FindAnyObjectByType<ItemManager>();
        if(itemManager != null)
        {
            itemManager.AddItem(selectedItem);  
        }
        else
        {
            Debug.LogError("[TreasureChest] No ItemManager found!");
        }

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
        Gizmos.DrawWireCube(transform.position = Vector3.up * 2f, Vector3.one * 0.5f);
    }
}
