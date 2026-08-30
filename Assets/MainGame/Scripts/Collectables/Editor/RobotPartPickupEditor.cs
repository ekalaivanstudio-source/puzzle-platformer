using UnityEditor;
using UnityEngine;

namespace Collectables.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="RobotPartPickup"/>.
    ///
    /// A pickup's identity normally comes from the level's <c>LevelConfig.robotPart</c>, not
    /// from the object — so the plain inspector was misleading: the Robot and Part Number
    /// fields looked editable while doing nothing, and there was no sign of what the pickup
    /// would actually turn into at runtime.
    ///
    /// This draws the resolved identity up front and greys out the override fields until
    /// Override Assignment is ticked.
    /// </summary>
    [CustomEditor(typeof(RobotPartPickup))]
    [CanEditMultipleObjects]
    public class RobotPartPickupEditor : Editor
    {
        private SerializedProperty _playerTag;
        private SerializedProperty _renderer;
        private SerializedProperty _applyPartSprite;
        private SerializedProperty _shineEffect;
        private SerializedProperty _collectEffectPrefab;
        private SerializedProperty _overrideAssignment;
        private SerializedProperty _robot;
        private SerializedProperty _partNumber;

        private void OnEnable()
        {
            _playerTag = serializedObject.FindProperty("m_PlayerTag");
            _renderer = serializedObject.FindProperty("m_Renderer");
            _applyPartSprite = serializedObject.FindProperty("m_ApplyPartSprite");
            _shineEffect = serializedObject.FindProperty("m_ShineEffect");
            _collectEffectPrefab = serializedObject.FindProperty("m_CollectEffectPrefab");
            _overrideAssignment = serializedObject.FindProperty("m_OverrideAssignment");
            _robot = serializedObject.FindProperty("m_Robot");
            _partNumber = serializedObject.FindProperty("m_PartNumber");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIdentitySummary();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_playerTag);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_renderer);
            EditorGUILayout.PropertyField(_applyPartSprite);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Feedback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_shineEffect);
            EditorGUILayout.PropertyField(_collectEffectPrefab);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Identity Override", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_overrideAssignment);

            // The whole point: these two only matter with the override on, so they read as
            // disabled rather than as settings that are being quietly ignored.
            using (new EditorGUI.DisabledScope(!_overrideAssignment.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_robot);
                EditorGUILayout.PropertyField(_partNumber);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Shows what this pickup will actually become, and where that comes from. Only drawn
        /// for a single selection — a shared box would be wrong for a multi-edit.
        /// </summary>
        private void DrawIdentitySummary()
        {
            if (serializedObject.isEditingMultipleObjects) return;

            if (_overrideAssignment.boolValue)
            {
                var overridden = (RobotId)_robot.enumValueIndex;
                EditorGUILayout.HelpBox(
                    $"Overridden: {overridden} part {_partNumber.intValue} " +
                    $"({RobotIds.PartKey(overridden, _partNumber.intValue - 1)}).\n" +
                    "This pickup ignores the level's LevelConfig.",
                    MessageType.Warning);
                return;
            }

            var config = FindLevelConfig();
            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "No LevelConfig found for this scene, so the pickup cannot tell which part " +
                    "it is and will hide itself at runtime.\n\n" +
                    "Assign a LevelConfig to the scene's LevelContext, or tick Override Assignment below.",
                    MessageType.Error);
                return;
            }

            var assignment = config.robotPart;
            if (assignment == null || !assignment.placePart)
            {
                EditorGUILayout.HelpBox(
                    $"{config.name} (level {config.levelNumber}) hides no robot part, so this " +
                    "pickup will hide itself at runtime.\n\n" +
                    "Tick Place Part on that config to give this level one.",
                    MessageType.Warning);
                EditorGUILayout.ObjectField("Level Config", config, typeof(LevelConfig), false);
                return;
            }

            EditorGUILayout.HelpBox(
                $"This is {assignment.robot} part {assignment.partNumber} ({assignment.PartKey}).\n\n" +
                $"Set by {config.name} (level {config.levelNumber}) — a level holds one part, so " +
                "change it there, not here.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Level Config", config, typeof(LevelConfig), false);

            if (GUILayout.Button("Select Level Config"))
                Selection.activeObject = config;
        }

        /// <summary>
        /// The config driving this pickup's scene. Uses the live LevelContext in play mode and
        /// searches the loaded scene otherwise, so the summary is right in both.
        /// </summary>
        private static LevelConfig FindLevelConfig()
        {
            if (LevelContext.Instance != null) return LevelContext.Instance.Config;

            var context = Object.FindFirstObjectByType<LevelContext>(FindObjectsInactive.Include);
            return context != null ? context.Config : null;
        }
    }
}
