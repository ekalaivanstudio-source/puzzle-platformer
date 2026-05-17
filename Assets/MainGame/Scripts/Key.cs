using UnityEngine;

/// <summary>
/// Collectible key object. Implements <see cref="IInteractable"/> so the player
/// can pick it up via the Interact timeline action.
/// On collection, notifies <see cref="GameManager"/> and deactivates itself.
/// </summary>
public class Key : MonoBehaviour, IInteractable
{
    /// <summary>
    /// Called when the player interacts with this key.
    /// Notifies GameManager and removes the key from the scene.
    /// </summary>
    public void Interact()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[Key] GameManager instance not found.", this);
            return;
        }

        GameManager.Instance.KeyCollected();
        gameObject.SetActive(false);
    }
}
