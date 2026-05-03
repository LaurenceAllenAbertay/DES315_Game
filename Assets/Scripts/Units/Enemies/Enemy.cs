using System.Collections.Generic;
using UnityEngine;

public class Enemy : Unit
{
    [Header("UI")]
    [SerializeField] private Sprite icon;

    [Header("Vision Cone")]
    public EnemyVisionCone visionCone;

    [Header("Shadow Visibility")]
    [SerializeField] private LightDetectable lightDetectable;
    [Tooltip("If the player is within this distance the enemy is permanently revealed.")]
    [SerializeField] private float revealDistance = 3f;
    [Tooltip("Seconds between visibility checks (0 = every frame).")]
    [SerializeField] private float visibilityCheckInterval = 0.1f;
    [Tooltip("Material applied to all renderers while the enemy is hidden in shadow.")]
    [SerializeField] private Material hiddenMaterial;

    [Header("Proximity Detection")]
    [Tooltip("If the player gets within this distance the enemy instantly detects them, regardless of line of sight or light. Set to 0 to disable.")]
    [SerializeField] private float proximityDetectDistance = 2f;

    [Header("Distance Hiding")]
    [Tooltip("Enemies beyond this distance from the player will have their model hidden. Set to 0 to disable.")]
    [SerializeField] private float farHideDistance = 20f;

    private PlayerController playerController;
    private Renderer[] modelRenderers;
    private Material[][] originalMaterials;
    private bool hasBeenRevealed;
    private bool hiddenMaterialApplied;
    private bool isHiddenByDistance;
    private bool proximityDetected;
    private float visibilityTimer;

    public bool IsHiddenFromPlayer => hiddenMaterialApplied;
    public Sprite Icon => icon;

    protected override void Awake()
    {
        base.Awake();

        playerController = FindFirstObjectByType<PlayerController>();

        if (lightDetectable == null)
            lightDetectable = GetComponent<LightDetectable>();

        visionCone = GetComponentInChildren<EnemyVisionCone>();
        if (visionCone == null)
        {
            GameObject coneObj = new GameObject("VisionCone");
            coneObj.transform.SetParent(transform);
            coneObj.transform.localPosition = Vector3.zero;
            coneObj.transform.localRotation = Quaternion.identity;
            visionCone = coneObj.AddComponent<EnemyVisionCone>();
        }

        visionCone.OnPlayerDetected += OnPlayerEnteredVisionCone;

        CacheRenderers();
    }

    /// <summary>
    /// Caches all non-vision-cone renderers and their original materials so they can be restored after hiding.
    /// </summary>
    private void CacheRenderers()
    {
        List<Renderer> filtered = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r.GetComponentInParent<EnemyVisionCone>() != null) continue;
            filtered.Add(r);
        }

        modelRenderers = filtered.ToArray();
        originalMaterials = new Material[modelRenderers.Length][];
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            originalMaterials[i] = modelRenderers[i].materials;
        }
    }

    private void Update()
    {
        if (visibilityCheckInterval > 0f)
        {
            visibilityTimer -= Time.deltaTime;
            if (visibilityTimer > 0f) return;
            visibilityTimer = visibilityCheckInterval;
        }

        UpdateVisibility();
        UpdateDistanceVisibility();
        UpdateProximityDetection();
    }

    private void UpdateVisibility()
    {
        if (hasBeenRevealed) return;

        if (lightDetectable != null && lightDetectable.IsInLight || IsPlayerClose())
        {
            Reveal();
            return;
        }

        if (!hiddenMaterialApplied)
        {
            ApplyHiddenMaterial();
        }
    }

    /// <summary>
    /// Instantly detects the player when they enter proximityDetectDistance, bypassing LoS and light checks.
    /// </summary>
    private void UpdateProximityDetection()
    {
        if (proximityDetected || proximityDetectDistance <= 0f || playerController == null) return;

        float distSqr = (transform.position - playerController.transform.position).sqrMagnitude;
        if (distSqr > proximityDetectDistance * proximityDetectDistance) return;

        proximityDetected = true;
        OnPlayerEnteredVisionCone();
    }

    /// <summary>
    /// Toggles renderer visibility based on distance from the player, independent of the shadow material system.
    /// </summary>
    private void UpdateDistanceVisibility()
    {
        if (playerController == null || farHideDistance <= 0f) return;

        float distSqr = (transform.position - playerController.transform.position).sqrMagnitude;
        bool shouldHide = distSqr > farHideDistance * farHideDistance;

        if (shouldHide == isHiddenByDistance) return;

        isHiddenByDistance = shouldHide;
        foreach (Renderer r in modelRenderers)
        {
            if (r != null) r.enabled = !isHiddenByDistance;
        }
    }

    public void Reveal()
    {
        if (hasBeenRevealed) return;
        hasBeenRevealed = true;
        ApplyOriginalMaterials();
    }

    private bool IsPlayerClose()
    {
        return playerController != null && revealDistance > 0f &&
               Vector3.Distance(transform.position, playerController.transform.position) <= revealDistance;
    }

    private void ApplyHiddenMaterial()
    {
        if (hiddenMaterial == null || modelRenderers == null) return;

        foreach (Renderer r in modelRenderers)
        {
            if (r == null) continue;
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = hiddenMaterial;
            r.materials = mats;
        }

        hiddenMaterialApplied = true;
    }

    private void ApplyOriginalMaterials()
    {
        if (modelRenderers == null || originalMaterials == null) return;

        for (int i = 0; i < modelRenderers.Length; i++)
        {
            if (modelRenderers[i] != null)
                modelRenderers[i].materials = originalMaterials[i];
        }

        hiddenMaterialApplied = false;
    }

    public override void TakeDamage(float amount)
    {
        Reveal();
        base.TakeDamage(amount);
    }

    private void OnPlayerEnteredVisionCone()
    {
        Reveal();
        if (debugMode) Debug.Log($"[Enemy] {gameObject.name} detected the player!");
        if (CheatManager.Instance != null && CheatManager.Instance.IgnoreEnemies) return;
        CombatManager.Instance?.StartCombatFromEnemy(this);
    }

    protected override void Die()
    {
        base.Die();
        RunScoreManager.Instance?.RegisterEnemyKilled();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (visionCone != null)
            visionCone.OnPlayerDetected -= OnPlayerEnteredVisionCone;
    }
}