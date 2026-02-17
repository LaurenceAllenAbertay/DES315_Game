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
        InitialiseGrid();
        GrowDungeon();
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

        //Pass 1: find and claim all valid 2x2 merges//
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (!grid[x, y].isOccupied || proccessed[x, y]) continue;

                int roomType = grid[x, y].roomType;

                //Only try 2x2 - skip lobbies, the stay 1x1//
                if (!grid[x,y].isLobby && CanMerge(x,y,2,2,roomType, proccessed))
                {
                    List<Vector2Int> cells = new List<Vector2Int>
                    {
                        new Vector2Int(x,y),
                        new Vector2Int(x+1,y),
                        new Vector2Int(x,y+1),
                        new Vector2Int(x+1,y+1)
                    };

                    foreach(Vector2Int cell in cells)
                    {
                        proccessed[cell.x, cell.y] = true;
                    }

                    rooms.Add(new Room(cells, roomType, false));
                }
            }
        }

        //Pass 2: any cell not yet claimed becomes a 1x1//
        for(int x = 0; x < gridSize; x++)
        {
            for(int y = 0;y < gridSize; y++)
            {
                if (!grid[x,y].isOccupied || proccessed[x,y]) continue;

                proccessed[x, y] = true;
                rooms.Add(new Room(new List<Vector2Int> { new Vector2Int(x, y) }, grid[x, y].roomType, grid[x, y].isLobby));
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
        Debug.Log($"[DungeonGenerator ]Room merging complete: {rooms.Count} rooms ({count2x2} x 2x2, {count1x1} x 1x1)");
        }
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

    //Find whcih room contains a specific grid cell - EM//
    private Room FindRoomContainingCell(Vector2Int cell)
    {
        foreach (Room room in rooms)
        {
            if (room.gridCells.Contains(cell))
                return room;
        }
        return null;
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
}