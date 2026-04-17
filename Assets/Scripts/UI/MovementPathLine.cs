using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Draws a NavMesh-following path line from the player to the mouse cursor (or active destination
/// while moving). Uses two separate LineRenderers so the white/red split is a hard colour swap.
/// Active during the player's combat turn, or outside combat when ShowVisibilityFeatures is held.
/// Hidden entirely while an ability is being targeted.
/// </summary>
public class MovementPathLine : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private AbilityTargeting abilityTargeting;
    [SerializeField] private LineRenderer whiteRenderer;
    [SerializeField] private LineRenderer redRenderer;

    [Header("Floor Raycast")]
    [Tooltip("Match this to PlayerController's walkableMask")]
    [SerializeField] private LayerMask walkableMask;
    [Tooltip("Match this to PlayerController's navMeshSampleDistance")]
    [SerializeField] private float navMeshSampleDistance = 0.3f;

    [Header("Line Appearance")]
    [SerializeField] private float lineHeightOffset = 0.02f;
    [Tooltip("Match this to PlayerController's minMoveDistance")]
    [SerializeField] private float minMoveDistance = 0.3f;
    [Tooltip("World-unit pull-back distance on each segment before rounding a corner")]
    [SerializeField] private float cornerRadius = 0.4f;
    [Tooltip("Bezier steps per rounded corner")]
    [SerializeField] [Range(2, 20)] private int cornerSteps = 8;
    [Tooltip("Turns smaller than this (degrees) are treated as straight to avoid false curves")]
    [SerializeField] [Range(0f, 45f)] private float minCornerAngleForRounding = 6f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI outOfRangeText;
    [SerializeField] private string outOfRangeMessage = "Movement out of range.";

    private NavMeshPath navPath;
    private InputAction showVisibilityAction;
    private InputAction holdMoveAction;

    private void Awake()
    {
        navPath = new NavMeshPath();

        if (mainCamera == null) mainCamera = Camera.main;
        if (player == null)    player    = FindFirstObjectByType<Player>();
        if (agent == null)     agent     = player != null ? player.GetComponent<NavMeshAgent>() : null;
        if (abilityTargeting == null) abilityTargeting = FindFirstObjectByType<AbilityTargeting>();

        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Player");
            showVisibilityAction = map?.FindAction("ShowVisibilityFeatures");
            holdMoveAction       = map?.FindAction("HoldMove");
        }

        if (walkableMask.value == 0)
            walkableMask = ~0;
    }

    private void OnEnable()
    {
        showVisibilityAction?.Enable();
        holdMoveAction?.Enable();
    }

    private void OnDisable()
    {
        showVisibilityAction?.Disable();
        holdMoveAction?.Disable();
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

        if (!NavMesh.SamplePosition(origin, out NavMeshHit originNavHit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            HideAll();
            SetOutOfRangeText(false);
            return;
        }

        if (!NavMesh.SamplePosition(target, out NavMeshHit targetNavHit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            HideAll();
            SetOutOfRangeText(false);
            return;
        }

        Vector3 originNav = originNavHit.position;
        Vector3 targetNav = targetNavHit.position;
        bool hasDirectPath = !NavMesh.Raycast(originNav, targetNav, out NavMeshHit _, NavMesh.AllAreas);

        List<Vector3> smooth;
        if (hasDirectPath)
        {
            smooth = new List<Vector3>(2)
            {
                new Vector3(origin.x,     origin.y     + lineHeightOffset, origin.z),
                new Vector3(targetNav.x,  targetNav.y  + lineHeightOffset, targetNav.z)
            };
        }
        else
        {
            NavMesh.CalculatePath(originNav, targetNav, NavMesh.AllAreas, navPath);

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

            // Ensure the rendered line starts exactly at the player.
            elevated[0] = new Vector3(origin.x, origin.y + lineHeightOffset, origin.z);

            List<Vector3> simplified = SimplifyNearCollinearCorners(elevated, minCornerAngleForRounding);
            smooth = BuildRoundedCornerPath(simplified);
        }

        float totalLength = 0f;
        for (int i = 0; i < smooth.Count - 1; i++)
            totalLength += Vector3.Distance(smooth[i], smooth[i + 1]);

        bool inCombat = CombatManager.Instance != null && CombatManager.Instance.InCombat;
        float remaining = inCombat ? Mathf.Max(0f, player.RemainingMoveDistance) : float.MaxValue;
        bool outOfRange = totalLength > remaining;

        if (!outOfRange)
        {
            SetRenderer(whiteRenderer, smooth);
            HideRenderer(redRenderer);
            SetOutOfRangeText(false);
        }
        else
        {
            SplitPath(smooth, remaining, out List<Vector3> whitePart, out List<Vector3> redPart);
            SetRenderer(whiteRenderer, whitePart);
            SetRenderer(redRenderer,   redPart);
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
                    float t         = segLen > 0f ? (remaining - accumulated) / segLen : 0f;
                    Vector3 splitPt = Vector3.Lerp(points[i], points[i + 1], t);
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

    private static void SetRenderer(LineRenderer lr, List<Vector3> points)
    {
        if (lr == null || points == null || points.Count < 2)
        {
            HideRenderer(lr);
            return;
        }

        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
        lr.enabled = true;
    }

    private static void HideRenderer(LineRenderer lr)
    {
        if (lr == null) return;
        lr.enabled       = false;
        lr.positionCount = 0;
    }

    private void HideAll()
    {
        HideRenderer(whiteRenderer);
        HideRenderer(redRenderer);
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
        if (!HideOnCameraEnter.RaycastIgnoreTransparent(ray, out RaycastHit hit, 200f, walkableMask))
            return null;

        RoomLA currentRoom = RoomManager.Instance?.CurrentRoom;
        if (currentRoom != null && !currentRoom.Contains(hit.point))
            return null;

        return hit.point;
    }

    private bool ShouldShow()
    {
        if (player == null) return false;
        if (abilityTargeting != null && abilityTargeting.IsTargeting) return false;
        if (holdMoveAction != null && holdMoveAction.phase == InputActionPhase.Performed) return false;

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
            float turnAngle = Vector3.Angle(inDir, outDir);

            // Keep visually straight paths straight.
            if (turnAngle < minCornerAngleForRounding)
            {
                result.Add(curr);
                continue;
            }

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

    private static List<Vector3> SimplifyNearCollinearCorners(List<Vector3> points, float minTurnAngleDeg)
    {
        if (points == null || points.Count <= 2)
            return points == null ? new List<Vector3>() : new List<Vector3>(points);

        List<Vector3> simplified = new List<Vector3>(points.Count) { points[0] };

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 prev = simplified[simplified.Count - 1];
            Vector3 curr = points[i];
            Vector3 next = points[i + 1];

            Vector3 toCurr = curr - prev;
            Vector3 toNext = next - curr;
            if (toCurr.sqrMagnitude < 0.0001f || toNext.sqrMagnitude < 0.0001f)
                continue;

            float turnAngle = Vector3.Angle(toCurr, toNext);
            if (turnAngle < minTurnAngleDeg)
                continue;

            simplified.Add(curr);
        }

        simplified.Add(points[points.Count - 1]);
        return simplified;
    }
}
