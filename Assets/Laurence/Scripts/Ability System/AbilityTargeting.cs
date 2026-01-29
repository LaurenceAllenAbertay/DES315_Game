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
    public Enemy singleTarget;
    public List<Enemy> multipleTargets;
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
    [Tooltip("Layer for enemies")]
    public LayerMask enemyLayer = 1 << 8;
    
    [Tooltip("Layer for ground for AOE placement")]
    public LayerMask groundLayer = 1;
    
    [Tooltip("Layers that block point-and-click targeting (line of sight)")]
    public LayerMask targetBlockerLayer = ~0;
    
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

    // Input
    private InputAction confirmAction;
    private InputAction cancelAction;
    private InputAction pointerPositionAction;

    // Tracking
    private Enemy hoveredEnemy;
    private bool hoveredEnemyInRange;

    public bool IsTargeting => isTargeting;

    private void Awake()
    {
        mainCamera = Camera.main;

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
            Debug.LogWarning("[AbilityTargeting] Cannot start targeting with null ability or caster");
            return;
        }

        currentAbility = ability;
        currentCaster = caster;
        isTargeting = true;
        currentAbilityName = ability.abilityName;

        // Show the appropriate visualizer based on targeting type
        switch (ability.targetingType)
        {
            case TargetingType.PointAndClick:
                break;

            case TargetingType.Cone:
                if (coneVisualizer != null)
                {
                    coneVisualizer.Show(ability.range, ability.coneAngle);
                }
                break;

            case TargetingType.RangedAOE:
                if (aoeVisualizer != null)
                {
                    aoeVisualizer.Show(ability.aoeRadius, ability.range);
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
            Debug.LogWarning("[AbilityTargeting] Lost reference to current ability, cancelling");
            CancelTargeting();
            return;
        }

        // Ensure we have a camera
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[AbilityTargeting] No main camera found");
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

        // Raycast to find enemies, but use targetBlockerLayer to allow blocking
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, enemyLayer | targetBlockerLayer))
        {
            // Check if we hit an enemy
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();

            if (enemy != null)
            {
                // Check if in range
                float distance = Vector3.Distance(currentCaster.transform.position, enemy.transform.position);
                bool inRange = distance <= currentAbility.range;

                // Check line of sight
                bool hasLineOfSight = CheckLineOfSight(currentCaster.transform.position, enemy.transform.position);

                // Only consider valid if both in range and has line of sight
                bool isValidTarget = inRange && hasLineOfSight;

                if (isValidTarget)
                {
                    // Valid target - highlight it
                    if (hoveredEnemy != enemy)
                    {
                        hoveredEnemy = enemy;
                        hoveredEnemyInRange = true;
                        SetHighlight(enemy);

                        if (debugMode)
                        {
                            Debug.Log($"[AbilityTargeting] Hovering valid target: {enemy.name} (distance: {distance:F1})");
                        }
                    }
                }
                else
                {
                    // Out of range or no line of sight - clear highlight
                    if (hoveredEnemy != null)
                    {
                        ClearHighlight();

                        if (debugMode)
                        {
                            string reason = !inRange ? "out of range" : "no line of sight";
                            Debug.Log($"[AbilityTargeting] Target {enemy.name} is {reason}");
                        }
                    }
                }
            }
            else
            {
                // Hit something that's not an enemy - clear highlight
                if (hoveredEnemy != null)
                {
                    ClearHighlight();
                }
            }
        }
        else
        {
            // Hit nothing - clear highlight
            if (hoveredEnemy != null)
            {
                ClearHighlight();
            }
        }
    }

    private void UpdateConeTargeting(Vector2 pointerPos)
    {
        if (currentCaster == null || coneVisualizer == null) return;

        // Raycast to find world point under cursor
        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer | enemyLayer))
        {
            Vector3 casterPos = currentCaster.transform.position;
            Vector3 targetPoint = hit.point;

            // Calculate direction from caster to cursor point
            Vector3 direction = targetPoint - casterPos;
            direction.y = 0; // Keep it horizontal
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

        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        // Raycast to find world point
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            Vector3 casterPos = currentCaster.transform.position;
            Vector3 targetPoint = hit.point;

            // Clamp to ability range
            Vector3 offset = targetPoint - casterPos;
            offset.y = 0; // Horizontal distance only

            if (offset.magnitude > currentAbility.range)
            {
                offset = offset.normalized * currentAbility.range;
                targetPoint = casterPos + offset;
                targetPoint.y = hit.point.y; // Maintain ground height
            }

            // Snap to ground (find the actual ground position at the clamped point)
            if (Physics.Raycast(targetPoint + Vector3.up * 10f, Vector3.down, out RaycastHit groundHit, 20f, groundLayer))
            {
                targetPoint = groundHit.point;
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
        if (hoveredEnemy == null || !hoveredEnemyInRange)
        {
            if (debugMode)
            {
                Debug.Log("[AbilityTargeting] No valid target to confirm");
            }
            // Do nothing - don't cancel, just ignore the click
            return;
        }

        TargetingResult result = new TargetingResult
        {
            type = TargetingResultType.SingleTarget,
            singleTarget = hoveredEnemy,
            multipleTargets = null,
            targetPoint = hoveredEnemy.transform.position
        };

        CompleteTargeting(result);
    }

    private void ConfirmCone()
    {
        if (currentCaster == null || coneVisualizer == null) return;

        Vector3 origin = currentCaster.transform.position;
        Vector3 direction = coneVisualizer.CurrentDirection;

        List<Enemy> enemies = GetEnemiesInCone(origin, direction, currentAbility.range, currentAbility.coneAngle);

        TargetingResult result = new TargetingResult
        {
            type = TargetingResultType.MultipleTargets,
            singleTarget = null,
            multipleTargets = enemies,
            targetPoint = origin + direction * currentAbility.range
        };

        CompleteTargeting(result);
    }

    private void ConfirmAOE()
    {
        if (aoeVisualizer == null) return;

        Vector3 center = aoeVisualizer.CurrentPosition;
        List<Enemy> enemies = GetEnemiesInRadius(center, currentAbility.aoeRadius);

        TargetingResult result = new TargetingResult
        {
            type = TargetingResultType.MultipleTargets,
            singleTarget = null,
            multipleTargets = enemies,
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
            Enemy hitEnemy = hit.collider.GetComponentInParent<Enemy>();
            if (hitEnemy == null)
            {
                // Hit a blocker that's not the enemy - no line of sight
                return false;
            }
        }

        return true;
    }

    private List<Enemy> GetEnemiesInCone(Vector3 origin, Vector3 direction, float range, float angle)
    {
        List<Enemy> enemies = new List<Enemy>();

        Collider[] colliders = Physics.OverlapSphere(origin, range, enemyLayer);

        float halfAngle = angle * 0.5f;

        foreach (Collider col in colliders)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            Vector3 toEnemy = enemy.transform.position - origin;
            toEnemy.y = 0;

            float enemyAngle = Vector3.Angle(direction, toEnemy.normalized);

            if (enemyAngle <= halfAngle)
            {
                enemies.Add(enemy);
            }
        }

        return enemies;
    }

    private List<Enemy> GetEnemiesInRadius(Vector3 center, float radius)
    {
        List<Enemy> enemies = new List<Enemy>();

        Collider[] colliders = Physics.OverlapSphere(center, radius, enemyLayer);

        foreach (Collider col in colliders)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null && !enemies.Contains(enemy))
            {
                enemies.Add(enemy);
            }
        }

        return enemies;
    }

    private void SetHighlight(Enemy enemy)
    {
        if (targetHighlighter != null)
        {
            targetHighlighter.SetTarget(enemy.gameObject);
        }
    }

    private void ClearHighlight()
    {
        hoveredEnemy = null;
        hoveredEnemyInRange = false;

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
}
