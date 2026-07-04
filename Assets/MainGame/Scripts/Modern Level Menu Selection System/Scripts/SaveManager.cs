using System;
using UnityEngine;

namespace ModernLevelSelection
{
    /// <summary>
    /// Persistent save manager using PlayerPrefs. Stores highest unlocked level, stars per level and completion state.
    /// </summary>
    public static class SaveManager
    {
        private const string HighestUnlockedKey = "MLS_HighestUnlocked";
        private const string StarsKeyFormat = "MLS_Stars_{0}"; // {level}
        private const string CompletedKeyFormat = "MLS_Completed_{0}"; // {level}

        /// <summary>
        /// Resets all saved progress. Use with care.
        /// </summary>
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(HighestUnlockedKey);
            // Note: We don't know how many levels were generated; best-effort: keep highestUnlocked cleared and let levels overwrite their stars.
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns the highest unlocked level. Defaults to 1.
        /// </summary>
        public static int GetHighestUnlocked()
        {
            return PlayerPrefs.GetInt(HighestUnlockedKey, 1);
        }

        /// <summary>
        /// Sets the highest unlocked level (only increases)
        /// </summary>
        public static void SetHighestUnlocked(int level)
        {
            if (level <= 0) return;
            int current = GetHighestUnlocked();
            if (level > current)
            {
                PlayerPrefs.SetInt(HighestUnlockedKey, level);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Get stored stars for a level (0-3). Defaults to 0.
        /// </summary>
        public static int GetStars(int level)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(string.Format(StarsKeyFormat, level), 0), 0, 3);
        }

        /// <summary>
        /// Store stars for a level. Only updates if newStars > existing.
        /// </summary>
        public static void SetStars(int level, int newStars)
        {
            if (level <= 0) return;
            newStars = Mathf.Clamp(newStars, 0, 3);
            string key = string.Format(StarsKeyFormat, level);
            int current = GetStars(level);
            if (newStars > current)
            {
                PlayerPrefs.SetInt(key, newStars);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Mark a level as completed (internal flag).
        /// </summary>
        public static void SetCompleted(int level)
        {
            if (level <= 0) return;
            string key = string.Format(CompletedKeyFormat, level);
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Check whether a level was marked completed previously.
        /// </summary>
        public static bool IsCompleted(int level)
        {
            if (level <= 0) return false;
            return PlayerPrefs.GetInt(string.Format(CompletedKeyFormat, level), 0) == 1;
        }
    }
}
