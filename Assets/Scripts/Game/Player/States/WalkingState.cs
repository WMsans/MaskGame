using UnityEngine;

public class WalkingState : PlayerState
{
    private float _cameraPitch = 0f;
    private IInteractable _currentInteractable; 

    public WalkingState(PlayerController controller) : base(controller) { }

    public override void Enter()
    {
        // 1. Sync Camera Rotation
        // Capture current rotation so the view doesn't snap to forward
        Vector3 currentEuler = Controller.mainCamera.transform.eulerAngles;

        _cameraPitch = currentEuler.x;
        // Normalize pitch to -180 to 180 range
        if (_cameraPitch > 180f) _cameraPitch -= 360f;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -85f, 85f);

        // Apply Yaw to Body, Pitch to Camera
        Controller.transform.rotation = Quaternion.Euler(0, currentEuler.y, 0);
        Controller.mainCamera.transform.localRotation = Quaternion.Euler(_cameraPitch, 0, 0);

        // 3. Lock Cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 4. Physics Setup
        if (Controller.Rb != null)
        {
            Controller.Rb.isKinematic = false;
            Controller.Rb.constraints = RigidbodyConstraints.FreezeRotation; 
        }
    }

    public override void Exit()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Controller.SetInteractionHint(false, "");
    }

    public override void Update()
    {
        HandleLook();
        HandleInteractionCheck(); 
        HandleInteractionInput(); 
    }

    public override void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleLook()
    {
        Vector2 mouseDelta = Controller.MouseMoveAction.ReadValue<Vector2>();
        if(mouseDelta.sqrMagnitude >= 20000) return;

        float yaw = mouseDelta.x * Controller.lookSensitivity;
        Controller.transform.Rotate(Vector3.up, yaw);

        float pitch = mouseDelta.y * Controller.lookSensitivity;
        
        if (Controller.invertY) pitch = -pitch;
        else pitch = -pitch; 

        _cameraPitch += pitch;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -85f, 85f); 

        Controller.mainCamera.transform.localEulerAngles = new Vector3(_cameraPitch, 0, 0);
    }

    private void HandleInteractionCheck()
    {
        Ray ray = new Ray(Controller.mainCamera.transform.position, Controller.mainCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Controller.interactionDistance, Controller.interactionLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                if (_currentInteractable != interactable)
                {
                    _currentInteractable = interactable;
                    Controller.SetInteractionHint(true, interactable.GetInteractionPrompt(), hit.transform);
                }
                return; 
            }
        }

        if (_currentInteractable != null)
        {
            _currentInteractable = null;
            Controller.SetInteractionHint(false, "");
        }
    }

    private void HandleInteractionInput()
    {
        if (_currentInteractable != null && Controller.InteractAction.WasPressedThisFrame())
        {
            _currentInteractable.Interact();
        }
    }

    private void HandleMovement()
    {
        if (Controller.Rb == null) return;

        Vector2 input = Controller.MoveAction.ReadValue<Vector2>();
        
        Vector3 moveDir = Controller.transform.right * input.x + Controller.transform.forward * input.y;
        moveDir.Normalize();

        Vector3 targetVelocity = moveDir * Controller.walkSpeed;

        Controller.Rb.linearVelocity = targetVelocity;
    }
}