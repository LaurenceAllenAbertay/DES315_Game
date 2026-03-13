using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private TutorialDatabase database;

    [Header("UI References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI messageText;

    private readonly HashSet<string> _shownThisRun = new(System.StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panel != null) panel.SetActive(false);
    }

    private void Start()
    {
        Trigger("game_start");
    }

    /// <summary>
    /// Show the tutorial popup for the given trigger key and pause the game until closed.
    /// Does nothing if the key was already shown this run, is missing from the database, or required UI references are unassigned.
    /// </summary>
    public void Trigger(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (_shownThisRun.Contains(key)) return;
        if (database == null)
        {
            Debug.LogWarning("[TutorialManager] No TutorialDatabase assigned.");
            return;
        }
        if (!database.TryGetMessage(key, out string message))
        {
            Debug.LogWarning($"[TutorialManager] Trigger key not found in database: '{key}'");
            return;
        }
        if (panel == null || messageText == null)
        {
            Debug.LogWarning("[TutorialManager] Panel or MessageText reference is missing.");
            return;
        }

        _shownThisRun.Add(key);
        messageText.text = message;
        panel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
    }
}