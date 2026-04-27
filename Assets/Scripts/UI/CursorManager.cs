using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CursorManager : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite defaultSprite;
    public Sprite hoverSprite;
    public Sprite targetingSprite;

    [Header("References")]
    public Image cursorImage;
    [SerializeField] private PlayerAbilityManager playerAbilityManager;

    private static readonly Vector2 PivotTopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 PivotCenter  = new Vector2(0.5f, 0.5f);

    [Header("Flip Spin")]
    [SerializeField] private float flipSpinSpeed = 360f;

    private float spinAngle = 0f;
    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform cursorRect;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    private void Start()
    {
        canvas = cursorImage.canvas;
        canvasRect = canvas.GetComponent<RectTransform>();
        cursorRect = cursorImage.rectTransform;
        cursorRect.pivot = PivotTopLeft;

        Cursor.visible = false;
        cursorImage.sprite = defaultSprite;

        if (canvas == null)
            Debug.LogError("CursorManager: canvas is null. Check that cursorImage is assigned and is inside a Canvas.");

        if (playerAbilityManager == null)
            playerAbilityManager = FindFirstObjectByType<PlayerAbilityManager>();
    }

    private void Update()
    {
        if (canvas == null || canvasRect == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            mousePosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out Vector2 localPoint
        );

        bool isTargeting = playerAbilityManager != null && playerAbilityManager.IsTargeting && !PauseStack.IsPaused;
        bool isFlipping  = isTargeting && playerAbilityManager.IsFlipping;

        if (isTargeting)
        {
            cursorRect.pivot = PivotCenter;
            cursorRect.localPosition = localPoint;
            cursorImage.sprite = targetingSprite != null ? targetingSprite : defaultSprite;

            if (isFlipping)
            {
                spinAngle += flipSpinSpeed * Time.deltaTime;
                if (spinAngle >= 360f) spinAngle -= 360f;
            }
            else
            {
                spinAngle = 0f;
            }

            cursorRect.localEulerAngles = new Vector3(0f, 0f, spinAngle);
        }
        else
        {
            spinAngle = 0f;
            cursorRect.localEulerAngles = Vector3.zero;
            cursorRect.pivot = PivotTopLeft;
            cursorRect.localPosition = localPoint;
            cursorImage.sprite = IsHoveringTarget(mousePosition) ? hoverSprite : defaultSprite;
        }
    }

    /// <summary>
    /// Returns true if any UI element under the cursor has a HoverTarget component on it.
    /// </summary>
    private bool IsHoveringTarget(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        return raycastResults.Count > 0 && raycastResults[0].gameObject.GetComponent<HoverTarget>() != null;
    }

    public void SetHoverCursor() => cursorImage.sprite = hoverSprite;
    public void SetDefaultCursor() => cursorImage.sprite = defaultSprite;

    private void OnEnable() => Cursor.visible = false;
    private void OnDisable() => Cursor.visible = true;
}