using UnityEngine;
using UnityEngine.InputSystem;

// -------------------------------------------------------------------------
// 1. CONTEXT: The Player Controller (State Machine Manager)
// -------------------------------------------------------------------------
public class PlayerController : MonoBehaviour
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
    public float minZoomDistance = 2.0f;
    public float maxZoomDistance = 50.0f;

    // Input Actions (Managed centrally so states can access them)
    public InputAction RightClickAction { get; private set; }
    public InputAction MouseMoveAction { get; private set; }
    public InputAction ZoomAction { get; private set; }

    // State Management
    private PlayerState _currentState;

    private void Awake()
    {
        // Setup Input Actions
        RightClickAction = new InputAction(type: InputActionType.Button, binding: "<Mouse>/rightButton");
        MouseMoveAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
        ZoomAction = new InputAction(type: InputActionType.PassThrough, binding: "<Mouse>/scroll");
    }

    private void Start()
    {
        if (targetObject == null)
        {
            Debug.LogWarning("Target Object is not assigned! Please assign an object for the camera to orbit.");
        }

        // Set the initial state to RotatorState (the logic from your existing rotator)
        ChangeState(new RotatorState(this));
    }

    private void OnEnable()
    {
        RightClickAction.Enable();
        MouseMoveAction.Enable();
        ZoomAction.Enable();
    }

    private void OnDisable()
    {
        RightClickAction.Disable();
        MouseMoveAction.Disable();
        ZoomAction.Disable();
    }

    private void Update()
    {
        if (targetObject == null) return;
        
        // Execute the current state's logic
        _currentState?.Update();
    }

    public void ChangeState(PlayerState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState?.Enter();
    }
}