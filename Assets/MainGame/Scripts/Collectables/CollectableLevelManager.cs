using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Collectables
{
    /// <summary>
    /// Per-scene coordinator for the collectable systems. Place one on a manager object
    /// in every level scene (e.g. under "Managers") and assign the shared
    /// <see cref="CollectableDatabase"/>. Every <see cref="Collectable"/> and
    /// <see cref="CollectableHUD"/> in the scene talks to this manager, so there is a
    /// single access point for the database and the save file.
    ///
    /// The current level number defaults to the scene build index (Home = 0, Level1 = 1, …),
    /// matching the convention used by MainMenuManager / LevelManager. Override it with
    /// <see cref="_levelNumberOverride"/> for scenes that don't follow that convention.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollectableLevelManager : MonoBehaviour
    {
        public static CollectableLevelManager Instance { get; private set; }

        [Header("Config")]
        [Tooltip("Shared database asset holding per-level collectable counts. " +
                 "Assign the same asset in every level scene.")]
        [SerializeField] private CollectableDatabase _database;

        [Tooltip("Level number for this scene. Leave at 0 to auto-derive from the scene build index.")]
        [SerializeField] private int _levelNumberOverride = 0;

        /// <summary>Raised whenever a collectable is picked up in this level. UI listens to this.</summary>
        public event Action OnCollectableCollected;

        public CollectableDatabase Database => _database;

        /// <summary>The resolved level number for this scene.</summary>
        public int CurrentLevel { get; private set; }

        private void Awake()
        {
            // Per-scene, not persistent — the manager belongs to the level it lives in.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CollectableLevelManager] Another instance already exists in this scene.", this);
            }
            Instance = this;

            CurrentLevel = _levelNumberOverride > 0
                ? _levelNumberOverride
                : SceneManager.GetActiveScene().buildIndex;

            if (_database == null)
                Debug.LogWarning("[CollectableLevelManager] No CollectableDatabase assigned.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Collect API (called by Collectable) ────────────────────────────────────

        /// <summary>Has this collectable id already been picked up (this or a previous session)?</summary>
        public bool IsCollected(string uniqueId) => CollectableSaveSystem.IsCollected(uniqueId);

        /// <summary>
        /// Records a pickup and notifies listeners. Returns false if the id was already
        /// collected (e.g. duplicate trigger), in which case nothing changes.
        /// </summary>
        public bool Collect(string uniqueId, CollectableType type)
        {
            bool changed = CollectableSaveSystem.MarkCollected(uniqueId, type, CurrentLevel);
            if (changed) OnCollectableCollected?.Invoke();
            return changed;
        }

        // ─── Robot Parts (single game-wide total) ────────────────────────────────────

        /// <summary>Robot Parts collected across the whole game.</summary>
        public int RobotPartsCollectedTotal => CollectableSaveSystem.GetTotalCollected(CollectableType.RobotPart);

        /// <summary>
        /// Robot Parts grand total shown in the HUD (e.g. 56). This is the fixed
        /// game-wide figure from <see cref="CollectableConstants.RobotPartsGrandTotal"/>,
        /// NOT the database sum — per-level counts are placement authoring and won't add
        /// up to the grand total until every level is filled in.
        /// </summary>
        public int RobotPartsGrandTotal => CollectableConstants.RobotPartsGrandTotal;

        // ─── Memory Shards (tiered, per level range) ─────────────────────────────────

        /// <summary>True when this level belongs to a configured Memory-Shard story tier.</summary>
        public bool TryGetCurrentTier(out MemoryShardTier tier)
            => CollectableConstants.TryGetTierForLevel(CurrentLevel, out tier);

        /// <summary>Memory Shards collected so far within the current level's tier range.</summary>
        public int MemoryShardsCollectedInTier
        {
            get
            {
                if (!TryGetCurrentTier(out var tier)) return 0;
                return CollectableSaveSystem.GetCollectedInLevelRange(
                    CollectableType.MemoryShard, tier.FromLevel, tier.ToLevel);
            }
        }

        /// <summary>Shards required to complete the current level's tier (0 if none).</summary>
        public int MemoryShardsTierTarget => TryGetCurrentTier(out var tier) ? tier.Required : 0;

        /// <summary>True once the current tier's shard requirement has been met (story unlocked).</summary>
        public bool IsCurrentStoryUnlocked
        {
            get
            {
                if (!TryGetCurrentTier(out var tier)) return false;
                return MemoryShardsCollectedInTier >= tier.Required;
            }
        }

        /// <summary>
        /// Returns whether the story tier covering the given level has been fully collected.
        /// Useful for a future store/gallery screen outside a gameplay scene.
        /// </summary>
        public static bool IsStoryUnlockedForLevel(int level)
        {
            if (!CollectableConstants.TryGetTierForLevel(level, out var tier)) return false;
            int collected = CollectableSaveSystem.GetCollectedInLevelRange(
                CollectableType.MemoryShard, tier.FromLevel, tier.ToLevel);
            return collected >= tier.Required;
        }
    }
}
