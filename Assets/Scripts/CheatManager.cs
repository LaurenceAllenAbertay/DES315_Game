using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Manages cheat/debug toggles.
/// Attach this to a persistent GameObject in the scene.
/// The UI buttons in your cheat menu should call the public Toggle methods.
/// </summary>
public class CheatManager : MonoBehaviour
{
    public static CheatManager Instance { get; private set; }

    [Header("Cheat State")]
    [SerializeField] private bool infiniteHealth = false;
    [SerializeField] private bool infiniteCoins  = false;
    [SerializeField] private bool showFinalRoom = false;

    [Header("Final Room Waypoint")]
    [Tooltip("The marker prefab to spawn aboce the final room")]
    public GameObject finalRoomMarkerPrefab;

    [Tooltip("Height above the room centre the marker floats")]
    public float markerHeight = 3f;

    [Tooltip("Pulse speed for the default marker")]
    public float beaconPulseSpeed = 2f;

    // Events so the UI can update its toggle visuals without polling
    public event System.Action<bool> OnInfiniteHealthChanged;
    public event System.Action<bool> OnInfiniteCoinsChanged;
    public event System.Action<bool> OnShowFinalRoomChanged;

    // Properties
    public bool InfiniteHealth => infiniteHealth;
    public bool InfiniteCoins  => infiniteCoins;
    public bool ShowFinalRoom => showFinalRoom;

    private GameObject activeMarker;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>Toggle infinite health on/off.</summary>
    public void ToggleInfiniteHealth()
    {
        SetInfiniteHealth(!infiniteHealth);
    }

    /// <summary>Set infinite health explicitly.</summary>
    public void SetInfiniteHealth(bool value)
    {
        if (infiniteHealth == value) return;
        infiniteHealth = value;
        Debug.Log($"[CheatManager] Infinite Health: {infiniteHealth}");
        OnInfiniteHealthChanged?.Invoke(infiniteHealth);
    }

    /// <summary>Toggle infinite coins (action points) on/off.</summary>
    public void ToggleInfiniteCoins()
    {
        SetInfiniteCoins(!infiniteCoins);
    }

    /// <summary>Set infinite coins explicitly.</summary>
    public void SetInfiniteCoins(bool value)
    {
        if (infiniteCoins == value) return;
        infiniteCoins = value;
        Debug.Log($"[CheatManager] Infinite Coins: {infiniteCoins}");
        OnInfiniteCoinsChanged?.Invoke(infiniteCoins);
    }

    //Toggle the final room waypoint marker on/off -EM//
    public void SetShowFinalRoom(bool value)
    {
        if(showFinalRoom == value) return; 
        showFinalRoom = value;
        Debug.Log($"[CheatManager] Show final room: {showFinalRoom}");

        if (showFinalRoom) SpawnFinalRoomMarker();
        else RemoveFinalRoomMarker();

        OnShowFinalRoomChanged?.Invoke(showFinalRoom);
    }

    //Toggle the final room waypoint marker on and off//
    public void ToggleShowFinalRoom()
    {
        SetShowFinalRoom(!showFinalRoom);
    }
    private void SpawnFinalRoomMarker()
    {
        //Find the WinCondition Object - it lives in the final room//
        WinCondition winCondition = FindFirstObjectByType<WinCondition>();
        if(winCondition == null)
        {
            Debug.LogWarning("[CheatManager] No WinCondition found in scene, cannot show final room marker");
            return;
        }

        //Position the marker above the wind condition object//
        Vector3 markerPos = winCondition.transform.position + Vector3.up * markerHeight;

        if(finalRoomMarkerPrefab != null)
        {
            activeMarker = Instantiate(finalRoomMarkerPrefab, markerPos, Quaternion.identity);
        }
        else
        {
            //Build a simple pulsing beacon out of primitive shapes if no prefab is set//
            activeMarker = BuildDefaultBeacon(markerPos);
        }

        if(activeMarker != null)
        {
            activeMarker.name = "FinalRoomMarker";
            //Attach a pulse component so it animates//
            FinalRoomBeaconPulse pulse = activeMarker.AddComponent<FinalRoomBeaconPulse>();
            pulse.pulseSpeed = beaconPulseSpeed;
            pulse.targetTransform = winCondition.transform;
        }
    }

    private void RemoveFinalRoomMarker()
    {
        if(activeMarker != null)
        {
            Destroy(activeMarker);
            activeMarker = null;
        }
    }

    //Builds a simple vertical line beacon using LineRenderer - EM//
    private GameObject BuildDefaultBeacon(Vector3 position)
    {
        GameObject beacon = new GameObject("DefaultBeacon");
        beacon.transform.position = position;

        LineRenderer lr = beacon.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.up * 10f);
        lr.startWidth = 0.1f;
        lr.endWidth = 0.02f;
        lr.useWorldSpace = false;

        //Use the standard unit colour shader//
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.yellow;
        lr.endColor = new Color(1f, 0.8f, 0f, 0f); //Fade to transparent at top//

        //Add a sphere at the base for extra visibility//
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(beacon.transform);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * 0.4f;
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = Color.yellow;
        sphere.GetComponent<Renderer>().material = mat;

        return beacon;
    }
}

//Helper component - pulses the beacon and always faces / tracks the player//
public class FinalRoomBeaconPulse : MonoBehaviour
{
    public float pulseSpeed = 2f;
    public Transform targetTransform;

    private Vector3 baseLocalScale;
    private Vector3 basePosition;

    private void Start()
    {
        baseLocalScale = transform.localScale;
        basePosition = transform.position;
    }

    private void Update()
    {
        //Bob up and down//
        float bob = Mathf.Sin(Time.time * pulseSpeed) * 0.3f;
        transform.position = basePosition + Vector3.up * bob;

        //Pulse scale//
        float scale = 1f + Mathf.Sin(Time.time * pulseSpeed * 0.5f) * 0.15f;
        transform.localScale = baseLocalScale * scale;
    }
}