using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// The collection rules: what it means to collect a part, when a Robo is complete, and how
    /// completing one Robo unlocks the next. Gameplay calls <see cref="CollectPart"/> when the
    /// player picks up a part; everything else (unlocks, percentages) is derived from the database
    /// + save data. Persistence is delegated to <see cref="CollectionSaveManager"/>.
    ///
    /// Static service so gameplay can collect parts from anywhere; call <see cref="Configure"/>
    /// once (e.g. on the Collections screen or a bootstrap) to register the database and seed the
    /// first Robo as unlocked.
    /// </summary>
    public static class CollectionManager
    {
        private static CollectionDatabase s_Database;

        public static void Configure(CollectionDatabase database)
        {
            s_Database = database;
            EnsureInitialized(database);
        }

        private static void EnsureInitialized(CollectionDatabase database)
        {
            if (database == null || database.Count == 0) return;

            // First Robo is unlocked by default; create its record if the save is fresh.
            RoboData first = database.FirstRobo;
            RoboProgress p = CollectionSaveManager.GetRobo(first.RoboId);
            if (p == null)
            {
                CollectionSaveManager.GetOrCreateRobo(first.RoboId, unlockedIfNew: true);
                CollectionSaveManager.Save();
            }
        }

        // ─── Queries ────────────────────────────────────────────────────────────

        public static bool IsRoboUnlocked(string roboId)
        {
            // The first Robo is always unlocked, even before any save record exists.
            if (s_Database != null && s_Database.FirstRobo != null && s_Database.FirstRobo.RoboId == roboId)
                return true;
            RoboProgress p = CollectionSaveManager.GetRobo(roboId);
            return p != null && p.Unlocked;
        }

        public static bool IsPartCollected(string roboId, string partId)
        {
            RoboProgress p = CollectionSaveManager.GetRobo(roboId);
            return p != null && p.HasPart(partId);
        }

        /// <summary>How many of the Robo's authored parts have been collected.</summary>
        public static int GetCollectedCount(RoboData robo)
        {
            if (robo == null) return 0;
            RoboProgress p = CollectionSaveManager.GetRobo(robo.RoboId);
            if (p == null) return 0;

            int count = 0;
            foreach (RobotPartData part in robo.Parts)
                if (part != null && p.HasPart(part.PartId)) count++;
            return count;
        }

        /// <summary>0..1 completion fraction for the Robo.</summary>
        public static float GetCompletion(RoboData robo)
        {
            if (robo == null || robo.PartCount == 0) return 0f;
            return (float)GetCollectedCount(robo) / robo.PartCount;
        }

        public static bool IsRoboComplete(RoboData robo) =>
            robo != null && robo.PartCount > 0 && GetCollectedCount(robo) >= robo.PartCount;

        // ─── Mutations ──────────────────────────────────────────────────────────

        /// <summary>
        /// Records that the player collected a part. If this completes the Robo, the next Robo in
        /// the database is unlocked. Persists once. Uses the database from <see cref="Configure"/>.
        /// </summary>
        public static void CollectPart(string roboId, string partId) =>
            CollectPart(s_Database, roboId, partId);

        /// <inheritdoc cref="CollectPart(string,string)"/>
        public static void CollectPart(CollectionDatabase database, string roboId, string partId)
        {
            if (database == null)
            {
                Debug.LogError("[CollectionManager] No database configured. Call CollectionManager.Configure() first.");
                return;
            }

            RoboData robo = database.GetById(roboId);
            if (robo == null)
            {
                Debug.LogError($"[CollectionManager] Robo '{roboId}' not in database.");
                return;
            }

            RoboProgress progress = CollectionSaveManager.GetOrCreateRobo(roboId, unlockedIfNew: true);
            bool added = progress.AddPart(partId);
            if (!added)
            {
                // Already had it — nothing changed, no need to save.
                return;
            }

            if (IsRoboComplete(robo))
                UnlockNext(database, roboId);

            CollectionSaveManager.Save();
            Debug.Log($"[CollectionManager] Collected '{partId}' for {roboId} " +
                      $"({GetCollectedCount(robo)}/{robo.PartCount}).");
        }

        private static void UnlockNext(CollectionDatabase database, string completedRoboId)
        {
            RoboData next = database.GetNext(completedRoboId);
            if (next == null) return; // last Robo — nothing to unlock.

            RoboProgress nextProgress = CollectionSaveManager.GetOrCreateRobo(next.RoboId);
            if (!nextProgress.Unlocked)
            {
                nextProgress.Unlocked = true;
                Debug.Log($"[CollectionManager] {completedRoboId} complete → unlocked {next.RoboId}.");
            }
        }
    }
}
