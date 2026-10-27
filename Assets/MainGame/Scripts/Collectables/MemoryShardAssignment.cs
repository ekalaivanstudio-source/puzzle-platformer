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

        [Tooltip("Which shard sprite to show (0 to spread by level number). There is one " +
                 "sprite today, so every value looks the same. Cosmetic — every shard counts the same.")]
        [Range(0, MemoryShardIds.VariantCount)]
        public int shardVariant = 0;

        /// <summary>
        /// The 0-based shard sprite index, resolving 0 to the automatic spread.
        /// </summary>
        public int VariantIndex(int levelNumber) =>
            shardVariant <= 0
                ? MemoryShardIds.AutoVariantIndex(levelNumber)
                : Mathf.Clamp(shardVariant, 1, MemoryShardIds.VariantCount) - 1;
    }
}
