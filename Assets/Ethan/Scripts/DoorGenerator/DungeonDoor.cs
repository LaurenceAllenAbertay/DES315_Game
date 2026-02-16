using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

//Static door/archway that transitions player to connected room -EM//
//Player can click door or press button when nearby to transitions -EM//

[RequireComponent(typeof(NavMeshObstacle))]
public class DungeonDoor : MonoBehaviour
{
    [Header("Door State")]
    [SerializeField] private bool isLocked = false;
    //Toggle between archway and door visuals -EM//
    [SerializeField] private bool useArchway = true;

    [Header("Room References")]
    public Room connectedRoomA; //One side of the door//
    public Room connectedRoomB; //Other side of the door//

    [Header("Visual References")]
    [SerializeField] private GameObject doorVisuals; //Parent Object for door/ archway mesh//
    [SerializeField] private GameObject archwayVisuals; //Archway mesh (if using archway)//

    [Header("Player Detection")]
    [Tooltip("Layer for the player")]
    public LayerMask playerLayer = 1 << 7;

    [Tooltip("Distance player must be to interact with door")]
    public float interactionRange = 3f;

    [Header("UI")]
    [SerializeField] private Canvas interactionCanvas;
    [SerializeField] private Button interactionButton;
    [SerializeField] private GameObject interactionPrompt;

    [Header("Transition Settings")]
    [Tooltip("Duration of fade to black (in seconds)")]
    public float fadeDuration = 0.5f;

    [Tooltip("How long to stay black before fading back in")]
    public float blackScreenDuration = 0.2f;

    private Transform playerTransform;
    private bool playerInRange = false;
    private bool IsTransitioning = false;

    //Events//
    public delegate void DoorTransition(DungeonDoor door, Room targetRoom);
    public static event DoorTransition OnDoorTransition;

    private void Start()
    {
       //Setup UI interaction button//
       if(interactionButton != null)
        {
            interactionButton.onClick.AddListener(OnInteractionButtonClicked);
        }

        //Hide UI initially//
        SetUIVisibility(false);

        //Set visual style based on useArchway flag//
        UpdateVisualStyle();
    }

    private void Update()
    {
        //check for player in range//
        CheckPlayerProximity();

        //Check for E key press when in range//
        if (playerInRange && !isTransitioning && Input.GetKeyDown(KeyCode.E))
        {
            TriggerTransition();
        }
    }

    private void OnDestroy()
    {
        if (interactionButton != null)
        {
            interactionButton.onClick.RemoveListener(OnInteractionButtonClicked);
        }
    }

    //Check if player is within interaction range -EM//
    private void CheckPlayerProximity()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, playerLayer);
        bool wasInRange = playerInRange;
        playerInRange = colliders.Length > 0;

        if(playerInRange && !wasInRange)
        {
            //Player entered range//
            if(colliders.Length > 0)
            {
                playerTransform = colliders[0].transform;
            }
            SetUIVisibility(true);
        }
        else if (!playerInRange && wasInRange)
        {
            //Player left range//
            playerTransform = null;
            SetUIVisibility(false);
        }
    }

    //Handle UI button click -EM//
    private void OnInteractionButtonClicked()
    {
        if(!isTransitioning && !isLocked)
        {
            TriggerTransition();
        }
    }

    //handle clicking directily on the door object -EM//
    private void OnMouseDown()
    {
        if(playerInRange && !isTransitioning &&  !isLocked)
        {
            TriggerTransition();
        }
    }

    //Start the room transition process -EM//
    private void TriggerTransition()
    {
        if(isLocked)
        {
            Debug.Log($"[DungeonDoor] Door is locked!");
            return;
        }

        if(playerTransform == null)
        {
            Debug.LogWarning("[DungeonDoor] No player found for transition!");
            return;
        }

        //Determine which room thye player is transitioning to -EM//
        Room targetRoom = GetTargetRoom();

        if(targetRoom == null)
        {
            Debug.LogError("[DungeonDoor] No Connected room found!");
            return;
        }

        //Start transition coroutine//
        StartCoroutine(TransitionToRoom(targetRoom));
    }

    //Determine which room the player should transition to -EM//
    private Room GetTargetRoom()
    {
        if(connectedRoomA == null || connectedRoomB == null)
        {
            Debug.LogWarning("[DungeonDoor] Door missing room connections!");
            //return whichever is not null//
            return connectedRoomA ?? connectedRoomB;
        }

        //Check which room the player is currently in -EM//
        Vector3 playerPos = playerTransform.position;

        float distToA = Vector3.Distance(playerPos, GetRoomCenter(connectedRoomA));
        float distToB = Vector3.Distance(playerPos, GetRoomCenter(connectedRoomB));

        //Return the room the player is Not currently in -EM//
        return distToA < distToB ? connectedRoomB : connectedRoomA;
    }

    //Get approximate center of a room -EM//
    private Vector3 GetRoomCenter(Room room)
    {
        if(room == null || room.gridCells.Count == 0) return Vector3.zero;

        Vector2Int centerCell = room.gridCells[room.gridCells.Count / 2];

        //Convert to world position//
        DungeonGenerator generator = FindAnyObjectByType<DungeonGenerator>();
        if(generator != null)
        {
            RoomBuilder builder = generator.GetComponent<RoomBuilder>();
            if(builder != null)
            {
                float cellSize = builder.cellSize;
                int gridSize = generator.gridSize;
                Vector3 offset = new Vector3(-gridSize * cellSize * 0.5f, 0f, -gridSize * cellSize * 0.5f);
                return offset + new Vector3(centerCell.x * cellSize + cellSize * 0.5f, 1f, centerCell.y * cellSize + cellSize * 0.5f);
            }
        }
        return Vector3.zero;
    }

    //Coroutine to handle fade and room transition -EM//
    private IEnumerator TransitionToRoom(Room targetRoom)
    {
        isTransitioning = true;
        SetUIVisibility(false);

        //Get fade panel//
        DoorTransitionManager transitionManager = FindAnyObjectByType<DoorTransitionManager>();

        if(transitionManager != null)
        {
            //Fade to black//
            yield return StartCoroutine(transitionManager.FadeToBlack(fadeDuration));

            //Move player to target room//
            Vector3 targetPosition = GetRoomCenter(targetRoom);
            playerTransform.position = targetPosition;

            //Notify systems about room change//
            OnDoorTransition?.Invoke(this, targetRoom);

            //Stay black briefly//
            yield return new WaitForSeconds(blackScreenDuration);

            //Fade back in//
            yield return StartCoroutine(transitionManager.FadeFromBlack(fadeDuration));
        }
        else
        {
            //Fall back if no transition manager exists//
            Debug.LogWarning("[DungeonDoor] No DoorTransitionManager found! Instant teleport.");
            Vector3 targetPosition = GetRoomCenter(targetRoom);
            playerTransform.position = targetPosition;
            OnDoorTransition?.Invoke(this, targetRoom);
        }

        isTransitioning = false;
    }

    //Show or hide interaction UI -EM//
    private void SetUIVisibility(bool visible)
    {
        if(interactionCanvas != null)
        {
            interactionCanvas.gameObject.SetActive(visible && !isLocked);
        }

        if(interactionPrompt != null)
        {
            interactionPrompt.SetActive(visible && !isLocked);
        }
    }

    //update visual appearance based on UseArchway setting -EM//
    private void UpdateVisualStyle()
    {
        if(doorVisuals != null)
        {
            doorVisuals.SetActive(!useArchway);
        }

        if(archwayVisuals != null)
        {
            archwayVisuals.SetActive(useArchway);
        }
    }

    //Lock the door (prevents opening) -EM//
    public void Lock()
    {
        isLocked = true;
    }

    public void Unlock()
    {
        isLocked = false;
    }

    //Switch between door and archway visuals -EM//
   public void SetVisualStyle(bool archway)
    {
        useArchway = archway;
        UpdateVisualStyle();
    }

    //Properties -EM//
    public bool IsLocked => isLocked;
    public bool ISTransitioning => IsTransitioning;
    public Room ConnectedRoomA => connectedRoomA;
    public Room ConnectedRoomB => connectedRoomB;

    private void OnDrawGizmosSelected()
    {
        //Draw Interaction Range//
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        //Draw door state//
        Gizmos.color = isLocked ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

        //Draw connections ro rooms//
        if(connectedRoomA != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, GetRoomCenter(connectedRoomA));
        }
        if (connectedRoomB != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, GetRoomCenter(connectedRoomB));
        }
    }

}
