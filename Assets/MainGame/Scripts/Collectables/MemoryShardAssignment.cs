using System;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// Whether a level hides a memory shard. Lives on the level's <c>LevelConfig</c>, next to
    /// <see cref="RobotPartAssignment"/>, so everything a level holds is authored in one asset.
    ///
    /// This is the switch the design asks for: tick <see cref="placeShard"/> and the level has
    /// a shard, untick it and the <see cref="MemoryShardPickup"/> in the scene hides itself.
    /// A level holds at most one shard — that is what lets the level number be the shard's id
    /// (see <see cref="MemoryShardIds"/>).
    ///
    /// Note there is deliberately nothing here about <i>which story</i> the shard unlocks.
    /// Stories are unlocked by the running total alone, so no level ever owns one.
    /// </summary>
    [Serializable]
    public class MemoryShardAssignment
    {
        [Tooltip("Tick when this level hides a memory shard. Untick and the pickup in the " +
                 "scene hides itself, and the shard stops counting towards the total.")]
        public bool placeShard = false;

        [Tooltip("Leftover from when the five sprites were read as five shards: they are the " +
                 "frames of one spinning shard, and the pickup plays all of them. This only " +
                 "picks the resting frame (1-5, or 0 to spread by level number) for a shard " +
                 "with no SpriteSheetAnimator. Cosmetic either way — every shard counts the same.")]
        [Range(0, MemoryShardIds.VariantCount)]
        public int shardVariant = 0;

        /// <summary>
        /// The 0-based frame a shard with no animator rests on, resolving 0 to the automatic
        /// spread. A spinning shard ignores it.
        /// </summary>
        public int VariantIndex(int levelNumber) =>
            shardVariant <= 0
                ? MemoryShardIds.AutoVariantIndex(levelNumber)
                : Mathf.Clamp(shardVariant, 1, MemoryShardIds.VariantCount) - 1;
    }
}
