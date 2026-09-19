using System.Collections.Generic;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// The one asset holding everything the memory-shard system needs that is not per-level:
    /// the shard artwork and the story thresholds. <see cref="MemoryShardService"/> loads it
    /// from Resources, so pickups and menus reach it without a scene reference.
    ///
    /// Lives at <c>Assets/Resources/MemoryShardDatabase.asset</c> — the path the service looks
    /// for. Rebuild it with Tools ▸ Memory Shards ▸ Run Full Setup.
    ///
    /// <b>This asset is where the design is tuned.</b> The shard counts that unlock stories are
    /// the <c>requiredShards</c> values in <see cref="stories"/>; change them here and nothing
    /// else in the project needs touching.
    /// </summary>
    [CreateAssetMenu(fileName = "MemoryShardDatabase", menuName = "Collectables/Memory Shard Database", order = 2)]
    public class MemoryShardDatabase : ScriptableObject
    {
        [Header("Artwork")]
        [Tooltip("The shard's spin, frame by frame (1.png … 5.png). These are one turning shard " +
                 "— front, edge-on, back — not five different shards, so the order is the " +
                 "animation and 1 is the front-facing frame used wherever a still is needed.")]
        public Sprite[] shardSprites = new Sprite[0];

        /// <summary>
        /// The spin frames in order, or an empty array when the database has no art. This is
        /// what the pickup plays; <see cref="GetShardSprite"/> is for the stills (HUD icon).
        /// </summary>
        public Sprite[] ShardFrames => shardSprites != null ? shardSprites : new Sprite[0];

        [Header("Stories")]
        [Tooltip("Every story and the shard total that unlocks it. Order here does not matter — " +
                 "the service sorts by required shards.")]
        public MemoryStoryEntry[] stories = new MemoryStoryEntry[0];

        /// <summary>Number of story entries.</summary>
        public int StoryCount => stories != null ? stories.Length : 0;

        /// <summary>
        /// The stories in ascending threshold order, skipping empty slots. This is the order
        /// they unlock and the order the Memories list shows them in.
        /// </summary>
        public List<MemoryStoryEntry> StoriesByThreshold()
        {
            var ordered = new List<MemoryStoryEntry>();
            if (stories == null) return ordered;

            foreach (var story in stories)
                if (story != null) ordered.Add(story);

            ordered.Sort((a, b) => a.requiredShards.CompareTo(b.requiredShards));
            return ordered;
        }

        /// <summary>The story with this id, or null when the database has no such entry.</summary>
        public MemoryStoryEntry GetStory(string storyId)
        {
            if (stories == null || string.IsNullOrEmpty(storyId)) return null;
            foreach (var story in stories)
                if (story != null && story.SafeId == storyId) return story;
            return null;
        }

        /// <summary>
        /// A single frame by 0-based index, or null when the database has no art. Frame 0 is
        /// the front-facing one, which is what a still (the HUD icon) wants.
        ///
        /// Out-of-range indices wrap rather than fail, so nothing ends up invisible because the
        /// frame count changed.
        /// </summary>
        public Sprite GetShardSprite(int variantIndex)
        {
            if (shardSprites == null || shardSprites.Length == 0) return null;
            int index = ((variantIndex % shardSprites.Length) + shardSprites.Length) % shardSprites.Length;
            return shardSprites[index];
        }

        /// <summary>
        /// The highest threshold any story asks for — what a "collect them all" goal would be.
        /// 0 when no stories are authored yet.
        /// </summary>
        public int HighestThreshold
        {
            get
            {
                int highest = 0;
                if (stories == null) return highest;
                foreach (var story in stories)
                    if (story != null && story.requiredShards > highest) highest = story.requiredShards;
                return highest;
            }
        }
    }
}
