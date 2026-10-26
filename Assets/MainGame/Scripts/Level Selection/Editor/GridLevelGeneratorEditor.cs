using UnityEditor;
using UnityEngine;

namespace LevelSelection.Editor
{
    [CustomEditor(typeof(GridLevelGenerator))]
    public class GridLevelGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var generator = (GridLevelGenerator)target;

            if (generator.ArcDatabase != null)
            {
                EditorGUILayout.HelpBox(
                    $"Centralized Arc Database: \"{generator.ArcDatabase.name}\" ({generator.ArcDatabase.Count} Arcs, {generator.ArcDatabase.CalculateTotalLevels()} Total Levels)\n" +
                    $"Any new arcs added to this database automatically apply to both Main Menu and Pause Menu.",
                    MessageType.Info);

                if (GUILayout.Button("Ping Centralized Arc Database", GUILayout.Height(24)))
                {
                    Selection.activeObject = generator.ArcDatabase;
                    EditorGUIUtility.PingObject(generator.ArcDatabase);
                }
                EditorGUILayout.Space(6);
            }

            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Generator Controls", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-Wire All References", GUILayout.Height(30)))
            {
                Undo.RecordObject(generator, "Auto-Wire Grid Level Generator");
                generator.AutoWireReferences();
                EditorUtility.SetDirty(generator);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Previous Page", GUILayout.Height(26)))
            {
                generator.PreviousPage();
            }
            if (GUILayout.Button("Next Page ▶", GUILayout.Height(26)))
            {
                generator.NextPage();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Refresh Grid Preview", GUILayout.Height(26)))
            {
                generator.Refresh();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Current Arc Index: {generator.CurrentArcIndex} | Current Page: {generator.CurrentPageIndex + 1}\n" +
                $"Levels Per Page: {generator.LevelsPerPage}",
                MessageType.Info);
        }
    }
}
