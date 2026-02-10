using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

//UI manager for treasure chest item selection, shows 3 cards with items and lets player pick one -EM//
public class TreasureChestUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Root GameObject for the entrie UI (will be shown/hidden)")]
    public GameObject uiRoot;

    [Tooltip("Parent transform where card buttons will be spawned")]
    public Transform cardContainer;

    [Tooltip("Prefab for individual item card")]
    public TreasureChestCard cardPrefab;

    [Header("Animation")]
    [Tooltip("Delay between spawning each card (for staggered animation)")]
    public float cardSpawnDelay = 0.1f;

    [Header("Debug")]
    public bool debugMode = true;

    private List<TreasureChestCard> spawnedCards = new List<TreasureChestCard>();
    private Action<ItemDefinition> onItemSelectedCallBack;

    private void Awake()
    {
        //hide UI by default//
        if(uiRoot != null)
        {
            uiRoot.SetActive(false);
        }
    }

    //Show the item selection UI with given Items -EM//
    public void ShowItemSelection(List<ItemDefinition> items, Action<ItemDefinition> onItemSelected)
    {
        if(items == null || items.Count == 0)
        {
            Debug.LogWarning("[TreasureChestUI] No Items to display!");
            return;
        }

        if(cardPrefab == null || cardContainer == null)
        {
            Debug.LogError("[TreasureChestUI] Card prefab or container not assigned!");
        }

        onItemSelectedCallBack = onItemSelected;

        //Clear any existing cards//
        ClearCards();

        //Show UI//
        if(uiRoot != null)
        {
            uiRoot.SetActive(true);
        }

        //Spawn cards with staggered animation//
        StartCoroutine(SpawnCardsCoroutine(items));
    }

    //Spawn cards one by one with a delay -EM//
    private System.Collections.IEnumerator SpawnCardsCoroutine(List<ItemDefinition> items)
    {
        if (debugMode) Debug.Log($"[TreasureChestUI] Starting to spawn {items.Count} cards");

        for (int i = 0; i < items.Count; i++)
        {
            if (debugMode) Debug.Log($"[TreasureChestUI] Spawning card {i + 1}...");

            ItemDefinition item = items[i];

            if (item == null)
            {
                Debug.LogWarning($"[TreasureChestUI] Item {i} is null, skipping");
                continue;
            }

            if (cardPrefab == null)
            {
                Debug.LogError("[TreasureChestUI] Card prefab became null!");
                yield break;
            }

            if (cardContainer == null)
            {
                Debug.LogError("[TreasureChestUI] Card container became null!");
                yield break;
            }

            // Instantiate card
            if (debugMode) Debug.Log($"[TreasureChestUI] Instantiating card for item: {item.itemName}");
            TreasureChestCard card = Instantiate(cardPrefab, cardContainer);

            if (debugMode) Debug.Log($"[TreasureChestUI] Setting up card...");
            card.Setup(item, OnCardClicked);

            if (debugMode) Debug.Log($"[TreasureChestUI] Adding card to list");
            spawnedCards.Add(card);

            // Wait before spawning next card
            if (i < items.Count - 1)
            {
                yield return new WaitForSeconds(cardSpawnDelay);
            }
        }

        if (debugMode)
        {
            Debug.Log($"[TreasureChestUI] Finished spawning {spawnedCards.Count} cards");
        }
    }

    //Called when player clicks on a card -EM//
    private void OnCardClicked(TreasureChestCard card)
    {
        if(card == null || card.Item == null)
        {
            return;
        }

        if(debugMode)
        {
            Debug.Log($"[TreasureChestUI] Card Clicked: {card.Item.itemName}");
        }

        //Notify callback//
        onItemSelectedCallBack?.Invoke(card.Item);

        //Hide UI//
        Hide();
    }

    //Hide the item selection UI -EM//
    public void Hide()
    {
        if(uiRoot != null)
        {
            uiRoot.SetActive(false);
        }

        ClearCards();
    }

    //Clear all spawned cards -EM//
    private void ClearCards()
    {
        foreach(var card in spawnedCards)
        {
            if(card != null)
            {
                Destroy(card.gameObject);
            }
        }

        spawnedCards.Clear();
    }
}
