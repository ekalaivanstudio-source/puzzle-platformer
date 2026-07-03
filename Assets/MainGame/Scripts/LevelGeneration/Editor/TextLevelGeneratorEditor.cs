#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelGenerationSystem.EditorTools
{
    /// <summary>
    /// Inspector + menu tooling for <see cref="TextLevelGenerator"/>.
    ///
    /// Adds "Generate Level In Scene" / "Clear Generated" buttons to the component so designers can
    /// build the level at edit time. Generation uses <see cref="PrefabUtility.InstantiatePrefab"/>
    /// (tiles stay linked to their source prefab) and records full Undo, exactly like placing the
    /// tiles by hand.
    ///
    /// Also adds:  Tools ▸ Level Generation ▸ Create Level Generator In Scene
    /// to drop a ready-to-configure generator object into the open scene.
    /// </summary>
    [CustomEditor(typeof(TextLevelGenerator))]
    public class TextLevelGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var generator = (TextLevelGenerator)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generate builds the level from the text file at the origin (top-left cell). " +
                "Running it again clears the previous tiles first, so it is safe to tweak and " +
                "regenerate. Both actions support Undo (Ctrl+Z).",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(
                       generator.Palette == null || generator.LevelFile == null))
            {
                if (GUILayout.Button("Generate Level In Scene"))
                    GenerateInScene(generator);
            }

            using (new EditorGUI.DisabledScope(generator.SpawnedCount == 0))
            {
                if (GUILayout.Button($"Clear Generated ({generator.SpawnedCount})"))
                    ClearInScene(generator);
            }
        }

        private static void GenerateInScene(TextLevelGenerator generator)
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Level");

            int count = generator.Generate(
                prefab =>
                {
                    var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (tile != null)
                        Undo.RegisterCreatedObjectUndo(tile, "Generate Level Tile");
                    return tile;
                },
                tile => Undo.DestroyObjectImmediate(tile));

            if (count < 0) return; // Generate already logged the reason (missing palette/file).

            EditorUtility.SetDirty(generator);
            MarkActiveSceneDirty(generator);
            Debug.Log($"[TextLevelGenerator] Generated {count} tile(s).", generator);
        }

        private static void ClearInScene(TextLevelGenerator generator)
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Generated Level");
            generator.ClearGenerated(tile => Undo.DestroyObjectImmediate(tile));
            EditorUtility.SetDirty(generator);
            MarkActiveSceneDirty(generator);
        }

        private static void MarkActiveSceneDirty(Object context)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Level Generation/Create Level Generator In Scene", priority = 0)]
        private static void CreateGeneratorInScene()
        {
            var go = new GameObject("TextLevelGenerator");
            go.AddComponent<TextLevelGenerator>();
            Undo.RegisterCreatedObjectUndo(go, "Create Level Generator");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
#endif
