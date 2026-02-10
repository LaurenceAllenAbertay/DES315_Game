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
    [Tooltip("Seconds between coin fill steps.")]
    public float fillStepDelay = 0.05f;

    private int currentUICoins = 0;
    private Coroutine fillCoroutine;

    private void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player>();
        }

        if (player != null)
        {
            player.OnCoinsChanged += OnCoinsChanged;
            player.OnCombatStateChanged += OnCombatStateChanged;
            SetCoinUIVisible(player.IsInCombat);
            if (player.IsInCombat)
            {
                RefreshCoins(player.CurrentCoins);
            }
            else
            {
                ClearCoins();
            }
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.OnCoinsChanged -= OnCoinsChanged;
            player.OnCombatStateChanged -= OnCombatStateChanged;
        }
    }

    private void OnCoinsChanged(int current, int max)
    {
        RefreshCoins(current);
    }

    private void OnCombatStateChanged(bool inCombat)
    {
        SetCoinUIVisible(inCombat);
        if (inCombat)
        {
            RefreshCoins(player != null ? player.CurrentCoins : 0);
        }
        else
        {
            ClearCoins();
        }
    }

    private void SetCoinUIVisible(bool visible)
    {
        if (coinParent != null)
        {
            coinParent.gameObject.SetActive(visible);
        }
    }

    private void ClearCoins()
    {
        if (coinParent == null) return;
        int childCount = coinParent.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Destroy(coinParent.GetChild(i).gameObject);
        }

        currentUICoins = 0;
    }

    private void RefreshCoins(int count)
    {
        if (coinPrefab == null || coinParent == null) return;

        if (fillCoroutine != null)
        {
            StopCoroutine(fillCoroutine);
            fillCoroutine = null;
        }

        // Add coins if we need more
        if (currentUICoins < count)
        {
            fillCoroutine = StartCoroutine(FillCoins(currentUICoins, count));
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

    private IEnumerator FillCoins(int startCount, int targetCount)
    {
        currentUICoins = startCount;

        while (currentUICoins < targetCount)
        {
            Instantiate(coinPrefab, coinParent);
            currentUICoins++;

            if (fillStepDelay > 0f)
            {
                yield return new WaitForSeconds(fillStepDelay);
            }
            else
            {
                yield return null;
            }
        }

        fillCoroutine = null;
    }
}
