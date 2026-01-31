using UnityEngine;
using UnityEngine.InputSystem; // Required for the New Input System

public class MouseRotator : MonoBehaviour
{
    [Header("Settings")]
    public float rotationSpeed = 0.5f; // Default is lower; New Input delta is often in raw pixels
    public bool invertX = false;
    public bool invertY = false;

    // We define InputActions to handle the binding logic
    private InputAction rightClickAction;
    private InputAction mouseMoveAction;

    private void Awake()
    {
        // Setup the actions programmatically
        // 1. Define the "Enable Rotation" button (Right Mouse Button)
        rightClickAction = new InputAction(type: InputActionType.Button, binding: "<Mouse>/rightButton");

        // 2. Define the "Look" input (Mouse Delta)
        mouseMoveAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
    }

    private void OnEnable()
    {
        // The New Input System requires you to explicitly enable actions
        rightClickAction.Enable();
        mouseMoveAction.Enable();
    }

    private void OnDisable()
    {
        rightClickAction.Disable();
        mouseMoveAction.Disable();
    }

    private void Update()
    {
        // Check if the Right Mouse Button is currently held down
        if (rightClickAction.IsPressed())
        {
            // Read the delta (movement) value directly from the action
            Vector2 delta = mouseMoveAction.ReadValue<Vector2>();

            // Apply rotation speed
            float xVal = delta.x * rotationSpeed;
            float yVal = delta.y * rotationSpeed;

            // Handle Inversion
            if (invertX) xVal = -xVal;
            if (invertY) yVal = -yVal;

            // Apply Rotation
            // Horizontal mouse movement rotates around World Y (Up)
            transform.Rotate(Vector3.up, -xVal, Space.World);

            // Vertical mouse movement rotates around Local X (Right)
            transform.Rotate(Vector3.right, yVal, Space.Self);
        }
    }
}