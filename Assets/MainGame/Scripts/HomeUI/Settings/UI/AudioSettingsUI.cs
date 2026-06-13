using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// Binds the Audio settings controls (four volume sliders + a Mute All toggle) to
    /// <see cref="SettingsData"/>. Changes apply live through the AudioSettingsManager and save
    /// immediately, so the player hears the effect while dragging a slider.
    /// </summary>
    public class AudioSettingsUI : MonoBehaviour
    {
        [SerializeField] private SettingsManager m_Settings;

        [Header("Sliders (0..1)")]
        [SerializeField] private Slider m_MasterSlider;
        [SerializeField] private Slider m_MusicSlider;
        [SerializeField] private Slider m_SfxSlider;
        [SerializeField] private Slider m_UiSlider;

        [Header("Toggle")]
        [SerializeField] private Toggle m_MuteAllToggle;

        private bool m_Wired;

        private SettingsManager Settings => m_Settings != null ? m_Settings : SettingsManager.Instance;
        private SettingsData D => Settings != null ? Settings.Data : null;

        /// <summary>Syncs the controls to current data. Call when the panel opens.</summary>
        public void Refresh()
        {
            if (Settings == null || D == null) return;
            WireOnce();

            if (m_MasterSlider != null) m_MasterSlider.SetValueWithoutNotify(D.MasterVolume);
            if (m_MusicSlider != null) m_MusicSlider.SetValueWithoutNotify(D.MusicVolume);
            if (m_SfxSlider != null) m_SfxSlider.SetValueWithoutNotify(D.SfxVolume);
            if (m_UiSlider != null) m_UiSlider.SetValueWithoutNotify(D.UiVolume);
            if (m_MuteAllToggle != null) m_MuteAllToggle.SetIsOnWithoutNotify(D.MuteAll);
        }

        private void WireOnce()
        {
            if (m_Wired) return;
            m_Wired = true;

            if (m_MasterSlider != null) m_MasterSlider.onValueChanged.AddListener(v => { D.MasterVolume = v; ApplySave(); });
            if (m_MusicSlider != null) m_MusicSlider.onValueChanged.AddListener(v => { D.MusicVolume = v; ApplySave(); });
            if (m_SfxSlider != null) m_SfxSlider.onValueChanged.AddListener(v => { D.SfxVolume = v; ApplySave(); });
            if (m_UiSlider != null) m_UiSlider.onValueChanged.AddListener(v => { D.UiVolume = v; ApplySave(); });
            if (m_MuteAllToggle != null) m_MuteAllToggle.onValueChanged.AddListener(v => { D.MuteAll = v; ApplySave(); });
        }

        private void ApplySave() => Settings.ApplyAndSave();
    }
}
