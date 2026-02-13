using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World-space ring that shows remaining movement distance.
/// Shrinks as the player moves during their turn.
/// </summary>
public class MovementRing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private CombatManager combatManager;

    [Header("Sizing")]
    [Tooltip("World radius represented by the ring when local scale is 1 on X/Z.")]
    [SerializeField] private float baseRadius = 5f;

    [Tooltip("Minimum radius to avoid collapsing to zero.")]
    [SerializeField] private float minRadius = 0.25f;

    [Header("Animation")]
    [SerializeField] private float lerpSpeed = 8f;

    private Vector3 baseScale;
    private float targetRadius;
    private SpriteRenderer sr;
    private void Awake()
    {
        baseScale = transform.localScale;
        sr = GetComponent<SpriteRenderer>();
        if (baseRadius <= 0f)
        {
            baseRadius = 1f;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (!ShouldShow())
        {
            UpdateVisibility();
            return;
        }

        UpdateTargetRadius();
        UpdateScale();
        UpdateVisibility();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (combatManager == null)
        {
            combatManager = CombatManager.Instance != null
                ? CombatManager.Instance
                : FindFirstObjectByType<CombatManager>();
        }
    }

    private void SubscribeEvents()
    {
        if (combatManager != null)
        {
            combatManager.OnTurnStarted += HandleTurnChanged;
            combatManager.OnTurnEnded += HandleTurnChanged;
            combatManager.OnCombatStarted += HandleCombatStarted;
            combatManager.OnCombatEnded += HandleCombatEnded;
            combatManager.OnPhaseChanged += HandlePhaseChanged;
        }

        if (player != null)
        {
            player.OnCombatStateChanged += HandleCombatStateChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (combatManager != null)
        {
            combatManager.OnTurnStarted -= HandleTurnChanged;
            combatManager.OnTurnEnded -= HandleTurnChanged;
            combatManager.OnCombatStarted -= HandleCombatStarted;
            combatManager.OnCombatEnded -= HandleCombatEnded;
            combatManager.OnPhaseChanged -= HandlePhaseChanged;
        }

        if (player != null)
        {
            player.OnCombatStateChanged -= HandleCombatStateChanged;
        }
    }

    private void HandleTurnChanged(Unit unit)
    {
        Refresh();
    }

    private void HandleCombatStarted(List<Enemy> enemies)
    {
        Refresh();
    }

    private void HandleCombatEnded(CombatManager.CombatOutcome outcome)
    {
        Refresh();
    }

    private void HandlePhaseChanged(CombatManager.CombatPhase phase)
    {
        Refresh();
    }

    private void HandleCombatStateChanged(bool inCombat)
    {
        Refresh();
    }

    private void Refresh()
    {
        UpdateVisibility();
        if (ShouldShow())
        {
            UpdateTargetRadius();
            UpdateScale(true);
        }
    }

    private void UpdateVisibility()
    {
        sr.enabled = ShouldShow() && transform.localScale.x >= minRadius;
    }

    private void UpdateTargetRadius()
    {
        if (player == null)
        {
            targetRadius = minRadius;
            return;
        }

        float maxRadius = Mathf.Max(0f, player.MaxCombatMoveDistance);
        float remaining = Mathf.Clamp(player.RemainingMoveDistance, 0f, maxRadius);
        targetRadius = Mathf.Max(minRadius, remaining);
    }

    private void UpdateScale(bool snap = false)
    {
        float scaleFactor = targetRadius / baseRadius;
        Vector3 desired = new Vector3(baseScale.x * scaleFactor, baseScale.y * scaleFactor, baseScale.z);

        if (snap)
        {
            transform.localScale = desired;
            return;
        }

        float t = Mathf.Clamp01(Time.deltaTime * lerpSpeed);
        transform.localScale = Vector3.Lerp(transform.localScale, desired, t);
    }

    private bool ShouldShow()
    {
        if (player == null || combatManager == null)
        {
            sr.enabled = false;
            return false;
        }

        return combatManager.InCombat && combatManager.IsPlayerTurn;
    }
}
