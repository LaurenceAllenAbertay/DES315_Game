using UnityEngine;
using UnityEngine.UI;

public class HealthOrbUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Player player;

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<Player>();
    }

    private void OnEnable()
    {
        if (player != null)
            player.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (player != null)
            player.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (fillImage != null && max > 0f)
            fillImage.fillAmount = current / max;
    }
}