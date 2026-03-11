using UnityEngine;
using UnityEngine.UI;

public class EndTurnButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    private void Start()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnTurnStarted += OnTurnStarted;
            CombatManager.Instance.OnTurnEnded += OnTurnEnded;
            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
        }

        button.onClick.AddListener(OnClicked);
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnTurnStarted -= OnTurnStarted;
            CombatManager.Instance.OnTurnEnded -= OnTurnEnded;
            CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
        }
    }

    private void OnTurnStarted(Unit unit) => SetVisible(CombatManager.Instance.IsPlayerTurn);
    private void OnTurnEnded(Unit unit) => SetVisible(false);
    private void OnCombatEnded(CombatManager.CombatOutcome outcome) => SetVisible(false);

    private void OnClicked()
    {
        if (CombatManager.Instance != null && CombatManager.Instance.IsPlayerTurn)
            CombatManager.Instance.EndCurrentTurn();
    }

    private void SetVisible(bool visible) => gameObject.SetActive(visible);
}