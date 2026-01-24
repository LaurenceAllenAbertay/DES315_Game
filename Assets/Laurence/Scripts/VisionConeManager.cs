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
            showVisionConesAction = playerMap.FindAction("ShowVisionCones");
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
                    cone.SetVisible(isPressed);
                }
            }
        }
    }

    // Call this when new enemies spawn to refresh the list
    public void RefreshVisionCones()
    {
        visionCones = FindObjectsByType<EnemyVisionCone>(FindObjectsSortMode.None);
    }
}