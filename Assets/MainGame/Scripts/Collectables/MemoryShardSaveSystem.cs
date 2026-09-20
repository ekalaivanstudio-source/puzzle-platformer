using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Collectables
{
    /// <summary>Root serialisable container written to disk as JSON.</summary>
    [Serializable]
    public class MemoryShardSaveData
    {
        /// <summary>Shard ids that have been picked up, e.g. "shard_7" (see <see cref="MemoryShardIds.ShardKey"/>).</summary>
        public List<string> collectedShards = new List<string>();

        /// <summary>
        /// Ids of stories whose cutscene has already been played, so it is not replayed on the
        /// next level completion. Deliberately NOT a list of unlocked stories — see the class
        /// comment on <see cref="MemoryShardSaveSystem"/>.
        /// </summary>
        public List<string> shownStories = new List<string>();
    }

    /// <summary>
    /// Persists memory-shard progress as a single JSON file in
    /// <see cref="Application.persistentDataPath"/>, alongside <see cref="RobotPartSaveSystem"/>
    /// and in the same shape.
    ///
    /// Two things are stored, and it matters which: <b>the shards picked up</b>, and <b>the
    /// stories already played</b>. What is <i>unlocked</i> is never stored — it is recomputed
    /// from the shard count against the live thresholds in <see cref="MemoryShardDatabase"/>
    /// every time it is asked for. That is what lets the design keep re-tuning "5 shards, then
    /// 10, then …" after players already have saves: lower a threshold and the story unlocks
    /// for them on the spot; raise one and it goes back to locked, with nothing to migrate.
    ///
    /// Call this through <see cref="MemoryShardService"/> rather than directly — the service is
    /// the single access point and is what raises the events the UI needs.
    /// </summary>
    public static class MemoryShardSaveSystem
    {
        private const string FileName = "memoryshards.json";

        private static MemoryShardSaveData _data;
        private static HashSet<string> _collected;
        private static HashSet<string> _shown;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        // ─── Load / Save ──────────────────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            Load();
        }

        /// <summary>Forces a reload from disk, discarding any in-memory cache.</summary>
        public static void Load()
        {
            _data = null;

            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    if (!string.IsNullOrWhiteSpace(json))
                        _data = JsonUtility.FromJson<MemoryShardSaveData>(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MemoryShardSaveSystem] Failed to read save, starting fresh. {e.Message}");
            }

            if (_data == null) _data = new MemoryShardSaveData();
            if (_data.collectedShards == null) _data.collectedShards = new List<string>();
            if (_data.shownStories == null) _data.shownStories = new List<string>();

            _collected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in _data.collectedShards)
                if (!string.IsNullOrEmpty(id)) _collected.Add(id);

            _shown = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in _data.shownStories)
                if (!string.IsNullOrEmpty(id)) _shown.Add(id);
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(SavePath, JsonUtility.ToJson(_data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MemoryShardSaveSystem] Failed to write save. {e.Message}");
            }
        }

        // ─── Shards ───────────────────────────────────────────────────────────────

        /// <summary>True if this shard id has been picked up.</summary>
        public static bool IsCollected(string shardId)
        {
            if (string.IsNullOrEmpty(shardId)) return false;
            EnsureLoaded();
            return _collected.Contains(shardId);
        }

        /// <summary>How many shards have been picked up in total. The number every story is gated on.</summary>
        public static int TotalCollected
        {
            get
            {
                EnsureLoaded();
                return _collected.Count;
            }
        }

        /// <summary>
        /// Records a pickup. No-op (returns false) if the shard was already collected, which
        /// keeps the total idempotent when a level is replayed.
        /// </summary>
        public static bool MarkCollected(string shardId)
        {
            if (string.IsNullOrEmpty(shardId))
            {
                Debug.LogWarning("[MemoryShardSaveSystem] Ignoring shard with an empty id.");
                return false;
            }

            EnsureLoaded();
            if (!_collected.Add(shardId)) return false;

            _data.collectedShards.Add(shardId);
            Save();
            return true;
        }

        // ─── Stories ──────────────────────────────────────────────────────────────

        /// <summary>True once this story's cutscene has been played.</summary>
        public static bool IsStoryShown(string storyId)
        {
            if (string.IsNullOrEmpty(storyId)) return false;
            EnsureLoaded();
            return _shown.Contains(storyId);
        }

        /// <summary>
        /// Records that a story's cutscene has played, so it is not shown again. Written to disk
        /// immediately: a story interrupted by a quit should not replay forever, and one that
        /// finished should not replay at all.
        /// </summary>
        public static bool MarkStoryShown(string storyId)
        {
            if (string.IsNullOrEmpty(storyId)) return false;

            EnsureLoaded();
            if (!_shown.Add(storyId)) return false;

            _data.shownStories.Add(storyId);
            Save();
            return true;
        }

        // ─── Reset ────────────────────────────────────────────────────────────────

        /// <summary>Wipes all memory-shard progress (file + cache). Used by New Game and the reset tools.</summary>
        public static void ResetAll()
        {
            _data = new MemoryShardSaveData();
            _collected = new HashSet<string>(StringComparer.Ordinal);
            _shown = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MemoryShardSaveSystem] Failed to delete save file. {e.Message}");
            }
        }
    }
}
