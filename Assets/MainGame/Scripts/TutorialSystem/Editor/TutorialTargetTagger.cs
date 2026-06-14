#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TutorialSystem.EditorTools
{
    /// <summary>
    /// Scene-side tooling for <see cref="TutorialTarget"/>:
    ///   • A right-click / menu action that turns any selected UI or world object into a tutorial
    ///     target with an auto-generated unique id.
    ///   • A custom inspector with Generate / Copy id buttons and a live duplicate-id warning.
    ///
    /// This is what makes "click an object → it's now a tutorial target" possible without typing.
    /// </summary>
    public static class TutorialTargetTagger
    {
        [MenuItem("GameObject/Tutorial/Convert To Tutorial Target", false, 10)]
        private static void ConvertSelection()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                TutorialTarget t = go.GetComponent<TutorialTarget>();
                if (t == null)
                {
                    t = Undo.AddComponent<TutorialTarget>(go);
                    SetId(t, GenerateId(go.name));
                }
                EditorUtility.SetDirty(t);
            }
            if (Selection.activeGameObject != null)
                EditorGUIUtility.PingObject(Selection.activeGameObject);
        }

        [MenuItem("GameObject/Tutorial/Convert To Tutorial Target", true)]
        private static bool ConvertSelectionValidate() => Selection.activeGameObject != null;

        /// <summary>Generates a readable, reasonably-unique id: "play_button_3f9a".</summary>
        public static string GenerateId(string baseName)
        {
            var sb = new StringBuilder();
            foreach (char c in baseName.ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            string slug = sb.ToString().Trim('_');
            if (string.IsNullOrEmpty(slug)) slug = "target";
            string suffix = System.Guid.NewGuid().ToString("N").Substring(0, 4);
            return $"{slug}_{suffix}";
        }

        internal static void SetId(TutorialTarget target, string id)
        {
            var so = new SerializedObject(target);
            so.FindProperty("m_TargetId").stringValue = id;
            so.ApplyModifiedProperties();
        }
    }

    /// <summary>Custom inspector for <see cref="TutorialTarget"/> with id helpers + dup detection.</summary>
    [CustomEditor(typeof(TutorialTarget))]
    [CanEditMultipleObjects]
    public class TutorialTargetInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty idProp = serializedObject.FindProperty("m_TargetId");

            EditorGUILayout.PropertyField(idProp, new GUIContent("Target Id",
                "Stable, unique id that tutorial steps use to find this object."));

            var target = (TutorialTarget)this.target;
            string kind = (target.transform is RectTransform) ? "UI element" : "World object";
            EditorGUILayout.LabelField("Detected as", kind, EditorStyles.miniLabel);

            // Duplicate id warning (checks the loaded scenes).
            if (!string.IsNullOrEmpty(idProp.stringValue) && CountWithId(idProp.stringValue) > 1)
            {
                EditorGUILayout.HelpBox(
                    $"Another TutorialTarget already uses the id '{idProp.stringValue}'. " +
                    "Ids must be unique — generate a new one.", MessageType.Warning);
            }
            if (string.IsNullOrEmpty(idProp.stringValue))
            {
                EditorGUILayout.HelpBox("This target has no id and cannot be referenced.",
                    MessageType.Error);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Id"))
                    idProp.stringValue = TutorialTargetTagger.GenerateId(target.name);
                if (GUILayout.Button("Copy Id"))
                    EditorGUIUtility.systemCopyBuffer = idProp.stringValue;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static int CountWithId(string id)
        {
            int count = 0;
            foreach (TutorialTarget t in Object.FindObjectsOfType<TutorialTarget>())
                if (t.TargetId == id) count++;
            return count;
        }
    }
}
#endif
