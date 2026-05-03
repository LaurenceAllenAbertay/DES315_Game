using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Displays the minimap composite texture inside a circular masked panel and manages
/// player/enemy arrow markers that rotate to match their world-space facing direction.
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform displayRoot;
    [SerializeField] private RawImage      minimapImage;
    [SerializeField] private RectTransform markersContainer;
    [Tooltip("Assign the Main Camera transform here.")]
    [SerializeField] private Transform     cameraTransform;
    [SerializeField] private Button        zoomInButton;
    [SerializeField] private Button        zoomOutButton;

    [Header("Markers")]
    [SerializeField] private Sprite playerMarkerSprite;
    [SerializeField] private Sprite enemyMarkerSprite;
    [SerializeField] private float  markerSize  = 10f;
    [SerializeField] private Color  playerColor = new Color(0.25f, 0.45f, 1f);
    [SerializeField] private Color  enemyColor  = new Color(1f, 0.2f, 0.2f);

    private Player        playerRef;
    private RectTransform playerMarkerRect;
    private float         cameraYaw;

    private readonly List<Enemy>         trackedEnemies   = new List<Enemy>();
    private readonly List<RectTransform> enemyMarkerRects = new List<RectTransform>();

    private void Start()
    {
        minimapImage.texture = MinimapManager.Instance.CompositeTexture;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        playerRef = FindFirstObjectByType<Player>();
        CreateMarker(playerMarkerSprite, playerColor, out playerMarkerRect);

        if (zoomInButton  != null) zoomInButton.onClick.AddListener(MinimapManager.Instance.ZoomIn);
        if (zoomOutButton != null) zoomOutButton.onClick.AddListener(MinimapManager.Instance.ZoomOut);
    }

    private void Update()
    {
        cameraYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;

        displayRoot.localRotation = Quaternion.Euler(0f, 0f, cameraYaw);

        SyncEnemyList();
        PruneDeadEnemies();

        if (playerRef != null)
            PlaceMarker(playerMarkerRect, playerRef.transform.position, playerRef.transform.eulerAngles.y);

        UpdateEnemyMarkers();
    }

    private void SyncEnemyList()
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Enemy e in all)
        {
            if (!trackedEnemies.Contains(e))
                RegisterEnemy(e);
        }
    }

    private void RegisterEnemy(Enemy enemy)
    {
        CreateMarker(enemyMarkerSprite, enemyColor, out RectTransform rt);
        trackedEnemies.Add(enemy);
        enemyMarkerRects.Add(rt);
    }

    private void CreateMarker(Sprite sprite, Color color, out RectTransform rt)
    {
        GameObject go = new GameObject("MinimapMarker", typeof(Image));
        go.transform.SetParent(markersContainer, false);

        Image img = go.GetComponent<Image>();
        img.sprite        = sprite;
        img.color         = color;
        img.raycastTarget = false;

        rt           = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(markerSize, markerSize);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
    }

    private void PruneDeadEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            if (trackedEnemies[i] != null) continue;
            Destroy(enemyMarkerRects[i].gameObject);
            trackedEnemies.RemoveAt(i);
            enemyMarkerRects.RemoveAt(i);
        }
    }

    private void UpdateEnemyMarkers()
    {
        for (int i = 0; i < trackedEnemies.Count; i++)
        {
            Enemy enemy = trackedEnemies[i];
            if (enemy == null) continue;

            RoomLA room       = MinimapManager.Instance.GetRoomForPosition(enemy.transform.position);
            bool   showMarker = room != null && MinimapManager.Instance.IsVisited(room);
            enemyMarkerRects[i].gameObject.SetActive(showMarker);

            if (!showMarker) continue;
            PlaceMarker(enemyMarkerRects[i], enemy.transform.position, enemy.transform.eulerAngles.y);
        }
    }
    
    private void PlaceMarker(RectTransform marker, Vector3 worldPos, float worldYRotation)
    {
        MinimapManager mgr = MinimapManager.Instance;

        float nx = (worldPos.x - mgr.CurrentRoomCenter.x) / mgr.WorldUnitsVisible + 0.5f;
        float nz = (worldPos.z - mgr.CurrentRoomCenter.z) / mgr.WorldUnitsVisible + 0.5f;

        Rect r = markersContainer.rect;
        marker.anchoredPosition = new Vector2((nx - 0.5f) * r.width, (nz - 0.5f) * r.height);
        marker.localRotation    = Quaternion.Euler(0f, 0f, -worldYRotation);
    }
}