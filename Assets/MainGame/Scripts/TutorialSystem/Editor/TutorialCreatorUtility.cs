#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TutorialSystem.EditorTools
{
    /// <summary>
    /// Shared editor helpers for manipulating <see cref="TutorialSequenceData"/> and its
    /// <see cref="TutorialStepData"/> sub-assets. Centralizing it here means the setup tool, the
    /// Tutorial Creator window, and the custom inspectors all create/delete/reorder steps the same
    /// (correct, Undo-friendly) way.
    ///
    /// Steps are stored as sub-assets of the sequence so a tutorial is a single, self-contained
    /// asset file — no loose step assets cluttering the project.
    /// </summary>
    public static class TutorialCreatorUtility
    {
        /// <summary>Creates a new step, parents it under the sequence, and appends it to the list.</summary>
        public static TutorialStepData AddStep(TutorialSequenceData sequence)
        {
            var step = ScriptableObject.CreateInstance<TutorialStepData>();
            step.name = "Step";
            Undo.RegisterCreatedObjectUndo(step, "Add Tutorial Step");
            AssetDatabase.AddObjectToAsset(step, sequence);

            SerializedObject so = new SerializedObject(sequence);
            SerializedProperty list = so.FindProperty("m_Steps");
            int i = list.arraySize;
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = step;
            so.ApplyModifiedProperties();

            Renumber(sequence);
            AssetDatabase.SaveAssets();
            return step;
        }

        /// <summary>Removes the step at <paramref name="index"/> and destroys its sub-asset.</summary>
        public static void RemoveStep(TutorialSequenceData sequence, int index)
        {
            SerializedObject so = new SerializedObject(sequence);
            SerializedProperty list = so.FindProperty("m_Steps");
            if (index < 0 || index >= list.arraySize) return;

            SerializedProperty element = list.GetArrayElementAtIndex(index);
            Object step = element.objectReferenceValue;

            element.objectReferenceValue = null;     // clear the slot first…
            list.DeleteArrayElementAtIndex(index);    // …then remove it (Unity quirk).
            so.ApplyModifiedProperties();

            if (step != null)
            {
                Undo.DestroyObjectImmediate(step);
                AssetDatabase.SaveAssets();
            }
            RenumberAndSave(sequence);
        }

        /// <summary>Moves the step at <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static void MoveStep(TutorialSequenceData sequence, int from, int to)
        {
            SerializedObject so = new SerializedObject(sequence);
            SerializedProperty list = so.FindProperty("m_Steps");
            if (from < 0 || from >= list.arraySize || to < 0 || to >= list.arraySize) return;
            list.MoveArrayElement(from, to);
            so.ApplyModifiedProperties();
            RenumberAndSave(sequence);
        }

        /// <summary>Returns the sequence's steps as a plain list (for iteration in editor code).</summary>
        public static List<TutorialStepData> GetSteps(TutorialSequenceData sequence)
        {
            var result = new List<TutorialStepData>();
            SerializedObject so = new SerializedObject(sequence);
            SerializedProperty list = so.FindProperty("m_Steps");
            for (int i = 0; i < list.arraySize; i++)
                result.Add(list.GetArrayElementAtIndex(i).objectReferenceValue as TutorialStepData);
            return result;
        }

        /// <summary>Keeps the sub-asset names ("Step 1", "Step 2" …) in sync with list order.</summary>
        public static void Renumber(TutorialSequenceData sequence)
        {
            List<TutorialStepData> steps = GetSteps(sequence);
            for (int i = 0; i < steps.Count; i++)
                if (steps[i] != null) steps[i].name = $"Step {i + 1}";
        }

        private static void RenumberAndSave(TutorialSequenceData sequence)
        {
            Renumber(sequence);
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
