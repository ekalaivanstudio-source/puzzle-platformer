using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Collectables
{
    /// <summary>Root serialisable container written to disk as JSON.</summary>
    [Serializable]
    public class RobotPartSaveData
    {
        /// <summary>Stable part ids that have been picked up, e.g. "echo_3" (see <see cref="RobotIds.PartKey"/>).</summary>
        public List<string> collectedParts = new List<string>();
    }

    /// <summary>
    /// Persists robot-part progress as a single JSON file in
    /// <see cref="Application.persistentDataPath"/> (mirrors <c>SettingsSaveSystem</c>).
    ///
    /// Identity is the part id alone — a part belongs to a robot, not to a level, so moving
    /// a part to a different scene never loses or duplicates progress. Data is cached in
    /// memory and written on every change.
    ///
    /// Call this through <see cref="RobotCollectionService"/> rather than directly: the
    /// service is the single access point and is what raises the change events the UI needs.
    /// </summary>
    public static class RobotPartSaveSystem
    {
        private const string FileName = "robotparts.json";

        private static RobotPartSaveData _data;
        private static HashSet<string> _collected;

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
                        _data = JsonUtility.FromJson<RobotPartSaveData>(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RobotPartSaveSystem] Failed to read save, starting fresh. {e.Message}");
            }

            if (_data == null) _data = new RobotPartSaveData();
            if (_data.collectedParts == null) _data.collectedParts = new List<string>();

            _collected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in _data.collectedParts)
                if (!string.IsNullOrEmpty(id)) _collected.Add(id);
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
                Debug.LogError($"[RobotPartSaveSystem] Failed to write save. {e.Message}");
            }
        }

        // ─── Queries ──────────────────────────────────────────────────────────────

        /// <summary>True if this part id has been picked up.</summary>
        public static bool IsCollected(string partId)
        {
            if (string.IsNullOrEmpty(partId)) return false;
            EnsureLoaded();
            return _collected.Contains(partId);
        }

        /// <summary>How many of a robot's parts have been picked up (0..<see cref="RobotIds.PartsPerRobot"/>).</summary>
        public static int CountCollected(RobotId robot)
        {
            EnsureLoaded();
            int count = 0;
            for (int i = 0; i < RobotIds.PartsPerRobot; i++)
                if (_collected.Contains(RobotIds.PartKey(robot, i))) count++;
            return count;
        }

        /// <summary>How many parts have been picked up across every robot.</summary>
        public static int TotalCollected
        {
            get
            {
                EnsureLoaded();
                return _collected.Count;
            }
        }

        // ─── Mutations ────────────────────────────────────────────────────────────

        /// <summary>
        /// Records a pickup. No-op (returns false) if the part was already collected, which
        /// keeps counts idempotent when a level is replayed.
        /// </summary>
        public static bool MarkCollected(string partId)
        {
            if (string.IsNullOrEmpty(partId))
            {
                Debug.LogWarning("[RobotPartSaveSystem] Ignoring part with an empty id.");
                return false;
            }

            EnsureLoaded();
            if (!_collected.Add(partId)) return false;

            _data.collectedParts.Add(partId);
            Save();
            return true;
        }

        /// <summary>Wipes all robot-part progress (file + cache). Used by the reset tools.</summary>
        public static void ResetAll()
        {
            _data = new RobotPartSaveData();
            _collected = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RobotPartSaveSystem] Failed to delete save file. {e.Message}");
            }
        }
    }
}
