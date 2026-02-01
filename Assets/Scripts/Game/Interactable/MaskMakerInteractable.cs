using UnityEngine;
using DG.Tweening;
using Game.UI;

public class MaskMakerInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] private string _promptText = "[E] Make Mask";
    
    [Header("UI Settings")]
    [Tooltip("The UI Panel to show when interacting (e.g., EditorPanel). If null, will try to find EditorPanel in scene.")]
    public GameObject editorPanel;

    [Header("Camera Settings")]
    [Tooltip("The transform representing where the camera should move to when interacting.")]
    [SerializeField] private Transform _viewPoint;
    [SerializeField] private float _transitionDuration = 1.0f;

    private PlayerController _player;
    private Vector3 _originalCamPos;
    private Quaternion _originalCamRot;
    private bool _isInteracting = false;

    // FIX: Add a variable to track when interaction started
    private float _interactionStartTime;

    public string GetInteractionPrompt()
    {
        return _promptText;
    }

    public void Interact()
    {
        // Prevent double interaction
        if (_isInteracting) return;

        _player = FindFirstObjectByType<PlayerController>();
        if (_player == null) return;
        
        // Auto-find panel if not assigned
        if (editorPanel == null)
        {
            EditorPanel foundPanel = FindFirstObjectByType<EditorPanel>(FindObjectsInactive.Include);
            if (foundPanel != null) editorPanel = foundPanel.gameObject;
        }

        _isInteracting = true;
        _interactionStartTime = Time.time; // FIX: Record the start time

        // 1. Show UI
        if (editorPanel != null) editorPanel.SetActive(true);

        // 2. Stop Player Physics (Prevent sliding while in menu)
        if (_player.Rb != null) _player.Rb.linearVelocity = Vector3.zero;

        // 2. Store original camera transform (World Space) so we can return exactly
        _originalCamPos = _player.mainCamera.transform.position;
        _originalCamRot = _player.mainCamera.transform.rotation;

        // 3. Set the target object for the RotatorState (so we rotate THIS object)
        _player.targetObject = this.transform;

        // 4. Switch to RotatorState (Disables Walking controls/logic)
        _player.ChangeState(new RotatorState(_player));
        
        // 5. Hide the interaction hint immediately
        _player.SetInteractionHint(false, "");

        // 6. Tween Camera to the View Point
        if (_viewPoint != null)
        {
            _player.mainCamera.transform.DOMove(_viewPoint.position, _transitionDuration);
            _player.mainCamera.transform.DORotate(_viewPoint.rotation.eulerAngles, _transitionDuration);
        }
    }

    public void ExitInteraction()
    {
        _isInteracting = false;

        // Hide UI
        if (editorPanel != null) editorPanel.SetActive(false);

        // Restore camera position immediately so WalkingState starts with the correct view
        if (_player != null && _player.mainCamera != null)
        {
            // Stop any active DOTween animations on the camera to prevent conflict
            _player.mainCamera.transform.DOKill();

            _player.mainCamera.transform.position = _originalCamPos;
            _player.mainCamera.transform.rotation = _originalCamRot;
        }
    }
}