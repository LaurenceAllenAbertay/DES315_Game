using UnityEngine;

/// <summary>
/// Tracks per-run statistics and calculates the final score when the run ends
/// </summary>
public class RunScoreManager : MonoBehaviour
{
    public static RunScoreManager Instance { get; private set; }

    [Header("Score Values")]
    public int pointsPerKill = 100;
    public int penaltyPerDamagePoint = 5;
    public int pointsPerRoom = 10;
    public int pointsPerChest = 250;
    public int penaltyPerSecond = 1;

    public int EnemiesKilled { get; private set; }
    public float DamageTaken { get; private set; }
    public int RoomsExplored { get; private set; }
    public int ChestsOpened { get; private set; }
    public float TimeTaken { get; private set; }

    private float startTime;
    private readonly System.Collections.Generic.HashSet<RoomLA> visitedRooms = new System.Collections.Generic.HashSet<RoomLA>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        startTime = Time.unscaledTime;

        if (RoomManager.Instance != null)
            RoomManager.Instance.RoomChanged += OnRoomChanged;
    }

    private void OnDestroy()
    {
        if (RoomManager.Instance != null)
            RoomManager.Instance.RoomChanged -= OnRoomChanged;
    }

    private void OnRoomChanged(RoomLA previous, RoomLA current)
    {
        if (current != null && visitedRooms.Add(current))
            RoomsExplored++;
    }

    public void RegisterEnemyKilled() => EnemiesKilled++;

    public void RegisterDamageTaken(float amount)
    {
        if (amount > 0f)
            DamageTaken += amount;
    }

    public void RegisterChestOpened() => ChestsOpened++;

    public void FinalizeTime()
    {
        TimeTaken = Time.unscaledTime - startTime;
    }

    public int CalculateScore()
    {
        int score = 1000;
        score += EnemiesKilled * pointsPerKill;
        score -= Mathf.RoundToInt(DamageTaken) * penaltyPerDamagePoint;
        score += RoomsExplored * pointsPerRoom;
        score += ChestsOpened * pointsPerChest;
        score -= Mathf.RoundToInt(TimeTaken) * penaltyPerSecond;
        return score;
    }
}