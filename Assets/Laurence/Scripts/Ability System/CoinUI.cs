using UnityEngine;

/// <summary>
/// Simple UI that shows coin prefabs in a vertical layout.
/// Instantiates coins at turn start, destroys them when spent.
/// </summary>
public class CoinUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The coin prefab to instantiate")]
    public GameObject coinPrefab;
    
    [Tooltip("Parent transform with VerticalLayoutGroup")]
    public Transform coinParent;
    
    [Tooltip("Reference to the player (auto-finds if null)")]
    public Player player;

    private void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (player != null)
        {
            player.OnCoinsChanged += OnCoinsChanged;
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.OnCoinsChanged -= OnCoinsChanged;
        }
    }

    private void OnCoinsChanged(int current, int max)
    {
        RefreshCoins(current);
    }

    private void RefreshCoins(int count)
    {
        if (coinPrefab == null || coinParent == null) return;

        // Get current coin count in UI
        int currentUICoins = coinParent.childCount;

        // Add coins if we need more
        while (currentUICoins < count)
        {
            Instantiate(coinPrefab, coinParent);
            currentUICoins++;
        }

        // Remove coins if we have too many (destroy from end)
        while (currentUICoins > count)
        {
            currentUICoins--;
            Destroy(coinParent.GetChild(currentUICoins).gameObject);
        }
    }
}