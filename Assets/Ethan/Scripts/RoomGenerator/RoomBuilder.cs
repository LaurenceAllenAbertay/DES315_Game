using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using System.Runtime.CompilerServices;


//Builds 3D room geometry from the abstract dungeon layout -EM//
//uses procedural walls (easy to texture) + prefab floors (artistic flexibility) -EM//

[RequireComponent(typeof(DungeonGenerator))]
public class RoomBuilder : MonoBehaviour
{
    [Header("Room Settings")]
    [Tooltip("Size of each grid cell in world units (should match visualiser")]
    public float cellSize = 16f;

    [Header("Lobby Prefabs")]
    [Tooltip("Prefab for the 1x1 lobby (cellSize x cellSize)")]
    public GameObject lobby1x1Prefab;
    [Tooltip("Prefab for the 2x2 lobby (cellSize*2 x cellSize*2)")]
    public GameObject lobby2x2Prefab;

    [Header("Room Type 1 Prefab")]
    [Tooltip("Prefab for a 1x1 room type 1")]
    public GameObject roomType1_1x1Prefab;
    [Tooltip("Prefab for a 2x2 room type 1")]
    public GameObject roomType1_2x2Prefab;

    [Header("Room Type 2 Prefab")]
    [Tooltip("Prefab for a 1x1 room type 2")]
    public GameObject roomType2_1x1Prefab;
    [Tooltip("Prefab for a 2x2 room type 2")]
    public GameObject roomType2_2x2Prefab;

    [Header("Room Type 3 Prefab")]
    [Tooltip("Prefab for a 1x1 room type 3")]
    public GameObject roomType3_1x1Prefab;
    [Tooltip("Prefab for a 2x2 room type 3")]
    public GameObject roomType3_2x2Prefab;

    [Header("Room Type 4 Prefab")]
    [Tooltip("Prefab for a 1x1 room type 4")]
    public GameObject roomType4_1x1Prefab;
    [Tooltip("Prefab for a 2x2 room type 4")]
    public GameObject roomType4_2x2Prefab;

    [Header("NavMesh")]
    [Tooltip("Automatically bake NavMesh after building rooms")]
    public bool autoBakeNavMesh = true;

    [Header("Organisation")]
    [Tooltip("Parent object for all room geometry")]
    public Transform roomsParent;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private DungeonGenerator generator;
    private NavMeshSurface navMeshSurface;

    private void Awake()
    {
        generator = GetComponent<DungeonGenerator>();

        //Create rooms parent if needed//
        if(roomsParent == null)
        {
            GameObject parent = new GameObject("Rooms");
            parent.transform.SetParent(transform);
            roomsParent = parent.transform;
        }

        //Get or create NavMeshSurface//
        navMeshSurface = GetComponent<NavMeshSurface>();
        if(navMeshSurface == null)
        {
            navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        }
    }

    //Build all rooms as 3D geometry - EM//
    [ContextMenu("Build Rooms")]
    public void BuildRooms()
    {
        if(generator == null)
        {
            if (showDebugLogs) Debug.LogError("[RoomBuilder] No DungeonGenerator found!");
            return;
        }

        //Clear any existing rooms//
        ClearRooms();

        //Get generated rooms//
        List<Room> rooms = generator.GetRooms();
        if(rooms == null || rooms.Count == 0)
        {
            if (showDebugLogs) Debug.LogWarning("[RoomBuilder] No rooms to build! Generate dungeon first.");
            return;
        }

        if(showDebugLogs)
        {
            Debug.Log($"[RoomBuilder] Building {rooms.Count} rooms...");
        }

        //Build each room//
        foreach(Room room in rooms)
        {
            buildRoom(room);
        }

        //Bake NavMesh//
        if(autoBakeNavMesh)
        {
            BakeNavMesh();
        }

        if(showDebugLogs)
        {
            Debug.Log($"[RoomBuilder] Room building complete!");
        }
    }

    //Build a single room's geomertry - EM//
    private void buildRoom(Room room)
    {
        //Determine if this is a 1x1 or 2x2 room from cell count//
        bool is2x2 = room.gridCells.Count == 4;
        string sizeLabel = is2x2 ? "2x2" : "1x1";

        //Get the correct prefab//
        GameObject prefab = GetRoomPrefab(room, is2x2);

        if(prefab == null )
        {
            if(showDebugLogs)
            {
                Debug.LogWarning($"[RoomBuilder] No {sizeLabel} prefab assigned for room type {room.roomType}! Skipping room.");
            }
            return;
        }

        //Get the bottom-left grid cell of this room for world placement//
        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);
        Vector3 placementPos = GridToWorldPosition(minX, minY);

        //Create a container to keep the hierarchy tidy//
        string roomLabel = room.isLobby ? "Lobby" : $"Type{room.roomType}";
        GameObject roomContainer = new GameObject($"Room_{roomLabel}_{sizeLabel}_{minX}_{minY}");
        roomContainer.transform.SetParent(roomsParent);
        roomContainer.transform.position = placementPos;

        //Instantiate prefab at 1:1 scale//
        GameObject roomInstance = Instantiate(prefab, placementPos, Quaternion.identity, roomContainer.transform);
        roomInstance.name = $"Prefab_{roomLabel}_{sizeLabel}";

        if(showDebugLogs)
        {
            Debug.Log($"[RoomBuilder] Placed {sizeLabel} {roomLabel} prefab at grid ({minX},{minY}) -> world {placementPos}");
        }
    }

    //Retrun the correct prefab for a room based on type and size -EM//
    private GameObject GetRoomPrefab(Room room, bool is2x2)
    {
        if(room.isLobby)
        {
            return is2x2 ? lobby2x2Prefab : lobby1x1Prefab;
        }

        switch(room.roomType)
        {
            case 1: return is2x2 ? roomType1_2x2Prefab : roomType1_1x1Prefab;
            case 2: return is2x2 ? roomType2_2x2Prefab : roomType2_1x1Prefab;
            case 3: return is2x2 ? roomType3_2x2Prefab : roomType3_1x1Prefab;
            case 4: return is2x2 ? roomType4_2x2Prefab : roomType4_1x1Prefab;
            default:
                Debug.LogWarning($"[RoomBuilder] Unkown room type: {room.roomType}");
                return null;
        }
    }

    #region Utility Methods

    //Convert grid coordinates to world positions - EM//
    private Vector3 GridToWorldPosition(int gridX, int gridY)
    {
        int gridSize = generator.gridSize;
        Vector3 offset = new Vector3(-gridSize * cellSize * 0.5f, 0f, -gridSize * cellSize * 0.5f);
        return offset + new Vector3(gridX * cellSize, 0f, gridY * cellSize);
    }

    //Clear all vuilt rooms -EM//
    [ContextMenu("Clear Rooms")]
    public void ClearRooms()
    {
        if(roomsParent == null) return;

        //Destory all children//
        while(roomsParent.childCount > 0)
        {
            DestroyImmediate(roomsParent.GetChild(0).gameObject);
        }

        if(showDebugLogs)
        {
            Debug.Log("[RoomBuilder] Cleared all rooms");
        }
    }

    //Bake the NavMesh - EM//
    private void BakeNavMesh()
    {
        if(navMeshSurface == null) return;

        navMeshSurface.BuildNavMesh();

        if(showDebugLogs)
        {
            Debug.Log("[RoomBuilder] NavMesh Baked!");
        }
    }

    //Generate dungeon and build rooms in one step -EM//
    [ContextMenu("Generate and Build")]
    public void GenerateAndBuild()
    {
        if(generator != null)
        {
            generator.GenerateDungeon();
        }
        BuildRooms();
    }

    #endregion
}
