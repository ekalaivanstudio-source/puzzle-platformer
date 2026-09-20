using System;
using UnityEngine;
using UnityEngine.Video;

namespace Collectables
{
    /// <summary>
    /// One story the player earns by reaching a shard count. Authored in
    /// <see cref="MemoryShardDatabase"/>.
    ///
    /// The unlock rule is only ever "<see cref="requiredShards"/> shards collected in total".
    /// No story is tied to a particular shard or a particular level, so shards can be added,
    /// moved or removed and these entries keep working untouched.
    ///
    /// <see cref="requiredShards"/> is expected to change while the story is being written —
    /// that is supported. Nothing about an unlock is ever baked into the save file: what is
    /// unlocked is recomputed from the live thresholds every time it is asked for
    /// (see <see cref="MemoryShardService"/>), so re-tuning a number takes effect immediately,
    /// even for a player mid-save.
    /// </summary>
    [Serializable]
    public class MemoryStoryEntry
    {
        [Tooltip("Stable id, e.g. \"memory_01\". Written to the save file to remember that this " +
                 "story has already been played, so RENAMING IT REPLAYS THE STORY. Keep it " +
                 "stable even when the threshold or the clip changes.")]
        public string id = "memory_01";

        [Tooltip("Total memory shards the player must have collected for this story to unlock. " +
                 "Safe to re-tune at any time — unlocks are recomputed from this, never stored.")]
        [Min(1)] public int requiredShards = 5;

        [Tooltip("Name shown in the Memories list and in the 'new memory unlocked' toast. " +
                 "Not shown during the video itself.")]
        public string title = "Memory";

        [Tooltip("The cutscene played when this story unlocks. Leave empty to unlock the entry " +
                 "without playing anything — useful while the video is still being made.")]
        public VideoClip clip;

        /// <summary>True when this entry is complete enough to be worth playing.</summary>
        public bool HasClip => clip != null;

        /// <summary>A usable id even if the asset was left with an empty one.</summary>
        public string SafeId => string.IsNullOrWhiteSpace(id) ? $"memory_at_{requiredShards}" : id.Trim();
    }
}
