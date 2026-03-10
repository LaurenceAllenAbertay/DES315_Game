using UnityEngine;
using UnityEngine.SceneManagement;

//Place this on the win condition prefab in the final room//
//Player clicks the object to trigger the win screen -EM//
public class WinCondition : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("How close the player must be to click and win")]
    public float interactionRange = 3f;

    [Tooltip("Layer mask for the player")]
    public LayerMask playerLayer;

    [Header("Win Screen")]
    [Tooltip("Name of the scene to load on win (leave blank to show UI panel instead")]
    public string winSceneName = "";

    [Tooltip("Optional UI panel to activate on win (used if winSceneName is blank")]
    public GameObject winScreenPanel;

    [Tooltip("Optional prompt shown above the object when the player is in range")]
    public GameObject interactPrompt;

    [Header("Room Marker")]
    [Tooltip("Optional decorative prefab to spawn at the centre of the final room, Leave blank to use the WinCondition object's own model.")]
    public GameObject roomMarkerPrefab;

    [Tooltip("Offset from the win conditions position to spawn the marker")]
    public Vector3 markerOffset = Vector3.zero;

    [Header("Pulse Visual")]
    [Tooltip("If true, the object bobs up abd down to draw attention")]
    public bool doPulse = true;
    public float pulseSpeed = 1.5f;
    public float pulseHeight = 0.15f;

    [Header("Debug")]
    public bool debugMode = true;

    private Transform playerTransform;
    private Vector3 startPosition;
    private bool gameWon = false;

    private void Start()
    {
        startPosition = transform.position;

        //Find player//
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;

        if (interactPrompt != null) interactPrompt.SetActive(false);

        //Spawn the decorative room marker if one is assigned//
        if(roomMarkerPrefab != null)
        {
            GameObject marker = Instantiate(roomMarkerPrefab, transform.position + markerOffset, Quaternion.identity, transform);
            marker.name = "FinalRoomMarker";
        }
    }

    private void Update()
    {
        if (gameWon) return;

        //Pulse animation//
        if(doPulse)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * pulseSpeed) * pulseHeight;
            transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        }

        //Show or hide ineract prompt based on player proximity//
        if(interactPrompt != null && playerTransform != null)
        {
            bool inRange = IsPlayerInRange();
            interactPrompt.SetActive(inRange);
        }
    }

    //Unity calls this when the player clicks the objects collider//
    private void OnMouseDown()
    {
        if(gameWon) return;

        if(!IsPlayerInRange())
        {
            if (debugMode) Debug.Log("[WinCondition] Player clicked but its too far away");
            return;
        }

        TriggerWin();
    }

    public void TriggerWin()
    {
        if (gameWon) return;
        gameWon = true;

        if (debugMode) Debug.Log("[WinCondition] Player won the game!");

        MessageUI.Instance?.EnqueueMessage("You escaped the dungeon! You Win!");

        if(!string.IsNullOrEmpty(winSceneName))
        {
            //Load win scene//
            SceneManager.LoadScene(winSceneName);
        }
        else if(winScreenPanel != null)
        {
            //Activate in-scene win panel//
            winScreenPanel.SetActive(true);

            //Pause time so combat/movement stops//
            Time.timeScale = 0f;
        }
        else
        {
            //Fallback//
            Debug.LogWarning("[WinCondition] No Win scene or panel set! Add on in the inspector");
        }
    }

    private bool IsPlayerInRange()
    {
        if (playerTransform == null) return false;
        return Vector3.Distance(transform.position, playerTransform.position) <= interactionRange;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
