using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//UI manager for treasure chest item selection, shows 3 cards with items or abilities and lets player pick one -EM//
public class TreasureChestUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Root GameObject for the entrie UI (will be shown/hidden)")]
    public GameObject uiRoot;

    [Tooltip("Parent transform where card buttons will be spawned")]
    public Transform cardContainer;

    [Tooltip("Prefab for individual item card")]
    public TreasureChestCard cardPrefab;

    [Header("Replace Prompt")]
    [Tooltip("Optional label ot show 'Choose an ability to replace'")]
    public GameObject replacePromptLabel;

    [Header("Animation")]
    [Tooltip("Delay between spawning each card (for staggered animation)")]
    public float cardSpawnDelay = 0.1f;

    [Header("Debug")]
    public bool debugMode = true;

    private List<TreasureChestCard> spawnedCards = new List<TreasureChestCard>();
    private Action<ChestReward> onRewardSelectedCallBack;
    private Action<int> onReplaceSlotSelectedCallBack;

    private void Awake()
    {
        //hide UI by default//
        if(uiRoot != null)
        {
            uiRoot.SetActive(false);
        }

        if(replacePromptLabel != null) 
        {
            replacePromptLabel.SetActive(false);
        }
    }

    //Show the reward selection UI with a mixed list of items and abilities -EM//
    public void ShowRewards(List<ChestReward> rewards, Action<ChestReward> onRewardSelected)
    {
        if(rewards == null || rewards.Count == 0)
        {
            Debug.LogWarning("[TreasureChestUI] No Rewards to display!");
            return;
        }

        if(cardPrefab == null || cardContainer == null)
        {
            Debug.LogError("[TreasureChestUI] Card prefab or container not assigned!");
        }

        StopAllCoroutines();

        onRewardSelectedCallBack = onRewardSelected;
        onReplaceSlotSelectedCallBack = null;

        //Clear any existing cards//
        ClearCards();

        //Show UI//
        if(uiRoot != null)
        {
            uiRoot.SetActive(true);
        }

        //Spawn cards with staggered animation//
        StartCoroutine(SpawnCardsCoroutine(rewards));
    }

    //Show the replace slot UI when ability slots are full -EM//
    public void ShowReplacePrompt(Ability[] equippedAbilities, Action<int> onSlotSelected)
    {
        if(equippedAbilities == null || equippedAbilities.Length == 0)
        {
            Debug.LogWarning("[TreasureChestUI] No equipped abilities to replace!");
            return;
        }

        StopAllCoroutines();

        onReplaceSlotSelectedCallBack = onSlotSelected;
        onRewardSelectedCallBack = null;

        ClearCards();

        if(replacePromptLabel != null) replacePromptLabel.SetActive(true);
        if (uiRoot != null) uiRoot.SetActive(true);

        StartCoroutine(SpawnReplaceCardsCoroutine(equippedAbilities));
    }

    private IEnumerator SpawnReplaceCardsCoroutine(Ability[] equippedAbilities)
    {
        if (debugMode) Debug.Log($"[TreasureChestUI] Showing {equippedAbilities.Length} equipped abilities for replacement");

        for(int i = 0; i < equippedAbilities.Length; i++)
        {
            Ability ability = equippedAbilities[i];
            if (ability == null) continue;

            ChestReward reward = new ChestReward
            {
                type = ChestRewardType.Ability,
                ability = ability
            };

            int slotIndex = i; //Capture for lambda//
            TreasureChestCard card = Instantiate(cardPrefab, cardContainer);
            card.Setup(reward, (c) => OnReplaceCardClicked(slotIndex));
            spawnedCards.Add(card);

            if(i < equippedAbilities.Length - 1)
            {
                yield return new WaitForSeconds(cardSpawnDelay);
            }
        }
    }

    //Called when player clicks a card during normal reward selection -EM//
    private void OnCardClicked(TreasureChestCard card)
    {
        onRewardSelectedCallBack?.Invoke(card.Reward);
        Hide();
    }

    //Called when player clicks a card during normal reward selection -EM//
    private void OnReplaceCardClicked(int slotIndex)
    {
        if (debugMode) Debug.Log($"[TreasureChestUI] Player chose to replace slot {slotIndex}");
        onReplaceSlotSelectedCallBack?.Invoke(slotIndex);
        Hide();
    }

    //Spawn cards one by one with a delay -EM//
    private IEnumerator SpawnCardsCoroutine(List<ChestReward> rewards)
    {
        if (debugMode) Debug.Log($"[TreasureChestUI] Starting to spawn {rewards.Count} cards");

        for (int i = 0; i < rewards.Count; i++)
        {
            ChestReward reward  = rewards[i];

            TreasureChestCard card = Instantiate(cardPrefab, cardContainer);
            card.Setup(reward, OnCardClicked);
            spawnedCards.Add(card);

            // Wait before spawning next card
            if (i < rewards.Count - 1)
            {
                yield return new WaitForSeconds(cardSpawnDelay);
            }
        }
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
