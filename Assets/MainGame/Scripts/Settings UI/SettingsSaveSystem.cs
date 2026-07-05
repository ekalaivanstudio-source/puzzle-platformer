using System;
using System.IO;
using UnityEngine;

namespace Setting.Menu
{
    /// <summary>
    /// Handles loading and saving of settings data to a JSON file.
    /// </summary>
    public static class SettingsSaveSystem
    {
        #region Constants

        private const string FileName = "settings.json";

        #endregion

        #region Properties

        /// <summary>
        /// Gets the full persistent data path used for the settings file.
        /// </summary>
        public static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads settings from disk. If the file does not exist, a default settings object is created and saved.
        /// </summary>
        /// <returns>The loaded or newly created settings data.</returns>
        public static SettingsData LoadSettings()
        {
            if (!File.Exists(SavePath))
            {
                return CreateDefaultSettingsData(saveImmediately: true);
            }

            try
            {
                string json = File.ReadAllText(SavePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return CreateDefaultSettingsData(saveImmediately: true);
                }

                SettingsData settingsData = JsonUtility.FromJson<SettingsData>(json);

                if (settingsData == null)
                {
                    return CreateDefaultSettingsData(saveImmediately: true);
                }

                return settingsData;
            }
            catch (Exception)
            {
                return CreateDefaultSettingsData(saveImmediately: true);
            }
        }

        /// <summary>
        /// Saves the provided settings to the JSON file.
        /// </summary>
        /// <param name="settingsData">The settings data to persist.</param>
        /// <returns>True if the save operation completed successfully.</returns>
        public static bool SaveSettings(SettingsData settingsData)
        {
            if (settingsData == null)
            {
                settingsData = new SettingsData();
            }

            try
            {
                string directoryPath = Path.GetDirectoryName(SavePath);

                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string json = JsonUtility.ToJson(settingsData, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a new instance of SettingsData using default values.
        /// </summary>
        /// <param name="saveImmediately">If true, the newly created settings are written to disk immediately.</param>
        /// <returns>A new settings object with default values.</returns>
        public static SettingsData CreateDefaultSettingsData(bool saveImmediately)
        {
            SettingsData defaultSettings = new SettingsData();

            if (saveImmediately)
            {
                SaveSettings(defaultSettings);
            }

            return defaultSettings;
        }

        #endregion
    }
}

