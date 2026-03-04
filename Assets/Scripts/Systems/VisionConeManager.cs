using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class VisionConeManager : MonoBehaviour
{
    public static VisionConeManager Instance { get; private set; }

    [Header("Input")]
    public InputActionAsset inputActions;
    private InputAction showVisionConesAction;

    private readonly List<EnemyVisionCone> visionCones = new List<EnemyVisionCone>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            showVisionConesAction = playerMap.FindAction("ShowVisibilityFeatures");
        }
    }

    private void OnEnable()
    {
        if (showVisionConesAction != null)
            showVisionConesAction.Enable();
    }

    private void OnDisable()
    {
        if (showVisionConesAction != null)
            showVisionConesAction.Disable();
    }

    public void Register(EnemyVisionCone cone)
    {
        if (cone != null && !visionCones.Contains(cone))
            visionCones.Add(cone);
    }

    public void Unregister(EnemyVisionCone cone)
    {
        visionCones.Remove(cone);
    }

    private void Update()
    {
        if (showVisionConesAction == null) return;

        bool isPressed = showVisionConesAction.IsPressed();
        bool allowVisionCones = CombatManager.Instance == null || !CombatManager.Instance.InCombat;

        foreach (var cone in visionCones)
        {
            if (cone != null)
                cone.SetVisible(allowVisionCones && isPressed);
        }
    }
}