using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// The audio category of the settings system. It does NOT play sounds — the project already
    /// has a playback <see cref="global::AudioManager"/> singleton for that. This module simply
    /// pushes the saved volume/mute values onto that manager, so there is one audio system with a
    /// clean settings front-end rather than two competing ones.
    ///
    /// If a project has no AudioManager in the scene, every call is a safe no-op.
    /// </summary>
    public class AudioSettingsManager : MonoBehaviour, ISettingsModule
    {
        public void Apply(SettingsData data)
        {
            var audio = global::AudioManager.Instance;
            if (audio == null) return;

            audio.MasterVolume = data.MasterVolume;
            audio.MusicVolume = data.MusicVolume;
            audio.SfxVolume = data.SfxVolume;
            audio.UiVolume = data.UiVolume;
            audio.Muted = data.MuteAll;
        }
    }
}
