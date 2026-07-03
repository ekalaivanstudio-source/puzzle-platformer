using UnityEditor;
using UnityEngine;

namespace ModernLevelSelection
{
    [CustomEditor(typeof(LevelGenerator))]
    public class LevelGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LevelGenerator generator = (LevelGenerator)target;
            GUILayout.Space(8);
            if (GUILayout.Button("Generate Levels"))
            {
                Undo.RecordObject(generator, "Generate Levels");
                generator.Generate();
                EditorUtility.SetDirty(generator);
            }

            if (GUILayout.Button("Clear Generated"))
            {
                Undo.RecordObject(generator, "Clear Generated");
                generator.Clear();
                EditorUtility.SetDirty(generator);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Reset Progress (PlayerPrefs)"))
            {
                if (EditorUtility.DisplayDialog("Reset Progress", "This will clear saved progress in PlayerPrefs. Continue?", "Yes", "No"))
                {
                    SaveManager.ResetProgress();
                    var lm = FindObjectOfType<LevelManager>();
                    lm?.RefreshUI();
                }
            }
        }
    }
}
