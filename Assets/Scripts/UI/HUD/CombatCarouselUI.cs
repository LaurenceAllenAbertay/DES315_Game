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
        if (root == null || root == gameObject)
        {
            Debug.LogError("[CombatCarouselUI] Root must be assigned to a child GameObject, not this object itself. Carousel will not function.");
            root = null;
            return;
        }

        if (entryContainer == null)
        {
            entryContainer = transform;
        }

        SetRootActive(false);
    }

    private void Start()
    {
        combatManager = CombatManager.Instance;
        if (combatManager != null)
        {
            combatManager.OnCombatStarted += HandleCombatStarted;
            combatManager.OnCombatEnded += HandleCombatEnded;
            combatManager.OnTurnStarted += HandleTurnStarted;
            combatManager.OnTurnEnded += HandleTurnEnded;
            combatManager.OnRoundStarted += HandleRoundStarted;

            if (combatManager.InCombat)
            {
                BuildCarousel(combatManager.TurnOrder);
                SetRootActive(true);
            }
        }
        else
        {
            Debug.LogError("[CombatCarouselUI] CombatManager instance not found.");
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
            combatManager.OnRoundStarted -= HandleRoundStarted;
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
        SetEntryCompleted(unit, true);
    }

    private void HandleRoundStarted(int round)
    {
        CleanupNullEntries();
        foreach (CombatCarouselEntry entry in entries)
        {
            entry.SetCompletedState(false);
        }
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

    private void SetEntryCompleted(Unit unit, bool completed)
    {
        if (unit == null) return;

        foreach (CombatCarouselEntry entry in entries)
        {
            if (entry != null && entry.Unit == unit)
            {
                entry.SetCompletedState(completed);
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