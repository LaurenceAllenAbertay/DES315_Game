using System.Collections.Generic;
using UnityEngine;

//A room made up of one or more merged grid cells - EM//
public class Room
{
    public List<Vector2Int> gridCells;
    public int roomType;
    public bool isLobby;
    public List<Room> connections;

    public Room(List<Vector2Int> cells, int type, bool lobby)
    {
        gridCells = cells;
        roomType = type;
        isLobby = lobby;
        connections = new List<Room>();
    }

    //Get the bounding box of this room in grid coordinates - EM//
    public void GetBounds(out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = int.MaxValue;
        minY = int.MaxValue;
        maxX = int.MinValue;
        maxY = int.MinValue;

        foreach (Vector2Int cell in gridCells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxY = Mathf.Max(maxY, cell.y);
        }
    }
}
