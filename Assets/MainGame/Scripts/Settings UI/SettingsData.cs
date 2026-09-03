using System;
using UnityEngine;

namespace Setting.Menu
{
    /// <summary>
    /// Serializable data container for user-configurable game settings.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        #region Fields

        [Header("Audio")]
        public float MasterVolume = 1f;
        public float MusicVolume = 1f;
        public float AmbienceVolume = 1f;
        public float SFXVolume = 1f;
        public float VoiceVolume = 1f;

        [Header("Visual")]
        public float Brightness = 1f;

        [Header("Display")]
        public bool Fullscreen = true;

        [Header("Haptics")]
        // Read and written by HapticService, which is the only thing that touches it: a
        // call site asking for a buzz never has to check whether the player wants one.
        // Defaults to on, so a settings file written before this field existed — where
        // JsonUtility leaves it at the value set here — keeps vibration switched on.
        public bool HapticsEnabled = true;

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a deep copy of the current settings data.
        /// </summary>
        /// <returns>A cloned copy of this settings object.</returns>
        public SettingsData Clone()
        {
            return new SettingsData
            {
                MasterVolume = this.MasterVolume,
                MusicVolume = this.MusicVolume,
                AmbienceVolume = this.AmbienceVolume,
                SFXVolume = this.SFXVolume,
                VoiceVolume = this.VoiceVolume,
                Brightness = this.Brightness,
                Fullscreen = this.Fullscreen,
                HapticsEnabled = this.HapticsEnabled
            };
        }

        #endregion
    }
}

