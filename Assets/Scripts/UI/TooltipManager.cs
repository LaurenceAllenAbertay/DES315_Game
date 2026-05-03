using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RectTransform tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;

    [Header("Settings")]
    [Tooltip("Pixel gap between the cursor hotspot and the nearest tooltip corner")]
    [SerializeField] private float cursorOffset = 12f;

    private Canvas _rootCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _rootCanvas = tooltipPanel.GetComponentInParent<Canvas>();
        tooltipPanel.gameObject.SetActive(false);
    }

    public void Show(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        tooltipText.text = text;
        tooltipPanel.gameObject.SetActive(true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipText.rectTransform);
        PositionTooltip();
    }

    public void Hide()
    {
        tooltipPanel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (tooltipPanel.gameObject.activeSelf)
            PositionTooltip();
    }
    
    private void PositionTooltip()
    {
        Vector2 mousePos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        bool isRight = mousePos.x > Screen.width * 0.5f;
        bool isTop   = mousePos.y > Screen.height * 0.5f;
        
        float pivotX = isRight ? 1f : 0f;
        float pivotY = isTop   ? 1f : 0f;
        tooltipPanel.pivot = new Vector2(pivotX, pivotY);

        float offsetX = isRight ? -cursorOffset :  cursorOffset;
        float offsetY = isTop   ? -cursorOffset :  cursorOffset;

        Vector2 anchoredPos;

        if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                mousePos + new Vector2(offsetX, offsetY),
                _rootCanvas.worldCamera,
                out anchoredPos);
        }
        else
        {
            anchoredPos = mousePos + new Vector2(offsetX, offsetY);
        }

        tooltipPanel.anchoredPosition = anchoredPos;
    }
}