using UnityEngine;
using System;

public class RoomManager : MonoBehaviour
{
    [Header("Room Tracking")]
    [SerializeField] private RoomLA currentRoom;
    public RoomLA CurrentRoom => currentRoom;
    [SerializeField] private Transform player;
    [SerializeField] private RoomLA[] rooms;

    public event Action<RoomLA, RoomLA> RoomChanged;

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        RoomLA newRoom = FindRoomForPosition(player.position);
        if (newRoom == CurrentRoom)
        {
            return;
        }

        RoomLA previousRoom = CurrentRoom;
        currentRoom = newRoom;
        RoomChanged?.Invoke(previousRoom, CurrentRoom);

        if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
        {
            CombatManager.Instance.ForceEndCombat(CombatManager.CombatOutcome.Draw);
        }
    }

    public void RefreshRooms()
    {
        rooms = FindObjectsByType<RoomLA>(FindObjectsSortMode.None);
    }

    private RoomLA FindRoomForPosition(Vector3 position)
    {
        if (rooms == null)
        {
            return null;
        }

        foreach (var room in rooms)
        {
            if (room == null)
            {
                continue;
            }

            if (room.Contains(position))
            {
                return room;
            }
        }

        return null;
    }

}
