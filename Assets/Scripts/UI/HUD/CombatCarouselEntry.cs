using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CombatCarouselEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image unitIconImage;
    [SerializeField] private Slider missingHealthSlider;
    [SerializeField] private GameObject currentTurnIndicator;
    [SerializeField] private HealthUI healthUI;

    [Header("Active Turn")]
    [SerializeField] private Vector3 activeTurnScale = new Vector3(1.3f, 1.3f, 1f);

    [Header("Completed Turn")]
    [SerializeField] private Color completedTintColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private static readonly Vector3 DefaultScale = new Vector3(0.8f, 0.8f, 1f);

    private Unit unit;
    private bool isHovering;

    private void Awake()
    {
        if (missingHealthSlider != null)
        {
            missingHealthSlider.minValue = 0f;
            missingHealthSlider.value = 0f;
        }

        if (healthUI == null)
        {
            healthUI = FindFirstObjectByType<HealthUI>();
        }

        // Pivot at top-centre so scaling expands downward, away from the screen edge
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.pivot = new Vector2(0.5f, 1f);
        }

        SetTurnIndicatorActive(false);
    }

    private void OnEnable()
    {
        if (unit == null) return;
        Unsubscribe();
        Subscribe();
        UpdateUI(unit.CurrentHealth, unit.MaxHealth);
    }

    private void OnDisable()
    {
        ClearHoverOverride();
        Unsubscribe();
    }

    public void Initialize(Unit targetUnit)
    {
        SetUnit(targetUnit);
    }

    private void SetUnit(Unit targetUnit)
    {
        if (unit == targetUnit)
        {
            return;
        }

        Unsubscribe();
        unit = targetUnit;

        if (unit == null)
        {
            UpdateUI(0f, 1f);
            return;
        }

        Subscribe();
        UpdateUI(unit.CurrentHealth, unit.MaxHealth);
    }

    private void Subscribe()
    {
        if (unit == null)
        {
            return;
        }

        unit.OnHealthChanged += HandleHealthChanged;
        unit.OnDied += HandleUnitDied;
    }

    private void Unsubscribe()
    {
        if (unit == null)
        {
            return;
        }

        unit.OnHealthChanged -= HandleHealthChanged;
        unit.OnDied -= HandleUnitDied;
    }

    private void HandleHealthChanged(float current, float max)
    {
        UpdateUI(current, max);
    }

    private void HandleUnitDied(Unit deadUnit)
    {
        if (deadUnit != unit)
        {
            return;
        }

        ClearHoverOverride();
        Destroy(gameObject);
    }

    private void UpdateUI(float current, float max)
    {
        if (unitIconImage != null)
        {
            Sprite icon = GetUnitIcon(unit);
            unitIconImage.sprite = icon;
            unitIconImage.enabled = icon != null;
        }

        if (missingHealthSlider != null)
        {
            float safeMax = Mathf.Max(1f, max);
            missingHealthSlider.maxValue = safeMax;
            missingHealthSlider.value = Mathf.Clamp(safeMax - current, 0f, safeMax);
        }
    }

    public Unit Unit => unit;

    public void SetTurnIndicatorActive(bool isActive)
    {
        if (currentTurnIndicator != null)
        {
            currentTurnIndicator.SetActive(isActive);
        }

        transform.localScale = isActive ? activeTurnScale : DefaultScale;
    }

    public void SetCompletedState(bool completed)
    {
        if (unitIconImage != null)
        {
            unitIconImage.color = completed ? completedTintColor : Color.white;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (healthUI == null || unit == null)
        {
            return;
        }

        isHovering = true;
        healthUI.SetExternalOverride(unit);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHoverOverride();
    }

    private void ClearHoverOverride()
    {
        if (!isHovering)
        {
            return;
        }

        isHovering = false;

        if (healthUI != null)
        {
            healthUI.ClearExternalOverride(unit);
        }
    }

    private static Sprite GetUnitIcon(Unit targetUnit)
    {
        if (targetUnit == null)
        {
            return null;
        }

        if (targetUnit is Player player)
        {
            return player.Icon;
        }

        if (targetUnit is Enemy enemy)
        {
            return enemy.Icon;
        }

        return null;
    }
}