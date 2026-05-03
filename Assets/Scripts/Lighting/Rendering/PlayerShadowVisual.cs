using UnityEngine;

/// <summary>
/// Darkens the player's renderers when they are in shadow by applying a colour tint
/// </summary>
[RequireComponent(typeof(LightDetectable))]
public class PlayerShadowVisual : MonoBehaviour
{
    [Header("Shadow Tint")]
    [Tooltip("Colour multiplied onto the renderer when in shadow. Alpha is ignored.")]
    public Color shadowColor = new Color(0.35f, 0.35f, 0.45f, 1f);
    [Tooltip("Transition speed between lit and shadowed states.")]
    public float transitionSpeed = 4f;

    [Header("Renderer Filter")]
    [Tooltip("Leave empty to use all child renderers.")]
    public Renderer[] targetRenderers;

    private LightDetectable lightDetectable;
    private MaterialPropertyBlock propBlock;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private Color currentColor = Color.white;
    private Color targetColor  = Color.white;

    private void Awake()
    {
        lightDetectable = GetComponent<LightDetectable>();
        propBlock = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        lightDetectable.OnLightStateChanged += HandleLightStateChanged;
    }

    private void OnDisable()
    {
        lightDetectable.OnLightStateChanged -= HandleLightStateChanged;
    }

    private void HandleLightStateChanged(bool inLight)
    {
        targetColor = inLight ? Color.white : shadowColor;
    }

    private void Update()
    {
        currentColor = Color.Lerp(currentColor, targetColor, transitionSpeed * Time.deltaTime);
        ApplyColor(currentColor);
    }

    private void ApplyColor(Color color)
    {
        foreach (Renderer r in targetRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorID, color);
            r.SetPropertyBlock(propBlock);
        }
    }
}