using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Runtime.CompilerServices;


//Builds 3D room geometry from the abstract dungeon layout -EM//
//uses procedural walls (easy to texture) + prefab floors (artistic flexibility) -EM//

[RequireComponent(typeof(DungeonGenerator))]
public class RoomBuilder : MonoBehaviour
{
    [Header("Room Settings")]
    [Tooltip("Size of each grid cell in world units (should match visualiser")]
    public float cellSize = 1.8f;

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

    [Header("NavMesh Agents")]
    [Tooltip("Try to re-link agents to the baked NavMesh after runtime generation")]
    public bool rebindAgentsAfterBake = true;
    [Tooltip("Search radius used to snap agents onto the baked NavMesh")]
    public float agentRebindRadius = 2f;
    [Tooltip("Disable agents before baking to avoid invalid NavMesh warnings")]
    public bool disableAgentsDuringBake = true;
    [Tooltip("Enable agents after baking (useful if prefabs start disabled)")]
    public bool enableAgentsAfterBake = true;
    [Tooltip("Which layers of agents should be enabled after baking")]
    public LayerMask agentEnableMask;

    [Header("NavMesh Build Filters")]
    [Tooltip("Base layer mask used for NavMesh building")]
    public LayerMask navMeshLayerMask = ~0;
    [Tooltip("Exclude Player and Enemy layers from NavMesh build")]
    public bool excludePlayerAndEnemyLayers = true;

    [Header("Auto Generation")]
    [Tooltip("Automatically generate and build rooms on Start")]
    public bool autoGenerateAndBuildOnStart = true;
    [Tooltip("Automatically spawn doors after rooms are built")]
    public bool autoSpawnDoorsOnStart = true;

    [Header("Organisation")]
    [Tooltip("Parent object for all room geometry")]
    public Transform roomsParent;

    [Header("References")]
    [Tooltip("Optional DoorSpawner to auto-spawn doors after build")]
    public DoorSpawner doorSpawner;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private DungeonGenerator generator;
    private NavMeshSurface navMeshSurface;
    private readonly List<NavMeshAgent> disabledAgents = new List<NavMeshAgent>();

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

        ConfigureNavMeshSurface();

        if(agentEnableMask == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int mask = 0;
            if(playerLayer >= 0) mask |= 1 << playerLayer;
            if(enemyLayer >= 0) mask |= 1 << enemyLayer;
            agentEnableMask = mask != 0 ? mask : ~0;
        }

        if(doorSpawner == null)
        {
            doorSpawner = GetComponent<DoorSpawner>();
        }

        if(autoGenerateAndBuildOnStart && generator != null)
        {
            generator.generateOnStart = false;
        }
    }

    private void Start()
    {
        if(autoGenerateAndBuildOnStart)
        {
            GenerateAndBuild();
            if(autoSpawnDoorsOnStart && doorSpawner != null)
            {
                doorSpawner.SpawnDoors();
            }
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

        ConfigureNavMeshSurface();
        navMeshSurface.BuildNavMesh();

        if(disableAgentsDuringBake)
        {
            ReenableAgentsAfterBake();
        }
        else if(rebindAgentsAfterBake)
        {
            RebindAgentsToNavMesh();
        }

        if(showDebugLogs)
        {
            Debug.Log("[RoomBuilder] NavMesh Baked!");
        }
    }

    private void RebindAgentsToNavMesh()
    {
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        foreach (NavMeshAgent agent in agents)
        {
            if(agent == null || !agent.isActiveAndEnabled) continue;
            if(agent.isOnNavMesh) continue;

            if(NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, agentRebindRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }

    //Generate dungeon and build rooms in one step -EM//
    [ContextMenu("Generate and Build")]
    public void GenerateAndBuild()
    {
        if(generator != null)
        {
            if(disableAgentsDuringBake)
            {
                DisableAgentsForBake();
            }
            generator.GenerateDungeon();
        }
        BuildRooms();
    }

    #endregion

    private void ConfigureNavMeshSurface()
    {
        if(navMeshSurface == null) return;

        int mask = navMeshLayerMask;
        if(excludePlayerAndEnemyLayers)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if(playerLayer >= 0) mask &= ~(1 << playerLayer);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if(enemyLayer >= 0) mask &= ~(1 << enemyLayer);
        }

        navMeshSurface.layerMask = mask;
    }

    private void DisableAgentsForBake()
    {
        disabledAgents.Clear();
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        foreach (NavMeshAgent agent in agents)
        {
            if(agent == null || !agent.enabled) continue;
            agent.enabled = false;
            disabledAgents.Add(agent);
        }
    }

    private void ReenableAgentsAfterBake()
    {
        foreach (NavMeshAgent agent in disabledAgents)
        {
            if(agent == null) continue;
            agent.enabled = true;
        }

        if(enableAgentsAfterBake)
        {
            NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach (NavMeshAgent agent in agents)
            {
                if(agent == null) continue;
                if(agent.enabled) continue;
                if(((1 << agent.gameObject.layer) & agentEnableMask) == 0) continue;
                agent.enabled = true;
            }
        }

        if(rebindAgentsAfterBake)
        {
            RebindAgentsToNavMesh();
        }

        disabledAgents.Clear();
    }
}
