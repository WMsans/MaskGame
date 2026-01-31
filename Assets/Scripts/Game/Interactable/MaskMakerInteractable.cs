using UnityEngine;

public class MaskMakerInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [SerializeField] private string _promptText = "[E] Make Mask";
    
    public string GetInteractionPrompt()
    {
        // This text appears in the UI when the player walks up to the sign
        return _promptText;
    }

    public void Interact()
    {
    }
}