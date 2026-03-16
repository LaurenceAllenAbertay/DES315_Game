using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//Individual card UI elelement for item selection, shows item icon, name and description -EM//
public class TreasureChestCard : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Visual Feedback")]
    [Tooltip("Optional: tint or bage to distinguish ability cards from item cards")]
    public GameObject abilityBadge;
    [Tooltip("Optional: Object to enable when hovering")]
    public GameObject hoverHighlight;

    private ChestReward reward;
    private Action<TreasureChestCard> onClickedCallback;

    public ChestReward Reward => reward;

    public ItemDefinition Item => reward.item;

    private void Awake()
    {
        //Get Button if not assigned//
        if(button == null)
        {
            button = GetComponent<Button>();
        }

        //Setup button listener//
        if(button != null)
        {
            button.onClick.AddListener(OnClicked);
        }

        //Hide hover highlight by default//
        if(hoverHighlight != null)
        {
            hoverHighlight.SetActive(false);
        }

        if(abilityBadge != null)
        {
            abilityBadge.SetActive(false);
        }
    }

    //Setup this card with a chestReward (item or ability) -EM//
    public void Setup(ChestReward chestReward, Action<TreasureChestCard> onClicked)
    {
        reward = chestReward;
        onClickedCallback = onClicked;

        if(reward.type == ChestRewardType.Item && reward.item != null)
        {
            if (iconImage != null) iconImage.sprite = reward.item.icon;
            if (nameText != null) nameText.text = reward.item.itemName;
            if (descriptionText != null) descriptionText.text = reward.item.description;
            if (abilityBadge != null) abilityBadge.SetActive(false);
        }
        else if(reward.type == ChestRewardType.Ability && reward.ability != null)
        {
            if (iconImage != null) iconImage.sprite = reward.ability.icon;
            if (nameText != null) nameText.text = reward.ability.abilityName;
            if (descriptionText != null) descriptionText.text = reward.ability.description;
            if (abilityBadge != null) abilityBadge.SetActive(true);
        }
        else 
        {
            Debug.LogWarning("[TreasureChestCard] Reward data is null or type mismatch");
        }
    }

    //Called when this card is clicked -EM//
    private void OnClicked()
    {
        onClickedCallback?.Invoke(this);
    }

    //Call from UI for hover effects -EM//
    public void OnPointerEnter()
    {
        if(hoverHighlight != null)
        {
            hoverHighlight.SetActive(true);
        }
    }

    //Call from UI for hover effects -EM//
    public void OnPointerExit()
    {
        if(hoverHighlight != null)
        {
            hoverHighlight.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if(button != null)
        {
            button.onClick.RemoveListener(OnClicked);
        }
    }
}
