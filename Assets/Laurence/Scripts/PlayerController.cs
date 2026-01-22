using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple player controller for the demo.
/// Moves with WASD/Arrow keys and checks light status.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("Light Detection")]
    [Tooltip("Offset from transform.position to check light (usually center of character)")]
    public Vector3 lightCheckOffset = new Vector3(0f, 1f, 0f);

    [Header("Current State (Read Only)")]
    [SerializeField] private bool isInLight;
    [SerializeField] private float currentLightLevel;
    [SerializeField] private int lightsReaching;

    private CharacterController characterController;
    private Vector3 velocity;

    // Events for UI and other systems to subscribe to
    public delegate void LightStateChanged(bool inLight, float lightLevel);
    public event LightStateChanged OnLightStateChanged;

    public bool IsInLight => isInLight;
    public float CurrentLightLevel => currentLightLevel;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Initial light check
        UpdateLightState();
    }

    private void Update()
    {
        HandleMovement();
        UpdateLightState();
    }

    private void HandleMovement()
    {
        // Get input using new Input System
        Vector2 input = Vector2.zero;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;
        }

        // Calculate movement direction
        Vector3 move = new Vector3(input.x, 0f, input.y).normalized;

        // Apply movement
        if (move.magnitude >= 0.1f)
        {
            characterController.Move(move * moveSpeed * Time.deltaTime);
        }

        // Apply gravity
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void UpdateLightState()
    {
        if (LightDetectionManager.Instance == null) return;

        Vector3 checkPoint = transform.position + lightCheckOffset;
        LightCheckResult result = LightDetectionManager.Instance.CheckLightAtPoint(checkPoint);

        bool previousState = isInLight;

        isInLight = result.isInLight;
        currentLightLevel = result.totalLightContribution;
        lightsReaching = result.contributingLights.Count;

        // Fire event if state changed
        if (previousState != isInLight)
        {
            OnLightStateChanged?.Invoke(isInLight, currentLightLevel);
        }
    }

    /// <summary>
    /// Get the point where light detection occurs
    /// </summary>
    public Vector3 GetLightCheckPoint()
    {
        return transform.position + lightCheckOffset;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize light check point
        Vector3 checkPoint = transform.position + lightCheckOffset;
        Gizmos.color = isInLight ? Color.yellow : Color.blue;
        Gizmos.DrawWireSphere(checkPoint, 0.2f);
    }
}