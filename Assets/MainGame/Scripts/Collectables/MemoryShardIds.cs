using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// The id format for memory shards, and the one place that knows it.
    ///
    /// A shard's identity is <b>the level it is hidden in</b> — "shard_7" — and nothing else.
    /// That is the whole design: unlike a robot part, a shard has no meaning of its own, so
    /// there is no per-shard content to keep in sync. A level hides at most one shard, which
    /// makes the level number a stable, collision-free id that survives the artwork changing,
    /// the shard being dragged somewhere else in the scene, or stories being re-tuned.
    ///
    /// The save file stores these strings, so <see cref="ShardKey"/> must never change format.
    /// </summary>
    public static class MemoryShardIds
    {
        /// <summary>
        /// How many sprites are in <c>Sprites/Collectibles/memory shards/</c> (1.png … 5.png).
        ///
        /// They turned out to be the frames of one shard spinning rather than five different
        /// shards, so in a level this is the length of the animation, and "variant" only means
        /// "which frame a shard that cannot animate stands still on". Either way it is a count
        /// of pictures, never of anything the player can reason about.
        /// </summary>
        public const int VariantCount = 5;

        private const string Prefix = "shard_";

        /// <summary>The stable save id of the shard hidden in a level, e.g. "shard_7".</summary>
        public static string ShardKey(int levelNumber) => Prefix + Mathf.Max(0, levelNumber);

        /// <summary>
        /// The frame a level's shard rests on when the level does not name one, spread across
        /// consecutive levels. 0-based. Only reaches a shard with no animator — a spinning one
        /// plays every frame regardless.
        /// </summary>
        public static int AutoVariantIndex(int levelNumber) =>
            ((Mathf.Max(0, levelNumber) - 1) % VariantCount + VariantCount) % VariantCount;

        /// <summary>True when <paramref name="variantIndex"/> is a real 0-based sprite slot.</summary>
        public static bool IsValidVariantIndex(int variantIndex) =>
            variantIndex >= 0 && variantIndex < VariantCount;
    }
}
