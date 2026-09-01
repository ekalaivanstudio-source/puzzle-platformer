using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// The one asset that lists every collectable robot. <see cref="RobotCollectionService"/>
    /// loads it from Resources, so nothing needs a scene reference to reach robot art or names.
    ///
    /// Lives at <c>Assets/Resources/RobotCollectionDatabase.asset</c> — the path
    /// <see cref="RobotCollectionService"/> looks for. Rebuild it with
    /// Tools ▸ Robot Collection ▸ Run Full Setup.
    /// </summary>
    [CreateAssetMenu(fileName = "RobotCollectionDatabase", menuName = "Collectables/Robot Collection Database", order = 1)]
    public class RobotCollectionDatabase : ScriptableObject
    {
        [Tooltip("Every robot, in the order the collection UI lays them out.")]
        public RobotDefinition[] robots = new RobotDefinition[0];

        /// <summary>Number of robots in the database.</summary>
        public int RobotCount => robots != null ? robots.Length : 0;

        /// <summary>Total parts across every robot in the database.</summary>
        public int TotalParts => RobotCount * RobotIds.PartsPerRobot;

        /// <summary>The definition for a robot, or null when it isn't in the database.</summary>
        public RobotDefinition Get(RobotId robot)
        {
            if (robots == null) return null;
            for (int i = 0; i < robots.Length; i++)
            {
                if (robots[i] != null && robots[i].robot == robot) return robots[i];
            }
            return null;
        }

        /// <summary>The definition at a layout slot, or null when out of range.</summary>
        public RobotDefinition GetAt(int index)
        {
            if (robots == null || index < 0 || index >= robots.Length) return null;
            return robots[index];
        }
    }
}
