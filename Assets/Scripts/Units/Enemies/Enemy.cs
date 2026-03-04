using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy-specific functionality built on top of Unit base class.
/// Handles vision cones, shadow visibility via material swapping, and permanent reveal tracking.
/// </summary>
public class Enemy : Unit
{
    [Header("UI")]
    [SerializeField] private Sprite icon;

    [Header("Vision Cone")]
    public EnemyVisionCone visionCone;

    [Header("Shadow Visibility")]
    [SerializeField] private Vector3 lightCheckOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("If the player is within this distance the enemy is permanently revealed.")]
    [SerializeField] private float revealDistance = 3f;
    [Tooltip("Seconds between visibility checks (0 = every frame).")]
    [SerializeField] private float visibilityCheckInterval = 0.1f;
    [Tooltip("Material applied to all renderers while the enemy is hidden in shadow.")]
    [SerializeField] private Material hiddenMaterial;

    private PlayerController playerController;
    private Renderer[] modelRenderers;
    private Material[][] originalMaterials;
    private bool hasBeenRevealed;
    private bool hiddenMaterialApplied;
    private float visibilityTimer;

    public bool IsHiddenFromPlayer => hiddenMaterialApplied;
    public Sprite Icon => icon;

    protected override void Awake()
    {
        base.Awake();

        playerController = FindFirstObjectByType<PlayerController>();

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
    }

    private void UpdateVisibility()
    {
        if (hasBeenRevealed) return;

        if (IsInLight() || IsPlayerClose())
        {
            Reveal();
            return;
        }

        if (!hiddenMaterialApplied)
        {
            ApplyHiddenMaterial();
        }
    }

    public void Reveal()
    {
        if (hasBeenRevealed) return;
        hasBeenRevealed = true;
        ApplyOriginalMaterials();
    }

    private bool IsInLight()
    {
        if (LightDetectionManager.Instance == null) return true;
        return LightDetectionManager.Instance.IsPointInLight(transform.position + lightCheckOffset);
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
            visionCone.OnPlayerDetected -= OnPlayerEnteredVisionCone;
    }
}