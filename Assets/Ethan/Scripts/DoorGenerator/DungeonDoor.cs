using UnityEngine;

//Static door/archway that transitions player to connected room -EM//
//Player can click door or press button when nearby to transitions -EM//
public class DungeonDoor : MonoBehaviour
{
    [Header("Teleportation")]
    [SerializeField] private Vector3 doorPosition;
    [SerializeField] private Vector3 destinationA; //TP position for going to room A//
    [SerializeField] private Vector3 destinationB; //TP position for going to room B//

    [Header("Interaction Settings")]
    [Tooltip("Distance player must be to interact with door")]
    public float interactionRange = 3f;

    [Header("References")]
    public Room roomA; //One side of the door//
    public Room roomB; //Other side of the door//

    [Header("Player Detection")]
    [Tooltip("Layer for the player")]
    public LayerMask playerLayer = 1 << 7;

    [Header("UI")]
    [Tooltip("Offset for UI prompt above door")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    private Transform playerTransform;
    private bool playerInRange = false;
    private bool showPrompt = false;

    
    public void SetupTeleportDoor(Vector3 doorPos, Vector3 destA, Vector3 destB, Room rA, Room bB)
    {
        doorPosition = doorPos;
        destinationA = destA;
        destinationB = destB;
        roomA = rA;
        roomB = bB;
    }
   
    private void Update()
    {
        //check for player in range//
        CheckPlayerProximity();

        //Check for E key press when in range//
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            TeleportPlayer();
        }
    }

    //Check if player is within interaction range -EM//
    private void CheckPlayerProximity()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, playerLayer);

        bool wasInRange = playerInRange;
        playerInRange = colliders.Length > 0;

        if(playerInRange && colliders.Length > 0)
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
        if(playerInRange && !wasInRange)
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
        if(distToA < distToB)
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
        playerTransform.position = destinationPos;

        Debug.Log($"[DungeonDoor] Player teleport to {destinationPos}");
    }

    //Draw UI prompt when player is in range//
    private void OnGUI()
    {
        if(!showPrompt || playerTransform == null) return;

        //Convert world position to screen position//
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + promptOffset);

        //Only show if in front of camera//
        if(screenPos.z > 0)
        {
            //Flip y coordinate (GUI Uses top-left origin)//
            screenPos.y = Screen.height - screenPos.y;

            //Draw prompt backgorund//
            float width = 100f;
            float height = 40f;
            Rect bgRect = new Rect(screenPos.x - width/2, screenPos.y - height/2, width, height);

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
