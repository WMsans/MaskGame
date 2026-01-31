using UnityEngine;
using UnityEngine.InputSystem;

public class CameraOrbit : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The object the camera will orbit around and look at.")]
    public Transform targetObject;

    [Header("Rotation Settings")]
    public float rotationSpeed = 0.5f;
    public bool invertX = false;
    public bool invertY = false;

    [Header("Zoom Settings")]
    public float zoomSpeed = 0.5f;
    public float minZoomDistance = 2.0f; // Prevent going inside the object
    public float maxZoomDistance = 50.0f; // Prevent going too far away

    // Input Actions
    private InputAction rightClickAction;
    private InputAction mouseMoveAction;
    private InputAction zoomAction;

    private void Awake()
    {
        // Setup Input Actions
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
        if (targetObject == null)
        {
            Debug.LogWarning("Target Object is not assigned! Please assign an object for the camera to orbit.");
        }
        else
        {
            // Optional: Immediately snap camera to look at the target on start
            transform.LookAt(targetObject);
        }
    }

    private void Update()
    {
        if (targetObject == null) return;

        HandleRotation();
        HandleZoom();
    }

    private void HandleRotation()
    {
        // Only orbit when right mouse button is held
        if (rightClickAction.IsPressed())
        {
            Vector2 delta = mouseMoveAction.ReadValue<Vector2>();

            float xVal = delta.x * rotationSpeed;
            float yVal = delta.y * rotationSpeed;

            if (invertX) xVal = -xVal;
            if (invertY) yVal = -yVal;

            // ORBIT LOGIC:
            // 1. Rotate around the target's Y axis (Horizontal movement)
            // Note: We use positive xVal here so dragging mouse left moves camera left (orbiting right)
            transform.RotateAround(targetObject.position, Vector3.up, xVal);

            // 2. Rotate around the Camera's Right axis (Vertical movement)
            // Note: We use -yVal because dragging up usually means we want to orbit "up" (camera moves down)
            transform.RotateAround(targetObject.position, transform.right, -yVal);

            // 3. Fix any rotation drift to keep the target dead center
            transform.LookAt(targetObject);
        }
    }

    private void HandleZoom()
    {
        Vector2 scrollDelta = zoomAction.ReadValue<Vector2>();

        if (scrollDelta.y != 0)
        {
            // Calculate direction from camera to target
            float moveAmount = scrollDelta.y * zoomSpeed * 0.01f;
            
            // Calculate current distance
            float currentDistance = Vector3.Distance(transform.position, targetObject.position);

            // Predict new distance
            // If moveAmount is positive (scrolling up), we move forward (distance decreases)
            float projectedDistance = currentDistance - moveAmount;

            // Apply movement only if within min/max bounds
            if (projectedDistance > minZoomDistance && projectedDistance < maxZoomDistance)
            {
                // Move the camera forward/backward along its own forward vector
                transform.position += transform.forward * moveAmount;
            }
        }
    }
}