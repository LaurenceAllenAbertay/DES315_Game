using UnityEngine;

/// <summary>
/// Enemy-specific functionality built on top of Unit base class
/// Handles vision cones and enemy-specific mechanics
/// </summary>
public class Enemy : Unit
{
    [Header("Vision Cone")]
    public EnemyVisionCone visionCone;

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
    }

    private void OnPlayerEnteredVisionCone()
    {
        // TODO: Add combat start logic here
        Debug.Log($"[Enemy] {gameObject.name} detected the player!");
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
    }
}
