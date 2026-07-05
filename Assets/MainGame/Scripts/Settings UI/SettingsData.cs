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
                Fullscreen = this.Fullscreen
            };
        }

        #endregion
    }
}

