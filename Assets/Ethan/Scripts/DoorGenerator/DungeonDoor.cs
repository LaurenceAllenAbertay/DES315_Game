using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

//Static door/archway that transitions player to connected room -EM//
//Player can click door or press button when nearby to transitions -EM//
public class DungeonDoor : MonoBehaviour
{
    [Header("Teleportation")]
    [SerializeField] private Vector3 doorPosition;
    [SerializeField] private Vector3 destinationA; //TP position for going to room A//
    [SerializeField] private Vector3 destinationB; //TP position for going to room B//

    [Header("References")]
    public Room roomA; //One side of the door//
    public Room roomB; //Other side of the door//

    [Header("Interaction Settings")]
    [Tooltip("Distance player must be to interact with door")]
    public float interactionRange = 2f;


    [Header("Player Detection")]
    [Tooltip("Layer for the player")]
    public LayerMask playerLayer = 1 << 6;

    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("UI")]
    [Tooltip("Offset for UI prompt above door")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    [Header("Cooldown")]
    [Tooltip("Cooldown after teleporting to prevent re-triggering")]
    public float teleportCooldown = 5.0f;

    private Transform playerTransform;
    private bool playerInRange = false;
    private bool showPrompt = false;
    private float lastTeleportTime = -999f;
    private static int lastTeleportFrame = -999;
    private static float globalLastTeleportTime = -999f;


    private InputAction interactionAction;

    public void SetupTeleportDoor(Vector3 doorPos, Vector3 destA, Vector3 destB, Room rA, Room bB)
    {
        doorPosition = doorPos;
        destinationA = destA;
        destinationB = destB;
        roomA = rA;
        roomB = bB;
    }

    private void Awake()
    {
        SetupInteractionAction();
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

    public void ConfigureInput(InputActionAsset actions)
    {
        if (interactionAction != null)
        {
            interactionAction.performed -= OnInteract;
            interactionAction.Disable();
        }

        inputActions = actions;
        SetupInteractionAction();

        if (isActiveAndEnabled && interactionAction != null)
        {
            interactionAction.performed += OnInteract;
            interactionAction.Enable();
        }
    }

    private void Update()
    {
        //check for player in range//
        CheckPlayerProximity();

        //Hide prompt if in global cooldown//
        if(Time.time < globalLastTeleportTime + teleportCooldown)
        {
            showPrompt = false;
        }
    }

    //Check if player is within interaction range -EM//
    private void CheckPlayerProximity()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, playerLayer);

        bool wasInRange = playerInRange;
        playerInRange = colliders.Length > 0;

        if (playerInRange && colliders.Length > 0)
        {
            playerTransform = colliders[0].transform;
            showPrompt = true;
        }
        else
        {
            showPrompt = false;
            playerTransform = null;
        }

        //Log for debugging//
        if (playerInRange && !wasInRange)
        {
            Debug.Log($"[DungeonDoor] Player in range of door at {transform.position}");
        }
    }

    private void TeleportPlayer()
    {
        if (playerTransform == null) return;

        //Determine which room player is currently in//
        Vector3 playerPos = playerTransform.position;
        Vector3 destinationPos;

        //Calculate which destination to use based on player's current position//
        float distToA = Vector3.Distance(playerPos, destinationA);
        float distToB = Vector3.Distance(playerPos, destinationB);

        //If player is closer to destA, they're in roomA, so teleport to roomB//
        if (distToA < distToB)
        {
            destinationPos = destinationB;
            Debug.Log($"[DungeonDoor] Teleporting from Room {roomA.GetHashCode()} to Room {roomB.GetHashCode()}");
        }
        else
        {
            destinationPos = destinationA;
            Debug.Log($"[DungeonDoor] Teleporting from Room {roomB.GetHashCode()} to Room {roomA.GetHashCode()}");
        }

        //Teleport the player//
        NavMeshAgent agent = playerTransform.GetComponent<NavMeshAgent>();
        if(agent != null)
        {
            //Properly teleports NavMeshAgent//
            agent.Warp(destinationPos);
        }
        else
        {
            //Fallback if no NavMeshAgent//
            playerTransform.position = destinationPos;
        }

        //Record teleport time for cooldown//
        lastTeleportTime = Time.time;
        globalLastTeleportTime = Time.time;
        lastTeleportFrame = Time.frameCount;

        //Hide prompt immediately//
        showPrompt = false;

        Debug.Log($"[DungeonDoor] Player teleport to {destinationPos}");
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!playerInRange) return;
        if (Time.time <= globalLastTeleportTime) return;
        if (Time.frameCount == lastTeleportFrame) return;
        TeleportPlayer();
    }

    private void SetupInteractionAction()
    {
        if (inputActions == null) return;

        var playerMap = inputActions.FindActionMap("Player");
        interactionAction = playerMap?.FindAction("Interact");
    }

    //Draw UI prompt when player is in range//
    private void OnGUI()
    {
        if (!showPrompt || playerTransform == null) return;

        //Convert world position to screen position//
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + promptOffset);

        //Only show if in front of camera//
        if (screenPos.z > 0)
        {
            //Flip y coordinate (GUI Uses top-left origin)//
            screenPos.y = Screen.height - screenPos.y;

            //Draw prompt backgorund//
            float width = 100f;
            float height = 40f;
            Rect bgRect = new Rect(screenPos.x - width / 2, screenPos.y - height / 2, width, height);

            GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
            GUI.Box(bgRect, "");

            //Draw "Press E" text//
            GUI.contentColor = Color.yellow;
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 16;
            style.fontStyle = FontStyle.Bold;

            GUI.Label(bgRect, "Press E", style);
        }
    }

    private void OnDrawGizmosSelected()
    {
        //Draw Interaction Range//
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        //Draw teleport destinations//
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(destinationA, 0.3f);
        Gizmos.DrawLine(transform.position, destinationA);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(destinationB, 0.3f);
        Gizmos.DrawLine(transform.position, destinationB);
    }

}


