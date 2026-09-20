using System;
using System.Collections.Generic;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// The single access point for memory shards and the stories they unlock. Pickups, the HUD
    /// counter, the story presenter and any Memories menu all talk to this — nothing else
    /// touches <see cref="MemoryShardSaveSystem"/> or loads the database itself.
    ///
    /// It is static and scene-independent for the same reason
    /// <see cref="RobotCollectionService"/> is: the counts have to be readable on the home
    /// screen, where there is no level and no level manager.
    ///
    /// <b>The unlock rule, in one place:</b> a story is unlocked when the player's total shard
    /// count has reached its <c>requiredShards</c>. Nothing else. No story belongs to a shard
    /// or to a level, and unlock state is never written to disk — <see cref="UnlockedStories"/>
    /// recomputes it from the live thresholds on every call, so the numbers stay tunable right
    /// through production.
    ///
    /// Listeners must unsubscribe in <c>OnDisable</c>/<c>OnDestroy</c> — these are static
    /// events and they outlive the scene that subscribed to them.
    /// </summary>
    public static class MemoryShardService
    {
        private const string DatabaseResourcePath = "MemoryShardDatabase";

        private static MemoryShardDatabase _database;
        private static bool _databaseLookupDone;

        /// <summary>
        /// Raised after a shard is newly collected, with the new running total. Use for
        /// one-shot pickup feedback.
        /// </summary>
        public static event Action<int> OnShardCollected;

        /// <summary>
        /// Raised whenever the collected set changes at all — a pickup or a progress reset.
        /// Use to repaint counters; it fires for resets too, which <see cref="OnShardCollected"/>
        /// does not.
        /// </summary>
        public static event Action OnProgressChanged;

        /// <summary>
        /// Raised the moment a pickup pushes the total over a story's threshold. The story is
        /// unlocked and queued at this point, <b>not</b> played — the cutscene waits for the end
        /// of the level (see <see cref="MemoryStoryPresenter"/>). Use this for the small
        /// "new memory unlocked" toast, not for the story itself.
        /// </summary>
        public static event Action<MemoryStoryEntry> OnStoryUnlocked;

        // ─── Database ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The shard database, loaded once from <c>Resources/MemoryShardDatabase</c>.
        /// Null (with one warning) when the asset is missing.
        /// </summary>
        public static MemoryShardDatabase Database
        {
            get
            {
                if (_database != null) return _database;

                _database = Resources.Load<MemoryShardDatabase>(DatabaseResourcePath);
                if (_database == null && !_databaseLookupDone)
                {
                    Debug.LogWarning(
                        "[MemoryShardService] No MemoryShardDatabase found at " +
                        $"Resources/{DatabaseResourcePath}. Run Tools ▸ Memory Shards ▸ Run Full Setup.");
                }
                _databaseLookupDone = true;
                return _database;
            }
        }

        /// <summary>Drops the cached database so the next access reloads it. Used by the editor tools.</summary>
        public static void InvalidateDatabase()
        {
            _database = null;
            _databaseLookupDone = false;
        }

        /// <summary>One frame of the shard's spin by 0-based index, or null when there is no art.
        /// Frame 0 is the front-facing one — the still to use for an icon.</summary>
        public static Sprite GetShardSprite(int variantIndex) =>
            Database != null ? Database.GetShardSprite(variantIndex) : null;

        /// <summary>
        /// The shard's spin, frame by frame. Empty when there is no art. What the pickup feeds
        /// its <see cref="SpriteSheetAnimator"/>, so changing the art in the database changes
        /// every shard in the game without rebuilding a prefab.
        /// </summary>
        public static Sprite[] ShardFrames =>
            Database != null ? Database.ShardFrames : new Sprite[0];

        // ─── Shard queries ────────────────────────────────────────────────────────

        /// <summary>True if the shard hidden in this level has been picked up.</summary>
        public static bool IsCollected(int levelNumber) =>
            MemoryShardSaveSystem.IsCollected(MemoryShardIds.ShardKey(levelNumber));

        /// <summary>
        /// Shards picked up so far. This is the number every story threshold is measured against.
        /// </summary>
        public static int TotalCollected => MemoryShardSaveSystem.TotalCollected;

        /// <summary>
        /// The shard total the last story asks for — the meaningful denominator for a
        /// "3 / 20" style counter, since collecting past it unlocks nothing further.
        /// 0 when no stories are authored yet.
        /// </summary>
        public static int ShardsForAllStories => Database != null ? Database.HighestThreshold : 0;

        // ─── Story queries ────────────────────────────────────────────────────────

        /// <summary>
        /// Every story the player has earned, in threshold order. Recomputed from the current
        /// shard count each call, so a re-tuned threshold is reflected immediately.
        /// </summary>
        public static List<MemoryStoryEntry> UnlockedStories()
        {
            var unlocked = new List<MemoryStoryEntry>();
            if (Database == null) return unlocked;

            int total = TotalCollected;
            foreach (var story in Database.StoriesByThreshold())
                if (total >= story.requiredShards) unlocked.Add(story);

            return unlocked;
        }

        /// <summary>
        /// Stories that are unlocked but whose cutscene has not played yet, in threshold order.
        /// This is the queue <see cref="MemoryStoryPresenter"/> drains at the end of a level.
        ///
        /// Because it is derived rather than stored, a story unlocked in a session that was quit
        /// before the level ended is still waiting here next time — the unlock cannot be lost,
        /// only deferred.
        /// </summary>
        public static List<MemoryStoryEntry> PendingStories()
        {
            var pending = new List<MemoryStoryEntry>();
            foreach (var story in UnlockedStories())
                if (!MemoryShardSaveSystem.IsStoryShown(story.SafeId)) pending.Add(story);
            return pending;
        }

        /// <summary>True when there is at least one unlocked story still waiting to be played.</summary>
        public static bool HasPendingStory() => PendingStories().Count > 0;

        /// <summary>True once this story's cutscene has played.</summary>
        public static bool IsStoryShown(MemoryStoryEntry story) =>
            story != null && MemoryShardSaveSystem.IsStoryShown(story.SafeId);

        /// <summary>
        /// Records that a story's cutscene has finished, taking it out of
        /// <see cref="PendingStories"/> for good.
        /// </summary>
        public static void MarkStoryShown(MemoryStoryEntry story)
        {
            if (story == null) return;
            if (MemoryShardSaveSystem.MarkStoryShown(story.SafeId))
                OnProgressChanged?.Invoke();
        }

        // ─── Mutations ────────────────────────────────────────────────────────────

        /// <summary>
        /// Records the pickup of this level's shard and notifies listeners, raising
        /// <see cref="OnStoryUnlocked"/> for every story the new total has just reached.
        /// Returns false when the shard was already collected, in which case nothing changes.
        /// </summary>
        public static bool Collect(int levelNumber)
        {
            int before = TotalCollected;

            if (!MemoryShardSaveSystem.MarkCollected(MemoryShardIds.ShardKey(levelNumber))) return false;

            int after = TotalCollected;

            OnShardCollected?.Invoke(after);
            OnProgressChanged?.Invoke();

            // Every threshold crossed by this single pickup — normally one, but two stories
            // sharing a threshold, or a threshold lowered mid-save, can bring several at once.
            if (Database != null)
            {
                foreach (var story in Database.StoriesByThreshold())
                {
                    if (story.requiredShards > before && story.requiredShards <= after)
                        OnStoryUnlocked?.Invoke(story);
                }
            }

            return true;
        }

        /// <summary>Wipes all shard progress and played-story history, and repaints any live UI.</summary>
        public static void ResetAll()
        {
            MemoryShardSaveSystem.ResetAll();
            OnProgressChanged?.Invoke();
        }
    }
}
