using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

//Individual door instance - handles opening/ closing, Navmesh blocking and collision -EM//
//Doors start closed and block NavMesh and line of sight -EM//

[RequireComponent(typeof(NavMeshObstacle))]
public class DungeonDoor : MonoBehaviour
{
    [Header("Door State")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private bool isLocked = false;

    [Header("References")]
    [SerializeField] private GameObject leftDoor;
    [SerializeField] private GameObject rightDoor;
    [SerializeField] private NavMeshObstacle navObstacle;

    [Header("Animation Settings")]
    [Tooltip("How far doors swing open (in degrees)")]
    public float openAngle = 90f;

    [Tooltip("How fast doors open (degrees per second)")]
    public float openSpeed = 120f;

    [Header("Player Detection")]
    [Tooltip("Layer for the player")]
    public LayerMask playerLayer = 1 << 7;

    [Tooltip("Distance player must be to interact with door")]
    public float interactionRange = 3f;

    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;
    private Quaternion leftOpenRotation;
    private Quaternion rightOpenRotation;
    private bool isAnimating = false;

    //Events//
    public delegate void DoorStateChanged(bool isOpen);
    public event DoorStateChanged OnDoorStateChanged;

    private void Awake()
    {
        navObstacle = GetComponent<NavMeshObstacle>();

        //Configure NavMesh obstacle//
        navObstacle.carving = true; //Cuts into NavMesh//
        navObstacle.carveOnlyStationary = false; //Carve even when moving (for animation)//
        navObstacle.carvingMoveThreshold = 0.1f;
        navObstacle.carvingTimeToStationary = 0.1f;
    }

    private void Start()
    {
       //Store initial rotation//
       if(leftDoor != null)
        {
            leftClosedRotation = leftDoor.transform.localRotation;
            leftOpenRotation = leftClosedRotation * Quaternion.Euler(0f, -openAngle, 0f);
        }

        if (rightDoor != null)
        {
            rightClosedRotation = rightDoor.transform.localRotation;
            rightOpenRotation = rightClosedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }

        //Starts closed//
        SetDoorState(false, true);
    }

    private void Update()
    {
        //Handle door animation//
        if(isAnimating)
        {
            AnimateDoors();
        }
    }

    //Open the door (animated) -EM//
    public void Open()
    {
        if(isLocked)
        {
            Debug.Log($"[DungeonDoor] {name} is locked!");
            return;
        }

        if(!isOpen)
        {
            isOpen = true;
            isAnimating = true;

            //Disable NavMesh obstacle when open//
            navObstacle.enabled = false;

            OnDoorStateChanged?.Invoke(true);
            Debug.Log($"[DungeonDoor] {name} opening...");
        }
    }

    //Close the door (animated) -EM//
    public void Close()
    {
        if(isOpen)
        {
            isOpen = false;
            isAnimating = true;

            //Enable NavMesh obstacle when closed//
            navObstacle.enabled = true;

            OnDoorStateChanged?.Invoke(false);
            Debug.Log($"[DungeonDoor] {name} closing...");
        }
    }

    //Set door state immediately (no animation) -EM//
    public void SetDoorState(bool open, bool immediate = false)
    {
        isOpen = open;

        if(immediate)
        {
            if(leftDoor != null)
            {
                leftDoor.transform.localRotation = open ? leftOpenRotation : leftClosedRotation;
            }

            if(rightDoor != null)
            {
                rightDoor.transform.localRotation = open ? rightOpenRotation : rightClosedRotation;
            }

            isAnimating = false;
        }
        else
        {
            isAnimating = true;
        }

        //Update NavMesh obstacle//
        navObstacle.enabled = !open;

        OnDoorStateChanged?.Invoke(open);
    }

    private void AnimateDoors()
    {
        bool leftComplete = true;
        bool rightComplete = true;

        //Animate left door//
        if(leftDoor != null)
        {
            Quaternion targetRotation = isOpen ? leftOpenRotation : leftClosedRotation;
            leftDoor.transform.localRotation = Quaternion.RotateTowards(leftDoor.transform.localRotation, targetRotation, openSpeed * Time.deltaTime);
            leftComplete = Quaternion.Angle(leftDoor.transform.localRotation, targetRotation) < 0.1f;
        }

        //Animate right door//
        if(rightDoor != null)
        {
            Quaternion targetRotation = isOpen? rightOpenRotation : rightClosedRotation;
            rightDoor.transform.localRotation = Quaternion.RotateTowards(rightDoor.transform.localRotation, targetRotation, openSpeed * Time.deltaTime);
            rightComplete = Quaternion.Angle(rightDoor.transform.localRotation, targetRotation) < 0.1f;
        }

        //Stop animating when both doors reach target//
        if(leftComplete && rightComplete)
        {
            isAnimating = false;
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

    //Check if the player is in range to interact -EM//
    public bool isPlayerInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, playerLayer);
        return colliders.Length > 0;
    }

    //Properties -EM//
    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;
    public bool IsAnimating => isAnimating;

    private void OnDrawGizmosSelected()
    {
        //Draw Interaction Range//
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        //Draw door state//
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }


}
