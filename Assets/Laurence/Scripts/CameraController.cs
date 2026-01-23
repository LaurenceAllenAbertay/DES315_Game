using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float moveSpeed = 10f;
    public float fastMoveMultiplier = 2f;

    [Header("Rotation Settings")]
    public float rotateSpeed = 0.3f;

    private bool isRotating;

    private void Update()
    {
        HandlePan();
        HandleRotation();
    }

    private void HandlePan()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed)
            input.y += 1f;
        if (keyboard.sKey.isPressed)
            input.y -= 1f;
        if (keyboard.dKey.isPressed)
            input.x += 1f;
        if (keyboard.aKey.isPressed)
            input.x -= 1f;

        if (input == Vector2.zero) return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDirection = (forward * input.y + right * input.x).normalized;

        float speed = moveSpeed;
        if (keyboard.shiftKey.isPressed)
            speed *= fastMoveMultiplier;

        transform.position += moveDirection * speed * Time.deltaTime;
    }

    private void HandleRotation()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            isRotating = true;
        }

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            isRotating = false;
        }

        if (isRotating)
        {
            float mouseDelta = mouse.delta.x.ReadValue();
            float angle = mouseDelta * rotateSpeed;

            transform.Rotate(0f, angle, 0f, Space.World);
        }
    }
}