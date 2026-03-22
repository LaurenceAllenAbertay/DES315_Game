using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton that owns the minimap's composite Texture2D.
/// </summary>
public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance { get; private set; }

    [Header("Scan Settings")]
    [Tooltip("Layer mask matching your occluder layer(s) — same mask used by HardShadowFeature.")]
    [SerializeField] private LayerMask occluderLayer;
    [SerializeField] private Color floorColor    = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color occluderColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [Tooltip("How many scan-texture pixels map to one world unit. 3 is a good low-res default.")]
    [SerializeField] private float scanPixelsPerUnit = 3f;
    [Tooltip("Distance above the room's highest collider point to start each downward ray.")]
    [SerializeField] private float scanHeightOffset  = 3f;

    [Header("Composite Settings")]
    [Tooltip("Width and height of the composite RenderTexture in pixels.")]
    [SerializeField] private int   compositeSize     = 256;
    [Tooltip("How many world units are visible across the full width of the composite.")]
    [SerializeField] private float worldUnitsVisible = 40f;
    [Tooltip("How many world units to step per zoom button press.")]
    [SerializeField] private float zoomStep          = 10f;
    [Tooltip("Minimum world units visible (most zoomed in).")]
    [SerializeField] private float minWorldUnits     = 15f;
    [Tooltip("Maximum world units visible (most zoomed out).")]
    [SerializeField] private float maxWorldUnits     = 100f;

    [Header("Room Tinting")]
    [Tooltip("Color multiplier applied to discovered-but-not-current rooms.")]
    [SerializeField] private Color nonCurrentRoomTint = new Color(0.4f, 0.4f, 0.4f, 1f);

    private readonly HashSet<RoomLA> visitedRooms = new HashSet<RoomLA>();
    private Texture2D compositeTexture;
    private Color[]   compositeBuffer;

    public Texture2D CompositeTexture => compositeTexture;
    public float     WorldUnitsVisible => worldUnitsVisible;
    public Vector3   CurrentRoomCenter { get; private set; }

    public IReadOnlyCollection<RoomLA> VisitedRooms => visitedRooms;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        compositeTexture = new Texture2D(compositeSize, compositeSize, TextureFormat.RGBA32, false);
        compositeTexture.filterMode = FilterMode.Point;
        compositeTexture.wrapMode   = TextureWrapMode.Clamp;
        compositeBuffer = new Color[compositeSize * compositeSize];
    }

    private void Start()
    {
        if (RoomManager.Instance == null) return;

        RoomManager.Instance.RoomChanged += OnRoomChanged;

        RoomLA startRoom = RoomManager.Instance.CurrentRoom;
        if (startRoom != null)
            VisitAndComposite(startRoom);
    }

    private void OnDestroy()
    {
        if (RoomManager.Instance != null)
            RoomManager.Instance.RoomChanged -= OnRoomChanged;

        if (compositeTexture != null)
            Destroy(compositeTexture);
    }

    private void OnRoomChanged(RoomLA previous, RoomLA current)
    {
        if (current != null)
            VisitAndComposite(current);
    }

    public bool IsVisited(RoomLA room) => room != null && visitedRooms.Contains(room);

    public void ZoomIn()
    {
        worldUnitsVisible = Mathf.Max(minWorldUnits, worldUnitsVisible - zoomStep);
        RebuildComposite();
    }

    public void ZoomOut()
    {
        worldUnitsVisible = Mathf.Min(maxWorldUnits, worldUnitsVisible + zoomStep);
        RebuildComposite();
    }


    public RoomLA GetRoomForPosition(Vector3 worldPos)
    {
        foreach (RoomLA room in visitedRooms)
        {
            if (room != null && room.Contains(worldPos))
                return room;
        }
        return null;
    }

    private void VisitAndComposite(RoomLA room)
    {
        visitedRooms.Add(room);

        MinimapRoomData data = room.GetComponent<MinimapRoomData>();
        if (data == null)
            data = room.gameObject.AddComponent<MinimapRoomData>();

        if (!data.HasBeenScanned)
            data.ScanRoom(occluderLayer, floorColor, occluderColor, scanPixelsPerUnit, scanHeightOffset);

        RebuildComposite();
    }

    private void RebuildComposite()
    {
        RoomLA current = RoomManager.Instance?.CurrentRoom;
        if (current == null) return;

        MinimapRoomData currentData = current.GetComponent<MinimapRoomData>();
        if (currentData == null) return;

        CurrentRoomCenter = currentData.WorldBounds.center;

        float pixelsPerUnit = compositeSize / worldUnitsVisible;
        int   half          = compositeSize / 2;

        for (int i = 0; i < compositeBuffer.Length; i++)
            compositeBuffer[i] = Color.clear;

        foreach (RoomLA room in visitedRooms)
        {
            if (room == null) continue;
            MinimapRoomData data = room.GetComponent<MinimapRoomData>();
            if (data == null || !data.HasBeenScanned) continue;
            BlitRoom(data, room == current, pixelsPerUnit, half);
        }

        compositeTexture.SetPixels(compositeBuffer);
        compositeTexture.Apply();
    }

    private void BlitRoom(MinimapRoomData data, bool isCurrent, float pixelsPerUnit, int half)
    {
        Bounds  b      = data.WorldBounds;
        int     texW   = data.ScannedTexture.width;
        int     texH   = data.ScannedTexture.height;
        Color[] src    = data.PixelCache;

        int cxMin = Mathf.RoundToInt((b.min.x - CurrentRoomCenter.x) * pixelsPerUnit) + half;
        int cxMax = Mathf.RoundToInt((b.max.x - CurrentRoomCenter.x) * pixelsPerUnit) + half;
        int czMin = Mathf.RoundToInt((b.min.z - CurrentRoomCenter.z) * pixelsPerUnit) + half;
        int czMax = Mathf.RoundToInt((b.max.z - CurrentRoomCenter.z) * pixelsPerUnit) + half;

        int cxRange = cxMax - cxMin;
        int czRange = czMax - czMin;
        if (cxRange <= 0 || czRange <= 0) return;

        for (int cx = cxMin; cx < cxMax; cx++)
        {
            if (cx < 0 || cx >= compositeSize) continue;
            for (int cz = czMin; cz < czMax; cz++)
            {
                if (cz < 0 || cz >= compositeSize) continue;

                float u  = (float)(cx - cxMin) / cxRange;
                float v  = (float)(cz - czMin) / czRange;
                int   rx = Mathf.Clamp(Mathf.RoundToInt(u * (texW - 1)), 0, texW - 1);
                int   rz = Mathf.Clamp(Mathf.RoundToInt(v * (texH - 1)), 0, texH - 1);

                Color c = src[rz * texW + rx];
                if (c.a < 0.01f) continue;

                if (!isCurrent)
                    c *= nonCurrentRoomTint;

                compositeBuffer[cz * compositeSize + cx] = c;
            }
        }
    }
}