using System;
using UnityEngine;

namespace LevelSelectionSystem
{
    /// <summary>
    /// Runtime, per-level player progress. This is the *mutable* counterpart to the
    /// authored <see cref="LevelData"/>: it records what the player has achieved on a
    /// specific level — nothing about what the level contains.
    ///
    /// Fields are public so Unity's <see cref="JsonUtility"/> can serialize them inside
    /// the save file. Mutate stars only through <see cref="TrySetStars"/> so the
    /// "keep the best result" rule lives in exactly one place.
    /// </summary>
    [Serializable]
    public class LevelProgress
    {
        /// <summary>Maximum number of stars a level can award.</summary>
        public const int MaxStars = 3;

        [Tooltip("Matches LevelData.LevelId — the level this progress record belongs to.")]
        public int LevelId;

        [Tooltip("True once the player has finished the level at least once.")]
        public bool IsCompleted;

        [Tooltip("Best (highest) star rating ever achieved on this level, 0..3.")]
        public int StarCount;

        /// <summary>Parameterless constructor required by JsonUtility.</summary>
        public LevelProgress() { }

        public LevelProgress(int levelId)
        {
            LevelId = levelId;
            IsCompleted = false;
            StarCount = 0;
        }

        /// <summary>
        /// Records a new star result, keeping only the best. Replaying a level and
        /// scoring fewer stars will NOT lower the stored value.
        /// </summary>
        /// <param name="newStars">Stars earned this attempt (clamped to 0..<see cref="MaxStars"/>).</param>
        /// <returns>True if the stored value improved; false if the previous result was equal or better.</returns>
        public bool TrySetStars(int newStars)
        {
            newStars = Mathf.Clamp(newStars, 0, MaxStars);
            if (newStars <= StarCount) return false;

            StarCount = newStars;
            return true;
        }
    }
}
