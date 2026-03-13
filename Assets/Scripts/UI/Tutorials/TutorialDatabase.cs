using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialDatabase", menuName = "Tutorial/Tutorial Database")]
public class TutorialDatabase : ScriptableObject
{
    [Serializable]
    public class TutorialEntry
    {
        public string triggerKey;
        [TextArea(2, 6)]
        public string message;
    }

    [SerializeField] private List<TutorialEntry> entries = new();

    private Dictionary<string, string> _lookup;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (TutorialEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.triggerKey)) continue;
            if (!_lookup.TryAdd(entry.triggerKey, entry.message))
                Debug.LogWarning($"[TutorialDatabase] Duplicate trigger key: '{entry.triggerKey}'");
        }
    }

    public bool TryGetMessage(string key, out string message)
    {
        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(key, out message);
    }
}