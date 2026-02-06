using UnityEngine;
using Unity.AI.Navigation;

[RequireComponent(typeof(NavMeshSurface))]
public class RoomNavMesh : MonoBehaviour
{
    private NavMeshSurface surface;

    private void Awake()
    {
        surface = GetComponent<NavMeshSurface>();
    }

    private void Start()
    {
        BakeNavMesh();
    }

    public void BakeNavMesh()
    {
        surface.BuildNavMesh();
    }

    public void ClearNavMesh()
    {
        surface.RemoveData();
    }
}