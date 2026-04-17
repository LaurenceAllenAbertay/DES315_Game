using UnityEngine;
using UnityEngine.InputSystem;

//Place this on the win condition prefab in the final room//
//Player presses Interact when in range to trigger the win screen -EM//
public class WinCondition : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("How close the player must be to interact and win")]
    public float interactionRange = 3f;
    public LayerMask playerLayer;

    [Header("Win Screen")]
    public GameObject winScreenPanel;

    [Header("UI")]
    [Tooltip("Drag the Canvas child here - shows/hides based on proximity")]
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
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            interactionAction = playerMap?.FindAction("Interact");
        }

        if (winScreenPanel == null)
        {
            WinScreenUI ui = FindFirstObjectByType<WinScreenUI>(FindObjectsInactive.Include);
            if (ui != null)
                winScreenPanel = ui.gameObject;
        }
    }

    private void Start()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;

        if (interactPrompt != null) interactPrompt.SetActive(false);
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
        if (interactionAction != null)
        {
            interactionAction.performed -= OnInteract;
            interactionAction.Disable();
        }
    }

    private void Update()
    {
        if (gameWon) return;

        bool wasInRange = playerInRange;
        playerInRange = playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= interactionRange;

        if (playerInRange != wasInRange && interactPrompt != null)
        {
            interactPrompt.SetActive(playerInRange);
            if (debugMode) Debug.Log($"[WinCondition] Player {(playerInRange ? "entered" : "left")} interaction range");
        }
    }

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

        if (winScreenPanel != null)
        {
            RunScoreManager.Instance?.FinalizeTime();
            winScreenPanel.SetActive(true);
            PauseStack.Push();
        }
        else
        {
            Debug.LogWarning("[WinCondition] No win screen panel set! Add one in the Inspector.");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}