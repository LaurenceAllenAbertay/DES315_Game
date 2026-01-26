using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Vision Cone")]
    public EnemyVisionCone visionCone;

    public delegate void HealthChanged(int current, int max);
    public event HealthChanged OnHealthChanged;

    public delegate void EnemyDied(Enemy enemy);
    public event EnemyDied OnDied;

    private void Awake()
    {
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

    private void Start()
    {
        currentHealth = maxHealth;
    }

    private void OnPlayerEnteredVisionCone()
    {
        // Add your combat start logic here
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"{gameObject.name} took {damage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died!");
        OnDied?.Invoke(this);
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