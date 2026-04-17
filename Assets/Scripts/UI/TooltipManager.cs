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

    /// <summary>
    /// Snaps the tooltip to the cursor quadrant: the panel corner nearest the screen
    /// centre is placed at the cursor so it always opens away from the screen edge.
    /// </summary>
    private void PositionTooltip()
    {
        Vector2 mousePos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        bool isRight = mousePos.x > Screen.width * 0.5f;
        bool isTop   = mousePos.y > Screen.height * 0.5f;

        // Pivot (0,0) = bottom-left corner anchors to cursor → panel extends up-right
        // Pivot (1,0) = bottom-right corner              → panel extends up-left
        // Pivot (0,1) = top-left corner                  → panel extends down-right
        // Pivot (1,1) = top-right corner                 → panel extends down-left
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