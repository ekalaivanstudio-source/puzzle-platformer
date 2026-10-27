using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelSelection
{
    /// <summary>
    /// Centralized database ScriptableObject holding all Level Selection Arcs.
    /// Shared between Main Menu (HomeScreen) and In-Game Pause Menu.
    /// Add or reorder ArcData assets here once to update all level selection screens across the game.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelArcDatabase", menuName = "Level Selection/Level Arc Database")]
    public class LevelArcDatabase : ScriptableObject
    {
        [Tooltip("Ordered list of all ArcData ScriptableObjects (e.g. ARC 1 TUT, ARC 1, ARC 2, etc.).")]
        [SerializeField] private List<ArcData> m_Arcs = new List<ArcData>();

        /// <summary>
        /// Gets the list of configured ArcData items.
        /// </summary>
        public List<ArcData> Arcs => m_Arcs;

        /// <summary>
        /// Total number of arc entries in the database.
        /// </summary>
        public int Count => m_Arcs != null ? m_Arcs.Count : 0;

        /// <summary>
        /// Indexer to access ArcData by index.
        /// </summary>
        public ArcData this[int index] => (m_Arcs != null && index >= 0 && index < m_Arcs.Count) ? m_Arcs[index] : null;

        /// <summary>
        /// Calculates the total number of playable levels across all arcs in this database.
        /// </summary>
        public int CalculateTotalLevels()
        {
            if (m_Arcs == null) return 0;
            int total = 0;
            for (int i = 0; i < m_Arcs.Count; i++)
            {
                if (m_Arcs[i] != null) total += m_Arcs[i].totalLevelsInArc;
            }
            return total;
        }

        /// <summary>
        /// Finds the arc that contains a specific 1-based level number.
        /// </summary>
        public ArcData FindArcForLevel(int levelNumber)
        {
            if (m_Arcs == null || levelNumber < 1) return null;
            int currentLevel = 1;
            for (int i = 0; i < m_Arcs.Count; i++)
            {
                ArcData arc = m_Arcs[i];
                if (arc == null) continue;
                if (levelNumber >= currentLevel && levelNumber < currentLevel + arc.totalLevelsInArc)
                {
                    return arc;
                }
                currentLevel += arc.totalLevelsInArc;
            }
            return null;
        }
    }
}
