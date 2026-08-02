namespace Collectables
{
    /// <summary>
    /// One Memory-Shard milestone tier: a contiguous range of levels whose shards
    /// count toward a single story unlock. When the player has collected
    /// <see cref="Required"/> shards from levels in [<see cref="FromLevel"/>, <see cref="ToLevel"/>],
    /// the story identified by <see cref="StoryId"/> is unlocked.
    /// </summary>
    public readonly struct MemoryShardTier
    {
        public readonly int FromLevel;
        public readonly int ToLevel;
        public readonly int Required;
        public readonly string StoryId;

        public MemoryShardTier(int fromLevel, int toLevel, int required, string storyId)
        {
            FromLevel = fromLevel;
            ToLevel = toLevel;
            Required = required;
            StoryId = storyId;
        }

        public bool Contains(int level) => level >= FromLevel && level <= ToLevel;
    }

    /// <summary>
    /// Central, hand-edited configuration for the collectable systems.
    ///
    /// NOTHING here is derived from gameplay — these are the design "dials".
    /// To change how many shards a story needs, or to add a new story tier for a
    /// future level range, edit <see cref="MemoryShardTiers"/> below. To change the
    /// Robot Parts grand total shown in the HUD (e.g. 0/56), edit
    /// <see cref="RobotPartsGrandTotal"/>.
    ///
    /// Per-level placement counts (how many parts/shards are physically dropped in
    /// each level) live in each level's <c>LevelConfig</c> ScriptableObject instead,
    /// because those are content, not global rules.
    /// </summary>
    public static class CollectableConstants
    {
        /// <summary>
        /// Total number of Robot Parts in the whole game. Drives the "x/56" HUD label.
        /// Keep this in sync with the sum of the per-level counts in the LevelConfig assets.
        /// </summary>
        public const int RobotPartsGrandTotal = 56;

        /// <summary>
        /// Memory-Shard story tiers, in ascending level order.
        ///
        /// Example current design:
        ///   • Levels  1–12 → collect  6 shards → unlocks "Story_1"
        ///   • Levels 13–50 → collect 20 shards → unlocks "Story_2"
        ///
        /// These numbers are NOT fixed for the whole game — add / edit rows as the
        /// game grows. Ranges should not overlap.
        /// </summary>
        public static readonly MemoryShardTier[] MemoryShardTiers =
        {
            new MemoryShardTier(fromLevel: 1,  toLevel: 20, required: 40,  storyId: "Story_1"),
        };

        /// <summary>
        /// Returns the tier a level belongs to. Returns false if the level is not
        /// covered by any configured tier.
        /// </summary>
        public static bool TryGetTierForLevel(int level, out MemoryShardTier tier)
        {
            for (int i = 0; i < MemoryShardTiers.Length; i++)
            {
                if (MemoryShardTiers[i].Contains(level))
                {
                    tier = MemoryShardTiers[i];
                    return true;
                }
            }

            tier = default;
            return false;
        }
    }
}
