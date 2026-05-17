/// <summary>
/// Contract for any object the player can interact with via the Interact timeline action.
/// Implement on Keys, Doors, Switches, Buttons, or any interactable in the level.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Called when the player executes an Interact action and this object
    /// is within the interaction radius.
    /// </summary>
    void Interact();
}
