using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Analytics;

//Teleports the player from light to shadow, leaving a 1 - Health clone behind, the clone acts as a decoy to draw enemy attention -EM//
[CreateAssetMenu(fileName = "TeleportEffect", menuName = "Abilities/Effects/Teleport")]
public class TeleportEffect : AbilityEffect
{
    [Header("Teleport Settings")]

    [Header("Shadow Search")]
    [Tooltip("How many random points to try when searching for a valid shadow position")]
    public int maxSearchAttempts = 50;

    [Tooltip("Max distance to search for a valid NavMesh position at each candidate point")]
    public float navMeshSampleDistance = 2f;

    [Tooltip("Minimum distance from the player the teleport destination must be")]
    public float minTeleportDistance = 3f;

    [Tooltip("How high above the candidate to start the floor raycast")]
    public float raycastStartHeight = 10f;

    [Tooltip("Minimum clearence from walls")]
    public float wallClearance = 0.4f;


    [Header("Requirements")]
    [Tooltip("Does caster need to be in light to teleport?")]
    public bool requireCasterInLight = true;
    

    public override void Execute(AbilityExecutionContext context)
    {
        //This effect must target self (the caster moves)//
        if(!targetSelf)
        {
            Debug.LogWarning("[TeleportEffect] TeleportEffect should have targetSelf = true!");
            return;
        }

        if(context.Caster == null)
        {
            Debug.LogWarning("[TeleportEffect] No Caster found!");
            return;
        }

        //Check light requirements//
        if(requireCasterInLight)
        {
            bool casterInLight = LightDetectionManager.Instance != null && LightDetectionManager.Instance.IsPointInLight(context.Caster.transform.position);
            if (!casterInLight)
            {
                MessageUI.Instance?.EnqueueMessage("You must be in light to use the ability!");
                Debug.Log("[TeleportEffect] Teleport failed: Caster not in light");
                return;
            }
            
        }

        //Get the current room//
        RoomLA currentRoom = RoomManager.Instance?.CurrentRoom;
        if(currentRoom == null)
        {
            Debug.LogWarning("[TeleportEffect] No current room found!");
            MessageUI.Instance?.EnqueueMessage("Cannot teleport - no room detected!");
            return;
        }

        //Find a random shadow point in the room//
        if(!TryFindShadowPoint(context.Caster.transform.position, currentRoom, out Vector3 destination))
        {
            MessageUI.Instance?.EnqueueMessage("No shadow to teleport into!");
            Debug.Log("[TeleportEffect] Teleport failed: No valid shadow point found in room");
            return;
        }

        //Get the nav mesh agent//
        NavMeshAgent agent = context.Caster.GetComponent<NavMeshAgent>();
        if(agent == null)
        {
            Debug.LogWarning("[TeleportEffect] Caster has no NavMeshAgent!");
            return;
        }


        //Store original position for clone spawning//
        Vector3 originalPosition = context.Caster.transform.position;

        //Teleport the player//
        agent.Warp(destination);

        //Clear any existing movement destination so the player doesn't resume walking after teleport//
        PlayerController controller = context.Caster.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.ForceStopMovement();
        }

        Debug.Log($"[TeleportEffect] Player teleported from {originalPosition} to {destination}");
        MessageUI.Instance?.EnqueueMessage("You vanish into the shadows!");
    }

    //Search for a random point in the room that is in shadow and on the NavMesh -EM//
    private bool TryFindShadowPoint(Vector3 casterPosition, RoomLA room, out Vector3 result)
    {
        result = Vector3.zero;

        Bounds roomBounds = GetRoomBounds(room);

        int failedContains = 0;
        int failedNavMesh = 0;
        int failedDistance = 0;
        int failedLight = 0;

        for (int i = 0; i < maxSearchAttempts; i++)
        {
            //Pick a random XZ point within the room's bounds//
            float randomX = Random.Range(roomBounds.min.x, roomBounds.max.x);
            float randomZ = Random.Range(roomBounds.min.z, roomBounds.max.z);

            //Use the caster's Y as the floor level - player is always on the floor//
            Vector3 candidate = new Vector3(randomX, casterPosition.y, randomZ);

            //Must be inside the room boundary at floor level//
            if (!room.Contains(candidate))
            {
                failedContains++;
                continue;
            }

            //Must be far enough from the player//
            if (Vector3.Distance(candidate, casterPosition) < minTeleportDistance)
            {
                failedDistance++;
                continue;
            }

            //Must land on the NavMesh//
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                failedNavMesh++;
                continue;
            }

            Vector3 teleportPos = navHit.position; 

            //Must be in shadow/
            if (LightDetectionManager.Instance != null && LightDetectionManager.Instance.IsPointInLight(teleportPos))
            {
                failedLight++;
                continue;
            }

            Debug.Log($"[TeleportEffect] Found shadow point at {teleportPos} after {i + 1} attempts"); 
            result = teleportPos;
            return true;
        }

        Debug.Log($"[TeleportEffect] Search failed after {maxSearchAttempts} attempts - " +
                  $"Contains:{failedContains} Distance:{failedDistance} NavMesh:{failedNavMesh} Light:{failedLight}");
        return false;
    }

    //Calculate the combined bounds of the room from its boundary colliders -EM//
    private Bounds GetRoomBounds(RoomLA room)
    {
        Collider[] colliders = room.BoundaryColliders;

        if (colliders == null || colliders.Length == 0)
        {
            //Fallback: small area around room transform//
            return new Bounds(room.transform.position, Vector3.one * 10f);
        }

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            if (colliders[i] != null) bounds.Encapsulate(colliders[i].bounds);
        }

        return bounds;
    }
}
