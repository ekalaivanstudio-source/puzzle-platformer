using System;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// Which robot part is hidden in one level. Lives on that level's <c>LevelConfig</c>, so
    /// a scene's part is authored in the same asset as the rest of its level data.
    ///
    /// A level holds at most one part — that is the design rule, and it is why this is a
    /// single value rather than a list. <see cref="RobotPartPickup"/> reads it to work out
    /// what it represents, so moving the pickup object around a scene never changes its identity.
    /// </summary>
    [Serializable]
    public class RobotPartAssignment
    {
        [Tooltip("Off when this level hides no robot part. The pickup then hides itself.")]
        public bool placePart = false;

        [Tooltip("Which robot the part hidden in this level belongs to.")]
        public RobotId robot = RobotId.Echo;

        [Tooltip("Which of the robot's five parts this level hides (1-5).")]
        [Range(1, RobotIds.PartsPerRobot)]
        public int partNumber = 1;

        /// <summary>The 0-based index into the robot's part arrays.</summary>
        public int PartIndex => Mathf.Clamp(partNumber, 1, RobotIds.PartsPerRobot) - 1;

        /// <summary>The stable save id of the assigned part, e.g. "echo_3".</summary>
        public string PartKey => RobotIds.PartKey(robot, PartIndex);
    }
}
