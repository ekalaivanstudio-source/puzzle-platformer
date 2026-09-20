using System;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// What a level hides and whether the player already has it — the single access point for
    /// any UI that draws a level it is not standing in.
    ///
    /// The two collectable systems each answer half the question, and neither knows about
    /// levels it is not running in: <see cref="RobotCollectionService"/> knows whether
    /// <c>pixel_3</c> is collected but not which level holds it, and the level that holds it is
    /// authored on a <c>LevelConfig</c> that is normally only reachable through that level's
    /// own scene. This joins the two through <see cref="LevelConfigDatabase"/> so the level
    /// selection map can ask one question — <see cref="For"/> — and get everything it needs to
    /// draw a node.
    ///
    /// Nothing here is stored. Every call reads the live assignment and the live save, so a
    /// re-assignment in a LevelConfig, or a Reset Progress, shows up immediately.
    /// </summary>
    public static class LevelCollectableService
    {
        #region Types

        /// <summary>
        /// Everything one level holds, resolved against the current save. A level holds at most
        /// one robot part and at most one memory shard, which is what makes this a flat struct
        /// rather than a list.
        /// </summary>
        public readonly struct LevelCollectables
        {
            /// <summary>True when this level hides a robot part belonging to a live robot.</summary>
            public readonly bool HasRobotPart;

            /// <summary>Which robot the hidden part belongs to. Only meaningful when <see cref="HasRobotPart"/>.</summary>
            public readonly RobotId Robot;

            /// <summary>0-based index of the hidden part. Only meaningful when <see cref="HasRobotPart"/>.</summary>
            public readonly int PartIndex;

            /// <summary>True when that part is already in the collection.</summary>
            public readonly bool RobotPartCollected;

            /// <summary>True when this level hides a memory shard.</summary>
            public readonly bool HasMemoryShard;

            /// <summary>True when that shard is already picked up.</summary>
            public readonly bool MemoryShardCollected;

            public LevelCollectables(bool hasRobotPart, RobotId robot, int partIndex, bool robotPartCollected,
                                     bool hasMemoryShard, bool memoryShardCollected)
            {
                HasRobotPart = hasRobotPart;
                Robot = robot;
                PartIndex = partIndex;
                RobotPartCollected = robotPartCollected;
                HasMemoryShard = hasMemoryShard;
                MemoryShardCollected = memoryShardCollected;
            }

            /// <summary>True when the level hides anything at all. UI hides itself when this is false.</summary>
            public bool Any => HasRobotPart || HasMemoryShard;

            /// <summary>How many collectables the level hides (0-2).</summary>
            public int Total => (HasRobotPart ? 1 : 0) + (HasMemoryShard ? 1 : 0);

            /// <summary>How many of them are already found (0-2).</summary>
            public int Collected =>
                (HasRobotPart && RobotPartCollected ? 1 : 0) +
                (HasMemoryShard && MemoryShardCollected ? 1 : 0);

            /// <summary>True when the level hides something and all of it is found.</summary>
            public bool IsComplete => Any && Collected == Total;
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised whenever either collectable system's progress changes, so a screen showing
        /// many levels repaints from one subscription instead of two.
        ///
        /// <b>Static</b> — unsubscribe in OnDisable/OnDestroy or a destroyed object keeps being
        /// called for the rest of the session.
        /// </summary>
        public static event Action OnProgressChanged
        {
            add { EnsureHooked(); s_ProgressChanged += value; }
            remove { s_ProgressChanged -= value; }
        }

        private static Action s_ProgressChanged;
        private static bool s_Hooked;

        #endregion

        #region Database

        private static LevelConfigDatabase s_Database;

        /// <summary>
        /// The level config database, loaded from Resources on first use. Null when the asset is
        /// missing — every query below then reports "this level hides nothing", so a missing
        /// database costs the indicators, never an exception.
        /// </summary>
        public static LevelConfigDatabase Database
        {
            get
            {
                if (s_Database == null)
                {
                    s_Database = Resources.Load<LevelConfigDatabase>(LevelConfigDatabase.ResourcePath);
                    if (s_Database == null)
                    {
                        Debug.LogWarning("[LevelCollectableService] No LevelConfigDatabase at Resources/" +
                                         LevelConfigDatabase.ResourcePath + ". Run " +
                                         "Tools ▸ Level Collectables ▸ Run Full Setup. The level " +
                                         "selection map will show no collectable indicators.");
                    }
                }
                return s_Database;
            }
        }

        /// <summary>Drops the cached database so the next call reloads it. Used by the editor tools.</summary>
        public static void InvalidateDatabase()
        {
            s_Database = null;
        }

        #endregion

        #region Queries

        /// <summary>
        /// What <paramref name="levelNumber"/> hides, resolved against the current save. An
        /// unknown level, or one with no assignments, comes back empty.
        /// </summary>
        public static LevelCollectables For(int levelNumber)
        {
            LevelConfigDatabase database = Database;
            LevelConfig config = database != null ? database.Get(levelNumber) : null;
            if (config == null) return default;

            bool hasPart = false;
            RobotId robot = default;
            int partIndex = 0;
            bool partCollected = false;

            RobotPartAssignment part = config.robotPart;
            if (part != null && part.placePart && IsLiveRobot(part.robot))
            {
                hasPart = true;
                robot = part.robot;
                partIndex = part.PartIndex;
                partCollected = RobotCollectionService.IsCollected(robot, partIndex);
            }

            bool hasShard = config.memoryShard != null && config.memoryShard.placeShard;
            bool shardCollected = hasShard && MemoryShardService.IsCollected(levelNumber);

            return new LevelCollectables(hasPart, robot, partIndex, partCollected, hasShard, shardCollected);
        }

        /// <summary>
        /// The icon for a level's robot part: the artist's full pickup artwork, with the whole
        /// robot dark and this part lit. The UI layer sprites are deliberately not used here —
        /// several are only a handful of pixels and vanish at icon size, which is the same
        /// reason the world pickups use the pickup art.
        /// </summary>
        public static Sprite RobotPartIcon(RobotId robot, int partIndex)
        {
            RobotDefinition definition = RobotCollectionService.GetDefinition(robot);
            return definition != null ? definition.GetPickupSprite(partIndex) : null;
        }

        /// <summary>
        /// The icon for a memory shard: frame 1 of the spin, the front-facing one. A still shard
        /// should face the player — the other frames are the edge-on sliver and the
        /// three-quarter turns, which read as a different object at icon size.
        /// </summary>
        public static Sprite MemoryShardIcon() => MemoryShardService.GetShardSprite(0);

        #endregion

        #region Private

        /// <summary>
        /// True for a robot that is currently in the game. A parked robot's leftover assignment
        /// on an old LevelConfig must not draw an indicator for a part that cannot be collected.
        /// </summary>
        private static bool IsLiveRobot(RobotId robot)
        {
            for (int i = 0; i < RobotIds.All.Length; i++)
            {
                if (RobotIds.All[i] == robot) return true;
            }
            return false;
        }

        /// <summary>
        /// Subscribes to both collectable systems once, the first time anyone listens here.
        /// These handlers are static and live for the session by design: the forwarding is what
        /// lets consumers hold a single subscription, and there is nothing to leak.
        /// </summary>
        private static void EnsureHooked()
        {
            if (s_Hooked) return;
            s_Hooked = true;

            RobotCollectionService.OnProgressChanged += RaiseProgressChanged;
            MemoryShardService.OnProgressChanged += RaiseProgressChanged;
        }

        private static void RaiseProgressChanged() => s_ProgressChanged?.Invoke();

        #endregion
    }
}
