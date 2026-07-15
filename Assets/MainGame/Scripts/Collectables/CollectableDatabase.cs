using System;
using System.Collections.Generic;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// Per-level authoring data: how many of each collectable are placed in a level.
    /// This is the design spec ("Level 1 has 1 robot part"). Actual collection state
    /// lives in the save file, keyed by each placed collectable's unique id.
    /// </summary>
    [Serializable]
    public class LevelCollectableData
    {
        [Tooltip("Level number. Matches the scene build index (Home = 0, Level1 = 1, ...).")]
        public int levelNumber;

        [Min(0)]
        [Tooltip("How many Robot Parts are placed in this level.")]
        public int robotPartCount;

        [Min(0)]
        [Tooltip("How many Memory Shards are placed in this level.")]
        public int memoryShardCount;
    }

    /// <summary>
    /// Single shared ScriptableObject holding the collectable layout for every level.
    /// Assign one instance to every <see cref="CollectableLevelManager"/>; all runtime
    /// objects read their targets through the manager, so there is one source of truth.
    ///
    /// Create via: Assets ▸ Create ▸ Collectables ▸ Collectable Database,
    /// or Tools ▸ Collectables ▸ Collectable Tools.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CollectableDatabase",
        menuName = "Collectables/Collectable Database",
        order = 0)]
    public class CollectableDatabase : ScriptableObject
    {
        [Tooltip("One row per level. Levels not listed are treated as having zero collectables.")]
        [SerializeField] private List<LevelCollectableData> _levels = new List<LevelCollectableData>();

        public IReadOnlyList<LevelCollectableData> Levels => _levels;

        /// <summary>Returns the authoring data for a level, or null if none is configured.</summary>
        public LevelCollectableData GetLevel(int levelNumber)
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i] != null && _levels[i].levelNumber == levelNumber)
                    return _levels[i];
            }
            return null;
        }

        /// <summary>How many Robot Parts are designed to exist in the given level.</summary>
        public int GetRobotPartCount(int levelNumber) => GetLevel(levelNumber)?.robotPartCount ?? 0;

        /// <summary>How many Memory Shards are designed to exist in the given level.</summary>
        public int GetMemoryShardCount(int levelNumber) => GetLevel(levelNumber)?.memoryShardCount ?? 0;

        /// <summary>Content-derived Robot Part grand total (sum across all configured levels).</summary>
        public int TotalRobotParts
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _levels.Count; i++)
                    if (_levels[i] != null) sum += _levels[i].robotPartCount;
                return sum;
            }
        }

        /// <summary>Content-derived Memory Shard grand total (sum across all configured levels).</summary>
        public int TotalMemoryShards
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _levels.Count; i++)
                    if (_levels[i] != null) sum += _levels[i].memoryShardCount;
                return sum;
            }
        }

        /// <summary>Sum of Memory Shards placed in levels within the given inclusive range.</summary>
        public int GetMemoryShardCountInRange(int fromLevel, int toLevel)
        {
            int sum = 0;
            for (int i = 0; i < _levels.Count; i++)
            {
                var l = _levels[i];
                if (l != null && l.levelNumber >= fromLevel && l.levelNumber <= toLevel)
                    sum += l.memoryShardCount;
            }
            return sum;
        }
    }
}
