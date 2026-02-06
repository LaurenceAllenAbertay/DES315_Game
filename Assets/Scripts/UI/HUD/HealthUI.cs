using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI unitNameText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider healthSlider;

    [Header("Raycast")]
    [SerializeField] private LayerMask unitLayer = ~0;
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private bool ignorePlayer = true;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    private InputAction pointerPositionAction;
    private Camera mainCamera;
    private Unit hoveredUnit;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (root == null)
        {
            root = gameObject;
        }

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            pointerPositionAction = playerMap.FindAction("PointerPosition");
        }

        SetUIActive(false);
    }

    private void OnEnable()
    {
        if (pointerPositionAction != null)
        {
            pointerPositionAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (pointerPositionAction != null)
        {
            pointerPositionAction.Disable();
        }

        ClearHoveredUnit();
    }

    private void Update()
    {
        if (pointerPositionAction == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        Vector2 pointerPos = pointerPositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(pointerPos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, unitLayer, QueryTriggerInteraction.Ignore))
        {
            Unit unit = hit.collider.GetComponentInParent<Unit>();
            if (unit != null && (!ignorePlayer || unit.GetComponent<Player>() == null))
            {
                if (unit != hoveredUnit)
                {
                    SetHoveredUnit(unit);
                }

                return;
            }
        }

        ClearHoveredUnit();
    }

    private void SetHoveredUnit(Unit unit)
    {
        ClearHoveredUnit();
        hoveredUnit = unit;

        hoveredUnit.OnHealthChanged += HandleHealthChanged;
        UpdateUI(hoveredUnit.CurrentHealth, hoveredUnit.MaxHealth);
        SetUIActive(true);
    }

    private void ClearHoveredUnit()
    {
        if (hoveredUnit != null)
        {
            hoveredUnit.OnHealthChanged -= HandleHealthChanged;
        }

        hoveredUnit = null;
        SetUIActive(false);
    }

    private void HandleHealthChanged(float current, float max)
    {
        UpdateUI(current, max);
    }

    private void UpdateUI(float current, float max)
    {
        if (unitNameText != null && hoveredUnit != null)
        {
            unitNameText.text = hoveredUnit.gameObject.name;
        }

        if (healthText != null)
        {
            int currentValue = Mathf.RoundToInt(current);
            int maxValue = Mathf.RoundToInt(max);
            healthText.text = $"{currentValue}/{maxValue}";
        }

        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }
    }

    private void SetUIActive(bool isActive)
    {
        if (root != null)
        {
            root.SetActive(isActive);
        }
    }
}
