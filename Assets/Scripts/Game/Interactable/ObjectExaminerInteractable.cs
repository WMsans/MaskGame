using UnityEngine;
using DG.Tweening;
using Game.UI;

public class ObjectExaminerInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] private string _promptText = "[E] Inspect";
    
    [Header("UI Settings")]
    [Tooltip("The UI Panel to show when interacting. If null, will try to find ExaminationPanel in scene.")]
    public GameObject infoPanel;

    [Header("Inspection Settings")]
    [Tooltip("Distance from camera to hold the object.")]
    [SerializeField] private float _holdDistance = 1.5f;
    [SerializeField] private float _transitionDuration = 0.5f;

    private PlayerController _player;
    
    // Store original Transform of THIS object
    private Vector3 _originalPos;
    private Quaternion _originalRot;
    
    private bool _isInteracting = false;
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
        if (infoPanel == null)
        {
            ExaminationPanel foundPanel = FindFirstObjectByType<ExaminationPanel>(FindObjectsInactive.Include);
            if (foundPanel != null) infoPanel = foundPanel.gameObject;
        }

        _isInteracting = true;
        _interactionStartTime = Time.time;

        // 1. Show UI
        if (infoPanel != null) infoPanel.SetActive(true);

        // 2. Stop Player Physics
        if (_player.Rb != null) _player.Rb.linearVelocity = Vector3.zero;

        // 3. Store original transform of the object
        _originalPos = this.transform.position;
        _originalRot = this.transform.rotation;

        // 4. Set the target object for the RotatorState
        _player.targetObject = this.transform;

        // 5. Switch to RotatorState
        _player.ChangeState(new RotatorState(_player));
        
        // 6. Hide the interaction hint
        _player.SetInteractionHint(false, "");

        // 7. DOTween THIS object to front of camera
        Transform camTrans = _player.mainCamera.transform;
        Vector3 targetPos = camTrans.position + (camTrans.forward * _holdDistance);
        
        // Rotate to face the camera (LookAt camera position, then flip 180 if needed, usually we want the "front" of the object facing camera)
        // Simple LookAt:
        // Quaternion targetRot = Quaternion.LookRotation(camTrans.position - targetPos); // Look BACK at camera
        // Or just match camera rotation but flipped Y?
        // Let's make it look at the camera.
        Quaternion targetRot = camTrans.rotation;

        this.transform.DOMove(targetPos, _transitionDuration).SetEase(Ease.OutBack);
        this.transform.DORotateQuaternion(targetRot, _transitionDuration).SetEase(Ease.OutBack);
    }

    public void ExitInteraction()
    {
        _isInteracting = false;

        // Hide UI
        if (infoPanel != null) infoPanel.SetActive(false);

        // Return Object to original position
        // We use DOTween so it looks smooth returning
        this.transform.DOMove(_originalPos, _transitionDuration);
        this.transform.DORotateQuaternion(_originalRot, _transitionDuration);
    }
}
