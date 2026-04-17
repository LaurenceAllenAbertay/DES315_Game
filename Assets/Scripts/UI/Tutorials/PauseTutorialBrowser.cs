using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PauseTutorialBrowser : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private TutorialDatabase database;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI counterText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    private int _currentIndex;
    private int _count;

    private void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(ShowPrevious);
        if (nextButton != null) nextButton.onClick.AddListener(ShowNext);
    }

    private void OnEnable()
    {
        _count = database != null ? database.GetAllEntries().Count : 0;
        _currentIndex = 0;
        Refresh();
    }

    private void ShowPrevious()
    {
        if (_count == 0) return;
        _currentIndex = (_currentIndex - 1 + _count) % _count;
        Refresh();
    }

    private void ShowNext()
    {
        if (_count == 0) return;
        _currentIndex = (_currentIndex + 1) % _count;
        Refresh();
    }

    private void Refresh()
    {
        if (_count == 0)
        {
            if (messageText != null) messageText.text = "No tutorials available.";
            if (counterText != null) counterText.text = "0/0";
            return;
        }

        var entry = database.GetAllEntries()[_currentIndex];
        if (messageText != null) messageText.text = entry.message;
        if (counterText != null) counterText.text = $"{_currentIndex + 1}/{_count}";
    }
}