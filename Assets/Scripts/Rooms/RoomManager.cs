using UnityEngine;
using System;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Room Tracking")]
    [SerializeField] private RoomLA currentRoom;
    public RoomLA CurrentRoom => currentRoom;
    [SerializeField] private Transform player;

    private readonly List<RoomLA> rooms = new List<RoomLA>();

    public event Action<RoomLA, RoomLA> RoomChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        player = GameObject.FindFirstObjectByType<Player>().transform;
    }

    private void Start()
    {
        RefreshRooms();
    }

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

    public void Register(RoomLA room)
    {
        if (room != null && !rooms.Contains(room))
            rooms.Add(room);
    }

    public void Unregister(RoomLA room)
    {
        rooms.Remove(room);
    }

    public void RefreshRooms()
    {
        rooms.Clear();
        rooms.AddRange(FindObjectsByType<RoomLA>(FindObjectsSortMode.None));
    }

    private RoomLA FindRoomForPosition(Vector3 position)
    {
        foreach (var room in rooms)
        {
            if (room == null) continue;
            if (room.Contains(position)) return room;
        }
        return null;
    }
}