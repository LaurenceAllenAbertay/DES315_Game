using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Result type from targeting
/// </summary>
public enum TargetingResultType
{
    SingleTarget,
    MultipleTargets,
    Point
}

/// <summary>
/// Data returned when targeting completes
/// </summary>
public struct TargetingResult
{
    public TargetingResultType type;
    public Unit singleTarget;
    public List<Unit> multipleTargets;
    public Vector3 targetPoint;
}

/// <summary>
/// Handles all ability targeting modes: Point & Click, Cone, and Ranged AOE
/// Manages visualization and input during targeting
/// </summary>
public class AbilityTargeting : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("Targeting Settings")]
    [Tooltip("Layer for targetable units")]
    public LayerMask enemyLayer = 1 << 8;
    
    [Tooltip("Layer for ground for AOE placement")]
    public LayerMask groundLayer = 1;
    
    [Tooltip("Layers that block point-and-click targeting (line of sight)")]
    public LayerMask targetBlockerLayer = ~0;

    [Tooltip("Layer for props/destructibles")]
    public LayerMask propLayer = 1 << 9;
    
    [Header("Visualizers")] 
    private ConeTargetingVisualizer coneVisualizer;
    private AOECircleVisualizer aoeVisualizer;
    private TargetHighlighter targetHighlighter;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool isTargeting = false;
    private string currentAbilityName = "";

    // Events
    public delegate void TargetConfirmed(TargetingResult result);
    public event TargetConfirmed OnTargetConfirmed;

    public delegate void TargetingCancelled();
    public event TargetingCancelled OnTargetingCancelled;

    // State
    private Ability currentAbility;
    private Player currentCaster;
    private Camera mainCamera;
    private RoomManager roomManager;
    private RoomLA currentAoeRoom;
    private float currentAbilityRange;
    private float currentAbilityConeAngle;
    private float currentAbilityAoeRadius;
    private float currentAbilityAoeHeight;

    // Input
    private InputAction confirmAction;
    private InputAction cancelAction;
    private InputAction pointerPositionAction;

    // Tracking
    private Unit hoveredUnit;
    private bool hoveredUnitInRange;

    public bool IsTargeting => isTargeting;

    private void Awake()
    {
        mainCamera = Camera.main;
        roomManager = FindFirstObjectByType<RoomManager>();

        // Setup input
        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            confirmAction = playerMap.FindAction("ConfirmTarget");
            cancelAction = playerMap.FindAction("CancelAbility");
            pointerPositionAction = playerMap.FindAction("PointerPosition");
        }
        
        // Create Visualizers
        if (coneVisualizer == null)
        {
            GameObject coneGO = new GameObject("ConeTargetingVisualizer");
            coneGO.transform.SetParent(transform);
            coneVisualizer = coneGO.AddComponent<ConeTargetingVisualizer>();
        }

        if (aoeVisualizer == null)
        {
            GameObject aoeGO = new GameObject("AOECircleVisualizer");
            aoeGO.transform.SetParent(transform);
            aoeVisualizer = aoeGO.AddComponent<AOECircleVisualizer>();
        }

        if (targetHighlighter == null)
        {
            GameObject highlightGO = new GameObject("TargetHighlighter");
            highlightGO.transform.SetParent(transform);
            targetHighlighter = highlightGO.AddComponent<TargetHighlighter>();
        }
    }

    private void OnEnable()
    {
        if (confirmAction != null)
        {
            confirmAction.performed += OnConfirmPerformed;
            confirmAction.Enable();
        }

        if (pointerPositionAction != null)
        {
            pointerPositionAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (confirmAction != null)
        {
            confirmAction.performed -= OnConfirmPerformed;
            confirmAction.Disable();
        }

        if (pointerPositionAction != null)
        {
            pointerPositionAction.Disable();
        }

        // Clean up targeting state if the component is disabled
        if (isTargeting)
        {
            CancelTargeting();
        }
    }

    private void Update()
    {
        if (!isTargeting) return;

        UpdateTargeting();
    }

    /// <summary>
    /// Start targeting for an ability
    /// </summary>
    public void StartTargeting(Ability ability, Player caster)
    {
        if (ability == null || caster == null)
        {
            if (debugMode) Debug.LogWarning("[AbilityTargeting] Cannot start targeting with null ability or caster");
            return;
        }

        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }

        currentAbility = ability;
        currentCaster = caster;
        isTargeting = true;
        currentAbilityName = ability.abilityName;
        currentAbilityRange = StatsManager.Instance != null
            ? StatsManager.Instance.ApplyAbilityRange(ability.range)
            : ability.range;
        currentAbilityConeAngle = StatsManager.Instance != null
            ? StatsManager.Instance.ApplyAoeSize(ability.coneAngle)
            : ability.coneAngle;
        currentAbilityAoeRadius = StatsManager.Instance != null
            ? StatsManager.Instance.ApplyAoeSize(ability.aoeRadius)
            : ability.aoeRadius;
        currentAbilityAoeHeight = StatsManager.Instance != null
            ? StatsManager.Instance.ApplyAoeSize(ability.aoeHeight)
            : ability.aoeHeight;

        // Show the appropriate visualizer based on targeting type
        switch (ability.targetingType)
        {
            case TargetingType.PointAndClick:
                break;

            case TargetingType.Cone:
                if (coneVisualizer != null)
                {
                    coneVisualizer.SetObstacleMask(targetBlockerLayer);
                    coneVisualizer.Show(currentAbilityRange, currentAbilityConeAngle);
                }
                break;

            case TargetingType.RangedAOE:
                if (aoeVisualizer != null)
                {
                    currentAoeRoom = roomManager != null ? roomManager.CurrentRoom : null;
                    aoeVisualizer.SetRoom(currentAoeRoom);
                    aoeVisualizer.Show(currentAbilityAoeRadius, currentAbilityRange);
                }
                break;
        }

        if (debugMode)
        {
            Debug.Log($"[AbilityTargeting] Started targeting for '{currentAbilityName}' (Type: {ability.targetingType})");
        }
    }

    /// <summary>
    /// Cancel current targeting
    /// </summary>
    public void CancelTargeting()
    {
        HideAllVisualizers();
        ClearHighlight();

        currentAbility = null;
        currentCaster = null;
        currentAbilityName = "";
        isTargeting = false;

        OnTargetingCancelled?.Invoke();

        if (debugMode)
        {
            Debug.Log("[AbilityTargeting] Targeting cancelled");
        }
    }

    private void UpdateTargeting()
    {
        // Validate current ability exists
        if (currentAbility == null)
        {
            if (debugMode) Debug.LogWarning("[AbilityTargeting] Lost reference to current ability, cancelling");
            CancelTargeting();
            return;
        }

        // Ensure we have a camera
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                if (debugMode) Debug.LogWarning("[AbilityTargeting] No main camera found");
                return;
            }
        }

        // Read pointer position
        if (pointerPositionAction == null) return;
        Vector2 pointerPos = pointerPositionAction.ReadValue<Vector2>();

        // Route to appropriate targeting update
        switch (currentAbility.targetingType)
        {
            case TargetingType.PointAndClick:
                UpdatePointAndClickTargeting(pointerPos);
                break;

            case TargetingType.Cone:
                UpdateConeTargeting(pointerPos);
                break;

            case TargetingType.RangedAOE:
                UpdateAOETargeting(pointerPos);
                break;
        }
    }

    private void UpdatePointAndClickTargeting(Vector2 pointerPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        LayerMask targetMask = enemyLayer | propLayer;

        // Raycast to find targets, but use targetBlockerLayer to allow blocking
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, targetMask | targetBlockerLayer))
        {
            // Check if we hit an enemy
            Unit unit = hit.collider.GetComponentInParent<Unit>();

            if (unit != null && unit.GetComponent<Player>() == null)
            {
                if (!IsUnitInCurrentRoom(unit))
                {
                    if (hoveredUnit != null)
                    {
                        ClearHighlight();
                    }
                    return;
                }

                if (IsUnitHiddenFromPlayer(unit))
                {
                    if (hoveredUnit != null)
                    {
                        ClearHighlight();
                    }
                    return;
                }

                // Check if in range
                float distance = Vector3.Distance(currentCaster.transform.position, unit.transform.position);
                bool inRange = distance <= currentAbilityRange;

                // Check line of sight
                bool hasLineOfSight = CheckLineOfSight(currentCaster.transform.position, unit.transform.position);

                // Only consider valid if both in range and has line of sight
                bool isValidTarget = inRange && hasLineOfSight;

                if (isValidTarget)
                {
                    // Valid target - highlight it
                    if (hoveredUnit != unit)
                    {
                        hoveredUnit = unit;
                        hoveredUnitInRange = true;
                        SetHighlight(unit);

                        if (debugMode)
                        {
                            Debug.Log($"[AbilityTargeting] Hovering valid target: {unit.name} (distance: {distance:F1})");
                        }
                    }
                }
                else
                {
                    // Out of range or no line of sight - clear highlight
                    if (hoveredUnit != null)
                    {
                        ClearHighlight();

                        if (debugMode)
                        {
                            string reason = !inRange ? "out of range" : "no line of sight";
                            Debug.Log($"[AbilityTargeting] Target {unit.name} is {reason}");
                        }
                    }
                }
            }
            else
            {
                // Hit something that's not a unit - clear highlight
                if (hoveredUnit != null)
                {
                    ClearHighlight();
                }
            }
        }
        else
        {
            // Hit nothing - clear highlight
            if (hoveredUnit != null)
            {
                ClearHighlight();
            }
        }
    }

    private void UpdateConeTargeting(Vector2 pointerPos)
    {
        if (currentCaster == null || coneVisualizer == null) return;

        Ray ray = mainCamera.ScreenPointToRay(pointerPos);
        Vector3 casterPos = currentCaster.transform.position;
        Plane plane = new Plane(Vector3.up, casterPos);
        if (plane.Raycast(ray, out float distance))
        {
            Vector3 targetPoint = ray.GetPoint(distance);
            Vector3 direction = targetPoint - casterPos;
            direction.y = 0;
            direction.Normalize();

            if (direction != Vector3.zero)
            {
                coneVisualizer.UpdateDirection(casterPos, direction);
            }
        }
    }

    private void UpdateAOETargeting(Vector2 pointerPos)
    {
        if (currentCaster == null || aoeVisualizer == null) return;
        if (roomManager != null && roomManager.CurrentRoom != currentAoeRoom)
        {
            currentAoeRoom = roomManager.CurrentRoom;
            aoeVisualizer.SetRoom(currentAoeRoom);
        }

        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        // Raycast to find world point
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            Vector3 casterPos = currentCaster.transform.position;
            Vector3 targetPoint = hit.point;

            // Clamp to ability range
            Vector3 offset = targetPoint - casterPos;
            offset.y = 0; // Horizontal distance only

            if (offset.magnitude > currentAbilityRange)
            {
                offset = offset.normalized * currentAbilityRange;
                targetPoint = casterPos + offset;
                targetPoint.y = hit.point.y; // Maintain ground height
            }

            // Snap to ground (find the actual ground position at the clamped point)
            if (Physics.Raycast(targetPoint + Vector3.up * 10f, Vector3.down, out RaycastHit groundHit, 20f, groundLayer))
            {
                targetPoint = groundHit.point;
            }

            if (!IsPointInCurrentRoom(targetPoint))
            {
                aoeVisualizer.Hide();
                return;
            }

            if (!aoeVisualizer.IsVisible)
            {
                aoeVisualizer.Show(currentAbilityAoeRadius, currentAbilityRange);
            }

            aoeVisualizer.UpdatePosition(targetPoint);
        }
    }

    private void OnConfirmPerformed(InputAction.CallbackContext context)
    {
        if (!isTargeting || currentAbility == null) return;

        switch (currentAbility.targetingType)
        {
            case TargetingType.PointAndClick:
                ConfirmPointAndClick();
                break;

            case TargetingType.Cone:
                ConfirmCone();
                break;

            case TargetingType.RangedAOE:
                ConfirmAOE();
                break;
        }
    }

    private void ConfirmPointAndClick()
    {
        // Validate we have a valid target
        if (hoveredUnit == null || !hoveredUnitInRange)
        {
            if (debugMode)
            {
                Debug.Log("[AbilityTargeting] No valid target to confirm");
            }
            // Do nothing - don't cancel, just ignore the click
            return;
        }

        if (IsUnitHiddenFromPlayer(hoveredUnit))
        {
            ClearHighlight();
            return;
        }

        TargetingResult result = new TargetingResult
        {
            type = TargetingResultType.SingleTarget,
            singleTarget = hoveredUnit,
            multipleTargets = null,
            targetPoint = hoveredUnit.transform.position
        };

        CompleteTargeting(result);
    }

    private void ConfirmCone()
    {
        if (currentCaster == null || coneVisualizer == null) return;

        Vector3 origin = currentCaster.transform.position;
        Vector3 direction = coneVisualizer.CurrentDirection;

        List<Unit> targets = GetUnitsInCone(origin, direction, currentAbilityRange, currentAbilityConeAngle, currentAbilityAoeHeight);

        TargetingResult result = new TargetingResult
        {
            type = TargetingResultType.MultipleTargets,
            singleTarget = null,
            multipleTargets = targets,
            targetPoint = origin + direction * currentAbilityRange
        };

        CompleteTargeting(result);
    }

    private void ConfirmAOE()
    {
        if (aoeVisualizer == null || currentCaster == null) return;

        Vector3 center = aoeVisualizer.CurrentPosition;
        if (!HasLineOfSightToPoint(currentCaster.transform.position, center))
        {
            if (debugMode)
            {
                Debug.Log("[AbilityTargeting] AOE cast blocked by line of sight");
            }
            return;
        }

        List<Unit> targets = GetUnitsInRadius(center, currentAbilityAoeRadius, currentAbilityAoeHeight);

        TargetingResult result = new TargetingResult
        {
            type = TargetingResultType.MultipleTargets,
            singleTarget = null,
            multipleTargets = targets,
            targetPoint = center
        };

        CompleteTargeting(result);
    }

    private void CompleteTargeting(TargetingResult result)
    {
        HideAllVisualizers();
        ClearHighlight();

        string abilityName = currentAbilityName;

        currentAbility = null;
        currentCaster = null;
        currentAbilityName = "";
        isTargeting = false;

        if (debugMode)
        {
            string targetInfo = result.type switch
            {
                TargetingResultType.SingleTarget => $"single target: {result.singleTarget?.name}",
                TargetingResultType.MultipleTargets => $"{result.multipleTargets?.Count ?? 0} targets",
                TargetingResultType.Point => $"point: {result.targetPoint}",
                _ => "unknown"
            };
            Debug.Log($"[AbilityTargeting] Confirmed '{abilityName}' with {targetInfo}");
        }

        OnTargetConfirmed?.Invoke(result);
    }

    /// <summary>
    /// Check if there is line of sight between two points
    /// </summary>
    private bool CheckLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;

        // Offset the start position slightly to avoid self-intersection
        Vector3 startPos = from + Vector3.up * 0.5f;
        Vector3 endPos = to + Vector3.up * 0.5f;
        direction = endPos - startPos;

        // Raycast to check for blockers
        if (Physics.Raycast(startPos, direction.normalized, out RaycastHit hit, distance, targetBlockerLayer))
        {
            // Check if we hit something that isn't the target
            Unit hitUnit = hit.collider.GetComponentInParent<Unit>();
            if (hitUnit == null)
            {
                // Hit a blocker that's not the target - no line of sight
                return false;
            }
        }

        return true;
    }

    private List<Unit> GetUnitsInCone(Vector3 origin, Vector3 direction, float range, float angle, float height)
    {
        List<Unit> targets = new List<Unit>();

        LayerMask targetMask = enemyLayer | propLayer;
        Collider[] colliders = Physics.OverlapSphere(origin, range, targetMask);

        float halfAngle = angle * 0.5f;

        foreach (Collider col in colliders)
        {
            Unit unit = col.GetComponentInParent<Unit>();
            if (unit == null || unit.GetComponent<Player>() != null) continue;
            if (!IsUnitInCurrentRoom(unit)) continue;

            Vector3 toUnit = unit.transform.position - origin;
            toUnit.y = 0;

            float unitAngle = Vector3.Angle(direction, toUnit.normalized);

            if (unitAngle <= halfAngle)
            {
                if (height > 0f)
                {
                    float verticalDistance = Mathf.Abs(unit.transform.position.y - origin.y);
                    if (verticalDistance > height)
                    {
                        continue;
                    }
                }

                targets.Add(unit);
            }
        }

        return targets;
    }

    private List<Unit> GetUnitsInRadius(Vector3 center, float radius, float height)
    {
        List<Unit> targets = new List<Unit>();

        LayerMask targetMask = enemyLayer | propLayer;
        Collider[] colliders = Physics.OverlapSphere(center, radius, targetMask);

        foreach (Collider col in colliders)
        {
            Unit unit = col.GetComponentInParent<Unit>();
            if (unit == null || unit.GetComponent<Player>() != null || targets.Contains(unit))
            {
                continue;
            }

            if (!IsUnitInCurrentRoom(unit))
            {
                continue;
            }

            if (height > 0f)
            {
                float verticalDistance = Mathf.Abs(unit.transform.position.y - center.y);
                if (verticalDistance > height)
                {
                    continue;
                }
            }

            if (!targets.Contains(unit))
            {
                targets.Add(unit);
            }
        }

        return targets;
    }

    private void SetHighlight(Unit unit)
    {
        if (targetHighlighter != null)
        {
            targetHighlighter.SetTarget(unit.gameObject);
        }
    }

    private void ClearHighlight()
    {
        hoveredUnit = null;
        hoveredUnitInRange = false;

        if (targetHighlighter != null)
        {
            targetHighlighter.ClearTarget();
        }
    }

    private void HideAllVisualizers()
    {
        if (coneVisualizer != null) coneVisualizer.Hide();
        if (aoeVisualizer != null) aoeVisualizer.Hide();
    }

    private bool IsUnitHiddenFromPlayer(Unit unit)
    {
        if (unit == null) return false;
        Enemy enemy = unit.GetComponent<Enemy>();
        return enemy != null && enemy.IsHiddenFromPlayer;
    }

    private bool IsUnitInCurrentRoom(Unit unit)
    {
        if (unit == null) return false;
        if (roomManager == null || roomManager.CurrentRoom == null) return true;
        return roomManager.CurrentRoom.Contains(unit.transform.position);
    }

    private bool IsPointInCurrentRoom(Vector3 point)
    {
        if (roomManager == null || roomManager.CurrentRoom == null) return true;
        return roomManager.CurrentRoom.Contains(point);
    }

    private bool HasLineOfSightToPoint(Vector3 from, Vector3 targetPoint)
    {
        Vector3 startPos = from + Vector3.up * 0.5f;
        Vector3 endPos = targetPoint + Vector3.up * 0.5f;
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;

        return !Physics.Raycast(startPos, direction.normalized, distance, targetBlockerLayer);
    }
}
