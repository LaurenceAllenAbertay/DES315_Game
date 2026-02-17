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
    [SerializeField] private Slider blockSlider;
    [SerializeField] private TextMeshProUGUI blockAmountText;

    [Header("Raycast")]
    [SerializeField] private LayerMask unitLayer = ~0;
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private bool ignorePlayer = true;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    private InputAction pointerPositionAction;
    private Camera mainCamera;
    private Unit hoveredUnit;
    private Unit lockedUnit;
    private bool lockToTurnUnit;
    private CombatManager combatManager;

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
        combatManager = CombatManager.Instance;
        if (combatManager != null)
        {
            combatManager.OnTurnStarted += HandleTurnStarted;
            combatManager.OnCombatEnded += HandleCombatEnded;

            if (combatManager.InCombat && combatManager.CurrentUnit is Enemy)
            {
                LockToTurnUnit(combatManager.CurrentUnit);
            }
        }

        if (pointerPositionAction != null)
        {
            pointerPositionAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (combatManager != null)
        {
            combatManager.OnTurnStarted -= HandleTurnStarted;
            combatManager.OnCombatEnded -= HandleCombatEnded;
        }

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

        if (lockToTurnUnit)
        {
            if (lockedUnit == null || lockedUnit.IsDead)
            {
                UnlockTurnUnit();
            }
            else
            {
                if (hoveredUnit != lockedUnit)
                {
                    SetHoveredUnit(lockedUnit);
                }
                return;
            }
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
                if (IsUnitHiddenFromPlayer(unit))
                {
                    ClearHoveredUnit();
                    return;
                }

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
        hoveredUnit.OnBlockChanged += HandleBlockChanged;
        UpdateUI(hoveredUnit.CurrentHealth, hoveredUnit.MaxHealth);
        UpdateBlockUI(hoveredUnit.CurrentBlock, hoveredUnit.MaxHealth);
        SetUIActive(true);
    }

    private void ClearHoveredUnit()
    {
        if (hoveredUnit != null)
        {
            hoveredUnit.OnHealthChanged -= HandleHealthChanged;
            hoveredUnit.OnBlockChanged -= HandleBlockChanged;
        }

        hoveredUnit = null;
        SetUIActive(false);
    }

    private void LockToTurnUnit(Unit unit)
    {
        if (unit == null)
        {
            return;
        }

        lockToTurnUnit = true;
        lockedUnit = unit;
        SetHoveredUnit(unit);
    }

    private void UnlockTurnUnit()
    {
        lockToTurnUnit = false;
        lockedUnit = null;
        ClearHoveredUnit();
    }

    private void HandleTurnStarted(Unit unit)
    {
        if (unit is Enemy)
        {
            LockToTurnUnit(unit);
        }
        else
        {
            UnlockTurnUnit();
        }
    }

    private void HandleCombatEnded(CombatManager.CombatOutcome outcome)
    {
        UnlockTurnUnit();
    }

    private void HandleHealthChanged(float current, float max)
    {
        UpdateUI(current, max);
        if (hoveredUnit != null)
        {
            UpdateBlockUI(hoveredUnit.CurrentBlock, max);
        }
    }

    private void HandleBlockChanged(float current)
    {
        if (hoveredUnit == null)
        {
            return;
        }

        UpdateBlockUI(current, hoveredUnit.MaxHealth);
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

    private void UpdateBlockUI(float current, float max)
    {
        if (blockSlider != null)
        {
            blockSlider.maxValue = max;
            blockSlider.value = current;
        }

        if (blockAmountText != null)
        {
            int currentValue = Mathf.RoundToInt(current);
            blockAmountText.text = $"{currentValue}";
        }

        bool hasBlock = current > 0f;
        if (blockSlider != null)
        {
            blockSlider.gameObject.SetActive(hasBlock);
        }
    }

    private void SetUIActive(bool isActive)
    {
        if (root != null)
        {
            root.SetActive(isActive);
        }
    }

    private bool IsUnitHiddenFromPlayer(Unit unit)
    {
        if (unit == null) return false;
        Enemy enemy = unit.GetComponent<Enemy>();
        return enemy != null && enemy.IsHiddenFromPlayer;
    }
}
