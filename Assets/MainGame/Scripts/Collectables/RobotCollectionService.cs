using System;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// The single access point for robot-part collectables. Pickups, the level HUD and the
    /// home-screen collection tab all talk to this — nothing else touches
    /// <see cref="RobotPartSaveSystem"/> or loads the database itself.
    ///
    /// It is static and scene-independent on purpose: the collection UI has to work in the
    /// HomeScreen, where there is no level and no level manager.
    ///
    /// Listeners must unsubscribe from <see cref="OnProgressChanged"/> in
    /// <c>OnDisable</c>/<c>OnDestroy</c> — a static event outlives the scene that
    /// subscribed to it.
    /// </summary>
    public static class RobotCollectionService
    {
        private const string DatabaseResourcePath = "RobotCollectionDatabase";

        private static RobotCollectionDatabase _database;
        private static bool _databaseLookupDone;

        /// <summary>
        /// Raised after a part is newly collected, with the robot and the 0-based part index.
        /// Use for one-shot feedback (pop animation, sound).
        /// </summary>
        public static event Action<RobotId, int> OnPartCollected;

        /// <summary>
        /// Raised whenever the collected set changes at all — a pickup or a progress reset.
        /// Use to repaint UI; it fires for resets too, which <see cref="OnPartCollected"/> does not.
        /// </summary>
        public static event Action OnProgressChanged;

        // ─── Database ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The robot database, loaded once from <c>Resources/RobotCollectionDatabase</c>.
        /// Null (with one warning) when the asset is missing.
        /// </summary>
        public static RobotCollectionDatabase Database
        {
            get
            {
                if (_database != null) return _database;

                _database = Resources.Load<RobotCollectionDatabase>(DatabaseResourcePath);
                if (_database == null && !_databaseLookupDone)
                {
                    Debug.LogWarning(
                        "[RobotCollectionService] No RobotCollectionDatabase found at " +
                        $"Resources/{DatabaseResourcePath}. Run Tools ▸ Robot Collection ▸ Run Full Setup.");
                }
                _databaseLookupDone = true;
                return _database;
            }
        }

        /// <summary>The definition for a robot, or null when the database is missing it.</summary>
        public static RobotDefinition GetDefinition(RobotId robot) => Database != null ? Database.Get(robot) : null;

        /// <summary>Drops the cached database so the next access reloads it. Used by the editor tools.</summary>
        public static void InvalidateDatabase()
        {
            _database = null;
            _databaseLookupDone = false;
        }

        // ─── Queries ──────────────────────────────────────────────────────────────

        /// <summary>True if this specific part has been picked up.</summary>
        public static bool IsCollected(RobotId robot, int partIndex)
        {
            if (!RobotIds.IsValidPartIndex(partIndex)) return false;
            return RobotPartSaveSystem.IsCollected(RobotIds.PartKey(robot, partIndex));
        }

        /// <summary>How many of one robot's parts have been found (0..5).</summary>
        public static int CollectedCount(RobotId robot) => RobotPartSaveSystem.CountCollected(robot);

        /// <summary>True once every one of a robot's parts has been found.</summary>
        public static bool IsComplete(RobotId robot) => CollectedCount(robot) >= RobotIds.PartsPerRobot;

        /// <summary>Parts found across every robot.</summary>
        public static int TotalCollected => RobotPartSaveSystem.TotalCollected;

        /// <summary>
        /// Parts that exist in the whole game. Comes from the database when one is loaded so
        /// the total tracks the real robot count, else from <see cref="RobotIds.TotalParts"/>.
        /// </summary>
        public static int TotalParts => Database != null && Database.RobotCount > 0
            ? Database.TotalParts
            : RobotIds.TotalParts;

        // ─── Mutations ────────────────────────────────────────────────────────────

        /// <summary>
        /// Records a pickup and notifies listeners. Returns false when the part was already
        /// collected or the index is out of range, in which case nothing changes.
        /// </summary>
        public static bool Collect(RobotId robot, int partIndex)
        {
            if (!RobotIds.IsValidPartIndex(partIndex))
            {
                Debug.LogWarning($"[RobotCollectionService] Part index {partIndex} is out of range for {robot}.");
                return false;
            }

            if (!RobotPartSaveSystem.MarkCollected(RobotIds.PartKey(robot, partIndex))) return false;

            OnPartCollected?.Invoke(robot, partIndex);
            OnProgressChanged?.Invoke();
            return true;
        }

        /// <summary>Wipes all robot-part progress and repaints any live UI.</summary>
        public static void ResetAll()
        {
            RobotPartSaveSystem.ResetAll();
            OnProgressChanged?.Invoke();
        }
    }
}
