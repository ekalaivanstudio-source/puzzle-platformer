using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// Persists collectable progress as a single JSON file in
    /// <see cref="Application.persistentDataPath"/> (mirrors <c>SettingsSaveSystem</c>).
    ///
    /// The file records every picked-up collectable by its unique id, so a collectable
    /// can be hidden permanently the next time its level loads, and so counts survive
    /// across sessions. Data is cached in memory and written on every change.
    ///
    /// Reset everything with <see cref="ResetAll"/> (also exposed via
    /// Tools ▸ Collectables ▸ Collectable Tools).
    /// </summary>
    public static class CollectableSaveSystem
    {
        private const string FileName = "collectables.json";

        private static CollectableSaveData _data;
        private static HashSet<string> _collectedKeys;

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
                        _data = JsonUtility.FromJson<CollectableSaveData>(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CollectableSaveSystem] Failed to read save, starting fresh. {e.Message}");
            }

            if (_data == null) _data = new CollectableSaveData();
            if (_data.records == null) _data.records = new List<CollectedRecord>();

            RebuildIndex();
        }

        private static void RebuildIndex()
        {
            _collectedKeys = new HashSet<string>();
            foreach (var r in _data.records)
                if (r != null && !string.IsNullOrEmpty(r.id)) _collectedKeys.Add(Key(r.type, r.level, r.id));
        }

        // A collectable's identity is (type, level, id) — NOT id alone. This way the same id
        // reused in a different level (e.g. a duplicated prefab instance) is a distinct thing,
        // so collecting one does not hide the other.
        private static string Key(CollectableType type, int level, string id) => $"{(int)type}|{level}|{id}";

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string json = JsonUtility.ToJson(_data, prettyPrint: true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CollectableSaveSystem] Failed to write save. {e.Message}");
            }
        }

        // ─── Queries ──────────────────────────────────────────────────────────────

        /// <summary>True if this specific collectable (type + level + id) has been picked up.</summary>
        public static bool IsCollected(CollectableType type, int level, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureLoaded();
            return _collectedKeys.Contains(Key(type, level, id));
        }

        /// <summary>Total number of collected items of a type across the whole game.</summary>
        public static int GetTotalCollected(CollectableType type)
        {
            EnsureLoaded();
            int count = 0;
            foreach (var r in _data.records)
                if (r != null && r.type == type) count++;
            return count;
        }

        /// <summary>Number collected of a type within an inclusive level range.</summary>
        public static int GetCollectedInLevelRange(CollectableType type, int fromLevel, int toLevel)
        {
            EnsureLoaded();
            int count = 0;
            foreach (var r in _data.records)
                if (r != null && r.type == type && r.level >= fromLevel && r.level <= toLevel) count++;
            return count;
        }

        /// <summary>Number collected of a type in a single level.</summary>
        public static int GetCollectedForLevel(CollectableType type, int level)
            => GetCollectedInLevelRange(type, level, level);

        // ─── Mutations ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Records a pickup. No-op (returns false) if this exact collectable (type + level + id)
        /// was already collected, which keeps counts idempotent when a level is replayed.
        /// </summary>
        public static bool MarkCollected(string id, CollectableType type, int level)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[CollectableSaveSystem] Ignoring collectable with empty id.");
                return false;
            }

            EnsureLoaded();
            string key = Key(type, level, id);
            if (_collectedKeys.Contains(key)) return false;

            _data.records.Add(new CollectedRecord { id = id, type = type, level = level });
            _collectedKeys.Add(key);
            Save();
            return true;
        }

        /// <summary>Wipes all collectable progress (file + cache). Used by the reset tool.</summary>
        public static void ResetAll()
        {
            _data = new CollectableSaveData();
            _collectedKeys = new HashSet<string>();

            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CollectableSaveSystem] Failed to delete save file. {e.Message}");
            }
        }
    }
}
