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

    [Header("Aim Rotation")]
    [Tooltip("Degrees per second to rotate toward aim direction. 0 = instant.")]
    [SerializeField] private float aimRotationSpeed = 720f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool isTargeting = false;
    private string currentAbilityName = "";

    [Header("Flip Visuals")]
    [SerializeField] private Color defaultHighlightColor = new Color(0.471f, 0.059f, 0.055f, 0.5f);
    [SerializeField] private Color flipHighlightColor = new Color(0.6f, 0.2f, 1f, 1f);

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
    private Unit carouselHoverUnit;

    public bool IsTargeting => isTargeting;

    private float targetConfirmedBlockTimer = 0f;
    private const float TARGET_CONFIRM_BLOCK_DURATION = 0.15f;
    public bool TargetJustConfirmed => targetConfirmedBlockTimer > 0f;

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

        if (isTargeting)
        {
            CancelTargeting();
        }
    }

    private void Update()
    {
        if (targetConfirmedBlockTimer > 0f)
            targetConfirmedBlockTimer -= Time.unscaledDeltaTime;

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

        SetFlipVisuals(false);

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
        SetFlipVisuals(false);

        carouselHoverUnit = null;
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
        if (currentAbility == null)
        {
            if (debugMode) Debug.LogWarning("[AbilityTargeting] Lost reference to current ability, cancelling");
            CancelTargeting();
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                if (debugMode) Debug.LogWarning("[AbilityTargeting] No main camera found");
                return;
            }
        }

        if (pointerPositionAction == null) return;
        Vector2 pointerPos = pointerPositionAction.ReadValue<Vector2>();

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
        if (carouselHoverUnit != null)
        {
            ProcessCarouselHoverUnit();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        LayerMask targetMask = enemyLayer | propLayer;

        if (HideOnCameraEnter.RaycastIgnoreTransparent(ray, out RaycastHit hit, 100f, targetMask | targetBlockerLayer))
        {
            Unit unit = hit.collider.GetComponentInParent<Unit>();

            if (unit != null && unit.GetComponent<Player>() == null)
            {
                if (!IsUnitInCurrentRoom(unit))
                {
                    if (hoveredUnit != null) ClearHighlight();
                    return;
                }

                if (IsUnitHiddenFromPlayer(unit))
                {
                    if (hoveredUnit != null) ClearHighlight();
                    return;
                }

                float distance = Vector3.Distance(currentCaster.transform.position, unit.transform.position);
                bool inRange = distance <= currentAbilityRange;
                bool hasLineOfSight = CheckLineOfSight(currentCaster.transform.position, unit.transform.position);
                bool isValidTarget = inRange && hasLineOfSight;

                if (isValidTarget)
                {
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

                    Vector3 toUnit = unit.transform.position - currentCaster.transform.position;
                    toUnit.y = 0f;
                    if (toUnit.sqrMagnitude > 0.0001f)
                    {
                        RotateCasterToward(toUnit.normalized);
                    }
                }
                else
                {
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
                if (hoveredUnit != null) ClearHighlight();
            }
        }
        else
        {
            if (hoveredUnit != null) ClearHighlight();
        }
    }

    private void UpdateConeTargeting(Vector2 pointerPos)
    {
        if (currentCaster == null || coneVisualizer == null) return;

        if (carouselHoverUnit != null)
        {
            ProcessCarouselHoverCone();
            return;
        }

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
                RotateCasterToward(direction);

                List<Unit> targets = GetUnitsInCone(casterPos, direction, currentAbilityRange, currentAbilityConeAngle, currentAbilityAoeHeight);
                SetHighlightsForUnits(targets);
            }
            else
            {
                ClearHighlight();
            }
        }
        else
        {
            ClearHighlight();
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

        if (carouselHoverUnit != null)
        {
            ProcessCarouselHoverAOE();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        if (HideOnCameraEnter.RaycastIgnoreTransparent(ray, out RaycastHit hit, 100f, groundLayer))
        {
            Vector3 casterPos = currentCaster.transform.position;
            Vector3 targetPoint = hit.point;

            Vector3 offset = targetPoint - casterPos;
            offset.y = 0;

            if (offset.magnitude > currentAbilityRange)
            {
                offset = offset.normalized * currentAbilityRange;
                targetPoint = casterPos + offset;
                targetPoint.y = hit.point.y;
            }

            if (HideOnCameraEnter.RaycastIgnoreTransparent(new Ray(targetPoint + Vector3.up * 10f, Vector3.down), out RaycastHit groundHit, 20f, groundLayer))
            {
                targetPoint = groundHit.point;
            }

            if (!IsPointInCurrentRoom(targetPoint))
            {
                aoeVisualizer.Hide();
                ClearHighlight();
                return;
            }

            if (!aoeVisualizer.IsVisible)
            {
                aoeVisualizer.Show(currentAbilityAoeRadius, currentAbilityRange);
            }

            aoeVisualizer.UpdatePosition(targetPoint);

            Vector3 toAoeTarget = targetPoint - currentCaster.transform.position;
            toAoeTarget.y = 0f;
            if (toAoeTarget.sqrMagnitude > 0.0001f)
            {
                RotateCasterToward(toAoeTarget.normalized);
            }

            List<Unit> targets = GetUnitsInRadius(targetPoint, currentAbilityAoeRadius, currentAbilityAoeHeight);
            SetHighlightsForUnits(targets);
        }
        else
        {
            ClearHighlight();
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
        if (hoveredUnit == null || !hoveredUnitInRange)
        {
            if (debugMode) Debug.Log("[AbilityTargeting] No valid target to confirm");
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

        carouselHoverUnit = null;
        currentAbility = null;
        currentCaster = null;
        currentAbilityName = "";
        isTargeting = false;
        targetConfirmedBlockTimer = TARGET_CONFIRM_BLOCK_DURATION;

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

    public void SetCarouselHoverUnit(Unit unit)
    {
        if (!isTargeting || currentAbility == null) return;
        carouselHoverUnit = unit;
    }

    public void ClearCarouselHoverUnit()
    {
        carouselHoverUnit = null;
    }

    /// <summary>
    /// Runs the same range/LOS checks as UpdatePointAndClickTargeting but against carouselHoverUnit instead of a raycast result.
    /// </summary>
    private void ProcessCarouselHoverUnit()
    {
        Unit unit = carouselHoverUnit;

        if (unit == null || IsUnitHiddenFromPlayer(unit) || !IsUnitInCurrentRoom(unit))
        {
            if (hoveredUnit != null) ClearHighlight();
            return;
        }

        float distance = Vector3.Distance(currentCaster.transform.position, unit.transform.position);
        bool inRange = distance <= currentAbilityRange;
        bool hasLineOfSight = CheckLineOfSight(currentCaster.transform.position, unit.transform.position);

        if (inRange && hasLineOfSight)
        {
            if (hoveredUnit != unit)
            {
                hoveredUnit = unit;
                hoveredUnitInRange = true;
                SetHighlight(unit);
            }

            Vector3 toUnit = unit.transform.position - currentCaster.transform.position;
            toUnit.y = 0f;
            if (toUnit.sqrMagnitude > 0.0001f)
            {
                RotateCasterToward(toUnit.normalized);
            }
        }
        else
        {
            if (hoveredUnit != null) ClearHighlight();
        }
    }

    /// <summary>
    /// Aims the cone visualizer toward carouselHoverUnit when a carousel entry is hovered.
    /// </summary>
    private void ProcessCarouselHoverCone()
    {
        Unit unit = carouselHoverUnit;

        if (unit == null || IsUnitHiddenFromPlayer(unit) || !IsUnitInCurrentRoom(unit))
        {
            ClearHighlight();
            return;
        }

        Vector3 casterPos = currentCaster.transform.position;
        Vector3 direction = unit.transform.position - casterPos;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f) return;

        direction.Normalize();
        coneVisualizer.UpdateDirection(casterPos, direction);
        RotateCasterToward(direction);

        List<Unit> targets = GetUnitsInCone(casterPos, direction, currentAbilityRange, currentAbilityConeAngle, currentAbilityAoeHeight);
        SetHighlightsForUnits(targets);
    }

    /// <summary>
    /// Places the AOE circle on carouselHoverUnit's position, clamped to ability range, when a carousel entry is hovered.
    /// </summary>
    private void ProcessCarouselHoverAOE()
    {
        Unit unit = carouselHoverUnit;

        if (unit == null || IsUnitHiddenFromPlayer(unit) || !IsUnitInCurrentRoom(unit))
        {
            aoeVisualizer.Hide();
            ClearHighlight();
            return;
        }

        Vector3 casterPos = currentCaster.transform.position;
        Vector3 targetPoint = unit.transform.position;

        Vector3 offset = targetPoint - casterPos;
        offset.y = 0f;
        if (offset.magnitude > currentAbilityRange)
        {
            offset = offset.normalized * currentAbilityRange;
            targetPoint = casterPos + offset;
            targetPoint.y = unit.transform.position.y;
        }

        if (HideOnCameraEnter.RaycastIgnoreTransparent(new Ray(targetPoint + Vector3.up * 10f, Vector3.down), out RaycastHit groundHit, 20f, groundLayer))
        {
            targetPoint = groundHit.point;
        }

        if (!IsPointInCurrentRoom(targetPoint))
        {
            aoeVisualizer.Hide();
            ClearHighlight();
            return;
        }

        if (!aoeVisualizer.IsVisible)
        {
            aoeVisualizer.Show(currentAbilityAoeRadius, currentAbilityRange);
        }

        aoeVisualizer.UpdatePosition(targetPoint);

        Vector3 toTarget = targetPoint - casterPos;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            RotateCasterToward(toTarget.normalized);
        }

        List<Unit> targets = GetUnitsInRadius(targetPoint, currentAbilityAoeRadius, currentAbilityAoeHeight);
        SetHighlightsForUnits(targets);
    }

    public void SetFlipVisuals(bool enabled)
    {
        Color highlightColor = enabled ? flipHighlightColor : defaultHighlightColor;
        if (targetHighlighter != null) targetHighlighter.SetColor(highlightColor);
    }

    /// <summary>
    /// Rotates the caster to face the given flat (XZ) direction, respecting aimRotationSpeed.
    /// </summary>
    private void RotateCasterToward(Vector3 direction)
    {
        if (currentCaster == null || direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        if (aimRotationSpeed <= 0f)
        {
            currentCaster.transform.rotation = targetRotation;
        }
        else
        {
            currentCaster.transform.rotation = Quaternion.RotateTowards(
                currentCaster.transform.rotation,
                targetRotation,
                aimRotationSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Check if there is line of sight between two points
    /// </summary>
    private bool CheckLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;

        Vector3 startPos = from + Vector3.up * 0.5f;
        Vector3 endPos = to + Vector3.up * 0.5f;
        direction = endPos - startPos;

        if (Physics.Raycast(startPos, direction.normalized, out RaycastHit hit, distance, targetBlockerLayer))
        {
            Unit hitUnit = hit.collider.GetComponentInParent<Unit>();
            if (hitUnit == null)
            {
                return false;
            }
        }

        return true;
    }

    private List<Unit> GetUnitsInCone(Vector3 origin, Vector3 direction, float range, float angle, float height)
    {
        List<Unit> targets = new List<Unit>();
        HashSet<Unit> seenUnits = new HashSet<Unit>();

        LayerMask targetMask = enemyLayer | propLayer;
        Collider[] colliders = Physics.OverlapSphere(origin, range, targetMask);

        float halfAngle = angle * 0.5f;
        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
        if (flatDirection.sqrMagnitude > 0.0001f)
        {
            flatDirection.Normalize();
        }

        foreach (Collider col in colliders)
        {
            Unit unit = col.GetComponentInParent<Unit>();
            if (unit == null || unit.GetComponent<Player>() != null) continue;
            if (seenUnits.Contains(unit)) continue;
            if (!IsUnitInCurrentRoom(unit)) continue;

            if (!IsColliderWithinCone(col, origin, flatDirection, range, halfAngle, height)) continue;
            if (!CheckLineOfSight(origin, unit.transform.position)) continue;

            seenUnits.Add(unit);
            targets.Add(unit);
        }

        return targets;
    }

    private List<Unit> GetUnitsInRadius(Vector3 center, float radius, float height)
    {
        List<Unit> targets = new List<Unit>();
        HashSet<Unit> seenUnits = new HashSet<Unit>();

        LayerMask targetMask = enemyLayer | propLayer;
        Collider[] colliders = Physics.OverlapSphere(center, radius, targetMask);

        foreach (Collider col in colliders)
        {
            Unit unit = col.GetComponentInParent<Unit>();
            if (unit == null || unit.GetComponent<Player>() != null || seenUnits.Contains(unit)) continue;
            if (!IsUnitInCurrentRoom(unit)) continue;
            if (!IsColliderWithinRadius(col, center, radius, height)) continue;

            seenUnits.Add(unit);
            targets.Add(unit);
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

    private void SetHighlightsForUnits(List<Unit> units)
    {
        if (targetHighlighter == null) return;

        if (units == null || units.Count == 0)
        {
            targetHighlighter.ClearTargets();
            return;
        }

        List<GameObject> targets = new List<GameObject>(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit == null) continue;
            if (IsUnitHiddenFromPlayer(unit)) continue;
            targets.Add(unit.gameObject);
        }

        if (targets.Count == 0)
        {
            targetHighlighter.ClearTargets();
            return;
        }

        targetHighlighter.SetTargets(targets);
    }

    private void ClearHighlight()
    {
        hoveredUnit = null;
        hoveredUnitInRange = false;

        if (targetHighlighter != null)
        {
            targetHighlighter.ClearTargets();
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

    private bool IsColliderWithinRadius(Collider col, Vector3 center, float radius, float height)
    {
        Vector3 closestPoint = col.ClosestPoint(center);
        Vector3 flatOffset = closestPoint - center;
        flatOffset.y = 0f;

        if (flatOffset.magnitude > radius) return false;

        if (height > 0f)
        {
            float verticalDistance = Mathf.Abs(closestPoint.y - center.y);
            if (verticalDistance > height) return false;
        }

        return true;
    }

    private bool IsColliderWithinCone(Collider col, Vector3 origin, Vector3 flatDirection, float range, float halfAngle, float height)
    {
        Vector3 toBoundsCenter = col.bounds.center - origin;
        float projected = Vector3.Dot(toBoundsCenter, flatDirection);
        projected = Mathf.Clamp(projected, 0f, range);
        Vector3 axisPoint = origin + flatDirection * projected;
        Vector3 closestPoint = col.ClosestPoint(axisPoint);

        Vector3 toPoint = closestPoint - origin;
        Vector3 toPointFlat = new Vector3(toPoint.x, 0f, toPoint.z);

        if (toPointFlat.sqrMagnitude <= 0.0001f) return true;

        float angleToPoint = Vector3.Angle(flatDirection, toPointFlat.normalized);
        if (angleToPoint > halfAngle) return false;
        if (toPointFlat.magnitude > range) return false;

        if (height > 0f)
        {
            float verticalDistance = Mathf.Abs(closestPoint.y - origin.y);
            if (verticalDistance > height) return false;
        }

        return true;
    }
}