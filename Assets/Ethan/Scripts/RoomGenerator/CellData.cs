using UnityEngine;
//Data for a single grid cell - EM//
[System.Serializable]
public class CellData
{
    public bool isOccupied = false;
    public int roomType = 0; //0 = Lobby, 1-4 = placeholder//
    public bool isLobby = false;
}