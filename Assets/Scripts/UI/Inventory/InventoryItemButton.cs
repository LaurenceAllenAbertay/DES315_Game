using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;

    private ItemDefinition item;
    private string baseName;
    private bool isEquipped;
    private Action<InventoryItemButton> onClicked;

    public ItemDefinition Item => item;
    public bool IsEquipped => isEquipped;

    public void Setup(ItemDefinition itemDefinition, bool equipped, Action<InventoryItemButton> clicked)
    {
        item = itemDefinition;
        onClicked = clicked;
        baseName = itemDefinition != null ? itemDefinition.itemName : string.Empty;

        SetEquipped(equipped);

        if (descriptionText != null)
        {
            descriptionText.text = itemDefinition != null ? itemDefinition.description : string.Empty;
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }

        if (itemDefinition != null && itemDefinition.icon != null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = itemDefinition.icon;
            }
            else if (button != null && button.image != null)
            {
                button.image.sprite = itemDefinition.icon;
            }
        }
    }

    public void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
        if (nameText != null)
        {
            nameText.text = GetDisplayName(equipped);
        }
    }

    private void HandleClicked()
    {
        onClicked?.Invoke(this);
    }

    private string GetDisplayName(bool equipped)
    {
        if (string.IsNullOrEmpty(baseName))
        {
            return equipped ? "(EQUIPPED)" : string.Empty;
        }

        return equipped ? $"{baseName} (EQUIPPED)" : baseName;
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }
}
