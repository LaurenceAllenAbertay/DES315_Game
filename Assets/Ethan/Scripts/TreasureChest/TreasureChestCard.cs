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
    [Tooltip("Optional: Object to enable when hovering")]
    public GameObject hoverHighlight;

    private ItemDefinition item;
    private Action<TreasureChestCard> onClickedCallback;

    public ItemDefinition Item => item;

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
    }

    //Setup this card with item data -EM//
    public void Setup(ItemDefinition itemDef, Action<TreasureChestCard> onClicked)
    {
        item = itemDef;
        onClickedCallback = onClicked;

        if(item == null)
        {
            Debug.LogWarning("[TreasureChestCard] Item is null");
        }

        //Set Icon//
        if (iconImage != null && item.icon != null)
        {
            iconImage.sprite = item.icon;
        }

        //Set name//
        if(nameText != null)
        {
            nameText.text = item.itemName;
        }

        //Set description//
        if(descriptionText != null)
        {
            descriptionText.text = item.description;
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
