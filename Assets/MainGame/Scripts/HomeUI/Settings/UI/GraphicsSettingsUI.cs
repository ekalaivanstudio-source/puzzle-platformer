using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// Binds the Graphics settings controls to <see cref="SettingsData"/>. Reading happens in
    /// <see cref="Refresh"/> (control values ← data); writing happens in the onValueChanged
    /// handlers (data ← control, then apply+save). Dropdown contents are populated from the
    /// <see cref="GraphicsManager"/>, so the resolution list reflects the player's real hardware.
    /// </summary>
    public class GraphicsSettingsUI : MonoBehaviour
    {
        [SerializeField] private SettingsManager m_Settings;

        [Header("Controls")]
        [SerializeField] private TMP_Dropdown m_ResolutionDropdown;
        [SerializeField] private TMP_Dropdown m_DisplayModeDropdown;
        [SerializeField] private Toggle m_VSyncToggle;
        [SerializeField] private TMP_Dropdown m_FpsDropdown;
        [SerializeField] private TMP_Dropdown m_QualityDropdown;
        [SerializeField] private Slider m_GammaSlider;
        [SerializeField] private TMP_Dropdown m_TextureDropdown;
        [SerializeField] private TMP_Dropdown m_AntiAliasingDropdown;

        // Fixed option value tables (labels are data; the indices map to these).
        private static readonly int[] FpsValues = { 30, 60, 120, -1 };
        private static readonly string[] FpsLabels = { "30", "60", "120", "Unlimited" };
        private static readonly int[] AaValues = { 0, 2, 4, 8 };
        private static readonly string[] AaLabels = { "Off", "2x", "4x", "8x" };
        private static readonly string[] DisplayLabels = { "Fullscreen", "Windowed", "Borderless" };
        private static readonly string[] TextureLabels = { "Ultra", "High", "Medium", "Low" };

        private List<Resolution> m_Resolutions;
        private bool m_Wired;

        private SettingsManager Settings => m_Settings != null ? m_Settings : SettingsManager.Instance;
        private SettingsData D => Settings != null ? Settings.Data : null;

        /// <summary>Populates options and syncs every control to the current data. Call when the panel opens.</summary>
        public void Refresh()
        {
            if (Settings == null || D == null) return;

            PopulateOptions();
            WireOnce();

            // Resolution → find matching entry.
            int resIndex = FindResolutionIndex(D.ResolutionWidth, D.ResolutionHeight);
            SetDropdown(m_ResolutionDropdown, resIndex);

            SetDropdown(m_DisplayModeDropdown, (int)D.DisplayMode);
            SetToggle(m_VSyncToggle, D.VSync);
            SetDropdown(m_FpsDropdown, IndexOf(FpsValues, D.FpsLimit, 1));
            SetDropdown(m_QualityDropdown, D.QualityPreset);
            SetSlider(m_GammaSlider, D.Gamma);
            SetDropdown(m_TextureDropdown, (int)D.TextureQuality);
            SetDropdown(m_AntiAliasingDropdown, IndexOf(AaValues, D.AntiAliasing, 1));
        }

        private void PopulateOptions()
        {
            GraphicsManager gfx = Settings.Graphics;

            if (m_ResolutionDropdown != null)
            {
                m_Resolutions = gfx != null ? new List<Resolution>(gfx.SupportedResolutions) : new List<Resolution>();
                var labels = new List<string>(m_Resolutions.Count);
                foreach (Resolution r in m_Resolutions) labels.Add($"{r.width} x {r.height}");
                SetOptions(m_ResolutionDropdown, labels);
            }

            SetOptions(m_DisplayModeDropdown, new List<string>(DisplayLabels));
            SetOptions(m_FpsDropdown, new List<string>(FpsLabels));
            SetOptions(m_TextureDropdown, new List<string>(TextureLabels));
            SetOptions(m_AntiAliasingDropdown, new List<string>(AaLabels));

            if (m_QualityDropdown != null && gfx != null)
                SetOptions(m_QualityDropdown, new List<string>(gfx.QualityPresetNames));
        }

        private void WireOnce()
        {
            if (m_Wired) return;
            m_Wired = true;

            if (m_ResolutionDropdown != null) m_ResolutionDropdown.onValueChanged.AddListener(OnResolution);
            if (m_DisplayModeDropdown != null) m_DisplayModeDropdown.onValueChanged.AddListener(i => { D.DisplayMode = (DisplayMode)i; ApplySave(); });
            if (m_VSyncToggle != null) m_VSyncToggle.onValueChanged.AddListener(v => { D.VSync = v; ApplySave(); });
            if (m_FpsDropdown != null) m_FpsDropdown.onValueChanged.AddListener(i => { D.FpsLimit = FpsValues[Mathf.Clamp(i, 0, FpsValues.Length - 1)]; ApplySave(); });
            if (m_QualityDropdown != null) m_QualityDropdown.onValueChanged.AddListener(i => { D.QualityPreset = i; ApplySave(); });
            if (m_GammaSlider != null) m_GammaSlider.onValueChanged.AddListener(v => { D.Gamma = v; ApplySave(); });
            if (m_TextureDropdown != null) m_TextureDropdown.onValueChanged.AddListener(i => { D.TextureQuality = (TextureQuality)i; ApplySave(); });
            if (m_AntiAliasingDropdown != null) m_AntiAliasingDropdown.onValueChanged.AddListener(i => { D.AntiAliasing = AaValues[Mathf.Clamp(i, 0, AaValues.Length - 1)]; ApplySave(); });
        }

        private void OnResolution(int index)
        {
            if (m_Resolutions == null || index < 0 || index >= m_Resolutions.Count) return;
            Resolution r = m_Resolutions[index];
            D.ResolutionWidth = r.width;
            D.ResolutionHeight = r.height;
            D.ResolutionRefreshRate = Mathf.RoundToInt((float)r.refreshRateRatio.value);
            ApplySave();
        }

        private void ApplySave() => Settings.ApplyAndSave();

        // ─── Small helpers (notify-free setters keep Refresh from re-triggering writes) ──

        private int FindResolutionIndex(int w, int h)
        {
            if (m_Resolutions == null) return 0;
            for (int i = 0; i < m_Resolutions.Count; i++)
                if (m_Resolutions[i].width == w && m_Resolutions[i].height == h) return i;
            return 0;
        }

        private static int IndexOf(int[] values, int value, int fallback)
        {
            for (int i = 0; i < values.Length; i++) if (values[i] == value) return i;
            return fallback;
        }

        private static void SetOptions(TMP_Dropdown d, List<string> opts)
        {
            if (d == null) return;
            d.ClearOptions();
            d.AddOptions(opts);
        }

        private static void SetDropdown(TMP_Dropdown d, int value)
        {
            if (d != null) d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, d.options.Count - 1)));
        }

        private static void SetToggle(Toggle t, bool v) { if (t != null) t.SetIsOnWithoutNotify(v); }
        private static void SetSlider(Slider s, float v) { if (s != null) s.SetValueWithoutNotify(v); }
    }
}
