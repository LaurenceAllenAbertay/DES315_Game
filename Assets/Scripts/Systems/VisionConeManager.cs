using UnityEngine;
using UnityEngine.InputSystem;

public class VisionConeManager : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset inputActions;
    private InputAction showVisionConesAction;

    private EnemyVisionCone[] visionCones;

    private void Awake()
    {
        RefreshVisionCones();

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            showVisionConesAction = playerMap.FindAction("ShowVisibilityFeatures");
        }
    }

    private void OnEnable()
    {
        if (showVisionConesAction != null)
        {
            showVisionConesAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (showVisionConesAction != null)
        {
            showVisionConesAction.Disable();
        }
    }

    private void Update()
    {
        if (showVisionConesAction != null)
        {
            bool isPressed = showVisionConesAction.IsPressed();

            foreach (var cone in visionCones)
            {
                if (cone != null)
                {
                    if (isPressed && IsConeAllowedToShow(cone))
                    {
                        cone.SetVisible(true);
                    }
                    else
                    {
                        cone.SetVisible(false);
                    }
                }
            }
        }
    }

    public void RefreshVisionCones()
    {
        visionCones = FindObjectsByType<EnemyVisionCone>(FindObjectsSortMode.None);
    }

    private bool IsConeAllowedToShow(EnemyVisionCone cone)
    {
        Enemy enemy = cone.GetComponentInParent<Enemy>();
        return enemy == null || !enemy.IsHiddenFromPlayer;
    }
}
