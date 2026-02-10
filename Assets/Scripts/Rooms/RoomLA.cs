using System;
using UnityEngine;

public class RoomLA : MonoBehaviour
{
    [Tooltip("Colliders that define the room boundaries.")]
    [SerializeField] private Collider[] boundaryColliders;

    public Collider[] BoundaryColliders => boundaryColliders;

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
        // ClosestPoint returns the same position when the point is inside the collider.
        Vector3 closest = collider.ClosestPoint(position);
        return (closest - position).sqrMagnitude <= 0.0001f;
    }
}
