using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-scoped component lookups, for the objects that have to be found rather than wired —
/// a scene object a PREFAB cannot hold a reference to, or one that is still disabled when the
/// search runs.
///
/// Unity's own FindAnyObjectByType / FindObjectsByType cannot be used for that second case.
/// Asked to INCLUDE inactive objects, they also return components living on PREFAB ASSETS
/// loaded in memory, because those sit in a hidden scene of their own. In the editor that
/// silently hands out the prefab asset instead of the scene instance: its Awake has never run,
/// so every cached reference on it is null, and anything written to its transform is written
/// to the asset on disk. Walking the active scene's roots cannot reach an asset at all.
/// </summary>
public static class SceneObjects
{
    /// <summary>
    /// The first <typeparamref name="T"/> in the active scene, disabled ones included.
    /// Null when the scene has none.
    /// </summary>
    public static T FindInActiveScene<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        return null;
    }
}
