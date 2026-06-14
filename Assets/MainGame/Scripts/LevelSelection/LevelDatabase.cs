using System.Collections.Generic;
using UnityEngine;

namespace LevelSelectionSystem
{
    /// <summary>
    /// The single source of truth for "which levels exist and in what order".
    ///
    /// Create one asset via:  Create → Level Selection → Level Database
    /// then drag every <see cref="LevelData"/> asset into <c>Levels</c> in the
    /// order they should appear / unlock.
    ///
    /// The whole system is data-driven from this list — there is no hard-coded
    /// level count anywhere. Adding the 501st level means dragging one more
    /// asset in here; no code changes are required.
    ///
    /// Order in this list defines unlock order: completing the level at index i
    /// unlocks the level at index i + 1.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LevelDatabase",
        menuName = "Level Selection/Level Database",
        order = 1)]
    public class LevelDatabase : ScriptableObject
    {
        [Tooltip("Every level in the game, in unlock / display order.")]
        [SerializeField] private List<LevelData> m_Levels = new List<LevelData>();

        /// <summary>Total number of levels. Drives the entire UI — no hard-coded count.</summary>
        public int Count => m_Levels.Count;

        /// <summary>Read-only view of all levels in unlock order.</summary>
        public IReadOnlyList<LevelData> Levels => m_Levels;

        /// <summary>Returns the level at a given position in the list, or null if out of range.</summary>
        public LevelData GetByIndex(int index)
        {
            if (index < 0 || index >= m_Levels.Count) return null;
            return m_Levels[index];
        }

        /// <summary>Returns the position of a level id in the list, or -1 if not present.</summary>
        public int IndexOf(int levelId)
        {
            for (int i = 0; i < m_Levels.Count; i++)
            {
                if (m_Levels[i] != null && m_Levels[i].LevelId == levelId)
                    return i;
            }
            return -1;
        }

        /// <summary>Looks up a level by its stable id, or null if not found.</summary>
        public LevelData GetById(int levelId)
        {
            int index = IndexOf(levelId);
            return index >= 0 ? m_Levels[index] : null;
        }

        /// <summary>The first level in the list (the one unlocked by default), or null if empty.</summary>
        public LevelData FirstLevel => m_Levels.Count > 0 ? m_Levels[0] : null;

        /// <summary>
        /// Returns the level that comes immediately after the given id, or null if the
        /// id is the last level (or unknown). This is what "unlock the next level" uses.
        /// </summary>
        public LevelData GetNextLevel(int levelId)
        {
            int index = IndexOf(levelId);
            if (index < 0) return null;
            return GetByIndex(index + 1);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only sanity check: warns about duplicate ids or missing entries when
        /// the asset is edited, so content mistakes are caught before they ship.
        /// </summary>
        private void OnValidate()
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < m_Levels.Count; i++)
            {
                LevelData level = m_Levels[i];
                if (level == null)
                {
                    Debug.LogWarning($"[LevelDatabase] Entry {i} is empty (null).", this);
                    continue;
                }
                if (!seen.Add(level.LevelId))
                    Debug.LogWarning($"[LevelDatabase] Duplicate LevelId {level.LevelId} on '{level.name}'.", this);
            }
        }
#endif
    }
}
