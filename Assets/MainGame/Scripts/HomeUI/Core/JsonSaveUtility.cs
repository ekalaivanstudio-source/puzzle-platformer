using System;
using System.IO;
using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// Tiny, reusable JSON persistence helper shared by every save system in this project
    /// (settings, collections, …). One responsibility: turn a serializable object into a file
    /// under <c>Application.persistentDataPath</c> and back, swallowing/​logging I/O errors so
    /// callers never have to wrap each access in try/catch.
    ///
    /// Keeping persistence in one place means the storage backend (file → cloud, JSON → binary,
    /// PlayerPrefs, …) can change in exactly one spot without touching gameplay code.
    /// </summary>
    public static class JsonSaveUtility
    {
        /// <summary>Serializes <paramref name="data"/> to <paramref name="fileName"/> (e.g. "settings.json").</summary>
        public static void Save<T>(string fileName, T data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(PathFor(fileName), json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonSaveUtility] Failed to save '{fileName}': {e.Message}");
            }
        }

        /// <summary>
        /// Loads <paramref name="fileName"/> into a <typeparamref name="T"/>. Returns
        /// <paramref name="fallback"/> (or a new T) if the file is missing or unreadable.
        /// </summary>
        public static T Load<T>(string fileName, T fallback = default) where T : class, new()
        {
            try
            {
                string path = PathFor(fileName);
                if (!File.Exists(path)) return fallback ?? new T();

                T result = JsonUtility.FromJson<T>(File.ReadAllText(path));
                return result ?? fallback ?? new T();
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonSaveUtility] Failed to load '{fileName}': {e.Message}");
                return fallback ?? new T();
            }
        }

        /// <summary>True if a save file with this name exists.</summary>
        public static bool Exists(string fileName) => File.Exists(PathFor(fileName));

        /// <summary>Deletes the save file if present (used for "reset" / "delete save" flows).</summary>
        public static void Delete(string fileName)
        {
            string path = PathFor(fileName);
            if (File.Exists(path)) File.Delete(path);
        }

        private static string PathFor(string fileName) =>
            Path.Combine(Application.persistentDataPath, fileName);
    }
}
