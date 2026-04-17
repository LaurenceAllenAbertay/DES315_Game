using System;
using UnityEngine;

public static class PauseStack
{
    private static int _count = 0;

    public static bool IsPaused => _count > 0;

    public static event Action<bool> OnPauseStateChanged;

    public static void Push()
    {
        _count++;
        if (_count == 1)
        {
            Time.timeScale = 0f;
            OnPauseStateChanged?.Invoke(true);
        }
    }

    public static void Pop()
    {
        _count = Mathf.Max(0, _count - 1);
        if (_count == 0)
        {
            Time.timeScale = 1f;
            OnPauseStateChanged?.Invoke(false);
        }
    }

    public static void Reset()
    {
        bool wasPaused = _count > 0;
        _count = 0;
        Time.timeScale = 1f;
        if (wasPaused) OnPauseStateChanged?.Invoke(false);
    }
}