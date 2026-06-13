using UnityEngine;
using UnityEngine.UI;

namespace HomeUI
{
    /// <summary>
    /// Binds the Controls settings: mouse sensitivity, controller vibration, the per-binding
    /// rebind rows, and a Reset Controls button (which routes through the reusable confirmation
    /// popup before clearing rebinds).
    /// </summary>
    public class ControlsSettingsUI : MonoBehaviour
    {
        [SerializeField] private SettingsManager m_Settings;

        [Header("Controls")]
        [SerializeField] private Slider m_SensitivitySlider;
        [SerializeField] private Toggle m_VibrationToggle;
        [SerializeField] private Button m_ResetButton;

        [Header("Rebind Rows")]
        [Tooltip("All rebind rows under this panel. Found automatically if left empty.")]
        [SerializeField] private RebindButtonUI[] m_RebindRows;

        [Header("Reset Confirmation (optional)")]
        [SerializeField] private ConfirmationPopup m_ConfirmationPopup;

        private bool m_Wired;

        private SettingsManager Settings => m_Settings != null ? m_Settings : SettingsManager.Instance;
        private SettingsData D => Settings != null ? Settings.Data : null;

        private void Awake()
        {
            if (m_RebindRows == null || m_RebindRows.Length == 0)
                m_RebindRows = GetComponentsInChildren<RebindButtonUI>(includeInactive: true);
        }

        /// <summary>Syncs the controls to current data. Call when the panel opens.</summary>
        public void Refresh()
        {
            if (Settings == null || D == null) return;
            WireOnce();

            if (m_SensitivitySlider != null) m_SensitivitySlider.SetValueWithoutNotify(D.MouseSensitivity);
            if (m_VibrationToggle != null) m_VibrationToggle.SetIsOnWithoutNotify(D.ControllerVibration);
            RefreshRebinds();
        }

        private void WireOnce()
        {
            if (m_Wired) return;
            m_Wired = true;

            if (m_SensitivitySlider != null)
                m_SensitivitySlider.onValueChanged.AddListener(v => { D.MouseSensitivity = v; Settings.ApplyAndSave(); });
            if (m_VibrationToggle != null)
                m_VibrationToggle.onValueChanged.AddListener(v =>
                {
                    D.ControllerVibration = v;
                    Settings.ApplyAndSave();
                    if (v) Settings.Input?.Rumble(); // quick confirmation buzz
                });
            if (m_ResetButton != null)
                m_ResetButton.onClick.AddListener(OnResetClicked);
        }

        private void OnResetClicked()
        {
            AudioManager.Instance?.PlayButton();

            if (m_ConfirmationPopup != null)
                m_ConfirmationPopup.Show("Reset Controls",
                    "Restore all control mappings to their defaults?", onYes: DoReset);
            else
                DoReset();
        }

        private void DoReset()
        {
            Settings.Input?.ResetBindings();
            RefreshRebinds();
        }

        private void RefreshRebinds()
        {
            if (m_RebindRows == null) return;
            foreach (RebindButtonUI row in m_RebindRows)
                if (row != null) row.Refresh();
        }
    }
}
