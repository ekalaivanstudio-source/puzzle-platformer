using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModernLevelSelection
{
    /// <summary>
    /// Helper component to mark the current level as completed. Call LevelComplete.CompleteCurrentLevel(stars) from your level UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelComplete : MonoBehaviour
    {
        /// <summary>
        /// Mark the current active scene as completed and provide stars (0-3).
        /// </summary>
        public void CompleteCurrentLevel(int stars)
        {
            int current = GetCurrentLevelNumber();
            if (current <= 0)
            {
                Debug.LogWarning("Current scene is not a Level_{n} scene. Completion ignored.");
                return;
            }

            LevelManager.Instance?.CompleteLevel(current, stars);
        }

        /// <summary>
        /// Parse the active scene name for a Level_{n} pattern and return n or -1 if not found.
        /// </summary>
        public static int GetCurrentLevelNumber()
        {
            string name = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(name)) return -1;
            if (!name.StartsWith("Level_", StringComparison.OrdinalIgnoreCase)) return -1;
            var suffix = name.Substring("Level_".Length);
            if (int.TryParse(suffix, out var n)) return n;
            return -1;
        }
    }
}
