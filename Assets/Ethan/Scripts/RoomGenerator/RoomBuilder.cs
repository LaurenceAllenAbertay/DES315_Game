using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;


//Builds 3D room geometry from the abstract dungeon layout -EM//
//Creates placeholder rooms using primitives for now, easily replacable with prefabs later -EM//

[RequireComponent(typeof(DungeonGenerator))]
public class RoomBuilder : MonoBehaviour
{
    [Header("Room Settings")]
    [Tooltip("Size of each grid cell in world units (should match visualiser")]
    public float cellSize = 5f;
    public float wallThickness = 0.2f;

    [Tooltip("Height of room walls")]
    public float wallHeight = 3f;

    [Header("Materials")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material lobbyFloorMaterial;

    [Header("Doorway Settings")]
    [Tooltip("Width of doorways between connected rooms")]
    public float doorwayWidth = 2f;

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
        //Create room container//
        GameObject roomObject = new GameObject($"Room_Type{room.roomType}_{room.gridCells.Count}");
        roomObject.transform.SetParent(roomsParent);

        //Get room bounds//
        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);

        //Calculate room world position (centered at origin)//
        Vector3 roomWorldPos = GridToWorldPosition(minX, minY);

        //Build floor//
        BuildFloor(roomObject, room, minX, minY, maxX, maxY, roomWorldPos);

        //Build walls//
        BuildWalls(roomObject, room, minX, minY, maxX, maxY, roomWorldPos);
    }

    //Build the floor for a room - EM//
    private void BuildFloor(GameObject roomObject, Room room, int minX, int minY, int maxX, int maxY, Vector3 basePos)
    {
        //Calculate floor size//
        float width = (maxX - minX + 1) * cellSize;
        float depth = (maxY - minY + 1) * cellSize;

        //Create floor//
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(roomObject.transform);

        //Position and Scale//
        Vector3 floorCenter = basePos + new Vector3(width * 0.5f, -0.5f, depth * 0.5f);
        floor.transform.position = floorCenter;
        floor.transform.localScale = new Vector3(width, 1f, depth);

        //Apply material//
        Renderer renderer = floor.GetComponent<Renderer>();
        if(renderer != null )
        {
            if(room.isLobby && lobbyFloorMaterial != null)
            {
                renderer.material = lobbyFloorMaterial;
            }
            else if (floorMaterial != null)
            {
                renderer.material = floorMaterial;
            }
        }

        //Add to NavMesh//
        floor.layer = LayerMask.NameToLayer("Default");
    }

    //Build walls around the perimeter of a room - EM//
    private void BuildWalls(GameObject roomObject, Room room, int minX, int minY, int maxX, int maxY, Vector3 basePos)
    {
        //Create walls container//
        GameObject wallsContainer = new GameObject("Walls");
        wallsContainer.transform.SetParent(roomObject.transform);

        //Check each cell in the room//
        foreach (Vector2Int cell in room.gridCells)
        {
            Vector3 cellWorldPos = GridToWorldPosition(cell.x, cell.y);

            //Check each cardinal direction//
            BuildWallIfNeeded(wallsContainer, room, cell, new Vector2Int(0, 1), cellWorldPos); //North//
            BuildWallIfNeeded(wallsContainer, room, cell, new Vector2Int(1, 0), cellWorldPos); //East//
            BuildWallIfNeeded(wallsContainer, room, cell, new Vector2Int(0, -1), cellWorldPos); //South//
            BuildWallIfNeeded(wallsContainer, room, cell, new Vector2Int(-1, 0), cellWorldPos); //West//
        }
    }

    //Build a wall segment if needed (edge of room or no connection) - EM//
    private void BuildWallIfNeeded(GameObject wallsContainer, Room room, Vector2Int cell, Vector2Int direction, Vector3 cellWorldPos)
    {
        Vector2Int neighbour = cell + direction;

        //If neighbour is part of this room, no wall needed//
        if(room.gridCells.Contains(neighbour))
        {
            return;
        }

        //Check if there's a connected room in this direction//
        bool hasConnection = HasConnectionInDirection(room, cell, direction);

        //Determine wall position and roation based on direction//
        Vector3 wallPos = cellWorldPos;
        Vector3 wallScale;
        Quaternion wallRot = Quaternion.identity;

        if(direction == new Vector2Int(0,1)) //North//
        {
            wallPos += new Vector3(cellSize * 0.5f, wallHeight * 0.5f, cellSize);
            wallScale = new Vector3(cellSize, wallHeight, wallThickness);
            wallRot = Quaternion.identity;
        }
        else if(direction == new Vector2Int(1,0)) //East//
        {
            wallPos += new Vector3(cellSize, wallHeight * 0.5f, cellSize * 0.5f);
            wallScale = new Vector3(wallThickness, wallHeight, cellSize);
            wallRot = Quaternion.identity;
        }
        else if(direction == new Vector2Int(0,-1)) //South//
        {
            wallPos += new Vector3(cellSize * 0.5f, wallHeight * 0.5f, 0f);
            wallScale = new Vector3(cellSize, wallHeight, wallThickness);
            wallRot = Quaternion.identity;
        }
        else //West//
        {
            wallPos += new Vector3(0f, wallHeight * 0.5f, cellSize * 0.5f);
            wallScale = new Vector3(wallThickness, wallHeight, cellSize);
            wallRot = Quaternion.identity;
        }

        //If there's a connection, create a wall with a doorway//
        if (hasConnection)
        {
            BuildWallWithDoorway(wallsContainer, wallPos, wallScale, wallRot, direction);
        }
        else
        {
            //Solid wall//
            BuildSolidWall(wallsContainer, wallPos, wallScale, wallRot);
        }
    }

    //Check if this room has a connection in the given direction from this cell - EM//
    private bool HasConnectionInDirection(Room room, Vector2Int cell, Vector2Int direction)
    {
        Vector2Int neighbour = cell + direction;

        //Find which room (if any) occupies the neighbour cell//
        foreach(Room connectedRoom in room.connections)
        {
            if(connectedRoom.gridCells.Contains(neighbour))
            {
                return true;
            }
        }
        return false;
    }

    //Build a solid wall segment -EM//
    private void BuildSolidWall(GameObject parent, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent.transform);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.transform.rotation = rotation;

        //Apply material//
        if(wallMaterial != null)
        {
            wall.GetComponent<Renderer>().material = wallMaterial;
        }
    }

    //Build a wall with a doorway opening - EM//
    private void BuildWallWithDoorway(GameObject parent, Vector3 position, Vector3 scale, Quaternion rotation, Vector2Int direction)
    {
        //For now, create two walls segments with a gap in the middle//
        bool isVertical = (direction.x != 0); //East or West//

        float wallLength = isVertical ? scale.z : scale.x;
        float sideWallLength = (wallLength - doorwayWidth) * 0.5f;

        if (sideWallLength <= 0.1f)
        {
            //Door is too widce skip wall entirely//
            return;
        }

        //Create left/button wall segment//
        GameObject wall1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall1.name = "WallSegment";
        wall1.transform.SetParent(parent.transform);
        wall1.transform.rotation = rotation;

        //Create right/top wall segment//
        GameObject wall2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall2.name = "WallSegment";
        wall2.transform.SetParent(parent.transform);
        wall2.transform.rotation = rotation;

        if(isVertical)
        {
            //East/West walls//
            float offset = (wallLength - sideWallLength) * 0.5f;

            wall1.transform.position = position + rotation * new Vector3(0f, 0f, -offset);
            wall1.transform.localScale = new Vector3(scale.x, scale.y, sideWallLength);

            wall2.transform.position = position + rotation * new Vector3(0f, 0f, offset);
            wall2.transform.localScale = new Vector3(scale.x, scale.y, sideWallLength);
        }
        else
        {
            //North/South walls//
            float offset = (wallLength - sideWallLength) * 0.5f;

            wall1.transform.position = position + rotation * new Vector3(-offset, 0f, 0f);
            wall1.transform.localScale = new Vector3(sideWallLength, scale.y, scale.z);

            wall2.transform.position = position + rotation * new Vector3(offset, 0f, 0f);
            wall2.transform.localScale = new Vector3(sideWallLength, scale.y, scale.z);
        }

        //Apply materials//
        if(wallMaterial != null)
        {
            wall1.GetComponent<Renderer>().material = wallMaterial;
            wall2.GetComponent<Renderer>().material = wallMaterial;
        }
    }

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
    [ContextMenu("Generate & Build")]
    public void GenerateAndBuild()
    {
        if(generator != null)
        {
            generator.GenerateDungeon();
        }
        BuildRooms();
    }
}
