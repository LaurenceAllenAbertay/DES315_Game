using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

//Spawns doors at all room connection points -EM//
//Doors block NavMesh and line of sight between rooms -EM//
public class DoorSpawner : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("Prefab for door (Leave empty to create simple doors)")]
    public GameObject doorPrefab;

    [Tooltip("Width of door opening")]
    public float doorWidth = 2f;

    [Tooltip("Height of door")]
    public float doorHeight = 3f;

    [Tooltip("Thickness of door")]
    public float doorThickness = 0.2f;

    [Header("Materials")]
    public Material doorMaterial;
    public Material frameMaterial;

    [Header("References")]
    [Tooltip("The RoomBuilder component")]
    public RoomBuilder roomBuilder;

    [Tooltip("Parent for all doors")]
    public Transform doorsParent;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private DungeonGenerator generator;
    private List<DungeonDoor> spawnedDoors = new List<DungeonDoor>();

    private void Awake()
    {
        if(roomBuilder == null)
        {
            roomBuilder = GetComponent<RoomBuilder>();
        }

        generator = GetComponent<DungeonGenerator>();

        if(doorsParent == null)
        {
            GameObject parent = new GameObject("Doors");
            parent.transform.SetParent(transform);
            doorsParent = parent.transform;
        }
    }

    //Spawn doors at all room connections -EM//
    [ContextMenu("Spawn Doors")]
    public void SpawnDoors()
    {
        if(generator == null)
        {
            Debug.LogError("[DoorSpawner] No DungeonGenerator found!");
            return;
        }

        //Clear existing doors//
        ClearDoors();

        List<Room> rooms = generator.GetRooms();
        if(rooms == null || rooms.Count == 0)
        {
            Debug.LogWarning("[DoorSpawner] No rooms found!");
            return;
        }

        int doorsSpawned = 0;
        HashSet<string> processedConnections = new HashSet<string>();

        //For each room, check its connections//
        foreach(Room room in rooms)
        {
            foreach(Room connectedRoom in room.connections)
            {
                //Find ALL connection points between these two rooms//
                List<ConnectionPoint> connectionPoints = FindAllConnectionPoints(room, connectedRoom);

                foreach (ConnectionPoint cp in connectionPoints)
                {
                    //Create unique key for THIS specific door location//
                    string doorKey = GetDoorKey(cp.position);

                    if (processedConnections.Contains(doorKey))
                    {
                        continue; //Already spawned door here//
                    }

                    processedConnections.Add(doorKey);
                    SpawnDoor(cp.position, cp.direction, room, connectedRoom);
                    doorsSpawned++;
                }
            }
        }

        if(showDebugLogs)
        {
            Debug.Log($"[DoorSpawner] Spawned {doorsSpawned} doors");
        }
    }

    //Simple struct to hold connection point data -EM//
    private struct ConnectionPoint
    {
        public Vector3 position;
        public Vector2Int direction;
    }
    private string GetDoorKey(Vector3 position)
    {
        //Round to nearest 0.1 to avoid floating point issue//
        int x = Mathf.RoundToInt(position.x * 10f);
        int z = Mathf.RoundToInt(position.z * 10f);
        return $"{x}_{z}";
    }

    private List<ConnectionPoint> FindAllConnectionPoints(Room roomA, Room roomB)
    {

        List<ConnectionPoint> connections = new List<ConnectionPoint>();

        //Find adjacent cells between the two rooms//
        foreach(Vector2Int cellA in roomA.gridCells)
        {
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0,1), //North//
                new Vector2Int(1,0), //East//
                new Vector2Int(0,-1), //South//
                new Vector2Int(-1,0) //West//
            };

            foreach(Vector2Int dir in directions)
            {
                Vector2Int neighbour = cellA + dir;

                //If this neighbour belongs to roomB, it's a connection point//
                if (roomB.gridCells.Contains(neighbour))
                {
                    Vector3 cellWorldPos = GridToWorldPosition(cellA);
                    float cellSize = roomBuilder.cellSize;
                    Vector3 doorOffset = new Vector3(dir.x * cellSize * 0.5f, 0f, dir.y * cellSize * 0.5f);

                    ConnectionPoint cp = new ConnectionPoint
                    {
                        position = cellWorldPos + doorOffset,
                        direction = dir
                    };

                    connections.Add(cp);
                }
            }
        }
        return connections;
    }

    private void SpawnDoor(Vector3 position, Vector2Int direction, Room roomA, Room roomB)
    {
        GameObject doorObject;

        if(doorPrefab != null)
        {
            doorObject = Instantiate(doorPrefab, doorsParent);
        }
        else
        {
            doorObject = CreateDefaultDoor();
        }

        doorObject.name = $"Door_{roomA.GetHashCode()}_{roomB.GetHashCode()}";
        doorObject.transform.position = position;

        //Orient door based on connection direction//
        //Door faces perpendicular to the wall//
        if(direction.x != 0)
        {
            //Door faces North - South//
            doorObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }
        else
        {
            //Door faces East - West//
            doorObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }

        //Add DungeonDoor component if not present//
        DungeonDoor doorScript = doorObject.GetComponent<DungeonDoor>();
        if(doorScript == null)
        {
            doorScript = doorObject.AddComponent<DungeonDoor>();
        }

        spawnedDoors.Add(doorScript);

        if (showDebugLogs)
        {
            Debug.Log($"[DoorSpawner] Spawned door at {position}, direction {direction}");
        }
    }

    private GameObject CreateDefaultDoor()
    {
        GameObject doorObject = new GameObject("Door");

        //Create doorframe//
        CreateDoorFrame(doorObject);

        //Create left door//
        GameObject leftDoor = CreateDoorPanel("LeftDoor", new Vector3(-doorWidth * 0.25f, doorHeight * 0.5f, 0f));
        leftDoor.transform.SetParent(doorObject.transform);

        //Create right door//
        GameObject rightDoor = CreateDoorPanel("RightDoor", new Vector3(doorWidth * 0.25f, doorHeight * 0.5f, 0f));
        rightDoor.transform.SetParent(doorObject.transform);

        //Setup Dungeon Door component references//
        DungeonDoor doorScript = doorObject.AddComponent<DungeonDoor>();

        //use reflection component references//
        var leftDoorField = typeof(DungeonDoor).GetField("leftDoor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rightDoorField = typeof(DungeonDoor).GetField("rightDoor", System.Reflection.BindingFlags.NonPublic |System.Reflection.BindingFlags.Instance);

        leftDoorField?.SetValue(doorScript, leftDoor);
        rightDoorField?.SetValue(doorScript, rightDoor);

        return doorObject;
    }

    private void CreateDoorFrame(GameObject parent)
    {
        //Left Post//
        GameObject leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPost.name = "LeftPost";
        leftPost.transform.SetParent(parent.transform);
        leftPost.transform.localPosition = new Vector3(-doorWidth * 0.5f - 0.15f, doorHeight * 0.5f, 0f);
        leftPost.transform.localScale = new Vector3(0.3f, doorHeight, doorThickness * 2f);
        ApplyMaterial(leftPost, frameMaterial);

        //Right Post//
        GameObject rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPost.name = "RightPost";
        rightPost.transform.SetParent(parent.transform);
        rightPost.transform.localPosition = new Vector3(doorWidth * 0.5f + 0.15f, doorHeight * 0.5f, 0f);
        rightPost.transform.localScale = new Vector3(0.3f, doorHeight, doorThickness * 2f);
        ApplyMaterial(rightPost, frameMaterial);

        //Top beam//
        GameObject topBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topBeam.name = "TopBeam";
        topBeam.transform.SetParent(parent.transform);
        topBeam.transform.localPosition = new Vector3(0f, doorHeight + 0.15f, 0f);
        topBeam.transform.localScale = new Vector3(doorWidth + 0.6f, 0.3f,doorThickness * 2f);
        ApplyMaterial(topBeam, frameMaterial);
    }

    private GameObject CreateDoorPanel(string name, Vector3 localPosition)
    {
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = name;
        door.transform.localPosition = localPosition;
        door.transform.localScale = new Vector3(doorWidth * 0.5f, doorHeight, doorThickness);

        ApplyMaterial (door, doorMaterial);

        return door;
    }

    private void ApplyMaterial(GameObject obj, Material mat)
    {
        if(mat != null)
        {
            obj.GetComponent<Renderer>().material = mat;
        }
    }

    private Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        if (roomBuilder == null) return Vector3.zero;

        int gridSize = generator.gridSize;
        float cellSize = roomBuilder.cellSize;
        Vector3 offset = new Vector3(-gridSize * cellSize * 0.5f, 0f, -gridSize * cellSize * 0.5f);

        return offset + new Vector3(gridPos.x * cellSize + cellSize * 0.5f, 0f, gridPos.y * cellSize + cellSize * 0.5f);
    }

    [ContextMenu("Clear Doors")]
    public void ClearDoors()
    {
        if (doorsParent == null) return;

        while(doorsParent.childCount > 0)
        {
            DestroyImmediate(doorsParent.GetChild(0).gameObject);
        }

        spawnedDoors.Clear();

        if(showDebugLogs)
        {
            Debug.Log("[DoorSpawner] Cleared all doors");
        }
    }

    //Get all spawned doors -EM//
    public List<DungeonDoor> GetDoors()
    {
        return spawnedDoors;
    }
}
