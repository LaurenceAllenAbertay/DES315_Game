using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Enemy-specific functionality built on top of Unit base class
/// Handles vision cones and enemy-specific mechanics
/// </summary>
public class Enemy : Unit
{
    [Header("Vision Cone")]
    public EnemyVisionCone visionCone;

    [Header("Shadow Visibility")]
    [Tooltip("Assign the visual model root (do not assign the enemy root).")]
    [SerializeField] private GameObject modelRoot;
    [SerializeField] private Vector3 lightCheckOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("If the player is within this distance, the enemy is visible even in shadow.")]
    [SerializeField] private float revealDistance = 3f;
    [Tooltip("Seconds between visibility checks (0 = every frame).")]
    [SerializeField] private float visibilityCheckInterval = 0.1f;

    private PlayerController playerController;
    private Renderer[] modelRenderers;
    private bool isModelVisible = true;
    private float visibilityTimer;
    private bool forceRevealUntilTurnEnd;
    private bool combatCallbacksRegistered;

    public bool IsHiddenFromPlayer => !isModelVisible;

    protected override void Awake()
    {
        base.Awake();

        // Get or create vision cone
        visionCone = GetComponentInChildren<EnemyVisionCone>();
        if (visionCone == null)
        {
            GameObject coneObj = new GameObject("VisionCone");
            coneObj.transform.SetParent(transform);
            coneObj.transform.localPosition = Vector3.zero;
            coneObj.transform.localRotation = Quaternion.identity;
            visionCone = coneObj.AddComponent<EnemyVisionCone>();
        }

        // Subscribe to player detection
        visionCone.OnPlayerDetected += OnPlayerEnteredVisionCone;

        CacheModelRenderers();
    }

    private void OnEnable()
    {
        RegisterCombatCallbacks();
    }

    private void OnDisable()
    {
        UnregisterCombatCallbacks();
    }

    private void Start()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }
        RegisterCombatCallbacks();
        UpdateModelVisibility();
    }

    private void Update()
    {
        if (visibilityCheckInterval > 0f)
        {
            visibilityTimer -= Time.deltaTime;
            if (visibilityTimer > 0f)
            {
                return;
            }
            visibilityTimer = visibilityCheckInterval;
        }

        UpdateModelVisibility();
    }

    private void OnPlayerEnteredVisionCone()
    {
        if (IsInShadow())
        {
            RevealUntilTurnEnd();
        }

        // TODO: Add combat start logic here
        if (debugMode) Debug.Log($"[Enemy] {gameObject.name} detected the player!");
        CombatManager.Instance?.StartCombatFromEnemy(this);
    }

    protected override void Die()
    {
        base.Die();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (visionCone != null)
        {
            visionCone.OnPlayerDetected -= OnPlayerEnteredVisionCone;
        }
        UnregisterCombatCallbacks();
    }

    private void CacheModelRenderers()
    {
        if (modelRoot != null)
        {
            modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> filtered = new List<Renderer>(renderers.Length);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (renderer.GetComponentInParent<EnemyVisionCone>() != null) continue;
            filtered.Add(renderer);
        }

        modelRenderers = filtered.ToArray();
    }

    private void UpdateModelVisibility()
    {
        if (forceRevealUntilTurnEnd)
        {
            SetModelVisible(true);
            UpdateVisionConeVisibility(true);
            return;
        }

        if (LightDetectionManager.Instance == null)
        {
            SetModelVisible(true);
            UpdateVisionConeVisibility(true);
            return;
        }

        bool playerClose = false;
        bool playerInLight = true;
        if (playerController != null && revealDistance > 0f)
        {
            playerClose = Vector3.Distance(transform.position, playerController.transform.position) <= revealDistance;
            playerInLight = playerController.IsInLight;
        }

        Vector3 enemyCheckPoint = transform.position + lightCheckOffset;
        bool enemyInLight = LightDetectionManager.Instance.IsPointInLight(enemyCheckPoint);
        bool shouldBeVisible = enemyInLight || (playerClose && !playerInLight);
        SetModelVisible(shouldBeVisible);
        UpdateVisionConeVisibility(shouldBeVisible);
    }

    private void UpdateVisionConeVisibility(bool isVisible)
    {
        if (visionCone == null) return;
        if (!isVisible)
        {
            visionCone.SetVisible(false);
        }
    }

    private void SetModelVisible(bool visible)
    {
        if (isModelVisible == visible)
        {
            return;
        }

        isModelVisible = visible;

        if (modelRoot != null)
        {
            modelRoot.SetActive(visible);
            return;
        }

        if (modelRenderers == null || modelRenderers.Length == 0)
        {
            return;
        }

        foreach (Renderer renderer in modelRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private bool IsInShadow()
    {
        if (LightDetectionManager.Instance == null)
        {
            return false;
        }

        Vector3 enemyCheckPoint = transform.position + lightCheckOffset;
        return !LightDetectionManager.Instance.IsPointInLight(enemyCheckPoint);
    }

    private void RevealUntilTurnEnd()
    {
        if (forceRevealUntilTurnEnd)
        {
            return;
        }

        forceRevealUntilTurnEnd = true;
        SetModelVisible(true);
        UpdateVisionConeVisibility(true);
    }

    private void RegisterCombatCallbacks()
    {
        if (combatCallbacksRegistered)
        {
            return;
        }

        CombatManager combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            return;
        }

        combatManager.OnTurnEnded += HandleTurnEnded;
        combatManager.OnCombatEnded += HandleCombatEnded;
        combatCallbacksRegistered = true;
    }

    private void UnregisterCombatCallbacks()
    {
        if (!combatCallbacksRegistered)
        {
            return;
        }

        CombatManager combatManager = CombatManager.Instance;
        if (combatManager != null)
        {
            combatManager.OnTurnEnded -= HandleTurnEnded;
            combatManager.OnCombatEnded -= HandleCombatEnded;
        }

        combatCallbacksRegistered = false;
    }

    private void HandleTurnEnded(Unit unit)
    {
        if (!forceRevealUntilTurnEnd || unit != this)
        {
            return;
        }

        forceRevealUntilTurnEnd = false;
        UpdateModelVisibility();
    }

    private void HandleCombatEnded(CombatManager.CombatOutcome outcome)
    {
        if (!forceRevealUntilTurnEnd)
        {
            return;
        }

        forceRevealUntilTurnEnd = false;
        UpdateModelVisibility();
    }
}
