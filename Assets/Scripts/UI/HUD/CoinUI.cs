using System.Collections;
using UnityEngine;

/// <summary>
/// Simple UI that shows coins
/// </summary>
public class CoinUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The coin prefab to instantiate")]
    public GameObject coinPrefab;
    
    [Tooltip("Parent transform")]
    public Transform coinParent;
    
    [Tooltip("Reference to the player")]
    public Player player;

    [Header("Animation")]
    public float destroyDelay = 1f;

    private int currentUICoins = 0;

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

        // Add coins if we need more
        while (currentUICoins < count)
        {
            Instantiate(coinPrefab, coinParent);
            currentUICoins++;
        }

        // Remove coins if we have too many
        while (currentUICoins > count)
        {
            currentUICoins--;
            
            if (coinParent.childCount > currentUICoins)
            {
                GameObject coin = coinParent.GetChild(currentUICoins).gameObject;
                StartCoroutine(SpendCoin(coin));
            }
        }
    }

    private IEnumerator SpendCoin(GameObject coin)
    {
        Animator animator = coin.GetComponent<Animator>();
        
        if (animator != null)
        {
            animator.SetTrigger("CoinSpent");
            yield return new WaitForSeconds(destroyDelay);
        }
        
        Destroy(coin);
    }
}