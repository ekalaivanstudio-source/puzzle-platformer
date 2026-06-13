using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LevelSelectionSystem
{
    /// <summary>
    /// The complete on-disk save payload. Kept tiny and flat so it serializes cleanly
    /// with <see cref="JsonUtility"/> and stays cheap to read/write even with 500+ levels.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// Id of the highest level the player has unlocked. Acts as the unlock "pointer":
        /// a level is unlocked if it sits at or before this level in the database order.
        /// A sentinel of <see cref="int.MinValue"/> means "never initialised" — the first
        /// run seeds it with the first level's id.
        /// </summary>
        public int HighestUnlockedLevelId = int.MinValue;

        /// <summary>Per-level completion + star records. Only levels the player has touched appear here.</summary>
        public List<LevelProgress> Levels = new List<LevelProgress>();
    }

    /// <summary>
    /// Owns persistence and NOTHING else. It knows how to load, hold, mutate and write the
    /// <see cref="SaveData"/> blob; it does not know unlock rules, the level list, or the UI.
    /// Those concerns live in <see cref="LevelManager"/> and the UI layer respectively.
    ///
    /// It is a static service (stateless I/O, no scene presence) rather than a MonoBehaviour
    /// singleton — there is no per-frame behaviour to host and nothing to wire in the Inspector.
    ///
    /// Storage: a JSON file under <c>Application.persistentDataPath</c>, which works on every
    /// platform (including mobile) and is trivial to inspect or wipe during development.
    /// </summary>
    public static class SaveManager
    {
        private const string FileName = "level_progress.json";

        /// <summary>
        /// Raised after every successful <see cref="Save"/>. The UI subscribes to this so it
        /// can refresh itself whenever progress changes, without polling.
        /// </summary>
        public static event Action OnProgressChanged;

        private static SaveData s_Data;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// The in-memory save data, loaded lazily on first access. Read freely; to persist
        /// changes call <see cref="Save"/>.
        /// </summary>
        public static SaveData Data
        {
            get
            {
                if (s_Data == null) Load();
                return s_Data;
            }
        }

        /// <summary>Convenience accessor for the stored highest-unlocked level id.</summary>
        public static int HighestUnlockedLevelId
        {
            get => Data.HighestUnlockedLevelId;
            set => Data.HighestUnlockedLevelId = value;
        }

        /// <summary>
        /// Reads the save file into memory (or starts a fresh save if none exists / it is
        /// corrupt). Safe to call repeatedly; called automatically on first <see cref="Data"/> access.
        /// </summary>
        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    s_Data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
                }
                else
                {
                    s_Data = new SaveData();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to load save, starting fresh. {e.Message}");
                s_Data = new SaveData();
            }

            // Guard against a null list arriving from older / partial save files.
            s_Data.Levels ??= new List<LevelProgress>();
        }

        /// <summary>Writes the in-memory data to disk and notifies listeners via <see cref="OnProgressChanged"/>.</summary>
        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, prettyPrint: true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to write save. {e.Message}");
            }

            OnProgressChanged?.Invoke();
        }

        /// <summary>Returns the stored progress for a level, or null if the player has never touched it.</summary>
        public static LevelProgress GetProgress(int levelId)
        {
            List<LevelProgress> levels = Data.Levels;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].LevelId == levelId) return levels[i];
            }
            return null;
        }

        /// <summary>Returns the progress for a level, creating (but not yet saving) an empty record if needed.</summary>
        public static LevelProgress GetOrCreateProgress(int levelId)
        {
            LevelProgress existing = GetProgress(levelId);
            if (existing != null) return existing;

            var created = new LevelProgress(levelId);
            Data.Levels.Add(created);
            return created;
        }

        /// <summary>
        /// Wipes all progress and persists the empty state. Handy for a "Reset Progress"
        /// settings button or for QA.
        /// </summary>
        public static void ResetProgress()
        {
            s_Data = new SaveData();
            Save();
        }
    }
}
