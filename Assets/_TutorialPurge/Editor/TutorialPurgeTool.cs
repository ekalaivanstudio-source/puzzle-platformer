#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TEMPORARY one-shot tool used to remove the Tutorial System from the project.
///
/// It scans every scene and prefab and strips anything belonging to the tutorial
/// system, using Unity's own object API so prefab-instance bookkeeping, added-component
/// lists and scene roots are all cleaned up correctly (no corrupt YAML, no dangling
/// "missing script" stubs).
///
/// Detection is by reflection on the component's namespace/type name, so this file does
/// NOT hard-reference the tutorial types — it keeps working right up until the tutorial
/// scripts are deleted, and it can itself be deleted afterwards.
///
/// USAGE: Tools ▸ Tutorial System ▸ PURGE Tutorial From Scenes And Prefabs, then delete
/// the Assets/_TutorialPurge folder.
/// </summary>
public static class TutorialPurgeTool
{
    // Any component whose type lives in this namespace is part of the tutorial system.
    private const string TutorialNamespace = "TutorialSystem";

    // Tutorial-related components that live OUTSIDE the TutorialSystem namespace.
    private static readonly HashSet<string> LooseTutorialTypeNames = new HashSet<string>
    {
        "TutorialInputCountTrigger",
    };

    // The type name whose presence marks a whole GameObject (the TutorialSystem prefab
    // instance) for removal rather than just the individual component.
    private const string SystemRootTypeName = "TutorialManager";

    private static bool IsTutorialComponent(MonoBehaviour mb)
    {
        if (mb == null) return false;
        var t = mb.GetType();
        return t.Namespace == TutorialNamespace || LooseTutorialTypeNames.Contains(t.Name);
    }

    [MenuItem("Tools/Tutorial System/PURGE Tutorial From Scenes And Prefabs")]
    public static void Purge()
    {
        if (!EditorUtility.DisplayDialog(
                "Purge Tutorial System",
                "This removes every tutorial component, the TutorialSystem object and all tutorial " +
                "references from every scene and prefab, then saves them.\n\n" +
                "COMMIT your work first so you can review/revert the diff.\n\nProceed?",
                "Purge", "Cancel"))
            return;

        int scenesChanged = 0, prefabsChanged = 0, componentsRemoved = 0, objectsRemoved = 0;

        // Remember which scene was open so we can restore it afterwards.
        string openScenePath = EditorSceneManager.GetActiveScene().path;
        // Give the user a chance to save any unsaved scene edits before we start swapping scenes.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // ---- Scenes ----
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue; // skip package scenes

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (PurgeScene(scene, ref componentsRemoved, ref objectsRemoved))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesChanged++;
            }
        }

        // ---- Prefabs ----
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            // The TutorialSystem prefab itself is deleted wholesale later — don't bother editing it.
            if (path.Replace('\\', '/').Contains("/Tutorial/")) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!IsTutorialComponent(mb)) continue;
                    Object.DestroyImmediate(mb, true);
                    componentsRemoved++;
                    changed = true;
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabsChanged++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Restore the scene the user had open.
        if (!string.IsNullOrEmpty(openScenePath))
            EditorSceneManager.OpenScene(openScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();

        Debug.Log($"[TutorialPurgeTool] Complete. Scenes changed: {scenesChanged}, prefabs changed: {prefabsChanged}, " +
                  $"components removed: {componentsRemoved}, tutorial objects removed: {objectsRemoved}.\n" +
                  "You can now delete the tutorial scripts/assets/prefab/data and the Assets/_TutorialPurge folder.");

        EditorUtility.DisplayDialog(
            "Purge Complete",
            $"Scenes changed: {scenesChanged}\nPrefabs changed: {prefabsChanged}\n" +
            $"Components removed: {componentsRemoved}\nTutorial objects removed: {objectsRemoved}\n\n" +
            "Tell your assistant it's done — it will delete the tutorial files and this tool.",
            "OK");
    }

    private static bool PurgeScene(Scene scene, ref int componentsRemoved, ref int objectsRemoved)
    {
        var objectsToDestroy = new HashSet<GameObject>();
        var componentsToDestroy = new List<MonoBehaviour>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!IsTutorialComponent(mb)) continue;

                if (mb.GetType().Name == SystemRootTypeName)
                {
                    // Destroy the whole TutorialSystem object (its prefab-instance root if it is one).
                    GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(mb.gameObject);
                    objectsToDestroy.Add(instanceRoot != null ? instanceRoot : mb.gameObject);
                }
                else
                {
                    componentsToDestroy.Add(mb);
                }
            }

            // Fallback: catch a TutorialSystem prefab instance that exposes no reachable
            // TutorialManager (e.g. name-only match on a root object).
            if (root.name == "TutorialSystem")
                objectsToDestroy.Add(root);
        }

        bool changed = false;

        // Remove individual tutorial components first (skip ones on objects we'll destroy wholesale).
        foreach (MonoBehaviour mb in componentsToDestroy)
        {
            if (mb == null) continue;
            if (mb.gameObject != null && IsInsideAny(mb.gameObject, objectsToDestroy)) continue;
            Object.DestroyImmediate(mb, true);
            componentsRemoved++;
            changed = true;
        }

        // Then destroy whole tutorial objects.
        foreach (GameObject go in objectsToDestroy)
        {
            if (go == null) continue;
            Object.DestroyImmediate(go, true);
            objectsRemoved++;
            changed = true;
        }

        return changed;
    }

    // True if 'go' is one of, or a descendant of any of, the given roots.
    private static bool IsInsideAny(GameObject go, HashSet<GameObject> roots)
    {
        for (Transform t = go.transform; t != null; t = t.parent)
        {
            if (roots.Contains(t.gameObject)) return true;
        }
        return false;
    }
}
#endif
