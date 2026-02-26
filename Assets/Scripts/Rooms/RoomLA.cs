using UnityEngine;

public class RoomLA : MonoBehaviour
{
    [Tooltip("Colliders that define the room boundaries.")]
    [SerializeField] private Collider[] boundaryColliders;

    public Collider[] BoundaryColliders => boundaryColliders;

    private LightSource[] roomLights;

    public bool Contains(Vector3 position)
    {
        if (boundaryColliders == null || boundaryColliders.Length == 0)
        {
            return false;
        }

        foreach (var collider in boundaryColliders)
        {
            if (collider == null)
            {
                continue;
            }

            if (IsPointInsideCollider(collider, position))
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        roomLights = GetComponentsInChildren<LightSource>(true);

        if (RoomManager.Instance != null)
            RoomManager.Instance.Register(this);
    }

    private void Start()
    {
        if (RoomManager.Instance != null)
            RoomManager.Instance.RoomChanged += OnRoomChanged;

        bool isCurrentRoom = RoomManager.Instance != null && RoomManager.Instance.CurrentRoom == this;
        SetLightsActive(isCurrentRoom);
    }

    private void OnDestroy()
    {
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.Unregister(this);
            RoomManager.Instance.RoomChanged -= OnRoomChanged;
        }
    }

    private void OnRoomChanged(RoomLA previous, RoomLA current)
    {
        if (current == this)
            SetLightsActive(true);
        else if (previous == this)
            SetLightsActive(false);
    }

    private void SetLightsActive(bool active)
    {
        foreach (var light in roomLights)
        {
            if (light != null)
                light.transform.parent.gameObject.SetActive(active);
        }
    }

    private void Reset()
    {
        TryAutoCollect();
    }

    private void OnValidate()
    {
        TryAutoCollect();
    }

    private void TryAutoCollect()
    {
        if (boundaryColliders == null || boundaryColliders.Length == 0)
        {
            boundaryColliders = GetComponents<Collider>();
        }
    }

    private static bool IsPointInsideCollider(Collider collider, Vector3 position)
    {
        Vector3 closest = collider.ClosestPoint(position);
        return (closest - position).sqrMagnitude <= 0.0001f;
    }
}