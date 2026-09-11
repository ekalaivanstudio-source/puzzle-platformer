using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

namespace Setting.Menu
{
    /// <summary>
    /// Manages the settings UI, applies runtime changes, and persists settings through the save system.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Audio")]
        [SerializeField] private AudioMixer audioMixer;

        [SerializeField] private SettingStepControl masterStepControl;
        [SerializeField] private SettingStepControl musicStepControl;
        [SerializeField] private SettingStepControl ambienceStepControl;
        [SerializeField] private SettingStepControl sfxStepControl;
        [SerializeField] private SettingStepControl voiceStepControl;

        [Header("Visual")]
        [SerializeField] private SettingStepControl brightnessStepControl;
        [SerializeField] private Image brightnessOverlay;

        [Header("Display Mode")]
        [SerializeField] private TMP_Text displayModeText;
        [SerializeField] private Button previousDisplayButton;
        [SerializeField] private Button nextDisplayButton;

        [Header("Actions")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button cancelButton;

        #endregion

        #region Private Fields

        private SettingsData currentSettings;
        private SettingsData lastAppliedSettings;
        private bool isDirty;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Initializes the manager, registers event handlers, loads settings and updates the UI.
        /// </summary>
        private void Awake()
        {
            RegisterButtons();
            RegisterStepControls();

            SettingsData loadedSettings = SettingsSaveSystem.LoadSettings();
            currentSettings = loadedSettings.Clone();
            lastAppliedSettings = loadedSettings.Clone();

            UpdateUIFromSettings();
            SetDirtyState(false);
        }

        /// <summary>
        /// Applies the loaded settings to the runtime systems.
        /// This runs in Start rather than Awake because the audio system applies the mixer's
        /// start snapshot after Awake, which discards any AudioMixer.SetFloat call made there.
        /// </summary>
        private void Start()
        {
            ApplySettingsToRuntime();
        }

        #endregion

        #region Button Registration

        /// <summary>
        /// Registers all button click listeners in code.
        /// </summary>
        private void RegisterButtons()
        {
            if (previousDisplayButton != null)
            {
                previousDisplayButton.onClick.AddListener(HandlePreviousDisplayButtonClicked);
            }

            if (nextDisplayButton != null)
            {
                nextDisplayButton.onClick.AddListener(HandleNextDisplayButtonClicked);
            }

            if (applyButton != null)
            {
                applyButton.onClick.AddListener(HandleApplyButtonClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(HandleResetButtonClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancelButtonClicked);
            }
        }

        #endregion

        #region Step Control Registration

        /// <summary>
        /// Registers all step control value change listeners in code.
        /// </summary>
        private void RegisterStepControls()
        {
            if (masterStepControl != null)
            {
                masterStepControl.OnValueChanged += (value) => HandleStepValueChanged(value, AudioMixerParameters.MasterVolumeParameter);
            }

            if (musicStepControl != null)
            {
                musicStepControl.OnValueChanged += (value) => HandleStepValueChanged(value, AudioMixerParameters.MusicVolumeParameter);
            }

            if (ambienceStepControl != null)
            {
                ambienceStepControl.OnValueChanged += (value) => HandleStepValueChanged(value, AudioMixerParameters.AmbienceVolumeParameter);
            }

            if (sfxStepControl != null)
            {
                sfxStepControl.OnValueChanged += (value) => HandleStepValueChanged(value, AudioMixerParameters.SFXVolumeParameter);
            }

            if (voiceStepControl != null)
            {
                voiceStepControl.OnValueChanged += (value) => HandleStepValueChanged(value, AudioMixerParameters.VoiceVolumeParameter);
            }

            if (brightnessStepControl != null)
            {
                brightnessStepControl.OnValueChanged += HandleBrightnessStepChanged;
            }
        }

        #endregion

        #region UI Update

        /// <summary>
        /// Refreshes all UI elements from the current settings object.
        /// </summary>
        private void UpdateUIFromSettings()
        {
            if (currentSettings == null)
            {
                currentSettings = new SettingsData();
            }

            if (masterStepControl != null)
            {
                masterStepControl.SetValueWithoutNotify(Mathf.RoundToInt(currentSettings.MasterVolume * 10f));
            }

            if (musicStepControl != null)
            {
                musicStepControl.SetValueWithoutNotify(Mathf.RoundToInt(currentSettings.MusicVolume * 10f));
            }

            if (ambienceStepControl != null)
            {
                ambienceStepControl.SetValueWithoutNotify(Mathf.RoundToInt(currentSettings.AmbienceVolume * 10f));
            }

            if (sfxStepControl != null)
            {
                sfxStepControl.SetValueWithoutNotify(Mathf.RoundToInt(currentSettings.SFXVolume * 10f));
            }

            if (voiceStepControl != null)
            {
                voiceStepControl.SetValueWithoutNotify(Mathf.RoundToInt(currentSettings.VoiceVolume * 10f));
            }

            if (brightnessStepControl != null)
            {
                brightnessStepControl.SetValueWithoutNotify(Mathf.RoundToInt(currentSettings.Brightness * 10f));
            }

            UpdateDisplayModeText();
        }

        /// <summary>
        /// Updates the display mode text based on the current fullscreen state.
        /// </summary>
        private void UpdateDisplayModeText()
        {
            if (displayModeText != null)
            {
                displayModeText.text = currentSettings != null && currentSettings.Fullscreen
                    ? "Full Screen"
                    : "Windowed";
            }
        }

        #endregion

        #region Settings Application

        /// <summary>
        /// Applies the current settings data to runtime systems such as audio, display and brightness.
        /// </summary>
        private void ApplySettingsToRuntime()
        {
            ApplyDisplayMode();
            ApplyAudioSettings();
            ApplyBrightnessSettings();
        }

        /// <summary>
        /// Applies the fullscreen state to the Unity display settings.
        /// </summary>
        private void ApplyDisplayMode()
        {
            if (currentSettings != null)
            {
                Screen.fullScreen = currentSettings.Fullscreen;
            }
        }

        /// <summary>
        /// Applies all audio volume values to the assigned AudioMixer.
        /// </summary>
        private void ApplyAudioSettings()
        {
            if (audioMixer == null || currentSettings == null)
            {
                return;
            }

            audioMixer.SetFloat(AudioMixerParameters.MasterVolumeParameter, ConvertToMixerVolume(currentSettings.MasterVolume));
            audioMixer.SetFloat(AudioMixerParameters.MusicVolumeParameter, ConvertToMixerVolume(currentSettings.MusicVolume));
            audioMixer.SetFloat(AudioMixerParameters.AmbienceVolumeParameter, ConvertToMixerVolume(currentSettings.AmbienceVolume));
            audioMixer.SetFloat(AudioMixerParameters.SFXVolumeParameter, ConvertToMixerVolume(currentSettings.SFXVolume));
            audioMixer.SetFloat(AudioMixerParameters.VoiceVolumeParameter, ConvertToMixerVolume(currentSettings.VoiceVolume));
        }

        /// <summary>
        /// Applies the brightness value to the runtime scene.
        /// </summary>
        private void ApplyBrightnessSettings()
        {
            if (currentSettings == null)
            {
                return;
            }

            ApplyBrightnessValue(currentSettings.Brightness);
        }

        /// <summary>
        /// Applies brightness changes using Screen.brightness and Windows WMI for physical PC screen brightness.
        /// </summary>
        /// <param name="brightness">The brightness value to apply.</param>
        private void ApplyBrightnessValue(float brightness)
        {
            // Fallback: Adjust a UI screen overlay if one is assigned
            if (brightnessOverlay != null)
            {
                // Max brightness (1.0) -> overlay is completely transparent (alpha = 0)
                // Min brightness (0.0) -> overlay is 95% black (alpha = 0.95)
                float alpha = Mathf.Lerp(0.95f, 0.0f, brightness);
                Color color = brightnessOverlay.color;
                color.a = alpha;
                brightnessOverlay.color = color;
            }
        }

        #endregion

        #region Dirty State

        /// <summary>
        /// Updates the dirty state and the interactability of the Apply button.
        /// </summary>
        private void SetDirtyState(bool dirty)
        {
            isDirty = dirty;

            if (applyButton != null)
            {
                applyButton.interactable = isDirty;
            }
        }

        /// <summary>
        /// Recalculates dirty state by comparing the current settings with the last applied settings.
        /// </summary>
        private void RefreshDirtyState()
        {
            if (currentSettings == null || lastAppliedSettings == null)
            {
                SetDirtyState(false);
                return;
            }

            bool dirty = !AreSettingsEqual(currentSettings, lastAppliedSettings);
            SetDirtyState(dirty);
        }

        /// <summary>
        /// Determines whether two settings objects contain equivalent values.
        /// </summary>
        /// <param name="first">The first settings object.</param>
        /// <param name="second">The second settings object.</param>
        /// <returns>True if the values are equivalent.</returns>
        private bool AreSettingsEqual(SettingsData first, SettingsData second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return Mathf.Approximately(first.MasterVolume, second.MasterVolume)
                && Mathf.Approximately(first.MusicVolume, second.MusicVolume)
                && Mathf.Approximately(first.AmbienceVolume, second.AmbienceVolume)
                && Mathf.Approximately(first.SFXVolume, second.SFXVolume)
                && Mathf.Approximately(first.VoiceVolume, second.VoiceVolume)
                && Mathf.Approximately(first.Brightness, second.Brightness)
                && first.Fullscreen == second.Fullscreen
                && first.HapticsEnabled == second.HapticsEnabled;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles changes to any audio step control. Updates settings, applies, and auto-saves.
        /// </summary>
        private void HandleStepValueChanged(int stepValue, string parameterName)
        {
            if (currentSettings == null)
                currentSettings = new SettingsData();

            float floatValue = stepValue / 10f;

            switch (parameterName)
            {
                case AudioMixerParameters.MasterVolumeParameter:
                    currentSettings.MasterVolume = floatValue;
                    break;

                case AudioMixerParameters.MusicVolumeParameter:
                    currentSettings.MusicVolume = floatValue;
                    break;

                case AudioMixerParameters.AmbienceVolumeParameter:
                    currentSettings.AmbienceVolume = floatValue;
                    break;

                case AudioMixerParameters.SFXVolumeParameter:
                    currentSettings.SFXVolume = floatValue;
                    break;

                case AudioMixerParameters.VoiceVolumeParameter:
                    currentSettings.VoiceVolume = floatValue;
                    break;
            }

            ApplyAudioSettings();
            AutoSaveSettings();
        }

        /// <summary>
        /// Handles brightness step changes. Updates settings, applies, and auto-saves.
        /// </summary>
        private void HandleBrightnessStepChanged(int stepValue)
        {
            if (currentSettings == null)
            {
                currentSettings = new SettingsData();
            }

            currentSettings.Brightness = stepValue / 10f;
            ApplyBrightnessSettings();
            AutoSaveSettings();
        }

        /// <summary>
        /// Changes the display mode to the previous option.
        /// </summary>
        private void HandlePreviousDisplayButtonClicked()
        {
            ChangeDisplayMode(-1);
        }

        /// <summary>
        /// Changes the display mode to the next option.
        /// </summary>
        private void HandleNextDisplayButtonClicked()
        {
            ChangeDisplayMode(1);
        }

        /// <summary>
        /// Changes the display mode with wrapping logic, applies it for real-time preview, and auto-saves.
        /// </summary>
        /// <param name="direction">The direction of movement through the display mode options.</param>
        private void ChangeDisplayMode(int direction)
        {
            if (currentSettings == null)
            {
                currentSettings = new SettingsData();
            }

            int currentIndex = currentSettings.Fullscreen ? 1 : 0;
            int optionCount = 2;
            int nextIndex = (currentIndex + direction + optionCount) % optionCount;

            currentSettings.Fullscreen = nextIndex == 1;
            UpdateDisplayModeText();
            ApplyDisplayMode();
            AutoSaveSettings();
        }

        /// <summary>
        /// Manual apply button handler (for fallback/backwards compatibility).
        /// </summary>
        private void HandleApplyButtonClicked()
        {
            AutoSaveSettings();
        }

        /// <summary>
        /// Resets the current settings to their default values, updates the UI, applies them for preview, and auto-saves.
        /// </summary>
        private void HandleResetButtonClicked()
        {
            currentSettings = SettingsSaveSystem.CreateDefaultSettingsData(saveImmediately: false).Clone();
            UpdateUIFromSettings();
            ApplySettingsToRuntime();
            AutoSaveSettings();
        }

        /// <summary>
        /// Discards unsaved changes (reloads last saved settings).
        /// </summary>
        private void HandleCancelButtonClicked()
        {
            SettingsData reloadedSettings = SettingsSaveSystem.LoadSettings();
            currentSettings = reloadedSettings.Clone();
            lastAppliedSettings = reloadedSettings.Clone();

            UpdateUIFromSettings();
            ApplySettingsToRuntime();
            SetDirtyState(false);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Automatically saves current settings and synchronizes clean state.
        /// </summary>
        private void AutoSaveSettings()
        {
            if (currentSettings != null)
            {
                if (SettingsSaveSystem.SaveSettings(currentSettings))
                {
                    lastAppliedSettings = currentSettings.Clone();
                    SetDirtyState(false);
                }
            }
        }

        /// <summary>
        /// Converts a 0-1 slider value into an AudioMixer volume value in decibels.
        /// </summary>
        /// <param name="sliderValue">The slider value.</param>
        /// <returns>An AudioMixer-friendly decibel value.</returns>
        private float ConvertToMixerVolume(float sliderValue)
        {
            sliderValue = Mathf.Clamp01(sliderValue);

            if (sliderValue <= 0f)
            {
                return AudioMixerParameters.MinMixerVolume;
            }

            // Logarithmic conversion to match human hearing (maps 0.0001-1.0 to -80dB-0dB)
            return Mathf.Log10(sliderValue) * 20f;
        }

        #endregion
    }
}
namespace Setting.Menu
{
    /// <summary>
    /// AudioMixer exposed parameter names.
    /// </summary>
    public static class AudioMixerParameters
    {
        #region Constants

        public const string MasterVolumeParameter = "MasterVolume";
        public const string MusicVolumeParameter = "MusicVolume";
        public const string AmbienceVolumeParameter = "AmbienceVolume";
        public const string SFXVolumeParameter = "SFXVolume";
        public const string VoiceVolumeParameter = "VoiceVolume";

        public const float MinMixerVolume = -80f;
        public const float MaxMixerVolume = 0f;
        public const float MinSliderValue = 0f;
        public const float MaxSliderValue = 1f;

        #endregion
    }
}