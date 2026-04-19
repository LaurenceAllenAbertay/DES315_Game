using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Manages cheat/debug toggles.
/// Attach this to a persistent GameObject in the scene.
/// The UI buttons in your cheat menu should call the public Toggle methods.
/// </summary>
public class CheatManager : MonoBehaviour
{
    public static CheatManager Instance { get; private set; }

    [Header("Cheat State")]
    [SerializeField] private bool infiniteHealth = false;
    [SerializeField] private bool infiniteCoins = false;
    [SerializeField] private bool showFinalRoom = false;
    [SerializeField] private bool ignoreEnemies = false;

    [Header("Final Room Arrow")]
    [Tooltip("Assign the FinalRoomArrow component in the scene")]
    public FinalRoomArrow finalRoomArrow;


    // Events so the UI can update its toggle visuals without polling
    public event System.Action<bool> OnInfiniteHealthChanged;
    public event System.Action<bool> OnInfiniteCoinsChanged;
    public event System.Action<bool> OnShowFinalRoomChanged;
    public event System.Action<bool> OnIgnoreEnemiesChanged;

    // Properties
    public bool InfiniteHealth => infiniteHealth;
    public bool InfiniteCoins => infiniteCoins;
    public bool ShowFinalRoom => showFinalRoom;
    public bool IgnoreEnemies => ignoreEnemies;

    private GameObject activeMarker;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>Toggle infinite health on/off.</summary>
    public void ToggleInfiniteHealth()
    {
        SetInfiniteHealth(!infiniteHealth);
    }

    /// <summary>Set infinite health explicitly.</summary>
    public void SetInfiniteHealth(bool value)
    {
        if (infiniteHealth == value) return;
        infiniteHealth = value;
        Debug.Log($"[CheatManager] Infinite Health: {infiniteHealth}");
        OnInfiniteHealthChanged?.Invoke(infiniteHealth);
    }

    /// <summary>Toggle infinite coins (action points) on/off.</summary>
    public void ToggleInfiniteCoins()
    {
        SetInfiniteCoins(!infiniteCoins);
    }

    /// <summary>Set infinite coins explicitly.</summary>
    public void SetInfiniteCoins(bool value)
    {
        if (infiniteCoins == value) return;
        infiniteCoins = value;
        Debug.Log($"[CheatManager] Infinite Coins: {infiniteCoins}");
        OnInfiniteCoinsChanged?.Invoke(infiniteCoins);
    }

    //Toggle the final room waypoint marker on and off//
    public void ToggleShowFinalRoom()
    {
        SetShowFinalRoom(!showFinalRoom);
    }


    //Toggle the final room waypoint arrow-EM//
    public void SetShowFinalRoom(bool value)
    {
        if (showFinalRoom == value) return;
        showFinalRoom = value;
        Debug.Log($"[CheatManager] Show final room Arrow: {showFinalRoom}");

        if (finalRoomArrow != null) finalRoomArrow.SetVisible(showFinalRoom);
        else Debug.LogWarning("[CheatManager] No FinalRoomArrow assigned in Inspector");

        OnShowFinalRoomChanged?.Invoke(showFinalRoom);
    }

    public void ToggleIgnoreEnemies()
    {
        SetIgnoreEnemies(!ignoreEnemies);
    }

    public void SetIgnoreEnemies(bool value)
    {
        if (ignoreEnemies == value) return;
        ignoreEnemies = value;

        if (ignoreEnemies && CombatManager.Instance != null && CombatManager.Instance.InCombat)
        {
            CombatManager.Instance.ForceEndCombat(CombatManager.CombatOutcome.Draw);
        }

        OnIgnoreEnemiesChanged?.Invoke(ignoreEnemies);
    }
}