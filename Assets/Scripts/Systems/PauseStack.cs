using UnityEngine;

public static class PauseStack
{
    private static int _count = 0;

    public static void Push()
    {
        _count++;
        Time.timeScale = 0f;
    }

    public static void Pop()
    {
        _count = Mathf.Max(0, _count - 1);
        if (_count == 0) Time.timeScale = 1f;
    }

    public static void Reset()
    {
        _count = 0;
        Time.timeScale = 1f;
    }
}