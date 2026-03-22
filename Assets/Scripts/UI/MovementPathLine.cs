using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Draws a NavMesh-following path line from the player to the mouse cursor (or active destination
/// while moving). Uses two separate LineRenderers so the white/red split is a hard material colour
/// swap with no gradient blending — compatible with any shader. Only the tip fades to transparent.
/// Active during the player's combat turn, or outside combat when ShowVisibilityFeatures is held.
/// </summary>
public class MovementPathLine : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Floor Raycast")]
    [Tooltip("Match this to PlayerController's walkableMask")]
    [SerializeField] private LayerMask walkableMask;

    [Header("Line Appearance")]
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private float lineHeightOffset = 0.02f;
    [Tooltip("Match this to PlayerController's minMoveDistance")]
    [SerializeField] private float minMoveDistance = 0.3f;
    [Tooltip("World-unit pull-back distance on each segment before rounding a corner")]
    [SerializeField] private float cornerRadius = 0.4f;
    [Tooltip("Bezier steps per rounded corner")]
    [SerializeField] [Range(2, 20)] private int cornerSteps = 8;
    [Tooltip("World-space distance over which the line tip fades to transparent")]
    [SerializeField] private float tipFadeDistance = 0.3f;
    [Tooltip("Material used for both line segments — any transparent unlit material works")]
    [SerializeField] private Material lineMaterial;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI outOfRangeText;
    [SerializeField] private string outOfRangeMessage = "Movement out of range.";

    private LineRenderer whiteRenderer;
    private LineRenderer redRenderer;
    private NavMeshPath navPath;
    private InputAction showVisibilityAction;

    private void Awake()
    {
        navPath = new NavMeshPath();

        if (mainCamera == null) mainCamera = Camera.main;
        if (player == null)    player    = FindFirstObjectByType<Player>();
        if (agent == null)     agent     = player != null ? player.GetComponent<NavMeshAgent>() : null;

        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Player");
            showVisibilityAction = map?.FindAction("ShowVisibilityFeatures");
        }

        if (walkableMask.value == 0)
            walkableMask = ~0;

        whiteRenderer = GetOrCreateRenderer("WhiteLine", Color.white);
        redRenderer   = GetOrCreateRenderer("RedLine",   Color.red);
    }

    private LineRenderer GetOrCreateRenderer(string childName, Color colour)
    {
        Transform existing = transform.Find(childName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.GetComponent<LineRenderer>();
        if (lr == null) lr = go.AddComponent<LineRenderer>();

        lr.useWorldSpace   = true;
        lr.startWidth      = lineWidth;
        lr.endWidth        = lineWidth;
        lr.positionCount   = 0;
        lr.enabled         = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows  = false;

        // Instance the material and set the colour directly — works with any shader
        if (lineMaterial != null)
        {
            lr.material         = new Material(lineMaterial);
            float originalAlpha = lineMaterial.color.a;
            lr.material.color   = new Color(colour.r, colour.g, colour.b, originalAlpha);
        }
        else
        {
            lr.material       = new Material(Shader.Find("Sprites/Default"));
            lr.material.color = colour;
        }

        return lr;
    }

    private void OnEnable()
    {
        showVisibilityAction?.Enable();
    }

    private void OnDisable()
    {
        showVisibilityAction?.Disable();
        HideAll();
    }

    private void Update()
    {
        if (!ShouldShow())
        {
            HideAll();
            SetOutOfRangeText(false);
            return;
        }

        Vector3? target = GetTarget();
        if (target == null)
        {
            HideAll();
            SetOutOfRangeText(false);
            return;
        }

        DrawNavMeshLine(target.Value);
    }

    /// <summary>
    /// Returns the destination to draw toward: the agent's active destination while moving,
    /// otherwise the mouse floor point.
    /// </summary>
    private Vector3? GetTarget()
    {
        if (agent != null && agent.hasPath && !agent.isStopped)
            return agent.destination;

        return GetMouseFloorPoint();
    }

    /// <summary>
    /// Calculates the NavMesh path to the target, smooths corners, then populates the two
    /// LineRenderers: white up to remaining range, red beyond.
    /// </summary>
    private void DrawNavMeshLine(Vector3 target)
    {
        Vector3 origin = player.transform.position;

        NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, navPath);

        if (navPath.status == NavMeshPathStatus.PathInvalid || navPath.corners.Length < 2)
        {
            HideAll();
            SetOutOfRangeText(false);
            return;
        }

        Vector3[] corners = navPath.corners;
        List<Vector3> elevated = new List<Vector3>(corners.Length);
        for (int i = 0; i < corners.Length; i++)
            elevated.Add(new Vector3(corners[i].x, corners[i].y + lineHeightOffset, corners[i].z));

        List<Vector3> smooth = BuildRoundedCornerPath(elevated);

        float totalLength = 0f;
        for (int i = 0; i < smooth.Count - 1; i++)
            totalLength += Vector3.Distance(smooth[i], smooth[i + 1]);

        bool inCombat = CombatManager.Instance != null && CombatManager.Instance.InCombat;
        float remaining = inCombat ? Mathf.Max(0f, player.RemainingMoveDistance) : float.MaxValue;
        bool outOfRange = totalLength > remaining;

        if (!outOfRange)
        {
            SetRenderer(whiteRenderer, smooth, tipFadeDistance);
            HideRenderer(redRenderer);
            SetOutOfRangeText(false);
        }
        else
        {
            SplitPath(smooth, remaining, out List<Vector3> whitePart, out List<Vector3> redPart);
            SetRenderer(whiteRenderer, whitePart, 0f);
            SetRenderer(redRenderer,   redPart,   tipFadeDistance);
            SetOutOfRangeText(true);
        }
    }

    /// <summary>
    /// Splits the smoothed point list at the remaining-distance boundary into two separate lists.
    /// </summary>
    private static void SplitPath(List<Vector3> points, float remaining,
        out List<Vector3> before, out List<Vector3> after)
    {
        before = new List<Vector3>();
        after  = new List<Vector3>();

        float accumulated = 0f;
        bool  split       = false;

        for (int i = 0; i < points.Count - 1; i++)
        {
            float segLen  = Vector3.Distance(points[i], points[i + 1]);
            float nextAcc = accumulated + segLen;

            if (!split)
            {
                before.Add(points[i]);

                if (nextAcc >= remaining)
                {
                    float t          = segLen > 0f ? (remaining - accumulated) / segLen : 0f;
                    Vector3 splitPt  = Vector3.Lerp(points[i], points[i + 1], t);
                    before.Add(splitPt);
                    after.Add(splitPt);
                    split = true;
                }
            }
            else
            {
                after.Add(points[i]);
            }

            accumulated = nextAcc;
        }

        if (!split)
            before.Add(points[points.Count - 1]);
        else
            after.Add(points[points.Count - 1]);
    }

    /// <summary>
    /// Assigns points to a LineRenderer and builds an alpha-only gradient that fades the tip to
    /// transparent over tipFade world units. Pass tipFade=0 for a fully opaque line.
    /// </summary>
    private static void SetRenderer(LineRenderer lr, List<Vector3> points, float tipFade)
    {
        if (points == null || points.Count < 2)
        {
            HideRenderer(lr);
            return;
        }

        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
        lr.colorGradient = BuildAlphaFadeGradient(points, tipFade);
        lr.enabled       = true;
    }

    private static void HideRenderer(LineRenderer lr)
    {
        lr.enabled       = false;
        lr.positionCount = 0;
    }

    /// <summary>
    /// Gradient that is fully opaque, fading to transparent only at the tip over tipFade world units.
    /// Colour is driven entirely by the material, not the gradient.
    /// </summary>
    private static Gradient BuildAlphaFadeGradient(List<Vector3> points, float tipFade)
    {
        var g = new Gradient();

        if (tipFade <= 0f || points.Count < 2)
        {
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }

        // Walk backwards by world distance to find the index-fraction for fade start
        float accumulated = 0f;
        float fadeStartT  = 0f;
        for (int i = points.Count - 1; i > 0; i--)
        {
            accumulated += Vector3.Distance(points[i], points[i - 1]);
            if (accumulated >= tipFade)
            {
                fadeStartT = (i - 1) / (float)(points.Count - 1);
                break;
            }
        }

        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, fadeStartT),
                new GradientAlphaKey(0f, 1f),
            }
        );
        return g;
    }

    private void HideAll()
    {
        if (whiteRenderer != null) HideRenderer(whiteRenderer);
        if (redRenderer   != null) HideRenderer(redRenderer);
    }

    private void SetOutOfRangeText(bool visible)
    {
        if (outOfRangeText == null) return;
        outOfRangeText.text    = visible ? outOfRangeMessage : string.Empty;
        outOfRangeText.enabled = visible;
    }

    private Vector3? GetMouseFloorPoint()
    {
        if (mainCamera == null || Mouse.current == null) return null;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, walkableMask))
            return null;

        RoomLA currentRoom = RoomManager.Instance?.CurrentRoom;
        if (currentRoom != null && !currentRoom.Contains(hit.point))
            return null;

        return hit.point;
    }

    private bool ShouldShow()
    {
        if (player == null) return false;

        if (CombatManager.Instance != null && CombatManager.Instance.InCombat)
        {
            if (!CombatManager.Instance.IsPlayerTurn) return false;
            if (!player.CanMove()) return false;
            return player.RemainingMoveDistance > minMoveDistance;
        }

        return showVisibilityAction != null && showVisibilityAction.IsPressed();
    }

    /// <summary>
    /// Rounds each NavMesh corner with a quadratic bezier. Pulls back cornerRadius along each
    /// segment then curves through the corner as the control handle — no looping possible.
    /// </summary>
    private List<Vector3> BuildRoundedCornerPath(List<Vector3> controls)
    {
        List<Vector3> result = new List<Vector3>();

        if (controls.Count < 2) return new List<Vector3>(controls);
        if (controls.Count == 2) { result.AddRange(controls); return result; }

        result.Add(controls[0]);

        for (int i = 1; i < controls.Count - 1; i++)
        {
            Vector3 prev = controls[i - 1];
            Vector3 curr = controls[i];
            Vector3 next = controls[i + 1];

            Vector3 inDir  = (curr - prev).normalized;
            Vector3 outDir = (next - curr).normalized;

            float r  = Mathf.Min(cornerRadius, Vector3.Distance(prev, curr) * 0.5f,
                                               Vector3.Distance(curr, next) * 0.5f);
            Vector3 p0 = curr - inDir  * r;
            Vector3 p2 = curr + outDir * r;

            result.Add(p0);

            for (int step = 1; step <= cornerSteps; step++)
            {
                float t  = step / (float)cornerSteps;
                float it = 1f - t;
                result.Add(it * it * p0 + 2f * it * t * curr + t * t * p2);
            }
        }

        result.Add(controls[controls.Count - 1]);
        return result;
    }

    private void OnDestroy()
    {
        if (whiteRenderer != null) Destroy(whiteRenderer.material);
        if (redRenderer   != null) Destroy(redRenderer.material);
    }
}