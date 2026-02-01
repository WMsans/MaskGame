using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// -------------------------------------------------------------------------
// 1. CONTEXT: The Player Controller (State Machine Manager)
// -------------------------------------------------------------------------
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The object the camera will orbit around and look at.")]
    public Transform targetObject;
    public Camera mainCamera;

    [Header("UI Settings")] 
    public GameObject interactionHintObject; 
    public TextMeshProUGUI interactionText;
    
    // NEW: Offset to make the text float above the object
    [Tooltip("How high above the object the hint should float")]
    public Vector3 interactionHintOffset = new Vector3(0, 2.0f, 0); 

    [Header("Interaction Settings")]
    public LayerMask interactionLayer; 
    public float interactionDistance = 3.0f; 

    [Header("Rotation Settings")]
    public float rotationSpeed = 0.5f;
    public bool invertX = false;
    public bool invertY = false;

    [Header("Zoom Settings")]
    public float zoomSpeed = 0.5f;
    public float minZoomDistance = 2.0f;
    public float maxZoomDistance = 50.0f;
    
    [Header("Walking Settings")]
    public float walkSpeed = 5.0f;
    public float lookSensitivity = 0.5f;

    // Input Actions
    public InputAction RightClickAction { get; private set; }
    public InputAction MouseMoveAction { get; private set; }
    public InputAction ZoomAction { get; private set; }
    public InputAction MoveAction { get; private set; } 
    public InputAction InteractAction { get; private set; } 

    // State Management
    private PlayerState _currentState;
    
    // Components
    public Rigidbody Rb { get; private set; }

    // NEW: Track the current object we are hinting at
    private Transform _currentHintTarget;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();

        RightClickAction = new InputAction(type: InputActionType.Button, binding: "<Mouse>/rightButton");
        MouseMoveAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
        ZoomAction = new InputAction(type: InputActionType.PassThrough, binding: "<Mouse>/scroll");
        
        MoveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
        MoveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        InteractAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/e");
    }

    private void Start()
    {
        if (targetObject == null) Debug.LogWarning("Target Object is not assigned!");
        
        SetInteractionHint(false, "");

        ChangeState(new WalkingState(this));
    }

    private void OnEnable()
    {
        RightClickAction.Enable();
        MouseMoveAction.Enable();
        ZoomAction.Enable();
        MoveAction.Enable();
        InteractAction.Enable();
    }

    private void OnDisable()
    {
        RightClickAction.Disable();
        MouseMoveAction.Disable();
        ZoomAction.Disable();
        MoveAction.Disable();
        InteractAction.Disable();
    }

    private void Update()
    {
        _currentState?.Update();
    }
    
    private void FixedUpdate()
    {
        _currentState?.FixedUpdate();
    }
    
    // NEW: Update UI position after camera has moved
    private void LateUpdate()
    {
        UpdateHintPosition();
    }

    public void ChangeState(PlayerState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState?.Enter();
    }

    public void ExitMaskMaker()
    {
        // NEW: Check if we are currently targeting an IInteractable and tell it to reset
        if (targetObject != null)
        {
            IInteractable interactable = targetObject.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.ExitInteraction();
            }
        }

        ChangeState(new WalkingState(this));
    }

    // UPDATED: Now accepts a target transform to track
    public void SetInteractionHint(bool active, string text, Transform target = null)
    {
        if (interactionHintObject != null)
        {
            interactionHintObject.SetActive(active);
            
            if (active)
            {
                if (interactionText != null) interactionText.text = text;
                _currentHintTarget = target;
                
                // Update position immediately so it doesn't flicker
                UpdateHintPosition();
            }
            else
            {
                _currentHintTarget = null;
            }
        }
    }

    // NEW: Logic to move the UI to the object's screen position
    private void UpdateHintPosition()
    {
        if (interactionHintObject.activeSelf && _currentHintTarget != null && mainCamera != null)
        {
            // Convert World Position -> Screen Position
            Vector3 worldPos = _currentHintTarget.position + interactionHintOffset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos) + new Vector3(0, 100, 0);

            // Check if object is behind the camera (z < 0)
            if (screenPos.z > 0)
            {
                interactionHintObject.transform.position = screenPos;
            }
            else
            {
                // Optionally hide it if it goes behind us, or clamp it
                // For now, we keep the previous position or move it offscreen
            }
        }
    }
}