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
        }
    }
}
