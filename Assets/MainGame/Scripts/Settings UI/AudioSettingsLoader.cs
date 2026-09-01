using UnityEngine;
using UnityEngine.Audio;

namespace Setting.Menu
{
    /// <summary>
    /// Loads saved audio settings and applies them to the AudioMixer.
    /// Attach this to a GameObject in your first scene.
    /// </summary>
    public class AudioSettingsLoader : MonoBehaviour
    {
        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;

        private void Start()
        {
            ApplySavedAudioSettings();
        }

        public void ApplySavedAudioSettings()
        {
            if (audioMixer == null)
            {
                Debug.LogWarning("Audio Mixer is not assigned.");
                return;
            }

            SettingsData settings = SettingsSaveSystem.LoadSettings();

            if (settings == null)
            {
                Debug.LogWarning("No saved settings found.");
                return;
            }

            audioMixer.SetFloat(AudioMixerParameters.MasterVolumeParameter, SliderToMixer(settings.MasterVolume));
            audioMixer.SetFloat(AudioMixerParameters.MusicVolumeParameter, SliderToMixer(settings.MusicVolume));
            audioMixer.SetFloat(AudioMixerParameters.AmbienceVolumeParameter, SliderToMixer(settings.AmbienceVolume));
            audioMixer.SetFloat(AudioMixerParameters.SFXVolumeParameter, SliderToMixer(settings.SFXVolume));
            audioMixer.SetFloat(AudioMixerParameters.VoiceVolumeParameter, SliderToMixer(settings.VoiceVolume));

            Debug.Log("Saved audio settings applied.");

            audioMixer.GetFloat(AudioMixerParameters.MusicVolumeParameter, out float value);

            Debug.Log($"Saved Music:{settings.MusicVolume}  Applied Music:{value}");
        }

        private float SliderToMixer(float sliderValue)
        {
            sliderValue = Mathf.Clamp01(sliderValue);

            if (sliderValue <= 0f)
                return AudioMixerParameters.MinMixerVolume;

            // Logarithmic conversion to match human hearing (maps 0.0001-1.0 to -80dB-0dB)
            return Mathf.Log10(sliderValue) * 20f;
        }
    }
}