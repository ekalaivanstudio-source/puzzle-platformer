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

        [Tooltip("This level's solution, played back by the Auto Play test button. " +
                 "Every entry must be a concrete action — Any is not playable. " +
                 "Leave empty to fall back to the level's correct sequence, which only " +
                 "works when that sequence has no Any (wildcard) slots.")]
        public ActionTypeEnum[] autoPlaySequence = new ActionTypeEnum[0];
    }

    // ─── Tutorial hint settings ───────────────────────────────────────────────────
    [System.Serializable]
    public class TutorialSettings
    {
        [Tooltip("Show this level's on-screen tutorial hints. Off for normal levels — the " +
                 "tutorial canvas ships with Managers, so this checkbox is all a level needs.")]
        public bool showTutorial = false;

        [Tooltip("Hints played on a loop before the player's first input, in this order. " +
                 "Use for a mechanic the key hints cannot teach — e.g. Push on the " +
                 "move-brick level. Leave empty to go straight to the key hints.")]
        public TutorialAnimType[] introHints = new TutorialAnimType[0];

        [Tooltip("Seconds each intro hint stays on screen before the next one.")]
        [Min(0.1f)] public float introHintDuration = 3f;
    }

    [Header("Identity")]
    [Tooltip("Level number. Matches the scene build index: 0 is the Launcher splash, then " +
             "levels run 1..N in build order (Tutorial1-4 are levels 1-4, Level1-9 are 5-13), " +
             "with HomeScreen last.")]
    public int levelNumber = 1;

    [Header("Collectables")]
    [Min(0)] public int robotPartCount = 0;
    [Min(0)] public int memoryShardCount = 0;

    [Header("Camera Follow Dead Zone")]
    public CameraDeadZoneSettings cameraDeadZone = new CameraDeadZoneSettings();

    [Header("Sequence")]
    public SequenceSettings sequence = new SequenceSettings();

    [Header("Tutorial")]
    public TutorialSettings tutorial = new TutorialSettings();
}
