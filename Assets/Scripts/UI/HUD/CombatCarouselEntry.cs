using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CombatCarouselEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image unitIconImage;
    [SerializeField] private Slider missingHealthSlider;
    [SerializeField] private GameObject currentTurnIndicator;
    [SerializeField] private HealthUI healthUI;

    private CameraController cameraController;

    [Header("Active Turn")]
    [SerializeField] private Vector3 activeTurnScale = new Vector3(1.3f, 1.3f, 1f);

    [Header("Completed Turn")]
    [SerializeField] private Color completedTintColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private static readonly Vector3 DefaultScale = new Vector3(0.8f, 0.8f, 1f);

    private static readonly string ArrowObjectName = "TargetArrow";

    private Unit unit;
    private bool isHovering;
    private GameObject hoverArrow;

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

        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<CameraController>();
        }
        
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
        hoverArrow = null;

        if (unit == null)
        {
            UpdateUI(0f, 1f);
            return;
        }

        Transform arrowTransform = unit.transform.Find(ArrowObjectName);
        if (arrowTransform == null)
        {
            foreach (Transform child in unit.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == ArrowObjectName) { arrowTransform = child; break; }
            }
        }
        hoverArrow = arrowTransform != null ? arrowTransform.gameObject : null;

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

        if (transform.parent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
        }
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
        if (unit == null) return;

        isHovering = true;

        if (healthUI != null)
            healthUI.SetExternalOverride(unit);

        if (hoverArrow != null)
            hoverArrow.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHoverOverride();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (unit == null || cameraController == null) return;
        cameraController.PanToPosition(unit.transform.position);
    }

    private void ClearHoverOverride()
    {
        if (!isHovering)
        {
            return;
        }

        isHovering = false;

        if (hoverArrow != null)
            hoverArrow.SetActive(false);

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