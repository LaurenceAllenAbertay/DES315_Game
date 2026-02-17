using UnityEngine;


// Visualizes the generated dungeon using Gizmos for debugging -EM//

[RequireComponent(typeof(DungeonGenerator))]
public class DungeonVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    [Tooltip("Size of each grid cell in world units")]
    public float cellSize = 1.8f;

    [Header("Display Options")]
    public bool showGrid = true;
    public bool showRooms = true;
    public bool showConnections = true;
    public bool showRoomTypes = true;
    public bool showRoomBorders = true;

    [Header("Colors")]
    public Color gridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color emptyColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
    public Color lobbyColor = new Color(0f, 1f, 0f, 1f);
    public Color roomType1Color = new Color(1f, 0f, 0f, 1f);
    public Color roomType2Color = new Color(0f, 0f, 1f, 1f);
    public Color roomType3Color = new Color(1f, 1f, 0f, 1f);
    public Color roomType4Color = new Color(1f, 0f, 1f, 1f);
    public Color connectionColor = new Color(1f, 1f, 1f, 0.8f);
    public Color roomBorderColor = new Color(0f, 0f, 0f, 1f);

    [Header("Border Settings")]
    [Tooltip("Thickness of room borders")]
    public float borderThickness = 0.2f;
    [Tooltip("Height offset for borders")]
    public float borderHeight = 0.3f;

    private DungeonGenerator generator;

    private void Awake()
    {
        generator = GetComponent<DungeonGenerator>();
    }

    private void OnDrawGizmos()
    {
        if (generator == null) return;

        CellData[,] grid = generator.GetGrid();
        if (grid == null) return;

        int gridSize = grid.GetLength(0);

        // Draw grid lines //
        if (showGrid)
        {
            DrawGridLines(gridSize);
        }

        // Draw cells //
        if (showRooms)
        {
            DrawCells(grid, gridSize);
        }

        // Draw room borders//
        if(showRoomBorders)
        {
            DrawRoomBorders();
        }

        // Draw connections //
        if (showConnections)
        {
            DrawConnections();
        }

        // Draw room type labels //
        if (showRoomTypes)
        {
            DrawRoomLabels();
        }
    }

    // Draw grid lines -EM//
 
    private void DrawGridLines(int gridSize)
    {
        Gizmos.color = gridColor;

        float worldSize = gridSize * cellSize;
        Vector3 offset = GetGridOffset(gridSize);

        // Vertical lines
        for (int x = 0; x <= gridSize; x++)
        {
            Vector3 start = offset + new Vector3(x * cellSize, 0f, 0f);
            Vector3 end = offset + new Vector3(x * cellSize, 0f, worldSize);
            Gizmos.DrawLine(start, end);
        }

        // Horizontal lines
        for (int y = 0; y <= gridSize; y++)
        {
            Vector3 start = offset + new Vector3(0f, 0f, y * cellSize);
            Vector3 end = offset + new Vector3(worldSize, 0f, y * cellSize);
            Gizmos.DrawLine(start, end);
        }
    }


    // Draw cells with colors based on occupation and type -EM//
    private void DrawCells(CellData[,] grid, int gridSize)
    {
        Vector3 offset = GetGridOffset(gridSize);

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                Vector3 cellCenter = offset + new Vector3(
                    (x + 0.5f) * cellSize,
                    0f,
                    (y + 0.5f) * cellSize
                );

                if (!grid[x, y].isOccupied)
                {
                    // Empty cell //
                    Gizmos.color = emptyColor;
                    DrawCell(cellCenter, cellSize * 0.9f);
                }
                else
                {
                    // Occupied cell - color by type //
                    Gizmos.color = GetRoomTypeColor(grid[x, y]);
                    DrawCell(cellCenter, cellSize * 0.95f);
                }
            }
        }
    }


    // Draw a single cell as a cube - EM//
    private void DrawCell(Vector3 center, float size)
    {
        Gizmos.DrawCube(center, new Vector3(size, 0.1f, size));
    }


    // Get color for a room type -EM//
    private Color GetRoomTypeColor(CellData cell)
    {
        if (cell.isLobby)
            return lobbyColor;

        switch (cell.roomType)
        {
            case 1: return roomType1Color;
            case 2: return roomType2Color;
            case 3: return roomType3Color;
            case 4: return roomType4Color;
            default: return Color.white;
        }
    }

    //Draw borders around each room to show individual room boundaries - EM//
    private void DrawRoomBorders()
    {
        var rooms = generator.GetRooms();
        if (rooms == null || rooms.Count == 0) return;

        Gizmos.color = roomBorderColor;
        int gridSize = generator.gridSize;
        Vector3 offset = GetGridOffset(gridSize);

        foreach (Room room in rooms)
        {
            //Get room bounds//
            room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);

            //Draw border lines around room perimeter//
            DrawRoomPerimeter(room, offset, minX, minY, maxX, maxY);
        }
    }

    //Draw the perimeter of a room by checking each cell's edges - EM//
    private void DrawRoomPerimeter(Room room, Vector3 offset, int minX, int minY, int maxX, int maxY)
    {
        //For each cell in the room check if its edges are on the perimeter//
        foreach (Vector2Int cell in room.gridCells)
        {
            Vector3 cellWorldPos = offset + new Vector3(cell.x * cellSize, borderHeight, cell.y * cellSize);

            //Check each edge of this cell//
            //North Edge (top) //
            if (!room.gridCells.Contains(new Vector2Int(cell.x, cell.y + 1)))
            {
                Vector3 start = cellWorldPos + new Vector3(0f, 0f, cellSize);
                Vector3 end = cellWorldPos + new Vector3(cellSize, 0f, cellSize);
                DrawThickLine(start, end, borderThickness);
            }

            //East Edge (Right)//
            if (!room.gridCells.Contains(new Vector2Int(cell.x + 1, cell.y)))
            {
                Vector3 start = cellWorldPos + new Vector3(cellSize, 0f, 0f);
                Vector3 end = cellWorldPos + new Vector3(cellSize, 0f, cellSize);
                DrawThickLine(start, end, borderThickness);
            }

            //South Edge (Bottom)//
            if (!room.gridCells.Contains(new Vector2Int(cell.x, cell.y - 1)))
            {
                Vector3 start = cellWorldPos;
                Vector3 end = cellWorldPos + new Vector3(cellSize, 0f, 0f);
                DrawThickLine(start, end, borderThickness);
            }

            //West Edge (Left)//
            if (!room.gridCells.Contains(new Vector2Int(cell.x - 1, cell.y)))
            {
                Vector3 start = cellWorldPos;
                Vector3 end = cellWorldPos + new Vector3(0f, 0f, cellSize);
                DrawThickLine(start, end, borderThickness);
            }
        }
    }

    //Draw a thick line using multiple gizmos.Drawline calls - EM//
    private void DrawThickLine(Vector3 start, Vector3 end, float thickness)
    {
        //Draw main line//
        Gizmos.DrawLine(start, end);

        //Draw offset lines for thickness//
        Vector3 direction = (end - start).normalized;
        Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x) * thickness * 0.05f;

        Gizmos.DrawLine(start + perpendicular, end + perpendicular);
        Gizmos.DrawLine(start - perpendicular, end - perpendicular);
    }

    // Draw connections between rooms - EM//
    private void DrawConnections()
    {
        var rooms = generator.GetRooms();
        if (rooms == null || rooms.Count == 0) return;

        Gizmos.color = connectionColor;
        int gridSize = generator.gridSize;
        Vector3 offset = GetGridOffset(gridSize);

        foreach (Room room in rooms)
        {
            Vector3 roomCenter = GetRoomCenter(room, offset);

            foreach (Room connectedRoom in room.connections)
            {
                Vector3 connectedCenter = GetRoomCenter(connectedRoom, offset);

                // Draw line between room centers
                Gizmos.DrawLine(roomCenter + Vector3.up * 0.5f, connectedCenter + Vector3.up * 0.5f);
            }
        }
    }

    // Draw room type labels in the scene - EM//
    private void DrawRoomLabels()
    {
        var rooms = generator.GetRooms();
        if (rooms == null || rooms.Count == 0) return;

        int gridSize = generator.gridSize;
        Vector3 offset = GetGridOffset(gridSize);

        foreach (Room room in rooms)
        {
            Vector3 roomCenter = GetRoomCenter(room, offset);

            string label = room.isLobby ? "LOBBY" : $"Type {room.roomType}";

            // Draw label using GUI (only visible in Scene view)
#if UNITY_EDITOR
            UnityEditor.Handles.Label(roomCenter + Vector3.up, label);
#endif
        }
    }

    // Get the world position center of a room - EM//
    private Vector3 GetRoomCenter(Room room, Vector3 offset)
    {
        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);

        float centerX = (minX + maxX + 1f) * 0.5f * cellSize;
        float centerY = (minY + maxY + 1f) * 0.5f * cellSize;

        return offset + new Vector3(centerX, 0f, centerY);
    }


    // Calculate offset to center the grid at world origin - EM//
    private Vector3 GetGridOffset(int gridSize)
    {
        float worldSize = gridSize * cellSize;
        return new Vector3(-worldSize * 0.5f, 0f, -worldSize * 0.5f);
    }


    // Draw a preview of the grid bounds - EM//
    private void OnDrawGizmosSelected()
    {
        if (generator == null) return;

        int gridSize = generator.gridSize;
        float worldSize = gridSize * cellSize;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(worldSize, 0.5f, worldSize));
    }
}