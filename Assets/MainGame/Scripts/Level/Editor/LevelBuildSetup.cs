using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drops a <see cref="LevelBuildDirector"/> into levels, so the level-build intro is one menu
/// click per level rather than a component placed and wired by hand twenty-odd times.
///
/// The director finds everything it animates by itself, so there is nothing to configure after
/// this runs — the object it creates is empty except for the component. Anything a particular
/// level wants done differently is set on that component afterwards, and re-running this leaves
/// a level that already has one completely alone, tuning included.
/// </summary>
public static class LevelBuildSetup
{
    private const string k_HolderName = "[Level Build]";

    [MenuItem("Tools/Level Build/Add Director To Open Scene")]
    private static void AddToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (AddDirector(scene, out LevelBuildDirector director))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = director.gameObject;
            Debug.Log($"[Level Build] Added a director to '{scene.name}'. " +
                      "Save the scene to keep it.", director);
        }
        else
        {
            Debug.Log($"[Level Build] '{scene.name}' already has a director — left as it is.",
                      director);
        }
    }

    [MenuItem("Tools/Level Build/Remove Director From Open Scene")]
    private static void RemoveFromOpenScene()
    {
        LevelBuildDirector director = Object.FindAnyObjectByType<LevelBuildDirector>(
            FindObjectsInactive.Include);

        if (director == null)
        {
            Debug.Log("[Level Build] The open scene has no director.");
            return;
        }

        // The holder object goes with it when this tool is what created it; a director someone
        // put on an object of their own only loses the component.
        GameObject holder = director.gameObject;
        if (holder.name == k_HolderName && holder.transform.childCount == 0 &&
            holder.GetComponents<Component>().Length == 2)
        {
            Undo.DestroyObjectImmediate(holder);
        }
        else
        {
            Undo.DestroyObjectImmediate(director);
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[Level Build] Director removed.");
    }

    /// <summary>
    /// Adds a director to every scene in the build list that does not already have one, and
    /// saves each as it goes. Asked for out loud first, because it edits and saves scenes
    /// that are not currently open — the one thing here that a Ctrl+Z cannot take back.
    /// </summary>
    [MenuItem("Tools/Level Build/Add Director To All Build Scenes")]
    private static void AddToAllBuildScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!EditorUtility.DisplayDialog(
                "Add Level Build Director",
                "Add a Level Build Director to every scene in the build list that does not " +
                "already have one, and SAVE each of those scenes?\n\nScenes that already have " +
                "one are left untouched. This cannot be undone with Ctrl+Z.",
                "Add and save", "Cancel"))
        {
            return;
        }

        string reopen = SceneManager.GetActiveScene().path;
        int added = 0;
        int skipped = 0;

        try
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene entry = scenes[i];
                if (!entry.enabled || string.IsNullOrEmpty(entry.path)) continue;

                EditorUtility.DisplayProgressBar(
                    "Level Build", entry.path, (i + 1) / (float)scenes.Length);

                Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);

                // A level is a level because it has a player in it: the launcher, the home
                // screen and any other non-level scene in the build list have nothing for a
                // director to build and would only get a stray object out of this.
                if (SceneObjects.FindInActiveScene<PlayerController>() == null)
                {
                    skipped++;
                    continue;
                }

                if (!AddDirector(scene, out LevelBuildDirector _))
                {
                    skipped++;
                    continue;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                added++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (!string.IsNullOrEmpty(reopen))
                EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
        }

        string summary = $"Added a director to {added} scene(s). " +
                         $"Left {skipped} alone (already had one, or is not a level).";

        Debug.Log($"[Level Build] {summary}");
        EditorUtility.DisplayDialog("Level Build", summary, "Done");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────────────

    // False when the scene already has one, so every caller can treat "did nothing" and
    // "added it" differently without searching twice.
    private static bool AddDirector(Scene scene, out LevelBuildDirector director)
    {
        director = Object.FindAnyObjectByType<LevelBuildDirector>(FindObjectsInactive.Include);
        if (director != null) return false;

        var holder = new GameObject(k_HolderName);
        SceneManager.MoveGameObjectToScene(holder, scene);
        Undo.RegisterCreatedObjectUndo(holder, "Add Level Build Director");

        director = Undo.AddComponent<LevelBuildDirector>(holder);
        return true;
    }
}
