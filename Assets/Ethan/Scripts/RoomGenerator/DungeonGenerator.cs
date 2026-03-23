using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

//Generates procedural dungeon layouts with random room types and merging - EM//
public class DungeonGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Grid dimensions (will be gridsize x gridsize)")]
    public int gridSize = 11;

    [Tooltip("Percentage of grid to fill with rooms (0 - 1)")]
    [Range(0f, 1f)]
    public float occupationPercentage = 0.6f;

    [Header("Room Settings")]
    [Tooltip("Number of different room types available")]
    public int roomTypeCount = 4;

    [Header("Seed Settings")]
    [Tooltip("Enable to use a specific seed for reproducible generation")]
    public bool useRandomSeed = true;

    [Tooltip("Seed value for dungeon generation (ignored if useRandomSeed is true)")]
    public int seed = 0;

    [Tooltip("The seed used in the last generation - read this to recreate the dungeon")]
    [HideInInspector] public string lastSeedUsed = "";

    [Tooltip("Chance (0-1) a new cell inherits a neighbour's type instead of rolling randomly - higher values cluster same-type cells together producing more 2x2 merges")]
    [Range(0f, 1f)]
    //0.0 fully random, 0.65 default increase in 2x2, 0.85 heavy clustering, 1.0 every cell inherits a neighbour//
    public float typeClusterChance = 0.65f;

    [Header("Debug")]
    public bool generateOnStart = true;
    public bool showDebugLogs = true;

    //Grid Data//
    private CellData[,] grid;
    private List<Room> rooms = new List<Room>();
    private Vector2Int startPosition;

    void Start()
    {
        if (generateOnStart)
        {
            GenerateDungeon();
        }
    }

    //Main dungeon generation entry point - EM//
    public void GenerateDungeon()
    {
        //Seed setup//
        if(useRandomSeed)
        {
            //Random seed based on time//
            seed = System.Environment.TickCount;
        }

        Random.InitState(seed);
        lastSeedUsed = seed.ToString();

        if(showDebugLogs)
        {
            Debug.Log($"[DungeonGenerator] Using seed: {lastSeedUsed}");
        }

        InitialiseGrid();
        GrowDungeon();
        ReserveFinalRoomCells();
        MergeRooms();
        CalculateConnections();

        if (showDebugLogs)
        {
            Debug.Log($"[DungeonGenerator] Dungeon generated: {rooms.Count} rooms created");
        }
    }

    //Initialise Empty Grid - EM//
    private void InitialiseGrid()
    {
        grid = new CellData[gridSize, gridSize];
        rooms.Clear();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                grid[x, y] = new CellData();
            }
        }

        //Place starting room at the center//
        startPosition = new Vector2Int(gridSize / 2, gridSize / 2);
        grid[startPosition.x, startPosition.y].isOccupied = true;
        grid[startPosition.x, startPosition.y].roomType = 0;
        grid[startPosition.x, startPosition.y].isLobby = true;
    }

    //Grow Dungeon outward from center until target occupation is reached -EM//
    private void GrowDungeon()
    {
        int targetCells = Mathf.RoundToInt(gridSize * gridSize * occupationPercentage);
        //Start with lobby room//
        int currentCells = 1;

        HashSet<Vector2Int> frontier = new HashSet<Vector2Int>();

        //Add initial neighbours of lobby to frontier//
        AddNeighboursToFrontier(startPosition, frontier);

        int safetyCounter = 0;
        //Prevent infinite loops//
        int maxIterations = gridSize * gridSize * 10;

        while (currentCells < targetCells && frontier.Count > 0 && safetyCounter < maxIterations)
        {
            safetyCounter++;
            
            //Convert to list to pick randon element//
            List<Vector2Int> frontierList = new List<Vector2Int>(frontier);
            //Pick a random cell from frontier//
            int randomIndex = Random.Range(0, frontierList.Count);
            Vector2Int cell = frontierList[randomIndex];
            frontier.Remove(cell);

            //Skip if already occupied//
            if (grid[cell.x, cell.y].isOccupied) continue;

            //Occupy this cell with a random room type (1 - 4, 0 reserved for starting room/lobby)//
            grid[cell.x, cell.y].isOccupied = true;
            grid[cell.x, cell.y].roomType = PickRoomType(cell);
            currentCells++;

            //Add this cell's neighbour to frontier//
            AddNeighboursToFrontier(cell, frontier);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[DungeonGenerator] Grid growth complete: {currentCells}/{targetCells} cells occupied (iterations: {safetyCounter})");
        }
    }

    //Add unoccupied cardinal neighbours to frontier set - EM//
    private void AddNeighboursToFrontier(Vector2Int cell, HashSet<Vector2Int> frontier)
    {
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0,1), //North//
            new Vector2Int(1,0), //East//
            new Vector2Int(0,-1), //South//
            new Vector2Int(-1,0) //West//
        };

        foreach (Vector2Int dir in directions)
        {
            Vector2Int neighbour = cell + dir;

            //Check bounds//
            if (neighbour.x < 0 || neighbour.x >= gridSize || neighbour.y < 0 || neighbour.y >= gridSize) continue;

            //Only add if not occupied and not already in frontier//
            if (!grid[neighbour.x, neighbour.y].isOccupied && !frontier.Contains(neighbour))
            {
                frontier.Add(neighbour);
            }
        }
    }

    //Pick a room type for a newly occupied cell -EM//
    //Rolls against typeClusterChance to inherit a neighbour's type, otherwise picks randomly -EM//
    private int PickRoomType(Vector2Int cell)
    {
        if(Random.value < typeClusterChance)
        {
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0,1),
                new Vector2Int(1,0),
                new Vector2Int(0,-1),
                new Vector2Int(-1,0)
            };

            //Collect occupied neighbour types, ignoring lobby (type 0) -EM//
            List<int> neighbourTypes = new List<int>();
            foreach(Vector2Int dir in directions)
            {
                Vector2Int neighbour = cell + dir;
                if(neighbour.x < 0 || neighbour.x >= gridSize || neighbour.y < 0 || neighbour.y >= gridSize) continue;
                if (!grid[neighbour.x, neighbour.y].isOccupied) continue;
                int nType = grid[neighbour.x, neighbour.y].roomType;
                if(nType > 0) neighbourTypes.Add(nType);
            }

            if(neighbourTypes.Count > 0)
            {
                return neighbourTypes[Random.Range(0, neighbourTypes.Count)];
            }
        }
        //Fall back to fully random type//
        return Random.Range(1, roomTypeCount + 1);
    }

    //Detect and merge adjacent cells of the same type into rooms (max 2 x 2) - EM//
    //Two pass approach: claim all possible 2x2's first then fill remaining cells as 1x1's -EM//
    //This prevents 1x1's being consumed before a valid 2x2 can claim them -EM//
    private void MergeRooms()
    {
        bool[,] proccessed = new bool[gridSize, gridSize];

        //Pass 0: guarantee at least one 2x2 per room type//
        //Tracks which types have already been guaranteed a 2x2 this pass//
        HashSet<int> guaranteedTypes = new HashSet<int>();

        for (int type = 1; type <= roomTypeCount; type++)
        {
            //Scan the entire grid looking for the first valid 2x2 of this type//
            bool found = false;
            for (int x = 0; x < gridSize && !found; x++)
            {
                for (int y = 0; y < gridSize && !found; y++)
                {
                    if (!grid[x, y].isOccupied || proccessed[x, y]) continue;
                    if (grid[x, y].roomType != type) continue;
                    if (grid[x, y].isFinalRoom) continue; //Dont let pass 0 claim final room cells//

                    if (CanMerge(x, y, 2, 2, type, proccessed))
                    {
                        List<Vector2Int> cells = new List<Vector2Int>
                    {
                        new Vector2Int(x, y),
                        new Vector2Int(x + 1, y),
                        new Vector2Int(x, y + 1),
                        new Vector2Int(x + 1, y + 1)
                    };

                        foreach (Vector2Int cell in cells)
                            proccessed[cell.x, cell.y] = true;

                        rooms.Add(new Room(cells, type, false, false));
                        guaranteedTypes.Add(type);
                        found = true;

                        if (showDebugLogs)
                            Debug.Log($"[DungeonGenerator] Pass 0: Guaranteed 2x2 for type {type} at ({x},{y})");
                    }
                }
            }

            //Warn if a type had no valid 2x2 available on this seed//
            if (!found && showDebugLogs)
                Debug.LogWarning($"[DungeonGenerator] Pass 0: Could not guarantee a 2x2 for type {type} - not enough clustered cells. Consider raising typeClusterChance or gridSize.");
        }

        //Pass 0.5: claim the reserved final room as a single guaranteed 2x2//
        for (int x = 0; x < gridSize - 1; x++)
        {
            for (int y = 0; y < gridSize - 1; y++)
            {
                if (!grid[x, y].isFinalRoom || proccessed[x, y]) continue;
                if (!grid[x + 1, y].isFinalRoom || proccessed[x + 1, y]) continue;
                if (!grid[x, y + 1].isFinalRoom || proccessed[x, y + 1]) continue;
                if (!grid[x + 1, y + 1].isFinalRoom || proccessed[x + 1, y + 1]) continue;

                List<Vector2Int> cells = new List<Vector2Int>
            {
                new Vector2Int(x, y),
                new Vector2Int(x + 1, y),
                new Vector2Int(x, y + 1),
                new Vector2Int(x + 1, y + 1)
            };

                foreach (Vector2Int cell in cells)
                    proccessed[cell.x, cell.y] = true;

                rooms.Add(new Room(cells, grid[x, y].roomType, false, true)); 

                if (showDebugLogs) Debug.Log($"[DungeonGenerator] Pass 0.5: Claimed FinalRoom 2x2 at ({x},{y})");
                goto finalRoomClaimed; //Only one final room, exit both loops//
            }
        }
    finalRoomClaimed:;

        //Pass 1: greedily claim any remaining valid 2x2 merges//
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (!grid[x, y].isOccupied || proccessed[x, y]) continue;

                int roomType = grid[x, y].roomType;

                //Only try 2x2 - skip lobbies and final room cells, they stay 1x1//
                if (!grid[x, y].isLobby && !grid[x, y].isFinalRoom && CanMerge(x, y, 2, 2, roomType, proccessed))
                {
                    List<Vector2Int> cells = new List<Vector2Int>
                {
                    new Vector2Int(x, y),
                    new Vector2Int(x + 1, y),
                    new Vector2Int(x, y + 1),
                    new Vector2Int(x + 1, y + 1)
                };

                    foreach (Vector2Int cell in cells)
                    {
                        proccessed[cell.x, cell.y] = true;
                    }

                    rooms.Add(new Room(cells, roomType, false, false));
                }
            }
        }

        //Pass 2: any cell not yet claimed becomes a 1x1//
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (!grid[x, y].isOccupied || proccessed[x, y]) continue;

                proccessed[x, y] = true;
                rooms.Add(new Room(new List<Vector2Int> { new Vector2Int(x, y) }, grid[x, y].roomType, grid[x, y].isLobby, grid[x, y].isFinalRoom));
            }
        }

        if (showDebugLogs)
        {
            int count1x1 = 0;
            int count2x2 = 0;

            foreach (Room r in rooms)
            {
                if (r.gridCells.Count == 1) count1x1++;
                else if (r.gridCells.Count == 4) count2x2++;
            }
            Debug.Log($"[DungeonGenerator] Room merging complete: {rooms.Count} rooms ({count2x2} x 2x2, {count1x1} x 1x1) | Guaranteed types: {string.Join(", ", guaranteedTypes)}"); 
        }
    }

    //Find the best occupied 2x2 block far from the lobby and stamp is as the final room//
    //Called before MergeRooms so the forced same-type block is guaranteed to merge -EM//
    private void ReserveFinalRoomCells()
    {
        Vector2 lobbyCentre = new Vector2(startPosition.x, startPosition.y);

        Vector2Int bestOrigin = new Vector2Int(-1, -1);
        float bestDist = -1f;

        //Scan every possible 2x2 origin//
        for(int x = 0; x < gridSize - 1; x++)
        {
            for(int y = 0; y < gridSize - 1; y++)
            {
                //All four cells must be occupied and none can be the lobby//
                if (!grid[x,y].isOccupied || grid[x,y].isLobby) continue;
                if (!grid[x + 1, y].isOccupied || grid[x + 1, y].isLobby) continue;
                if (!grid[x,y+1].isOccupied || grid[x, y+1].isLobby) continue;
                if (!grid[x + 1, y + 1].isOccupied || grid[x + 1, y + 1].isLobby) continue;

                Vector2 centre = new Vector2(x + 0.5f, y + 0.5f);
                float dist = Vector2.Distance(centre, lobbyCentre);

                if (dist > bestDist)
                {
                    bestDist = dist;
                    bestOrigin = new Vector2Int(x, y);
                }
            }
        }

        if(bestOrigin.x == -1)
        {
            if (showDebugLogs) Debug.LogWarning("[DungeonGenerator] Could not find any valid 2x2 blocki for the final room - dungeon may be too sparse");
            return;
        }

        //Force all four cells to be the same type so CanMerge will always succeed//
        int sharedType = grid[bestOrigin.x, bestOrigin.y].roomType;
        if (sharedType == 0) sharedType = 1; //Avoid lobby type 0;//

        for(int dx = 0; dx < 2; dx++)
        {
            for(int dy = 0; dy < 2; dy++)
            {
                CellData cell = grid[bestOrigin.x + dx, bestOrigin.y + dy];
                cell.roomType = sharedType;
                cell.isFinalRoom = true;
            }
        }

        if (showDebugLogs) Debug.Log($"[DungeonGenerator] Final room reserved at grid ({bestOrigin.x}, {bestOrigin.y}), distance {bestDist:F1} from lobby");
    }

    //Check if we can merge a width x height block starting at (x,y) - EM//
    private bool CanMerge(int x, int y, int width, int height, int roomType, bool[,] processed)
    {
        //Check bounds//
        if (x < 0 || y < 0 || x + width > gridSize || y + height > gridSize) return false;

        //Check all cells in the rectangle//
        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                int checkX = x + dx;
                int checkY = y + dy;

                //Must be occupied, same type, and not already processed//
                if (!grid[checkX, checkY].isOccupied || grid[checkX, checkY].roomType != roomType || processed[checkX, checkY])
                {
                    return false;
                }

                //Protect final room//
                if (grid[checkX, checkY].isFinalRoom) return false;
            }
        }
        return true;
    }

    //Calculate which rooms connect to each other -EM//
    private void CalculateConnections()
    {
        foreach (Room room in rooms)
        {
            room.connections.Clear();

            //Check all cells in this room//
            foreach (Vector2Int cell in room.gridCells)
            {
                //Check Cardinal Neighbours//
                CheckAndAddConnections(room, cell, new Vector2Int(0, 1)); //North//
                CheckAndAddConnections(room, cell, new Vector2Int(1, 0)); //East//
                CheckAndAddConnections(room, cell, new Vector2Int(0, -1)); //South//
                CheckAndAddConnections(room, cell, new Vector2Int(-1, 0)); //West//
            }
        }
    }

    //Check if neighbour is a different room and add connections -EM//
    private void CheckAndAddConnections(Room room, Vector2Int cell, Vector2Int direction)
    {
        Vector2Int neighbour = cell + direction;

        //Check bounds//
        if (neighbour.x < 0 || neighbour.x >= gridSize || neighbour.y < 0 || neighbour.y >= gridSize) return;

        //Check if occupied//
        if (!grid[neighbour.x, neighbour.y].isOccupied) return;

        //Find which room this neighbour this belongs to//
        Room neighbourRoom = FindRoomContainingCell(neighbour);

        if (neighbourRoom != null && neighbourRoom != room && !room.connections.Contains(neighbourRoom))
        {
            room.connections.Add(neighbourRoom);
        }
    }

    //Find which room contains a specific grid cell - EM//
    private Room FindRoomContainingCell(Vector2Int cell)
    {
        foreach (Room room in rooms)
        {
            if (room.gridCells.Contains(cell))
                return room;
        }
        return null;
    }

    //Test generate with current seed value in inspector -EM//
    [ContextMenu("Generate With Current Seed")]
    private void TestGenerateWithSeed()
    {
        useRandomSeed = false;
        GenerateDungeon();

        //Also rebuild rooms if RoomBuilder is present//
        RoomBuilder rb = GetComponent<RoomBuilder>();
        if (rb != null) rb.BuildRooms();

        Debug.Log($"[DungeonGenerator] Generated with seed: {seed}");
    }

    //Test generate a fresh random dungeo0n and print the seed -EM//
    [ContextMenu("Generate Random and Print Seed")]
    private void TestGenerateRandom()
    {
        useRandomSeed = true;
        GenerateDungeon();

        RoomBuilder rb = GetComponent<RoomBuilder>();
        if(rb != null) rb.BuildRooms();

        Debug.Log($"[DungeonGenerator] Random seed was: {seed} - copy this int 'Seed' field to recreate");
    }

    //Get the generated rooms (for external use) -EM//
    public List<Room> GetRooms()
    {
        return rooms;
    }

    //Get the grid data (for debugging/ visualisation) - EM//
    public CellData[,] GetGrid()
    {
        return grid;
    }

    //Retursn the seed used in the last generation -EM//
    public int GetLastSeed()
    {
        return seed;
    }

    //Feed a known seed and regenerate -EM//
    public void GenerateWithSeed(int knownSeed)
    {
        useRandomSeed = false;
        seed = knownSeed;
        GenerateDungeon();
    }
}