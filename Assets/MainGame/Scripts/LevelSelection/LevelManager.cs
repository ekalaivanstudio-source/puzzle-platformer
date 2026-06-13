using UnityEngine;

namespace LevelSelectionSystem
{
    /// <summary>
    /// The unlock + completion *rules* of the game. It is the only place that knows how
    /// completing a level translates into saved progress and a freshly unlocked next level.
    ///
    /// Responsibilities:
    ///   • Decide whether a level is unlocked (from the stored unlock pointer + database order).
    ///   • Apply a level completion: mark it done, keep the best star result, unlock the next.
    ///
    /// It delegates ALL persistence to <see cref="SaveManager"/> and reads ordering from a
    /// <see cref="LevelDatabase"/>. It is a static service so gameplay can call it from
    /// anywhere; call <see cref="Configure"/> once at startup to set the active database
    /// (or pass a database explicitly to the overloads for tests / multiple databases).
    /// </summary>
    public static class LevelManager
    {
        private static LevelDatabase s_Database;

        /// <summary>
        /// Registers the active level database and seeds first-run state so that only the
        /// first level is unlocked by default. Call once early (e.g. from the level-select
        /// screen's Awake, or a bootstrap script).
        /// </summary>
        public static void Configure(LevelDatabase database)
        {
            s_Database = database;
            EnsureInitialized(database);
        }

        /// <summary>
        /// On the very first run there is no save yet, so the unlock pointer is a sentinel.
        /// Seed it with the first level's id — that is what makes "only Level 1 unlocked by
        /// default" true without hard-coding anything.
        /// </summary>
        private static void EnsureInitialized(LevelDatabase database)
        {
            if (database == null || database.Count == 0) return;

            if (SaveManager.HighestUnlockedLevelId == int.MinValue)
            {
                SaveManager.HighestUnlockedLevelId = database.FirstLevel.LevelId;
                SaveManager.Save();
            }
        }

        // ─── Queries ────────────────────────────────────────────────────────────

        /// <summary>True if the level is currently playable (it sits at or before the unlock pointer).</summary>
        public static bool IsLevelUnlocked(int levelId) => IsLevelUnlocked(s_Database, levelId);

        /// <inheritdoc cref="IsLevelUnlocked(int)"/>
        public static bool IsLevelUnlocked(LevelDatabase database, int levelId)
        {
            if (database == null) return false;

            int levelIndex = database.IndexOf(levelId);
            if (levelIndex < 0) return false;

            // The first level is always unlocked, even before any save exists.
            if (levelIndex == 0) return true;

            int highestIndex = database.IndexOf(SaveManager.HighestUnlockedLevelId);
            return levelIndex <= highestIndex;
        }

        /// <summary>True if the player has completed the level at least once.</summary>
        public static bool IsLevelCompleted(int levelId)
        {
            LevelProgress p = SaveManager.GetProgress(levelId);
            return p != null && p.IsCompleted;
        }

        /// <summary>Best star count earned on the level, 0 if never completed.</summary>
        public static int GetStars(int levelId)
        {
            LevelProgress p = SaveManager.GetProgress(levelId);
            return p != null ? p.StarCount : 0;
        }

        // ─── Completion ─────────────────────────────────────────────────────────

        /// <summary>
        /// Records the result of finishing a level: marks it completed, keeps the best star
        /// result, unlocks the next level in the database, then persists everything in one write.
        /// Uses the database registered via <see cref="Configure"/>.
        /// </summary>
        /// <param name="levelId">Id of the level that was just completed.</param>
        /// <param name="starsEarned">Stars earned this attempt (0..3).</param>
        public static void CompleteLevel(int levelId, int starsEarned)
            => CompleteLevel(s_Database, levelId, starsEarned);

        /// <inheritdoc cref="CompleteLevel(int,int)"/>
        public static void CompleteLevel(LevelDatabase database, int levelId, int starsEarned)
        {
            if (database == null)
            {
                Debug.LogError("[LevelManager] No database configured. Call LevelManager.Configure(database) first.");
                return;
            }
            if (database.GetById(levelId) == null)
            {
                Debug.LogError($"[LevelManager] LevelId {levelId} is not in the database; ignoring completion.");
                return;
            }

            LevelProgress progress = SaveManager.GetOrCreateProgress(levelId);
            progress.IsCompleted = true;

            // "Keep the best result" — replaying with fewer stars never lowers the record.
            progress.TrySetStars(starsEarned);

            UnlockNextLevel(database, levelId);

            // Single write covers completion, stars and the unlock; fires OnProgressChanged once.
            SaveManager.Save();

            Debug.Log($"[LevelManager] Completed level {levelId} with {progress.StarCount}★ (best). " +
                      $"Highest unlocked is now {SaveManager.HighestUnlockedLevelId}.");
        }

        /// <summary>
        /// Advances the unlock pointer to the next level in the database — but only forward,
        /// so completing an early level again can never roll back later unlocks.
        /// </summary>
        private static void UnlockNextLevel(LevelDatabase database, int completedLevelId)
        {
            LevelData next = database.GetNextLevel(completedLevelId);
            if (next == null) return; // Completed the final level — nothing left to unlock.

            int nextIndex = database.IndexOf(next.LevelId);
            int currentHighestIndex = database.IndexOf(SaveManager.HighestUnlockedLevelId);

            if (nextIndex > currentHighestIndex)
                SaveManager.HighestUnlockedLevelId = next.LevelId;
        }
    }
}
