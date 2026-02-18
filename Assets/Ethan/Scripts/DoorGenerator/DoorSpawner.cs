using Mono.Cecil;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor.MemoryProfiler;
using UnityEngine;

//Spawns doors at all room connection points -EM//
//Doors are now static archways for room transitions -EM//
public class DoorSpawner : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("Prefab for door (Leave empty for yellow cube)")]
    public GameObject doorPrefab;

    [Tooltip("Size of door marker cube")]
    public float doorMarkerSize = 0.5f;

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
                    string doorKey = GetDoorKey(cp.doorPosition);

                    if (processedConnections.Contains(doorKey))
                    {
                        continue; //Already spawned door here//
                    }

                    processedConnections.Add(doorKey);
                    SpawnDoor(cp, room, connectedRoom);
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
        public Vector3 doorPosition; //World position of door marker//
        public Vector2Int direction; //Direction from roomA to roomB//
        public Vector3 teleportOffsetA; //Offset for teleporting To roomA//
        public Vector3 teleportOffsetB; //Offset for teleporting To roomB//
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
        float cellSize = roomBuilder.cellSize;

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
                Vector2Int cellB = cellA + dir;

                //If this neighbour belongs to roomB, it's a connection point//
                if (roomB.gridCells.Contains(cellB))
                {
                    ConnectionPoint cp = new ConnectionPoint();
                    cp.direction = dir;

                    //Get world position of cellA's bottom-left corner//
                    Vector3 cellAWorld = GridToWorldPosition(cellA);

                    //Calculate door position at the center of the shared edge//
                    //For a cell at (0,0) with cellSize 18//
                    //North edge center: (0.9, 0, 1.8)//
                    //East edge center: (1.8,0,0.9)//
                    //South edge center: (0.9, 0, 0)//
                    //West edge center: (0,0,0.9)//

                    //How far to pull door inward from wall edge//
                    float doorInset = 1f;

                    if(dir == new Vector2Int(0,1)) //North//
                    {
                        cp.doorPosition = cellAWorld + new Vector3(cellSize * 0.5f, 0.5f, cellSize - doorInset);
                        cp.teleportOffsetA = new Vector3(0, 0, -0.5f); //Step back into roomA//
                        cp.teleportOffsetB = new Vector3(0, 0, 0.5f); //Step forward into roomB//
                    }
                    else if (dir == new Vector2Int(1, 0)) //East//
                    {
                        cp.doorPosition = cellAWorld + new Vector3(cellSize - doorInset, 0.5f, cellSize * 0.5f);
                        cp.teleportOffsetA = new Vector3(-0.5f, 0, 0f); //Step back into roomA//
                        cp.teleportOffsetB = new Vector3(0.5f, 0, 0); //Step forward into roomB//
                    }
                    else if (dir == new Vector2Int(0, -1)) //South//
                    {
                        cp.doorPosition = cellAWorld + new Vector3(cellSize * 0.5f, 0.5f, doorInset);
                        cp.teleportOffsetA = new Vector3(-0.5f, 0, 0f); //Step back into roomA//
                        cp.teleportOffsetB = new Vector3(0.5f, 0, 0); //Step forward into roomB//
                    }
                    else //West//
                    {
                        cp.doorPosition = cellAWorld + new Vector3(doorInset, 0.5f, cellSize * 0.5f);
                        cp.teleportOffsetA = new Vector3(-0.5f, 0, 0f); //Step back into roomA//
                        cp.teleportOffsetB = new Vector3(0.5f, 0, 0); //Step forward into roomB//
                    }

                    connections.Add(cp);
                }
            }
        }
        return connections;
    }

    private void SpawnDoor(ConnectionPoint connection, Room roomA, Room roomB)
    {
        GameObject doorObject;

        if(doorPrefab != null)
        {
            doorObject = Instantiate(doorPrefab, doorsParent);
        }
        else
        {
            //Create default yellow cube marker//
            doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorObject.transform.SetParent(doorsParent);

            //Make it yellow and visible//
            Renderer renderer = doorObject.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = Color.yellow;

            //Scale it//
            doorObject.transform.localScale = Vector3.one * doorMarkerSize;
        }

        doorObject.name = $"Door_{roomA.GetHashCode()}_{roomB.GetHashCode()}";
        doorObject.transform.position = connection.doorPosition;

        //Add DungeonDoor component//
        DungeonDoor doorScript = doorObject.GetComponent<DungeonDoor>();
        if(doorScript == null)
        {
            doorScript = doorObject.AddComponent<DungeonDoor>();
        }

        //Setup teleportation data//
        doorScript.SetupTeleportDoor(connection.doorPosition, connection.doorPosition + connection.teleportOffsetA, connection.doorPosition + connection.teleportOffsetB, roomA, roomB);

        spawnedDoors.Add(doorScript);

        if(showDebugLogs)
        {
            Debug.Log($"[DoorSpawner] Spawned door at {connection.doorPosition}, direction {connection.direction}");
        }
    }

    private Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        if (roomBuilder == null) return Vector3.zero;

        int gridSize = generator.gridSize;
        float cellSize = roomBuilder.cellSize;
        Vector3 offset = new Vector3(-gridSize * cellSize * 0.5f, 0f, -gridSize * cellSize * 0.5f);

        return offset + new Vector3(gridPos.x * cellSize, 0f, gridPos.y * cellSize);
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
