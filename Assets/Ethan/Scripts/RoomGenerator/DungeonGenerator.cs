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
            Debug.Log($"Dungeon generated: {rooms.Count} rooms created");
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
            grid[cell.x, cell.y].roomType = Random.Range(1, roomTypeCount + 1);
            currentCells++;

            //Add this cell's neighbour to frontier//
            AddNeighboursToFrontier(cell, frontier);
        }

        if (showDebugLogs)
        {
            Debug.Log($"Grid growth complete: {currentCells}/{targetCells} cells occupied (iterations: {safetyCounter})");
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

    //Detect and merge adjacent cells of the same type into rooms (max 2 x 2) - EM//
    private void MergeRooms()
    {
        bool[,] proccessed = new bool[gridSize, gridSize];

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (!grid[x, y].isOccupied || proccessed[x, y]) continue;

                //Try to create largest possible room starting from this cell//
                Room room = CreateRoomFromCell(x, y, proccessed);
                rooms.Add(room);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"Room merging complete: {rooms.Count} rooms from grid cells");
        }
    }

    //Create a room by merging cells starting from position (x,y), Tries to create 2x2, then 2x1/1x2, then 1x1 - EM//
    private Room CreateRoomFromCell(int x, int y, bool[,] processed)
    {
        int roomType = grid[x, y].roomType;
        bool isLobby = grid[x, y].isLobby;
        List<Vector2Int> cells = new List<Vector2Int>();

        //Try to create 2x2//
        if (CanMerge(x, y, 2, 2, roomType, processed))
        {
            cells.Add(new Vector2Int(x, y));
            cells.Add(new Vector2Int(x + 1, y));
            cells.Add(new Vector2Int(x, y + 1));
            cells.Add(new Vector2Int(x + 1, y + 1));
        }

        //Try to create a 2x1 (horizontal)//
        else if (CanMerge(x, y, 2, 1, roomType, processed))
        {
            cells.Add(new Vector2Int(x, y));
            cells.Add(new Vector2Int(x + 1, y));
        }

        //Try to create a 1x2 (vertical)//
        else if (CanMerge(x, y, 1, 2, roomType, processed))
        {
            cells.Add(new Vector2Int(x, y));
            cells.Add(new Vector2Int(x, y + 1));
        }
        //Juse 1x1//
        else
        {
            cells.Add(new Vector2Int(x, y));
        }

        //Mark all as processed//
        foreach (Vector2Int cell in cells)
        {
            processed[cell.x, cell.y] = true;
        }

        return new Room(cells, roomType, isLobby);
    }

    //Check if we can merge a width x height block starting at (x,y) - EM//
    private bool CanMerge(int x, int y, int width, int height, int roomType, bool[,] processed)
    {
        //Check bounds//
        if (x + width > gridSize || y + height > gridSize) return false;

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

    //Check if neighbour is a s different room and add connections -EM//
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