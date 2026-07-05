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
        #region Constants

        private const string MasterVolumeParameter = "MasterVolume";
        private const string MusicVolumeParameter = "MusicVolume";
        private const string AmbienceVolumeParameter = "AmbienceVolume";
        private const string SFXVolumeParameter = "SFXVolume";
        private const string VoiceVolumeParameter = "VoiceVolume";

        private const float MinMixerVolume = -80f;
        private const float MaxMixerVolume = 0f;
        private const float MinSliderValue = 0f;
        private const float MaxSliderValue = 1f;

        #endregion

        #region Inspector Fields

        [Header("Audio")]
        [SerializeField] private AudioMixer audioMixer;

        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider ambienceSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider voiceSlider;

        [Header("Visual")]
        [SerializeField] private Slider brightnessSlider;
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
        /// Initializes the manager, registers event handlers, loads settings, updates the UI, and applies the settings.
        /// </summary>
        private void Awake()
        {
            RegisterButtons();
            RegisterSliders();

            SettingsData loadedSettings = SettingsSaveSystem.LoadSettings();
            currentSettings = loadedSettings.Clone();
            lastAppliedSettings = loadedSettings.Clone();

            UpdateUIFromSettings();
            ApplySettingsToRuntime();
            SetDirtyState(false);
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

        #region Slider Registration

        /// <summary>
        /// Registers all slider value change listeners in code.
        /// </summary>
        private void RegisterSliders()
        {
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.AddListener(value => HandleAudioVolumeChanged(value, MasterVolumeParameter));
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.AddListener(value => HandleAudioVolumeChanged(value, MusicVolumeParameter));
            }

            if (ambienceSlider != null)
            {
                ambienceSlider.onValueChanged.AddListener(value => HandleAudioVolumeChanged(value, AmbienceVolumeParameter));
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.AddListener(value => HandleAudioVolumeChanged(value, SFXVolumeParameter));
            }

            if (voiceSlider != null)
            {
                voiceSlider.onValueChanged.AddListener(value => HandleAudioVolumeChanged(value, VoiceVolumeParameter));
            }

            if (brightnessSlider != null)
            {
                brightnessSlider.onValueChanged.AddListener(HandleBrightnessChanged);
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

            if (masterSlider != null)
            {
                masterSlider.SetValueWithoutNotify(currentSettings.MasterVolume);
            }

            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(currentSettings.MusicVolume);
            }

            if (ambienceSlider != null)
            {
                ambienceSlider.SetValueWithoutNotify(currentSettings.AmbienceVolume);
            }

            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(currentSettings.SFXVolume);
            }

            if (voiceSlider != null)
            {
                voiceSlider.SetValueWithoutNotify(currentSettings.VoiceVolume);
            }

            if (brightnessSlider != null)
            {
                brightnessSlider.SetValueWithoutNotify(currentSettings.Brightness);
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

            audioMixer.SetFloat(MasterVolumeParameter, ConvertToMixerVolume(currentSettings.MasterVolume));
            audioMixer.SetFloat(MusicVolumeParameter, ConvertToMixerVolume(currentSettings.MusicVolume));
            audioMixer.SetFloat(AmbienceVolumeParameter, ConvertToMixerVolume(currentSettings.AmbienceVolume));
            audioMixer.SetFloat(SFXVolumeParameter, ConvertToMixerVolume(currentSettings.SFXVolume));
            audioMixer.SetFloat(VoiceVolumeParameter, ConvertToMixerVolume(currentSettings.VoiceVolume));
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
            // Adjust screen brightness on mobile devices
            Screen.brightness = brightness;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                int brightnessInt = Mathf.RoundToInt(brightness * 100f);
                brightnessInt = Mathf.Clamp(brightnessInt, 0, 100);

                // Use WMI via PowerShell to change physical PC screen brightness
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"(Get-WmiObject -Namespace root/WMI -Class WmiMonitorBrightnessMethods).WmiSetBrightness(1, {brightnessInt})\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to apply physical PC screen brightness: {ex.Message}");
            }
#endif

            // Fallback: Adjust a UI screen overlay if one is assigned
            if (brightnessOverlay != null)
            {
                // Max brightness (1.0) -> overlay is completely transparent (alpha = 0)
                // Min brightness (0.0) -> overlay is 80% black (alpha = 0.8)
                float alpha = Mathf.Lerp(0.8f, 0.0f, brightness);
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
                && first.Fullscreen == second.Fullscreen;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles changes to any audio slider. Updates local currentSettings values and applies changes to the mixer for real-time preview.
        /// </summary>
        /// <param name="value">The slider value from 0 to 1.</param>
        /// <param name="parameterName">The AudioMixer parameter to update.</param>
        private void HandleAudioVolumeChanged(float value, string parameterName)
        {
            if (currentSettings == null)
            {
                currentSettings = new SettingsData();
            }

            switch (parameterName)
            {
                case MasterVolumeParameter:
                    currentSettings.MasterVolume = Mathf.Clamp(value, MinSliderValue, MaxSliderValue);
                    break;
                case MusicVolumeParameter:
                    currentSettings.MusicVolume = Mathf.Clamp(value, MinSliderValue, MaxSliderValue);
                    break;
                case AmbienceVolumeParameter:
                    currentSettings.AmbienceVolume = Mathf.Clamp(value, MinSliderValue, MaxSliderValue);
                    break;
                case SFXVolumeParameter:
                    currentSettings.SFXVolume = Mathf.Clamp(value, MinSliderValue, MaxSliderValue);
                    break;
                case VoiceVolumeParameter:
                    currentSettings.VoiceVolume = Mathf.Clamp(value, MinSliderValue, MaxSliderValue);
                    break;
            }

            ApplyAudioSettings();
            RefreshDirtyState();
        }

        /// <summary>
        /// Handles brightness slider changes. Updates local currentSettings values and applies brightness for real-time preview.
        /// </summary>
        /// <param name="value">The new brightness value.</param>
        private void HandleBrightnessChanged(float value)
        {
            if (currentSettings == null)
            {
                currentSettings = new SettingsData();
            }

            currentSettings.Brightness = Mathf.Clamp(value, MinSliderValue, MaxSliderValue);
            ApplyBrightnessSettings();
            RefreshDirtyState();
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
        /// Changes the display mode with wrapping logic, applies it for real-time preview, and marks the settings as dirty.
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
            RefreshDirtyState();
        }

        /// <summary>
        /// Applies the current settings, saves them to disk and disables the Apply button.
        /// </summary>
        private void HandleApplyButtonClicked()
        {
            if (currentSettings == null)
            {
                currentSettings = new SettingsData();
            }

            ApplySettingsToRuntime();

            if (SettingsSaveSystem.SaveSettings(currentSettings))
            {
                lastAppliedSettings = currentSettings.Clone();
                SetDirtyState(false);
            }
        }

        /// <summary>
        /// Resets the current settings to their default values, updates the UI, applies them for preview, and marks the settings as dirty.
        /// </summary>
        private void HandleResetButtonClicked()
        {
            currentSettings = SettingsSaveSystem.CreateDefaultSettingsData(saveImmediately: false).Clone();
            UpdateUIFromSettings();
            ApplySettingsToRuntime();
            RefreshDirtyState();
        }

        /// <summary>
        /// Discards unsaved changes, reloads the last saved settings, updates the UI and resets the runtime parameters.
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
        /// Converts a 0-1 slider value into an AudioMixer volume value in decibels.
        /// </summary>
        /// <param name="sliderValue">The slider value.</param>
        /// <returns>An AudioMixer-friendly decibel value.</returns>
        private float ConvertToMixerVolume(float sliderValue)
        {
            float clampedValue = Mathf.Clamp(sliderValue, MinSliderValue, MaxSliderValue);

            if (clampedValue <= MinSliderValue)
            {
                return MinMixerVolume;
            }

            return Mathf.Lerp(MinMixerVolume, MaxMixerVolume, Mathf.InverseLerp(MinSliderValue, MaxSliderValue, clampedValue));
        }

        #endregion
    }
}
