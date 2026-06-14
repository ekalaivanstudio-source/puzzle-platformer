#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TutorialSystem.EditorTools
{
    /// <summary>
    /// The visual Tutorial Creator: a single window where a designer builds an entire tutorial with
    /// no code. From here you can run the one-click system setup, create/select a sequence asset,
    /// add & drag-reorder steps, edit a step inline, assign a target straight from the scene
    /// selection, validate references, reset progress, and (in Play Mode) play the sequence to test.
    ///
    /// Open via:  Tools ▸ Tutorial System ▸ Tutorial Creator
    /// </summary>
    public class TutorialCreatorWindow : EditorWindow
    {
        private TutorialSequenceData m_Sequence;
        private SerializedObject m_SequenceSO;
        private ReorderableList m_List;
        private Editor m_StepEditor;
        private int m_CachedStepIndex = -1;
        private Vector2 m_ScrollLeft, m_ScrollRight;
        private readonly List<string> m_Issues = new List<string>();

        [MenuItem("Tools/Tutorial System/Tutorial Creator", priority = 1)]
        public static void Open() => Open(null);

        public static void Open(TutorialSequenceData sequence)
        {
            var w = GetWindow<TutorialCreatorWindow>("Tutorial Creator");
            w.minSize = new Vector2(560f, 520f);
            if (sequence != null) w.Select(sequence);
            w.Show();
        }

        private void OnEnable()
        {
            if (m_Sequence != null) Select(m_Sequence);
        }

        private void OnDisable()
        {
            if (m_StepEditor != null) DestroyImmediate(m_StepEditor);
        }

        // ─── Sequence selection / list building ─────────────────────────────────────

        private void Select(TutorialSequenceData sequence)
        {
            m_Sequence = sequence;
            m_CachedStepIndex = -1;
            if (m_StepEditor != null) { DestroyImmediate(m_StepEditor); m_StepEditor = null; }

            if (sequence == null) { m_SequenceSO = null; m_List = null; return; }

            m_SequenceSO = new SerializedObject(sequence);
            SerializedProperty steps = m_SequenceSO.FindProperty("m_Steps");
            m_List = new ReorderableList(m_SequenceSO, steps, true, true, false, false);
            m_List.drawHeaderCallback = r => EditorGUI.LabelField(r, "Steps  (drag to reorder)");
            m_List.drawElementCallback = (rect, i, active, focus) =>
            {
                var step = steps.GetArrayElementAtIndex(i).objectReferenceValue as TutorialStepData;
                rect.y += 2; rect.height = EditorGUIUtility.singleLineHeight;
                string label = step == null
                    ? $"{i + 1}.  (missing)"
                    : $"{i + 1}.  [{step.ActionType}]  {Trim(step.StepName, 16)}";
                EditorGUI.LabelField(rect, label);
            };
            m_List.onReorderCallback = _ =>
            {
                TutorialCreatorUtility.Renumber(m_Sequence);
                EditorUtility.SetDirty(m_Sequence);
            };
            m_List.index = m_Sequence.StepCount > 0 ? 0 : -1;
        }

        // ─── GUI ────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();

            if (m_Sequence == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "Pick a Tutorial Sequence above, or click 'New Sequence'.\n\n" +
                    "First time? Click 'Setup Tutorial System' to build the in-scene rig and a " +
                    "sample tutorial.", MessageType.Info);
                return;
            }

            if (m_SequenceSO == null) Select(m_Sequence);
            m_SequenceSO.Update();

            DrawIdentity();

            EditorGUILayout.BeginHorizontal();
            DrawStepList();    // left column
            DrawStepEditor();  // right column
            EditorGUILayout.EndHorizontal();

            m_SequenceSO.ApplyModifiedProperties();

            DrawActions();
            DrawValidation();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Setup Tutorial System", EditorStyles.toolbarButton, GUILayout.Width(160)))
                    TutorialSystemSetup.SetupTutorialSystem();

                if (GUILayout.Button("New Sequence", EditorStyles.toolbarButton, GUILayout.Width(110)))
                    CreateNewSequence();

                GUILayout.Space(8);
                EditorGUI.BeginChangeCheck();
                var picked = (TutorialSequenceData)EditorGUILayout.ObjectField(
                    m_Sequence, typeof(TutorialSequenceData), false);
                if (EditorGUI.EndChangeCheck()) Select(picked);

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawIdentity()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(m_SequenceSO.FindProperty("m_SequenceId"));
                EditorGUILayout.PropertyField(m_SequenceSO.FindProperty("m_PlayOnce"));
                EditorGUILayout.PropertyField(m_SequenceSO.FindProperty("m_Resumable"));
            }
        }

        private void DrawStepList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
            {
                m_ScrollLeft = EditorGUILayout.BeginScrollView(m_ScrollLeft);
                m_List.DoLayoutList();
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Add Step"))
                    {
                        TutorialCreatorUtility.AddStep(m_Sequence);
                        Select(m_Sequence);
                        m_List.index = m_Sequence.StepCount - 1;
                    }
                    using (new EditorGUI.DisabledScope(m_List.index < 0))
                    {
                        if (GUILayout.Button("- Remove") &&
                            EditorUtility.DisplayDialog("Remove Step", "Delete this step?", "Delete", "Cancel"))
                        {
                            TutorialCreatorUtility.RemoveStep(m_Sequence, m_List.index);
                            Select(m_Sequence);
                        }
                    }
                }
            }
        }

        private void DrawStepEditor()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int idx = m_List != null ? m_List.index : -1;
                TutorialStepData step = m_Sequence.GetStep(idx);
                if (step == null)
                {
                    EditorGUILayout.LabelField("Select a step to edit it.", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                if (idx != m_CachedStepIndex || m_StepEditor == null || m_StepEditor.target != step)
                {
                    if (m_StepEditor != null) DestroyImmediate(m_StepEditor);
                    m_StepEditor = Editor.CreateEditor(step);
                    m_CachedStepIndex = idx;
                }

                m_ScrollRight = EditorGUILayout.BeginScrollView(m_ScrollRight);
                m_StepEditor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(m_List == null || m_List.index < 0 ||
                                                   Selection.activeGameObject == null))
                {
                    if (GUILayout.Button("Assign Scene Selection As Target"))
                        AssignSelectionAsTarget();
                }
                using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
                {
                    if (GUILayout.Button("Make Selection A Target"))
                        MakeSelectionATarget();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate")) Validate();
                if (GUILayout.Button("Reset Saved Progress"))
                    TutorialSaveSystem.ResetSequence(m_Sequence.SequenceId);

                using (new EditorGUI.DisabledScope(!Application.isPlaying ||
                                                   TutorialManager.Instance == null))
                {
                    if (GUILayout.Button("▶ Play This Sequence"))
                        TutorialManager.Instance.PlaySequence(m_Sequence, force: true);
                }
            }
            if (!Application.isPlaying)
                EditorGUILayout.LabelField("Enter Play Mode to test the sequence live.",
                    EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawValidation()
        {
            if (m_Issues.Count == 0) return;
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Validation: {m_Issues.Count} issue(s)", EditorStyles.boldLabel);
                foreach (string issue in m_Issues)
                    EditorGUILayout.LabelField("• " + issue, EditorStyles.wordWrappedMiniLabel);
            }
        }

        // ─── Actions ─────────────────────────────────────────────────────────────────

        private void AssignSelectionAsTarget()
        {
            GameObject go = Selection.activeGameObject;
            TutorialTarget t = go.GetComponent<TutorialTarget>();
            if (t == null)
            {
                t = Undo.AddComponent<TutorialTarget>(go);
                TutorialTargetTagger.SetId(t, TutorialTargetTagger.GenerateId(go.name));
                EditorUtility.SetDirty(t);
            }

            TutorialStepData step = m_Sequence.GetStep(m_List.index);
            if (step == null) return;
            TutorialSystemSetup.SetString(step, "m_TargetId", t.TargetId);
            if (m_StepEditor != null) m_StepEditor.serializedObject.Update();
            Debug.Log($"[TutorialCreator] Step {m_List.index + 1} now targets '{t.TargetId}' ({go.name}).");
        }

        private void MakeSelectionATarget()
        {
            GameObject go = Selection.activeGameObject;
            if (go.GetComponent<TutorialTarget>() != null) return;
            TutorialTarget t = Undo.AddComponent<TutorialTarget>(go);
            TutorialTargetTagger.SetId(t, TutorialTargetTagger.GenerateId(go.name));
            EditorUtility.SetDirty(t);
        }

        private void Validate()
        {
            m_Issues.Clear();
            if (string.IsNullOrEmpty(m_Sequence.SequenceId))
                m_Issues.Add("Sequence has no Sequence Id (needed for save tracking).");

            // Map of scene target ids so we can flag references that won't resolve.
            var sceneIds = new HashSet<string>();
            foreach (TutorialTarget t in Object.FindObjectsOfType<TutorialTarget>())
                if (!string.IsNullOrEmpty(t.TargetId)) sceneIds.Add(t.TargetId);

            List<TutorialStepData> steps = TutorialCreatorUtility.GetSteps(m_Sequence);
            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStepData s = steps[i];
                int n = i + 1;
                if (s == null) { m_Issues.Add($"Step {n}: missing asset."); continue; }
                if (string.IsNullOrEmpty(s.Message))
                    m_Issues.Add($"Step {n}: empty message.");

                bool needsTarget = s.ActionType != TutorialActionType.PopupOnly;
                if (needsTarget && !s.HasTarget)
                    m_Issues.Add($"Step {n}: action '{s.ActionType}' needs a Target Id but none is set.");
                if (s.HasTarget && !sceneIds.Contains(s.TargetId))
                    m_Issues.Add($"Step {n}: target '{s.TargetId}' is not in the open scene " +
                                 "(ok if it lives in another scene loaded at runtime).");
            }
            if (m_Issues.Count == 0) m_Issues.Add("No issues found. ✔");
        }

        private void CreateNewSequence()
        {
            TutorialSystemSetup.EnsureFolder(TutorialSystemSetup.DataFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(TutorialSystemSetup.DataFolder, "Tutorial_New.asset"));
            var seq = CreateInstance<TutorialSequenceData>();
            AssetDatabase.CreateAsset(seq, path);
            TutorialSystemSetup.SetString(seq, "m_SequenceId", Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
            AssetDatabase.SaveAssets();
            Select(seq);
            Selection.activeObject = seq;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
#endif
