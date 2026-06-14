#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TutorialSystem.EditorTools
{
    /// <summary>
    /// Action-aware inspector for a single <see cref="TutorialStepData"/>. It only shows the fields
    /// that matter for the chosen action type (e.g. the auto-advance fields disappear for steps that
    /// wait on a button click), and adds a "pick target from scene" dropdown so designers never have
    /// to type an id by hand. Also used inline by the Tutorial Creator window.
    /// </summary>
    [CustomEditor(typeof(TutorialStepData))]
    public class TutorialStepInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_StepName"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            DrawTargetField(serializedObject.FindProperty("m_TargetId"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);
            SerializedProperty action = serializedObject.FindProperty("m_ActionType");
            EditorGUILayout.PropertyField(action);

            var type = (TutorialActionType)action.enumValueIndex;
            bool eventDriven = type == TutorialActionType.WaitForObjectInteraction ||
                               type == TutorialActionType.DragAndDrop ||
                               type == TutorialActionType.CustomEvent;
            bool tapAdvanced = type == TutorialActionType.PopupOnly ||
                               type == TutorialActionType.Highlight;

            if (eventDriven)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CustomEventId"),
                    new GUIContent("Completion Event Id"));
                EditorGUILayout.HelpBox(
                    "This step completes when TutorialEventBus.Fire(id) is called with the id above " +
                    "(or the Target Id if left blank). Use a TutorialEventTrigger or call it from code.",
                    MessageType.Info);
            }
            else if (type == TutorialActionType.WaitForButtonClick)
            {
                EditorGUILayout.HelpBox(
                    "Completes when the target's UI Button is clicked. The target must be a UI object " +
                    "with a Button component.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Content", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Message"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CharacterSprite"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DimBackground"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShowHighlight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShowArrow"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_HighlightPadding"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PopupAnchor"));

            if (tapAdvanced)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Advance Rules", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AllowTapToContinue"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AutoAdvanceDelay"));
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Draws the target id field plus a "Pick ▾" dropdown of scene targets.</summary>
        public static void DrawTargetField(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent("Target Id"));
                if (GUILayout.Button("Pick ▾", GUILayout.Width(60)))
                    ShowTargetMenu(idProp);
            }
        }

        private static void ShowTargetMenu(SerializedProperty idProp)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(none — full-screen message)"), false,
                () => { idProp.stringValue = ""; idProp.serializedObject.ApplyModifiedProperties(); });
            menu.AddSeparator("");

            TutorialTarget[] targets = Object.FindObjectsOfType<TutorialTarget>();
            if (targets.Length == 0)
                menu.AddDisabledItem(new GUIContent("No TutorialTargets in the open scene"));
            foreach (TutorialTarget t in targets)
            {
                string id = t.TargetId;
                if (string.IsNullOrEmpty(id)) continue;
                menu.AddItem(new GUIContent($"{id}  ({t.name})"), idProp.stringValue == id,
                    () => { idProp.stringValue = id; idProp.serializedObject.ApplyModifiedProperties(); });
            }
            menu.ShowAsContext();
        }
    }

    /// <summary>
    /// Inspector for a <see cref="TutorialSequenceData"/>: identity fields plus a reorderable list of
    /// steps (drag to reorder, +/- to add/remove sub-assets) and a shortcut to the creator window.
    /// </summary>
    [CustomEditor(typeof(TutorialSequenceData))]
    public class TutorialSequenceInspector : Editor
    {
        private ReorderableList m_List;

        private void OnEnable()
        {
            SerializedProperty steps = serializedObject.FindProperty("m_Steps");
            m_List = new ReorderableList(serializedObject, steps, true, true, true, true);
            m_List.drawHeaderCallback = r => EditorGUI.LabelField(r, "Steps (drag to reorder)");
            m_List.drawElementCallback = (rect, i, active, focus) =>
            {
                var step = steps.GetArrayElementAtIndex(i).objectReferenceValue as TutorialStepData;
                rect.y += 2; rect.height = EditorGUIUtility.singleLineHeight;
                string label = step == null
                    ? $"{i + 1}.  (missing step)"
                    : $"{i + 1}.  [{step.ActionType}]  {Trim(step.StepName, 18)} — \"{Trim(step.Message, 40)}\"";
                EditorGUI.LabelField(rect, label);
            };
            m_List.onAddCallback = _ =>
            {
                TutorialCreatorUtility.AddStep((TutorialSequenceData)target);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            };
            m_List.onRemoveCallback = l =>
            {
                TutorialCreatorUtility.RemoveStep((TutorialSequenceData)target, l.index);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            };
            m_List.onReorderCallback = _ =>
            {
                TutorialCreatorUtility.Renumber((TutorialSequenceData)target);
                EditorUtility.SetDirty(target);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SequenceId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Description"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PlayOnce"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Resumable"));
            EditorGUILayout.Space();
            m_List.DoLayoutList();

            if (GUILayout.Button("Open in Tutorial Creator"))
                TutorialCreatorWindow.Open((TutorialSequenceData)target);

            serializedObject.ApplyModifiedProperties();
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
#endif
