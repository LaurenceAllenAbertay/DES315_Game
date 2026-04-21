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

    [Header("Player Spawn")]
    [Tooltip("The player to teleport to the lobby center on start")]
    public Transform playerTransform;
    public Transform cameraTransform;

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

        //Get agentTypeID from existing surface if present, otherwise use default//
        navMeshSurface = GetComponent<NavMeshSurface>();

        

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

    [ContextMenu("Build Rooms")]
    public void BuildRooms()
    {
        if(generator == null)
        {
            if (showDebugLogs) Debug.LogError("[RoomBuilder] No DungeonGenerator found!");
            return;
        }

        ClearRooms();

        List<Room> rooms = generator.GetRooms();
        if(rooms == null || rooms.Count == 0)
        {
            if (showDebugLogs) Debug.LogWarning("[RoomBuilder] No rooms to build! Generate dungeon first.");
            return;
        }

        if(showDebugLogs) Debug.Log($"[RoomBuilder] Building {rooms.Count} rooms...");

        // Pass 1: instantiate all rooms and collect their surfaces — no baking yet
        List<NavMeshSurface> surfacesToBake = new List<NavMeshSurface>();
        foreach(Room room in rooms)
        {
            NavMeshSurface surface = InstantiateRoom(room);
            if (surface != null) surfacesToBake.Add(surface);
        }

        // Agents now exist — disable them before any bake touches the NavMesh
        if(disableAgentsDuringBake)
            DisableAgentsForBake();

        // Pass 2: bake every room surface with agents safely disabled
        foreach(NavMeshSurface surface in surfacesToBake)
            surface.BuildNavMesh();

        ReenableAgentsAfterBake();

        if (showDebugLogs) Debug.Log($"[RoomBuilder] Room building complete!");
    }

    /// <summary>
    /// Instantiates a room prefab at the correct world position and prepares its NavMeshSurface.
    /// Does NOT bake — baking is deferred to BuildRooms so agents can be disabled first.
    /// </summary>
    private NavMeshSurface InstantiateRoom(Room room)
    {
        bool is2x2 = room.gridCells.Count == 4;
        string sizeLabel = is2x2 ? "2x2" : "1x1";

        GameObject prefab = GetRoomPrefab(room, is2x2);
        if(prefab == null)
        {
            if(showDebugLogs) Debug.LogWarning($"[RoomBuilder] No {sizeLabel} prefab assigned for room type {room.roomType}! Skipping room.");
            return null;
        }

        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);
        Vector3 placementPos = GridToWorldPosition(minX, minY);
        float roomWidth = (maxX - minX + 1) * cellSize;
        float roomDepth = (maxY - minY + 1) * cellSize;
        Vector3 centerPos = placementPos + new Vector3(roomWidth * 0.5f, 0f, roomDepth * 0.5f);

        string roomLabel = room.isLobby ? "Lobby" : room.isFinalRoom ? "FinalRoom" : $"Type{room.roomType}";
        GameObject roomContainer = new GameObject($"Room_{roomLabel}_{sizeLabel}_{minX}_{minY}");
        roomContainer.transform.SetParent(roomsParent);
        roomContainer.transform.position = centerPos;

        GameObject roomInstance = Instantiate(prefab, centerPos, Quaternion.identity, roomContainer.transform);
        roomInstance.name = $"Prefab_{roomLabel}_{sizeLabel}";

        // Remove any stale pre-baked NavMeshSurface data the prefab brought with it.
        // Without this, old baked data (potentially including wall tops from before
        // NavMeshModifiers were configured) sits in the global NavMesh alongside the new bake.
        foreach (NavMeshSurface existing in roomInstance.GetComponentsInChildren<NavMeshSurface>())
        {
            existing.RemoveData();
            Destroy(existing);
        }

        // Configure a fresh surface — BuildNavMesh() is called later in BuildRooms()
        NavMeshSurface roomSurface = roomInstance.AddComponent<NavMeshSurface>();
        roomSurface.agentTypeID = navMeshSurface != null ? navMeshSurface.agentTypeID : 0;
        roomSurface.collectObjects = CollectObjects.Children;
        roomSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        int mask = navMeshLayerMask;
        if(excludePlayerAndEnemyLayers)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) mask &= ~(1 << playerLayer);
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) mask &= ~(1 << enemyLayer);
        }
        roomSurface.layerMask = mask;

        if (showDebugLogs) Debug.Log($"[RoomBuilder] Placed {sizeLabel} {roomLabel} prefab at grid ({minX},{minY}) -> world {centerPos}");

        return roomSurface;
    }

    //Retrun the correct prefab for a room based on type and size -EM//
    private GameObject GetRoomPrefab(Room room, bool is2x2)
    {
        //Final room always uses the lobby 2x2 prefab//
        if(room.isFinalRoom)
        {
            return lobby2x2Prefab;
        }

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

        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        

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
            if (agent == null || !agent.isActiveAndEnabled) continue;

            if (showDebugLogs) Debug.Log($"[RoomBuilder] Agent {agent.name} isOnNavMesh:{agent.isOnNavMesh}"); //-EM//

            if (agent.isOnNavMesh) continue;

            if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, agentRebindRadius, NavMesh.AllAreas))
            {
                if (showDebugLogs) Debug.Log($"[RoomBuilder] Warping {agent.name} to NavMesh at {hit.position}"); //-EM//
                agent.Warp(hit.position);
            }
            else
            {
                if (showDebugLogs) Debug.LogWarning($"[RoomBuilder] Could not find NavMesh near {agent.name} at {agent.transform.position}"); //-EM//
            }
        }
    }

    [ContextMenu("Generate and Build")]
    public void GenerateAndBuild()
    {
        if(generator != null)
            generator.GenerateDungeon();

        BuildRooms();
        SpawnPlayerAtLobby();
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

    private void SpawnPlayerAtLobby()
    {
        if (playerTransform == null) return;

        List<Room> rooms = generator.GetRooms();
        if(rooms == null) return;

        Room lobby = rooms.Find(r => r.isLobby);
        if (lobby == null)
        {
            if (showDebugLogs) Debug.LogWarning("[RoomBuilder] No lobby room found for player spawn!");
            return;
        }

        lobby.GetBounds(out int minX, out int minY, out int maxX, out int maxY);
        Vector3 placementPos = GridToWorldPosition(minX, minY);

        float roomWidth = (maxX - minX + 1) * cellSize;
        float roomDepth = (maxY - minY + 1) * cellSize;
        Vector3 centerPos = placementPos + new Vector3(roomWidth * 0.5f, 0f, roomDepth * 0.5f);

        //use NavMeshAgent.warp if available, otherwise direct position//
        NavMeshAgent agent = playerTransform.GetComponent<NavMeshAgent>();
        if(agent != null)
        {
            agent.Warp(centerPos);
        }
        else
        {
            playerTransform.position = centerPos;
        }
        cameraTransform.position = centerPos + new Vector3(0f, 6f, 5f);

        if (showDebugLogs) Debug.Log($"[RoomBuilder] Player spawned at lobby center: {centerPos}");
    }
    
}