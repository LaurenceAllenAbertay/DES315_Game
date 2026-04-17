using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private TutorialDatabase database;

    [Header("UI References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Toggle tutorialsToggle;

    private readonly HashSet<string> _shownThisRun = new(System.StringComparer.OrdinalIgnoreCase);
    private bool _tutorialsEnabled = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panel != null) panel.SetActive(false);

        if (tutorialsToggle != null)
        {
            _tutorialsEnabled = tutorialsToggle.isOn;
            tutorialsToggle.onValueChanged.AddListener(val => _tutorialsEnabled = val);
        }
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
        if (!_tutorialsEnabled) return;
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
        PauseStack.Push();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        PauseStack.Pop();
    }
}