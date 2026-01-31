using UnityEngine;

public class WalkingState : PlayerState
{
    private float _cameraPitch = 0f;

    public WalkingState(PlayerController controller) : base(controller) { }

    public override void Enter()
    {
        // 1. Position Camera for First Person View
        // We attach the camera to the controller so it moves with the player.
        Controller.mainCamera.transform.SetParent(Controller.transform);
        Controller.mainCamera.transform.localPosition = new Vector3(0, 1.6f, 0); // Approx eye level
        Controller.mainCamera.transform.localRotation = Quaternion.identity;

        // 2. Lock Cursor for FPS controls
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 3. Ensure Rigidbody is ready for physics movement
        if (Controller.Rb != null)
        {
            Controller.Rb.isKinematic = false;
            Controller.Rb.constraints = RigidbodyConstraints.FreezeRotation; 
        }
    }

    public override void Exit()
    {
        // Unlock cursor when leaving this state
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public override void Update()
    {
        HandleLook();
    }

    public override void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleLook()
    {
        Vector2 mouseDelta = Controller.MouseMoveAction.ReadValue<Vector2>();

        // Horizontal Rotation (Rotate the Body/Controller)
        float yaw = mouseDelta.x * Controller.lookSensitivity;
        Controller.transform.Rotate(Vector3.up, yaw);

        // Vertical Rotation (Rotate only the Camera)
        float pitch = mouseDelta.y * Controller.lookSensitivity;
        
        // Handle Inversion logic from controller settings
        if (Controller.invertY) pitch = -pitch;
        else pitch = -pitch; // Standard FPS: Mouse Up (positive Y) -> Look Up (Negative X Rot)

        _cameraPitch += pitch;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -85f, 85f); // Clamp to prevent neck breaking

        Controller.mainCamera.transform.localEulerAngles = new Vector3(_cameraPitch, 0, 0);
    }

    private void HandleMovement()
    {
        if (Controller.Rb == null) return;

        Vector2 input = Controller.MoveAction.ReadValue<Vector2>();
        
        // Calculate movement direction relative to where the player is looking
        // Note: transform.forward is the body's forward direction (yaw), ignoring camera pitch
        Vector3 moveDir = Controller.transform.right * input.x + Controller.transform.forward * input.y;
        moveDir.Normalize();

        Vector3 targetVelocity = moveDir * Controller.walkSpeed;

        // Apply to Rigidbody
        Controller.Rb.linearVelocity = targetVelocity;
    }
}