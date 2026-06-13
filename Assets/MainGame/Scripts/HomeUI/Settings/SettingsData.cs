using System;

namespace HomeUI
{
    /// <summary>How the game window is presented.</summary>
    public enum DisplayMode { Fullscreen = 0, Windowed = 1, Borderless = 2 }

    /// <summary>Texture detail level, mapped to QualitySettings mipmap limit (0 = full res).</summary>
    public enum TextureQuality { Ultra = 0, High = 1, Medium = 2, Low = 3 }

    /// <summary>
    /// The complete, serializable bag of player settings — the single data object that is saved,
    /// loaded, and handed to each category manager to apply. UI reads/writes these fields; the
    /// managers translate them into engine calls.
    ///
    /// Adding a new setting = add a field here + read it in the relevant manager. Nothing else in
    /// the pipeline changes (that is the open/closed design: new data + new behaviour, no edits to
    /// the coordinator or save layer).
    ///
    /// Defaults below are conservative; designers can override them per project via
    /// <see cref="SettingsDefaults"/>.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        // ─── Graphics ──────────────────────────────────────────────
        public int ResolutionWidth = 1920;
        public int ResolutionHeight = 1080;
        public int ResolutionRefreshRate = 60;
        public DisplayMode DisplayMode = DisplayMode.Fullscreen;
        public bool VSync = true;
        /// <summary>Target frame rate; -1 means unlimited.</summary>
        public int FpsLimit = 60;
        /// <summary>Index into QualitySettings preset names (0 = lowest). Designer-defined order.</summary>
        public int QualityPreset = 2;
        /// <summary>Brightness / gamma multiplier, ~0.5..2.0 (1 = neutral).</summary>
        public float Gamma = 1f;
        public TextureQuality TextureQuality = TextureQuality.High;
        /// <summary>MSAA samples: 0, 2, 4 or 8.</summary>
        public int AntiAliasing = 2;

        // ─── Audio ─────────────────────────────────────────────────
        public float MasterVolume = 1f;
        public float MusicVolume = 0.6f;
        public float SfxVolume = 1f;
        public float UiVolume = 1f;
        public bool MuteAll = false;

        // ─── Controls ──────────────────────────────────────────────
        public float MouseSensitivity = 1f;
        public bool ControllerVibration = true;
        /// <summary>Opaque rebinding overrides produced by InputActionAsset.SaveBindingOverridesAsJson().</summary>
        public string InputBindingOverridesJson = "";

        /// <summary>Returns a field-by-field copy (used so the defaults asset is never mutated).</summary>
        public SettingsData Clone() => (SettingsData)MemberwiseClone();
    }
}
