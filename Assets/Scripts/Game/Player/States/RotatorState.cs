using UnityEngine;

public class RotatorState : PlayerState
{
    public RotatorState(PlayerController controller) : base(controller) { }

    public override void Update()
    {
        HandleRotation();
        HandleZoom();
    }

    private void HandleRotation()
    {
        // Only orbit when right mouse button is held
        if (Controller.RightClickAction.IsPressed())
        {
            Vector2 delta = Controller.MouseMoveAction.ReadValue<Vector2>();

            float xVal = delta.x * Controller.rotationSpeed;
            float yVal = delta.y * Controller.rotationSpeed;

            if (Controller.invertX) xVal = -xVal;
            if (Controller.invertY) yVal = -yVal;

            // ROTATION LOGIC (Object-Centric):
            // 1. Rotate the OBJECT around the World's Y axis (Horizontal).
            Controller.targetObject.Rotate(Vector3.up, -xVal, Space.World);

            // 2. Rotate the OBJECT around the Camera's Right axis (Vertical).
            //    FIX: Changed Controller.transform.right to Controller.mainCamera.transform.right
            //    This ensures the rotation axis matches the user's current view.
            Controller.targetObject.Rotate(Controller.mainCamera.transform.right, yVal, Space.World);
        }
    }

    private void HandleZoom()
    {
        Vector2 scrollDelta = Controller.ZoomAction.ReadValue<Vector2>();

        if (scrollDelta.y != 0)
        {
            // Calculate movement amount
            float moveAmount = scrollDelta.y * Controller.zoomSpeed * 0.01f;
            
            // Calculate current distance
            float currentDistance = Vector3.Distance(Controller.mainCamera.transform.position, Controller.targetObject.position);

            // Predict new distance (Positive moveAmount = Zoom In = Decrease distance)
            float projectedDistance = currentDistance - moveAmount;

            // Apply movement only if within min/max bounds
            if (projectedDistance > Controller.minZoomDistance && projectedDistance < Controller.maxZoomDistance)
            {
                // Move the OBJECT along the line to the camera (Camera's -Forward)
                // FIX: Changed Controller.transform.forward to Controller.mainCamera.transform.forward
                // This ensures the object moves towards/away from the camera, not the player body.
                Controller.targetObject.position += -Controller.mainCamera.transform.forward * moveAmount;
            }
        }
    }
}