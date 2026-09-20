namespace Collectables
{
    /// <summary>
    /// The four collectable robots. The enum value is the robot's stable numeric id and is
    /// written into save files, so <b>never renumber or reorder these</b> — append only.
    /// </summary>
    public enum RobotId
    {
        Echo = 0,
        Nova = 1,
        Patch = 2,
        Pixel = 3,
    }

    /// <summary>
    /// The identity rules for robots and their parts, in one place.
    ///
    /// A robot's id is its lower-case name ("echo"); a part's id is the robot id plus the
    /// 1-based part number ("echo_3"). Those strings are what the save file stores, so the
    /// format here is the single source of truth for on-disk identity.
    /// </summary>
    public static class RobotIds
    {
        /// <summary>Every robot ships with the same number of parts.</summary>
        public const int PartsPerRobot = 5;

        /// <summary>
        /// The robots that are live in the game, in display order — also the order the UI lays
        /// them out and the order <c>Assign Parts To Levels</c> walks.
        ///
        /// Only PIXEL ships for now. ECHO, NOVA and PATCH are <b>parked, not deleted</b>: their
        /// <see cref="RobotId"/> values, art and <c>RobotDefinition</c> assets all survive, so
        /// bringing one back is a one-line edit here plus a re-run of
        /// Tools ▸ Robot Collection ▸ Run Full Setup.
        /// </summary>
        public static readonly RobotId[] All =
        {
            RobotId.Pixel,
        };

        /// <summary>Total parts in the whole game (live robots x 5 parts).</summary>
        public static int TotalParts => All.Length * PartsPerRobot;

        /// <summary>Stable string id for a robot, e.g. "echo".</summary>
        public static string RobotKey(RobotId robot) => robot.ToString().ToLowerInvariant();

        /// <summary>
        /// Stable string id for one part, e.g. "echo_3". <paramref name="partIndex"/> is
        /// 0-based; the id uses the 1-based number so it matches the source art names.
        /// </summary>
        public static string PartKey(RobotId robot, int partIndex)
            => $"{RobotKey(robot)}_{partIndex + 1}";

        /// <summary>True when <paramref name="partIndex"/> is a valid 0-based part slot.</summary>
        public static bool IsValidPartIndex(int partIndex)
            => partIndex >= 0 && partIndex < PartsPerRobot;
    }
}
