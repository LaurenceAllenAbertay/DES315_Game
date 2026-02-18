using System.Collections.Generic;
using UnityEngine;

public class CombatCarouselUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Transform entryContainer;
    [SerializeField] private CombatCarouselEntry entryPrefab;

    private CombatManager combatManager;
    private readonly List<CombatCarouselEntry> entries = new List<CombatCarouselEntry>();

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (entryContainer == null)
        {
            entryContainer = transform;
        }

        if (root == gameObject)
        {
            Debug.LogWarning("[CombatCarouselUI] Root is set to the same GameObject as this script. " +
                             "Disable/enable will also disable this script, so assign a child root instead.");
        }

        SetRootActive(false);
    }

    private void OnEnable()
    {
        combatManager = CombatManager.Instance;
        if (combatManager != null)
        {
            combatManager.OnCombatStarted += HandleCombatStarted;
            combatManager.OnCombatEnded += HandleCombatEnded;
            combatManager.OnTurnStarted += HandleTurnStarted;
            combatManager.OnTurnEnded += HandleTurnEnded;

            if (combatManager.InCombat)
            {
                BuildCarousel(combatManager.TurnOrder);
                SetRootActive(true);
            }
        }
    }

    private void OnDisable()
    {
        if (combatManager != null)
        {
            combatManager.OnCombatStarted -= HandleCombatStarted;
            combatManager.OnCombatEnded -= HandleCombatEnded;
            combatManager.OnTurnStarted -= HandleTurnStarted;
            combatManager.OnTurnEnded -= HandleTurnEnded;
        }

        ClearEntries();
    }

    private void HandleCombatStarted(List<Enemy> enemies)
    {
        if (combatManager == null)
        {
            combatManager = CombatManager.Instance;
        }

        if (combatManager == null)
        {
            return;
        }

        BuildCarousel(combatManager.TurnOrder);
        SetRootActive(true);
    }

    private void HandleTurnStarted(Unit unit)
    {
        CleanupNullEntries();
        SetCurrentTurnIndicator(unit, true);
    }

    private void HandleTurnEnded(Unit unit)
    {
        CleanupNullEntries();
        SetCurrentTurnIndicator(unit, false);
    }

    private void HandleCombatEnded(CombatManager.CombatOutcome outcome)
    {
        ClearEntries();
        SetRootActive(false);
    }

    private void BuildCarousel(List<Unit> order)
    {
        ClearEntries();

        if (order == null || entryPrefab == null || entryContainer == null)
        {
            return;
        }

        foreach (Unit unit in order)
        {
            if (unit == null)
            {
                continue;
            }

            CombatCarouselEntry entry = Instantiate(entryPrefab, entryContainer);
            entry.Initialize(unit);
            entries.Add(entry);
        }

        SetCurrentTurnIndicator(combatManager != null ? combatManager.CurrentUnit : null, true);
    }

    private void ClearEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            CombatCarouselEntry entry = entries[i];
            if (entry != null)
            {
                Destroy(entry.gameObject);
            }
        }

        entries.Clear();
    }

    private void CleanupNullEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] == null)
            {
                entries.RemoveAt(i);
            }
        }
    }

    private void SetCurrentTurnIndicator(Unit unit, bool isActive)
    {
        if (unit == null)
        {
            return;
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            CombatCarouselEntry entry = entries[i];
            if (entry == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            if (entry.Unit == unit)
            {
                entry.SetTurnIndicatorActive(isActive);
                return;
            }
        }
    }

    private void SetRootActive(bool isActive)
    {
        if (root != null)
        {
            root.SetActive(isActive);
        }
    }
}
