using System.Collections.Generic;
using UnityEngine;

namespace HomeUI
{
    /// <summary>
    /// Applies all graphics settings to the engine. It is the ONLY place that calls Screen.*,
    /// QualitySettings.* and Application.targetFrameRate, so the rest of the game never touches
    /// raw graphics APIs.
    ///
    /// It also exposes the lists the UI needs to populate its dropdowns (supported resolutions
    /// detected from the user's hardware, quality preset names), keeping the UI data-driven.
    /// </summary>
    public class GraphicsManager : MonoBehaviour, ISettingsModule
    {
        [Tooltip("Optional: drives screen brightness via a fullscreen overlay's alpha (gamma is " +
                 "not directly settable on most platforms). Assign a black, full-screen UI Image " +
                 "with raycast disabled. Leave null to skip brightness.")]
        [SerializeField] private CanvasGroup m_BrightnessOverlay;

        private List<Resolution> m_Resolutions;

        /// <summary>Distinct resolutions reported by the player's display, highest first.</summary>
        public IReadOnlyList<Resolution> SupportedResolutions
        {
            get { if (m_Resolutions == null) BuildResolutionList(); return m_Resolutions; }
        }

        /// <summary>Quality preset names defined in Project Settings → Quality (drives the dropdown).</summary>
        public string[] QualityPresetNames => QualitySettings.names;

        public void Apply(SettingsData data)
        {
            ApplyResolutionAndMode(data);
            ApplyVSync(data);
            ApplyFpsLimit(data);
            ApplyQuality(data);
            ApplyTextureQuality(data);
            ApplyAntiAliasing(data);
            ApplyBrightness(data);
        }

        // ─── Individual appliers (also callable live from the UI for instant preview) ──

        public void ApplyResolutionAndMode(SettingsData data)
        {
            FullScreenMode mode = data.DisplayMode switch
            {
                DisplayMode.Fullscreen => FullScreenMode.ExclusiveFullScreen,
                DisplayMode.Borderless => FullScreenMode.FullScreenWindow,
                _                      => FullScreenMode.Windowed,
            };

            var refresh = new RefreshRate { numerator = (uint)Mathf.Max(1, data.ResolutionRefreshRate), denominator = 1 };
            Screen.SetResolution(data.ResolutionWidth, data.ResolutionHeight, mode, refresh);
        }

        public void ApplyVSync(SettingsData data) => QualitySettings.vSyncCount = data.VSync ? 1 : 0;

        public void ApplyFpsLimit(SettingsData data)
        {
            // With VSync on, targetFrameRate is ignored by Unity, which is expected.
            Application.targetFrameRate = data.FpsLimit <= 0 ? -1 : data.FpsLimit;
        }

        public void ApplyQuality(SettingsData data)
        {
            int clamped = Mathf.Clamp(data.QualityPreset, 0, QualitySettings.names.Length - 1);
            // applyExpensiveChanges:false — we set resolution/AA/textures ourselves below.
            QualitySettings.SetQualityLevel(clamped, applyExpensiveChanges: false);
        }

        public void ApplyTextureQuality(SettingsData data) =>
            QualitySettings.globalTextureMipmapLimit = (int)data.TextureQuality;

        public void ApplyAntiAliasing(SettingsData data) => QualitySettings.antiAliasing = data.AntiAliasing;

        public void ApplyBrightness(SettingsData data)
        {
            if (m_BrightnessOverlay == null) return;
            // Most platforms can't set hardware gamma, so we darken via a black overlay:
            // gamma >= 1 → fully clear; gamma 1.0..0.5 → overlay ramps up to 60% black.
            float g = Mathf.Clamp(data.Gamma, 0.5f, 2f);
            m_BrightnessOverlay.alpha = g >= 1f ? 0f : Mathf.InverseLerp(1f, 0.5f, g) * 0.6f;
        }

        private void BuildResolutionList()
        {
            // Distinct by width×height (ignore refresh-rate duplicates), highest resolution first.
            m_Resolutions = new List<Resolution>();
            var seen = new HashSet<long>();
            Resolution[] all = Screen.resolutions;
            for (int i = all.Length - 1; i >= 0; i--)
            {
                long key = ((long)all[i].width << 32) | (uint)all[i].height;
                if (seen.Add(key)) m_Resolutions.Add(all[i]);
            }

            // Guarantee the spec'd resolutions are offered even if the editor reports few.
            EnsureResolution(3840, 2160);
            EnsureResolution(2560, 1440);
            EnsureResolution(1920, 1080);
        }

        private void EnsureResolution(int w, int h)
        {
            foreach (Resolution r in m_Resolutions)
                if (r.width == w && r.height == h) return;
            m_Resolutions.Add(new Resolution { width = w, height = h,
                refreshRateRatio = new RefreshRate { numerator = 60, denominator = 1 } });
        }
    }
}
