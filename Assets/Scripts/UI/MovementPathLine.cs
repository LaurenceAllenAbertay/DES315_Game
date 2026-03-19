using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Draws a NavMesh-following path line from the player to the mouse cursor during the player's
/// combat turn. White along the reachable portion, red beyond the remaining move distance.
/// Shows a TextMeshProUGUI warning when the cursor is out of range.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MovementPathLine : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Camera mainCamera;

    [Header("Floor Raycast")]
    [Tooltip("Match this to PlayerController's walkableMask")]
    [SerializeField] private LayerMask walkableMask;

    [Header("Line Appearance")]
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private float lineHeightOffset = 0.02f;
    [Tooltip("Match this to PlayerController's minMoveDistance — line hides when remaining movement is below this")]
    [SerializeField] private float minMoveDistance = 0.67f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI outOfRangeText;
    [SerializeField] private string outOfRangeMessage = "Movement out of range.";

    private LineRenderer lineRenderer;
    private NavMeshPath navPath;

    private static readonly Color ColorInRange    = Color.white;
    private static readonly Color ColorOutOfRange = Color.red;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        navPath = new NavMeshPath();
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth    = lineWidth;
        lineRenderer.endWidth      = lineWidth;
        lineRenderer.positionCount = 0;
        lineRenderer.enabled       = false;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (player == null)
            player = FindFirstObjectByType<Player>();

        if (walkableMask.value == 0)
            walkableMask = ~0;
    }

    private void OnDisable()
    {
        HideLine();
        SetOutOfRangeText(false);
    }

    private void Update()
    {
        if (!ShouldShow())
        {
            HideLine();
            SetOutOfRangeText(false);
            return;
        }

        Vector3? mouseFloorPoint = GetMouseFloorPoint();
        if (mouseFloorPoint == null)
        {
            HideLine();
            SetOutOfRangeText(false);
            return;
        }

        DrawNavMeshLine(mouseFloorPoint.Value);
    }

    /// <summary>
    /// Calculates the NavMesh path to the mouse point and draws the line along the corners,
    /// splitting colour at the remaining-range boundary.
    /// </summary>
    private void DrawNavMeshLine(Vector3 mousePoint)
    {
        Vector3 origin = player.transform.position;

        NavMesh.CalculatePath(origin, mousePoint, NavMesh.AllAreas, navPath);

        if (navPath.status == NavMeshPathStatus.PathInvalid || navPath.corners.Length < 2)
        {
            HideLine();
            SetOutOfRangeText(false);
            return;
        }

        Vector3[] corners = navPath.corners;

        float totalPathLength = 0f;
        for (int i = 0; i < corners.Length - 1; i++)
            totalPathLength += Vector3.Distance(corners[i], corners[i + 1]);

        float remaining  = Mathf.Max(0f, player.RemainingMoveDistance);
        bool  outOfRange = totalPathLength > remaining;

        List<Vector3> points = new List<Vector3>(corners.Length);
        for (int i = 0; i < corners.Length; i++)
            points.Add(new Vector3(corners[i].x, corners[i].y + lineHeightOffset, corners[i].z));

        if (!outOfRange)
        {
            SetLinePoints(points, BuildGradient(ColorInRange, ColorInRange));
            SetOutOfRangeText(false);
        }
        else
        {
            List<Vector3> splitPoints = BuildSplitPoints(points, remaining, out float splitT);
            SetLinePoints(splitPoints, BuildSplitGradient(splitT, ColorInRange, ColorOutOfRange));
            SetOutOfRangeText(true);
        }
    }

    /// <summary>
    /// Walks the corner list and inserts an extra point exactly at the range boundary.
    /// Returns the normalised position of that split along the full line length (used for the gradient).
    /// </summary>
    private List<Vector3> BuildSplitPoints(List<Vector3> points, float remaining, out float splitT)
    {
        float totalLength = 0f;
        for (int i = 0; i < points.Count - 1; i++)
            totalLength += Vector3.Distance(points[i], points[i + 1]);

        List<Vector3> result = new List<Vector3>(points.Count + 1);
        splitT = 0f;
        float accumulated = 0f;
        bool  inserted    = false;

        for (int i = 0; i < points.Count - 1; i++)
        {
            result.Add(points[i]);

            if (!inserted)
            {
                float segLen = Vector3.Distance(points[i], points[i + 1]);
                float nextAcc = accumulated + segLen;

                if (accumulated < remaining && nextAcc >= remaining)
                {
                    float t       = segLen > 0f ? (remaining - accumulated) / segLen : 0f;
                    Vector3 split = Vector3.Lerp(points[i], points[i + 1], t);
                    result.Add(split);
                    splitT   = totalLength > 0f ? remaining / totalLength : 0.5f;
                    inserted = true;
                }

                accumulated = nextAcc;
            }
        }

        result.Add(points[points.Count - 1]);
        return result;
    }

    private void SetLinePoints(List<Vector3> points, Gradient gradient)
    {
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
        lineRenderer.colorGradient = gradient;
        lineRenderer.enabled       = true;
    }

    private void HideLine()
    {
        lineRenderer.enabled       = false;
        lineRenderer.positionCount = 0;
    }

    private void SetOutOfRangeText(bool visible)
    {
        if (outOfRangeText == null) return;
        outOfRangeText.text    = visible ? outOfRangeMessage : string.Empty;
        outOfRangeText.enabled = visible;
    }

    private Vector3? GetMouseFloorPoint()
    {
        if (mainCamera == null) return null;
        if (Mouse.current == null) return null;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, walkableMask))
            return null;

        RoomLA currentRoom = RoomManager.Instance?.CurrentRoom;
        if (currentRoom != null && !currentRoom.Contains(hit.point))
            return null;

        return hit.point;
    }

    private bool ShouldShow()
    {
        if (player == null || CombatManager.Instance == null) return false;
        if (!CombatManager.Instance.InCombat || !CombatManager.Instance.IsPlayerTurn) return false;
        if (!player.CanMove()) return false;
        return player.RemainingMoveDistance > minMoveDistance;
    }

    private static Gradient BuildGradient(Color start, Color end)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(1f,    0f), new GradientAlphaKey(1f,  1f) }
        );
        return g;
    }

    /// <summary>
    /// Hard colour split at splitT: white before, red after.
    /// </summary>
    private static Gradient BuildSplitGradient(float splitT, Color inRange, Color outOfRange)
    {
        const float epsilon = 0.0001f;
        float safeT = Mathf.Clamp(splitT, epsilon, 1f - epsilon);

        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(inRange,    0f),
                new GradientColorKey(inRange,    safeT),
                new GradientColorKey(outOfRange, safeT + epsilon),
                new GradientColorKey(outOfRange, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
        return g;
    }
}