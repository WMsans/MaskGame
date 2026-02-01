using UnityEngine;

// Interface for any object that displays a hint when looked at
public interface IInteractable
{
    // Returns the text to display (e.g., "Press E to Read")
    string GetInteractionPrompt();
    
    // Logic to execute when the player actually interacts (optional, but good to have)
    void Interact();

    // Logic to execute when exiting the interaction
    void ExitInteraction();
}