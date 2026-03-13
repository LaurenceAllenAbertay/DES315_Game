using UnityEngine;
using UnityEngine.UI;

public class HealthOrbUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Image blockFillImage;
    [SerializeField] private Player player;

    private float cachedMaxHealth = 1f;

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<Player>();
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.OnHealthChanged += HandleHealthChanged;
            player.OnBlockChanged += HandleBlockChanged;
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.OnHealthChanged -= HandleHealthChanged;
            player.OnBlockChanged -= HandleBlockChanged;
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (fillImage != null && max > 0f)
            fillImage.fillAmount = current / max;

        cachedMaxHealth = max;
    }

    private void HandleBlockChanged(float current)
    {
        if (blockFillImage != null && cachedMaxHealth > 0f)
            blockFillImage.fillAmount = current / cachedMaxHealth;
    }
}