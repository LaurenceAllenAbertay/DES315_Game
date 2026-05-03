using UnityEngine;

/// <summary>
/// Combat log implementation of the message system.
/// </summary>
public class MessageUI : MonoBehaviour
{
    public static MessageUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("The VerticalLayoutGroup GameObject that entries are parented to.")]
    [SerializeField] private Transform logContainer;

    [Tooltip("Prefab that has a CombatLogEntry component and a TextMeshProUGUI child.")]
    [SerializeField] private CombatLogEntry entryPrefab;

    [Header("Timing (applied to every spawned entry)")]
    [Tooltip("Seconds an entry stays fully visible before fading.")]
    [SerializeField] private float displayDuration = 3f;

    [Tooltip("Seconds the alpha lerp takes to reach zero.")]
    [SerializeField] private float fadeDuration = 1f;

    [Header("Capacity")]
    [Tooltip("Maximum number of entries visible at once. Oldest entry is destroyed when the limit is exceeded. 0 = unlimited.")]
    [SerializeField] private int maxEntries = 8;

    // Tracks live entries so we can enforce the cap
    private readonly System.Collections.Generic.Queue<CombatLogEntry> activeEntries =
        new System.Collections.Generic.Queue<CombatLogEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void EnqueueMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (entryPrefab == null || logContainer == null)
        {
            Debug.LogWarning("[MessageUI] entryPrefab or logContainer is not assigned.");
            return;
        }

        // Enforce the entry cap
        if (maxEntries > 0)
        {
            while (activeEntries.Count >= maxEntries)
            {
                CombatLogEntry oldest = activeEntries.Dequeue();
                if (oldest != null)
                    Destroy(oldest.gameObject);
            }
        }

        // Spawn the new entry
        CombatLogEntry entry = Instantiate(entryPrefab, logContainer);
        entry.Initialize(message, displayDuration, fadeDuration);

        // Track so we can prune if needed
        activeEntries.Enqueue(entry);

        // Also clean up any entries that have already destroyed themselves
        // (harmless null check keeps the queue tidy)
        while (activeEntries.Count > 0 && activeEntries.Peek() == null)
            activeEntries.Dequeue();
    }

    private void OnDisable()
    {
        // Destroy any remaining entries when the UI is disabled / scene changes
        foreach (CombatLogEntry entry in activeEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }

        activeEntries.Clear();
    }
}