using UnityEngine;

/// <summary>
/// All designer-tunable data for a single level, in one asset. Create one per level
/// (Level1Config, Level2Config, …) and assign it to that scene's <see cref="LevelContext"/>.
/// Every system that needs level data reads it from here, so a level's settings live in
/// exactly one place.
///
/// Create via: Assets ▸ Create ▸ Level ▸ Level Config,
/// or Tools ▸ Collectables ▸ Collectable Tools ▸ Create Level Config.
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Level/Level Config", order = 0)]
public class LevelConfig : ScriptableObject
{
    // ─── Camera dead-zone follow settings ─────────────────────────────────────────
    [System.Serializable]
    public class CameraDeadZoneSettings
    {
        public float deadZoneX = 0.1f;
        public float deadZoneY = 0.1f;
        public Vector2 offset = Vector2.zero;
        [Min(0f)] public float smoothTime = 1f;
        public float minX = 0f;
        public float maxX = 1f;
        public float minY = 0f;
        public float maxY = 1f;
        public bool followX = true;
        public bool followY = true;
    }

    // ─── Sequence (command queue) settings ────────────────────────────────────────
    [System.Serializable]
    public class SequenceSettings
    {
        [Min(1)] public int maxSequenceLength = 6;
        [Tooltip("When true the player must fill every slot before Enter.")]
        public bool requireFullSequence = false;
    }

    [Header("Identity")]
    [Tooltip("Level number. Matches the scene build index (Home = 0, Level1 = 1, ...).")]
    public int levelNumber = 1;

    [Header("Collectables")]
    [Min(0)] public int robotPartCount = 0;
    [Min(0)] public int memoryShardCount = 0;

    [Header("Camera Follow Dead Zone")]
    public CameraDeadZoneSettings cameraDeadZone = new CameraDeadZoneSettings();

    [Header("Sequence")]
    public SequenceSettings sequence = new SequenceSettings();
}
