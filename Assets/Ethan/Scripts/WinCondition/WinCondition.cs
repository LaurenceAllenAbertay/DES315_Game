using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

//Place this on the win condition prefab in the final room//
//Player clicks the object to trigger the win screen -EM//
public class WinCondition : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("How close the player must be to click and win")]
    public float interactionRange = 3f;
    public LayerMask playerLayer;

    [Header("Win Screen")]
    public string winSceneName = "";
    public GameObject winScreenPanel;

    [Header("UI")]
    [Tooltip("Drage the Canvas child here - shows/hide based on proximity")]
    public GameObject interactPrompt;

    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("Debug")]
    public bool debugMode = true;

    private InputAction interactionAction;
    private Transform playerTransform;
    private bool gameWon = false;
    private bool playerInRange = false;

    private void Awake()
    {
        //Get the interaction action from the Player map//
        if(inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            interactionAction = playerMap?.FindAction("Interact");
        }
    }
    private void Start()
    {
        //Find player//
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;

        //Hide prompt on start//
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void OnEnable()
    {
        if(interactionAction != null)
        {
            interactionAction.performed += OnInteract;
            interactionAction.Disable();
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
        if (gameWon) return;

        //Show or hide ineract prompt based on player proximity//
        bool wasInRange = playerInRange;
        playerInRange = playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= interactionRange;

        //Only update the Canvas when the state actually changes//
        if(playerInRange != wasInRange && interactPrompt != null)
        { 
            interactPrompt.SetActive(playerInRange);
            if (debugMode) Debug.Log($"[WinCondition] Player {(playerInRange ? "entered" : "left")} interaction range");
        }
    }

    //Called when the playert presses E//
    private void OnInteract(InputAction.CallbackContext context)
    {
        if (gameWon || !playerInRange) return;
        
        TriggerWin();
    }

    public void TriggerWin()
    {
        if (gameWon) return;
        gameWon = true;

        if (debugMode) Debug.Log("[WinCondition] Player won the game!");

        MessageUI.Instance?.EnqueueMessage("You escaped the dungeon! You Win");

        if(!string.IsNullOrEmpty(winSceneName))
        {
            SceneManager.LoadScene(winSceneName);
        }
        else if (winScreenPanel != null) 
        {
            winScreenPanel.SetActive(true);
            Time.timeScale = 0f;
        }
        else
        {
            Debug.LogWarning("[WinCondition] No win scene or panel set! Add one in the Inspector.");
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
