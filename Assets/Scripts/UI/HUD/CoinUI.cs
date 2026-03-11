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

    private const string CoinSpendingParam = "CoinSpending";
    private int currentUICoins = 0;
    private Coroutine fillCoroutine;
    private int spendingCoinCount = 0;

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
            SetCoinUIVisible(false);
        }

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnTurnStarted += OnTurnStarted;
            CombatManager.Instance.OnTurnEnded += OnTurnEnded;
            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.OnCoinsChanged -= OnCoinsChanged;
            player.OnCombatStateChanged -= OnCombatStateChanged;
        }

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnTurnStarted -= OnTurnStarted;
            CombatManager.Instance.OnTurnEnded -= OnTurnEnded;
            CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
        }
    }

    private void OnCoinsChanged(int current, int max)
    {
        RefreshCoins(current);
    }

    private void OnCombatStateChanged(bool inCombat)
    {
        if (!inCombat)
        {
            SetCoinUIVisible(false);
            ClearCoins();
        }
    }

    private void OnTurnStarted(Unit unit)
    {
        if (CombatManager.Instance == null || !CombatManager.Instance.IsPlayerTurn) return;
        SetCoinUIVisible(true);
        RefreshCoins(player != null ? player.CurrentCoins : 0);
    }

    private void OnTurnEnded(Unit unit) => SetCoinUIVisible(false);

    private void OnCombatEnded(CombatManager.CombatOutcome outcome)
    {
        SetCoinUIVisible(false);
        ClearCoins();
    }

    private void SetCoinUIVisible(bool visible)
    {
        if (coinParent != null)
        {
            coinParent.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            spendingCoinCount = 0;
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

        ApplyCoinSpending();
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
            GameObject coin = Instantiate(coinPrefab, coinParent);
            currentUICoins++;
            ApplyCoinSpendingToCoin(coin, coinParent.childCount - 1, coinParent.childCount);

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
        ApplyCoinSpending();
    }

    public void SetCoinSpendingCount(int count)
    {
        spendingCoinCount = Mathf.Max(0, count);

        ApplyCoinSpending();
    }

    private void ApplyCoinSpending()
    {
        if (coinParent == null) return;

        int childCount = coinParent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            ApplyCoinSpendingToCoin(coinParent.GetChild(i).gameObject, i, childCount);
        }
    }

    private void ApplyCoinSpendingToCoin(GameObject coin, int index, int totalCoins)
    {
        Animator animator = coin.GetComponent<Animator>();
        if (animator == null) return;

        int activeCount = Mathf.Min(spendingCoinCount, totalCoins);
        bool isSpending = index >= totalCoins - activeCount;
        animator.SetBool(CoinSpendingParam, isSpending);
    }

}