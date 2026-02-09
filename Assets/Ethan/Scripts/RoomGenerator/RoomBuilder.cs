using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;


//Builds 3D room geometry from the abstract dungeon layout -EM//
//uses procedural walls (easy to texture) + prefab floors (artistic flexibility) -EM//

[RequireComponent(typeof(DungeonGenerator))]
public class RoomBuilder : MonoBehaviour
{
    [Header("Room Settings")]
    [Tooltip("Size of each grid cell in world units (should match visualiser")]
    public float cellSize = 10f;
    public float wallThickness = 0.2f;

    [Tooltip("Height of room walls")]
    public float wallHeight = 3f;

    [Header("Floor Prefabs")]
    [Tooltip("Floor prefab for the lobby (roomType 0)")]
    public GameObject lobbyFloorPrefab;

    [Tooltip("Floor prefab for room type 1")]
    public GameObject roomType1FloorPrefab;

    [Tooltip("Floor prefab for room type 2")]
    public GameObject roomType2FloorPrefab;

    [Tooltip("Floor prefab for room type 3")]
    public GameObject roomType3FloorPrefab;

    [Tooltip("Floor prefab for room type 4")]
    public GameObject roomType4FloorPrefab;

    [Header("Materials")]
    public Material wallMaterial;

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

        //Spawn doors if available//
        SpawnDoors();

        //Setup fog of war if available after short delay//
        StartCoroutine(DelayedFogOfWar());

        if(showDebugLogs)
        {
            Debug.Log($"[RoomBuilder] Room building complete!");
        }
    }

    //Delayed fog setup to ensure doors are fully created first -EM//
    private System.Collections.IEnumerator DelayedFogOfWar()
    {
        //Wait for 2 frames for doors to finish//
        yield return null;
        yield return null;

        SetupFogOfWar();
    }

    //Try to setup fog of war automatically after building -EM//
    private void SetupFogOfWar()
    {
        //Try shader based fog first//
        ShaderFogOfWar shaderFog = FindFirstObjectByType<ShaderFogOfWar>();
        if(shaderFog != null)
        {
            shaderFog.SetupFogOfWar();
            if(showDebugLogs)
            {
                Debug.Log("[RoomBuilder] Triggered shader fog of war setup");
            }
            return;
        }
    }

    //Try to spawn doors automatically after building -EM//
    private void SpawnDoors()
    {
        DoorSpawner doorSpawner = FindAnyObjectByType<DoorSpawner>();
        if(doorSpawner != null)
        {
            doorSpawner.SpawnDoors();
            if(showDebugLogs)
            {
                Debug.Log("[RoomBuilder] Triggered door spawning");
            }
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

        //Build floor using prefab for this room type//
        BuildFloorPrefab(roomObject, room, minX, minY, maxX, maxY, roomWorldPos);

        //Build walls//
        BuildWallsProcedural(roomObject, room);
    }

    #region Floor Building (Prefab-Based)


    //Build the floor for a room using prefab tiles - EM//
    private void BuildFloorPrefab(GameObject roomObject, Room room, int minX, int minY, int maxX, int maxY, Vector3 basePos)
    {
        //Choose correct prefab//
        GameObject floorPrefab = GetFloorPrefabForRoomType(room);

        if (floorPrefab == null)
        {
            Debug.LogWarning("[RoomBuilder] No floor tile prefab assigned for room type {room.roomType}! Skipping floor.");
            return;
        }

        //Create floor container//
        GameObject floorContainer = new GameObject("Floor");
        floorContainer.transform.SetParent(roomObject.transform);

        //Calculate floor size//
        float width = (maxX - minX + 1) * cellSize;
        float depth = (maxY - minY + 1) * cellSize;

        //Calculate center position//
        Vector3 floorCenter = basePos + new Vector3(width * 0.5f, 0f, depth * 0.5f);

        //Instantiate the floor prefab//
        GameObject floor = Instantiate(floorPrefab, floorCenter, Quaternion.identity, floorContainer.transform);
        floor.name = room.isLobby ? "Floor_Lobby" : $"Floor_Type{room.roomType}";

        //Scale to match room size//
        floor.transform.localScale = new Vector3(width, 1f, depth);

        if(showDebugLogs)
        {
            Debug.Log($"[RoomBuilder] Created {(room.isLobby ? "lobby" : $"type {room.roomType}")} floor: {width}x{depth} units");
        }

    }

    //Get the appropriate floor prefab based on room type - EM//
    private GameObject GetFloorPrefabForRoomType(Room room)
    {
        if(room.isLobby)
        {
            return lobbyFloorPrefab;
        }
        switch(room.roomType)
        {
            case 1: return roomType1FloorPrefab;
            case 2: return roomType2FloorPrefab;
            case 3: return roomType3FloorPrefab;
            case 4: return roomType4FloorPrefab;
            default:
                Debug.LogWarning($"[RoomBuilder] Unknown room type: {room.roomType}");
                return null;
        }
    }
    #endregion

    #region Wall Building (Procedural)
    //Build walls around the perimeter of a room using procedural geometry - EM//
    private void BuildWallsProcedural(GameObject roomObject, Room room)
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

    //Build a wall segment if needed using procedural geomtry (edge of room or no connection) - EM//
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

    //Build a solid wall segment using procedural geometry -EM//
    private void BuildSolidWall(GameObject parent, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent.transform);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.transform.rotation = rotation;

        //Apply material//
        if (wallMaterial != null)
        {
            wall.GetComponent<Renderer>().material = wallMaterial;
        }
    }

    //Build a wall with a doorway opening using procedural geometry - EM//
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

        if (isVertical)
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
        if (wallMaterial != null)
        {
            wall1.GetComponent<Renderer>().material = wallMaterial;
            wall2.GetComponent<Renderer>().material = wallMaterial;
        }
    }

    #endregion

    #region Utility Methods

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

    #endregion
}
