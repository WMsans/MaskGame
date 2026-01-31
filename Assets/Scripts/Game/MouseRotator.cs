using UnityEngine;
using UnityEngine.InputSystem;

public class MouseRotator : MonoBehaviour
{
    [Header("Settings")]
    public float rotationSpeed = 0.5f;
    public bool invertX = false;
    public bool invertY = false;

    [Header("Zoom Settings")]
    public float zoomSpeed = 0.5f; // Increased slightly for world-space movement
    public Camera targetCamera;    // Reference to the camera we are zooming towards

    // Input Actions
    private InputAction rightClickAction;
    private InputAction mouseMoveAction;
    private InputAction zoomAction;

    private void Awake()
    {
        rightClickAction = new InputAction(type: InputActionType.Button, binding: "<Mouse>/rightButton");
        mouseMoveAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
        zoomAction = new InputAction(type: InputActionType.PassThrough, binding: "<Mouse>/scroll");
    }

    private void OnEnable()
    {
        rightClickAction.Enable();
        mouseMoveAction.Enable();
        zoomAction.Enable();
    }

    private void OnDisable()
    {
        rightClickAction.Disable();
        mouseMoveAction.Disable();
        zoomAction.Disable();
    }

    private void Start()
    {
        // If no camera is assigned in the Inspector, try to find the Main Camera
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) 
            {
                Debug.LogWarning("No Main Camera found! Zoom may not work.");
            }
        }
    }

    private void Update()
    {
        HandleRotation();
        HandleZoom();
    }

    private void HandleRotation()
    {
        if (rightClickAction.IsPressed())
        {
            Vector2 delta = mouseMoveAction.ReadValue<Vector2>();

            float xVal = delta.x * rotationSpeed;
            float yVal = delta.y * rotationSpeed;

            if (invertX) xVal = -xVal;
            if (invertY) yVal = -yVal;

            // Standard object rotation (Turntable style)
            transform.Rotate(Vector3.up, -xVal, Space.World);
            transform.Rotate(Vector3.right, yVal, Space.Self);
        }
    }

    private void HandleZoom()
    {
        if (targetCamera == null) return;

        Vector2 scrollDelta = zoomAction.ReadValue<Vector2>();

        if (scrollDelta.y != 0)
        {
            // Calculate how much to move
            float moveAmount = scrollDelta.y * zoomSpeed * 0.01f;

            // FIX: Instead of moving along 'transform.forward' (Local),
            // we move along 'targetCamera.transform.forward' (World direction of camera).
            
            Vector3 zoomDirection = targetCamera.transform.forward;
            
            // Move the object physically in World Space along that direction
            transform.position += zoomDirection * moveAmount;
        }
    }
}
